using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Contains generic collection extensions
    /// </summary>
    public static class CollectionExt
    {
        /// <summary>
        /// Returns the Count of the collection if its non-null, return 0 if the collection is null
        /// </summary>
        /// <typeparam name="T">Generic type argumet of the collection</typeparam>
        /// <param name="t">The collection instance</param>
        /// <returns>The number of items in the collection or 0 if <paramref name="t"/> is null</returns>
        public static int NullableCount<T>(this ICollection<T> t)
        {
            return t?.Count ?? 0;
        }

        /// <summary>
        /// Returns the Count of the readonly collection if its non-null, return 0 if the readonly collection is null
        /// </summary>
        /// <typeparam name="T">Generic type argumet of the readonly collection</typeparam>
        /// <param name="t">The readonly collection instance</param>
        /// <returns>The number of items in the collection or 0 if <paramref name="t"/> is null</returns>
        public static int NullableCountRo<T>(this IReadOnlyCollection<T> t)
        {
            return t?.Count ?? 0;
        }


        /// <summary>
        /// Returns an empty enumerable if the supplied parameter is null, this makes the code cleaner when iterating over collections etc that might be null
        /// </summary>
        /// <typeparam name="T">Generic type argumet of the enumerable</typeparam>
        /// <param name="t">The enumerable instance</param>
        /// <returns>The instance <paramref name="t"/> if it's non-null, else an empty collection instance</returns>
        public static IEnumerable<T> Nullable<T>(this IEnumerable<T> t)
        {
            return t ?? [];
        }

        /// <summary>
        /// Returns true if the enumerable is empty
        /// </summary>
        /// <param name="t">The enumerable instance</param>
        /// <returns>True if the instance <paramref name="t"/> is empty, else false</returns>
        public static bool IsEmpty(this IEnumerable t)
        {
            var e = t.GetEnumerator();
            using (e as IDisposable)
                return e.MoveNext();
        }



        /// <summary>
        /// Returns the index (position) of the first element that return true by the predicated
        /// </summary>
        /// <typeparam name="T">Generic type argumet of the enumerable</typeparam>
        /// <param name="t">The enumerable instance</param>
        /// <param name="predicate">A function that is evaluated for each value, return true to stop enumeration and return the index</param>
        /// <returns>The index of the element that first returned true from the predicate, or -1 if not found</returns>
        public static int IndexOf<T>(this IEnumerable<T> t, Func<T, bool> predicate)
        {
            int index = 0;
            var e = t.GetEnumerator();
            using (e as IDisposable)
            {
                while (e.MoveNext())
                {
                    if (predicate(e.Current))
                        return index;
                    ++index;
                }
            }
            return -1;
        }

        /// <summary>
        /// Returns the index (position) of the last element that return true by the predicated
        /// </summary>
        /// <typeparam name="T">Generic type argumet of the enumerable</typeparam>
        /// <param name="t">The enumerable instance</param>
        /// <param name="predicate">A function that is evaluated for each value, return true to return the index</param>
        /// <returns>The index of the element that last returned true from the predicate, or -1 if not found</returns>
        public static int LastIndexOf<T>(this IEnumerable<T> t, Func<T, bool> predicate)
        {
            int ret = -1;
            int index = 0;
            var e = t.GetEnumerator();
            using (e as IDisposable)
            {
                while (e.MoveNext())
                {
                    if (predicate(e.Current))
                        ret = index;
                    ++index;
                }
            }
            return ret;
        }

        /// <summary>
        /// If the collection is null or empty return null, else return an array with the elements
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection">The collection to convert to an array</param>
        /// <returns>null if the collection is null or empty, else an array of the elements</returns>
        public static T[] ArrayOrNullIfEmpty<T>(this IReadOnlyCollection<T> collection)
            => (collection?.Count ?? 0) <= 0 ? null : collection.ToArray();

        /// <summary>
        /// If the array is null or empty return null, else return it
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array">The array to check</param>
        /// <returns>null if the array is null or empty, else the array</returns>
        public static T[] ArrayOrNullIfEmpty<T>(this T[] array)
            => (array?.Length ?? 0) <= 0 ? null : array;





        /// <summary>
        /// Drain the queue and optionally try to keep a set number of entries
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="col"></param>
        /// <param name="keepAtLeast">The number of items to keep (in rare cases when multiple threads are draining the queue, the number of items can drop below)</param>
        public static void Drain<T>(this ConcurrentQueue<T> col, int keepAtLeast = 0)
        {
            while (col.Count > keepAtLeast)
                col.TryDequeue(out var _);
        }

        /// <summary>
        /// Drain a linked list (last) optionally try to keep a set number of entries
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="col"></param>
        /// <param name="keepAtLeast">The number of items to keep</param>
        public static void Drain<T>(this LinkedList<T> col, int keepAtLeast = 0)
        {
            while (col.Count > keepAtLeast)
                col.RemoveLast();
        }

    }




}
