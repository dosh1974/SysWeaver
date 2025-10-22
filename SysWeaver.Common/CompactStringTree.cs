using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;


namespace SysWeaver
{

    public sealed class CompactCharDictionary<T> : IEnumerable<KeyValuePair<Char, T>>
    {
        public int Count => Data?.Length ?? 0;

        public void Add(Char key, T value)
        {
            var d = Data;
            if (d == null)
            {
                d = Alloc(1);
                d[0] = new E(key, value);
                Data = d;
                return;
            }
            var dl = d.Length;
            var n = Alloc(dl + 1);
            for (int i = 0; i < dl; ++i)
                n[i] = d[i];
            n[dl] = new E(key, value);
            Array.Sort(n);
            Free(d);
            Data = n;
        }

        public bool TryGetValue(Char key, out T value)
        {
            var d = Data;
            if (d == null)
            {
                value = default(T); 
                return false;
            }
            var dl = d.Length;
            if (dl < 8)
            {
                for (int i = 0; i < dl; ++ i)
                {
                    var e = d[i];
                    if (e.Key == key)
                    {
                        value = e.Value;
                        return true;
                    }
                }
                value = default(T);
                return false;
            }
            var fi = BinarySearch.Find(0, dl, key, x => d[x].Key);
            if (fi < 0)
            {
                value = default(T);
                return false;
            }
            value = d[fi].Value;
            return true;
        }

        public IEnumerator<KeyValuePair<char, T>> GetEnumerator()
        {
            var d = Data;
            if (d != null)
            {
                foreach (var x in d)
                    yield return new KeyValuePair<Char, T>(x.Key, x.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        sealed class E : IEquatable<E>, IComparable<E>
        {
            public override string ToString()
                 => String.Concat('\'', Key, "': ", Value);

            public override bool Equals(object obj)
                => Equals(obj as E);

            public override int GetHashCode()
                => Key;

            public bool Equals(E other)
            {
                if (other == null)
                    return false;
                if (other.Key == Key)
                    throw new Exception("Key already exist!");
                return false;
            }

            public int CompareTo(E other)
                => Key - other.Key;

            public readonly T Value;
            public readonly Char Key;

            public E(char key, T value)
            {
                Key = key;
                Value = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        E[] Data;


        #region Array allocator

        sealed class ArrayCache
        {
            public E[] Alloc(int len)
            {
                if (S.TryPop(out var e))
                {
                    Interlocked.Decrement(ref Count);
                    return e;
                }
                e = new E[len + 1];
                return e;
            }

            public void Free(E[] data)
            {
                if (Interlocked.Increment(ref Count) > 1024)
                {
                    Interlocked.Decrement(ref Count);
                    return;
                }
                S.Push(data);
            }

            int Count;
            readonly ConcurrentStack<E[]> S = new ConcurrentStack<E[]>();
        }

        static E[] Alloc(int size)
        {
            --size;
            if (size < 32)
                return Cache[size].Alloc(size);
            return new E[size + 1];
        }

        static void Free(E[] d)
        {
            int l = d.Length - 1;
            if (l < 32)
            {
                Cache[l].Free(d);
                return;
            }
        }

        static readonly ArrayCache[] Cache = Enumerable.Range(0, 32).Select(x => new ArrayCache()).ToArray();


        #endregion // Array allocator




    }






    /// <summary>
    /// A string tree stores a bunch of strings in a way that makes it fast to check if a test string starts with ANY of the contained strings.
    /// </summary>
    public sealed class CompactStringTree
    {
        CompactCharDictionary<CompactStringTree> Nodes;
        bool IsLeaf;

#if DEBUG
        public override string ToString() => IsLeaf ? "Leaf" : ("Children: " + Nodes?.Count);
#endif//DEBUG

        /// <summary>
        /// True if the tree is case in-sensitive
        /// </summary>
        public bool IsCaseInSensitive => IsLeaf;

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
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
        /// <param name="text">The string to add, may not be null</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree, if the tree already exists, the casing from that tree is used</param>
        /// <param name="parent">An existing tree</param>
        /// <returns>The new tree (or the existing)</returns>
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
        /// <param name="parent">An existing or new tree to update</param>
        /// <param name="text">The string to add, may not be null</param>
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
        /// Find the longest string (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>The longest found match or null if no match is found</returns>
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
        /// Check if a string is already contained in the tree
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>True if string exists</returns>
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
        /// Find all matching strings (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>A list of matches, orderer from shortest match to longest match</returns>
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
        /// Get all string contained in the string tree, in any order
        /// </summary>
        /// <returns></returns>
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
                    sb.Append(x);
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
        /// Get all string contained in the string tree, ordered by key
        /// </summary>
        /// <returns></returns>
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
                    sb.Append(x);
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
        /// Get all string contained in the string tree, ordered by key
        /// </summary>
        /// <returns></returns>
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
                    sb.Append(x);
                    var next = x.Value;
                    var isLeaf = next.IsLeaf;
                    foreach (var r in next.GetAllInReverseOrder())
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



        public static long AllocatedNodes => Interlocked.Read(ref CountAllocNodes);

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
        /// Make a copy of a tree
        /// </summary>
        /// <returns></returns>
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

    /// <summary>
    /// Extension methods to CompactStringTree instances
    /// </summary>
    public static class CompactStringTreeExt
    {

        /// <summary>
        /// Find the index of the first matching string (from the tree)
        /// </summary>
        /// <param name="tree">The tree to use</param>
        /// <param name="match">The first matching string (if found) or null</param>
        /// <param name="text">The text to find the first matching string in</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>The position of the first matching string or -1 if no match is found</returns>
        public static int IndexOfAny(this CompactStringTree tree, out String match, String text, int start = 0)
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

        public static int LastIndexOfAny(this CompactStringTree tree, out String match, String text, int start = -1)
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




        public static void OnFoundWordsInText(this CompactStringTree tree, String text, Func<int, String, bool> onMatch, int start = 0, bool matchWholeWord = true)
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

