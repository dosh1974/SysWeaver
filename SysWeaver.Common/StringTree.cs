using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace SysWeaver
{

    /// <summary>
    /// A string tree (a mutable trie with one dictionary per node) stores a bunch of strings in a way that makes it fast to check if a test string starts with ANY of the contained strings.
    /// Empty strings are not supported, they can't be added and can't be searched for (throws in debug builds).
    /// </summary>
    /// <remarks>
    /// The root node is the tree, and the leaf of the root is used as the case in-sensitive marker.
    /// Case in-sensitive trees upper case all chars (invariant culture, see <see cref="CharExt.FastToUpper(char)"/>), the found strings are the added strings (original casing).
    /// Not thread safe for writes (concurrent reads without writes are safe).
    /// Every node have a finalizer (used to maintain <see cref="AllocatedNodes"/>), so a large tree is expensive to collect.
    /// Use <see cref="FrozenStringTree"/> (built from a <see cref="StringTree"/>) for a faster immutable version.
    /// </remarks>
    public sealed class StringTree : IStringTree
    {

#if DEBUG
        public override string ToString() => Leaf != null ? String.Join(Leaf, "Leaf \"", '"') : ("Children: " + Nodes?.Count);
#endif//DEBUG

        /// <summary>
        /// True if the tree is case in-sensitive (only valid for the root node)
        /// </summary>
        public bool IsCaseInSensitive => Leaf != null;

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null, empty strings or duplicates (duplicates after case folding for case in-sensitive trees)</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree (an empty tree if there are no strings)</returns>
        /// <exception cref="Exception">A string is a duplicate, or (in debug builds only) a string is empty</exception>
        public static StringTree Build(IEnumerable<String> strings, bool caseInSensitive = false)
        {
            StringTree parent = null;
            foreach (var s in strings)
            {
                ValidateAdd(s);
                parent = InternalAdd(s, parent, caseInSensitive);
            }
            parent = parent ?? new StringTree();
            if (caseInSensitive)
                parent.Leaf = "caseInSensitive";
            return parent;

        }

        /// <summary>
        /// Add a string to a new or existing tree
        /// </summary>
        /// <param name="text">The string to add, may not be null or empty</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree, if the tree already exists, the casing from that tree is used</param>
        /// <param name="parent">An existing tree (that is modified), or null to create a new tree</param>
        /// <returns>The new tree (or the existing)</returns>
        /// <exception cref="Exception">The string have already been added, or (in debug builds only) the string is empty</exception>
        public static StringTree Add(String text, bool caseInSensitive = false, StringTree parent = null)
        {
            if (parent != null)
                caseInSensitive = parent.IsCaseInSensitive;
            ValidateAdd(text);
            parent = InternalAdd(text, parent, caseInSensitive);
            parent = parent ?? new StringTree();
            if (caseInSensitive)
                parent.Leaf = "caseInSensitive";
            return parent;
        }


        /// <summary>
        /// Try to add a string to a new or existing tree
        /// </summary>
        /// <param name="parent">An existing tree to update, or null to create a new tree (assigned if the string was added)</param>
        /// <param name="text">The string to add, may not be null or empty</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree, if the tree already exists, the casing from that tree is used</param>
        /// <returns>True if the string was added, false if it already existed (the tree is unchanged)</returns>
        /// <exception cref="Exception">(Debug builds only) the string is empty</exception>
        public static bool TryAdd(ref StringTree parent, String text, bool caseInSensitive = false)
        {
            if (parent != null)
                caseInSensitive = parent.IsCaseInSensitive;
            ValidateAdd(text);
            if (!InternalAdd(out var x, text, parent, caseInSensitive))
                return false;
            parent = x ?? new StringTree();
            if (caseInSensitive)
                parent.Leaf = "caseInSensitive";
            return true;
        }

        /// <summary>
        /// Find the longest string (in the tree), that the text starts with (at the start offset)
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text (in release builds a start at or beyond the end returns null)</param>
        /// <returns>The longest found match (the added string) or null if no match is found</returns>
        /// <exception cref="Exception">(Debug builds only) start is at or beyond the end of the text</exception>
        public String StartsWithAny(String text, int start = 0)
        {
            ValidateSearch(text, start);
            StringTree node = this;
            int len = text.Length;
            String found = null;
            if (node.Leaf != null)
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
                    var val = n.Leaf;
                    node = n;
                    if (val != null)
                        found = val;
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
                    var val = n.Leaf;
                    node = n;
                    if (val != null)
                        found = val;
                }

            }
            return found;
        }

        /// <summary>
        /// Find the strings that are a prefix of the text, followed by the strings that "complete" the text (prefix matches and auto complete).
        /// The text is matched as far as possible (char by char), the result is all strings that the text starts with (shortest first),
        /// followed by all strings below the deepest matched node (ordered by char), i.e. the strings that starts with the longest matched part of the text.
        /// If not even the first char matches, all strings in the tree are returned.
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>A new list of strings, the prefixes (shortest first) followed by the completions (ordered by char)</returns>
        /// <exception cref="Exception">(Debug builds only) start is at or beyond the end of the text</exception>
        public List<String> AllStartsWithAny(String text, int start = 0)
        {
            ValidateSearch(text, start);
            StringTree node = this;
            int len = text.Length;
            List<String> found = new List<string>();
            if (node.Leaf != null)
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
                    var val = n.Leaf;
                    node = n;
                    if (val != null)
                        found.Add(val);
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
                    var val = n.Leaf;
                    node = n;
                    if (val != null)
                        found.Add(val);
                }
            }
            if (node != null)
                InternalAddAllInOrder(found, node);
            return found;
        }

        /// <summary>
        /// Find all strings (in the tree), that is a prefix of the text
        /// </summary>
        /// <param name="text">The text to find prefixes (in the tree) for, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>A new list of matches, ordered from shortest match to longest match</returns>
        /// <exception cref="Exception">(Debug builds only) start is at or beyond the end of the text</exception>
        public List<String> PrefixesOf(String text, int start = 0)
        {
            ValidateSearch(text, start);
            StringTree node = this;
            int len = text.Length;
            List<String> found = new();
            bool first = true;
            if (node.Leaf != null)
            {
                while (start < len)
                {
                    var val = first ? null : node.Leaf;
                    first = false;
                    if (val != null) 
                        found.Add(val);
                    var nodes = node.Nodes;
                    node = null;
                    var c = text[start];
                    if (nodes == null)
                        break;
                    c = c.FastToUpper();
                    ++start;
                    if (!nodes.TryGetValue(c, out node))
                        break;
                }
            }
            else
            {
                first = false;
                while (start < len)
                {
                    var val = node.Leaf;
                    if (val != null)
                        found.Add(val);
                    var nodes = node.Nodes;
                    node = null;
                    var c = text[start];
                    if (nodes == null)
                        break;
                    ++start;
                    if (!nodes.TryGetValue(c, out node))
                        break;
                }
            }
            if (node != null)
            {
                var val = first ? null : node.Leaf;
                if (val != null)
                    found.Add(val);
            }
            return found;
        }

        /// <summary>
        /// Add all strings below a node (not including the node itself), ordered by char
        /// </summary>
        void InternalAddAllInOrder(List<String> found, StringTree node)
        {
            var n = node.Nodes;
            if (n == null)
                return;
            foreach (var x in n.OrderBy(x => x.Key))
            {
                var next = x.Value;
                var val = next.Leaf;
                if (val != null)
                    found.Add(val);
                InternalAddAllInOrder(found, next);
            }
        }

        /// <summary>
        /// Add all strings below a node (not including the node itself), in any order
        /// </summary>
        void InternalAddAll(List<String> found, StringTree node)
        {
            var n = node.Nodes;
            if (n == null)
                return;
            foreach (var x in n)
            {
                var next = x.Value;
                var val = next.Leaf;
                if (val != null)
                    found.Add(val);
                InternalAddAll(found, next);
            }
        }
        /// <summary>
        /// Get all strings contained in the string tree, in any order (lazily enumerated, recursive)
        /// </summary>
        /// <returns>The strings</returns>
        public IEnumerable<String> GetAll()
        {
            var n = Nodes;
            if (n != null)
            {
                foreach (var x in n)
                {
                    var next = x.Value;
                    var val = next.Leaf;
                    if (val != null)
                        yield return val;
                    foreach (var r in next.GetAll())
                        yield return r;
                }
            }
        }

        /// <summary>
        /// Get all strings contained in the string tree, ordered by char (ordinal, of the upper cased chars for case in-sensitive trees), a string comes before the strings that it's a prefix of
        /// </summary>
        /// <returns>The strings (lazily enumerated, recursive)</returns>
        public IEnumerable<String> GetAllInOrder()
        {
            var n = Nodes;
            if (n != null)
            {
                foreach (var x in n.OrderBy(x => x.Key))
                {
                    var next = x.Value;
                    var val = next.Leaf;
                    if (val != null)
                        yield return val;
                    foreach (var r in next.GetAllInOrder())
                        yield return r;
                }
            }
        }

        /// <summary>
        /// Get all strings contained in the string tree, in the reverse order of <see cref="GetAllInOrder"/>
        /// </summary>
        /// <returns>The strings (lazily enumerated, recursive)</returns>
        public IEnumerable<String> GetAllInReverseOrder()
        {
            var n = Nodes;
            if (n != null)
            {
                foreach (var x in n.OrderByDescending(x => x.Key))
                {
                    var next = x.Value;
                    var val = next.Leaf;
                    foreach (var r in next.GetAllInReverseOrder())
                        yield return r;
                    if (val != null)
                        yield return val;
                }
            }
        }


        /// <summary>
        /// Add the chars of a string from an index (recursive)
        /// </summary>
        /// <param name="text">The string to add</param>
        /// <param name="current">The node for the chars before index c, or null to create it</param>
        /// <param name="caseInSensitive">True to upper case the chars</param>
        /// <param name="c">The index of the next char to add</param>
        /// <returns>The node for the chars before index c</returns>
        /// <exception cref="Exception">The string have already been added</exception>
        static StringTree InternalAdd(String text, StringTree current, bool caseInSensitive, int c = 0)
        {
            if (c == text.Length)
            {
                if (current != null)
                {
                    if (current.Leaf != null)
                        throw new Exception("The string \"" + text + "\" have already been added!");
                    current.Leaf = text;
                    return current;
                }
                return new StringTree(text);
            }
            var cc = text[c];
            if (caseInSensitive)
                cc = CharExt.FastUpper(cc);
            bool exists = false;
            ++c;
            if (current != null)
            {
                if (current.Nodes == null)
                    current.Nodes = new Dictionary<char, StringTree>();
                exists = current.Nodes.TryGetValue(cc, out var ex);
                if (exists)
                    InternalAdd(text, ex, caseInSensitive, c);
            }
            var nc = current ?? new StringTree();
            if (!exists)
            {
                var n = InternalAdd(text, null, caseInSensitive, c);
                nc.Nodes?.Add(cc, n);
            }
            return nc;
        }


        /// <summary>
        /// Add the chars of a string from an index (recursive), without throwing if the string already exists
        /// </summary>
        /// <returns>True if added, false if the string already exists (nothing is changed)</returns>
        static bool InternalAdd(out StringTree res, String text, StringTree current, bool caseInSensitive, int c = 0)
        {
            if (c == text.Length)
            {
                if (current != null)
                {
                    if (current.Leaf != null)
                    {
                        res = null;
                        return false;
                    }
                    current.Leaf = text;
                    res = current;
                    return true;
                }
                res = new StringTree(text);
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
                    current.Nodes = new Dictionary<char, StringTree>();
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
            var nc = current ?? new StringTree();
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
        /// Empty strings are not supported (the root leaf is used as the case in-sensitive marker), throws in debug builds
        /// </summary>
        [Conditional("DEBUG")]
        internal static void ValidateAdd(String text)
        {
            if (text.Length == 0)
                throw new Exception("Empty strings can't be added to a string tree!");
        }

        /// <summary>
        /// Searching for an empty string (start at or beyond the end of the text) is not supported, throws in debug builds
        /// </summary>
        [Conditional("DEBUG")]
        internal static void ValidateSearch(String text, int start)
        {
            if (start >= text.Length)
                throw new Exception("Can't search for an empty string in a string tree, start (" + start + ") must be less than the length of the text (" + text.Length + ")!");
        }

        /// <summary>
        /// The string that ends at this node (or null), for the root it's the case in-sensitive marker
        /// </summary>
        String Leaf;

        /// <summary>
        /// The child nodes (keyed by the next char, upper cased for case in-sensitive trees), or null
        /// </summary>
        Dictionary<Char, StringTree> Nodes;


        /// <summary>
        /// The string that ends at this node (or null), for the root node it's the case in-sensitive marker (non null if case in-sensitive)
        /// </summary>
        internal String GetLeaf() => Leaf;

        /// <summary>
        /// The child nodes (keyed by the next char, upper cased for case in-sensitive trees), or null
        /// </summary>
        internal Dictionary<Char, StringTree> GetNodes() => Nodes;

        /// <summary>
        /// The number of <see cref="StringTree"/> nodes that are currently allocated (created and not yet finalized), for diagnostics
        /// </summary>
        public static long AllocatedNodes => Interlocked.Read(ref CountAllocNodes);

        /// <summary>
        /// The number of allocated nodes (see <see cref="AllocatedNodes"/>)
        /// </summary>
        static long CountAllocNodes;

        ~StringTree()
        {
            Interlocked.Decrement(ref CountAllocNodes);
        }

        StringTree(string leaf)
        {
            Leaf = leaf;
            Interlocked.Increment(ref CountAllocNodes);
        }


        /// <summary>
        /// Create an empty tree (root node)
        /// </summary>
        /// <param name="caseInSesnitive">True to make a case in-sensitive tree</param>
        public StringTree(bool caseInSesnitive = false)
        {
            Leaf = caseInSesnitive ? "caseInSensitive" : null;
            Nodes = new();
            Interlocked.Increment(ref CountAllocNodes);
        }

        StringTree(string leaf, Dictionary<Char, StringTree> nodes)
        {
            Leaf = leaf;
            Nodes = nodes;
            Interlocked.Increment(ref CountAllocNodes);
        }

        /// <summary>
        /// Make a deep copy of a tree (all nodes are copied, the strings are shared)
        /// </summary>
        /// <returns>The copy</returns>
        public StringTree Clone()
        {
            Dictionary<Char, StringTree> nodes = null;
            var en = Nodes;
            if (en != null)
            {
                nodes = new Dictionary<char, StringTree>();
                foreach (var n in en)
                    nodes.Add(n.Key, n.Value.Clone());
            }
            return new StringTree(Leaf, nodes);
        }


    }


}
