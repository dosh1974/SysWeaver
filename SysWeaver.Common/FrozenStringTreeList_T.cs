using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// A string tree stores a bunch of strings (with values) in a way that makes it fast to check if a test string starts with ANY of the contained strings.
    /// The leaf of a string is the values that was added with that string (in the order that they where added), see FrozenStringTrie for the implementation.
    /// Empty strings are not supported, they can't be added and can't be searched for (throws in debug builds).
    /// </summary>
    public sealed class FrozenStringTreeList<T> : FrozenStringTrie
    {
        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null or empty strings</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static FrozenStringTreeList<T> Build(IEnumerable<Tuple<String, T>> strings, bool caseInSensitive = false)
            => new FrozenStringTreeList<T>(StringTreeList<T>.Build(strings, caseInSensitive));

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null or empty strings</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static FrozenStringTreeList<T> Build(IEnumerable<KeyValuePair<String, T>> strings, bool caseInSensitive = false)
            => new FrozenStringTreeList<T>(StringTreeList<T>.Build(strings, caseInSensitive));

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="values">The values to add, may not contain null</param>
        /// <param name="getKey">Function that extracts the string key (may not return null or an empty string)</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static FrozenStringTreeList<T> Build(IEnumerable<T> values, Func<T, String> getKey, bool caseInSensitive = false)
            => new FrozenStringTreeList<T>(StringTreeList<T>.Build(values, getKey, caseInSensitive));

        /// <summary>
        /// Find the longest string (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>The values of the longest found match or null if no match is found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<T> StartsWithAny(String text, int start = 0) => Unsafe.As<Values>(StartsWithAnyLeaf(text, start));

        /// <summary>
        /// Find all matching strings (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>The values of the matches, ordered from shortest match to longest match (followed by the values below the deepest match)</returns>
        public List<IReadOnlyList<T>> AllStartsWithAny(String text, int start = 0) => AllStartsWithAnyLeafs<IReadOnlyList<T>>(text, start);

        /// <summary>
        /// Get the values of all strings contained in the tree, in any order
        /// </summary>
        public IEnumerable<IReadOnlyList<T>> GetAll() => GetAllLeafs<IReadOnlyList<T>>();

        /// <summary>
        /// Get the values of all strings contained in the tree, ordered by key
        /// </summary>
        public IEnumerable<IReadOnlyList<T>> GetAllInOrder() => GetAllLeafsInOrder<IReadOnlyList<T>>();

        /// <summary>
        /// Get the values of all strings contained in the tree, in reverse key order
        /// </summary>
        public IEnumerable<IReadOnlyList<T>> GetAllInReverseOrder() => GetAllLeafsInReverseOrder<IReadOnlyList<T>>();

        /// <summary>
        /// Find the values of all strings (in the tree), that is a prefix of the text
        /// </summary>
        /// <param name="text">The text to find prefixes (in the tree) for, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>The values of the matches, ordered from shortest match to longest match. The returned list is shared (by all searches with the same result), it must not be modified</returns>
        public IReadOnlyList<IReadOnlyList<T>> PrefixesOf(String text, int start = 0)
        {
            // All strings that are a prefix of the text are prefixes of the longest one, so the result is precomputed for every string
            var leaf = Unsafe.As<Values>(StartsWithAnyLeaf(text, start));
            return leaf == null ? NoPrefixes : leaf.Prefixes;
        }

        /// <summary>
        /// Find all matching strings (in the tree), that matches the text
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="fn">Async task to execute for every match (shortest match first), return a value to stop enumeration</param>
        /// <param name="a0">Arg0</param>
        /// <param name="a1">Arg1</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>The first value returned by fn, or null</returns>
        public async Task<E> EnumPrefixesOf<E, A0, A1>(String text, Func<IReadOnlyList<T>, A0, A1, Task<E>> fn, A0 a0, A1 a1, int start = 0) where E : class
        {
            foreach (var val in PrefixesOf(text, start))
            {
                var rv = await fn(val, a0, a1).ConfigureAwait(true);
                if (rv != null)
                    return rv;
            }
            return null;
        }

        public static long AllocatedNodes => Interlocked.Read(ref CountAllocNodes);

        static long CountAllocNodes;

        ~FrozenStringTreeList()
        {
            Interlocked.Add(ref CountAllocNodes, -NodeCount);
        }

        public static readonly FrozenStringTreeList<T> Empty = new FrozenStringTreeList<T>(new StringTreeList<T>());

        public FrozenStringTreeList(StringTreeList<T> tree) : base(Source(tree, null), tree.IsCaseInSensitive)
        {
            Interlocked.Add(ref CountAllocNodes, NodeCount);
        }

        #region Implementation

        static readonly IReadOnlyList<IReadOnlyList<T>> NoPrefixes = Array.Empty<IReadOnlyList<T>>();

        /// <summary>
        /// The values of a string (the leaf), with the values of all strings that are a prefix of it
        /// </summary>
        sealed class Values : IReadOnlyList<T>
        {
            public Values(T[] items, IReadOnlyList<T>[] parentPrefixes)
            {
                Items = items;
                var p = new IReadOnlyList<T>[parentPrefixes.Length + 1];
                parentPrefixes.CopyTo(p, 0);
                p[parentPrefixes.Length] = this;
                Prefixes = p;
            }

#if DEBUG
            public override string ToString() => String.Join(", ", Items);
#endif//DEBUG

            /// <summary>
            /// The values
            /// </summary>
            readonly T[] Items;

            /// <summary>
            /// The values of all strings that are a prefix of this string (including this), shortest first
            /// </summary>
            public readonly IReadOnlyList<T>[] Prefixes;

            public T this[int index] => Items[index];

            public int Count => Items.Length;

            public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
        }

        /// <summary>
        /// Convert a string tree list to the source of the trie
        /// </summary>
        /// <param name="tree">The tree</param>
        /// <param name="prefixes">The values of all strings that are a prefix of this node, null for the root (the root leaf is the case in-sensitive marker)</param>
        static FrozenStringTrieSource Source(StringTreeList<T> tree, IReadOnlyList<T>[] prefixes)
        {
            Values leaf = null;
            if (prefixes == null)
            {
                prefixes = [];
            }
            else
            {
                var l = tree.GetLeaf();
                if ((l != null) && (l.Count > 0))
                {
                    leaf = new Values(l.ToArray(), prefixes);
                    prefixes = leaf.Prefixes;
                }
            }
            var n = tree.GetNodes();
            List<KeyValuePair<Char, FrozenStringTrieSource>> children = null;
            if (n != null)
            {
                children = new(n.Count);
                foreach (var x in n)
                    children.Add(KeyValuePair.Create(x.Key, Source(x.Value, prefixes)));
            }
            return new FrozenStringTrieSource
            {
                Leaf = leaf,
                Children = children,
            };
        }

        #endregion
    }

}
