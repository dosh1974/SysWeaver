using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace SysWeaver
{
    public static class EnumerableExt
    {

        /// <summary>
        /// Find the min and max value in a sequence
        /// </summary>
        /// <typeparam name="T">The enumerable type</typeparam>
        /// <typeparam name="E">The element type (value to extract from T)</typeparam>
        /// <param name="enumerable">The sequence to enumerate</param>
        /// <param name="predicate">A function to extract a value from an element in the sequence</param>
        /// <param name="comparer">An optional comaprer to use</param>
        /// <returns>The min and max value found, if sequence is empty, two default(E) is returned</returns>
        public static Tuple<E, E> MinMax<T, E>(this IEnumerable<T> enumerable, Func<T, E> predicate, IComparer<E> comparer = null)
        {
            if (comparer == null)
                comparer = Comparer<E>.Default;
            using var e = enumerable.GetEnumerator();
            if (!e.MoveNext())
                return Tuple.Create<E, E>(default, default);
            var c = predicate(e.Current);
            E min = c;
            E max = c;
            while (e.MoveNext())
            {
                c = predicate(e.Current);
                if (comparer.Compare(c, min) < 0)
                    min = c;
                if (comparer.Compare(c, max) > 0)
                    max = c;
            }
            return Tuple.Create(min, max);
        }


        /// <summary>
        /// Find the min and max value in a sequence
        /// </summary>
        /// <typeparam name="T">The enumerable type</typeparam>
        /// <param name="enumerable">The sequence to enumerate</param>
        /// <param name="comparer">An optional comaprer to use</param>
        /// <returns>The min and max value found, if sequence is empty, two default(T) is returned</returns>
        public static Tuple<T, T> MinMax<T>(this IEnumerable<T> enumerable, IComparer<T> comparer = null)
        {
            if (comparer == null)
                comparer = Comparer<T>.Default;
            using var e = enumerable.GetEnumerator();
            if (!e.MoveNext())
                return Tuple.Create<T, T>(default, default);
            var c = e.Current;
            T min = c;
            T max = c;
            while (e.MoveNext())
            {
                c = e.Current;
                if (comparer.Compare(c, min) < 0)
                    min = c;
                if (comparer.Compare(c, max) > 0)
                    max = c;
            }
            return Tuple.Create(min, max);
        }


        /*
        /// <summary>
        /// Create a dictionary from some values.
        /// Will not throw on duplicate keys, rather the last value will be used.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TVal"></typeparam>
        /// <param name="enumerable"></param>
        /// <param name="keyExtractor">A function that extract / creates the key for the given value</param>
        /// <param name="comparer">Optional comparer</param>
        /// <returns>A dictionary with the values</returns>
        public static Dictionary<TKey, TVal> ToDictionary<TKey, TVal>(this IEnumerable<TVal> enumerable, Func<TVal, TKey> keyExtractor, IEqualityComparer<TKey> comparer = null)
        {
            var d = comparer == null ? new Dictionary<TKey, TVal>() : new Dictionary<TKey, TVal>(comparer);
            foreach (var v in enumerable)
                d[keyExtractor(v)] = v;
            return d;
        }
        */

        /// <summary>
        /// Create a concurrent dictionary from some values.
        /// Will not throw on duplicate keys, rather the last value will be used.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TVal"></typeparam>
        /// <param name="enumerable"></param>
        /// <param name="keyExtractor">A function that extract / creates the key for the given value</param>
        /// <param name="comparer">Optional comparer</param>
        /// <returns>A concurrent dictionary with the values</returns>
        public static ConcurrentDictionary<TKey, TVal> ToConcurrentDictionary<TKey, TVal>(this IEnumerable<TVal> enumerable, Func<TVal, TKey> keyExtractor, IEqualityComparer<TKey> comparer = null)
        {
            var d = comparer == null ? new ConcurrentDictionary<TKey, TVal>() : new ConcurrentDictionary<TKey, TVal>(comparer);
            foreach (var v in enumerable)
                d[keyExtractor(v)] = v;
            return d;
        }


        /// <summary>
        /// Return a single value as an enumerable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value to return as an IEnumerable</param>
        /// <returns></returns>
        public static IEnumerable<T> AsEnumerable<T>(T value) => new IntEnum<T>(value);

        struct IntEnum<T> : IEnumerable<T>, IEnumerator<T>
        {
            public IntEnum(T val)
            {
                Value = val;
                State = 0;
            }

            readonly T Value;
            int State;

            public T Current => State == 1 ? Value : default;

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public IEnumerator<T> GetEnumerator() => this;

            public bool MoveNext()
            {
                var s = State;
                ++s;
                if (s > 2)
                    s = 2;
                State = s;
                return s == 1;
            }

            public void Reset()
            {
                State = 0;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }



        /// <summary>
        /// Process all elements in a list.
        /// Elements are processed in paralell (async).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list">The list to process</param>
        /// <param name="action">The action to perform on each element</param>
        /// <returns></returns>
        public static async Task ProcessAsync<T>(this IReadOnlyList<T> list, Func<T, Task> action)
        {
            if (list == null)
                return;
            var l = list.Count;
            if (l <= 0)
                return;
            var tt = GC.AllocateUninitializedArray<Task>(l);
            for (int i = 0; i < l; ++i)
                tt[i] = action(list[i]);
            await Task.WhenAll(tt).ConfigureAwait(false);
        }

        /// <summary>
        /// Process all elements in a list.
        /// Elements are processed in paralell (async).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable">The enumerable to process</param>
        /// <param name="action">The action to perform on each element</param>
        /// <returns></returns>
        public static Task ProcessAsync<T>(this IEnumerable<T> enumerable, Func<T, Task> action)
            => ProcessAsync(enumerable.ToList(), action);

        /// <summary>
        /// Process all elements in a list.
        /// Elements are processed in paralell (async).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list">The list to process</param>
        /// <param name="action">The action to perform on each element</param>
        /// <returns></returns>
        public static async ValueTask ProcessAsyncValue<T>(this IReadOnlyList<T> list, Func<T, ValueTask> action)
        {
            if (list == null)
                return;
            var l = list.Count;
            if (l <= 0)
                return;
            var tt = GC.AllocateUninitializedArray<ValueTask>(l);
            for (int i = 0; i < l; ++i)
                tt[i] = action(list[i]);
            await TaskExt.WhenAll(tt).ConfigureAwait(false);
        }

        /// <summary>
        /// Process all elements in a list.
        /// Elements are processed in paralell (async).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable">The enumerable to process</param>
        /// <param name="action">The action to perform on each element</param>
        /// <returns></returns>
        public static ValueTask ProcessAsyncValue<T>(this IEnumerable<T> enumerable, Func<T, ValueTask> action)
            => ProcessAsyncValue(enumerable.ToList(), action);





        /// <summary>
        /// Process all elements in a list.
        /// Elements are processed in paralell (async).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list">The list to process</param>
        /// <param name="action">The action to perform on each element</param>
        /// <returns></returns>
        public static async Task ProcessAsync<T>(this IReadOnlyList<T> list, Func<T, int, Task> action)
        {
            if (list == null)
                return;
            var l = list.Count;
            if (l <= 0)
                return;
            var tt = GC.AllocateUninitializedArray<Task>(l);
            for (int i = 0; i < l; ++i)
                tt[i] = action(list[i], i);
            await Task.WhenAll(tt).ConfigureAwait(false);
        }

        /// <summary>
        /// Process all elements in a list.
        /// Elements are processed in paralell (async).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable">The enumerable to process</param>
        /// <param name="action">The action to perform on each element</param>
        /// <returns></returns>
        public static Task ProcessAsync<T>(this IEnumerable<T> enumerable, Func<T, int, Task> action)
            => ProcessAsync(enumerable.ToList(), action);

        /// <summary>
        /// Process all elements in a list.
        /// Elements are processed in paralell (async).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list">The list to process</param>
        /// <param name="action">The action to perform on each element</param>
        /// <returns></returns>
        public static async ValueTask ProcessAsyncValue<T>(this IReadOnlyList<T> list, Func<T, int, ValueTask> action)
        {
            if (list == null)
                return;
            var l = list.Count;
            if (l <= 0)
                return;
            var tt = GC.AllocateUninitializedArray<ValueTask>(l);
            for (int i = 0; i < l; ++i)
                tt[i] = action(list[i], i);
            await TaskExt.WhenAll(tt).ConfigureAwait(false);
        }

        /// <summary>
        /// Process all elements in a list.
        /// Elements are processed in paralell (async).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable">The enumerable to process</param>
        /// <param name="action">The action to perform on each element</param>
        /// <returns></returns>
        public static ValueTask ProcessAsyncValue<T>(this IEnumerable<T> enumerable, Func<T, int, ValueTask> action)
            => ProcessAsyncValue(enumerable.ToList(), action);




        /// <summary>
        /// Only return unqiue keys
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="enumerable"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        public static IEnumerable<KeyValuePair<K, V>> WithUniqueKeys<K, V>(this IEnumerable<KeyValuePair<K, V>> enumerable, IEqualityComparer<K> comparer = default)
        {
            var seen = new HashSet<K>(comparer);
            foreach (var x in enumerable)
                if (seen.Add(x.Key))
                    yield return x;
        }

    }
}
