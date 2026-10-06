using System;
using System.Collections.Generic;

namespace SysWeaver
{


    /// <summary>
    /// Contains methods for doing searching on sorted data.
    /// </summary>
    /// <remarks>
    /// The data in the searched range must be sorted in ascending order according to the comparer used (the default comparer if none is given), else the result is undefined.
    /// No validation of the index and length is done, the range [index, index + length) must be valid for the data being searched.
    /// A non-positive length is an empty range, in that case the complement of the start index is returned.
    /// </remarks>
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
        /// <param name="comparer">An optional comparer, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The index containing the <paramref name="value"/> or negative if not found, use the two's complement operator (~) to get the index of the first element that is greater than the <paramref name="value"/> (the insertion point).
        /// If the range contains multiple elements that are equal to the <paramref name="value"/>, any of their indices may be returned (use Lower or Upper to get the first or the last one).</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="valueAt"/> is null and the range is non-empty</exception>
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
        /// <param name="comparer">An optional comparer, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The index containing the <paramref name="value"/> or negative if not found, use the two's complement operator (~) to get the index of the first element that is greater than the <paramref name="value"/> (the insertion point).
        /// If the range contains multiple elements that are equal to the <paramref name="value"/>, any of their indices may be returned.</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="valueAt"/> is null and the range is non-empty</exception>
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
        /// <param name="comparer">An optional comparer, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The index containing the <paramref name="value"/> or negative if not found, use the two's complement operator (~) to get the index of the first element that is greater than the <paramref name="value"/> (the insertion point).
        /// If the range contains multiple elements that are equal to the <paramref name="value"/>, any of their indices may be returned.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="container"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown by the list (arrays, List etc) if the range is outside of the container</exception>
        public static int Find<E>(IReadOnlyList<E> container, int index, int length, E value, IComparer<E> comparer = null)
        {
            ArgumentNullException.ThrowIfNull(container);
            return FindIn(container, index, length, value, comparer);
        }

        /// <summary>
        /// Find a value in list
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="container">The data to search in</param>
        /// <param name="value">The value to find</param>
        /// <param name="comparer">An optional comparer, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The index containing the <paramref name="value"/> or negative if not found, use the two's complement operator (~) to get the index of the first element that is greater than the <paramref name="value"/> (the insertion point).
        /// If the list contains multiple elements that are equal to the <paramref name="value"/>, any of their indices may be returned.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="container"/> is null</exception>
        public static int Find<E>(IReadOnlyList<E> container, E value, IComparer<E> comparer = null)
        {
            ArgumentNullException.ThrowIfNull(container);
            return FindIn(container, 0, container.Count, value, comparer);
        }

        /// <summary>
        /// Same as Find using a delegate, but accessing the list directly (no closure and delegate allocations)
        /// </summary>
        static int FindIn<E>(IReadOnlyList<E> container, int index, int length, E value, IComparer<E> comparer)
        {
            if (comparer == null)
                comparer = Comparer<E>.Default;
            var min = index;
            var max = index + length - 1;
            while (min <= max)
            {
                var mid = min + ((max - min) >> 1);
                var cmp = comparer.Compare(container[mid], value);
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
        /// Find the first (lowest index) element that is equal to a value in some sorted data
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="index">The start index (typically zero)</param>
        /// <param name="length">The length of the range to search in (typically the length of a container)</param>
        /// <param name="value">The value to find</param>
        /// <param name="valueAt">A function that returns the value at a specified index</param>
        /// <param name="comparer">An optional comparer, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The index of the first element that is equal to the <paramref name="value"/> or negative if not found, use the two's complement operator (~) to get the index of the first element that is greater than the <paramref name="value"/> (the insertion point)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="valueAt"/> is null and the range is non-empty</exception>
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
            if ((min >= index + length) || (comparer.Compare(valueAt(min), value) != 0))
                min = ~min;
            return min;
        }

        /// <summary>
        /// Find the first (lowest index) element that is equal to a value in some sorted data
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="index">The start index (typically zero)</param>
        /// <param name="length">The length of the range to search in (typically the length of a container)</param>
        /// <param name="value">The value to find</param>
        /// <param name="valueAt">A function that returns the value at a specified index</param>
        /// <param name="comparer">An optional comparer, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The index of the first element that is equal to the <paramref name="value"/> or negative if not found, use the two's complement operator (~) to get the index of the first element that is greater than the <paramref name="value"/> (the insertion point)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="valueAt"/> is null and the range is non-empty</exception>
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
            if ((min >= index + length) || (comparer.Compare(valueAt(min), value) != 0))
                min = ~min;
            return min;
        }

        /// <summary>
        /// Find the first (lowest index) element that is equal to a value in a sorted list
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="container">The data to search in</param>
        /// <param name="index">The start index (typically zero)</param>
        /// <param name="length">The length of the range to search in (typically the length of the container)</param>
        /// <param name="value">The value to find</param>
        /// <param name="comparer">An optional comparer, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The index of the first element that is equal to the <paramref name="value"/> or negative if not found, use the two's complement operator (~) to get the index of the first element that is greater than the <paramref name="value"/> (the insertion point)</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="container"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown by the list (arrays, List etc) if the range is outside of the container</exception>
        public static int Lower<E>(IList<E> container, int index, int length, E value, IComparer<E> comparer = null)
        {
            ArgumentNullException.ThrowIfNull(container);
            return LowerIn(container, index, length, value, comparer);
        }

        /// <summary>
        /// Find the first (lowest index) element that is equal to a value in a sorted list
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="container">The data to search in</param>
        /// <param name="value">The value to find</param>
        /// <param name="comparer">An optional comparer, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The index of the first element that is equal to the <paramref name="value"/> or negative if not found, use the two's complement operator (~) to get the index of the first element that is greater than the <paramref name="value"/> (the insertion point)</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="container"/> is null</exception>
        public static int Lower<E>(IList<E> container, E value, IComparer<E> comparer = null)
        {
            ArgumentNullException.ThrowIfNull(container);
            return LowerIn(container, 0, container.Count, value, comparer);
        }

        /// <summary>
        /// Same as Lower using a delegate, but accessing the list directly (no closure and delegate allocations)
        /// </summary>
        static int LowerIn<E>(IList<E> container, int index, int length, E value, IComparer<E> comparer)
        {
            if (comparer == null)
                comparer = Comparer<E>.Default;
            var min = index;
            var max = index + length - 1;
            while (min <= max)
            {
                var mid = min + ((max - min) >> 1);
                int cmp = comparer.Compare(container[mid], value);
                if (cmp < 0)
                    min = mid + 1;
                else
                    max = mid - 1;
            }
            if ((min >= index + length) || (comparer.Compare(container[min], value) != 0))
                min = ~min;
            return min;
        }

        /// <summary>
        /// Find the last (highest index) element that is equal to a value in some sorted data
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="index">The start index (typically zero)</param>
        /// <param name="length">The length of the range to search in (typically the length of a container)</param>
        /// <param name="value">The value to find</param>
        /// <param name="valueAt">A function that returns the value at a specified index</param>
        /// <param name="comparer">An optional comparer, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The index of the last element that is equal to the <paramref name="value"/> or negative if not found, use the two's complement operator (~) to get the index of the first element that is greater than the <paramref name="value"/> (the insertion point)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="valueAt"/> is null and the range is non-empty</exception>
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
            // min is the first element greater than the value, the found value is the element before it
            if ((min > index) && (comparer.Compare(valueAt(min - 1), value) == 0))
                return min - 1;
            return ~min;
        }

        /// <summary>
        /// Find the last (highest index) element that is equal to a value in some sorted data
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="index">The start index (typically zero)</param>
        /// <param name="length">The length of the range to search in (typically the length of a container)</param>
        /// <param name="value">The value to find</param>
        /// <param name="valueAt">A function that returns the value at a specified index</param>
        /// <param name="comparer">An optional comparer, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The index of the last element that is equal to the <paramref name="value"/> or negative if not found, use the two's complement operator (~) to get the index of the first element that is greater than the <paramref name="value"/> (the insertion point)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="valueAt"/> is null and the range is non-empty</exception>
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
            // min is the first element greater than the value, the found value is the element before it
            if ((min > index) && (comparer.Compare(valueAt(min - 1), value) == 0))
                return min - 1;
            return ~min;
        }

        /// <summary>
        /// Find the last (highest index) element that is equal to a value in a sorted list
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="container">The data to search in</param>
        /// <param name="index">The start index (typically zero)</param>
        /// <param name="length">The length of the range to search in (typically the length of the container)</param>
        /// <param name="value">The value to find</param>
        /// <param name="comparer">An optional comparer, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The index of the last element that is equal to the <paramref name="value"/> or negative if not found, use the two's complement operator (~) to get the index of the first element that is greater than the <paramref name="value"/> (the insertion point)</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="container"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown by the list (arrays, List etc) if the range is outside of the container</exception>
        public static int Upper<E>(IList<E> container, int index, int length, E value, IComparer<E> comparer = null)
        {
            ArgumentNullException.ThrowIfNull(container);
            return UpperIn(container, index, length, value, comparer);
        }

        /// <summary>
        /// Find the last (highest index) element that is equal to a value in a sorted list
        /// </summary>
        /// <typeparam name="E">The type of the value to find</typeparam>
        /// <param name="container">The data to search in</param>
        /// <param name="value">The value to find</param>
        /// <param name="comparer">An optional comparer, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The index of the last element that is equal to the <paramref name="value"/> or negative if not found, use the two's complement operator (~) to get the index of the first element that is greater than the <paramref name="value"/> (the insertion point)</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="container"/> is null</exception>
        public static int Upper<E>(IList<E> container, E value, IComparer<E> comparer = null)
        {
            ArgumentNullException.ThrowIfNull(container);
            return UpperIn(container, 0, container.Count, value, comparer);
        }

        /// <summary>
        /// Same as Upper using a delegate, but accessing the list directly (no closure and delegate allocations)
        /// </summary>
        static int UpperIn<E>(IList<E> container, int index, int length, E value, IComparer<E> comparer)
        {
            if (comparer == null)
                comparer = Comparer<E>.Default;
            var min = index;
            var max = index + length - 1;
            while (min <= max)
            {
                var mid = min + ((max - min) >> 1);
                int cmp = comparer.Compare(container[mid], value);
                if (cmp <= 0)
                    min = mid + 1;
                else
                    max = mid - 1;
            }
            // min is the first element greater than the value, the found value is the element before it
            if ((min > index) && (comparer.Compare(container[min - 1], value) == 0))
                return min - 1;
            return ~min;
        }

    }

}
