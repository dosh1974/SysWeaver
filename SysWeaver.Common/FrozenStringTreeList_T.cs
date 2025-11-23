using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// A string tree stores a bunch of strings in a way that makes it fast to check if a test string starts with ANY of the contained strings.
    /// </summary>
    public sealed class FrozenStringTreeList<T>
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
        public static FrozenStringTreeList<T> Build(IEnumerable<Tuple<String, T>> strings, bool caseInSensitive = false)
            => new FrozenStringTreeList<T>(StringTreeList<T>.Build(strings, caseInSensitive));

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static FrozenStringTreeList<T> Build(IEnumerable<KeyValuePair<String, T>> strings, bool caseInSensitive = false)
            => new FrozenStringTreeList<T>(StringTreeList<T>.Build(strings, caseInSensitive));

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="values">The values to add, may not contain null</param>
        /// <param name="getKey">Function that extracts the string key</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static FrozenStringTreeList<T> Build(IEnumerable<T> values, Func<T, String> getKey, bool caseInSensitive = false)
            => new FrozenStringTreeList<T>(StringTreeList<T>.Build(values, getKey, caseInSensitive));

        static readonly IReadOnlyList<T> LeafList = Array.Empty<T>();

        /// <summary>
        /// Find the longest string (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>The longest found match or null if no match is found</returns>
        public IReadOnlyList<T> StartsWithAny(String text, int start = 0)
        {
            FrozenStringTreeList<T> node = this;
            int len = text.Length;
            IReadOnlyList<T> found = null;
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
        public List<IReadOnlyList<T>> AllStartsWithAny(String text, int start = 0)
        {
            FrozenStringTreeList<T> node = this;
            int len = text.Length;
            List<IReadOnlyList<T>> found = new();
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
        public List<IReadOnlyList<T>> PrefixesOf(String text, int start = 0)
        {
            FrozenStringTreeList<T> node = this;
            int len = text.Length;
            List<IReadOnlyList<T>> found = new();
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
        void InternalAddAllInOrder(List<IReadOnlyList<T>> found, FrozenStringTreeList<T> node)
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
        void InternalAddAll(List<IReadOnlyList<T>> found, FrozenStringTreeList<T> node)
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



        readonly IReadOnlyList<T> Leaf;
        readonly IReadOnlyDictionary<Char, FrozenStringTreeList<T>> Nodes;


        public static long AllocatedNodes => Interlocked.Read(ref CountAllocNodes);

        static long CountAllocNodes;

        ~FrozenStringTreeList()
        {
            Interlocked.Decrement(ref CountAllocNodes);
        }

        public static readonly FrozenStringTreeList<T> Empty = new FrozenStringTreeList<T>();

        FrozenStringTreeList(bool caseInSesnitive = false)
        {
            Leaf = caseInSesnitive ? LeafList : null;
            Nodes = new Dictionary<Char, FrozenStringTreeList<T>>().Freeze();
            Interlocked.Increment(ref CountAllocNodes);
        }

        public FrozenStringTreeList(StringTreeList<T> tree)
        {
            Leaf = tree.IsCaseInSensitive ? LeafList : tree.GetLeaf()?.ToArray();
            var n = tree.GetNodes();
            if (n != null)
            {
                var d = new Dictionary<Char, FrozenStringTreeList<T>>(n.Comparer);
                foreach (var x in n)
                    d.Add(x.Key, new FrozenStringTreeList<T>(x.Value));
                Nodes = d.Freeze();
            }
            Interlocked.Increment(ref CountAllocNodes);
        }

/*
        /// <summary>
        /// Make a copy of a tree
        /// </summary>
        /// <returns></returns>
        public FrozenStringTreeList<T> Clone() => this;
*/

    }

}
