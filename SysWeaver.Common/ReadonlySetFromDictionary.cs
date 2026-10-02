using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


namespace SysWeaver
{

    /// <summary>
    /// Dictionary extensions
    /// </summary>
    public static class DictionaryExt
    {

        /// <summary>
        /// The keys of a dictionary as a read only set
        /// </summary>
        sealed class KeySet<K, V> : IReadOnlySet<K>
        {
            public KeySet(IReadOnlyDictionary<K, V> d)
            {
                D = d;
            }

            readonly IReadOnlyDictionary<K, V> D;

            public int Count => D.Count;

            public bool Contains(K item) => D.ContainsKey(item);

            /// <summary>
            /// The keys as a set (using the comparer of the dictionary, the default comparer if it's unknown)
            /// </summary>
            HashSet<K> ToSet() => new(D.Keys, TryGetComparer(D) ?? EqualityComparer<K>.Default);

            public bool IsProperSubsetOf(IEnumerable<K> other) => ToSet().IsProperSubsetOf(other);

            public bool IsProperSupersetOf(IEnumerable<K> other) => ToSet().IsProperSupersetOf(other);

            public bool IsSubsetOf(IEnumerable<K> other) => ToSet().IsSubsetOf(other);

            public bool IsSupersetOf(IEnumerable<K> other)
            {
                // All the other items must be keys (no allocation)
                foreach (var x in other)
                    if (!D.ContainsKey(x))
                        return false;
                return true;
            }

            public bool Overlaps(IEnumerable<K> other)
            {
                // Any of the other items is a key (no allocation)
                if (D.Count == 0)
                    return false;
                foreach (var x in other)
                    if (D.ContainsKey(x))
                        return true;
                return false;
            }

            public bool SetEquals(IEnumerable<K> other) => ToSet().SetEquals(other);

            public IEnumerator<K> GetEnumerator() => D.Keys.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => D.Keys.GetEnumerator();

        }

        /// <summary>
        /// Treats the keys of the dictionary as as read only set, changes to the underlaying dictionary is proagated
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="dictionary">The dictionary to treat as a read only set</param>
        /// <returns></returns>
        public static IReadOnlySet<K> KeysAsReadOnlySet<K, V>(this IReadOnlyDictionary<K, V> dictionary) => new KeySet<K, V>(dictionary);


        /// <summary>
        /// Aggregates the values on a dictionary with the data from another dictionary.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="dictionary">The dictionary to modify</param>
        /// <param name="with">The dictionary to aggregate into the dictionary</param>
        /// <param name="func">The aggregation function</param>
        /// <returns>The dictionary, same object, useful for chaining</returns>
        public static IDictionary<K, V> Aggregate<K, V>(this IDictionary<K, V> dictionary, IReadOnlyDictionary<K, V> with, Func<V, V, V> func)
        {
            if (dictionary is Dictionary<K, V> d)
            {
                // One lookup per key
                foreach (var v in with)
                {
                    ref var e = ref CollectionsMarshal.GetValueRefOrAddDefault(d, v.Key, out var exists);
                    e = exists ? func(e, v.Value) : v.Value;
                }
                return dictionary;
            }
            foreach (var v in with)
            {
                var key = v.Key;
                if (dictionary.TryGetValue(key, out var e))
                {
                    dictionary[key] = func(e, v.Value);
                }else
                {
                    dictionary[key] = v.Value;
                }
            }
            return dictionary;
        }

        /// <summary>
        /// Add values from another dictionary into a dictionary
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="dictionary">The dictionary to modify</param>
        /// <param name="with">The dictionary to add into the dictionary</param>
        /// <returns>The dictionary, same object, useful for chaining</returns>
        public static IDictionary<K, V> Add<K, V>(this IDictionary<K, V> dictionary, IReadOnlyDictionary<K, V> with) where V : IAdditionOperators<V, V, V> =>
            Aggregate<K, V>(dictionary, with, (a, b) => a + b);

        /// <summary>
        /// Take the maximum value from another dictionary into a dictionary
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="dictionary">The dictionary to modify</param>
        /// <param name="with">The dictionary to max into the dictionary</param>
        /// <returns>The dictionary, same object, useful for chaining</returns>
        public static IDictionary<K, V> Min<K, V>(this IDictionary<K, V> dictionary, IReadOnlyDictionary<K, V> with) where V : IComparisonOperators<V, V, bool> =>
            Aggregate<K, V>(dictionary, with, (a, b) => a < b ? a : b);

        /// <summary>
        /// Take the maximum value from another dictionary into a dictionary
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="dictionary">The dictionary to modify</param>
        /// <param name="with">The dictionary to max into the dictionary</param>
        /// <returns>The dictionary, same object, useful for chaining</returns>
        public static IDictionary<K, V> Max<K, V>(this IDictionary<K, V> dictionary, IReadOnlyDictionary<K, V> with) where V : IComparisonOperators<V, V, bool> =>
            Aggregate<K, V>(dictionary, with, (a, b) => a > b ? a : b);


        /// <summary>
        /// The number of items in a collection (if it's known without enumerating it), else 0
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Capacity<T>(IEnumerable<T> vals)
            => (vals != null) && vals.TryGetNonEnumeratedCount(out var c) ? c : 0;

        /// <summary>
        /// Create a dictionary from a collection, if the same key is present more than once, the last is used
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="vals">Values</param>
        /// <param name="k">Optional equality comparer</param>
        /// <returns></returns>
        public static Dictionary<K, V> Create<K, V>(IEnumerable<KeyValuePair<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = new Dictionary<K, V>(Capacity(vals), k);
            foreach (var x in vals.Nullable())
                d[x.Key] = x.Value;
            return d;
        }

        /// <summary>
        /// Create a dictionary from a collection, if the same key is present more than once, the last is used
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="vals">Values</param>
        /// <param name="k">Optional equality comparer</param>
        /// <returns></returns>
        public static Dictionary<K, V> Create<K, V>(IEnumerable<Tuple<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = new Dictionary<K, V>(Capacity(vals), k);
            foreach (var x in vals.Nullable())
                d[x.Item1] = x.Item2;
            return d;
        }

        /// <summary>
        /// Create a dictionary from a collection, if the same key is present more than once, the last is used
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="vals">Values</param>
        /// <param name="k">Optional equality comparer</param>
        /// <returns></returns>
        public static Dictionary<K, V> Create<K, V>(IEnumerable<ValueTuple<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = new Dictionary<K, V>(Capacity(vals), k);
            foreach (var x in vals.Nullable())
                d[x.Item1] = x.Item2;
            return d;
        }




        /// <summary>
        /// Create a dictionary from a collection, if the same key is present more than once, the last is used
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="vals">Values</param>
        /// <param name="k">Optional equality comparer</param>
        /// <returns></returns>
        public static ConcurrentDictionary<K, V> CreateConcurrent<K, V>(IEnumerable<KeyValuePair<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = new ConcurrentDictionary<K, V>(Environment.ProcessorCount, Math.Max(Capacity(vals), 31), k);
            foreach (var x in vals.Nullable())
                d[x.Key] = x.Value;
            return d;
        }

        /// <summary>
        /// Create a dictionary from a collection, if the same key is present more than once, the last is used
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="vals">Values</param>
        /// <param name="k">Optional equality comparer</param>
        /// <returns></returns>
        public static ConcurrentDictionary<K, V> CreateConcurrent<K, V>(IEnumerable<Tuple<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = new ConcurrentDictionary<K, V>(Environment.ProcessorCount, Math.Max(Capacity(vals), 31), k);
            foreach (var x in vals.Nullable())
                d[x.Item1] = x.Item2;
            return d;
        }

        /// <summary>
        /// Create a dictionary from a collection, if the same key is present more than once, the last is used
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="vals">Values</param>
        /// <param name="k">Optional equality comparer</param>
        /// <returns></returns>
        public static ConcurrentDictionary<K, V> CreateConcurrent<K, V>(IEnumerable<ValueTuple<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = new ConcurrentDictionary<K, V>(Environment.ProcessorCount, Math.Max(Capacity(vals), 31), k);
            foreach (var x in vals.Nullable())
                d[x.Item1] = x.Item2;
            return d;
        }


        /// <summary>
        /// Try to remove an element from a dictionary
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="d"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRemove<K, V>(this Dictionary<K, V> d, K key, out V value)
            => d.Remove(key, out value);

        /// <summary>
        /// Create a frozen version of a dictionary
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="d"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyDictionary<K, V> Freeze<K, V>(this Dictionary<K, V> d)
            => Freeze<K, V>(d, d?.Comparer);


        /// <summary>
        /// Create a frozen version of a dictionary
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="d"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyDictionary<K, V> Freeze<K, V>(this IReadOnlyDictionary<K, V> d)
            => Freeze<K, V>(d, d?.GetComparer());

        /// <summary>
        /// Create a frozen version of a dictionary
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="d"></param>
        /// <param name="comparer">The comparer to use (also kept for an empty dictionary)</param>
        /// <returns></returns>
        public static IReadOnlyDictionary<K, V> Freeze<K, V>(this IReadOnlyDictionary<K, V> d, IEqualityComparer<K> comparer)
        {
            if (d == null)
                return null;
            if (comparer == null)
                throw new Exception("Must specify a comparer!");
            var l = d.Count;
            if (l <= 0)
                return EmptyReadonlyDictionary<K, V>.Get(comparer);
            if (l == 1)
            {
                if ((d as SingleReadonlyDictionary<K, V>)?.Comp == comparer)
                    return d;
                var f = d.First();
                return new SingleReadonlyDictionary<K, V>(f.Key, f.Value, comparer);
            }
            if ((d as FrozenDictionary<K, V>)?.Comparer == comparer)
                return d;
            return d.ToFrozenDictionary(comparer);
        }

        /// <summary>
        /// Create an optimized dictionary from a single entry
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="comp"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyDictionary<K, V> Single<K, V>(K key, V value, IEqualityComparer<K> comp = null)
            => new SingleReadonlyDictionary<K, V>(key, value, comp ?? EqualityComparer<K>.Default);


        /// <summary>
        /// Get the comparer of a dictionary (FrozenDictionary, Dictionary, ConcurrentDictionary and the dictionaries returned by Freeze / Single)
        /// </summary>
        /// <exception cref="Exception">If the comparer is unknown</exception>
        public static IEqualityComparer<K> GetComparer<K, V>(this IReadOnlyDictionary<K, V> dict)
            => TryGetComparer(dict) ?? throw new Exception("No comparer could be found!");

        /// <summary>
        /// Get the comparer of a dictionary, or null if it's unknown
        /// </summary>
        static IEqualityComparer<K> TryGetComparer<K, V>(IReadOnlyDictionary<K, V> dict)
            => dict switch
            {
                FrozenDictionary<K, V> a => a.Comparer,
                IHaveComparere<K> b => b.Comp,
                Dictionary<K, V> c => c.Comparer,
                ConcurrentDictionary<K, V> d => d.Comparer,
                _ => null,
            };
    }


    public static class ReadOnlyDictionary<K, V>
    {
        public static readonly IReadOnlyDictionary<K, V> Empty = EmptyReadonlyDictionary<K, V>.Default;
    }



    interface IHaveComparere<K>
    {
        IEqualityComparer<K> Comp { get; }
    }

    sealed class EmptyReadonlyDictionary<K, V> : IReadOnlyDictionary<K, V>, IHaveComparere<K>
    {

        public static readonly EmptyReadonlyDictionary<K, V> Default = new (EqualityComparer<K>.Default);

        /// <summary>
        /// The empty dictionaries of other comparers
        /// </summary>
        static readonly ConditionalWeakTable<IEqualityComparer<K>, EmptyReadonlyDictionary<K, V>> Others = new();

        /// <summary>
        /// Get an empty dictionary with a comparer
        /// </summary>
        public static EmptyReadonlyDictionary<K, V> Get(IEqualityComparer<K> comparer)
        {
            if (comparer == Default.Comp)
                return Default;
            // The last used comparer is cached (it's typically the same comparer every time)
            var last = Last;
            if (last?.Comp == comparer)
                return last;
            last = Others.GetValue(comparer, c => new EmptyReadonlyDictionary<K, V>(c));
            Last = last;
            return last;
        }

        /// <summary>
        /// The last empty dictionary returned for another comparer than the default
        /// </summary>
        static EmptyReadonlyDictionary<K, V> Last;

        EmptyReadonlyDictionary(IEqualityComparer<K> comparer)
        {
            Comp = comparer;
        }

        public IEqualityComparer<K> Comp { get; init; }

        public V this[K key] => throw new KeyNotFoundException();

        // Expression bodied (not initialized from static fields, they are not initialized yet when Default is created)
        public IEnumerable<K> Keys => Array.Empty<K>();

        public IEnumerable<V> Values => Array.Empty<V>();

        public int Count => 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(K key) => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => ((IEnumerable<KeyValuePair<K, V>>)Array.Empty<KeyValuePair<K, V>>()).GetEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
        {
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    }


    sealed class SingleReadonlyDictionary<K, V> : IReadOnlyDictionary<K, V>, IHaveComparere<K>
    {
        public SingleReadonlyDictionary(K key, V value, IEqualityComparer<K> comp)
        {
            Key = key;
            Value = value;
            Comp = comp;
            IsDefault = typeof(K).IsValueType && (comp == EqualityComparer<K>.Default);
            Keys = [key];
            Values = [value];
            KVe = [new KeyValuePair<K, V>(key, value)];
        }

        readonly K Key;
        readonly V Value;
        readonly IEnumerable<KeyValuePair<K, V>> KVe;

        /// <summary>
        /// True if the key is a value type and the comparer is the default comparer (devirtualized)
        /// </summary>
        readonly bool IsDefault;

        public IEqualityComparer<K> Comp { get; init; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsKey(K key)
            => IsDefault ? EqualityComparer<K>.Default.Equals(key, Key) : Comp.Equals(key, Key);

        public V this[K key]
        {
            get
            {
                if (IsKey(key))
                    return Value;
                throw new KeyNotFoundException();
            }

        }

        public IEnumerable<K> Keys { get; init; }

        public IEnumerable<V> Values { get; init; }

        public int Count => 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(K key)
            => IsKey(key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => KVe.GetEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
        {
            if (IsKey(key))
            {
                value = Value;
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }



}
