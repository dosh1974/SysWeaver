using System;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// Extension methods to StringTree instances
    /// </summary>
    public static class StringTreeListExt
    {


        /// <summary>
        /// Find the index of the first matching string (from the tree)
        /// </summary>
        /// <param name="tree">The tree to use</param>
        /// <param name="match">The first matching string (if found) or null</param>
        /// <param name="text">The text to find the first matching string in</param>
        /// <param name="start">An optional start offset</param>
        /// <returns>The position of the first matching string or -1 if no match is found</returns>
        public static int IndexOfAny<T>(this StringTreeList<T> tree, out IReadOnlyList<T> match, String text, int start = 0)
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
        /// <returns>The position of the last matching string or -1 if no match is found</returns>

        public static int LastIndexOfAny<T>(this StringTreeList<T> tree, out IReadOnlyList<T> match, String text, int start = -1)
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


    }



}
