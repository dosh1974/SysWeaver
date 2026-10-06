using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;


namespace SysWeaver
{
    /// <summary>
    /// Read only set extensions: merging, freezing (creating optimized immutable sets) and getting the comparer of a set.
    /// </summary>
    /// <remarks>
    /// <see cref="Freeze{K}(IReadOnlySet{K}, IEqualityComparer{K})"/> picks the fastest implementation for the content:
    /// a shared empty set, a single item set, a vectorized set for 2-8 integer / enum keys, an open addressing table for more integer / enum keys,
    /// a fingerprint based set for ordinal string keys, else a <see cref="FrozenSet{T}"/>.
    /// The sets returned by Freeze remember their comparer, so freezing them again (with the same comparer) returns the same instance.
    /// </remarks>
    public static class SetExt
    {
        /// <summary>
        /// Merge (union) a set with zero or more other sets.
        /// The comparer used is the comparer of <paramref name="t"/> if it's non-null, else the comparer of the first non-empty set in <paramref name="others"/>.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="t">The first set (may be null)</param>
        /// <param name="others">The sets to merge into the first set (may be null, null sets are ignored)</param>
        /// <returns>The union of all sets.
        /// If at most one of the sets is non-empty, that set instance is returned as is (no copy is made, may be null if all sets are null), else a new <see cref="HashSet{T}"/>.
        /// The input sets are never modified.</returns>
        /// <exception cref="Exception">Thrown if more than one of the sets is non-empty (a new set is created) and the comparer of <paramref name="t"/> (or, if <paramref name="t"/> is null, of the first non-empty other set) can't be determined (see <see cref="GetComparer{T}(IReadOnlySet{T})"/>)</exception>
        public static IReadOnlySet<T> Merge<T>(this IReadOnlySet<T> t, params IReadOnlySet<T>[] others)
            => Merge<T>(t, false, others);


        /// <summary>
        /// Merge (union) a set with zero or more other sets, optionally freezing the result.
        /// The comparer used is the comparer of <paramref name="t"/> if it's non-null, else the comparer of the first non-empty set in <paramref name="others"/>.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="t">The first set (may be null)</param>
        /// <param name="freeze">True to return a frozen set (see <see cref="Freeze{K}(IReadOnlySet{K}, IEqualityComparer{K})"/>).
        /// If the result is one of the input sets and its comparer is unknown, it's returned as is (not frozen)</param>
        /// <param name="others">The sets to merge into the first set (may be null, null sets are ignored)</param>
        /// <returns>The union of all sets.
        /// If at most one of the sets is non-empty, that set instance (or its frozen version) is returned, else a new set.
        /// The input sets are never modified.</returns>
        /// <exception cref="Exception">Thrown if more than one of the sets is non-empty (a new set is created) and the comparer of <paramref name="t"/> (or, if <paramref name="t"/> is null, of the first non-empty other set) can't be determined (see <see cref="GetComparer{T}(IReadOnlySet{T})"/>)</exception>
        public static IReadOnlySet<T> Merge<T>(this IReadOnlySet<T> t, bool freeze, params IReadOnlySet<T>[] others)
        {
            if (others == null)
                return freeze ? TryFreeze(t) : t;
            //  The comparer is only needed (and resolved) when a new set must be created
            var org = t;
            var l = others.Length;
            for (int i = 0; i < l; ++i)
            {
                var x = others[i];
                if (x == null)
                    continue;
                if (x.Count <= 0)
                    continue;
                if ((t == null) || (t.Count <= 0))
                {
                    t = x;
                    continue;
                }
                var cmp = (org ?? t).GetComparer();
                // Copying a HashSet with the same comparer is fast (the buckets are copied)
                var ns = new HashSet<T>(t, cmp);
                for (; i < l; ++i)
                {
                    x = others[i];
                    if (x == null)
                        continue;
                    // A HashSet is enumerated without boxing the enumerator
                    if (x is HashSet<T> hs)
                    {
                        foreach (var item in hs)
                            ns.Add(item);
                    }
                    else
                    {
                        ns.UnionWith(x);
                    }
                }
                if (freeze)
                    return ns.Freeze();
                return ns;
            }
            return freeze ? TryFreeze(t) : t;
        }

        /// <summary>
        /// Freeze a set if the comparer is known (else return it as is)
        /// </summary>
        static IReadOnlySet<T> TryFreeze<T>(IReadOnlySet<T> t)
        {
            var cmp = TryGetComparer(t);
            return cmp == null ? t : Freeze(t, cmp);
        }


        /// <summary>
        /// Create a frozen (immutable, lookup optimized) copy of a hash set, using the comparer of the set.
        /// </summary>
        /// <typeparam name="K">The element type</typeparam>
        /// <param name="d">The set to freeze (may be null)</param>
        /// <returns>An immutable set with the same elements and comparer, or null if <paramref name="d"/> is null.
        /// See <see cref="Freeze{K}(IReadOnlySet{K}, IEqualityComparer{K})"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlySet<K> Freeze<K>(this HashSet<K> d)
            => Freeze<K>(d, d?.Comparer);

        /// <summary>
        /// Create a frozen (immutable, lookup optimized) version of a set, using the comparer of the set.
        /// </summary>
        /// <typeparam name="K">The element type</typeparam>
        /// <param name="d">The set to freeze (may be null)</param>
        /// <returns>An immutable set with the same elements and comparer, or null if <paramref name="d"/> is null.
        /// If <paramref name="d"/> is already frozen it's returned as is.
        /// See <see cref="Freeze{K}(IReadOnlySet{K}, IEqualityComparer{K})"/>.</returns>
        /// <exception cref="Exception">Thrown if the comparer of <paramref name="d"/> can't be determined (see <see cref="GetComparer{T}(IReadOnlySet{T})"/>)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlySet<K> Freeze<K>(this IReadOnlySet<K> d)
            => Freeze<K>(d, d.GetComparer());

        /// <summary>
        /// Create a frozen (immutable, lookup optimized) version of a set, using a specific comparer.
        /// </summary>
        /// <remarks>
        /// The implementation is chosen from the content and the comparer:
        /// a shared (cached per comparer) empty set, a single item set, a vectorized set for 2-8 keys of a 4 or 8 byte integer / enum type (default comparer),
        /// an open addressing table for more such keys, a fingerprint based set for ordinal string keys (<see cref="StringComparer.Ordinal"/> or the default string comparer),
        /// else a <see cref="FrozenSet{T}"/>.
        /// The returned sets are thread safe for reads.
        /// Freezing allocates a copy, it's intended for data that is created once and read many times.
        /// </remarks>
        /// <typeparam name="K">The element type</typeparam>
        /// <param name="d">The set to freeze (may be null)</param>
        /// <param name="comparer">The comparer to use (also kept for an empty set), must not be null</param>
        /// <returns>An immutable set with the elements of <paramref name="d"/> using <paramref name="comparer"/>, or null if <paramref name="d"/> is null.
        /// If <paramref name="d"/> is already a frozen set with the same comparer, it's returned as is.</returns>
        /// <exception cref="Exception">Thrown if <paramref name="comparer"/> is null (and <paramref name="d"/> is non-null)</exception>
        public static IReadOnlySet<K> Freeze<K>(this IReadOnlySet<K> d, IEqualityComparer<K> comparer)
        {
            if (d == null)
                return null;
            if (comparer == null)
                throw new Exception("Must specify a comparer!");
            // Already frozen with the same comparer
            if ((d is IHaveComparere<K> h) && (h.Comp == comparer))
                return d;
            var l = d.Count;
            if (l <= 0)
                return EmptyReadonlySet<K>.Get(comparer);
            if (l == 1)
            {
                var f = FrozenCopy.First(d);
                return KeyEquality<K>.IsDefault(comparer)
                    ? new SingleReadonlySet<K, DefaultKeyEquality<K>>(f, comparer)
                    : new SingleReadonlySet<K, ComparerKeyEquality<K>>(f, comparer);
            }
            // A few integer keys: faster than a FrozenSet (a linear search) and a HashSet
            if (SmallValueKeys<K>.CanUse(l, comparer))
                return new SmallValueKeyReadonlySet<K>(d);
            if ((d as FrozenSet<K>)?.Comparer == comparer)
                return d;
            // More integer keys: an open addressing table, faster than a FrozenSet
            if (ValueKeyTable<K>.CanUse(l, comparer))
                return new ValueKeyTableReadonlySet<K>(d);
            // String keys (ordinal): a lookup on a fingerprint of the keys (a few chars where the keys differ), faster than a FrozenSet
            if (OrdinalStringKeys.IsOrdinal(comparer))
            {
                var s = OrdinalStringKeys.TryCreateSet((IReadOnlySet<string>)(object)d, (IEqualityComparer<string>)(object)comparer);
                if (s != null)
                    return (IReadOnlySet<K>)(object)s;
            }
            return d.ToFrozenSet(comparer);
        }

        /// <summary>
        /// Get the comparer of a set (<see cref="FrozenSet{T}"/>, <see cref="HashSet{T}"/> and the sets returned by Freeze).
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="set">The set (may be null)</param>
        /// <returns>The comparer of the set, or <see cref="EqualityComparer{T}.Default"/> if <paramref name="set"/> is null</returns>
        /// <exception cref="Exception">Thrown if the comparer is unknown (any other set implementation, like a SortedSet or a custom set)</exception>
        public static IEqualityComparer<T> GetComparer<T>(this IReadOnlySet<T> set)
        {
            if (set == null)
                return EqualityComparer<T>.Default;
            return TryGetComparer(set) ?? throw new Exception("No comparer could be found!");
        }

        /// <summary>
        /// Get the comparer of a set, or null if it's unknown
        /// </summary>
        static IEqualityComparer<T> TryGetComparer<T>(IReadOnlySet<T> set)
            => set switch
            {
                FrozenSet<T> a => a.Comparer,
                IHaveComparere<T> b => b.Comp,
                HashSet<T> c => c.Comparer,
                _ => null,
            };
    }


    /// <summary>
    /// Shared read only set instances.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    public static class ReadOnlySet<T>
    {
        /// <summary>
        /// A shared, immutable, empty set using <see cref="EqualityComparer{T}.Default"/> (the same instance as <see cref="ReadOnlyData.EmptySet{T}"/>).
        /// </summary>
        public static readonly IReadOnlySet<T> Empty = EmptyReadonlySet<T>.Default;
    }


    /// <summary>
    /// An immutable empty set that remembers its comparer, one shared instance per comparer (returned by <see cref="SetExt.Freeze{K}(IReadOnlySet{K}, IEqualityComparer{K})"/> for an empty set).
    /// </summary>
    /// <typeparam name="K">The element type</typeparam>
    sealed class EmptyReadonlySet<K> : IReadOnlySet<K>, IHaveComparere<K>
    {

        /// <summary>
        /// The empty set using <see cref="EqualityComparer{T}.Default"/>
        /// </summary>

        public static readonly EmptyReadonlySet<K> Default = new(EqualityComparer<K>.Default);

        /// <summary>
        /// The empty sets of other comparers
        /// </summary>
        static readonly ConditionalWeakTable<IEqualityComparer<K>, EmptyReadonlySet<K>> Others = new();

        /// <summary>
        /// The last empty set returned (starts with the default comparer)
        /// </summary>
        static EmptyReadonlySet<K> Last = Default;

        /// <summary>
        /// Get the shared empty set of a comparer (the instances of other comparers than the default are kept in a weak table, so they don't keep the comparer alive).
        /// Thread safe.
        /// </summary>
        /// <param name="comparer">The comparer, must not be null</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EmptyReadonlySet<K> Get(IEqualityComparer<K> comparer)
        {
            // The last used comparer is cached (it's typically the same comparer every time)
            var last = Last;
            return last.Comp == comparer ? last : GetSlow(comparer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static EmptyReadonlySet<K> GetSlow(IEqualityComparer<K> comparer)
        {
            var last = comparer == Default.Comp ? Default : Others.GetValue(comparer, c => new EmptyReadonlySet<K>(c));
            Last = last;
            return last;
        }

        EmptyReadonlySet(IEqualityComparer<K> comparer)
        {
            Comp = comparer;
        }

        public IEqualityComparer<K> Comp { get; init; }

        public int Count => 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(K key) => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<K> GetEnumerator() => ((IEnumerable<K>)Array.Empty<K>()).GetEnumerator();

        /// <summary>
        /// True if the other collection have any items
        /// </summary>
        static bool Any(IEnumerable<K> other)
        {
            if (other.TryGetNonEnumeratedCount(out var c))
                return c > 0;
            using var e = other.GetEnumerator();
            return e.MoveNext();
        }

        public bool IsProperSubsetOf(IEnumerable<K> other) => Any(other);

        public bool IsProperSupersetOf(IEnumerable<K> other) => false;

        public bool IsSubsetOf(IEnumerable<K> other) => true;

        public bool IsSupersetOf(IEnumerable<K> other) => !Any(other);

        public bool Overlaps(IEnumerable<K> other) => false;

        public bool SetEquals(IEnumerable<K> other) => !Any(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// An immutable set with a single item (returned by <see cref="SetExt.Freeze{K}(IReadOnlySet{K}, IEqualityComparer{K})"/> for a set with one item).
    /// </summary>
    /// <typeparam name="K">The element type</typeparam>
    /// <typeparam name="TEq">How the item is compared, <see cref="DefaultKeyEquality{K}"/> for value types using the default comparer (devirtualized), else <see cref="ComparerKeyEquality{K}"/></typeparam>
    /// <remarks>
    /// The set operations (subset, superset etc) enumerate the other collection and compare using this set's comparer.
    /// </remarks>
    sealed class SingleReadonlySet<K, TEq> : IReadOnlySet<K>, IHaveComparere<K> where TEq : struct, IKeyEquality<K>
    {
        public SingleReadonlySet(K key, IEqualityComparer<K> comp)
        {
            Key = key;
            Comp = comp;
        }

        readonly K Key;

        /// <summary>
        /// The key as an array, created on first use (enumeration is rare, lookups are the common case), so that freezing a single item set is a single allocation
        /// </summary>
        K[] Ke;

        public IEqualityComparer<K> Comp { get; init; }

        public int Count => 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsKey(K key)
            => TEq.Equals(Comp, key, Key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(K key)
            => IsKey(key);

        public IEnumerator<K> GetEnumerator() => ((IEnumerable<K>)(Ke ??= [Key])).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Check if the other collection contains the key, and any other items
        /// </summary>
        void Scan(IEnumerable<K> other, out bool hasKey, out bool hasOther)
        {
            hasKey = false;
            hasOther = false;
            foreach (var x in other)
            {
                if (IsKey(x))
                    hasKey = true;
                else
                    hasOther = true;
                if (hasKey && hasOther)
                    return;
            }
        }

        public bool IsProperSubsetOf(IEnumerable<K> other)
        {
            Scan(other, out var hasKey, out var hasOther);
            return hasKey && hasOther;
        }

        public bool IsProperSupersetOf(IEnumerable<K> other)
        {
            // Only an empty set is a proper subset of a single item set
            if (other.TryGetNonEnumeratedCount(out var c))
                return c == 0;
            using var e = other.GetEnumerator();
            return !e.MoveNext();
        }

        public bool IsSubsetOf(IEnumerable<K> other)
        {
            foreach (var x in other)
                if (IsKey(x))
                    return true;
            return false;
        }

        public bool IsSupersetOf(IEnumerable<K> other)
        {
            foreach (var x in other)
                if (!IsKey(x))
                    return false;
            return true;
        }

        public bool Overlaps(IEnumerable<K> other) => IsSubsetOf(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetEquals(IEnumerable<K> other)
        {
            // Not empty, and only the key (any number of times)
            if (other is K[] arr)
            {
                // The common case: a single item
                if (arr.Length == 1)
                    return IsKey(arr[0]);
                if (arr.Length == 0)
                    return false;
                foreach (var x in arr)
                    if (!IsKey(x))
                        return false;
                return true;
            }
            using var e = other.GetEnumerator();
            if (!e.MoveNext())
                return false;
            do
            {
                if (!IsKey(e.Current))
                    return false;
            } while (e.MoveNext());
            return true;
        }



    }


}
