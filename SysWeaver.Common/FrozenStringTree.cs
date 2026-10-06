using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// A string tree stores a bunch of strings in a way that makes it fast to check if a test string starts with ANY of the contained strings.
    /// Empty strings are not supported, they can't be added and can't be searched for (throws in debug builds).
    /// The leaf of a string is the string itself (see <see cref="FrozenStringTrie"/> for the implementation).
    /// An immutable (thread safe) and faster version of <see cref="StringTree"/>, with the same results.
    /// </summary>
    public sealed class FrozenStringTree : FrozenStringTrie, IStringTree
    {
        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null, empty strings or duplicates (duplicates after case folding for case in-sensitive trees)</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        /// <exception cref="Exception">A string is a duplicate, or (in debug builds only) a string is empty</exception>
        public static FrozenStringTree Build(IEnumerable<String> strings, bool caseInSensitive = false)
            => new FrozenStringTree(StringTree.Build(strings, caseInSensitive));

        /// <summary>
        /// Find the longest string (in the tree), that the text starts with (at the start offset)
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text (in release builds a start at or beyond the end returns null)</param>
        /// <returns>The longest found match (the added string) or null if no match is found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public String StartsWithAny(String text, int start = 0) => Unsafe.As<String>(StartsWithAnyLeaf(text, start));

        /// <summary>
        /// Find the strings that are a prefix of the text, followed by the strings that "complete" the text (same as <see cref="StringTree.AllStartsWithAny(string, int)"/>).
        /// If not even the first char matches, all strings in the tree are returned.
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>A new list of matches, ordered from shortest match to longest match (followed by the strings below the deepest match, ordered by char)</returns>
        public List<String> AllStartsWithAny(String text, int start = 0) => AllStartsWithAnyLeafs<String>(text, start);

        /// <summary>
        /// Find all strings (in the tree), that is a prefix of the text
        /// </summary>
        /// <param name="text">The text to find prefixes (in the tree) for, may not be empty (from the start offset)</param>
        /// <param name="start">An optional start offset, must be less than the length of the text</param>
        /// <returns>A new list of matches, ordered from shortest match to longest match</returns>
        public List<String> PrefixesOf(String text, int start = 0) => PrefixesOfLeafs<String>(text, start);

        /// <summary>
        /// Get all strings contained in the string tree, in any order (node order)
        /// </summary>
        public IEnumerable<String> GetAll() => GetAllLeafs<String>();

        /// <summary>
        /// Get all strings contained in the string tree, ordered by char (ordinal, of the upper cased chars for case in-sensitive trees), a string comes before the strings that it's a prefix of
        /// </summary>
        public IEnumerable<String> GetAllInOrder() => GetAllLeafsInOrder<String>();

        /// <summary>
        /// Get all strings contained in the string tree, in the reverse order of <see cref="GetAllInOrder"/>
        /// </summary>
        public IEnumerable<String> GetAllInReverseOrder() => GetAllLeafsInReverseOrder<String>();

        /// <summary>
        /// The total number of trie nodes of all <see cref="FrozenStringTree"/> instances that are currently allocated (created and not yet finalized), for diagnostics
        /// </summary>
        public static long AllocatedNodes => Interlocked.Read(ref CountAllocNodes);

        /// <summary>
        /// The number of allocated nodes (see <see cref="AllocatedNodes"/>)
        /// </summary>
        static long CountAllocNodes;

        ~FrozenStringTree()
        {
            Interlocked.Add(ref CountAllocNodes, -NodeCount);
        }

        /// <summary>
        /// Create a frozen copy of a string tree (the source tree isn't used after construction)
        /// </summary>
        /// <param name="tree">The tree to copy, the casing is taken from the tree</param>
        public FrozenStringTree(StringTree tree) : base(Source(tree, true), tree.GetLeaf() != null)
        {
            Interlocked.Add(ref CountAllocNodes, NodeCount);
        }

        /// <summary>
        /// Convert a string tree to the source of the trie
        /// </summary>
        /// <param name="tree">The tree</param>
        /// <param name="isRoot">True for the root node (the root leaf is the case in-sensitive marker)</param>
        static FrozenStringTrieSource Source(StringTree tree, bool isRoot)
        {
            var n = tree.GetNodes();
            List<KeyValuePair<Char, FrozenStringTrieSource>> children = null;
            if (n != null)
            {
                children = new(n.Count);
                foreach (var x in n)
                    children.Add(KeyValuePair.Create(x.Key, Source(x.Value, false)));
            }
            return new FrozenStringTrieSource
            {
                Leaf = isRoot ? null : tree.GetLeaf(),
                Children = children,
            };
        }
    }

}
