using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver
{
    /// <summary>
    /// An open addressing (linear probing) hash table for more than 8 keys of a 4 or 8 byte integer (or enum) type, using the default comparer (see <see cref="SmallValueKeys{K}"/>).
    /// The size is a power of 2 (at most 75% used), a key is hashed using a multiply and shift (Fibonacci hashing), a slot without a key contains a value that isn't a key.
    /// Faster than a FrozenDictionary / FrozenSet: a key is usually found in the first slot, and a dictionary stores the key and the value next to each other (one cache line).
    /// </summary>
    static class ValueKeyTable<K>
    {
        /// <summary>
        /// Check if a collection can use the table implementation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanUse(int count, IEqualityComparer<K> comparer)
            => SmallValueKeys<K>.Supported && (count > SmallValueKeys<K>.MaxCount) && ReferenceEquals(comparer, EqualityComparer<K>.Default);

        /// <summary>
        /// The number of bits of the table size for a number of keys (at most 75% used)
        /// </summary>
        public static int Bits(int count) => BitOperations.Log2(BitOperations.RoundUpToPowerOf2((uint)(count + (count / 3) + 1)));

        /// <summary>
        /// Get the start slot of a key
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Hash(K key, int shift)
        {
            if (Unsafe.SizeOf<K>() == 4)
                return (int)((Unsafe.As<K, uint>(ref key) * 0x9E3779B9u) >> shift);
            return (int)((Unsafe.As<K, ulong>(ref key) * 0x9E3779B97F4A7C15UL) >> shift);
        }

        /// <summary>
        /// The shift used by <see cref="Hash"/> for a table size
        /// </summary>
        public static int Shift(int bits) => (Unsafe.SizeOf<K>() == 4 ? 32 : 64) - bits;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Eq(K a, K b) => EqualityComparer<K>.Default.Equals(a, b);

        /// <summary>
        /// Find a value that isn't a key (used for the empty slots), the default value (0) if possible (the table doesn't have to be filled)
        /// </summary>
        public static K FindEmpty(K[] keys)
        {
            // The keys as unsigned integers: 0 if it isn't a key, else the largest key + 1 (if the largest isn't the max value)
            var hasZero = false;
            ulong max = 0;
            foreach (var k in keys)
            {
                var v = ToBits(k);
                if (v == 0)
                    hasZero = true;
                if (v > max)
                    max = v;
            }
            if (!hasZero)
                return default;
            var maxValue = Unsafe.SizeOf<K>() == 4 ? uint.MaxValue : ulong.MaxValue;
            if (max != maxValue)
                return FromBits(max + 1);
            // Rare: both 0 and the max value are keys, use the first gap in the sorted keys (the keys are unique and there are less than 2^32 keys, so there is a gap)
            var n = keys.Length;
            var sorted = ArrayPool<ulong>.Shared.Rent(n);
            try
            {
                for (int i = 0; i < n; ++i)
                    sorted[i] = ToBits(keys[i]);
                var s = sorted.AsSpan(0, n);
                s.Sort();
                ulong expected = 0;
                foreach (var v in s)
                {
                    if (v != expected)
                        break;
                    ++expected;
                }
                return FromBits(expected);
            }
            finally
            {
                ArrayPool<ulong>.Shared.Return(sorted);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong ToBits(K key) => Unsafe.SizeOf<K>() == 4 ? Unsafe.As<K, uint>(ref key) : Unsafe.As<K, ulong>(ref key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static K FromBits(ulong v)
        {
            if (Unsafe.SizeOf<K>() == 4)
            {
                var x = (uint)v;
                return Unsafe.As<uint, K>(ref x);
            }
            return Unsafe.As<ulong, K>(ref v);
        }
    }

    /// <summary>
    /// A frozen dictionary with more than 8 keys of a 4 or 8 byte integer (or enum) type, using the default comparer (see <see cref="ValueKeyTable{K}"/>)
    /// </summary>
    sealed class ValueKeyTableReadonlyDictionary<K, V> : IReadOnlyDictionary<K, V>, IHaveComparere<K>
    {
        struct Entry
        {
            public K Key;
            public V Value;
        }

        public ValueKeyTableReadonlyDictionary(IReadOnlyDictionary<K, V> d)
        {
            var n = d.Count;
            var keys = new K[n];
            var values = new V[n];
            FrozenCopy.To(d, keys, values);
            var bits = ValueKeyTable<K>.Bits(n);
            var size = 1 << bits;
            var empty = ValueKeyTable<K>.FindEmpty(keys);
            var entries = new Entry[size];
            if (!ValueKeyTable<K>.Eq(empty, default))
                for (int i = 0; i < size; ++i)
                    entries[i].Key = empty;
            var shift = ValueKeyTable<K>.Shift(bits);
            var mask = size - 1;
            for (int i = 0; i < n; ++i)
            {
                var k = keys[i];
                var p = ValueKeyTable<K>.Hash(k, shift);
                while (!ValueKeyTable<K>.Eq(entries[p].Key, empty))
                    p = (p + 1) & mask;
                entries[p].Key = k;
                entries[p].Value = values[i];
            }
            Entries = entries;
            Empty = empty;
            Shift = shift;
            Mask = mask;
            Count = n;
        }

        readonly Entry[] Entries;
        readonly K Empty;
        readonly int Shift;
        readonly int Mask;

        public int Count { get; }

        public IEqualityComparer<K> Comp => EqualityComparer<K>.Default;

        /// <summary>
        /// Find a key
        /// </summary>
        /// <returns>A reference to the entry, a null reference if not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ref Entry Find(K key)
        {
            ref var e0 = ref MemoryMarshal.GetArrayDataReference(Entries);
            var p = ValueKeyTable<K>.Hash(key, Shift);
            while (true)
            {
                ref var e = ref Unsafe.Add(ref e0, p);
                var k = e.Key;
                if (ValueKeyTable<K>.Eq(k, key))
                    return ref ValueKeyTable<K>.Eq(key, Empty) ? ref Unsafe.NullRef<Entry>() : ref e;
                if (ValueKeyTable<K>.Eq(k, Empty))
                    return ref Unsafe.NullRef<Entry>();
                p = (p + 1) & Mask;
            }
        }

        public V this[K key]
        {
            get
            {
                ref var e = ref Find(key);
                if (Unsafe.IsNullRef(ref e))
                    throw new KeyNotFoundException();
                return e.Value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(K key) => !Unsafe.IsNullRef(ref Find(key));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
        {
            ref var e = ref Find(key);
            if (Unsafe.IsNullRef(ref e))
            {
                value = default;
                return false;
            }
            value = e.Value;
            return true;
        }

        public IEnumerable<K> Keys
        {
            get
            {
                foreach (var x in this)
                    yield return x.Key;
            }
        }

        public IEnumerable<V> Values
        {
            get
            {
                foreach (var x in this)
                    yield return x.Value;
            }
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            // Enumeration is rare (lookups are the common case), so the items are not stored in another array
            var entries = Entries;
            var empty = Empty;
            for (int i = 0; i < entries.Length; ++i)
            {
                var e = entries[i];
                if (!ValueKeyTable<K>.Eq(e.Key, empty))
                    yield return new KeyValuePair<K, V>(e.Key, e.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A frozen set with more than 8 keys of a 4 or 8 byte integer (or enum) type, using the default comparer (see <see cref="ValueKeyTable{K}"/>)
    /// </summary>
    sealed class ValueKeyTableReadonlySet<K> : IReadOnlySet<K>, IHaveComparere<K>
    {
        public ValueKeyTableReadonlySet(IReadOnlySet<K> s)
        {
            var n = s.Count;
            var keys = new K[n];
            FrozenCopy.To(s, keys);
            var bits = ValueKeyTable<K>.Bits(n);
            var size = 1 << bits;
            var empty = ValueKeyTable<K>.FindEmpty(keys);
            var table = new K[size];
            if (!ValueKeyTable<K>.Eq(empty, default))
                Array.Fill(table, empty);
            var shift = ValueKeyTable<K>.Shift(bits);
            var mask = size - 1;
            foreach (var k in keys)
            {
                var p = ValueKeyTable<K>.Hash(k, shift);
                while (!ValueKeyTable<K>.Eq(table[p], empty))
                    p = (p + 1) & mask;
                table[p] = k;
            }
            Table = table;
            Empty = empty;
            Shift = shift;
            Mask = mask;
            Count = n;
        }

        readonly K[] Table;
        readonly K Empty;
        readonly int Shift;
        readonly int Mask;

        public int Count { get; }

        public IEqualityComparer<K> Comp => EqualityComparer<K>.Default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(K item)
        {
            ref var t = ref MemoryMarshal.GetArrayDataReference(Table);
            var p = ValueKeyTable<K>.Hash(item, Shift);
            while (true)
            {
                var k = Unsafe.Add(ref t, p);
                if (ValueKeyTable<K>.Eq(k, item))
                    return !ValueKeyTable<K>.Eq(item, Empty);
                if (ValueKeyTable<K>.Eq(k, Empty))
                    return false;
                p = (p + 1) & Mask;
            }
        }

        public IEnumerator<K> GetEnumerator()
        {
            var table = Table;
            var empty = Empty;
            for (int i = 0; i < table.Length; ++i)
            {
                var k = table[i];
                if (!ValueKeyTable<K>.Eq(k, empty))
                    yield return k;
            }
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
