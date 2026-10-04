using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;


namespace SysWeaver
{
    public static class SetExt
    {
        /// <summary>
        /// Merge two or more sets.
        /// The comparer used is from the first non-null set.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <param name="others"></param>
        /// <returns></returns>
        public static IReadOnlySet<T> Merge<T>(this IReadOnlySet<T> t, params IReadOnlySet<T>[] others)
            => Merge<T>(t, false, others);


        /// <summary>
        /// Merge two or more sets.
        /// The comparer used is from the first non-null set.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <param name="freeze">True to return a frozen set (if the comparer of the result is known)</param>
        /// <param name="others"></param>
        /// <returns></returns>
        public static IReadOnlySet<T> Merge<T>(this IReadOnlySet<T> t, bool freeze, params IReadOnlySet<T>[] others)
        {
            if (others == null)
                return freeze ? TryFreeze(t) : t;
            var cmp = t?.GetComparer();
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
                    cmp = cmp ?? x.GetComparer();
                    continue;
                }
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
        /// Create a frozen version of a set
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <param name="d"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlySet<K> Freeze<K>(this HashSet<K> d)
            => Freeze<K>(d, d?.Comparer);

        /// <summary>
        /// Create a frozen version of a set
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <param name="d"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlySet<K> Freeze<K>(this IReadOnlySet<K> d)
            => Freeze<K>(d, d.GetComparer());

        /// <summary>
        /// Create a frozen version of a set
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <param name="d"></param>
        /// <param name="comparer">The comparer to use (also kept for an empty set)</param>
        /// <returns></returns>
        public static IReadOnlySet<K> Freeze<K>(this IReadOnlySet<K> d, IEqualityComparer<K> comparer)
        {
            if (d == null)
                return null;
            if (comparer == null)
                throw new Exception("Must specify a comparer!");
            var l = d.Count;
            if (l <= 0)
                return EmptyReadonlySet<K>.Get(comparer);
            if (l == 1)
            {
                if ((d as SingleReadonlySet<K>)?.Comp == comparer)
                    return d;
                var f = d.First();
                return new SingleReadonlySet<K>(f, comparer);
            }
            // A few integer keys: faster than a FrozenSet (a linear search) and a HashSet
            if (SmallValueKeys<K>.CanUse(l, comparer))
                return d as SmallValueKeyReadonlySet<K> ?? new SmallValueKeyReadonlySet<K>(d);
            if ((d as FrozenSet<K>)?.Comparer == comparer)
                return d;
            return d.ToFrozenSet(comparer);
        }

        /// <summary>
        /// Get the comparer of a set (FrozenSet, HashSet and the sets returned by Freeze), the default comparer for null
        /// </summary>
        /// <exception cref="Exception">If the comparer is unknown</exception>
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


    public static class ReadOnlySet<T>
    {
        public static readonly IReadOnlySet<T> Empty = EmptyReadonlySet<T>.Default;
    }


    sealed class EmptyReadonlySet<K> : IReadOnlySet<K>, IHaveComparere<K>
    {

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
        /// Get an empty set with a comparer
        /// </summary>
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

    sealed class SingleReadonlySet<K> : IReadOnlySet<K>, IHaveComparere<K>
    {
        public SingleReadonlySet(K key, IEqualityComparer<K> comp)
        {
            Key = key;
            Comp = comp;
            IsDefault = typeof(K).IsValueType && (comp == EqualityComparer<K>.Default);
        }

        readonly K Key;

        /// <summary>
        /// The key as an array, created on first use (enumeration is rare, lookups are the common case), so that freezing a single item set is a single allocation
        /// </summary>
        K[] Ke;

        /// <summary>
        /// True if the key is a value type and the comparer is the default comparer (devirtualized)
        /// </summary>
        readonly bool IsDefault;

        public IEqualityComparer<K> Comp { get; init; }

        public int Count => 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsKey(K key)
            => IsDefault ? EqualityComparer<K>.Default.Equals(key, Key) : Comp.Equals(key, Key);

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
