using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// A string tree stores a bunch of strings in a way that makes it fast to check if a test string starts with ANY of the contained strings.
    /// Empty strings are not supported, they can't be added and can't be searched for (throws in debug builds).
    /// </summary>
    /// <remarks>
    /// The tree is stored as a flat array of nodes (a compressed trie):
    /// - The children of a node are stored after each other, sorted by their first char (the first chars are stored in <see cref="Keys"/>).
    /// - A chain of nodes that have a single child and no string is compressed into a label (the chars after the first char), that is compared as a span.
    /// - Nodes with a lot of children have a lookup table (indexed by the char).
    /// - StartsWithAny rejects texts that are shorter than the shortest string, or starts with an ASCII char (or a pair of ASCII chars) that can't match using bit masks, before walking the tree.
    /// - Small trees (at most 32 strings of at least 4 chars, the typical use case) doesn't walk the tree in StartsWithAny, the first 4 or 8 chars are looked up in a collision free hash table instead,
    ///   followed by a compare of the strings that starts with them. Case in-sensitive trees use the tree if the text isn't ASCII.
    /// For case in-sensitive trees all chars (keys and labels) are upper cased (using CharExt.FastToUpper).
    /// </remarks>
    public sealed class FrozenStringTree : IStringTree
    {

#if DEBUG
        public override string ToString() => "Nodes: " + Nodes.Length + ", strings: " + Nodes.Count(x => x.Leaf != null);
#endif//DEBUG

        /// <summary>
        /// True if the tree is case in-sensitive
        /// </summary>
        public bool IsCaseInSensitive => CaseInSensitive;

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null or empty strings</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static FrozenStringTree Build(IEnumerable<String> strings, bool caseInSensitive = false)
            => new FrozenStringTree(StringTree.Build(strings, caseInSensitive));


        /// <summary>
        /// Find the longest string (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>The longest found match or null if no match is found</returns>
        public String StartsWithAny(String text, int start = 0)
        {
            StringTree.ValidateSearch(text, start);
            var len = text.Length;
            if ((uint)start < (uint)len)
            {
                // No string can match a text that is shorter than the shortest string
                if ((len - start) < MinLength)
                    return null;
                // Quick reject of ASCII chars that no string starts with
                var c = text[start];
                if (c < 128)
                {
                    var m = c < 64 ? FirstAscii0 : FirstAscii1;
                    if (((m >> c) & 1) == 0)
                        return null;
                }
                var keyChars = FastKeyChars;
                if (keyChars != 0)
                {
                    // Small trees: look up the first 4 or 8 chars (the text have at least that many chars, since it's not shorter than the shortest string)
                    ref var b = ref Unsafe.As<Char, Byte>(ref Unsafe.Add(ref Unsafe.AsRef(in text.GetPinnableReference()), start));
                    var k0 = Unsafe.ReadUnaligned<UInt64>(ref b);
                    var k1 = keyChars == 8 ? Unsafe.ReadUnaligned<UInt64>(ref Unsafe.Add(ref b, 8)) : 0;
                    if (!CaseInSensitive)
                        return FastStartsWithAny<ExactChars>(text, start, k0, k1);
                    // Case in-sensitive: ASCII text uses a loose key (a matching key, but not all matching keys are a match)
                    if (((k0 | k1) & NonAsciiChars) == 0)
                        return FastStartsWithAny<UpperChars>(text, start, k0 | LooseCase, k1 | FastLooseKey1);
                }
                // Quick reject of pairs of ASCII chars that no string starts with
                if (c < 128)
                {
                    var n = start + 1;
                    if (n < len)
                    {
                        var c1 = text[n];
                        if ((c1 < 128) && (((Unsafe.Add(ref Unsafe.As<PairBits, UInt64>(ref Unsafe.AsRef(in Pairs)), (c << 1) | (c1 >> 6)) >> c1) & 1) == 0))
                            return null;
                    }
                }
            }
            return CaseInSensitive ? StartsWithAny<UpperChars>(text, start) : StartsWithAny<ExactChars>(text, start);
        }

        /// <summary>
        /// Find the longest match using the small tree lookup
        /// </summary>
        /// <param name="text">The text</param>
        /// <param name="start">The start offset</param>
        /// <param name="k0">The first 4 chars of the text (folded)</param>
        /// <param name="k1">The next 4 chars of the text (folded) if the key is 8 chars, else 0</param>
        /// <returns>The longest match or null</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        String FastStartsWithAny<T>(String text, int start, UInt64 k0, UInt64 k1) where T : struct, IChars
        {
            // Slot 0 is an empty group (so a missing key doesn't need a separate check)
            var slot = Unsafe.Add(ref Unsafe.As<FastSlotBytes, Byte>(ref Unsafe.AsRef(in FastSlots)), FastHash(k0, k1, FastM0, FastM1));
            ref var g = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(FastGroups), slot);
            if (((g.Key0 ^ k0) | (g.Key1 ^ k1)) != 0)
                return null;
            return FastMatch<T>(ref g, text, start);
        }

        /// <summary>
        /// Find the longest string in a group (all strings in the group have the same key) that matches the text
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        String FastMatch<T>(ref FastGroup g, String text, int start) where T : struct, IChars
        {
            var remaining = text.Length - start;
            ref var t = ref Unsafe.Add(ref Unsafe.AsRef(in text.GetPinnableReference()), start);
            ref var candidates = ref MemoryMarshal.GetArrayDataReference(FastCandidates);
            ref var chars = ref MemoryMarshal.GetArrayDataReference(FastChars);
            var e = g.Start + g.Count;
            for (int i = g.Start; i < e; ++i)
            {
                // Longest first
                ref var x = ref Unsafe.Add(ref candidates, i);
                if (x.Length > remaining)
                    continue;
                var l = x.CompareLength;
                if ((l == 0) || default(T).Equals(ref Unsafe.Add(ref t, x.CompareFrom), ref Unsafe.Add(ref chars, x.CompareStart), l))
                    return x.Value;
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        String StartsWithAny<T>(String text, int start) where T : struct, IChars
        {
            int len = text.Length;
            if ((uint)start >= (uint)len)
            {
                if (start < 0)
                    _ = text[start];
                return null;
            }
            ref Node nodes = ref MemoryMarshal.GetArrayDataReference(Nodes);
            ref Char keys = ref MemoryMarshal.GetArrayDataReference(Keys);
            ref Byte table = ref MemoryMarshal.GetArrayDataReference(Table);
            ref Char labels = ref MemoryMarshal.GetArrayDataReference(Labels);
            ref Char t = ref Unsafe.AsRef(in text.GetPinnableReference());
            String found = null;
            ref Node node = ref nodes;
            for (; ; )
            {
                // Find the child
                int count = node.ChildCount;
                if (count == 0)
                    break;
                var c = default(T).Fold(Unsafe.Add(ref t, start));
                int child = node.ChildStart;
                if (count == 1)
                {
                    if (Unsafe.Add(ref keys, child) != c)
                        break;
                }
                else
                {
                    var i = FindChild(ref node, ref keys, ref table, c);
                    if (i < 0)
                        break;
                    child += i;
                }
                ++start;
                node = ref Unsafe.Add(ref nodes, child);
                // Match the label
                int ll = node.LabelLength;
                if (ll > 0)
                {
                    if ((len - start) < ll)
                        break;
                    if (!default(T).Equals(ref Unsafe.Add(ref t, start), ref Unsafe.Add(ref labels, node.LabelStart), ll))
                        break;
                    start += ll;
                }
                var leaf = node.Leaf;
                if (leaf != null)
                    found = leaf;
                if (start >= len)
                    break;
            }
            return found;
        }

        /// <summary>
        /// Find all matching strings (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>A list of matches, orderer from shortest match to longest match</returns>
        public List<String> AllStartsWithAny(String text, int start = 0)
        {
            StringTree.ValidateSearch(text, start);
            List<String> found = new List<string>();
            var nodes = Nodes;
            int len = text.Length;
            int ni = 0;
            while (start < len)
            {
                var child = FindChild(ni, text[start]);
                if (child < 0)
                    break;
                ++start;
                ref var node = ref nodes[child];
                int ll = node.LabelLength;
                if (ll > 0)
                {
                    if (!MatchLabel(ref node, text, start))
                    {
                        // Stopped inside the label, add the child and everything below it
                        if (node.Leaf != null)
                            found.Add(node.Leaf);
                        InternalAddAllInOrder(found, child);
                        return found;
                    }
                    start += ll;
                }
                ni = child;
                var val = node.Leaf;
                if (val != null)
                    found.Add(val);
            }
            InternalAddAllInOrder(found, ni);
            return found;
        }

        /// <summary>
        /// Find all strings (in the tree), that is a prefix of the text
        /// </summary>
        /// <param name="text">The text to find prefixes (in the tree) for, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>A list of matches, ordered by name</returns>
        public List<String> PrefixesOf(String text, int start = 0)
        {
            StringTree.ValidateSearch(text, start);
            List<String> found = new();
            var nodes = Nodes;
            int len = text.Length;
            int ni = 0;
            while (start < len)
            {
                var child = FindChild(ni, text[start]);
                if (child < 0)
                    break;
                ++start;
                ref var node = ref nodes[child];
                int ll = node.LabelLength;
                if (ll > 0)
                {
                    if (!MatchLabel(ref node, text, start))
                        break;
                    start += ll;
                }
                ni = child;
                var val = node.Leaf;
                if (val != null)
                    found.Add(val);
            }
            return found;
        }

        /// <summary>
        /// Get all string contained in the string tree, in any order
        /// </summary>
        /// <returns></returns>
        public IEnumerable<String> GetAll()
        {
            var nodes = Nodes;
            for (int i = 1; i < nodes.Length; ++i)
            {
                var val = nodes[i].Leaf;
                if (val != null)
                    yield return val;
            }
        }

        /// <summary>
        /// Get all string contained in the string tree, ordered by key
        /// </summary>
        /// <returns></returns>
        public IEnumerable<String> GetAllInOrder()
        {
            List<String> found = new();
            InternalAddAllInOrder(found, 0);
            return found;
        }

        /// <summary>
        /// Get all string contained in the string tree, ordered by key
        /// </summary>
        /// <returns></returns>
        public IEnumerable<String> GetAllInReverseOrder()
        {
            List<String> found = new();
            InternalAddAllInOrder(found, 0);
            found.Reverse();
            return found;
        }


        #region Implementation

        struct Node
        {
            /// <summary>
            /// The string that ends at this node (or null)
            /// </summary>
            public String Leaf;
            /// <summary>
            /// The index of the first child (in Nodes and Keys), the children are sorted by key
            /// </summary>
            public int ChildStart;
            /// <summary>
            /// The number of children
            /// </summary>
            public int ChildCount;
            /// <summary>
            /// The index of the label (the chars after the key) in Labels
            /// </summary>
            public int LabelStart;
            /// <summary>
            /// The number of chars in the label
            /// </summary>
            public int LabelLength;
            /// <summary>
            /// The index in Table of the lookup table (or -1 if there is no table), the table is indexed by the key - TableMin
            /// </summary>
            public int TableStart;
            /// <summary>
            /// The number of entries in the lookup table
            /// </summary>
            public UInt16 TableLength;
            /// <summary>
            /// The key of the first entry in the lookup table
            /// </summary>
            public Char TableMin;
        }

        /// <summary>
        /// Use a lookup table if a node have at least this many children
        /// </summary>
        const int TableMinChildren = 3;

        /// <summary>
        /// Max number of entries in the lookup table (per child), a table of TableMinEntries is always ok
        /// </summary>
        const int TableMaxEntriesPerChild = 8;

        /// <summary>
        /// A table with this many entries is always ok (if the node have at least TableMinChildren)
        /// </summary>
        const int TableMinEntries = 64;

        /// <summary>
        /// Nodes with at most this many children (and no lookup table) are searched using a linear scan, else IndexOf (vectorized) is used
        /// </summary>
        const int MaxLinearScan = 8;

        /// <summary>
        /// The nodes, the root is at index 0
        /// </summary>
        readonly Node[] Nodes;

        /// <summary>
        /// The first char (key) of every node (the root key is unused)
        /// </summary>
        readonly Char[] Keys;

        /// <summary>
        /// All labels
        /// </summary>
        readonly Char[] Labels;

        /// <summary>
        /// All lookup tables, an entry is the index of the child + 1 (0 = not found)
        /// </summary>
        readonly Byte[] Table;

        readonly bool CaseInSensitive;

        /// <summary>
        /// A bit for every ASCII char (0 - 63 and 64 - 127) that a string can start with (both cases for case in-sensitive trees)
        /// </summary>
        readonly UInt64 FirstAscii0, FirstAscii1;

        /// <summary>
        /// A bit for every pair of ASCII chars (first * 128 + second) that can be the start of a match (a string starts with the pair, or the first char is a string)
        /// </summary>
        readonly PairBits Pairs;

        [InlineArray(256)]
        struct PairBits
        {
            UInt64 Bits;
        }

        /// <summary>
        /// The length of the shortest string
        /// </summary>
        readonly int MinLength;

        /// <summary>
        /// The number of chars (4 or 8) used as the key for small trees, 0 if the small tree lookup isn't used
        /// </summary>
        readonly int FastKeyChars;

        /// <summary>
        /// LooseCase if the key is 8 chars and the tree is case in-sensitive, else 0
        /// </summary>
        readonly UInt64 FastLooseKey1;

        /// <summary>
        /// The hash multipliers for the key, selected so that the groups doesn't collide
        /// </summary>
        readonly UInt64 FastM0, FastM1;

        /// <summary>
        /// The group index for every hash value (top 8 bits of the hash), group 0 is empty (no strings)
        /// </summary>
        readonly FastSlotBytes FastSlots;

        /// <summary>
        /// The groups (strings that have the same key), the first group is empty
        /// </summary>
        readonly FastGroup[] FastGroups;

        /// <summary>
        /// The strings of all groups, ordered by group and then by length (longest first)
        /// </summary>
        readonly FastCandidate[] FastCandidates;

        /// <summary>
        /// The chars to compare of all strings (folded)
        /// </summary>
        readonly Char[] FastChars;

        [InlineArray(256)]
        struct FastSlotBytes
        {
            Byte Slot;
        }

        struct FastGroup
        {
            /// <summary>
            /// The key (the first 4 chars, and the next 4 chars if the key is 8 chars), the loose key of the folded chars for case in-sensitive trees
            /// </summary>
            public UInt64 Key0, Key1;
            /// <summary>
            /// The index of the first string in FastCandidates
            /// </summary>
            public int Start;
            /// <summary>
            /// The number of strings
            /// </summary>
            public int Count;
        }

        struct FastCandidate
        {
            /// <summary>
            /// The string
            /// </summary>
            public String Value;
            /// <summary>
            /// The length of the string
            /// </summary>
            public int Length;
            /// <summary>
            /// The index of the first char to compare (in the string)
            /// </summary>
            public int CompareFrom;
            /// <summary>
            /// The number of chars to compare
            /// </summary>
            public int CompareLength;
            /// <summary>
            /// The index in FastChars of the chars to compare
            /// </summary>
            public int CompareStart;
        }

        /// <summary>
        /// Use the small tree lookup if there is at most this many strings (and the shortest string is at least 4 chars)
        /// </summary>
        const int MaxFastStrings = 32;

        /// <summary>
        /// Bits that are set in 4 chars if any of them isn't ASCII
        /// </summary>
        const UInt64 NonAsciiChars = 0xff80ff80ff80ff80UL;

        /// <summary>
        /// Sets bit 5 (0x20) of 4 chars, an ASCII char (of the text) and FastToUpper of it always have the same loose value (but other chars can also have the same value, like '@' and '`')
        /// </summary>
        const UInt64 LooseCase = 0x0020002000200020UL;

        /// <summary>
        /// Create a key from the first 4 chars (as they are read from memory)
        /// </summary>
        static UInt64 Key(ReadOnlySpan<Char> s) => MemoryMarshal.Read<UInt64>(MemoryMarshal.AsBytes(s.Slice(0, 4)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int FastHash(UInt64 k0, UInt64 k1, UInt64 m0, UInt64 m1) => (int)(((k0 * m0) + (k1 * m1)) >> 56);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int FindChild(ref Node node, ref Char keys, ref Byte table, Char c)
        {
            if (node.TableStart >= 0)
            {
                var o = (uint)(c - node.TableMin);
                if (o >= (uint)node.TableLength)
                    return -1;
                return Unsafe.Add(ref table, node.TableStart + (int)o) - 1;
            }
            ref var k = ref Unsafe.Add(ref keys, node.ChildStart);
            var count = node.ChildCount;
            if (count <= MaxLinearScan)
            {
                // The keys are sorted
                for (int i = 0; i < count; ++i)
                {
                    var x = Unsafe.Add(ref k, i);
                    if (x >= c)
                        return x == c ? i : -1;
                }
                return -1;
            }
            return MemoryMarshal.CreateReadOnlySpan(ref k, count).IndexOf(c);
        }

        /// <summary>
        /// Find a child
        /// </summary>
        /// <param name="ni">The node index</param>
        /// <param name="c">The char (not folded)</param>
        /// <returns>The node index of the child or -1 if not found</returns>
        int FindChild(int ni, Char c)
        {
            ref var node = ref Nodes[ni];
            if (node.ChildCount == 0)
                return -1;
            if (CaseInSensitive)
                c = default(UpperChars).Fold(c);
            var i = FindChild(ref node, ref MemoryMarshal.GetArrayDataReference(Keys), ref MemoryMarshal.GetArrayDataReference(Table), c);
            return i < 0 ? -1 : node.ChildStart + i;
        }

        /// <summary>
        /// Check if the label of a node matches the text
        /// </summary>
        bool MatchLabel(ref Node node, String text, int start)
        {
            int ll = node.LabelLength;
            if ((text.Length - start) < ll)
                return false;
            ref var t = ref Unsafe.Add(ref Unsafe.AsRef(in text.GetPinnableReference()), start);
            ref var l = ref Labels[node.LabelStart];
            return CaseInSensitive ? default(UpperChars).Equals(ref t, ref l, ll) : default(ExactChars).Equals(ref t, ref l, ll);
        }

        /// <summary>
        /// How chars in the text are compared with the chars in the tree
        /// </summary>
        interface IChars
        {
            /// <summary>
            /// Convert a char in the text to a key
            /// </summary>
            Char Fold(Char c);

            /// <summary>
            /// Compare a text with a label
            /// </summary>
            bool Equals(ref Char text, ref Char label, int length);
        }

        /// <summary>
        /// Case sensitive
        /// </summary>
        readonly struct ExactChars : IChars
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Char Fold(Char c) => c;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(ref Char text, ref Char label, int length)
            {
                if (length >= MinVectorizedLabel)
                {
                    if (!Vector128.IsHardwareAccelerated)
                        return MemoryMarshal.CreateReadOnlySpan(ref text, length).SequenceEqual(MemoryMarshal.CreateReadOnlySpan(ref label, length));
                    // 8 chars at a time, the last block overlaps the previous one
                    ref var t = ref Unsafe.As<Char, UInt16>(ref text);
                    ref var l = ref Unsafe.As<Char, UInt16>(ref label);
                    var last = (nuint)(length - 8);
                    for (nuint i = 0; ; i += 8)
                    {
                        if (i > last)
                            i = last;
                        if (Vector128.LoadUnsafe(ref t, i) != Vector128.LoadUnsafe(ref l, i))
                            return false;
                        if (i == last)
                            return true;
                    }
                }
                for (int i = 0; i < length; ++i)
                    if (Unsafe.Add(ref text, i) != Unsafe.Add(ref label, i))
                        return false;
                return true;
            }
        }

        /// <summary>
        /// Case in-sensitive (the keys and labels are upper cased)
        /// </summary>
        readonly struct UpperChars : IChars
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Char Fold(Char c)
            {
                var u = Upper;
                return c < (uint)u.Length ? u[c] : c.FastToUpper();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(ref Char text, ref Char label, int length)
            {
                if ((length >= MinVectorizedLabel) && Vector128.IsHardwareAccelerated)
                {
                    // For ASCII text FastToUpper is the ASCII upper case, so it can be done vectorized (the label is upper cased, a non-ASCII label char never matches an ASCII char).
                    // 8 chars at a time, the last block overlaps the previous one, falls back to the scalar compare if the text isn't ASCII
                    ref var t = ref Unsafe.As<Char, UInt16>(ref text);
                    ref var l = ref Unsafe.As<Char, UInt16>(ref label);
                    var last = (nuint)(length - 8);
                    var nonAscii = Vector128.Create((UInt16)0xff80);
                    var lowerA = Vector128.Create((UInt16)'a');
                    var lowerRange = Vector128.Create((UInt16)('z' - 'a'));
                    var toUpper = Vector128.Create((UInt16)0x20);
                    for (nuint i = 0; ; i += 8)
                    {
                        if (i > last)
                            i = last;
                        var v = Vector128.LoadUnsafe(ref t, i);
                        if ((v & nonAscii) != Vector128<UInt16>.Zero)
                            break;
                        var upper = v - (Vector128.LessThanOrEqual(v - lowerA, lowerRange) & toUpper);
                        if (upper != Vector128.LoadUnsafe(ref l, i))
                            return false;
                        if (i == last)
                            return true;
                    }
                }
                for (int i = 0; i < length; ++i)
                    if (Fold(Unsafe.Add(ref text, i)) != Unsafe.Add(ref label, i))
                        return false;
                return true;
            }
        }

        /// <summary>
        /// CharExt.FastToUpper of the first 256 chars (CharExt have a static constructor, so every access to it's tables have a class init check)
        /// </summary>
        static readonly Char[] Upper = Enumerable.Range(0, 256).Select(x => ((Char)x).FastToUpper()).ToArray();

        /// <summary>
        /// Labels with at least this many chars are compared vectorized (if the text is ASCII for case in-sensitive trees)
        /// </summary>
        const int MinVectorizedLabel = 8;

        /// <summary>
        /// Add all strings below a node (not including the node itself), ordered by key
        /// </summary>
        void InternalAddAllInOrder(List<String> found, int ni)
        {
            var nodes = Nodes;
            ref var node = ref nodes[ni];
            var e = node.ChildStart + node.ChildCount;
            for (int i = node.ChildStart; i < e; ++i)
            {
                var val = nodes[i].Leaf;
                if (val != null)
                    found.Add(val);
                InternalAddAllInOrder(found, i);
            }
        }

        #endregion


        public static long AllocatedNodes => Interlocked.Read(ref CountAllocNodes);

        static long CountAllocNodes;

        ~FrozenStringTree()
        {
            Interlocked.Add(ref CountAllocNodes, -Nodes.Length);
        }


        public FrozenStringTree(StringTree tree)
        {
            CaseInSensitive = tree.GetLeaf() != null;
            // Breadth first, so that the children of a node are stored after each other
            List<StringTree> src = [tree];
            List<Node> nodes = [new Node { TableStart = -1 }];
            List<Char> keys = ['\0'];
            List<Char> labels = new();
            List<Byte> table = new();
            for (int ni = 0; ni < src.Count; ++ni)
            {
                var children = src[ni].GetNodes();
                if ((children == null) || (children.Count == 0))
                    continue;
                var n = nodes[ni];
                n.ChildStart = nodes.Count;
                n.ChildCount = children.Count;
                foreach (var x in children.OrderBy(x => x.Key))
                {
                    // Compress single child chains (without strings) into a label
                    var labelStart = labels.Count;
                    var child = x.Value;
                    while (child.GetLeaf() == null)
                    {
                        var cn = child.GetNodes();
                        if ((cn == null) || (cn.Count != 1))
                            break;
                        var only = cn.First();
                        labels.Add(only.Key);
                        child = only.Value;
                    }
                    src.Add(child);
                    keys.Add(x.Key);
                    nodes.Add(new Node
                    {
                        Leaf = child.GetLeaf(),
                        LabelStart = labelStart,
                        LabelLength = labels.Count - labelStart,
                        TableStart = -1,
                    });
                }
                // Lookup table for nodes with a lot of children (if the keys are close)
                var count = n.ChildCount;
                if ((count >= TableMinChildren) && (count < Byte.MaxValue))
                {
                    var min = keys[n.ChildStart];
                    var range = keys[n.ChildStart + count - 1] - min + 1;
                    if (range <= Math.Max(TableMinEntries, count * TableMaxEntriesPerChild))
                    {
                        n.TableStart = table.Count;
                        n.TableLength = (UInt16)range;
                        n.TableMin = min;
                        for (int i = 0; i < range; ++i)
                            table.Add(0);
                        for (int i = 0; i < count; ++i)
                            table[n.TableStart + keys[n.ChildStart + i] - min] = (Byte)(i + 1);
                    }
                }
                nodes[ni] = n;
            }
            Nodes = nodes.ToArray();
            Keys = keys.ToArray();
            Labels = labels.ToArray();
            Table = table.ToArray();
            // The ASCII chars (and pairs of ASCII chars) that can be the start of a match
            var pairs = new PairBits();
            for (int c = 0; c < 128; ++c)
            {
                var child = FindChild(0, (Char)c);
                if (child < 0)
                    continue;
                if (c < 64)
                    FirstAscii0 |= 1UL << c;
                else
                    FirstAscii1 |= 1UL << c;
                ref var node = ref Nodes[child];
                for (int c1 = 0; c1 < 128; ++c1)
                {
                    bool match;
                    if ((node.Leaf != null) && (node.LabelLength == 0))
                        match = true;
                    else if (node.LabelLength > 0)
                        match = Labels[node.LabelStart] == (CaseInSensitive ? default(UpperChars).Fold((Char)c1) : (Char)c1);
                    else
                        match = FindChild(child, (Char)c1) >= 0;
                    if (match)
                        pairs[(c << 1) | (c1 >> 6)] |= 1UL << c1;
                }
            }
            Pairs = pairs;
            // Small tree lookup
            var all = GetAll().ToArray();
            MinLength = all.Length == 0 ? 0 : all.Min(x => x.Length);
            if ((all.Length > 0) && (all.Length <= MaxFastStrings) && (MinLength >= 4) && BitConverter.IsLittleEndian)
            {
                var keyChars = MinLength >= 8 ? 8 : 4;
                var loose0 = CaseInSensitive ? LooseCase : 0;
                var loose1 = (CaseInSensitive && (keyChars == 8)) ? LooseCase : 0;
                var groups = all
                    .Select(x => (Value: x, Folded: CaseInSensitive ? String.Concat(x.Select(c => default(UpperChars).Fold(c))) : x))
                    .GroupBy(x => (Key0: Key(x.Folded) | loose0, Key1: keyChars == 8 ? (Key(x.Folded.AsSpan(4)) | loose1) : 0UL))
                    .Select(g => (g.Key.Key0, g.Key.Key1, Strings: g.OrderByDescending(x => x.Value.Length).ToArray()))
                    .ToArray();
                // Find hash multipliers without collisions
                var rng = new Random(1);
                var used = new bool[256];
                for (int attempt = 0; attempt < 1000; ++attempt)
                {
                    var m0 = (UInt64)rng.NextInt64() | 1;
                    var m1 = (UInt64)rng.NextInt64() | 1;
                    Array.Clear(used);
                    bool collision = false;
                    foreach (var g in groups)
                    {
                        var h = FastHash(g.Key0, g.Key1, m0, m1);
                        collision |= used[h];
                        used[h] = true;
                    }
                    if (collision)
                        continue;
                    var fastGroups = new FastGroup[groups.Length + 1];
                    List<FastCandidate> candidates = new();
                    List<Char> chars = new();
                    for (int i = 0; i < groups.Length; ++i)
                    {
                        var g = groups[i];
                        fastGroups[i + 1] = new FastGroup
                        {
                            Key0 = g.Key0,
                            Key1 = g.Key1,
                            Start = candidates.Count,
                            Count = g.Strings.Length,
                        };
                        FastSlots[FastHash(g.Key0, g.Key1, m0, m1)] = (Byte)(i + 1);
                        foreach (var x in g.Strings)
                        {
                            // Case sensitive: the chars after the key, case in-sensitive: all chars (the key is loose).
                            // If the chars to compare fits in the last MinVectorizedLabel chars of the string, the last MinVectorizedLabel chars are compared (a single vector compare)
                            var l = x.Value.Length;
                            var from = CaseInSensitive ? 0 : keyChars;
                            if ((l >= MinVectorizedLabel) && (l > from) && ((l - from) <= MinVectorizedLabel))
                                from = l - MinVectorizedLabel;
                            candidates.Add(new FastCandidate
                            {
                                Value = x.Value,
                                Length = l,
                                CompareFrom = from,
                                CompareLength = l - from,
                                CompareStart = chars.Count,
                            });
                            chars.AddRange(x.Folded.AsSpan(from));
                        }
                    }
                    FastKeyChars = keyChars;
                    FastLooseKey1 = loose1;
                    FastM0 = m0;
                    FastM1 = m1;
                    FastGroups = fastGroups;
                    FastCandidates = candidates.ToArray();
                    FastChars = chars.ToArray();
                    break;
                }
            }
            Interlocked.Add(ref CountAllocNodes, Nodes.Length);
        }

        /*
        /// <summary>
        /// Make a copy of a tree
        /// </summary>
        /// <returns></returns>
        public FrozenStringTree Clone() => this;
*/
    }

}
