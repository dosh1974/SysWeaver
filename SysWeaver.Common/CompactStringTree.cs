using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;


namespace SysWeaver
{

    /// <summary>
    /// A small memory efficient map from chars to values, stored as two exactly sized arrays (keys sorted), used for the nodes of <see cref="CompactStringTree"/>.
    /// </summary>
    /// <remarks>
    /// Every <see cref="Add(char, T)"/> allocates new arrays (O(n)), the old arrays are recycled in a static per size cache (shared by all instances of this T, max 1024 per size up to 64 entries).
    /// Not thread safe: a reader that runs concurrently with an <see cref="Add(char, T)"/> can see mismatched (and recycled) key and value arrays.
    /// </remarks>
    /// <typeparam name="T">The type of the values</typeparam>
    public sealed class CompactCharDictionary<T> : IEnumerable<KeyValuePair<Char, T>>
    {
        /// <summary>
        /// The number of entries
        /// </summary>
        public int Count => K?.Length ?? 0;

        /// <summary>
        /// Add an entry (keeping the keys sorted)
        /// </summary>
        /// <param name="key">The key, must not already exist (duplicates are not detected)</param>
        /// <param name="value">The value</param>
        public void Add(Char key, T value)
        {
            var k = K;
            var v = V;
            if (k == null)
            {

                (k, v) = Alloc(1);
                k[0] = key;
                v[0] = value;
                K = k;
                V = v;
                return;
            }
            var dl = k.Length;
            var (nk, nv) = Alloc(dl + 1);
            int o = 0;
            int i;
            for (i = 0; i < dl; ++i, ++o)
            {
                var kk = k[i];
                if (kk > key)
                {
                    nk[o] = key;
                    nv[o] = value;
                    ++o;
                    break;
                }
                nk[o] = kk;
                nv[o] = v[i];
            }
            for (; i < dl; ++i, ++o)
            {
                nk[o] = k[i];
                nv[o] = v[i];
            }
            if (o == dl)
            {
                nk[o] = key;
                nv[o] = value;
            }
            //Array.Sort(n, EComparer.Instance);
            Free((k, v));
            K = nk;
            V = nv;
        }

        /// <summary>
        /// Get the value of a key (linear search for less than 8 entries, else a binary search)
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value, or default if not found</param>
        /// <returns>True if the key was found</returns>
        public bool TryGetValue(Char key, out T value)
        {
            var k = K;
            if (k == null)
            {
                value = default; 
                return false;
            }
            var dl = k.Length;
            if (dl < 8)
            {
                for (int i = 0; i < dl; ++ i)
                {
                    var e = k[i];
                    if (e == key)
                    {
                        value = V[i];
                        return true;
                    }
                }
                value = default;
                return false;
            }
            var fi = BinarySearch.Find(k, 0, dl, key);
            if (fi < 0)
            {
                value = default;
                return false;
            }
            value = V[fi];
            return true;
        }

        /// <summary>
        /// Enumerate the entries, ordered by key
        /// </summary>
        public IEnumerator<KeyValuePair<char, T>> GetEnumerator()
        {
            var k = K;
            if (k != null)
            {
                var l = k.Length;
                var v = V;
                for (int i = 0; i < l; ++i)
                    yield return new KeyValuePair<Char, T>(k[i], v[i]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        /// <summary>
        /// The keys (sorted), null if empty
        /// </summary>
        Char[] K;

        /// <summary>
        /// The values (same order as the keys), null if empty
        /// </summary>
        T[] V;

        #region Array allocator

        /// <summary>
        /// A thread safe cache of key and value arrays of one size
        /// </summary>
        sealed class ArrayCache
        {
            /// <summary>
            /// Get a cached pair of arrays, or allocate new ones (the content of the arrays is undefined)
            /// </summary>
            public ValueTuple<Char[], T[]> Alloc(int size)
            {
                if (S.TryPop(out var e))
                {
                    Interlocked.Decrement(ref Count);
                    return e;
                }
                return (GC.AllocateUninitializedArray<char>(size), GC.AllocateUninitializedArray<T>(size));
            }

            /// <summary>
            /// Return a pair of arrays to the cache (dropped if the cache is full), the arrays are not cleared (so referenced values are kept alive until reused)
            /// </summary>
            public void Free(ValueTuple<Char[], T[]> data)
            {
                if (Interlocked.Increment(ref Count) > 1024)
                {
                    Interlocked.Decrement(ref Count);
                    return;
                }
                S.Push(data);
            }

            int Count;
            readonly ConcurrentStack<ValueTuple<Char[], T[]>> S = new ConcurrentStack<ValueTuple<Char[], T[]>>();

            /// <summary>
            /// Remove all cached arrays
            /// </summary>
            public void Flush()
            {
                var s = S;
                while (s.TryPop(out var _))
                    Interlocked.Decrement(ref Count);
            }
        }

        const int CacheCount = 64;

        /// <summary>
        /// Allocate a pair of arrays (from the cache if the size is at most <see cref="CacheCount"/>)
        /// </summary>
        static ValueTuple<Char[], T[]> Alloc(int size)
            => 
            size <= CacheCount 
            ? 
            Cache[size].Alloc(size) 
            : 
            (GC.AllocateUninitializedArray<char>(size), GC.AllocateUninitializedArray<T>(size))
            ;

        /// <summary>
        /// Return a pair of arrays to the cache (if the size is at most <see cref="CacheCount"/>)
        /// </summary>
        static void Free(ValueTuple<Char[], T[]> d)
        {
            int size = d.Item1.Length;
            if (size <= CacheCount)
                Cache[size].Free(d);
        }

        static readonly ArrayCache[] Cache = ArrayExt.Create(CacheCount + 1, x => new ArrayCache());


        /// <summary>
        /// Release all cached arrays (of this T), call it after building trees to free the memory
        /// </summary>
        public static void Flush()
        {
            Cache.Process(x => x.Flush());
        }


        #endregion // Array allocator




    }






    /// <summary>
    /// A string tree stores a bunch of strings in a way that makes it fast to check if a test string starts with ANY of the contained strings.
    /// A memory efficient version of <see cref="StringTree"/> that doesn't store the strings (only a leaf flag per node), found strings are extracted from the text.
    /// </summary>
    /// <remarks>
    /// The root node is the tree, the leaf flag of the root is used as the case in-sensitive marker.
    /// Case in-sensitive trees upper case all chars (invariant culture), and the found strings have the casing of the text (not the added string).
    /// Empty strings aren't validated: adding one marks the root as a leaf, which makes the tree case in-sensitive.
    /// Not thread safe for writes (see <see cref="CompactCharDictionary{T}"/>).
    /// Every node have a finalizer (used to maintain <see cref="AllocatedNodes"/>).
    /// Not used by the framework.
    /// </remarks>
    public sealed class CompactStringTree : IStringTree
    {
        /// <summary>
        /// The child nodes (keyed by the next char, upper cased for case in-sensitive trees), or null
        /// </summary>
        CompactCharDictionary<CompactStringTree> Nodes;

        /// <summary>
        /// Release all cached node arrays, see <see cref="CompactCharDictionary{T}.Flush"/>
        /// </summary>
        public static void Flush() => CompactCharDictionary<CompactStringTree>.Flush();


        /// <summary>
        /// True if a string ends at this node, for the root it's the case in-sensitive marker
        /// </summary>
        bool IsLeaf;

#if DEBUG
        public override string ToString() => IsLeaf ? "Leaf" : ("Children: " + Nodes?.Count);
#endif//DEBUG

        /// <summary>
        /// True if the tree is case in-sensitive (only valid for the root node)
        /// </summary>
        public bool IsCaseInSensitive => IsLeaf;

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null, empty strings or duplicates</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        /// <exception cref="Exception">A string is a duplicate</exception>
        public static CompactStringTree Build(IEnumerable<String> strings, bool caseInSensitive = false)
        {
            CompactStringTree parent = null;
            foreach (var s in strings)
                parent = InternalAdd(s, parent, caseInSensitive);
            parent = parent ?? new CompactStringTree();
            if (caseInSensitive)
                parent.IsLeaf = true;
            return parent;

        }

        /// <summary>
        /// Add a string to a new or existing tree
        /// </summary>
        /// <param name="text">The string to add, may not be null or empty</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree, if the tree already exists, the casing from that tree is used</param>
        /// <param name="parent">An existing tree (that is modified), or null to create a new tree</param>
        /// <returns>The new tree (or the existing)</returns>
        /// <exception cref="Exception">The string have already been added</exception>
        public static CompactStringTree Add(String text, bool caseInSensitive = false, CompactStringTree parent = null)
        {
            if (parent != null)
                caseInSensitive = parent.IsCaseInSensitive;
            parent = InternalAdd(text, parent, caseInSensitive);
            parent = parent ?? new CompactStringTree();
            if (caseInSensitive)
                parent.IsLeaf = true;
            return parent;
        }


        /// <summary>
        /// Try to add a string to a new or existing tree
        /// </summary>
        /// <param name="parent">An existing tree to update, or null to create a new tree (assigned if the string was added)</param>
        /// <param name="text">The string to add, may not be null or empty</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree, if the tree already exists, the casing from that tree is used</param>
        /// <returns>True if the string was added, false if it already existed</returns>
        public static bool TryAdd(ref CompactStringTree parent, String text, bool caseInSensitive = false)
        {
            if (parent != null)
                caseInSensitive = parent.IsCaseInSensitive;
            if (!InternalAdd(out var x, text, parent, caseInSensitive))
                return false;
            parent = x ?? new CompactStringTree();
            if (caseInSensitive)
                parent.IsLeaf = true;
            return true;
        }

        /// <summary>
        /// Find the longest string (in the tree), that the text starts with (at the start offset)
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset (a start at or beyond the end returns null)</param>
        /// <returns>The longest found match or null if no match is found. The match is a new substring of the text (with the casing of the text)</returns>
        public String StartsWithAny(String text, int start = 0)
        {
            CompactStringTree node = this;
            var ostart = start;
            int found = -1;
            int len = text.Length;
            if (node.IsLeaf)
            {
                while (start < len)
                {
                    var nodes = node.Nodes;
                    var c = text[start];
                    if (nodes == null)
                        break;
                    c = c.FastToUpper();
                    nodes.TryGetValue(c, out var n);
                    ++start;
                    if (n == null)
                        break;
                    node = n;
                    if (n.IsLeaf)
                        found = start;
                }
            }
            else
            {
                while (start < len)
                {
                    var nodes = node.Nodes;
                    var c = text[start];
                    if (nodes == null)
                        break;
                    nodes.TryGetValue(c, out var n);
                    ++start;
                    if (n == null)
                        break;
                    node = n;
                    if (n.IsLeaf)
                        found = start;
                }

            }
            if (found < 0)
                return null;
            return text.Substring(ostart, found - ostart);
        }


        /// <summary>
        /// Check if a string is contained in the tree (an exact match of the text from the start offset)
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>True if the string exists. Note that an empty text (or a start at the end) returns true for case in-sensitive trees (the root marker)</returns>
        public bool Contains(String text, int start = 0)
        {
            CompactStringTree node = this;
            int len = text.Length;
            if (node.IsLeaf)
            {
                while (start < len)
                {
                    var nodes = node.Nodes;
                    var c = text[start];
                    if (nodes == null)
                        break;
                    c = c.FastToUpper();
                    nodes.TryGetValue(c, out var n);
                    ++start;
                    if (n == null)
                        break;
                    node = n;
                }
            }
            else
            {
                while (start < len)
                {
                    var nodes = node.Nodes;
                    var c = text[start];
                    if (nodes == null)
                        break;
                    nodes.TryGetValue(c, out var n);
                    ++start;
                    if (n == null)
                        break;
                    node = n;
                }
            }
            return (start == len) && node.IsLeaf;
        }



        /// <summary>
        /// Find all strings (in the tree) that the text starts with (at the start offset).
        /// Note that unlike <see cref="StringTree.AllStartsWithAny(string, int)"/> this only returns the prefixes (no completions)
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>A new list of matches (substrings of the text), ordered from shortest match to longest match</returns>
        public List<String> AllStartsWithAny(String text, int start = 0)
        {
            CompactStringTree node = this;
            int len = text.Length;
            var ostart = start;
            List<String> found = new List<string>();
            if (node.IsLeaf)
            {
                while (start < len)
                {
                    var nodes = node.Nodes;
                    var c = text[start];
                    if (nodes == null)
                        break;
                    c = c.FastToUpper();
                    nodes.TryGetValue(c, out var n);
                    ++start;
                    if (n == null)
                        break;
                    node = n;
                    if (n.IsLeaf)
                        found.Add(text.Substring(ostart, start - ostart));
                }
            }
            else
            {
                while (start < len)
                {
                    var nodes = node.Nodes;
                    var c = text[start];
                    if (nodes == null)
                        break;
                    nodes.TryGetValue(c, out var n);
                    ++start;
                    if (n == null)
                        break;
                    node = n;
                    if (n.IsLeaf)
                        found.Add(text.Substring(ostart, start - ostart));
                }
            }
            return found;
        }


        /// <summary>
        /// Get all strings contained in the string tree, ordered by char (upper cased for case in-sensitive trees)
        /// </summary>
        /// <returns>The strings (lazily enumerated)</returns>
        public IEnumerable<String> GetAll()
            => InternalGetAll(new StringBuilder());

        IEnumerable<String> InternalGetAll(StringBuilder sb)
        {
            var n = Nodes;
            if (n != null)
            {
                foreach (var x in n)
                {
                    var sl = sb.Length;
                    sb.Append(x.Key);
                    var next = x.Value;
                    if (next.IsLeaf)
                        yield return sb.ToString();
                    foreach (var r in next.InternalGetAll(sb))
                        yield return r;
                    sb.Remove(sl, sb.Length - sl);
                }
            }
        }

        /// <summary>
        /// Get all strings contained in the string tree, ordered by char (upper cased for case in-sensitive trees)
        /// </summary>
        /// <returns>The strings (lazily enumerated)</returns>
        IEnumerable<String> GetAllInOrder()
            => InternalGetAllInOrder(new StringBuilder());

        IEnumerable<String> InternalGetAllInOrder(StringBuilder sb)
        {
            var n = Nodes;
            if (n != null)
            {
                foreach (var x in n.OrderBy(x => x.Key))
                {
                    var sl = sb.Length;
                    sb.Append(x.Key);
                    var next = x.Value;
                    if (next.IsLeaf)
                        yield return sb.ToString();
                    foreach (var r in next.InternalGetAllInOrder(sb))
                        yield return r;
                    sb.Remove(sl, sb.Length - sl);
                }
            }
        }

        /// <summary>
        /// Get all strings contained in the string tree, in reverse char order
        /// </summary>
        /// <returns>The strings (lazily enumerated)</returns>
        public IEnumerable<String> GetAllInReverseOrder()
             => InternalAllInReverseOrder(new StringBuilder());

        IEnumerable<String> InternalAllInReverseOrder(StringBuilder sb)
        {
            var n = Nodes;
            if (n != null)
            {
                foreach (var x in n.Reverse())
                {
                    var sl = sb.Length;
                    sb.Append(x.Key);
                    var next = x.Value;
                    var isLeaf = next.IsLeaf;
                    foreach (var r in next.InternalAllInReverseOrder(sb))
                        yield return r;
                    if (isLeaf)
                        yield return sb.ToString();
                    sb.Remove(sl, sb.Length - sl);
                }
            }
        }


        static CompactStringTree InternalAdd(String text, CompactStringTree current, bool caseInSensitive, int c = 0)
        {
            if (c == text.Length)
            {
                if (current != null)
                {
                    if (current.IsLeaf)
                        throw new Exception("The string \"" + text + "\" have already been added!");
                    current.IsLeaf = true;
                    return current;
                }
                return new CompactStringTree(text);
            }
            var cc = text[c];
            if (caseInSensitive)
                cc = CharExt.FastUpper(cc);
            bool exists = false;
            ++c;
            if (current != null)
            {
                if (current.Nodes == null)
                    current.Nodes = new CompactCharDictionary<CompactStringTree>();
                exists = current.Nodes.TryGetValue(cc, out var ex);
                if (exists)
                    InternalAdd(text, ex, caseInSensitive, c);
            }
            var nc = current ?? new CompactStringTree();
            if (!exists)
            {
                var n = InternalAdd(text, null, caseInSensitive, c);
                nc.Nodes?.Add(cc, n);
            }
            return nc;
        }


        static bool InternalAdd(out CompactStringTree res, String text, CompactStringTree current, bool caseInSensitive, int c = 0)
        {
            if (c == text.Length)
            {
                if (current != null)
                {
                    if (current.IsLeaf)
                    {
                        res = null;
                        return false;
                    }
                    current.IsLeaf = true;
                    res = current;
                    return true;
                }
                res = new CompactStringTree(text);
                return true;
            }
            var cc = text[c];
            if (caseInSensitive)
                cc = CharExt.FastUpper(cc);
            bool exists = false;
            ++c;
            if (current != null)
            {
                if (current.Nodes == null)
                    current.Nodes = new CompactCharDictionary<CompactStringTree>();
                exists = current.Nodes.TryGetValue(cc, out var ex);
                if (exists)
                {
                    if (!InternalAdd(out var _, text, ex, caseInSensitive, c))
                    {
                        res = null;
                        return false;
                    }
                }
            }
            var nc = current ?? new CompactStringTree();
            if (!exists)
            {
                if (!InternalAdd(out var n, text, null, caseInSensitive, c))
                {
                    res = null;
                    return false;
                }
                nc.Nodes?.Add(cc, n);
            }
            res = nc;
            return true;
        }



        /// <summary>
        /// The number of <see cref="CompactStringTree"/> nodes that are currently allocated (created and not yet finalized), for diagnostics
        /// </summary>
        public static long AllocatedNodes => Interlocked.Read(ref CountAllocNodes);

        /// <summary>
        /// The number of allocated nodes (see <see cref="AllocatedNodes"/>)
        /// </summary>
        static long CountAllocNodes;

        ~CompactStringTree()
        {
            Interlocked.Decrement(ref CountAllocNodes);
        }

        CompactStringTree(string leaf)
        {
            IsLeaf = leaf != null;
            Interlocked.Increment(ref CountAllocNodes);
        }


        /// <summary>
        /// Create an empty tree (root node)
        /// </summary>
        /// <param name="caseInSesnitive">True to make a case in-sensitive tree</param>
        public CompactStringTree(bool caseInSesnitive = false)
        {
            IsLeaf = caseInSesnitive;
            Nodes = new();
            Interlocked.Increment(ref CountAllocNodes);
        }

        CompactStringTree(bool isLeaf, CompactCharDictionary<CompactStringTree> nodes)
        {
            IsLeaf = isLeaf;
            Nodes = nodes;
            Interlocked.Increment(ref CountAllocNodes);
        }

        /// <summary>
        /// Make a deep copy of a tree
        /// </summary>
        /// <returns>The copy</returns>
        public CompactStringTree Clone()
        {
            CompactCharDictionary<CompactStringTree> nodes = null;
            var en = Nodes;
            if (en != null)
            {
                nodes = new CompactCharDictionary<CompactStringTree>();
                foreach (var n in en)
                    nodes.Add(n.Key, n.Value.Clone());
            }
            return new CompactStringTree(IsLeaf, nodes);
        }


    }


}

