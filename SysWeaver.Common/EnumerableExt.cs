using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace SysWeaver
{
    /// <summary>
    /// Contains extensions for enumerables and lists (min/max, conversions and processing, sync and async)
    /// </summary>
    public static class EnumerableExt
    {

        /// <summary>
        /// Find the min and max value in a sequence
        /// </summary>
        /// <typeparam name="T">The enumerable type</typeparam>
        /// <typeparam name="E">The element type (value to extract from T)</typeparam>
        /// <param name="enumerable">The sequence to enumerate</param>
        /// <param name="predicate">A function to extract a value from an element in the sequence</param>
        /// <param name="comparer">An optional comparer to use, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The min and max value found, if sequence is empty, two default(E) is returned.
        /// If multiple values are equal to the min (or max), the first one is returned.</returns>
        /// <remarks>Arrays are accessed directly (no enumerator is allocated)</remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> is null, or if <paramref name="enumerable"/> is null (debug builds only for <paramref name="enumerable"/>; release builds throw a <see cref="NullReferenceException"/>)</exception>
        public static Tuple<E, E> MinMax<T, E>(this IEnumerable<T> enumerable, Func<T, E> predicate, IComparer<E> comparer = null)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(enumerable);
#endif//DEBUG
            ArgumentNullException.ThrowIfNull(predicate);
            if (comparer == null)
                comparer = Comparer<E>.Default;
            E min;
            E max;
            if (enumerable is T[] a)
            {
                var s = new ReadOnlySpan<T>(a);
                var sl = s.Length;
                if (sl <= 0)
                    return Tuple.Create<E, E>(default, default);
                var v = predicate(s[0]);
                min = v;
                max = v;
                for (int i = 1; i < sl; ++i)
                {
                    v = predicate(s[i]);
                    if (comparer.Compare(v, min) < 0)
                        min = v;
                    if (comparer.Compare(v, max) > 0)
                        max = v;
                }
                return Tuple.Create(min, max);
            }
            using var e = enumerable.GetEnumerator();
            if (!e.MoveNext())
                return Tuple.Create<E, E>(default, default);
            var c = predicate(e.Current);
            min = c;
            max = c;
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
        /// <param name="comparer">An optional comparer to use, if null <see cref="Comparer{T}.Default"/> is used</param>
        /// <returns>The min and max value found, if sequence is empty, two default(T) is returned.
        /// If multiple values are equal to the min (or max), the first one is returned.</returns>
        /// <remarks>Arrays are accessed directly (no enumerator is allocated)</remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="enumerable"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        public static Tuple<T, T> MinMax<T>(this IEnumerable<T> enumerable, IComparer<T> comparer = null)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(enumerable);
#endif//DEBUG
            if (comparer == null)
                comparer = Comparer<T>.Default;
            T min;
            T max;
            if (enumerable is T[] a)
            {
                var s = new ReadOnlySpan<T>(a);
                var sl = s.Length;
                if (sl <= 0)
                    return Tuple.Create<T, T>(default, default);
                var v = s[0];
                min = v;
                max = v;
                for (int i = 1; i < sl; ++i)
                {
                    v = s[i];
                    if (comparer.Compare(v, min) < 0)
                        min = v;
                    if (comparer.Compare(v, max) > 0)
                        max = v;
                }
                return Tuple.Create(min, max);
            }
            using var e = enumerable.GetEnumerator();
            if (!e.MoveNext())
                return Tuple.Create<T, T>(default, default);
            var c = e.Current;
            min = c;
            max = c;
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
        /// <typeparam name="TKey">The key type</typeparam>
        /// <typeparam name="TVal">The value type (the element type of the enumerable)</typeparam>
        /// <param name="enumerable">The values to add</param>
        /// <param name="keyExtractor">A function that extract / creates the key for the given value</param>
        /// <param name="comparer">Optional comparer, if null the default comparer is used</param>
        /// <returns>A concurrent dictionary with the values</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="keyExtractor"/> is null, if the <paramref name="keyExtractor"/> returns a null key, or if <paramref name="enumerable"/> is null (debug builds only for <paramref name="enumerable"/>; release builds throw a <see cref="NullReferenceException"/>)</exception>
        public static ConcurrentDictionary<TKey, TVal> ToConcurrentDictionary<TKey, TVal>(this IEnumerable<TVal> enumerable, Func<TVal, TKey> keyExtractor, IEqualityComparer<TKey> comparer = null)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(enumerable);
#endif//DEBUG
            ArgumentNullException.ThrowIfNull(keyExtractor);
            var d = comparer == null ? new ConcurrentDictionary<TKey, TVal>() : new ConcurrentDictionary<TKey, TVal>(comparer);
            foreach (var v in enumerable)
                d[keyExtractor(v)] = v;
            return d;
        }


        /// <summary>
        /// Return a single value as an enumerable
        /// </summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="value">The value to return as an IEnumerable</param>
        /// <returns>An enumerable that contains the <paramref name="value"/> (once).
        /// The enumerable can be enumerated any number of times.
        /// The first enumeration (on the creating thread) doesn't allocate an enumerator.</returns>
        public static IEnumerable<T> AsEnumerable<T>(T value) => new SingleEnum<T>(value);

        /// <summary>
        /// An enumerable that contains a single value.
        /// Same pattern as a compiler generated iterator, the first GetEnumerator call (on the creating thread) returns this instance (so only one allocation is needed in the typical use case).
        /// </summary>
        sealed class SingleEnum<T> : IEnumerable<T>, IEnumerator<T>
        {
            public SingleEnum(T val)
            {
                Value = val;
                State = -2;
                ThreadId = Environment.CurrentManagedThreadId;
            }

            SingleEnum(T val, int state)
            {
                Value = val;
                State = state;
            }

            readonly T Value;

            /// <summary>
            /// -2 = Not used as an enumerator yet, 0 = Before the value, 1 = At the value, 2 = After the value
            /// </summary>
            int State;

            readonly int ThreadId;

            public T Current => State == 1 ? Value : default;

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public IEnumerator<T> GetEnumerator()
            {
                if ((State == -2) && (ThreadId == Environment.CurrentManagedThreadId))
                {
                    State = 0;
                    return this;
                }
                return new SingleEnum<T>(Value, 0);
            }

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
        /// Process all elements in a list in reversed order (last to first)
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="list">The list to process, if null nothing is done</param>
        /// <param name="action">The action to perform on each element</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="action"/> is null and the <paramref name="list"/> is non-null (debug builds only; release builds throw a <see cref="NullReferenceException"/> if the <paramref name="list"/> is non-empty)</exception>
        public static void ProcessReverse<T>(this IReadOnlyList<T> list, Action<T> action)
        {
            if (list == null)
                return;
#if DEBUG
            ArgumentNullException.ThrowIfNull(action);
#endif//DEBUG
            var l = list.Count;
            while (l > 0)
            {
                --l;
                action(list[l]);
            }
        }

        /// <summary>
        /// Process all elements in a list.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="list">The list to process, if null nothing is done</param>
        /// <param name="action">The action to perform on each element</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="action"/> is null and the <paramref name="list"/> is non-null (debug builds only; release builds throw a <see cref="NullReferenceException"/> if the <paramref name="list"/> is non-empty)</exception>
        public static void Process<T>(this IReadOnlyList<T> list, Action<T> action)
        {
            if (list == null)
                return;
#if DEBUG
            ArgumentNullException.ThrowIfNull(action);
#endif//DEBUG
            var l = list.Count;
            for (int i = 0; i < l; i++)
                action(list[i]);
        }

        /// <summary>
        /// Process all elements in an enumerable.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="enumerable">The enumerable to process, if null nothing is done</param>
        /// <param name="action">The action to perform on each element</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="action"/> is null and the <paramref name="enumerable"/> is non-null</exception>
        public static void Process<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            if (enumerable == null)
                return;
            ArgumentNullException.ThrowIfNull(action);
            foreach (var i in enumerable)
                action(i);
        }

        /// <summary>
        /// Process all elements in a list.
        /// Elements are processed in parallel (async).
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="list">The list to process, if null nothing is done</param>
        /// <param name="action">The action to perform on each element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A task that completes when all elements are processed, if any action fails, the task is faulted (after all actions have completed)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="action"/> is null and the <paramref name="list"/> is non-empty</exception>
        public static Task ProcessAsync<T>(this IReadOnlyList<T> list, Func<T, Task> action, int maxConcurrency = 0)
        {
            if (list == null)
                return Task.CompletedTask;
            var l = list.Count;
            if (l <= 0)
                return Task.CompletedTask;
            if (l == 1)
                return action(list[0]);
            ConcurrencyLimiter.LimitConcurrency(ref action, maxConcurrency, l);
            var tt = GC.AllocateUninitializedArray<Task>(l);
            for (int i = 0; i < l; ++i)
                tt[i] = action(list[i]);
            return Task.WhenAll(tt);
        }

        /// <summary>
        /// Process all elements in an enumerable.
        /// Elements are processed in parallel (async).
        /// </summary>
        /// <remarks>
        /// If the <paramref name="enumerable"/> is a <see cref="IReadOnlyList{T}"/> (arrays, List etc) it's used directly, else the elements are first copied to a list.
        /// </remarks>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="enumerable">The enumerable to process, if null nothing is done</param>
        /// <param name="action">The action to perform on each element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A task that completes when all elements are processed, if any action fails, the task is faulted (after all actions have completed)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="action"/> is null and the <paramref name="enumerable"/> is non-empty</exception>
        public static Task ProcessAsync<T>(this IEnumerable<T> enumerable, Func<T, Task> action, int maxConcurrency = 0)
            => ProcessAsync(AsList(enumerable), action, maxConcurrency);

        /// <summary>
        /// Get an enumerable as a read only list, without copying if possible
        /// </summary>
        static IReadOnlyList<T> AsList<T>(IEnumerable<T> enumerable)
            => enumerable == null ? null : (enumerable as IReadOnlyList<T> ?? enumerable.ToList());

        /// <summary>
        /// Process all elements in a list.
        /// Elements are processed in parallel (async).
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="list">The list to process, if null nothing is done</param>
        /// <param name="action">The action to perform on each element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A task that completes when all elements are processed, if any action fails, the task is faulted (after all actions have completed)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="action"/> is null and the <paramref name="list"/> is non-empty</exception>
        public static ValueTask ProcessAsyncValue<T>(this IReadOnlyList<T> list, Func<T, ValueTask> action, int maxConcurrency = 0)
        {
            if (list == null)
                return ValueTask.CompletedTask;
            var l = list.Count;
            if (l <= 0)
                return ValueTask.CompletedTask;
            if (l == 1)
                return action(list[0]);
            ConcurrencyLimiter.LimitConcurrency(ref action, maxConcurrency, l);
            var tt = GC.AllocateUninitializedArray<ValueTask>(l);
            for (int i = 0; i < l; ++i)
                tt[i] = action(list[i]);
            return TaskExt.WhenAll(tt);
        }

        /// <summary>
        /// Process all elements in an enumerable.
        /// Elements are processed in parallel (async).
        /// </summary>
        /// <remarks>
        /// Same as <see cref="ProcessAsync{T}(IEnumerable{T}, Func{T, Task}, int)"/>.
        /// If the <paramref name="enumerable"/> is a <see cref="IReadOnlyList{T}"/> (arrays, List etc) it's used directly, else the elements are first copied to a list.
        /// </remarks>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="enumerable">The enumerable to process, if null nothing is done</param>
        /// <param name="action">The action to perform on each element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A task that completes when all elements are processed, if any action fails, the task is faulted (after all actions have completed)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="action"/> is null and the <paramref name="enumerable"/> is non-empty</exception>
        public static Task ProcessAsyncValue<T>(this IEnumerable<T> enumerable, Func<T, Task> action, int maxConcurrency = 0)
            => ProcessAsync(AsList(enumerable), action, maxConcurrency);





        /// <summary>
        /// Process all elements in a list.
        /// Elements are processed in parallel (async).
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="list">The list to process, if null nothing is done</param>
        /// <param name="action">The action to perform on each element, the second argument is the index of the element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A task that completes when all elements are processed, if any action fails, the task is faulted (after all actions have completed)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="action"/> is null and the <paramref name="list"/> is non-empty</exception>
        public static Task ProcessAsync<T>(this IReadOnlyList<T> list, Func<T, int, Task> action, int maxConcurrency = 0)
        {
            if (list == null)
                return Task.CompletedTask;
            var l = list.Count;
            if (l <= 0)
                return Task.CompletedTask;
            if (l == 1)
                return action(list[0], 0);
            ConcurrencyLimiter.LimitConcurrency(ref action, maxConcurrency, l);
            var tt = GC.AllocateUninitializedArray<Task>(l);
            for (int i = 0; i < l; ++i)
                tt[i] = action(list[i], i);
            return Task.WhenAll(tt);
        }

        /// <summary>
        /// Process all elements in an enumerable.
        /// Elements are processed in parallel (async).
        /// </summary>
        /// <remarks>
        /// If the <paramref name="enumerable"/> is a <see cref="IReadOnlyList{T}"/> (arrays, List etc) it's used directly, else the elements are first copied to a list.
        /// </remarks>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="enumerable">The enumerable to process, if null nothing is done</param>
        /// <param name="action">The action to perform on each element, the second argument is the index of the element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A task that completes when all elements are processed, if any action fails, the task is faulted (after all actions have completed)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="action"/> is null and the <paramref name="enumerable"/> is non-empty</exception>
        public static Task ProcessAsync<T>(this IEnumerable<T> enumerable, Func<T, int, Task> action, int maxConcurrency = 0)
            => ProcessAsync(AsList(enumerable), action, maxConcurrency);

        /// <summary>
        /// Process all elements in a list.
        /// Elements are processed in parallel (async).
        /// </summary>
        /// <remarks>
        /// Same as <see cref="ProcessAsync{T}(IReadOnlyList{T}, Func{T, int, Task}, int)"/>.
        /// </remarks>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="list">The list to process, if null nothing is done</param>
        /// <param name="action">The action to perform on each element, the second argument is the index of the element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A task that completes when all elements are processed, if any action fails, the task is faulted (after all actions have completed)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="action"/> is null and the <paramref name="list"/> is non-empty</exception>
        public static Task ProcessAsyncValue<T>(this IReadOnlyList<T> list, Func<T, int, Task> action, int maxConcurrency = 0)
        {
            if (list == null)
                return Task.CompletedTask;
            var l = list.Count;
            if (l <= 0)
                return Task.CompletedTask;
            if (l == 1)
                return action(list[0], 0);
            ConcurrencyLimiter.LimitConcurrency(ref action, maxConcurrency, l);
            var tt = GC.AllocateUninitializedArray<Task>(l);
            for (int i = 0; i < l; ++i)
                tt[i] = action(list[i], i);
            return Task.WhenAll(tt);
        }

        /// <summary>
        /// Process all elements in an enumerable.
        /// Elements are processed in parallel (async).
        /// </summary>
        /// <remarks>
        /// If the <paramref name="enumerable"/> is a <see cref="IReadOnlyList{T}"/> (arrays, List etc) it's used directly, else the elements are first copied to a list.
        /// </remarks>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="enumerable">The enumerable to process, if null nothing is done</param>
        /// <param name="action">The action to perform on each element, the second argument is the index of the element</param>
        /// <param name="maxConcurrency">The maximum number of concurrent operations.
        /// If less than zero, it's a percentage of the number of available logical processors.
        /// If zero the concurrency is the number of processors minus one.
        /// Ex:
        /// -50 = 50% of the number of processors (so 4 if there are 8 processors).
        /// -200 = 200% of the number of processors (so 16 if there are 8 processors).
        /// 0 = Number of processors minus one (so 7 if there are 8 processors).
        /// </param>
        /// <returns>A task that completes when all elements are processed, if any action fails, the task is faulted (after all actions have completed)</returns>
        /// <exception cref="NullReferenceException">Thrown if <paramref name="action"/> is null and the <paramref name="enumerable"/> is non-empty</exception>
        public static Task ProcessAsyncValue<T>(this IEnumerable<T> enumerable, Func<T, int, Task> action, int maxConcurrency = 0)
            => ProcessAsyncValue(AsList(enumerable), action, maxConcurrency);




        /// <summary>
        /// Only return unique keys, the first key-value pair with a given key is returned, later pairs with the same key are skipped.
        /// The order of the pairs is preserved.
        /// </summary>
        /// <remarks>
        /// The enumeration is lazy (deferred), argument validation and enumeration of the source happens when the result is enumerated.
        /// </remarks>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="enumerable">The key-value pairs</param>
        /// <param name="comparer">An optional key comparer, if null the default comparer is used</param>
        /// <returns>The key-value pairs with unique keys</returns>
        /// <exception cref="NullReferenceException">Thrown (when enumerated) if <paramref name="enumerable"/> is null</exception>
        public static IEnumerable<KeyValuePair<K, V>> WithUniqueKeys<K, V>(this IEnumerable<KeyValuePair<K, V>> enumerable, IEqualityComparer<K> comparer = default)
        {
            var seen = new HashSet<K>(comparer);
            foreach (var x in enumerable)
                if (seen.Add(x.Key))
                    yield return x;
        }

    }
}
