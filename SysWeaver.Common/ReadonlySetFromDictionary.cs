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
    /// Dictionary extensions: creation, aggregation, freezing (creating optimized immutable dictionaries) and getting the comparer of a dictionary.
    /// </summary>
    /// <remarks>
    /// <see cref="Freeze{K, V}(IReadOnlyDictionary{K, V}, IEqualityComparer{K})"/> picks the fastest implementation for the content:
    /// a shared empty dictionary, a single entry dictionary, a vectorized dictionary for 2-8 integer / enum keys, an open addressing table for more integer / enum keys,
    /// a fingerprint based dictionary for ordinal string keys, else a <see cref="FrozenDictionary{TKey, TValue}"/>.
    /// The dictionaries returned by Freeze remember their comparer, so freezing them again (with the same comparer) returns the same instance.
    /// </remarks>
    public static class DictionaryExt
    {

        /// <summary>
        /// The keys of a dictionary as a read only set (a live view, no copy is made).
        /// Contains, IsSupersetOf and Overlaps don't allocate, the other set operations copy the keys to a <see cref="HashSet{T}"/>.
        /// </summary>
        sealed class KeySet<K, V> : IReadOnlySet<K>
        {
            public KeySet(IReadOnlyDictionary<K, V> d)
            {
                D = d;
                Dict = d as Dictionary<K, V>;
            }

            readonly IReadOnlyDictionary<K, V> D;

            /// <summary>
            /// The dictionary if it's a Dictionary (called directly, no interface call), else null
            /// </summary>
            readonly Dictionary<K, V> Dict;

            public int Count => D.Count;

            public bool Contains(K item)
            {
                var d = Dict;
                return d != null ? d.ContainsKey(item) : D.ContainsKey(item);
            }

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
        /// Treats the keys of the dictionary as a read only set, changes to the underlying dictionary are visible through the set (it's a view, not a copy).
        /// </summary>
        /// <remarks>
        /// Thread safety is the same as for the dictionary (don't modify a non-concurrent dictionary while the set is used).
        /// Lookups use the comparer of the dictionary.
        /// </remarks>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="dictionary">The dictionary to treat as a read only set, must not be null</param>
        /// <returns>A read only set view of the keys</returns>
        public static IReadOnlySet<K> KeysAsReadOnlySet<K, V>(this IReadOnlyDictionary<K, V> dictionary) => new KeySet<K, V>(dictionary);


        /// <summary>
        /// Aggregates the values of another dictionary into a dictionary (in-place).
        /// For keys that exist in both, the value is set to func(existing, other), keys that only exist in <paramref name="with"/> are added with their value.
        /// </summary>
        /// <remarks>
        /// Not thread safe (even for a concurrent dictionary the read-modify-write of a key isn't atomic).
        /// </remarks>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="dictionary">The dictionary to modify, must not be null</param>
        /// <param name="with">The dictionary to aggregate into the dictionary, must not be null</param>
        /// <param name="func">The aggregation function, the first argument is the existing value and the second is the value from <paramref name="with"/></param>
        /// <returns>The dictionary, same object, useful for chaining</returns>
        public static IDictionary<K, V> Aggregate<K, V>(this IDictionary<K, V> dictionary, IReadOnlyDictionary<K, V> with, Func<V, V, V> func)
        {
            if (dictionary is Dictionary<K, V> d)
            {
                // One lookup per key, a Dictionary is enumerated without boxing the enumerator
                if (with is Dictionary<K, V> wd)
                {
                    foreach (var v in wd)
                    {
                        ref var e = ref CollectionsMarshal.GetValueRefOrAddDefault(d, v.Key, out var exists);
                        e = exists ? func(e, v.Value) : v.Value;
                    }
                    return dictionary;
                }
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
        /// Add (sum) the values of another dictionary into a dictionary (in-place), see <see cref="Aggregate{K, V}(IDictionary{K, V}, IReadOnlyDictionary{K, V}, Func{V, V, V})"/>.
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="dictionary">The dictionary to modify, must not be null</param>
        /// <param name="with">The dictionary to add into the dictionary, must not be null</param>
        /// <returns>The dictionary, same object, useful for chaining</returns>
        public static IDictionary<K, V> Add<K, V>(this IDictionary<K, V> dictionary, IReadOnlyDictionary<K, V> with) where V : IAdditionOperators<V, V, V> =>
            Aggregate<K, V>(dictionary, with, (a, b) => a + b);

        /// <summary>
        /// Take the minimum value of each key from another dictionary into a dictionary (in-place), see <see cref="Aggregate{K, V}(IDictionary{K, V}, IReadOnlyDictionary{K, V}, Func{V, V, V})"/>.
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="dictionary">The dictionary to modify, must not be null</param>
        /// <param name="with">The dictionary to min into the dictionary, must not be null</param>
        /// <returns>The dictionary, same object, useful for chaining</returns>
        public static IDictionary<K, V> Min<K, V>(this IDictionary<K, V> dictionary, IReadOnlyDictionary<K, V> with) where V : IComparisonOperators<V, V, bool> =>
            Aggregate<K, V>(dictionary, with, (a, b) => a < b ? a : b);

        /// <summary>
        /// Take the maximum value of each key from another dictionary into a dictionary (in-place), see <see cref="Aggregate{K, V}(IDictionary{K, V}, IReadOnlyDictionary{K, V}, Func{V, V, V})"/>.
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="dictionary">The dictionary to modify, must not be null</param>
        /// <param name="with">The dictionary to max into the dictionary, must not be null</param>
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
        /// Create a dictionary from a collection of key-value pairs, if the same key is present more than once, the last value is used (doesn't throw).
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="vals">The key-value pairs (null is treated as empty)</param>
        /// <param name="k">Optional key comparer, if null the default comparer is used</param>
        /// <returns>A new dictionary</returns>
        /// <exception cref="ArgumentNullException">Thrown if a key is null</exception>
        public static Dictionary<K, V> Create<K, V>(IEnumerable<KeyValuePair<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = new Dictionary<K, V>(Capacity(vals), k);
            foreach (var x in vals.Nullable())
                d[x.Key] = x.Value;
            return d;
        }

        /// <summary>
        /// Create a dictionary from a collection of key-value pairs, if the same key is present more than once, the last value is used (doesn't throw).
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="vals">The key-value pairs (null is treated as empty)</param>
        /// <param name="k">Optional key comparer, if null the default comparer is used</param>
        /// <returns>A new dictionary</returns>
        /// <exception cref="ArgumentNullException">Thrown if a key is null</exception>
        public static Dictionary<K, V> Create<K, V>(IEnumerable<Tuple<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = new Dictionary<K, V>(Capacity(vals), k);
            foreach (var x in vals.Nullable())
                d[x.Item1] = x.Item2;
            return d;
        }

        /// <summary>
        /// Create a dictionary from a collection of key-value pairs, if the same key is present more than once, the last value is used (doesn't throw).
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="vals">The key-value pairs (null is treated as empty)</param>
        /// <param name="k">Optional key comparer, if null the default comparer is used</param>
        /// <returns>A new dictionary</returns>
        /// <exception cref="ArgumentNullException">Thrown if a key is null</exception>
        public static Dictionary<K, V> Create<K, V>(IEnumerable<ValueTuple<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = new Dictionary<K, V>(Capacity(vals), k);
            foreach (var x in vals.Nullable())
                d[x.Item1] = x.Item2;
            return d;
        }




        /// <summary>
        /// Create a concurrent dictionary from a collection of key-value pairs, if the same key is present more than once, the last value is used (doesn't throw).
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="vals">The key-value pairs (null is treated as empty)</param>
        /// <param name="k">Optional key comparer, if null the default comparer is used</param>
        /// <returns>A new concurrent dictionary (the concurrency level is the number of processors)</returns>
        /// <exception cref="ArgumentNullException">Thrown if a key is null</exception>
        public static ConcurrentDictionary<K, V> CreateConcurrent<K, V>(IEnumerable<KeyValuePair<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = new ConcurrentDictionary<K, V>(Environment.ProcessorCount, Math.Max(Capacity(vals), 31), k);
            foreach (var x in vals.Nullable())
                d[x.Key] = x.Value;
            return d;
        }

        /// <summary>
        /// Create a concurrent dictionary from a collection of key-value pairs, if the same key is present more than once, the last value is used (doesn't throw).
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="vals">The key-value pairs (null is treated as empty)</param>
        /// <param name="k">Optional key comparer, if null the default comparer is used</param>
        /// <returns>A new concurrent dictionary (the concurrency level is the number of processors)</returns>
        /// <exception cref="ArgumentNullException">Thrown if a key is null</exception>
        public static ConcurrentDictionary<K, V> CreateConcurrent<K, V>(IEnumerable<Tuple<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = new ConcurrentDictionary<K, V>(Environment.ProcessorCount, Math.Max(Capacity(vals), 31), k);
            foreach (var x in vals.Nullable())
                d[x.Item1] = x.Item2;
            return d;
        }

        /// <summary>
        /// Create a concurrent dictionary from a collection of key-value pairs, if the same key is present more than once, the last value is used (doesn't throw).
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="vals">The key-value pairs (null is treated as empty)</param>
        /// <param name="k">Optional key comparer, if null the default comparer is used</param>
        /// <returns>A new concurrent dictionary (the concurrency level is the number of processors)</returns>
        /// <exception cref="ArgumentNullException">Thrown if a key is null</exception>
        public static ConcurrentDictionary<K, V> CreateConcurrent<K, V>(IEnumerable<ValueTuple<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = new ConcurrentDictionary<K, V>(Environment.ProcessorCount, Math.Max(Capacity(vals), 31), k);
            foreach (var x in vals.Nullable())
                d[x.Item1] = x.Item2;
            return d;
        }


        /// <summary>
        /// Try to remove an element from a dictionary (same as <see cref="Dictionary{TKey, TValue}.Remove(TKey, out TValue)"/>, named like <see cref="ConcurrentDictionary{TKey, TValue}.TryRemove(TKey, out TValue)"/> so that the same code works for both).
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="d">The dictionary, must not be null</param>
        /// <param name="key">The key to remove, must not be null</param>
        /// <param name="value">The removed value, or default if the key wasn't found</param>
        /// <returns>True if the key was found and removed</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRemove<K, V>(this Dictionary<K, V> d, K key, out V value)
            => d.Remove(key, out value);

        /// <summary>
        /// Create a frozen (immutable, lookup optimized) copy of a dictionary, using the comparer of the dictionary.
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="d">The dictionary to freeze (may be null)</param>
        /// <returns>An immutable dictionary with the same entries and comparer, or null if <paramref name="d"/> is null.
        /// See <see cref="Freeze{K, V}(IReadOnlyDictionary{K, V}, IEqualityComparer{K})"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyDictionary<K, V> Freeze<K, V>(this Dictionary<K, V> d)
            => Freeze<K, V>(d, d?.Comparer);


        /// <summary>
        /// Create a frozen (immutable, lookup optimized) version of a dictionary, using the comparer of the dictionary.
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="d">The dictionary to freeze (may be null)</param>
        /// <returns>An immutable dictionary with the same entries and comparer, or null if <paramref name="d"/> is null.
        /// If <paramref name="d"/> is already frozen it's returned as is.
        /// See <see cref="Freeze{K, V}(IReadOnlyDictionary{K, V}, IEqualityComparer{K})"/>.</returns>
        /// <exception cref="Exception">Thrown if the comparer of <paramref name="d"/> can't be determined (see <see cref="GetComparer{K, V}(IReadOnlyDictionary{K, V})"/>)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyDictionary<K, V> Freeze<K, V>(this IReadOnlyDictionary<K, V> d)
            => Freeze<K, V>(d, d?.GetComparer());

        /// <summary>
        /// Create a frozen (immutable, lookup optimized) version of a dictionary, using a specific comparer.
        /// </summary>
        /// <remarks>
        /// The implementation is chosen from the content and the comparer:
        /// a shared (cached per comparer) empty dictionary, a single entry dictionary, a vectorized dictionary for 2-8 keys of a 4 or 8 byte integer / enum type (default comparer),
        /// an open addressing table for more such keys, a fingerprint based dictionary for ordinal string keys (<see cref="StringComparer.Ordinal"/> or the default string comparer),
        /// else a <see cref="FrozenDictionary{TKey, TValue}"/>.
        /// The returned dictionaries are thread safe for reads.
        /// Freezing allocates a copy, it's intended for data that is created once and read many times (see also <see cref="SemiFrozenDictionary{TKey, TValue}"/>).
        /// A null key throws <see cref="ArgumentNullException"/> on lookups (the indexer, ContainsKey and TryGetValue) for all implementations (like a <see cref="Dictionary{TKey, TValue}"/>), including the empty and single entry dictionaries.
        /// </remarks>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="d">The dictionary to freeze (may be null)</param>
        /// <param name="comparer">The comparer to use (also kept for an empty dictionary), must not be null</param>
        /// <returns>An immutable dictionary with the entries of <paramref name="d"/> using <paramref name="comparer"/>, or null if <paramref name="d"/> is null.
        /// If <paramref name="d"/> is already a frozen dictionary with the same comparer, it's returned as is.</returns>
        /// <exception cref="Exception">Thrown if <paramref name="comparer"/> is null (and <paramref name="d"/> is non-null)</exception>
        public static IReadOnlyDictionary<K, V> Freeze<K, V>(this IReadOnlyDictionary<K, V> d, IEqualityComparer<K> comparer)
        {
            if (d == null)
                return null;
            if (comparer == null)
                throw new Exception("Must specify a comparer!");
            // Already frozen with the same comparer
            if ((d is IHaveComparere<K> f) && (f.Comp == comparer))
                return d;
            var l = d.Count;
            if (l <= 0)
                return EmptyReadonlyDictionary<K, V>.Get(comparer);
            if (l == 1)
            {
                var e = FrozenCopy.First(d);
                return Single(e.Key, e.Value, comparer);
            }
            // A few integer keys: faster than a FrozenDictionary (a linear search) and a Dictionary
            if (SmallValueKeys<K>.CanUse(l, comparer))
                return new SmallValueKeyReadonlyDictionary<K, V>(d);
            if ((d as FrozenDictionary<K, V>)?.Comparer == comparer)
                return d;
            // More integer keys: an open addressing table, faster than a FrozenDictionary
            if (ValueKeyTable<K>.CanUse(l, comparer))
                return new ValueKeyTableReadonlyDictionary<K, V>(d);
            // String keys (ordinal): a lookup on a fingerprint of the keys (a few chars where the keys differ), faster than a FrozenDictionary
            if (OrdinalStringKeys.IsOrdinal(comparer))
            {
                var s = OrdinalStringKeys.TryCreateDictionary((IReadOnlyDictionary<string, V>)(object)d, (IEqualityComparer<string>)(object)comparer);
                if (s != null)
                    return (IReadOnlyDictionary<K, V>)(object)s;
            }
            return d.ToFrozenDictionary(comparer);
        }

        /// <summary>
        /// Create an immutable, lookup optimized dictionary with a single entry (a single allocation).
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="key">The key, should not be null (lookups with a null key throw an <see cref="ArgumentNullException"/>, so an entry with a null key can't be found)</param>
        /// <param name="value">The value</param>
        /// <param name="comp">The key comparer, if null the default comparer is used</param>
        /// <returns>A frozen dictionary with one entry (freezing it again with the same comparer returns the same instance).
        /// Lookups with a null key throw an <see cref="ArgumentNullException"/> (like a <see cref="Dictionary{TKey, TValue}"/>, the comparer is never called with a null key).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyDictionary<K, V> Single<K, V>(K key, V value, IEqualityComparer<K> comp = null)
        {
            comp ??= EqualityComparer<K>.Default;
            return KeyEquality<K>.IsDefault(comp)
                ? new SingleReadonlyDictionary<K, V, DefaultKeyEquality<K>>(key, value, comp)
                : new SingleReadonlyDictionary<K, V, ComparerKeyEquality<K>>(key, value, comp);
        }


        /// <summary>
        /// Get the comparer of a dictionary (<see cref="FrozenDictionary{TKey, TValue}"/>, <see cref="Dictionary{TKey, TValue}"/>, <see cref="ConcurrentDictionary{TKey, TValue}"/> and the dictionaries returned by Freeze / Single).
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="dict">The dictionary</param>
        /// <returns>The key comparer of the dictionary</returns>
        /// <exception cref="Exception">Thrown if the comparer is unknown (any other dictionary implementation, like a SortedDictionary or a ReadOnlyDictionary wrapper) or if <paramref name="dict"/> is null</exception>
        public static IEqualityComparer<K> GetComparer<K, V>(this IReadOnlyDictionary<K, V> dict)
            => TryGetComparer(dict) ?? throw new Exception("No comparer could be found!");

        /// <summary>
        /// Get the comparer of a dictionary, or null if it's unknown
        /// </summary>
        internal static IEqualityComparer<K> TryGetComparer<K, V>(IReadOnlyDictionary<K, V> dict)
            => dict switch
            {
                FrozenDictionary<K, V> a => a.Comparer,
                IHaveComparere<K> b => b.Comp,
                Dictionary<K, V> c => c.Comparer,
                ConcurrentDictionary<K, V> d => d.Comparer,
                _ => null,
            };
    }


    /// <summary>
    /// Shared read only dictionary instances.
    /// </summary>
    /// <remarks>
    /// Has the same name as System.Collections.ObjectModel.ReadOnlyDictionary, qualify the name if both namespaces are imported.
    /// </remarks>
    /// <typeparam name="K">The key type</typeparam>
    /// <typeparam name="V">The value type</typeparam>
    public static class ReadOnlyDictionary<K, V>
    {
        /// <summary>
        /// A shared, immutable, empty dictionary using <see cref="EqualityComparer{T}.Default"/> (the same instance as <see cref="ReadOnlyData.EmptyDictionary{K, V}"/>).
        /// </summary>
        public static readonly IReadOnlyDictionary<K, V> Empty = EmptyReadonlyDictionary<K, V>.Default;
    }



    /// <summary>
    /// Implemented by the frozen sets and dictionaries created by this library, exposes the comparer (so that they can be re-frozen without a copy).
    /// </summary>
    /// <typeparam name="K">The key type</typeparam>
    interface IHaveComparere<K>
    {
        /// <summary>
        /// The key comparer
        /// </summary>
        IEqualityComparer<K> Comp { get; }
    }

    /// <summary>
    /// An immutable empty dictionary that remembers its comparer, one shared instance per comparer (returned by <see cref="DictionaryExt.Freeze{K, V}(IReadOnlyDictionary{K, V}, IEqualityComparer{K})"/> for an empty dictionary).
    /// </summary>
    /// <remarks>
    /// Lookups with a null key throw an <see cref="ArgumentNullException"/> (like a <see cref="Dictionary{TKey, TValue}"/> and the other frozen dictionaries).
    /// </remarks>
    /// <typeparam name="K">The key type</typeparam>
    /// <typeparam name="V">The value type</typeparam>
    sealed class EmptyReadonlyDictionary<K, V> : IReadOnlyDictionary<K, V>, IHaveComparere<K>
    {

        /// <summary>
        /// The empty dictionary using <see cref="EqualityComparer{T}.Default"/>
        /// </summary>
        public static readonly EmptyReadonlyDictionary<K, V> Default = new (EqualityComparer<K>.Default);

        /// <summary>
        /// The empty dictionaries of other comparers
        /// </summary>
        static readonly ConditionalWeakTable<IEqualityComparer<K>, EmptyReadonlyDictionary<K, V>> Others = new();

        /// <summary>
        /// Get the shared empty dictionary of a comparer (the instances of other comparers than the default are kept in a weak table, so they don't keep the comparer alive).
        /// Thread safe.
        /// </summary>
        /// <param name="comparer">The comparer, must not be null</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EmptyReadonlyDictionary<K, V> Get(IEqualityComparer<K> comparer)
        {
            // The last used comparer is cached (it's typically the same comparer every time)
            var last = Last;
            return last.Comp == comparer ? last : GetSlow(comparer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static EmptyReadonlyDictionary<K, V> GetSlow(IEqualityComparer<K> comparer)
        {
            var last = comparer == Default.Comp ? Default : Others.GetValue(comparer, c => new EmptyReadonlyDictionary<K, V>(c));
            Last = last;
            return last;
        }

        /// <summary>
        /// The last empty dictionary returned (starts with the default comparer)
        /// </summary>
        static EmptyReadonlyDictionary<K, V> Last = Default;

        EmptyReadonlyDictionary(IEqualityComparer<K> comparer)
        {
            Comp = comparer;
        }

        public IEqualityComparer<K> Comp { get; init; }

        public V this[K key]
        {
            get
            {
                if (key is null)
                    OrdinalStringKeys.ThrowNullKey();
                throw new KeyNotFoundException();
            }
        }

        // Expression bodied (not initialized from static fields, they are not initialized yet when Default is created)
        public IEnumerable<K> Keys => Array.Empty<K>();

        public IEnumerable<V> Values => Array.Empty<V>();

        public int Count => 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(K key)
        {
            if (key is null)
                OrdinalStringKeys.ThrowNullKey();
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => ((IEnumerable<KeyValuePair<K, V>>)Array.Empty<KeyValuePair<K, V>>()).GetEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
        {
            if (key is null)
                OrdinalStringKeys.ThrowNullKey();
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    }


    /// <summary>
    /// How the key of a single item container is compared (a struct, so that the code is specialized and the compare is devirtualized)
    /// </summary>
    interface IKeyEquality<K>
    {
        static abstract bool Equals(IEqualityComparer<K> comparer, K a, K b);
    }

    /// <summary>
    /// The default comparer of a value type (devirtualized and inlined by the JIT)
    /// </summary>
    struct DefaultKeyEquality<K> : IKeyEquality<K>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(IEqualityComparer<K> comparer, K a, K b) => EqualityComparer<K>.Default.Equals(a, b);
    }

    /// <summary>
    /// Any comparer
    /// </summary>
    struct ComparerKeyEquality<K> : IKeyEquality<K>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(IEqualityComparer<K> comparer, K a, K b) => comparer.Equals(a, b);
    }

    /// <summary>
    /// Selects the <see cref="IKeyEquality{K}"/> implementation to use
    /// </summary>
    static class KeyEquality<K>
    {
        /// <summary>
        /// True if the key is a value type and the comparer is the default comparer (use <see cref="DefaultKeyEquality{K}"/>)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDefault(IEqualityComparer<K> comparer)
            => typeof(K).IsValueType && ReferenceEquals(comparer, EqualityComparer<K>.Default);
    }

    /// <summary>
    /// An immutable dictionary with a single entry (see <see cref="DictionaryExt.Single{K, V}(K, V, IEqualityComparer{K})"/>).
    /// </summary>
    /// <typeparam name="K">The key type</typeparam>
    /// <typeparam name="V">The value type</typeparam>
    /// <typeparam name="TEq">How the key is compared, <see cref="DefaultKeyEquality{K}"/> for value types using the default comparer (devirtualized), else <see cref="ComparerKeyEquality{K}"/></typeparam>
    /// <remarks>
    /// Lookups with a null key throw an <see cref="ArgumentNullException"/> (like a <see cref="Dictionary{TKey, TValue}"/> and the other frozen dictionaries), the comparer is never called with a null key.
    /// The null checks are removed by the JIT for value type keys.
    /// </remarks>
    sealed class SingleReadonlyDictionary<K, V, TEq> : IReadOnlyDictionary<K, V>, IHaveComparere<K> where TEq : struct, IKeyEquality<K>
    {
        public SingleReadonlyDictionary(K key, V value, IEqualityComparer<K> comp)
        {
            Key = key;
            Value = value;
            Comp = comp;
        }

        readonly K Key;
        readonly V Value;

        /// <summary>
        /// The item as an array, created on first use (enumeration is rare, lookups are the common case)
        /// </summary>
        KeyValuePair<K, V>[] KVe;

        public IEqualityComparer<K> Comp { get; init; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsKey(K key)
        {
            if (key is null)
                OrdinalStringKeys.ThrowNullKey();
            return TEq.Equals(Comp, key, Key);
        }

        public V this[K key]
        {
            get
            {
                if (IsKey(key))
                    return Value;
                throw new KeyNotFoundException();
            }

        }

        // Created on first use (rare), so that freezing a single item dictionary is a single allocation
        public IEnumerable<K> Keys => KeyArray ??= [Key];

        public IEnumerable<V> Values => ValueArray ??= [Value];

        K[] KeyArray;
        V[] ValueArray;

        public int Count => 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(K key)
            => IsKey(key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => ((IEnumerable<KeyValuePair<K, V>>)(KVe ??= [new KeyValuePair<K, V>(Key, Value)])).GetEnumerator();

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
