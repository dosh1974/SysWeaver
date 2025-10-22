using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SysWeaver
{


    /// <summary>
    /// A string tree stores a bunch of strings in a way that makes it fast to check if a test string starts with ANY of the contained strings.
    /// </summary>
    public sealed class StringTree
    {

#if DEBUG
        public override string ToString() => Leaf != null ? String.Join(Leaf, "Leaf \"", '"') : ("Children: " + Nodes?.Count);
#endif//DEBUG

        /// <summary>
        /// True if the tree is case in-sensitive
        /// </summary>
        public bool IsCaseInSensitive => Leaf != null;

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static StringTree Build(IEnumerable<String> strings, bool caseInSensitive = false)
        {
            StringTree parent = null;
            foreach (var s in strings)
                parent = InternalAdd(s, parent, caseInSensitive);
            parent = parent ?? new StringTree();
            if (caseInSensitive)
                parent.Leaf = "caseInSensitive";
            return parent;

        }

        /// <summary>
        /// Add a string to a new or existing tree
        /// </summary>
        /// <param name="text">The string to add, may not be null</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree, if the tree already exists, the casing from that tree is used</param>
        /// <param name="parent">An existing tree</param>
        /// <returns>The new tree (or the existing)</returns>
        public static StringTree Add(String text, bool caseInSensitive = false, StringTree parent = null)
        {
            if (parent != null)
                caseInSensitive = parent.IsCaseInSensitive;
            parent = InternalAdd(text, parent, caseInSensitive);
            parent = parent ?? new StringTree();
            if (caseInSensitive)
                parent.Leaf = "caseInSensitive";
            return parent;
        }


        /// <summary>
        /// Try to add a string to a new or existing tree
        /// </summary>
        /// <param name="parent">An existing or new tree to update</param>
        /// <param name="text">The string to add, may not be null</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree, if the tree already exists, the casing from that tree is used</param>
        /// <returns>True if the string was added, false if it already existed</returns>
        public static bool TryAdd(ref StringTree parent, String text, bool caseInSensitive = false)
        {
            if (parent != null)
                caseInSensitive = parent.IsCaseInSensitive;
            if (!InternalAdd(out var x, text, parent, caseInSensitive))
                return false;
            parent = x ?? new StringTree();
            if (caseInSensitive)
                parent.Leaf = "caseInSensitive";
            return true;
        }

        /// <summary>
        /// Find the longest string (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>The longest found match or null if no match is found</returns>
        public String StartsWithAny(String text, int start = 0)
        {
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
        /// Find all matching strings (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>A list of matches, orderer from shortest match to longest match</returns>
        public List<String> AllStartsWithAny(String text, int start = 0)
        {
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
        /// <param name="text">The text to find prefixes (in the tree) for </param>
        /// <param name="start">An optional start offset</param>
        /// <returns>A list of matches, ordered by name</returns>
        public List<String> PrefixesOf(String text, int start = 0)
        {
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
        /// Get all string contained in the string tree, ordered by key
        /// </summary>
        /// <returns></returns>
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
        /// Get all string contained in the string tree, ordered by key
        /// </summary>
        /// <returns></returns>
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
        /// Get all string contained in the string tree, in any order
        /// </summary>
        /// <returns></returns>
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
        /// Get all string contained in the string tree, ordered by key
        /// </summary>
        /// <returns></returns>
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
        /// Get all string contained in the string tree, ordered by key
        /// </summary>
        /// <returns></returns>
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

        String Leaf;
        Dictionary<Char, StringTree> Nodes;


        public static long AllocatedNodes => Interlocked.Read(ref CountAllocNodes);

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
        /// Make a copy of a tree
        /// </summary>
        /// <returns></returns>
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

    /// <summary>
    /// Extension methods to StringTree instances
    /// </summary>
    public static class StringTreeExt
    {

        /// <summary>
        /// Find the index of the first matching string (from the tree)
        /// </summary>
        /// <param name="tree">The tree to use</param>
        /// <param name="match">The first matching string (if found) or null</param>
        /// <param name="text">The text to find the first matching string in</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>The position of the first matching string or -1 if no match is found</returns>
        public static int IndexOfAny(this StringTree tree, out String match, String text, int start = 0)
        {
            match = null;
            var l = text.Length;
            while (start < l)
            {
                match = tree.StartsWithAny(text, start);
                if (match != null)
                    return start;
                ++start;
            }
            return -1;
        }

        /// <summary>
        /// Find the index of the last matching string (from the tree)
        /// </summary>
        /// <param name="tree">The tree to use</param>
        /// <param name="match">The last  matching string (if found) or null</param>
        /// <param name="text">The text to find the last  matching string in</param>
        /// <param name="start">An optional start offset, or -1 to start at the end of the string</param>
        /// <returns>The position of the last  matching string or -1 if no match is found</returns>

        public static int LastIndexOfAny(this StringTree tree, out String match, String text, int start = -1)
        {
            match = null;
            var l = text.Length;
            if ((start < 0) || (start > l))
                start = l;
            while (start > 0)
            {
                --start;
                match = tree.StartsWithAny(text, start);
                if (match != null)
                    return start;
            }
            return -1;
        }




        public static void OnFoundWordsInText(this StringTree tree, String text, Func<int, String, bool> onMatch, int start = 0, bool matchWholeWord = true)
        {
            var tl = text.Length;
            text.OnWordStart(i => 
            {
                var t = tree.StartsWithAny(text, i);
                if (t == null)
                    return true;
                if (matchWholeWord)
                {
                    var e = i + t.Length;
                    if (e < tl)
                        if (Char.IsLetterOrDigit(text[e]))
                            return true;
                }
                return onMatch(i, t);
            }, start);
        }

    }




}
