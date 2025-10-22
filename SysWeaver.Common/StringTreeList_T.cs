using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// A string tree stores a bunch of strings in a way that makes it fast to check if a test string starts with ANY of the contained strings.
    /// </summary>
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
        /// True if the tree is case in-sensitive
        /// </summary>
        public bool IsCaseInSensitive => Leaf == LeafList;

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static StringTreeList<T> Build(IEnumerable<Tuple<String, T>> strings, bool caseInSensitive = false)
        {
            StringTreeList<T> parent = null;
            foreach (var s in strings)
                parent = InternalAdd(s.Item1, s.Item2, parent, caseInSensitive);
            parent = parent ?? new StringTreeList<T>();
            if (caseInSensitive)
                parent.Leaf = LeafList;
            return parent;
        }

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static StringTreeList<T> Build(IEnumerable<KeyValuePair<String, T>> strings, bool caseInSensitive = false)
        {
            StringTreeList<T> parent = null;
            foreach (var s in strings)
                parent = InternalAdd(s.Key, s.Value, parent, caseInSensitive);
            parent = parent ?? new StringTreeList<T>();
            if (caseInSensitive)
                parent.Leaf = LeafList;
            return parent;
        }

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="values">The values to add, may not contain null</param>
        /// <param name="getKey">Function that extracts the string key</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static StringTreeList<T> Build(IEnumerable<T> values, Func<T, String> getKey, bool caseInSensitive = false)
        {
            StringTreeList<T> parent = null;
            foreach (var s in values)
                parent = InternalAdd(getKey(s), s, parent, caseInSensitive);
            parent = parent ?? new StringTreeList<T>();
            if (caseInSensitive)
                parent.Leaf = LeafList;
            return parent;
        }


        static readonly List<T> LeafList = new();


        /// <summary>
        /// Add a string to a new or existing tree
        /// </summary>
        /// <param name="text">The string to add, may not be null</param>
        /// <param name="value">The value associated with the string, may not be null</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree, if the tree already exists, the casing from that tree is used</param>
        /// <param name="parent">An existing tree</param>
        /// <returns>The new tree (or the existing)</returns>
        public static StringTreeList<T> Add(String text, T value, bool caseInSensitive = false, StringTreeList<T> parent = null)
        {
            if (parent != null)
                caseInSensitive = parent.IsCaseInSensitive;
            parent = InternalAdd(text, value, parent, caseInSensitive);
            parent = parent ?? new StringTreeList<T>();
            if (caseInSensitive)
                parent.Leaf = LeafList;
            return parent;
        }


        /// <summary>
        /// Try to add a string to a new or existing tree
        /// </summary>
        /// <param name="parent">An existing or new tree to update</param>
        /// <param name="text">The string to add, may not be null</param>
        /// <param name="value">The value associated with the string, may not be null</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree, if the tree already exists, the casing from that tree is used</param>
        /// <returns>True if the string was added, false if it already existed</returns>
        public static bool TryAdd(ref StringTreeList<T> parent, String text, T value, bool caseInSensitive = false)
        {
            if (parent != null)
                caseInSensitive = parent.IsCaseInSensitive;
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
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>The longest found match or null if no match is found</returns>
        public IReadOnlyList<T> StartsWithAny(String text, int start = 0)
        {
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
        /// Find all matching strings (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>A list of matches, ordered by name</returns>
        public List<List<T>> AllStartsWithAny(String text, int start = 0)
        {
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
        /// Find all matching strings (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>A list of matches, ordered by name</returns>
        public List<List<T>> PrefixesOf(String text, int start = 0)
        {
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
        /// Get all string contained in the string tree, ordered by key
        /// </summary>
        /// <returns></returns>
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
        /// Get all string contained in the string tree, ordered by key
        /// </summary>
        /// <returns></returns>
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
        /// Get all string contained in the string tree, in any order
        /// </summary>
        /// <returns></returns>
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
        /// Get all string contained in the string tree, ordered by key
        /// </summary>
        /// <returns></returns>
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
        /// Get all string contained in the string tree, ordered by key
        /// </summary>
        /// <returns></returns>
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

        List<T> Leaf;
        Dictionary<Char, StringTreeList<T>> Nodes;


        public static long AllocatedNodes => Interlocked.Read(ref CountAllocNodes);

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
        /// Make a copy of a tree
        /// </summary>
        /// <returns></returns>
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
            return new StringTreeList<T>(Leaf, nodes);
        }


    }



}
