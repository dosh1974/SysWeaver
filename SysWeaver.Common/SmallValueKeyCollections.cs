using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SysWeaver
{
    /// <summary>
    /// Keys for the frozen dictionaries and sets with a few (2 to 8) keys of a 4 or 8 byte integer (or enum) type, compared using the default comparer.
    /// The keys are stored in an array of 8 (unused slots are padded with the first key), so a key is found using a few vector compares (no hashing, no branches per key).
    /// A FrozenDictionary / FrozenSet with a few value type keys does a linear search, which is slower than a Dictionary / HashSet for keys that are found.
    /// </summary>
    static class SmallValueKeys<K>
    {
        /// <summary>
        /// The max number of keys
        /// </summary>
        public const int MaxCount = 8;

        /// <summary>
        /// True if the keys can be compared bitwise (the default comparer of a 4 or 8 byte integer or enum type)
        /// </summary>
        public static readonly bool Supported = IsSupported();

        static bool IsSupported()
        {
            var t = typeof(K);
            if (t.IsEnum)
                t = Enum.GetUnderlyingType(t);
            return (t == typeof(int)) || (t == typeof(uint)) || (t == typeof(long)) || (t == typeof(ulong));
        }

        /// <summary>
        /// Check if a collection can use the small key implementation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanUse(int count, IEqualityComparer<K> comparer)
            => Supported && (count >= 2) && (count <= MaxCount) && ReferenceEquals(comparer, EqualityComparer<K>.Default);

        /// <summary>
        /// Pad the keys: the unused slots are set to the first key
        /// </summary>
        /// <param name="padded">The 8 slots, the first count slots contains the keys</param>
        /// <param name="count">The number of keys (2 to 8, unique)</param>
        public static void Pad(Span<K> padded, int count)
        {
            var first = padded[0];
            for (int i = count; i < MaxCount; ++i)
                padded[i] = first;
        }

        /// <summary>
        /// Find a key
        /// </summary>
        /// <param name="k">The first of the 8 padded keys</param>
        /// <param name="key">The key to find</param>
        /// <returns>The index of the key (always less than the number of keys, since the padding is the first key), -1 if not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(ref K k, K key)
        {
            if (Vector256.IsHardwareAccelerated)
            {
                // 8 keys of 4 bytes in one compare, 8 keys of 8 bytes in two compares
                uint m;
                if (Unsafe.SizeOf<K>() == 4)
                {
                    ref var p = ref Unsafe.As<K, uint>(ref k);
                    m = Vector256.Equals(Vector256.LoadUnsafe(ref p), Vector256.Create(Unsafe.As<K, uint>(ref key))).ExtractMostSignificantBits();
                }
                else
                {
                    ref var p = ref Unsafe.As<K, ulong>(ref k);
                    var x = Vector256.Create(Unsafe.As<K, ulong>(ref key));
                    m = Vector256.Equals(Vector256.LoadUnsafe(ref p), x).ExtractMostSignificantBits()
                        | (Vector256.Equals(Vector256.LoadUnsafe(ref p, 4), x).ExtractMostSignificantBits() << 4);
                }
                return m == 0 ? -1 : BitOperations.TrailingZeroCount(m);
            }
            if (Vector128.IsHardwareAccelerated)
            {
                uint m;
                if (Unsafe.SizeOf<K>() == 4)
                {
                    ref var p = ref Unsafe.As<K, uint>(ref k);
                    var x = Vector128.Create(Unsafe.As<K, uint>(ref key));
                    m = Vector128.Equals(Vector128.LoadUnsafe(ref p), x).ExtractMostSignificantBits()
                        | (Vector128.Equals(Vector128.LoadUnsafe(ref p, 4), x).ExtractMostSignificantBits() << 4);
                }
                else
                {
                    ref var p = ref Unsafe.As<K, ulong>(ref k);
                    var x = Vector128.Create(Unsafe.As<K, ulong>(ref key));
                    m = Vector128.Equals(Vector128.LoadUnsafe(ref p), x).ExtractMostSignificantBits()
                        | (Vector128.Equals(Vector128.LoadUnsafe(ref p, 2), x).ExtractMostSignificantBits() << 2)
                        | (Vector128.Equals(Vector128.LoadUnsafe(ref p, 4), x).ExtractMostSignificantBits() << 4)
                        | (Vector128.Equals(Vector128.LoadUnsafe(ref p, 6), x).ExtractMostSignificantBits() << 6);
                }
                return m == 0 ? -1 : BitOperations.TrailingZeroCount(m);
            }
            for (int i = 0; i < MaxCount; ++i)
                if (EqualityComparer<K>.Default.Equals(Unsafe.Add(ref k, i), key))
                    return i;
            return -1;
        }
    }

    /// <summary>
    /// 8 items stored in the containing object or on the stack (no array), used for the keys and values of the small containers (one allocation)
    /// </summary>
    [InlineArray(SmallValueKeys<int>.MaxCount)]
    struct Inline8<T>
    {
        T First;
    }

    /// <summary>
    /// Copies the items of the containers that are frozen (a Dictionary / HashSet is enumerated without boxing the enumerator)
    /// </summary>
    static class FrozenCopy
    {
        public static void To<K, V>(IReadOnlyDictionary<K, V> d, Span<K> keys, Span<V> values)
        {
            int i = 0;
            if (d is Dictionary<K, V> dd)
            {
                foreach (var x in dd)
                {
                    keys[i] = x.Key;
                    values[i] = x.Value;
                    ++i;
                }
                return;
            }
            foreach (var x in d)
            {
                keys[i] = x.Key;
                values[i] = x.Value;
                ++i;
            }
        }

        public static void To<K>(IReadOnlySet<K> s, Span<K> keys)
        {
            int i = 0;
            if (s is HashSet<K> hs)
            {
                foreach (var x in hs)
                    keys[i++] = x;
                return;
            }
            foreach (var x in s)
                keys[i++] = x;
        }

        public static KeyValuePair<K, V> First<K, V>(IReadOnlyDictionary<K, V> d)
        {
            if (d is Dictionary<K, V> dd)
            {
                foreach (var x in dd)
                    return x;
            }
            return d.First();
        }

        public static K First<K>(IReadOnlySet<K> s)
        {
            if (s is HashSet<K> hs)
            {
                foreach (var x in hs)
                    return x;
            }
            return s.First();
        }
    }

    /// <summary>
    /// A frozen dictionary with a few (2 to 8) keys of a 4 or 8 byte integer (or enum) type, using the default comparer (see <see cref="SmallValueKeys{K}"/>)
    /// </summary>
    sealed class SmallValueKeyReadonlyDictionary<K, V> : IReadOnlyDictionary<K, V>, IHaveComparere<K>
    {
        public SmallValueKeyReadonlyDictionary(IReadOnlyDictionary<K, V> d)
        {
            var l = d.Count;
            FrozenCopy.To(d, Padded, ValueSlots);
            SmallValueKeys<K>.Pad(Padded, l);
            Count = l;
        }

        /// <summary>
        /// The keys (the first Count slots) and the padding
        /// </summary>
        Inline8<K> Padded;

        /// <summary>
        /// The values (the first Count slots)
        /// </summary>
        Inline8<V> ValueSlots;

        public IEqualityComparer<K> Comp => EqualityComparer<K>.Default;

        public V this[K key]
        {
            get
            {
                var i = SmallValueKeys<K>.IndexOf(ref Padded[0], key);
                if (i < 0)
                    throw new KeyNotFoundException();
                return Unsafe.Add(ref ValueSlots[0], i);
            }
        }

        public IEnumerable<K> Keys
        {
            get
            {
                for (int i = 0; i < Count; ++i)
                    yield return Padded[i];
            }
        }

        public IEnumerable<V> Values
        {
            get
            {
                for (int i = 0; i < Count; ++i)
                    yield return ValueSlots[i];
            }
        }

        public int Count { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(K key)
            => SmallValueKeys<K>.IndexOf(ref Padded[0], key) >= 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
        {
            var i = SmallValueKeys<K>.IndexOf(ref Padded[0], key);
            if (i < 0)
            {
                value = default;
                return false;
            }
            value = Unsafe.Add(ref ValueSlots[0], i);
            return true;
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            // Enumeration is rare (lookups are the common case), so the items are not stored
            for (int i = 0; i < Count; ++i)
                yield return new KeyValuePair<K, V>(Padded[i], ValueSlots[i]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A frozen set with a few (2 to 8) keys of a 4 or 8 byte integer (or enum) type, using the default comparer (see <see cref="SmallValueKeys{K}"/>)
    /// </summary>
    sealed class SmallValueKeyReadonlySet<K> : IReadOnlySet<K>, IHaveComparere<K>
    {
        public SmallValueKeyReadonlySet(IReadOnlySet<K> s)
        {
            var l = s.Count;
            FrozenCopy.To(s, Padded);
            SmallValueKeys<K>.Pad(Padded, l);
            Count = l;
        }

        /// <summary>
        /// The keys (the first Count slots) and the padding
        /// </summary>
        Inline8<K> Padded;

        public IEqualityComparer<K> Comp => EqualityComparer<K>.Default;

        public int Count { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(K item)
            => SmallValueKeys<K>.IndexOf(ref Padded[0], item) >= 0;

        public IEnumerator<K> GetEnumerator()
        {
            for (int i = 0; i < Count; ++i)
                yield return Padded[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsSupersetOf(IEnumerable<K> other)
        {
            // All the other items must be in the set (no allocation)
            foreach (var x in other)
                if (!Contains(x))
                    return false;
            return true;
        }

        public bool Overlaps(IEnumerable<K> other)
        {
            // Any of the other items is in the set (no allocation)
            foreach (var x in other)
                if (Contains(x))
                    return true;
            return false;
        }

        HashSet<K> ToSet() => new HashSet<K>(this);

        public bool IsProperSubsetOf(IEnumerable<K> other) => ToSet().IsProperSubsetOf(other);

        public bool IsProperSupersetOf(IEnumerable<K> other) => ToSet().IsProperSupersetOf(other);

        public bool IsSubsetOf(IEnumerable<K> other) => ToSet().IsSubsetOf(other);

        public bool SetEquals(IEnumerable<K> other) => ToSet().SetEquals(other);
    }
}
