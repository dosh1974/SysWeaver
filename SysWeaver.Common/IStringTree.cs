using System;

namespace SysWeaver
{
    /// <summary>
    /// A set of strings that can find the longest contained string that a text starts with (a prefix lookup).
    /// Implemented by <see cref="StringTree"/>, <see cref="FrozenStringTree"/>, <see cref="CompactStringTree"/> and <see cref="StringPrefixLookup"/>,
    /// see <see cref="IStringTreeExt"/> for search helpers built on top of it.
    /// </summary>
    public interface IStringTree
    {
        /// <summary>
        /// Find the longest string (in the tree) that the text starts with (at the start offset)
        /// </summary>
        /// <param name="text">The text to match against the strings in the tree</param>
        /// <param name="start">An optional start offset. Most implementations require it to be less than the length of the text (searching for an empty text throws in debug builds)</param>
        /// <returns>The longest found match or null if no match is found.
        /// For case in-sensitive trees the returned string is the stored string, except for <see cref="CompactStringTree"/> that returns the matching part of the text</returns>
        String StartsWithAny(String text, int start = 0);

    }




}
