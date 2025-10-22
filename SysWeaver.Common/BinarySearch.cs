using System;
using System.Collections.Generic;

namespace SysWeaver
{


    /// <summary>
    /// Contains methods for doing searching on sorted data
    /// </summary>
    public static class BinarySearch
    {
        /// <summary>
        /// Find a value in some sorted data
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="index">The start index (typically zero)</param>
        /// <param name="length">The length of the range to search in (typically the length of a container)</param>
        /// <param name="value">The value to find</param>
        /// <param name="valueAt">A function that returns the value at a specified index</param>
        /// <param name="comparer">An optional comparer</param>
        /// <returns>The index containing the <paramref name="value"/> or negative if not found, use the two's completent operator (~) to get the index of the first elemnt that was lower</returns>
        public static int Find<E>(int index, int length, E value, Func<int, E> valueAt, IComparer<E> comparer = null)
        {
            if (comparer == null)
                comparer = Comparer<E>.Default;
            var min = index;
            var max = index + length - 1;
            while (min <= max)
            {
                var mid = min + ((max - min) >> 1);
                var cmp = comparer.Compare(valueAt(mid), value);
                if (cmp == 0)
                    return mid;
                if (cmp < 0)
                    min = mid + 1;
                else
                    max = mid - 1;
            }
            return ~min;
        }

        /// <summary>
        /// Find a value in some sorted data
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="index">The start index (typically zero)</param>
        /// <param name="length">The length of the range to search in (typically the length of a container)</param>
        /// <param name="value">The value to find</param>
        /// <param name="valueAt">A function that returns the value at a specified index</param>
        /// <param name="comparer">An optional comparer</param>
        /// <returns>The index containing the <paramref name="value"/> or negative if not found, use the two's completent operator (~) to get the index of the first elemnt that was lower</returns>
        public static long Find<E>(long index, long length, E value, Func<long, E> valueAt, IComparer<E> comparer = null)
        {
            if (comparer == null)
                comparer = Comparer<E>.Default;
            var min = index;
            var max = index + length - 1;
            while (min <= max)
            {
                var mid = min + ((max - min) >> 1);
                var cmp = comparer.Compare(valueAt(mid), value);
                if (cmp == 0)
                    return mid;
                if (cmp < 0)
                    min = mid + 1;
                else
                    max = mid - 1;
            }
            return ~min;
        }

        /// <summary>
        /// Find a value in list
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="container">The data to search in</param>
        /// <param name="index">The start index (typically zero)</param>
        /// <param name="length">The length of the range to search in (typically the length of the container)</param>
        /// <param name="value">The value to find</param>
        /// <param name="comparer">An optional comparer</param>
        /// <returns>The index containing the <paramref name="value"/> or negative if not found, use the two's completent operator (~) to get the index of the first elemnt that was lower</returns>
        public static int Find<E>(IReadOnlyList<E> container, int index, int length, E value, IComparer<E> comparer = null)
        {
            return Find(index, length, value, ti => container[ti], comparer);
        }

        /// <summary>
        /// Find a value in list
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="container">The data to search in</param>
        /// <param name="value">The value to find</param>
        /// <param name="comparer">An optional comparer</param>
        /// <returns>The index containing the <paramref name="value"/> or negative if not found, use the two's completent operator (~) to get the index of the first elemnt that was lower</returns>
        public static int Find<E>(IReadOnlyList<E> container, E value, IComparer<E> comparer = null)
        {
            return Find(0, container.Count, value, ti => container[ti], comparer);
        }

        public static int Lower<E>(int index, int length, E value, Func<int, E> valueAt, IComparer<E> comparer = null)
        {
            if (comparer == null)
                comparer = Comparer<E>.Default;
            var min = index;
            var max = index + length - 1;
            while (min <= max)
            {
                var mid = min + ((max - min) >> 1);
                int cmp = comparer.Compare(valueAt(mid), value);
                if (cmp < 0)
                    min = mid + 1;
                else
                    max = mid - 1;
            }
            if ((min >= length) || (comparer.Compare(valueAt(min), value) != 0))
                min = ~min;
            return min;
        }

        public static long Lower<E>(long index, long length, E value, Func<long, E> valueAt, IComparer<E> comparer = null)
        {
            if (comparer == null)
                comparer = Comparer<E>.Default;
            var min = index;
            var max = index + length - 1;
            while (min <= max)
            {
                var mid = min + ((max - min) >> 1);
                int cmp = comparer.Compare(valueAt(mid), value);
                if (cmp < 0)
                    min = mid + 1;
                else
                    max = mid - 1;
            }
            if ((min >= length) || (comparer.Compare(valueAt(min), value) != 0))
                min = ~min;
            return min;
        }

        public static int Lower<E>(IList<E> container, int index, int length, E value, IComparer<E> comparer = null)
        {
            return Lower(index, length, value, ti => container[ti], comparer);
        }

        public static int Lower<E>(IList<E> container, E value, IComparer<E> comparer = null)
        {
            return Lower(0, container.Count, value, ti => container[ti], comparer);
        }

        public static int Upper<E>(int index, int length, E value, Func<int, E> valueAt, IComparer<E> comparer = null)
        {
            if (comparer == null)
                comparer = Comparer<E>.Default;
            var min = index;
            var max = index + length - 1;
            while (min <= max)
            {
                var mid = min + ((max - min) >> 1);
                int cmp = comparer.Compare(valueAt(mid), value);
                if (cmp <= 0)
                    min = mid + 1;
                else
                    max = mid - 1;
            }
            if ((min >= length) || (comparer.Compare(valueAt(min), value) != 0))
                min = ~min;
            return min;
        }

        public static long Upper<E>(long index, long length, E value, Func<long, E> valueAt, IComparer<E> comparer = null)
        {
            if (comparer == null)
                comparer = Comparer<E>.Default;
            var min = index;
            var max = index + length - 1;
            while (min <= max)
            {
                var mid = min + ((max - min) >> 1);
                int cmp = comparer.Compare(valueAt(mid), value);
                if (cmp <= 0)
                    min = mid + 1;
                else
                    max = mid - 1;
            }
            if ((min >= length) || (comparer.Compare(valueAt(min), value) != 0))
                min = ~min;
            return min;
        }

        public static int Upper<E>(IList<E> container, int index, int length, E value, IComparer<E> comparer = null)
        {
            return Upper(index, length, value, ti => container[ti], comparer);
        }

        public static int Upper<E>(IList<E> container, E value, IComparer<E> comparer = null)
        {
            return Upper(0, container.Count, value, ti => container[ti], comparer);
        }

    }

}
