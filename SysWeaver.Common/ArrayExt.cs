using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SysWeaver
{


    public static class ArrayExt
    {


        /// <summary>
        /// Get a value from an array, return a value on fail.
        /// Will fail if array is null or if the index is out of bound.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="a"></param>
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
        /// <typeparam name="T"></typeparam>
        /// <param name="a"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        public static T[] Push<T>(this T[] a, T val)
        {
            if (a == null)
                return [val];
            return [..a, val];
        }

        /// <summary>
        /// Concat two arrays.
        /// If both arrays are null, null is returned.
        /// If any array is null, the other array is returned.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
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
        /// <typeparam name="T"></typeparam>
        /// <param name="count"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static T[] Create<T>(int count, T value)
        {
            var t = GC.AllocateUninitializedArray<T>(count);
            var p = t.AsSpan();
            for (int i = 0; i < count; ++i)
                p[i] = value;
            return t;
        }



        /// <summary>
        /// Take N elements from an enumerable and create an array of them
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="values"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static T[] ToArray<T>(this IEnumerable<T> values, int count)
        {
            var t = GC.AllocateUninitializedArray<T>(count);
            var p = t.AsSpan();
            using var e = values.GetEnumerator();
            int i;
            for (i = 0; i < count; ++i)
                p[i] = e.MoveNext() ? e.Current : default(T);
            if (i < count)
                Array.Resize(ref t, i);
            return t;
        }




        /// <summary>
        /// Create a new re-ordered array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="values">The original array</param>
        /// <param name="order">The new order, ex: newArray[0] = values[order[0]]</param>
        /// <returns>A ew array with the elements ordered according to the order</returns>
        public static T[] Reordered<T>(this IReadOnlyList<T> values, IReadOnlyList<int> order)
        {
            if (values == null)
                return null;
            var c = values.Count;
            var r = new T[c];
            for (int i = 0; i < c; ++i)
                r[i] = values[order[i]];
            return r;
        }


        /// <summary>
        /// Clones (shallow) an array of primitive types (using a fast mem copy).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <returns></returns>
        public unsafe static T[] ShallowClonePrimitive<T>(this T[] array) where T : unmanaged
        {
            if (array == null)
                return array;
            var c = array.Length;
            var t = new T[c];
            var sizeZ = Marshal.SizeOf<T>();
            var sr = array.AsSpan().GetPinnableReference();
            var dr = t.AsSpan().GetPinnableReference();
            var size = sizeZ * c;
            Buffer.MemoryCopy((byte*)Unsafe.AsPointer(ref sr), (byte*)Unsafe.AsPointer(ref dr), size, size);
            return t;
        }

        /// <summary>
        /// Clones (shallow) an array (if the type T is primitive, please use the faster ShallowClonePrimitive instead).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <returns></returns>
        public unsafe static T[] ShallowClone<T>(this T[] array) where T : class
        {
            if (array == null)
                return array;
            var c = array.Length;
            var t = new T[c];
            if (typeof(T).IsPrimitive)
            {
                var sizeZ = Marshal.SizeOf<T>();
                var sr = array.AsSpan().GetPinnableReference();
                var dr = t.AsSpan().GetPinnableReference();
                var size = sizeZ * c;
                Buffer.MemoryCopy((byte*)Unsafe.AsPointer(ref sr), (byte*)Unsafe.AsPointer(ref dr), size, size);
            }
            else
            {
                for (int i = 0; i < c; ++i)
                    t[i] = array[i];
            }
            return t;
        }

        /// <summary>
        /// Deep clones an array (the T must implement the IClone_T interface)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <returns></returns>
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
        /// Deep clones an array (the T must implement the IClone interface)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <returns></returns>
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
        /// Deep clones an array (the D must implement the IClone_T interface)
        /// </summary>
        /// <typeparam name="D"></typeparam>
        /// <typeparam name="S"></typeparam>
        /// <param name="array"></param>
        /// <returns></returns>
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


        public static T[] RemoveAt<T>(this T[] array, int index)
        {
            var l = array.Length;
            var n = new T[l - 1];
            if (index > 0)
                Array.Copy(array, 0, n, 0, index);
            var d = index + 1;
            if (d < l)
                Array.Copy(array, d, n, index, l - d);
            return n;
        }





        public static T[] InsertionSort<T>(this T[] array, Func<T, T, int> compareFn)
        {
            if (array == null)
                return array;
            int n = array.Length;
            if (n <= 1)
                return array;

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
        /// Convert an array to another element type using a function
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static T[] Convert<E, T>(this IReadOnlyList<E> array, Func<E, T> func)
        {
            if (array == null)
                return null;
            var l = array.Count;
            if (l <= 0)
                return Array.Empty<T>();
            var t = GC.AllocateUninitializedArray<T>(l);
            var p = t.AsSpan();
            for (int i = 0; i < l; ++i)
                p[i] = func(array[i]);
            return t;
        }

        /// <summary>
        /// Convert an array to another element type using a function.
        /// Elements are converted in paralell (async).
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static async Task<T[]> ConvertAsync<E, T>(this IReadOnlyList<E> array, Func<E, Task<T>> func)
        {
            if (array == null)
                return null;
            var l = array.Count;
            if (l <= 0)
                return Array.Empty<T>();
            var tt = GC.AllocateUninitializedArray<Task<T>>(l);
            for (int i = 0; i < l; ++i)
                tt[i] = func(array[i]);
            await Task.WhenAll(tt).ConfigureAwait(false);
            var t = GC.AllocateUninitializedArray<T>(l);
            for (int i = 0; i < l; ++i)
                t[i] = tt[i].Result;
            return t;
        }

        /// <summary>
        /// Convert an array to another element type using a function.
        /// Elements are converted in paralell (async).
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static async ValueTask<T[]> ConvertAsyncValue<E, T>(this IReadOnlyList<E> array, Func<E, ValueTask<T>> func)
        {
            if (array == null)
                return null;
            var l = array.Count;
            if (l <= 0)
                return Array.Empty<T>();
            var tt = GC.AllocateUninitializedArray<ValueTask<T>>(l);
            for (int i = 0; i < l; ++i)
                tt[i] = func(array[i]);
            await TaskExt.WhenAll(tt).ConfigureAwait(false);
            var t = GC.AllocateUninitializedArray<T>(l);
            for (int i = 0; i < l; ++i)
                t[i] = tt[i].Result;
            return t;
        }

    }

}
