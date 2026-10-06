using System;

namespace SysWeaver
{
    /// <summary>
    /// Search extension methods for <see cref="IStringTree"/> instances (find contained strings anywhere in a text, not only at the start)
    /// </summary>
    public static class IStringTreeExt
    {

        /// <summary>
        /// Find the index of the first position in the text where any string (from the tree) starts.
        /// Calls <see cref="IStringTree.StartsWithAny(string, int)"/> at every position from <paramref name="start"/>, so it's O(n * depth).
        /// </summary>
        /// <param name="tree">The tree to use</param>
        /// <param name="match">The longest string that matches at the returned position, or null if no match is found</param>
        /// <param name="text">The text to find the first matching string in, may not be null</param>
        /// <param name="start">An optional start offset (positions before it are not tested)</param>
        /// <returns>The position of the first matching string or -1 if no match is found</returns>
        public static int IndexOfAny(this IStringTree tree, out String match, String text, int start = 0)
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
        /// Find the index of the last position in the text where any string (from the tree) starts.
        /// Calls <see cref="IStringTree.StartsWithAny(string, int)"/> at every position, going backwards from <paramref name="start"/> - 1.
        /// </summary>
        /// <param name="tree">The tree to use</param>
        /// <param name="match">The longest string that matches at the returned position, or null if no match is found</param>
        /// <param name="text">The text to find the last matching string in, may not be null</param>
        /// <param name="start">An optional exclusive end position (the first tested position is start - 1), a negative value or a value greater than the length of the text starts at the end of the text</param>
        /// <returns>The position of the last matching string or -1 if no match is found</returns>

        public static int LastIndexOfAny(this IStringTree tree, out String match, String text, int start = -1)
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



        /// <summary>
        /// Find the strings (from the tree) that starts at a word start in a text, and execute a function for every match.
        /// Word starts are found using <see cref="StringTools.OnWordStart(string, Func{int, bool}, int)"/> (a letter or digit that follows a non letter or digit).
        /// </summary>
        /// <param name="tree">The tree to use</param>
        /// <param name="text">The text to search, may not be null</param>
        /// <param name="onMatch">Called for every match with the position and the longest matching string, return false to stop the search</param>
        /// <param name="start">An optional start offset, if it's inside a word the search starts at the next word</param>
        /// <param name="matchWholeWord">If true, a match is ignored if the char after it is a letter or digit (the match must end at a word end)</param>
        public static void OnFoundWordsInText(this IStringTree tree, String text, Func<int, String, bool> onMatch, int start = 0, bool matchWholeWord = true)
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
