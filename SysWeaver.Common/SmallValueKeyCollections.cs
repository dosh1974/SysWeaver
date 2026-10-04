using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        /// Create the padded key array
        /// </summary>
        /// <param name="keys">The keys (2 to 8, unique)</param>
        /// <returns>An array of 8 keys, the unused slots are set to the first key</returns>
        public static K[] Pad(K[] keys)
        {
            var p = new K[MaxCount];
            keys.CopyTo(p, 0);
            for (int i = keys.Length; i < MaxCount; ++i)
                p[i] = keys[0];
            return p;
        }

        /// <summary>
        /// Find a key
        /// </summary>
        /// <param name="padded">The padded keys</param>
        /// <param name="key">The key to find</param>
        /// <returns>The index of the key (always less than the number of keys, since the padding is the first key), -1 if not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(K[] padded, K key)
        {
            ref var k = ref MemoryMarshal.GetArrayDataReference(padded);
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
    /// A frozen dictionary with a few (2 to 8) keys of a 4 or 8 byte integer (or enum) type, using the default comparer (see <see cref="SmallValueKeys{K}"/>)
    /// </summary>
    sealed class SmallValueKeyReadonlyDictionary<K, V> : IReadOnlyDictionary<K, V>, IHaveComparere<K>
    {
        public SmallValueKeyReadonlyDictionary(IReadOnlyDictionary<K, V> d)
        {
            var l = d.Count;
            var keys = new K[l];
            var values = new V[l];
            var kv = new KeyValuePair<K, V>[l];
            int i = 0;
            foreach (var x in d)
            {
                keys[i] = x.Key;
                values[i] = x.Value;
                kv[i] = x;
                ++i;
            }
            Padded = SmallValueKeys<K>.Pad(keys);
            KeyArray = keys;
            ValueArray = values;
            Items = kv;
        }

        readonly K[] Padded;
        readonly K[] KeyArray;
        readonly V[] ValueArray;
        readonly KeyValuePair<K, V>[] Items;

        public IEqualityComparer<K> Comp => EqualityComparer<K>.Default;

        public V this[K key]
        {
            get
            {
                var i = SmallValueKeys<K>.IndexOf(Padded, key);
                if (i < 0)
                    throw new KeyNotFoundException();
                return ValueArray[i];
            }
        }

        public IEnumerable<K> Keys => KeyArray;

        public IEnumerable<V> Values => ValueArray;

        public int Count => KeyArray.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(K key)
            => SmallValueKeys<K>.IndexOf(Padded, key) >= 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
        {
            var i = SmallValueKeys<K>.IndexOf(Padded, key);
            if (i < 0)
            {
                value = default;
                return false;
            }
            value = ValueArray[i];
            return true;
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => ((IEnumerable<KeyValuePair<K, V>>)Items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A frozen set with a few (2 to 8) keys of a 4 or 8 byte integer (or enum) type, using the default comparer (see <see cref="SmallValueKeys{K}"/>)
    /// </summary>
    sealed class SmallValueKeyReadonlySet<K> : IReadOnlySet<K>, IHaveComparere<K>
    {
        public SmallValueKeyReadonlySet(IReadOnlySet<K> s)
        {
            var keys = new K[s.Count];
            int i = 0;
            foreach (var x in s)
                keys[i++] = x;
            Padded = SmallValueKeys<K>.Pad(keys);
            KeyArray = keys;
        }

        readonly K[] Padded;
        readonly K[] KeyArray;

        public IEqualityComparer<K> Comp => EqualityComparer<K>.Default;

        public int Count => KeyArray.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(K item)
            => SmallValueKeys<K>.IndexOf(Padded, item) >= 0;

        public IEnumerator<K> GetEnumerator() => ((IEnumerable<K>)KeyArray).GetEnumerator();

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

        HashSet<K> ToSet() => new HashSet<K>(KeyArray);

        public bool IsProperSubsetOf(IEnumerable<K> other) => ToSet().IsProperSubsetOf(other);

        public bool IsProperSupersetOf(IEnumerable<K> other) => ToSet().IsProperSupersetOf(other);

        public bool IsSubsetOf(IEnumerable<K> other) => ToSet().IsSubsetOf(other);

        public bool SetEquals(IEnumerable<K> other) => ToSet().SetEquals(other);
    }
}
