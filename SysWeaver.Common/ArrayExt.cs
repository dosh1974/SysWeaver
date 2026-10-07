using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Contains array (and list / dictionary) helpers: creation, conversion (sync and async), cloning, sorting and intersection.
    /// </summary>
    public static class ArrayExt
    {


        /// <summary>
        /// Get a value from an array, return a value on fail.
        /// Will fail if array is null or if the index is out of bound.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="a">The array to get a value from (may be null)</param>
        /// <param name="index">The array index to get a value for (may be outside the array)</param>
        /// <param name="onFail">The value to return when failing (null or out of bounds)</param>
        /// <returns>The value at the index or the onFail value if failed</returns>
        public static T SafeGetAt<T>(this T[] a, int index, T onFail = default)
        {
            if (a == null)
                return onFail;
            if (index < 0)
                return onFail;
            if (index >= a.Length)
                return onFail;
            return a[index];
        }


        /// <summary>
        /// Push an item to the end of an array, reallocation will happen = slow
        /// If the array is null a new array with the val is returned
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="a">The array to add an item to (may be null), it's not modified</param>
        /// <param name="val">The value to add</param>
        /// <returns>A new array with the elements of <paramref name="a"/> followed by <paramref name="val"/></returns>
        public static T[] Push<T>(this T[] a, T val)
        {
            if (a == null)
                return [val];
            return [..a, val];
        }


        /// <summary>
        /// Insert an item to the start of an array, reallocation will happen = slow
        /// If the array is null a new array with the val is returned
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="a">The array to add an item to (may be null), it's not modified</param>
        /// <param name="val">The value to add</param>
        /// <returns>A new array with <paramref name="val"/> followed by the elements of <paramref name="a"/></returns>
        public static T[] PushFront<T>(this T[] a, T val)
        {
            if (a == null)
                return [val];
            return [val, .. a];
        }


        /// <summary>
        /// Concat two arrays.
        /// If both arrays are null, null is returned.
        /// If any array is null (or empty), the other array is returned.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="a">The first array (may be null)</param>
        /// <param name="b">The second array (may be null)</param>
        /// <returns>A new array with the elements of <paramref name="a"/> followed by the elements of <paramref name="b"/>, or one of the input arrays (not a copy) if the other one is null or empty</returns>
        public static T[] Concat<T>(this T[] a, T[] b)
        {
            if (a == null)
                return b;
            if (b == null)
                return a;
            var al = a.Length;
            var bl = b.Length;
            if (al <= 0)
                return b;
            if (bl <= 0)
                return a;
            return [..a, ..b];
        }


        /// <summary>
        /// Create and initiate an array with a scalar value
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="count">The number of elements in the array</param>
        /// <param name="value">The value to assign to all elements</param>
        /// <returns>A new array with <paramref name="count"/> elements, all set to <paramref name="value"/></returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="count"/> is negative (debug builds only; release builds throw an <see cref="OverflowException"/>)</exception>
        public static T[] Create<T>(int count, T value)
        {
#if DEBUG
            ArgumentOutOfRangeException.ThrowIfNegative(count);
#endif//DEBUG
            var t = GC.AllocateUninitializedArray<T>(count);
            // Fill is vectorized
            t.AsSpan().Fill(value);
            return t;
        }

        /// <summary>
        /// Create and initiate an array with a value per element
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="count">The number of elements in the array</param>
        /// <param name="getValue">The function that given an index returned the value to use</param>
        /// <returns>A new array with <paramref name="count"/> elements, element i is set to getValue(i)</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="count"/> is negative (debug builds only; release builds throw an <see cref="OverflowException"/>)</exception>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="getValue"/> is null and <paramref name="count"/> is positive</exception>
        public static T[] Create<T>(int count, Func<int, T> getValue)
        {
#if DEBUG
            ArgumentOutOfRangeException.ThrowIfNegative(count);
#endif//DEBUG
            var t = GC.AllocateUninitializedArray<T>(count);
            var p = t.AsSpan();
            for (int i = 0; i < count; ++i)
                p[i] = getValue(i);
            return t;
        }


        /// <summary>
        /// Create and initiate an array async with a value per element
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="count">The number of elements in the array, if zero or negative, an empty array is returned</param>
        /// <param name="getValue">The function that given an index returned the value to use</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A new array with <paramref name="count"/> elements, element i is set to the result of getValue(i).
        /// If any of the tasks fails, the returned task is faulted (after all tasks have completed).</returns>
        /// <exception cref="NullReferenceException">Thrown (by the returned task) if <paramref name="getValue"/> is null and <paramref name="count"/> is positive</exception>
        public static async Task<T[]> CreateAsync<T>(int count, Func<int, Task<T>> getValue, int maxConcurrency = 0)
        {
            if (count <= 0)
                return Array.Empty<T>();
            if (count == 1)
                return [await getValue(0).ConfigureAwait(false)];
            ConcurrencyLimiter.LimitConcurrency(ref getValue, maxConcurrency, count);
            var tt = GC.AllocateUninitializedArray<Task<T>>(count);
            for (int i = 0; i < count; ++i)
            {
                try
                {
                    tt[i] = getValue(i);
                }
                catch (Exception ex) when (i > 0)
                {
                    //  Earlier items are already running, so fault the result (after all items have completed) instead of throwing and leaving them unobserved
                    tt[i] = Task.FromException<T>(ex);
                }
            }
            // The non-generic WhenAll doesn't copy the task array (the generic one does, and also allocates a result array)
            await Task.WhenAll((Task[])tt).ConfigureAwait(false);
            return GetResults(tt);
        }

        /// <summary>
        /// Create and initiate an array async with a value per element
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="count">The number of elements in the array, if zero or negative, an empty array is returned</param>
        /// <param name="getValue">The function that given an index returned the value to use</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A new array with <paramref name="count"/> elements, element i is set to the result of getValue(i)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="getValue"/> is null and <paramref name="count"/> is positive</exception>
        public static ValueTask<T[]> CreateAsyncValue<T>(int count, Func<int, ValueTask<T>> getValue, int maxConcurrency = 0)
        {
            if (count <= 0)
                return TaskExt<T>.EmptyArrayValueTask;
            if (count == 1)
                return ValueTaskToArray(getValue(0));
            ConcurrencyLimiter.LimitConcurrency(ref getValue, maxConcurrency, count);
            var tt = GC.AllocateUninitializedArray<ValueTask<T>>(count);
            for (int i = 0; i < count; ++i)
            {
                try
                {
                    tt[i] = getValue(i);
                }
                catch (Exception ex) when (i > 0)
                {
                    //  Earlier items are already running, so fault the result (after all items have completed) instead of throwing and leaving them unobserved
                    tt[i] = ValueTask.FromException<T>(ex);
                }
            }
            return TaskExt.WhenAll(tt);
        }


        /// <summary>
        /// Take N elements from an enumerable and create an array of them
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="values">The values to enumerate (only the first <paramref name="count"/> elements are enumerated)</param>
        /// <param name="count">The number of elements in the returned array</param>
        /// <returns>A new array with <paramref name="count"/> elements.
        /// If the sequence contains fewer elements than <paramref name="count"/>, the remaining elements are default(T).</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="values"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="count"/> is negative (debug builds only; release builds throw an <see cref="OverflowException"/>)</exception>
        public static T[] ToArray<T>(this IEnumerable<T> values, int count)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(values);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
#endif//DEBUG
            var t = GC.AllocateUninitializedArray<T>(count);
            var p = t.AsSpan();
            using var e = values.GetEnumerator();
            int i;
            for (i = 0; i < count; ++i)
            {
                // Don't call MoveNext after the end of the sequence
                if (!e.MoveNext())
                    break;
                p[i] = e.Current;
            }
            if (i < count)
                p.Slice(i).Clear();
            return t;
        }

        /// <summary>
        /// Create a new re-ordered array
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="values">The original array</param>
        /// <param name="order">The new order, ex: newArray[0] = values[order[0]]. Must contain at least as many elements as <paramref name="values"/> (any extra elements are ignored), the same index may be used multiple times</param>
        /// <returns>A new array (with the same length as <paramref name="values"/>) with the elements ordered according to the order, or null if <paramref name="values"/> is null</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="order"/> is null and <paramref name="values"/> is non-null (debug builds only; release builds throw a <see cref="NullReferenceException"/> if <paramref name="values"/> is non-empty)</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown by the list (arrays, List etc) if an index is out of range or if <paramref name="order"/> is too short</exception>
        public static T[] Reordered<T>(this IReadOnlyList<T> values, IReadOnlyList<int> order)
        {
            if (values == null)
                return null;
#if DEBUG
            ArgumentNullException.ThrowIfNull(order);
#endif//DEBUG
            var c = values.Count;
            var r = new T[c];
            for (int i = 0; i < c; ++i)
                r[i] = values[order[i]];
            return r;
        }


        /// <summary>
        /// Clones (shallow) an array of primitive types (using a fast mem copy).
        /// </summary>
        /// <typeparam name="T">The (unmanaged) element type</typeparam>
        /// <param name="array">The array to clone (may be null)</param>
        /// <returns>A new array with the same elements, or null if <paramref name="array"/> is null</returns>
        public static T[] ShallowClonePrimitive<T>(this T[] array) where T : unmanaged
        {
            if (array == null)
                return array;
            // No need to zero the memory since everything is overwritten
            var t = GC.AllocateUninitializedArray<T>(array.Length);
            new ReadOnlySpan<T>(array).CopyTo(t);
            return t;
        }

        /// <summary>
        /// Clones (shallow) an array (if the type T is primitive, please use the faster ShallowClonePrimitive instead).
        /// </summary>
        /// <typeparam name="T">The (reference) element type</typeparam>
        /// <param name="array">The array to clone (may be null)</param>
        /// <returns>A new array (of type T[], even if <paramref name="array"/> is an array of a derived type) with the same references, or null if <paramref name="array"/> is null</returns>
        public static T[] ShallowClone<T>(this T[] array) where T : class
        {
            if (array == null)
                return array;
            var t = new T[array.Length];
            // ReadOnlySpan doesn't have the array variance check (Span does), so this works for covariant arrays (bulk copy)
            new ReadOnlySpan<T>(array).CopyTo(t);
            return t;
        }

        /// <summary>
        /// Deep clones an array, each element is cloned using <see cref="ICloneable{T}.Clone"/>
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="array">The array to clone (may be null)</param>
        /// <returns>A new array where each element is a clone of the element in <paramref name="array"/> (null elements are kept null), or null if <paramref name="array"/> is null</returns>
        public static T[] DeepClone<T>(this T[] array) where T : class, ICloneable<T>
        {
            if (array == null)
                return array;
            var c = array.Length;
            var t = new T[c];
            for (int i = 0; i < c; ++i)
                t[i] = array[i]?.Clone();
            return t;
        }

        /// <summary>
        /// Deep clones an array, each element is cloned using <see cref="System.ICloneable.Clone"/>
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="array">The array to clone (may be null)</param>
        /// <returns>A new array where each element is a clone of the element in <paramref name="array"/> (null elements are kept null, if the clone isn't a T the element is set to null), or null if <paramref name="array"/> is null</returns>
        public static T[] Clone<T>(this T[] array) where T : class, System.ICloneable
        {
            if (array == null)
                return array;
            var c = array.Length;
            var t = new T[c];
            for (int i = 0; i < c; ++i)
                t[i] = array[i]?.Clone() as T;
            return t;
        }


        /// <summary>
        /// Deep converts an array of some type to an array of a base type, a new instance of <typeparamref name="D"/> is created for each element and initialized using <see cref="ICloneable{T}.CopyFrom(T)"/>
        /// </summary>
        /// <typeparam name="D">The destination element type</typeparam>
        /// <typeparam name="S">The source element type (must derive from <typeparamref name="D"/>)</typeparam>
        /// <param name="array">The array to convert (may be null)</param>
        /// <returns>A new array where each element is a new <typeparamref name="D"/> initialized from the element in <paramref name="array"/> (null elements are kept null), or null if <paramref name="array"/> is null</returns>
        public static D[] DeepCovert<D, S>(this S[] array) where D : class, ICloneable<D>, new() where S: class, D
        {
            if (array == null)
                return null;
            var c = array.Length;
            var t = new D[c];
            for (int i = 0; i < c; ++i)
            {
                var v = array[i];
                if (v == null)
                    continue;
                var p = new D();
                p.CopyFrom(v);
                t[i] = p;
            }
            return t;
        }


        /// <summary>
        /// Create a new array with an element removed, reallocation will happen = slow
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="array">The array to remove an element from, it's not modified</param>
        /// <param name="index">The index of the element to remove</param>
        /// <returns>A new array with all elements except the one at <paramref name="index"/></returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="array"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="index"/> is negative or greater than or equal to the length of the <paramref name="array"/> (debug builds only; release builds throw an <see cref="OverflowException"/> if the <paramref name="array"/> is empty, else an <see cref="ArgumentException"/> or <see cref="ArgumentOutOfRangeException"/> from <see cref="Array.Copy(Array, int, Array, int, int)"/>)</exception>
        public static T[] RemoveAt<T>(this T[] array, int index)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(array);
#endif//DEBUG
            var l = array.Length;
#if DEBUG
            if ((uint)index >= (uint)l)
                ThrowIndexOutOfRange(index, l);
#endif//DEBUG
            var n = new T[l - 1];
            if (index > 0)
                Array.Copy(array, 0, n, 0, index);
            var d = index + 1;
            if (d < l)
                Array.Copy(array, d, n, index, l - d);
            return n;
        }

#if DEBUG
        [DoesNotReturn]
        static void ThrowIndexOutOfRange(int index, int length)
            => throw new ArgumentOutOfRangeException(nameof(index), index, "The index must be non-negative and less than the length of the array (" + length + ")");
#endif//DEBUG


        /// <summary>
        /// Sort an array in-place using insertion sort (stable, fast for small or almost sorted arrays, O(N^2) worst case)
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="array">The array to sort (may be null)</param>
        /// <param name="compareFn">The compare function, should return a negative value if the first argument should be placed before the second argument, zero if they are equal and a positive value if the first argument should be placed after the second</param>
        /// <returns>The same array instance (sorted) or null if <paramref name="array"/> is null</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="compareFn"/> is null and the <paramref name="array"/> contains more than one element</exception>
        public static T[] InsertionSort<T>(this T[] array, Func<T, T, int> compareFn)
        {
            if (array == null)
                return array;
            int n = array.Length;
            if (n <= 1)
                return array;
            ArgumentNullException.ThrowIfNull(compareFn);
            for (int i = 1; i < n; i++)
            {
                var key = array[i];
                int j = i - 1;
                while (j >= 0 && compareFn(array[j], key) > 0)
                {
                    array[j + 1] = array[j];
                    j--;
                }
                array[j + 1] = key;
            }
            return array;
        }


        /// <summary>
        /// Convert dictionary values from one type to another
        /// </summary>
        /// <typeparam name="Key">The key type</typeparam>
        /// <typeparam name="CurrentValue">The current value type</typeparam>
        /// <typeparam name="NewValue">The new value type</typeparam>
        /// <param name="dictionary">The dictionary to convert (may be null)</param>
        /// <param name="func">The function that convert a value, the first argument is the key and the second argument is the current value</param>
        /// <returns>A new dictionary (using the same comparer as the source if it can be determined, else the default comparer) with the same keys and converted values, or null if <paramref name="dictionary"/> is null</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="func"/> is null and the <paramref name="dictionary"/> is non-null (debug builds only; release builds throw a <see cref="NullReferenceException"/> if the <paramref name="dictionary"/> is non-empty)</exception>
        /// <remarks>The comparer can be determined for Dictionary, ConcurrentDictionary, FrozenDictionary and the frozen dictionaries of this library (see <see cref="DictionaryExt.GetComparer{K, V}(IReadOnlyDictionary{K, V})"/>), for other dictionaries (like a SortedDictionary or a ReadOnlyDictionary wrapper) the default equality comparer is used</remarks>
        public static Dictionary<Key, NewValue> ConvertValues<Key, CurrentValue, NewValue>(this IReadOnlyDictionary<Key, CurrentValue> dictionary, Func<Key, CurrentValue, NewValue> func)
        {
            if (dictionary == null)
                return null;
#if DEBUG
            ArgumentNullException.ThrowIfNull(func);
#endif//DEBUG
            var d = new Dictionary<Key, NewValue>(dictionary.Count, DictionaryExt.TryGetComparer(dictionary));
            foreach (var kv in dictionary)
                d.TryAdd(kv.Key, func(kv.Key, kv.Value));
            return d;
        }

        /// <summary>
        /// Convert dictionary values from one type to another
        /// </summary>
        /// <typeparam name="Key">The key type</typeparam>
        /// <typeparam name="CurrentValue">The current value type</typeparam>
        /// <typeparam name="NewValue">The new value type</typeparam>
        /// <param name="dictionary">The dictionary to convert (may be null)</param>
        /// <param name="func">The function that convert a value</param>
        /// <returns>A new dictionary (using the same comparer as the source if it can be determined, else the default comparer) with the same keys and converted values, or null if <paramref name="dictionary"/> is null</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="func"/> is null and the <paramref name="dictionary"/> is non-null (debug builds only; release builds throw a <see cref="NullReferenceException"/> if the <paramref name="dictionary"/> is non-empty)</exception>
        /// <remarks>The comparer can be determined for Dictionary, ConcurrentDictionary, FrozenDictionary and the frozen dictionaries of this library (see <see cref="DictionaryExt.GetComparer{K, V}(IReadOnlyDictionary{K, V})"/>), for other dictionaries (like a SortedDictionary or a ReadOnlyDictionary wrapper) the default equality comparer is used</remarks>
        public static Dictionary<Key, NewValue> ConvertValues<Key, CurrentValue, NewValue>(this IReadOnlyDictionary<Key, CurrentValue> dictionary, Func<CurrentValue, NewValue> func)
        {
            if (dictionary == null)
                return null;
#if DEBUG
            ArgumentNullException.ThrowIfNull(func);
#endif//DEBUG
            var d = new Dictionary<Key, NewValue>(dictionary.Count, DictionaryExt.TryGetComparer(dictionary));
            foreach (var kv in dictionary)
                d.TryAdd(kv.Key, func(kv.Value));
            return d;
        }




        /// <summary>
        /// Convert an array to another element type using a function
        /// </summary>
        /// <typeparam name="E">The source element type</typeparam>
        /// <typeparam name="T">The destination element type</typeparam>
        /// <param name="array">The list to convert (may be null)</param>
        /// <param name="func">The function that converts an element</param>
        /// <returns>A new array with the converted elements (in the same order as the source), or null if <paramref name="array"/> is null</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="func"/> is null and the <paramref name="array"/> is non-empty (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        public static T[] Convert<E, T>(this IReadOnlyList<E> array, Func<E, T> func)
        {
            if (array == null)
                return null;
            var l = array.Count;
            if (l <= 0)
                return Array.Empty<T>();
#if DEBUG
            ArgumentNullException.ThrowIfNull(func);
#endif//DEBUG
            var t = GC.AllocateUninitializedArray<T>(l);
            var p = t.AsSpan();
            for (int i = 0; i < l; ++i)
                p[i] = func(array[i]);
            return t;
        }

        /// <summary>
        /// Convert an array to another element type using a function.
        /// Elements are converted in parallel (async).
        /// </summary>
        /// <typeparam name="E">The source element type</typeparam>
        /// <typeparam name="T">The destination element type</typeparam>
        /// <param name="array">The list to convert (may be null)</param>
        /// <param name="func">The function that converts an element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A new array with the converted elements (in the same order as the source), or null if <paramref name="array"/> is null.
        /// If any conversion fails, the returned task is faulted (after all conversions have completed).</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="func"/> is null and the <paramref name="array"/> is non-empty</exception>
        public static async Task<T[]> ConvertAsync<E, T>(this IReadOnlyList<E> array, Func<E, Task<T>> func, int maxConcurrency = 0)
        {
            if (array == null)
                return null;
            var l = array.Count;
            if (l <= 0)
                return Array.Empty<T>();
            if (l == 1)
                return [await func(array[0]).ConfigureAwait(false)];
            ConcurrencyLimiter.LimitConcurrency(ref func, maxConcurrency, l);
            var tt = GC.AllocateUninitializedArray<Task<T>>(l);
            for (int i = 0; i < l; ++i)
            {
                try
                {
                    tt[i] = func(array[i]);
                }
                catch (Exception ex) when (i > 0)
                {
                    //  Earlier items are already running, so fault the result (after all items have completed) instead of throwing and leaving them unobserved
                    tt[i] = Task.FromException<T>(ex);
                }
            }
            // The non-generic WhenAll doesn't copy the task array (the generic one does, and also allocates a result array)
            await Task.WhenAll((Task[])tt).ConfigureAwait(false);
            return GetResults(tt);
        }



        /// <summary>
        /// Convert an array to another element type using a function.
        /// Elements are converted in parallel (async).
        /// </summary>
        /// <typeparam name="E">The source element type</typeparam>
        /// <typeparam name="T">The destination element type</typeparam>
        /// <param name="array">The list to convert (may be null)</param>
        /// <param name="func">The function that converts an element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A new array with the converted elements (in the same order as the source), or null if <paramref name="array"/> is null.
        /// If any conversion fails, the returned task is faulted (after all conversions have completed).</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="func"/> is null and the <paramref name="array"/> is non-empty</exception>
        public static ValueTask<T[]> ConvertAsyncValue<E, T>(this IReadOnlyList<E> array, Func<E, ValueTask<T>> func, int maxConcurrency = 0)
        {
            if (array == null)
                return default;
            var l = array.Count;
            if (l <= 0)
                return TaskExt<T>.EmptyArrayValueTask;
            if (l == 1)
                return ValueTaskToArray(func(array[0]));
            ConcurrencyLimiter.LimitConcurrency(ref func, maxConcurrency, l);
            var tt = GC.AllocateUninitializedArray<ValueTask<T>>(l);
            for (int i = 0; i < l; ++i)
            {
                try
                {
                    tt[i] = func(array[i]);
                }
                catch (Exception ex) when (i > 0)
                {
                    //  Earlier items are already running, so fault the result (after all items have completed) instead of throwing and leaving them unobserved
                    tt[i] = ValueTask.FromException<T>(ex);
                }
            }
            return TaskExt.WhenAll(tt);
        }

        /// <summary>
        /// Get the results of some completed tasks as an array
        /// </summary>
        static T[] GetResults<T>(Task<T>[] tasks)
        {
            var l = tasks.Length;
            var t = GC.AllocateUninitializedArray<T>(l);
            for (int i = 0; i < l; ++i)
                t[i] = tasks[i].GetAwaiter().GetResult();
            return t;
        }

        static async ValueTask<T[]> ValueTaskToArray<T>(ValueTask<T> t)
            => [await t.ConfigureAwait(false)];



        /// <summary>
        /// Convert an array to another element type using a function
        /// </summary>
        /// <typeparam name="E">The source element type</typeparam>
        /// <typeparam name="T">The destination element type</typeparam>
        /// <param name="array">The list to convert (may be null)</param>
        /// <param name="func">The function that converts an element, the second argument is the index of the element</param>
        /// <returns>A new array with the converted elements (in the same order as the source), or null if <paramref name="array"/> is null</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="func"/> is null and the <paramref name="array"/> is non-empty (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        public static T[] Convert<E, T>(this IReadOnlyList<E> array, Func<E, int, T> func)
        {
            if (array == null)
                return null;
            var l = array.Count;
            if (l <= 0)
                return Array.Empty<T>();
#if DEBUG
            ArgumentNullException.ThrowIfNull(func);
#endif//DEBUG
            var t = GC.AllocateUninitializedArray<T>(l);
            var p = t.AsSpan();
            for (int i = 0; i < l; ++i)
                p[i] = func(array[i], i);
            return t;
        }

        /// <summary>
        /// Convert an array to another element type using a function.
        /// Elements are converted in parallel (async).
        /// </summary>
        /// <typeparam name="E">The source element type</typeparam>
        /// <typeparam name="T">The destination element type</typeparam>
        /// <param name="array">The list to convert (may be null)</param>
        /// <param name="func">The function that converts an element, the second argument is the index of the element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A new array with the converted elements (in the same order as the source), or null if <paramref name="array"/> is null.
        /// If any conversion fails, the returned task is faulted (after all conversions have completed).</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="func"/> is null and the <paramref name="array"/> is non-empty</exception>
        public static async Task<T[]> ConvertAsync<E, T>(this IReadOnlyList<E> array, Func<E, int, Task<T>> func, int maxConcurrency = 0)
        {
            if (array == null)
                return null;
            var l = array.Count;
            if (l <= 0)
                return Array.Empty<T>();
            if (l == 1)
                return [await func(array[0], 0).ConfigureAwait(false)];
            ConcurrencyLimiter.LimitConcurrency(ref func, maxConcurrency, l);
            var tt = GC.AllocateUninitializedArray<Task<T>>(l);
            for (int i = 0; i < l; ++i)
            {
                try
                {
                    tt[i] = func(array[i], i);
                }
                catch (Exception ex) when (i > 0)
                {
                    //  Earlier items are already running, so fault the result (after all items have completed) instead of throwing and leaving them unobserved
                    tt[i] = Task.FromException<T>(ex);
                }
            }
            // The non-generic WhenAll doesn't copy the task array (the generic one does, and also allocates a result array)
            await Task.WhenAll((Task[])tt).ConfigureAwait(false);
            return GetResults(tt);
        }



        /// <summary>
        /// Convert an array to another element type using a function.
        /// Elements are converted in parallel (async).
        /// </summary>
        /// <typeparam name="E">The source element type</typeparam>
        /// <typeparam name="T">The destination element type</typeparam>
        /// <param name="array">The list to convert (may be null)</param>
        /// <param name="func">The function that converts an element, the second argument is the index of the element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A new array with the converted elements (in the same order as the source), or null if <paramref name="array"/> is null.
        /// If any conversion fails, the returned task is faulted (after all conversions have completed).</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="func"/> is null and the <paramref name="array"/> is non-empty</exception>
        public static ValueTask<T[]> ConvertAsyncValue<E, T>(this IReadOnlyList<E> array, Func<E, int, ValueTask<T>> func, int maxConcurrency = 0)
        {
            if (array == null)
                return default;
            var l = array.Count;
            if (l <= 0)
                return TaskExt<T>.EmptyArrayValueTask;
            if (l == 1)
                return ValueTaskToArray(func(array[0], 0));
            ConcurrencyLimiter.LimitConcurrency(ref func, maxConcurrency, l);
            var tt = GC.AllocateUninitializedArray<ValueTask<T>>(l);
            for (int i = 0; i < l; ++i)
            {
                try
                {
                    tt[i] = func(array[i], i);
                }
                catch (Exception ex) when (i > 0)
                {
                    //  Earlier items are already running, so fault the result (after all items have completed) instead of throwing and leaving them unobserved
                    tt[i] = ValueTask.FromException<T>(ex);
                }
            }
            return TaskExt.WhenAll(tt);
        }


        /// <summary>
        /// Converts a dictionary to an array of some type
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <typeparam name="T">The destination element type</typeparam>
        /// <param name="dict">The dictionary to convert (may be null)</param>
        /// <param name="func">The function to use for instance creation, the last argument is the index of the element</param>
        /// <returns>A new array with the converted elements (in the enumeration order of the source), or null if the source is null</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="func"/> is null and the source is non-empty (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        public static T[] ToArray<K, V, T>(this IReadOnlyDictionary<K, V> dict, Func<K, V, int, T> func)
        {
            if (dict == null)
                return null;
            var l = dict.Count;
            if (l <= 0)
                return Array.Empty<T>();
#if DEBUG
            ArgumentNullException.ThrowIfNull(func);
#endif//DEBUG
            var tt = GC.AllocateUninitializedArray<T>(l);
            int i = 0;
            foreach (var kv in dict)
            {
                //  The source may be modified concurrently (ex: a ConcurrentDictionary), so the number of items may differ from the count
                if (i >= tt.Length)
                    Array.Resize(ref tt, i + (i >> 1) + 1);
                tt[i] = func(kv.Key, kv.Value, i);
                ++i;
            }
            if (i != tt.Length)
                Array.Resize(ref tt, i);
            return tt;
        }

        /// <summary>
        /// Converts a dictionary to an array of some type in parallel
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <typeparam name="T">The destination element type</typeparam>
        /// <param name="dict">The dictionary to convert (may be null)</param>
        /// <param name="func">The function to use for instance creation, the last argument is the index of the element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A new array with the converted elements (in the enumeration order of the source), or null if the source is null.
        /// If any conversion fails, the returned task is faulted (after all conversions have completed).</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="func"/> is null and the source is non-empty</exception>
        public static async Task<T[]> ToArrayAsync<K, V, T>(this IReadOnlyDictionary<K, V> dict, Func<K, V, int, Task<T>> func, int maxConcurrency = 0)
        {
            if (dict == null)
                return null;
            var l = dict.Count;
            if (l <= 0)
                return Array.Empty<T>();
            if (l == 1)
            {
                var kv = dict.First();
                return [await func(kv.Key, kv.Value, 0).ConfigureAwait(false)];
            }
            ConcurrencyLimiter.LimitConcurrency(ref func, maxConcurrency, l);
            var tt = GC.AllocateUninitializedArray<Task<T>>(l);
            int i = 0;
            foreach (var kv in dict)
            {
                //  The source may be modified concurrently (ex: a ConcurrentDictionary), so the number of items may differ from the count
                if (i >= tt.Length)
                    Array.Resize(ref tt, i + (i >> 1) + 1);
                try
                {
                    tt[i] = func(kv.Key, kv.Value, i);
                }
                catch (Exception ex) when (i > 0)
                {
                    //  Earlier items are already running, so fault the result (after all items have completed) instead of throwing and leaving them unobserved
                    tt[i] = Task.FromException<T>(ex);
                }
                ++i;
            }
            if (i != tt.Length)
                Array.Resize(ref tt, i);
            // The non-generic WhenAll doesn't copy the task array (the generic one does, and also allocates a result array)
            await Task.WhenAll((Task[])tt).ConfigureAwait(false);
            return GetResults(tt);
        }

        /// <summary>
        /// Converts a dictionary to an array of some type in parallel
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <typeparam name="T">The destination element type</typeparam>
        /// <param name="dict">The dictionary to convert (may be null)</param>
        /// <param name="func">The function to use for instance creation, the last argument is the index of the element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A new array with the converted elements (in the enumeration order of the source), or null if the source is null.
        /// If any conversion fails, the returned task is faulted (after all conversions have completed).</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="func"/> is null and the source is non-empty</exception>
        public static ValueTask<T[]> ToArrayValueAsync<K, V, T>(this IReadOnlyDictionary<K, V> dict, Func<K, V, int, ValueTask<T>> func, int maxConcurrency = 0)
        {
            if (dict == null)
                return default;
            var l = dict.Count;
            if (l <= 0)
                return TaskExt<T>.EmptyArrayValueTask;
            if (l == 1)
            {
                var kv = dict.First();
                return ValueTaskToArray(func(kv.Key, kv.Value, 0));
            }
            ConcurrencyLimiter.LimitConcurrency(ref func, maxConcurrency, l);
            var tt = GC.AllocateUninitializedArray<ValueTask<T>>(l);
            int i = 0;
            foreach (var kv in dict)
            {
                //  The source may be modified concurrently (ex: a ConcurrentDictionary), so the number of items may differ from the count
                if (i >= tt.Length)
                    Array.Resize(ref tt, i + (i >> 1) + 1);
                try
                {
                    tt[i] = func(kv.Key, kv.Value, i);
                }
                catch (Exception ex) when (i > 0)
                {
                    //  Earlier items are already running, so fault the result (after all items have completed) instead of throwing and leaving them unobserved
                    tt[i] = ValueTask.FromException<T>(ex);
                }
                ++i;
            }
            if (i != tt.Length)
                Array.Resize(ref tt, i);
            return TaskExt.WhenAll(tt);
        }


        /// <summary>
        /// Converts a collection to an array of some type
        /// </summary>
        /// <typeparam name="K">The source element type</typeparam>
        /// <typeparam name="T">The destination element type</typeparam>
        /// <param name="col">The collection to convert (may be null)</param>
        /// <param name="func">The function to use for instance creation, the last argument is the index of the element</param>
        /// <returns>A new array with the converted elements (in the enumeration order of the source), or null if the source is null</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="func"/> is null and the source is non-empty (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        public static T[] ToArray<K, T>(this IReadOnlyCollection<K> col, Func<K, int, T> func)
        {
            if (col == null)
                return null;
            var l = col.Count;
            if (l <= 0)
                return Array.Empty<T>();
#if DEBUG
            ArgumentNullException.ThrowIfNull(func);
#endif//DEBUG
            var tt = GC.AllocateUninitializedArray<T>(l);
            int i = 0;
            foreach (var kv in col)
            {
                //  The source may be modified concurrently (ex: a ConcurrentDictionary), so the number of items may differ from the count
                if (i >= tt.Length)
                    Array.Resize(ref tt, i + (i >> 1) + 1);
                tt[i] = func(kv, i);
                ++i;
            }
            if (i != tt.Length)
                Array.Resize(ref tt, i);
            return tt;
        }

        /// <summary>
        /// Converts a collection to an array of some type in parallel
        /// </summary>
        /// <typeparam name="K">The source element type</typeparam>
        /// <typeparam name="T">The destination element type</typeparam>
        /// <param name="col">The collection to convert (may be null)</param>
        /// <param name="func">The function to use for instance creation, the last argument is the index of the element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A new array with the converted elements (in the enumeration order of the source), or null if the source is null.
        /// If any conversion fails, the returned task is faulted (after all conversions have completed).</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="func"/> is null and the source is non-empty</exception>
        public static async Task<T[]> ToArrayAsync<K, T>(this IReadOnlyCollection<K> col, Func<K, int, Task<T>> func, int maxConcurrency = 0)
        {
            if (col == null)
                return null;
            var l = col.Count;
            if (l <= 0)
                return Array.Empty<T>();
            if (l == 1)
                return [await func(col.First(), 0).ConfigureAwait(false)];
            ConcurrencyLimiter.LimitConcurrency(ref func, maxConcurrency, l);
            var tt = GC.AllocateUninitializedArray<Task<T>>(l);
            int i = 0;
            foreach (var kv in col)
            {
                //  The source may be modified concurrently (ex: a ConcurrentDictionary), so the number of items may differ from the count
                if (i >= tt.Length)
                    Array.Resize(ref tt, i + (i >> 1) + 1);
                try
                {
                    tt[i] = func(kv, i);
                }
                catch (Exception ex) when (i > 0)
                {
                    //  Earlier items are already running, so fault the result (after all items have completed) instead of throwing and leaving them unobserved
                    tt[i] = Task.FromException<T>(ex);
                }
                ++i;
            }
            if (i != tt.Length)
                Array.Resize(ref tt, i);
            // The non-generic WhenAll doesn't copy the task array (the generic one does, and also allocates a result array)
            await Task.WhenAll((Task[])tt).ConfigureAwait(false);
            return GetResults(tt);
        }

        /// <summary>
        /// Converts a collection to an array of some type in parallel
        /// </summary>
        /// <typeparam name="K">The source element type</typeparam>
        /// <typeparam name="T">The destination element type</typeparam>
        /// <param name="col">The collection to convert (may be null)</param>
        /// <param name="func">The function to use for instance creation, the last argument is the index of the element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A new array with the converted elements (in the enumeration order of the source), or null if the source is null.
        /// If any conversion fails, the returned task is faulted (after all conversions have completed).</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="func"/> is null and the source is non-empty</exception>
        public static ValueTask<T[]> ToArrayValueAsync<K, T>(this IReadOnlyCollection<K> col, Func<K, int, ValueTask<T>> func, int maxConcurrency = 0)
        {
            if (col == null)
                return default;
            var l = col.Count;
            if (l <= 0)
                return TaskExt<T>.EmptyArrayValueTask;
            if (l == 1)
                return ValueTaskToArray(func(col.First(), 0));
            ConcurrencyLimiter.LimitConcurrency(ref func, maxConcurrency, l);
            var tt = GC.AllocateUninitializedArray<ValueTask<T>>(l);
            int i = 0;
            foreach (var kv in col)
            {
                //  The source may be modified concurrently (ex: a ConcurrentDictionary), so the number of items may differ from the count
                if (i >= tt.Length)
                    Array.Resize(ref tt, i + (i >> 1) + 1);
                try
                {
                    tt[i] = func(kv, i);
                }
                catch (Exception ex) when (i > 0)
                {
                    //  Earlier items are already running, so fault the result (after all items have completed) instead of throwing and leaving them unobserved
                    tt[i] = ValueTask.FromException<T>(ex);
                }
                ++i;
            }
            if (i != tt.Length)
                Array.Resize(ref tt, i);
            return TaskExt.WhenAll(tt);
        }

        /// <summary>
        /// The maximum number of elements where the temporary index buffer of StableSort is stack allocated (4 KB)
        /// </summary>
        const int StableSortMaxStack = 1024;

        /// <summary>
        /// In-place stable sort using 4n bytes of memory (stack allocated if less than 4kb, else rented from the array pool)
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="list">List to sort (may be null)</param>
        /// <param name="fn">Compare function with values and their (original) indices, should return a negative value if the first element should be placed before the second element, zero if they are equal and a positive value if the first element should be placed after the second.
        /// Elements that compare equal keep their original order.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="fn"/> is null and the <paramref name="list"/> contains more than one element (debug builds only; release builds throw an <see cref="InvalidOperationException"/> from the sort, before the <paramref name="list"/> is modified)</exception>
        /// <exception cref="NotSupportedException">Thrown if the <paramref name="list"/> is read only</exception>
        public static void StableSort<T>(this IList<T> list, Func<T, int, T, int, int> fn)
        {
            if (list == null)
                return;
            var count = list.Count;
            if (count <= 1)
                return;
#if DEBUG
            ArgumentNullException.ThrowIfNull(fn);
#endif//DEBUG
            if (count > StableSortMaxStack)
            {
                StableSortPooled(list, fn, count);
                return;
            }
            // NOTE: Keep the stack allocated (common) path free from the array pool handling, it makes it significantly slower (measured)
            Span<int> temp = stackalloc int[count];
            for (int i = 0; i < count; ++i)
                temp[i] = i;
            temp.Sort((a, b) =>
            {
                var c = fn(list[a], a, list[b], b);
                return c == 0 ? a.CompareTo(b) : c;
            });
            ApplyPermutation(list, temp);
        }

        static void StableSortPooled<T>(IList<T> list, Func<T, int, T, int, int> fn, int count)
        {
            var rented = ArrayPool<int>.Shared.Rent(count);
            try
            {
                var temp = rented.AsSpan(0, count);
                for (int i = 0; i < count; ++i)
                    temp[i] = i;
                temp.Sort((a, b) =>
                {
                    var c = fn(list[a], a, list[b], b);
                    return c == 0 ? a.CompareTo(b) : c;
                });
                ApplyPermutation(list, temp);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// In-place stable sort using 4n bytes of memory (stack allocated if less than 4kb, else rented from the array pool)
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="list">List to sort (may be null)</param>
        /// <param name="fn">Compare function with values, should return a negative value if the first element should be placed before the second element, zero if they are equal and a positive value if the first element should be placed after the second.
        /// Elements that compare equal keep their original order.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="fn"/> is null and the <paramref name="list"/> contains more than one element (debug builds only; release builds throw an <see cref="InvalidOperationException"/> from the sort, before the <paramref name="list"/> is modified)</exception>
        /// <exception cref="NotSupportedException">Thrown if the <paramref name="list"/> is read only</exception>
        public static void StableSort<T>(this IList<T> list, Func<T, T, int> fn)
        {
            if (list == null)
                return;
            var count = list.Count;
            if (count <= 1)
                return;
#if DEBUG
            ArgumentNullException.ThrowIfNull(fn);
#endif//DEBUG
            if (count > StableSortMaxStack)
            {
                StableSortPooled(list, fn, count);
                return;
            }
            // NOTE: Keep the stack allocated (common) path free from the array pool handling, it makes it significantly slower (measured)
            Span<int> temp = stackalloc int[count];
            for (int i = 0; i < count; ++i)
                temp[i] = i;
            temp.Sort((a, b) =>
            {
                var c = fn(list[a], list[b]);
                return c == 0 ? a.CompareTo(b) : c;
            });
            ApplyPermutation(list, temp);
        }

        static void StableSortPooled<T>(IList<T> list, Func<T, T, int> fn, int count)
        {
            var rented = ArrayPool<int>.Shared.Rent(count);
            try
            {
                var temp = rented.AsSpan(0, count);
                for (int i = 0; i < count; ++i)
                    temp[i] = i;
                temp.Sort((a, b) =>
                {
                    var c = fn(list[a], list[b]);
                    return c == 0 ? a.CompareTo(b) : c;
                });
                ApplyPermutation(list, temp);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Re-order the elements of a list in-place so that list[i] = original list[temp[i]] (the temp is modified)
        /// </summary>
        static void ApplyPermutation<T>(IList<T> list, Span<int> temp)
        {
            var count = temp.Length;
            for (int i = 0; i < count; ++i)
            {
                var s = temp[i];
                if (s == i)
                    continue;
                var v = list[i];
                var j = i;
                while (s != i)
                {
                    list[j] = list[s];
                    temp[j] = j;
                    j = s;
                    s = temp[j];
                }
                list[j] = v;
                temp[j] = j;
            }
        }



        /// <summary>
        /// Get the intersection of two sorted lists (the elements that exists in both lists).
        /// List1 and List2 must be sorted (using the same comparer) and contain no duplicates!
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="list1">The first sorted list</param>
        /// <param name="list2">The second sorted list</param>
        /// <param name="comparer">An optional comparer (the lists must be sorted using this comparer), if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>A new list with the elements (from <paramref name="list1"/>) that exists in both lists, in sorted order</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="list1"/> or <paramref name="list2"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        public static List<T> IntersectSorted<T>(this IReadOnlyList<T> list1, IReadOnlyList<T> list2, IComparer<T> comparer = default)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(list1);
            ArgumentNullException.ThrowIfNull(list2);
#endif//DEBUG
            var c1 = list1.Count;
            var c2 = list2.Count;
            var result = new List<T>(Math.Min(c1, c2));
            int i = 0, j = 0;
            comparer = comparer ?? Comparer<T>.Default;
            while (i < c1 && j < c2)
            {
                var v = list1[i];
                int cmp = comparer.Compare(v, list2[j]);
                if (cmp == 0)
                {
                    result.Add(v);
                    i++;
                    j++;
                }
                else if (cmp < 0)
                {
                    i++; // Advance the smaller item
                }
                else
                {
                    j++; // Advance the smaller item
                }
            }

            return result;
        }



    }

}
