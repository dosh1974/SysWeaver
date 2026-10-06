using System;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// Search extension methods for <see cref="StringTreeList{T}"/> instances (find contained strings anywhere in a text, not only at the start)
    /// </summary>
    public static class StringTreeListExt
    {


        /// <summary>
        /// Find the index of the first position in the text where any string (from the tree) starts.
        /// Calls <see cref="StringTreeList{T}.StartsWithAny(string, int)"/> at every position from <paramref name="start"/>.
        /// </summary>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <param name="tree">The tree to use</param>
        /// <param name="match">The values of the longest string that matches at the returned position (the internal list, must not be modified), or null if no match is found</param>
        /// <param name="text">The text to find the first matching string in, may not be null</param>
        /// <param name="start">An optional start offset (positions before it are not tested)</param>
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
        /// Find the index of the last position in the text where any string (from the tree) starts.
        /// Calls <see cref="StringTreeList{T}.StartsWithAny(string, int)"/> at every position, going backwards from <paramref name="start"/> - 1.
        /// </summary>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <param name="tree">The tree to use</param>
        /// <param name="match">The values of the longest string that matches at the returned position (the internal list, must not be modified), or null if no match is found</param>
        /// <param name="text">The text to find the last matching string in, may not be null</param>
        /// <param name="start">An optional exclusive end position (the first tested position is start - 1), a negative value or a value greater than the length of the text starts at the end of the text</param>
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
