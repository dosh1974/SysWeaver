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
    /// The leaf of a string is the values that was added with that string (in the order that they where added), see <see cref="FrozenStringTrie"/> for the implementation.
    /// Empty strings are not supported, they can't be added (throws an <see cref="ArgumentException"/>) and can't be searched for (throws in debug builds).
    /// An immutable (thread safe) and faster version of <see cref="StringTreeList{T}"/>, with the same results (but the returned value lists are read only).
    /// For case sensitive lookups <see cref="StringPrefixLookup.BuildList{T}(IEnumerable{Tuple{string, T}})"/> is a faster drop in replacement.
    /// </summary>
    /// <typeparam name="T">The type of the values</typeparam>
    public sealed class FrozenStringTreeList<T> : FrozenStringTrie
    {
        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings (and values) to build a tree from, may not contain null or empty strings (duplicate strings are allowed, their values are kept in order)</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static FrozenStringTreeList<T> Build(IEnumerable<Tuple<String, T>> strings, bool caseInSensitive = false)
            => new FrozenStringTreeList<T>(StringTreeList<T>.Build(strings, caseInSensitive));

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings (and values) to build a tree from, may not contain null or empty strings (duplicate strings are allowed, their values are kept in order)</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static FrozenStringTreeList<T> Build(IEnumerable<KeyValuePair<String, T>> strings, bool caseInSensitive = false)
            => new FrozenStringTreeList<T>(StringTreeList<T>.Build(strings, caseInSensitive));

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="values">The values to add</param>
        /// <param name="getKey">Function that extracts the string key (may not return null or an empty string, values with the same key are kept in order)</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static FrozenStringTreeList<T> Build(IEnumerable<T> values, Func<T, String> getKey, bool caseInSensitive = false)
            => new FrozenStringTreeList<T>(StringTreeList<T>.Build(values, getKey, caseInSensitive));

        /// <summary>
        /// Find the values of the longest string (in the tree), that the text starts with (at the start offset)
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text (in release builds a start at or beyond the end returns null)</param>
        /// <returns>The values of the longest found match (a shared read only list) or null if no match is found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<T> StartsWithAny(String text, int start = 0) => Unsafe.As<Values>(StartsWithAnyLeaf(text, start));

        /// <summary>
        /// Find the values of the strings that are a prefix of the text, followed by the values of the strings that "complete" the text (same as <see cref="StringTreeList{T}.AllStartsWithAny(string, int)"/>).
        /// If not even the first char matches, the values of all strings in the tree are returned.
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>A new list with the values of the matches, ordered from shortest match to longest match (followed by the values below the deepest match, ordered by char)</returns>
        public List<IReadOnlyList<T>> AllStartsWithAny(String text, int start = 0) => AllStartsWithAnyLeafs<IReadOnlyList<T>>(text, start);

        /// <summary>
        /// Get the values of all strings contained in the tree, in any order
        /// </summary>
        public IEnumerable<IReadOnlyList<T>> GetAll() => GetAllLeafs<IReadOnlyList<T>>();

        /// <summary>
        /// Get the values of all strings contained in the tree, ordered by the string chars (ordinal, upper cased for case in-sensitive trees), a string comes before the strings that it's a prefix of
        /// </summary>
        public IEnumerable<IReadOnlyList<T>> GetAllInOrder() => GetAllLeafsInOrder<IReadOnlyList<T>>();

        /// <summary>
        /// Get the values of all strings contained in the tree, in the reverse order of <see cref="GetAllInOrder"/>
        /// </summary>
        public IEnumerable<IReadOnlyList<T>> GetAllInReverseOrder() => GetAllLeafsInReverseOrder<IReadOnlyList<T>>();

        /// <summary>
        /// Find the values of all strings (in the tree), that is a prefix of the text
        /// </summary>
        /// <param name="text">The text to find prefixes (in the tree) for, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text (in release builds a start at or beyond the end finds nothing)</param>
        /// <returns>The values of the matches, ordered from shortest match to longest match (an empty list if there are none). The returned list is precomputed and shared (by all searches with the same result), it must not be modified (no allocations)</returns>
        public IReadOnlyList<IReadOnlyList<T>> PrefixesOf(String text, int start = 0)
        {
            // All strings that are a prefix of the text are prefixes of the longest one, so the result is precomputed for every string
            var leaf = Unsafe.As<Values>(StartsWithAnyLeaf(text, start));
            return leaf == null ? NoPrefixes : leaf.Prefixes;
        }

        /// <summary>
        /// Execute an async function for the values of every string that is a prefix of the text (see <see cref="PrefixesOf(string, int)"/>), shortest match first, until the function returns a non null value
        /// </summary>
        /// <typeparam name="E">The type of the result</typeparam>
        /// <typeparam name="A0">The type of the first argument</typeparam>
        /// <typeparam name="A1">The type of the second argument</typeparam>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="fn">Async function to execute for the values of every match (shortest match first), return a non null value to stop the enumeration</param>
        /// <param name="a0">The first argument that is passed to the function (avoids a closure)</param>
        /// <param name="a1">The second argument that is passed to the function (avoids a closure)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>The first non null value returned by fn, or null if all calls returned null (or there are no matches).
        /// Note that the continuations are awaited with ConfigureAwait(true) (the synchronization context is captured)</returns>
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

#if DEBUG
        /// <summary>
        /// The total number of trie nodes of all <see cref="FrozenStringTreeList{T}"/> instances (of this T) that are currently allocated (created and not yet finalized), for diagnostics (debug builds only)
        /// </summary>
        public static long AllocatedNodes => Interlocked.Read(ref CountAllocNodes);

        /// <summary>
        /// The number of allocated nodes (see <see cref="AllocatedNodes"/>)
        /// </summary>
        static long CountAllocNodes;

        ~FrozenStringTreeList()
        {
            Interlocked.Add(ref CountAllocNodes, -NodeCount);
        }
#endif//DEBUG

        /// <summary>
        /// An empty (case sensitive) tree, nothing is ever found
        /// </summary>
        public static readonly FrozenStringTreeList<T> Empty = new FrozenStringTreeList<T>(new StringTreeList<T>());

        /// <summary>
        /// Create a frozen copy of a string tree list (the values are copied, the source tree isn't used after construction)
        /// </summary>
        /// <param name="tree">The tree to copy, the casing is taken from the tree</param>
        public FrozenStringTreeList(StringTreeList<T> tree) : base(Source(tree, null), tree.IsCaseInSensitive)
        {
#if DEBUG
            Interlocked.Add(ref CountAllocNodes, NodeCount);
#endif//DEBUG
        }

        #region Implementation

        /// <summary>
        /// The (shared) result of <see cref="PrefixesOf(string, int)"/> when nothing matches
        /// </summary>
        static readonly IReadOnlyList<IReadOnlyList<T>> NoPrefixes = Array.Empty<IReadOnlyList<T>>();

        /// <summary>
        /// The values of a string (the leaf), with the values of all strings that are a prefix of it
        /// </summary>
        sealed class Values : IReadOnlyList<T>
        {
            /// <summary>
            /// Create the values of a string
            /// </summary>
            /// <param name="items">The values (owned by this instance)</param>
            /// <param name="parentPrefixes">The values of all strings that are a prefix of this string (excluding this), shortest first</param>
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

            /// <inheritdoc/>
            public T this[int index] => Items[index];

            /// <inheritdoc/>
            public int Count => Items.Length;

            /// <inheritdoc/>
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
