using System;

namespace SysWeaver
{
    /// <summary>
    /// Extension methods to StringTree instances
    /// </summary>
    public static class IStringTreeExt
    {

        /// <summary>
        /// Find the index of the first matching string (from the tree)
        /// </summary>
        /// <param name="tree">The tree to use</param>
        /// <param name="match">The first matching string (if found) or null</param>
        /// <param name="text">The text to find the first matching string in</param>
        /// <param name="start">An optional start offset</param>
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
        /// Find the index of the last matching string (from the tree)
        /// </summary>
        /// <param name="tree">The tree to use</param>
        /// <param name="match">The last  matching string (if found) or null</param>
        /// <param name="text">The text to find the last  matching string in</param>
        /// <param name="start">An optional start offset, or -1 to start at the end of the string</param>
        /// <returns>The position of the last  matching string or -1 if no match is found</returns>

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
