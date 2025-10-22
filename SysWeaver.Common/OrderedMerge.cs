using System;
using System.Linq;
using System.Collections.Generic;

namespace SysWeaver
{
    public static class OrderedMerge
    {

        /// <summary>
        /// Merge any number of ordered lists, resulting in a new ordered list using O(N) complexity.
        /// Lists will be interleaved if it's a tie.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="isBetter">A function that should return true if the first argument is better than the second (using the same logic as when sorting)</param>
        /// <param name="maxLength">Maximum length of the output list</param>
        /// <param name="lists">Array of lists to merge</param>
        /// <returns>A new merged ordered list</returns>
        public static List<T> Merge<T>(Func<T, T, bool> isBetter, int maxLength, params IReadOnlyList<T>[] lists)
        {
            var o = new List<T>(maxLength);
            var l = lists.Length;
            if (l == 1)
                o.AddRange(lists[0].Take(maxLength));
            if (l <= 1)
                return o;
            var pos = new int[l];
            var length = new int[l];
            for (int i = 0; i < l; ++i)
                length[i] = lists[i].Count;
            for (int offset = 0; ;++ offset)
            {
                T bestVal = default(T);
                int bestList = -1;
                int bestJ = 0;
                for (int j = 0; j < l; ++ j)
                {
                    var i = (j + offset) % l;
                    var p = pos[i];
                    if (p < length[i])
                    {
                        bestVal = lists[i][p];
                        bestList = i;
                        bestJ = j;
                        break;
                    }
                }
                if (bestList < 0)
                    break;
                bool foundOne = false;
                for (int j = bestJ + 1; j < l; ++ j)
                {
                    var i = (j + offset) % l;
                    var p = pos[i];
                    if (p >= length[i])
                        continue;
                    foundOne = true;
                    var val = lists[i][p];
                    if (!isBetter(val, bestVal))
                        continue;
                    bestVal = val;
                    bestList = i;
                }
                if (!foundOne)
                {
                    //  Only one list left
                    var lastList = lists[bestList];
                    var lastIndex = pos[bestList];
                    var copy = length[bestList] - lastIndex;
                    var mc = maxLength - o.Count;
                    if (mc < copy)
                        copy = mc;
                    while (copy > 0)
                    {
                        o.Add(lastList[lastIndex]);
                        --copy;
                        ++lastIndex;
                    }
                    break;
                }
                var np = pos[bestList];
                o.Add(lists[bestList][np]);
                if (o.Count >= maxLength)
                    break;
                pos[bestList] = np + 1;
            }
            return o;
        }

        /// <summary>
        /// Merge any number of ordered lists, resulting in a new ordered list using O(N) complexity.
        /// Lists will be interleaved if it's a tie.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="isBetter">A function that should return true if the first argument is better than the second (using the same logic as when sorting)</param>
        /// <param name="lists">Array of lists to merge</param>
        /// <returns>A new merged ordered list</returns>
        public static List<T> Merge<T>(Func<T, T, bool> isBetter, params IReadOnlyList<T>[] lists)
            => Merge<T>(isBetter, int.MaxValue, lists);

        /// <summary>
        /// Merge any number of ordered lists, resulting in a new ordered list using O(N) complexity.
        /// Lists will be interleaved if it's a tie.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="maxLength">Maximum length of the output list</param>
        /// <param name="lists">Array of lists to merge</param>
        /// <returns>A new merged ordered list</returns>
        public static List<T> Merge<T>(int maxLength, params IReadOnlyList<T>[] lists) where T : IComparable<T>
            => Merge<T>((a, b) => a.CompareTo(b) > 0, maxLength, lists);

        /// <summary>
        /// Merge any number of ordered lists, resulting in a new ordered list in reverse order using O(N) complexity.
        /// Lists will be interleaved if it's a tie.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="maxLength">Maximum length of the output list</param>
        /// <param name="lists">Array of lists to merge</param>
        /// <returns>A new merged ordered list</returns>
        public static List<T> MergeDesc<T>(int maxLength, params IReadOnlyList<T>[] lists) where T : IComparable<T>
            => Merge<T>((a, b) => a.CompareTo(b) < 0, maxLength, lists);

        /// <summary>
        /// Merge any number of ordered lists, resulting in a new ordered list using O(N) complexity.
        /// Lists will be interleaved if it's a tie.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lists">Array of lists to merge</param>
        /// <returns>A new merged ordered list</returns>
        public static List<T> Merge<T>(params IReadOnlyList<T>[] lists) where T : IComparable<T>
            => Merge<T>((a, b) => a.CompareTo(b) > 0, int.MaxValue, lists);

        /// <summary>
        /// Merge any number of ordered lists, resulting in a new ordered list in reverse order using O(N) complexity.
        /// Lists will be interleaved if it's a tie.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lists">Array of lists to merge</param>
        /// <returns>A new merged ordered list</returns>
        public static List<T> MergeDesc<T>(params IReadOnlyList<T>[] lists) where T : IComparable<T>
            => Merge<T>((a, b) => a.CompareTo(b) < 0, int.MaxValue, lists);


    }


}
