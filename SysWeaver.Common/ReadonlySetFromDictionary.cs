using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;


namespace SysWeaver
{
    /// <summary>
    /// Dictionary extensions
    /// </summary>
    public static class DictionaryExt
    {
        struct Inst<K, V> : IReadOnlySet<K>
        {
            public Inst(IReadOnlyDictionary<K, V> d)
            {
                D = d;
            }
            readonly IReadOnlyDictionary<K, V> D;

            public int Count => D.Count;

            public bool Contains(K item) => D.ContainsKey(item);

            public bool IsProperSubsetOf(IEnumerable<K> other)
            {
                throw new NotImplementedException();
            }

            public bool IsProperSupersetOf(IEnumerable<K> other)
            {
                throw new NotImplementedException();
            }

            public bool IsSubsetOf(IEnumerable<K> other)
            {
                throw new NotImplementedException();
            }

            public bool IsSupersetOf(IEnumerable<K> other)
            {
                throw new NotImplementedException();
            }

            public bool Overlaps(IEnumerable<K> other)
            {
                throw new NotImplementedException();
            }

            public bool SetEquals(IEnumerable<K> other)
            {
                throw new NotImplementedException();
            }

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
        public static IReadOnlySet<K> KeysAsReadOnlySet<K, V>(this IReadOnlyDictionary<K, V> dictionary) => new Inst<K, V>(dictionary);


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
        /// Create a dictionary from a collection, if the same key is present more than once, the last is used
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="vals">Values</param>
        /// <param name="k">Optional equality comparer</param>
        /// <returns></returns>
        public static Dictionary<K, V> Create<K, V>(IEnumerable<KeyValuePair<K, V>> vals, IEqualityComparer<K> k = null)
        {
            var d = k == null ? new Dictionary<K, V>() : new Dictionary<K, V>(k);
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
            var d = k == null ? new Dictionary<K, V>() : new Dictionary<K, V>(k);
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
        public static bool TryRemove<K, V>(this Dictionary<K, V> d, K key, out V value)
        {
            if (!d.TryGetValue(key, out value))
                return false;
            d.Remove(key);
            return true;
        }

        /// <summary>
        /// Create a frozen version of a dictionary
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="d"></param>
        /// <returns></returns>
        public static IReadOnlyDictionary<K, V> Freeze<K, V>(this Dictionary<K, V> d)
            => Freeze<K, V>(d, d?.Comparer);


        /// <summary>
        /// Create a frozen version of a dictionary
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="d"></param>
        /// <returns></returns>
        public static IReadOnlyDictionary<K, V> Freeze<K, V>(this IReadOnlyDictionary<K, V> d)
            => Freeze<K, V>(d, d?.GetComparer());

        /// <summary>
        /// Create a frozen version of a dictionary
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="d"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        public static IReadOnlyDictionary<K, V> Freeze<K, V>(this IReadOnlyDictionary<K, V> d, IEqualityComparer<K> comparer)
        {
            if (d == null)
                return null;
            if (comparer == null)
                throw new Exception("Must specify a comparer!");
            var l = d.Count;
            if (l <= 0)
            {
                if ((d as EmptyReadonlyDictionary<K, V>)?.Comp == comparer)
                    return d;
                return new EmptyReadonlyDictionary<K, V>(comparer);
            }
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
        /// Create a frozen version of a set
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <param name="d"></param>
        /// <returns></returns>
        public static IReadOnlySet<K> Freeze<K>(this HashSet<K> d)
            => Freeze<K>(d, d.Comparer);

        /// <summary>
        /// Create a frozen version of a set
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <param name="d"></param>
        /// <returns></returns>
        public static IReadOnlySet<K> Freeze<K>(this IReadOnlySet<K> d)
            => Freeze<K>(d, d.GetComparer());

        /// <summary>
        /// Create a frozen version of a set
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <param name="d"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        public static IReadOnlySet<K> Freeze<K>(this IReadOnlySet<K> d, IEqualityComparer<K> comparer)
        {
            if (d == null)
                return null;
            if (comparer == null)
                throw new Exception("Must specify a comparer!");
            var l = d.Count;
            if (l <= 0)
            {
                if ((d as EmptyReadonlySet<K>)?.Comp == comparer)
                    return d;
                return new EmptyReadonlySet<K>(comparer);
            }
            if (l == 1)
            {
                if ((d as SingleReadonlySet<K>)?.Comp == comparer)
                    return d;
                var f = d.First();
                return new SingleReadonlySet<K>(f, comparer);
            }
            if ((d as FrozenSet<K>)?.Comparer == comparer)
                return d;
            return d.ToFrozenSet(comparer);
        }

        public static IEqualityComparer<T> GetComparer<T>(this IReadOnlySet<T> set)
        {
            var a = set as FrozenSet<T>;
            if (a != null)
                return a.Comparer;
            var b = set as IHaveComparere<T>;
            if (b != null)
                return b.Comp;
            var c = set as HashSet<T>;
            if (c != null)
                return c.Comparer;
            if (set == null)
                return EqualityComparer<T>.Default;
            throw new Exception("No comparer could be found!");
        }

        public static IEqualityComparer<K> GetComparer<K, V>(this IReadOnlyDictionary<K, V> dict)
        {
            var a = dict as FrozenDictionary<K, V>;
            if (a != null)
                return a.Comparer;
            var b = dict as IHaveComparere<K>;
            if (b != null)
                return b.Comp;
            var c = dict as Dictionary<K, V>;
            if (c != null)
                return c.Comparer;
            var d = dict as ConcurrentDictionary<K, V>;
            if (d != null)
                return d.Comparer;
            throw new Exception("No comparer could be found!");
        }
    }


    public static class ReadOnlySet<T>
    {
        public static readonly IReadOnlySet<T> Empty = new EmptyReadonlySet<T>(EqualityComparer<T>.Default);
    }

    public static class ReadOnlyDictionary<K, V>
    {
        public static readonly IReadOnlyDictionary<K, V> Empty = new EmptyReadonlyDictionary<K, V>(EqualityComparer<K>.Default);
    }

    public static class ReadOnlyData
    {

        public static IReadOnlySet<T> EmptySet<T>() => ReadOnlySet<T>.Empty;
        public static IReadOnlyDictionary<K, V> EmptyDictionary<K, V>() => ReadOnlyDictionary<K, V>.Empty;

        public static IReadOnlySet<T> Set<T>(IEqualityComparer<T> comparer, IEnumerable<T> data)
        {
            var t = comparer == null ? new HashSet<T>(data) : new HashSet<T>(data, comparer);
            return t.Freeze(comparer);
        }

        public static IReadOnlySet<T> Set<T>(IEnumerable<T> data)
        {
            var t = new HashSet<T>(data);
            return t.Freeze();
        }

        public static IReadOnlySet<T> Set<T>(IEqualityComparer<T> comparer, params T[] data)
        {
            var t = comparer == null ? new HashSet<T>(data) : new HashSet<T>(data, comparer);
            return t.Freeze(comparer);
        }

        public static IReadOnlySet<T> Set<T>(params T[] data)
        {
            var t = new HashSet<T>(data);
            return t.Freeze();
        }


        public static IReadOnlyDictionary<K, V> Dictionary<K, V>(IEqualityComparer<K> comparer, IEnumerable<KeyValuePair<K, V>> data)
        {
            var t = comparer == null ? new Dictionary<K, V>(data) : new Dictionary<K, V>(data, comparer);
            return t.Freeze(comparer);
        }

        public static IReadOnlyDictionary<K, V> Dictionary<K, V>(IEnumerable<KeyValuePair<K, V>> data)
        {
            var t = new Dictionary<K, V>(data);
            return t.Freeze();
        }

        public static IReadOnlyDictionary<K, V> Dictionary<K, V>(IEqualityComparer<K> comparer, params KeyValuePair<K, V>[] data)
        {
            var t = comparer == null ? new Dictionary<K, V>(data) : new Dictionary<K, V>(data, comparer);
            return t.Freeze(comparer);
        }

        public static IReadOnlyDictionary<K, V> Dictionary<K, V>(params KeyValuePair<K, V>[] data)
        {
            var t = new Dictionary<K, V>(data);
            return t.Freeze();
        }

    }



    interface IHaveComparere<K>
    {
        IEqualityComparer<K> Comp { get; }
    }

    sealed class EmptyReadonlyDictionary<K, V> : IReadOnlyDictionary<K, V>, IHaveComparere<K>
    {
        public EmptyReadonlyDictionary(IEqualityComparer<K> comparer)
        {
            Comp = comparer;
        }

        public IEqualityComparer<K> Comp { get; init; }

        public V this[K key] => throw new KeyNotFoundException();

        public IEnumerable<K> Keys => Enumerable.Empty<K>();

        public IEnumerable<V> Values => Enumerable.Empty<V>();

        public int Count => 0;

        public bool ContainsKey(K key) => false;

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => Enumerable.Empty<KeyValuePair<K, V>>().GetEnumerator();

        public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
        {
            value = default;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }


    sealed class SingleReadonlyDictionary<K, V> : IReadOnlyDictionary<K, V>, IHaveComparere<K>
    {
        public SingleReadonlyDictionary(K key, V value, IEqualityComparer<K> comp)
        {
            Key = key;
            Value = value;
            Comp = comp;
            Ke = [key];
            Ve = [value];
            KVe = [new KeyValuePair<K, V>(key, value)];
        }

        readonly K Key;
        readonly V Value;
        readonly K[] Ke;
        readonly V[] Ve;
        readonly IEnumerable<KeyValuePair<K, V>> KVe;

        public IEqualityComparer<K> Comp { get; init; }

        public V this[K key]
        {
            get
            {
                if (Comp.Equals(key, Key))
                    return Value;
                throw new KeyNotFoundException();
            }

        }

        public IEnumerable<K> Keys => Ke;

        public IEnumerable<V> Values => Ve;

        public int Count => 1;

        public bool ContainsKey(K key)
            => Comp.Equals(key, Key);

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => KVe.GetEnumerator();

        public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
        {
            var e = Comp.Equals(key, Key);
            value = e ? Value : default;
            return e;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }


    sealed class EmptyReadonlySet<K> : IReadOnlySet<K>, IHaveComparere<K>
    {
        public EmptyReadonlySet(IEqualityComparer<K> comparer)
        {
            Comp = comparer;
        }

        public IEqualityComparer<K> Comp { get; init; }

        public int Count => 0;

        public bool Contains(K key) => false;

        public IEnumerator<K> GetEnumerator() => Enumerable.Empty<K>().GetEnumerator();

        public bool IsProperSubsetOf(IEnumerable<K> other)
        {
            throw new NotImplementedException();
        }

        public bool IsProperSupersetOf(IEnumerable<K> other)
        {
            throw new NotImplementedException();
        }

        public bool IsSubsetOf(IEnumerable<K> other)
        {
            throw new NotImplementedException();
        }

        public bool IsSupersetOf(IEnumerable<K> other)
        {
            throw new NotImplementedException();
        }

        public bool Overlaps(IEnumerable<K> other)
        {
            throw new NotImplementedException();
        }

        public bool SetEquals(IEnumerable<K> other)
        {
            return !other.GetEnumerator().MoveNext();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    sealed class SingleReadonlySet<K> : IReadOnlySet<K>, IHaveComparere<K>
    {
        public SingleReadonlySet(K key, IEqualityComparer<K> comp)
        {
            Key = key;
            Comp = comp;
            Ke = [key];
        }

        readonly K Key;
        readonly IEnumerable<K> Ke;

        public IEqualityComparer<K> Comp { get; init; }

        public int Count => 1;

        public bool Contains(K key)
            => Comp.Equals(key, Key);

        public IEnumerator<K> GetEnumerator() => Ke.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsProperSubsetOf(IEnumerable<K> other)
        {
            throw new NotImplementedException();
        }

        public bool IsProperSupersetOf(IEnumerable<K> other)
        {
            throw new NotImplementedException();
        }

        public bool IsSubsetOf(IEnumerable<K> other)
        {
            throw new NotImplementedException();
        }

        public bool IsSupersetOf(IEnumerable<K> other)
        {
            throw new NotImplementedException();
        }

        public bool Overlaps(IEnumerable<K> other)
        {
            throw new NotImplementedException();
        }

        public bool SetEquals(IEnumerable<K> other)
        {
            var e = other.GetEnumerator();
            if (!e.MoveNext())
                return false;
            if (!Comp.Equals(Key, e.Current))
                return false;
            return !e.MoveNext();
        }
    }


}
