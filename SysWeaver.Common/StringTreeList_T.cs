using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// A string tree (a mutable trie with one dictionary per node) that stores a bunch of strings with values, in a way that makes it fast to check if a test string starts with ANY of the contained strings.
    /// A string can have more than one value (adding an existing string appends the value).
    /// Empty strings are not supported, they can't be added and can't be searched for (throws in debug builds).
    /// </summary>
    /// <remarks>
    /// The root node is the tree, a case in-sensitive tree is marked by a shared static empty list as the root leaf.
    /// Case in-sensitive trees upper case all chars (invariant culture, see <see cref="CharExt.FastToUpper(char)"/>).
    /// The value lists returned by the search methods are the internal (mutable) lists of the tree, they must not be modified.
    /// Not thread safe for writes (concurrent reads without writes are safe).
    /// Every node have a finalizer (used to maintain <see cref="AllocatedNodes"/>), so a large tree is expensive to collect.
    /// Use <see cref="FrozenStringTreeList{T}"/> (or <see cref="StringPrefixLookup.BuildList{T}(IEnumerable{Tuple{string, T}})"/> for case sensitive lookups) for a faster immutable version.
    /// </remarks>
    /// <typeparam name="T">The type of the values</typeparam>
    public sealed class StringTreeList<T>
    {

#if DEBUG
        public override string ToString() =>
            Leaf != null ?
                (Leaf == LeafList ?
                    "Root: Case insensitive"
                    :
                    String.Concat("Leaf: ", String.Join(", ", Leaf))
                )
                :
                (
                    Nodes == null ?
                    "Root: Case sensitive"
                    :
                    ("Children: " + Nodes.Count)
                );
#endif//DEBUG

        /// <summary>
        /// True if the tree is case in-sensitive (only valid for the root node)
        /// </summary>
        public bool IsCaseInSensitive => Leaf == LeafList;

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings (and their values) to build a tree from, may not contain null or empty strings (duplicate strings are allowed, their values are appended in order)</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree (an empty tree if there are no strings)</returns>
        public static StringTreeList<T> Build(IEnumerable<Tuple<String, T>> strings, bool caseInSensitive = false)
        {
            StringTreeList<T> parent = null;
            foreach (var s in strings)
            {
                StringTree.ValidateAdd(s.Item1);
                parent = InternalAdd(s.Item1, s.Item2, parent, caseInSensitive);
            }
            parent = parent ?? new StringTreeList<T>();
            if (caseInSensitive)
                parent.Leaf = LeafList;
            return parent;
        }

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings (and their values) to build a tree from, may not contain null or empty strings (duplicate strings are allowed, their values are appended in order)</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree (an empty tree if there are no strings)</returns>
        public static StringTreeList<T> Build(IEnumerable<KeyValuePair<String, T>> strings, bool caseInSensitive = false)
        {
            StringTreeList<T> parent = null;
            foreach (var s in strings)
            {
                StringTree.ValidateAdd(s.Key);
                parent = InternalAdd(s.Key, s.Value, parent, caseInSensitive);
            }
            parent = parent ?? new StringTreeList<T>();
            if (caseInSensitive)
                parent.Leaf = LeafList;
            return parent;
        }

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="values">The values to add</param>
        /// <param name="getKey">Function that extracts the string key (may not return null or an empty string, values with the same key are appended in order)</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree (an empty tree if there are no values)</returns>
        public static StringTreeList<T> Build(IEnumerable<T> values, Func<T, String> getKey, bool caseInSensitive = false)
        {
            StringTreeList<T> parent = null;
            foreach (var s in values)
            {
                var key = getKey(s);
                StringTree.ValidateAdd(key);
                parent = InternalAdd(key, s, parent, caseInSensitive);
            }
            parent = parent ?? new StringTreeList<T>();
            if (caseInSensitive)
                parent.Leaf = LeafList;
            return parent;
        }


        /// <summary>
        /// The root leaf of case in-sensitive trees (a marker, shared by all trees of this type)
        /// </summary>
        static readonly List<T> LeafList = new();


        /// <summary>
        /// Add a string to a new or existing tree
        /// </summary>
        /// <param name="text">The string to add, may not be null or empty. If the string already exists, the value is appended to the values of the string</param>
        /// <param name="value">The value associated with the string</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree, if the tree already exists, the casing from that tree is used</param>
        /// <param name="parent">An existing tree (that is modified), or null to create a new tree</param>
        /// <returns>The new tree (or the existing)</returns>
        public static StringTreeList<T> Add(String text, T value, bool caseInSensitive = false, StringTreeList<T> parent = null)
        {
            if (parent != null)
                caseInSensitive = parent.IsCaseInSensitive;
            StringTree.ValidateAdd(text);
            parent = InternalAdd(text, value, parent, caseInSensitive);
            parent = parent ?? new StringTreeList<T>();
            if (caseInSensitive)
                parent.Leaf = LeafList;
            return parent;
        }


        /// <summary>
        /// Try to add a string to a new or existing tree
        /// </summary>
        /// <param name="parent">An existing tree to update, or null to create a new tree (assigned)</param>
        /// <param name="text">The string to add, may not be null or empty</param>
        /// <param name="value">The value associated with the string</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree, if the tree already exists, the casing from that tree is used</param>
        /// <returns>Always true: if the string already exists the value is appended to its values (same as <see cref="Add(string, T, bool, StringTreeList{T})"/>)</returns>
        public static bool TryAdd(ref StringTreeList<T> parent, String text, T value, bool caseInSensitive = false)
        {
            if (parent != null)
                caseInSensitive = parent.IsCaseInSensitive;
            StringTree.ValidateAdd(text);
            if (!InternalAdd(out var x, text, value, parent, caseInSensitive))
                return false;
            parent = x ?? new StringTreeList<T>();
            if (caseInSensitive)
                parent.Leaf = LeafList;
            return true;
        }

        /// <summary>
        /// Find the longest string (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text (in release builds a start at or beyond the end returns null)</param>
        /// <returns>The values of the longest found match (the internal list, must not be modified) or null if no match is found</returns>
        /// <exception cref="Exception">(Debug builds only) start is at or beyond the end of the text</exception>
        public IReadOnlyList<T> StartsWithAny(String text, int start = 0)
        {
            StringTree.ValidateSearch(text, start);
            StringTreeList<T> node = this;
            int len = text.Length;
            List<T> found = null;
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
        /// Find the values of the strings that are a prefix of the text, followed by the values of the strings that "complete" the text (prefix matches and auto complete).
        /// The text is matched as far as possible (char by char), the result is the values of all strings that the text starts with (shortest first),
        /// followed by the values of all strings below the deepest matched node (ordered by char).
        /// If not even the first char matches, the values of all strings in the tree are returned.
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>A new list of the value lists (the internal lists, must not be modified), the prefixes (shortest first) followed by the completions (ordered by char)</returns>
        /// <exception cref="Exception">(Debug builds only) start is at or beyond the end of the text</exception>
        public List<List<T>> AllStartsWithAny(String text, int start = 0)
        {
            StringTree.ValidateSearch(text, start);
            StringTreeList<T> node = this;
            int len = text.Length;
            List<List<T>> found = new();
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
        /// Find the values of all strings (in the tree) that is a prefix of the text
        /// </summary>
        /// <param name="text">The text to find prefixes (in the tree) for, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>A new list of the value lists (the internal lists, must not be modified), ordered from shortest match to longest match</returns>
        /// <exception cref="Exception">(Debug builds only) start is at or beyond the end of the text</exception>
        public List<List<T>> PrefixesOf(String text, int start = 0)
        {
            StringTree.ValidateSearch(text, start);
            StringTreeList<T> node = this;
            int len = text.Length;
            List<List<T>> found = new();
            if (node.Leaf == LeafList)
            {
                while (start < len)
                {
                    var val = node.Leaf;
                    if (val != null)
                        if (val.Count > 0)
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
                var val = node.Leaf;
                if (val != null)
                    if (val.Count > 0)
                        found.Add(val);
            }
            return found;
        }

        /// <summary>
        /// Add the values of all strings below a node (not including the node itself), ordered by char
        /// </summary>
        void InternalAddAllInOrder(List<List<T>> found, StringTreeList<T> node)
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
        /// Add the values of all strings below a node (not including the node itself), in any order
        /// </summary>
        void InternalAddAll(List<List<T>> found, StringTreeList<T> node)
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
        /// Get the values of all strings contained in the tree, in any order
        /// </summary>
        /// <returns>The value lists (the internal lists, must not be modified), lazily enumerated (recursive)</returns>
        public IEnumerable<IReadOnlyList<T>> GetAll()
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
        /// Get the values of all strings contained in the tree, ordered by the string chars (ordinal, upper cased for case in-sensitive trees), a string comes before the strings that it's a prefix of
        /// </summary>
        /// <returns>The value lists (the internal lists, must not be modified), lazily enumerated (recursive)</returns>
        public IEnumerable<IReadOnlyList<T>> GetAllInOrder()
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
        /// Get the values of all strings contained in the tree, in the reverse order of <see cref="GetAllInOrder"/>
        /// </summary>
        /// <returns>The value lists (the internal lists, must not be modified), lazily enumerated (recursive)</returns>
        public IEnumerable<IReadOnlyList<T>> GetAllInReverseOrder()
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
        /// Add the chars of a string from an index (recursive), the value is appended to the leaf list of the last node
        /// </summary>
        /// <param name="text">The string to add</param>
        /// <param name="value">The value</param>
        /// <param name="current">The node for the chars before index c, or null to create it</param>
        /// <param name="caseInSensitive">True to upper case the chars</param>
        /// <param name="c">The index of the next char to add</param>
        /// <returns>The node for the chars before index c</returns>
        static StringTreeList<T> InternalAdd(String text, T value, StringTreeList<T> current, bool caseInSensitive, int c = 0)
        {
            if (c == text.Length)
            {
                if (current != null)
                {
                    var data = current.Leaf ?? new List<T>();
                    current.Leaf = data;
                    data.Add(value);
                    return current;
                }
                return new StringTreeList<T>(text, value);
            }
            var cc = text[c];
            if (caseInSensitive)
                cc = CharExt.FastUpper(cc);
            bool exists = false;
            ++c;
            if (current != null)
            {
                if (current.Nodes == null)
                    current.Nodes = new Dictionary<char, StringTreeList<T>>();
                exists = current.Nodes.TryGetValue(cc, out var ex);
                if (exists)
                    InternalAdd(text, value, ex, caseInSensitive, c);
            }
            var nc = current ?? new StringTreeList<T>();
            if (!exists)
            {
                var n = InternalAdd(text, value, null, caseInSensitive, c);
                nc.Nodes?.Add(cc, n);
            }
            return nc;
        }


        /// <summary>
        /// Same as the other InternalAdd overload (never fails, an existing string gets the value appended)
        /// </summary>
        /// <returns>Always true</returns>
        static bool InternalAdd(out StringTreeList<T> res, String text, T value, StringTreeList<T> current, bool caseInSensitive, int c = 0)
        {
            if (c == text.Length)
            {
                if (current != null)
                {
                    var data = current.Leaf ?? new List<T>();
                    current.Leaf = data;
                    data.Add(value);
                    res = current;
                    return true;
                }
                res = new StringTreeList<T>(text, value);
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
                    current.Nodes = new Dictionary<char, StringTreeList<T>>();
                exists = current.Nodes.TryGetValue(cc, out var ex);
                if (exists)
                {
                    if (!InternalAdd(out var _, text, value, ex, caseInSensitive, c))
                    {
                        res = null;
                        return false;
                    }
                }
            }
            var nc = current ?? new StringTreeList<T>();
            if (!exists)
            {
                if (!InternalAdd(out var n, text, value, null, caseInSensitive, c))
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
        /// The values of the string that ends at this node (or null), for the root it's the case in-sensitive marker (<see cref="LeafList"/>) or null
        /// </summary>
        List<T> Leaf;

        /// <summary>
        /// The child nodes (keyed by the next char, upper cased for case in-sensitive trees), or null
        /// </summary>
        Dictionary<Char, StringTreeList<T>> Nodes;

        /// <summary>
        /// The values of the string that ends at this node (or null), for the root node it's the case in-sensitive marker (an empty list) or null
        /// </summary>
        internal List<T> GetLeaf() => Leaf;

        /// <summary>
        /// The child nodes (keyed by the next char, upper cased for case in-sensitive trees), or null
        /// </summary>
        internal Dictionary<Char, StringTreeList<T>> GetNodes() => Nodes;



        /// <summary>
        /// The number of <see cref="StringTreeList{T}"/> nodes (of this T) that are currently allocated (created and not yet finalized), for diagnostics
        /// </summary>
        public static long AllocatedNodes => Interlocked.Read(ref CountAllocNodes);

        /// <summary>
        /// The number of allocated nodes (see <see cref="AllocatedNodes"/>)
        /// </summary>
        static long CountAllocNodes;

        ~StringTreeList()
        {
            Interlocked.Decrement(ref CountAllocNodes);
        }

        StringTreeList(string leaf, T value)
        {
            Leaf = new List<T>
            {
                value
            };
            Interlocked.Increment(ref CountAllocNodes);
        }


        /// <summary>
        /// Create an empty tree (root node)
        /// </summary>
        /// <param name="caseInSesnitive">True to make a case in-sensitive tree</param>
        public StringTreeList(bool caseInSesnitive = false)
        {
            Leaf = caseInSesnitive ? LeafList : null;
            Nodes = new();
            Interlocked.Increment(ref CountAllocNodes);
        }

        StringTreeList(List<T> leaf, Dictionary<Char, StringTreeList<T>> nodes)
        {
            Leaf = leaf;
            Nodes = nodes;
            Interlocked.Increment(ref CountAllocNodes);
        }

        /// <summary>
        /// Make a copy of a tree (all nodes and value lists are copied, the values themselves are not cloned)
        /// </summary>
        /// <returns>The copy</returns>
        public StringTreeList<T> Clone()
        {
            Dictionary<Char, StringTreeList<T>> nodes = null;
            var en = Nodes;
            if (en != null)
            {
                nodes = new Dictionary<char, StringTreeList<T>>();
                foreach (var n in en)
                    nodes.Add(n.Key, n.Value.Clone());
            }
            var leaf = Leaf;
            return new StringTreeList<T>((leaf == null) || (leaf == LeafList) ? leaf : new List<T>(leaf), nodes);
        }


    }

}
