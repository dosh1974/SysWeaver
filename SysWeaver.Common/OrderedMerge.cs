using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver
{
    /// <summary>
    /// Merges any number of ordered (sorted) lists into a new ordered list.
    /// </summary>
    public static class OrderedMerge
    {

        /// <summary>
        /// Merge any number of ordered lists, resulting in a new ordered list using O(N * L) complexity (N is the number of output elements and L the number of lists).
        /// Lists will be interleaved if it's a tie.
        /// </summary>
        /// <remarks>
        /// The input lists must be ordered using the same logic as <paramref name="isBetter"/>, else the output order is undefined (but all elements are still included).
        /// Ties are resolved round robin, so equal elements from different lists are interleaved (the first list starts).
        /// </remarks>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="isBetter">A function that should return true if the first argument is better than the second (using the same logic as when sorting)</param>
        /// <param name="maxLength">Maximum length of the output list, if zero or negative, an empty list is returned</param>
        /// <param name="lists">Array of lists to merge</param>
        /// <returns>A new merged ordered list (always a new instance, even if there is only one list)</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="isBetter"/>, <paramref name="lists"/> or any of the lists is null</exception>
        [SkipLocalsInit]
        public static unsafe List<T> Merge<T>(Func<T, T, bool> isBetter, int maxLength, params IReadOnlyList<T>[] lists)
        {
            ArgumentNullException.ThrowIfNull(isBetter);
            ArgumentNullException.ThrowIfNull(lists);
            var l = lists.Length;
            if ((maxLength <= 0) || (l <= 0))
                return new List<T>();
            if (l == 1)
                return CopySingle(lists[0], maxLength);
            long total = 0;
            for (int i = 0; i < l; ++i)
            {
                var x = lists[i];
                ArgumentNullException.ThrowIfNull(x, nameof(lists));
                total += x.Count;
            }
            var o = new List<T>((int)Math.Min(maxLength, total));
            // Pointers are used for the position and length arrays, spans are significantly slower here (and arrays allocates)
            if (l <= MaxStackLists)
            {
                int* mem = stackalloc int[l * 2];
                MergeCore(o, mem, isBetter, maxLength, lists);
            }
            else
            {
                var a = GC.AllocateUninitializedArray<int>(l * 2);
                fixed (int* mem = a)
                    MergeCore(o, mem, isBetter, maxLength, lists);
            }
            return o;
        }

        /// <summary>
        /// The maximum number of lists where the temporary memory is stack allocated (2 KB)
        /// </summary>
        const int MaxStackLists = 256;

        /// <summary>
        /// Copy at most maxLength elements from a single list
        /// </summary>
        static List<T> CopySingle<T>(IReadOnlyList<T> src, int maxLength)
        {
            ArgumentNullException.ThrowIfNull(src, "lists");
            var sc = Math.Min(src.Count, maxLength);
            ReadOnlySpan<T> s;
            if (src is T[] a)
                s = new ReadOnlySpan<T>(a, 0, sc);
            else if (src is List<T> sl)
                s = CollectionsMarshal.AsSpan(sl).Slice(0, sc);
            else
            {
                var r = new List<T>(sc);
                for (int i = 0; i < sc; ++i)
                    r.Add(src[i]);
                return r;
            }
            var o = new List<T>(sc);
            CollectionsMarshal.SetCount(o, sc);
            s.CopyTo(CollectionsMarshal.AsSpan(o));
            return o;
        }

        /// <summary>
        /// Merge the lists
        /// </summary>
        /// <param name="o">The output list</param>
        /// <param name="pos">Memory for 2 * lists.Length integers (the first half is the current position in each list, the second half is the length of each list)</param>
        /// <param name="isBetter">A function that should return true if the first argument is better than the second</param>
        /// <param name="maxLength">Maximum length of the output list</param>
        /// <param name="lists">Array of lists to merge (at least two)</param>
        static unsafe void MergeCore<T>(List<T> o, int* pos, Func<T, T, bool> isBetter, int maxLength, IReadOnlyList<T>[] lists)
        {
            var l = lists.Length;
            int* length = pos + l;
            for (int i = 0; i < l; ++i)
            {
                pos[i] = 0;
                length[i] = lists[i].Count;
            }
            // start = offset % l (the list to start searching from, rotated to interleave ties)
            for (int start = 0; ; )
            {
                T bestVal = default(T);
                int bestList = -1;
                int bestJ = 0;
                for (int j = 0; j < l; ++ j)
                {
                    var i = start + j;
                    if (i >= l)
                        i -= l;
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
                    var i = start + j;
                    if (i >= l)
                        i -= l;
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
                o.Add(bestVal);
                if (o.Count >= maxLength)
                    break;
                ++pos[bestList];
                ++start;
                if (start >= l)
                    start = 0;
            }
        }

        /// <summary>
        /// Merge any number of ordered lists, resulting in a new ordered list using O(N * L) complexity (N is the number of output elements and L the number of lists).
        /// Lists will be interleaved if it's a tie.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="isBetter">A function that should return true if the first argument is better than the second (using the same logic as when sorting)</param>
        /// <param name="lists">Array of lists to merge</param>
        /// <returns>A new merged ordered list</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="isBetter"/>, <paramref name="lists"/> or any of the lists is null</exception>
        public static List<T> Merge<T>(Func<T, T, bool> isBetter, params IReadOnlyList<T>[] lists)
            => Merge<T>(isBetter, int.MaxValue, lists);

        /// <summary>
        /// Merge any number of ordered (ascending) lists, resulting in a new ordered list using O(N * L) complexity (N is the number of output elements and L the number of lists).
        /// Lists will be interleaved if it's a tie.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="maxLength">Maximum length of the output list, if zero or negative, an empty list is returned</param>
        /// <param name="lists">Array of lists to merge</param>
        /// <returns>A new merged ordered list</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="lists"/> or any of the lists is null</exception>
        public static List<T> Merge<T>(int maxLength, params IReadOnlyList<T>[] lists) where T : IComparable<T>
            => Merge<T>((a, b) => a.CompareTo(b) < 0, maxLength, lists);

        /// <summary>
        /// Merge any number of ordered (descending) lists, resulting in a new ordered list in reverse order using O(N * L) complexity (N is the number of output elements and L the number of lists).
        /// Lists will be interleaved if it's a tie.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="maxLength">Maximum length of the output list, if zero or negative, an empty list is returned</param>
        /// <param name="lists">Array of lists to merge</param>
        /// <returns>A new merged ordered list</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="lists"/> or any of the lists is null</exception>
        public static List<T> MergeDesc<T>(int maxLength, params IReadOnlyList<T>[] lists) where T : IComparable<T>
            => Merge<T>((a, b) => a.CompareTo(b) > 0, maxLength, lists);

        /// <summary>
        /// Merge any number of ordered (ascending) lists, resulting in a new ordered list using O(N * L) complexity (N is the number of output elements and L the number of lists).
        /// Lists will be interleaved if it's a tie.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="lists">Array of lists to merge</param>
        /// <returns>A new merged ordered list</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="lists"/> or any of the lists is null</exception>
        public static List<T> Merge<T>(params IReadOnlyList<T>[] lists) where T : IComparable<T>
            => Merge<T>((a, b) => a.CompareTo(b) < 0, int.MaxValue, lists);

        /// <summary>
        /// Merge any number of ordered (descending) lists, resulting in a new ordered list in reverse order using O(N * L) complexity (N is the number of output elements and L the number of lists).
        /// Lists will be interleaved if it's a tie.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="lists">Array of lists to merge</param>
        /// <returns>A new merged ordered list</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="lists"/> or any of the lists is null</exception>
        public static List<T> MergeDesc<T>(params IReadOnlyList<T>[] lists) where T : IComparable<T>
            => Merge<T>((a, b) => a.CompareTo(b) > 0, int.MaxValue, lists);


    }


}
