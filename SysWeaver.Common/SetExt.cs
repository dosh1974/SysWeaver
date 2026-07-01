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
        /// <param name="freeze"></param>
        /// <param name="others"></param>
        /// <returns></returns>
        public static IReadOnlySet<T> Merge<T>(this IReadOnlySet<T> t, bool freeze, params IReadOnlySet<T>[] others)
        {
            if (others == null)
                return t;
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
                var ns = new HashSet<T>(t, cmp);
                for (; i < l; ++i)
                {
                    x = others[i];
                    if (x == null)
                        continue;
                    foreach (var y in x)
                        ns.Add(y);
                }
                t = ns;
                if (freeze)
                    t = t.Freeze();
                break;
            }
            return t;
        }


        /// <summary>
        /// Create a frozen version of a set
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <param name="d"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlySet<K> Freeze<K>(this HashSet<K> d)
            => Freeze<K>(d, d.Comparer);

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
                return EmptyReadonlySet<K>.Default;
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
    }


    public static class ReadOnlySet<T>
    {
        public static readonly IReadOnlySet<T> Empty = EmptyReadonlySet<T>.Default;
    }


    sealed class EmptyReadonlySet<K> : IReadOnlySet<K>, IHaveComparere<K>
    {

        public static readonly EmptyReadonlySet<K> Default = new(EqualityComparer<K>.Default);

        EmptyReadonlySet(IEqualityComparer<K> comparer)
        {
            Comp = comparer;
        }

        static readonly IEnumerable<K> EmptyK = Enumerable.Empty<K>();

        public IEqualityComparer<K> Comp { get; init; }

        public int Count => 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(K key) => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<K> GetEnumerator() => EmptyK.GetEnumerator();

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
