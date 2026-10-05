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
    /// Reads a fingerprint of a string: up to 4 chars at a fixed position (from the start or from the end) combined with the length.
    /// The fingerprint is used to find a key without hashing the whole string (the keys are analyzed when frozen to find a position where they differ).
    /// The caller guarantees that the chars read are inside the string or the null terminator (the length is checked against the min length of the keys first).
    /// </summary>
    interface IStringFingerprint
    {
        static abstract ulong Get(ref char s, int length, int offset);
    }

    /// <summary>
    /// The fingerprint readers (structs, so that the code is specialized for every reader)
    /// </summary>
    static class StringFingerprint
    {
        /// <summary>
        /// The length is mixed into the fingerprint
        /// </summary>
        const ulong LengthMul = 0x9E3779B97F4A7C15UL;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Read4(ref char s, int offset) => Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref s, offset)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Read2(ref char s, int offset) => Unsafe.ReadUnaligned<uint>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref s, offset)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Read1(ref char s, int offset) => Unsafe.Add(ref s, offset);

        /// <summary>
        /// 4 chars starting at offset
        /// </summary>
        public struct Left4 : IStringFingerprint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong Get(ref char s, int length, int offset) => Read4(ref s, offset) + (ulong)length * LengthMul;
        }

        /// <summary>
        /// 4 chars starting at length - offset
        /// </summary>
        public struct Right4 : IStringFingerprint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong Get(ref char s, int length, int offset) => Read4(ref s, length - offset) + (ulong)length * LengthMul;
        }

        /// <summary>
        /// 2 chars starting at offset
        /// </summary>
        public struct Left2 : IStringFingerprint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong Get(ref char s, int length, int offset) => Read2(ref s, offset) + (ulong)length * LengthMul;
        }

        /// <summary>
        /// 2 chars starting at length - offset
        /// </summary>
        public struct Right2 : IStringFingerprint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong Get(ref char s, int length, int offset) => Read2(ref s, length - offset) + (ulong)length * LengthMul;
        }

        /// <summary>
        /// The char at offset
        /// </summary>
        public struct Left1 : IStringFingerprint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong Get(ref char s, int length, int offset) => Read1(ref s, offset) + (ulong)length * LengthMul;
        }

        /// <summary>
        /// The char at length - offset
        /// </summary>
        public struct Right1 : IStringFingerprint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong Get(ref char s, int length, int offset) => Read1(ref s, length - offset) + (ulong)length * LengthMul;
        }

        /// <summary>
        /// Only the length
        /// </summary>
        public struct Length : IStringFingerprint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong Get(ref char s, int length, int offset) => (ulong)length * LengthMul;
        }

        public enum Kind
        {
            Left4,
            Right4,
            Left2,
            Right2,
            Left1,
            Right1,
            Length,
        }

        /// <summary>
        /// Get the fingerprint of a string (used when analyzing the keys, not on the lookup path)
        /// </summary>
        public static ulong Get(Kind kind, string s, int offset)
        {
            ref var c = ref Unsafe.AsRef(in s.GetPinnableReference());
            var l = s.Length;
            return kind switch
            {
                Kind.Left4 => Left4.Get(ref c, l, offset),
                Kind.Right4 => Right4.Get(ref c, l, offset),
                Kind.Left2 => Left2.Get(ref c, l, offset),
                Kind.Right2 => Right2.Get(ref c, l, offset),
                Kind.Left1 => Left1.Get(ref c, l, offset),
                Kind.Right1 => Right1.Get(ref c, l, offset),
                _ => Length.Get(ref c, l, offset),
            };
        }

        /// <summary>
        /// The number of chars read by a fingerprint
        /// </summary>
        static int Width(Kind kind) => kind switch
        {
            Kind.Left4 or Kind.Right4 => 4,
            Kind.Left2 or Kind.Right2 => 2,
            Kind.Left1 or Kind.Right1 => 1,
            _ => 0,
        };

        /// <summary>
        /// Find a fingerprint (kind and offset) for the keys, where at most maxDuplicates keys share a fingerprint with another key.
        /// The fingerprint with the fewest duplicates is used (the first one found if there are many without duplicates).
        /// </summary>
        /// <param name="keys">The keys (unique, not null)</param>
        /// <param name="minLength">The length of the shortest key</param>
        /// <param name="maxDuplicates">The max number of keys that may have the same fingerprint as another key</param>
        /// <param name="kind">The kind of fingerprint found</param>
        /// <param name="offset">The offset of the fingerprint found</param>
        /// <returns>True if a fingerprint was found</returns>
        [SkipLocalsInit]
        public static bool TryFind(ReadOnlySpan<string> keys, int minLength, int maxDuplicates, out Kind kind, out int offset)
        {
            // An open addressing set of the fingerprints, a power of 2 >= 2 * n (on the stack for up to 256 slots)
            var tableSize = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(keys.Length * 2, 16));
            if (tableSize <= 256)
                return TryFind(keys, minLength, maxDuplicates, stackalloc ulong[tableSize], stackalloc byte[tableSize], out kind, out offset);
            var table = ArrayPool<ulong>.Shared.Rent(tableSize);
            var used = ArrayPool<byte>.Shared.Rent(tableSize);
            try
            {
                return TryFind(keys, minLength, maxDuplicates, table.AsSpan(0, tableSize), used.AsSpan(0, tableSize), out kind, out offset);
            }
            finally
            {
                ArrayPool<ulong>.Shared.Return(table);
                ArrayPool<byte>.Shared.Return(used);
            }
        }

        static bool TryFind(ReadOnlySpan<string> keys, int minLength, int maxDuplicates, Span<ulong> table, Span<byte> used, out Kind kind, out int offset)
        {
            kind = Kind.Length;
            offset = 0;
            // The chars that all keys have in common at the start and at the end:
            // all windows inside them give the same fingerprints (only the length differs), so only one of them is tried
            var first0 = keys[0].AsSpan();
            int prefix = first0.Length;
            int suffix = first0.Length;
            foreach (var key in keys)
            {
                var k = key.AsSpan();
                prefix = first0.Slice(0, prefix).CommonPrefixLength(k);
                suffix = CommonSuffixLength(first0, k, Math.Min(suffix, k.Length));
            }
            var best = maxDuplicates + 1;
            // The candidates, the most selective first (more chars, from the start first)
            for (var k = Kind.Left4; k <= Kind.Length; ++k)
            {
                var w = Width(k);
                var right = k is Kind.Right4 or Kind.Right2 or Kind.Right1;
                // Left: offset 0 .. minLength - w + 1, right: offset w - 1 .. minLength (the chars at length - offset), length: no offset.
                // The last char read may be the null terminator (a string is always null terminated), so a fingerprint can be used with an empty or short key
                int first = right ? w - 1 : 0;
                int last = k == Kind.Length ? 0 : right ? minLength : minLength - w + 1;
                for (var o = first; o <= last; ++o)
                {
                    if (o > first)
                    {
                        // Skip to the first window that isn't inside the common prefix / suffix
                        if (!right && (o + w <= prefix))
                        {
                            o = prefix - w;
                            continue;
                        }
                        if (right && (o <= suffix))
                        {
                            o = suffix;
                            continue;
                        }
                    }
                    var d = CountDuplicates(keys, k, o, table, used, best);
                    if (d >= best)
                        continue;
                    best = d;
                    kind = k;
                    offset = o;
                    if (d == 0)
                        return true;
                }
            }
            return best <= maxDuplicates;
        }

        /// <summary>
        /// The number of chars that two strings have in common at the end (at most max), using a binary search of vectorized compares
        /// </summary>
        static int CommonSuffixLength(ReadOnlySpan<char> a, ReadOnlySpan<char> b, int max)
        {
            if (b.EndsWith(a.Slice(a.Length - max)))
                return max;
            // The last max chars differ, find the longest common suffix (lo is common, hi is not)
            int lo = 0;
            int hi = max;
            while (hi - lo > 1)
            {
                var m = (lo + hi) >> 1;
                if (b.EndsWith(a.Slice(a.Length - m)))
                    lo = m;
                else
                    hi = m;
            }
            return lo;
        }

        /// <summary>
        /// Count the keys that have the same fingerprint as a previous key (stops at the limit)
        /// </summary>
        static int CountDuplicates(ReadOnlySpan<string> keys, Kind kind, int offset, Span<ulong> table, Span<byte> used, int limit)
        {
            used.Clear();
            var mask = used.Length - 1;
            var dups = 0;
            foreach (var key in keys)
            {
                var fp = Get(kind, key, offset);
                var i = (int)(Mix(fp) & (uint)mask);
                while (true)
                {
                    if (used[i] == 0)
                    {
                        used[i] = 1;
                        table[i] = fp;
                        break;
                    }
                    if (table[i] == fp)
                    {
                        if (++dups >= limit)
                            return dups;
                        break;
                    }
                    i = (i + 1) & mask;
                }
            }
            return dups;
        }

        /// <summary>
        /// Mix the bits of a fingerprint (the top bits are used for the bucket, the low 32 bits are stored to reject most non matching keys without comparing the strings)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Mix(ulong x)
        {
            x ^= x >> 33;
            x *= 0xFF51AFD7ED558CCDUL;
            x ^= x >> 33;
            return x;
        }
    }

    /// <summary>
    /// Lookup of a few (2 to 8) string keys using the ordinal comparer.
    /// Every key has a unique fingerprint (up to 4 chars at a fixed position + the length), the fingerprints are found using a few vector compares, then a single string compare.
    /// </summary>
    readonly struct SmallStringKeys<TFp> where TFp : struct, IStringFingerprint
    {
        public SmallStringKeys(ReadOnlySpan<string> keys, IEqualityComparer<string> comparer, StringFingerprint.Kind kind, int offset, int minLength, int maxLength)
        {
            Inline8<ulong> fps = default;
            Inline8<string> k = default;
            var n = keys.Length;
            for (int i = 0; i < n; ++i)
            {
                fps[i] = StringFingerprint.Get(kind, keys[i], offset);
                k[i] = keys[i];
            }
            SmallValueKeys<ulong>.Pad(fps, n);
            Fingerprints = fps;
            Keys = k;
            Count = (byte)n;
            IsDefaultComparer = !ReferenceEquals(comparer, StringComparer.Ordinal);
            Offset = offset;
            MinLength = minLength;
            LengthRange = (uint)(maxLength - minLength);
        }

        /// <summary>
        /// The fingerprints of the keys (and the padding)
        /// </summary>
        readonly Inline8<ulong> Fingerprints;

        /// <summary>
        /// The keys (the first Count slots)
        /// </summary>
        public readonly Inline8<string> Keys;

        /// <summary>
        /// The number of keys (2 to 8)
        /// </summary>
        public readonly byte Count;

        /// <summary>
        /// The comparer is one of the two ordinal comparers (see <see cref="OrdinalStringKeys.IsOrdinal"/>), stored as a flag (smaller than a reference)
        /// </summary>
        readonly bool IsDefaultComparer;

        public IEqualityComparer<string> Comparer => IsDefaultComparer ? EqualityComparer<string>.Default : StringComparer.Ordinal;

        readonly int Offset;
        readonly int MinLength;
        readonly uint LengthRange;

        /// <summary>
        /// Find a key
        /// </summary>
        /// <returns>The index of the key, -1 if not found (or null)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOf(string key)
        {
            if (key == null)
                return -1;
            var l = key.Length;
            if ((uint)(l - MinLength) > LengthRange)
                return -1;
            var i = SmallValueKeys<ulong>.IndexOf(ref Unsafe.AsRef(in Fingerprints[0]), TFp.Get(ref Unsafe.AsRef(in key.GetPinnableReference()), l, Offset));
            if (i < 0)
                return -1;
            return OrdinalStringKeys.KeyEquals(Unsafe.Add(ref Unsafe.AsRef(in Keys[0]), i), key) ? i : -1;
        }
    }

    /// <summary>
    /// An entry of a <see cref="StringKeyTable{TFp, V}"/>: the key, the low 32 bits of its hash and the value (in the same cache line)
    /// </summary>
    struct StringKeyEntry<V>
    {
        public string Key;
        public uint Hash;
        public V Value;
    }

    /// <summary>
    /// The value of a set entry (nothing)
    /// </summary>
    struct NoValue
    {
    }

    /// <summary>
    /// Lookup of string keys using the ordinal comparer, a hash table on a fingerprint of the keys (up to 4 chars at a fixed position + the length).
    /// The entries are sorted by bucket, a bucket is a range of entries (no linked lists).
    /// An entry contains the key, the low 32 bits of the hash (most other keys are rejected without comparing the strings) and the value.
    /// </summary>
    readonly struct StringKeyTable<TFp, V> where TFp : struct, IStringFingerprint
    {
        /// <param name="keys">The keys</param>
        /// <param name="values">The values (in the same order as the keys), empty for a set</param>
        /// <param name="kind">The fingerprint kind (the same as TFp)</param>
        /// <param name="offset">The fingerprint offset</param>
        /// <param name="minLength">The length of the shortest key</param>
        /// <param name="maxLength">The length of the longest key</param>
        public StringKeyTable(ReadOnlySpan<string> keys, ReadOnlySpan<V> values, StringFingerprint.Kind kind, int offset, int minLength, int maxLength)
        {
            var n = keys.Length;
            var bits = BitOperations.Log2(BitOperations.RoundUpToPowerOf2((uint)n));
            var bucketCount = 1 << bits;
            var shift = 64 - bits;
            var starts = new int[bucketCount + 1];
            var entries = new StringKeyEntry<V>[n];
            var mixed = ArrayPool<ulong>.Shared.Rent(n);
            var pos = ArrayPool<int>.Shared.Rent(bucketCount);
            try
            {
                // Count the keys in every bucket
                for (int i = 0; i < n; ++i)
                {
                    var x = StringFingerprint.Mix(StringFingerprint.Get(kind, keys[i], offset));
                    mixed[i] = x;
                    ++starts[(int)(x >> shift) + 1];
                }
                // The start of every bucket
                for (int i = 1; i <= bucketCount; ++i)
                    starts[i] += starts[i - 1];
                // Place the entries (using the next free position of every bucket)
                Array.Copy(starts, pos, bucketCount);
                for (int i = 0; i < n; ++i)
                {
                    var x = mixed[i];
                    ref var e = ref entries[pos[(int)(x >> shift)]++];
                    e.Key = keys[i];
                    e.Hash = (uint)x;
                    if (!values.IsEmpty)
                        e.Value = values[i];
                }
            }
            finally
            {
                ArrayPool<int>.Shared.Return(pos);
                ArrayPool<ulong>.Shared.Return(mixed);
            }
            Starts = starts;
            Entries = entries;
            Shift = shift;
            Offset = offset;
            MinLength = minLength;
            LengthRange = (uint)(maxLength - minLength);
        }

        readonly int[] Starts;
        public readonly StringKeyEntry<V>[] Entries;
        readonly int Shift;
        readonly int Offset;
        readonly int MinLength;
        readonly uint LengthRange;

        /// <summary>
        /// Find a key
        /// </summary>
        /// <returns>A reference to the entry, a null reference if not found (or null)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref StringKeyEntry<V> Find(string key)
        {
            if (key != null)
            {
                var l = key.Length;
                if ((uint)(l - MinLength) <= LengthRange)
                {
                    var x = StringFingerprint.Mix(TFp.Get(ref Unsafe.AsRef(in key.GetPinnableReference()), l, Offset));
                    ref var s = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Starts), (nint)(x >> Shift));
                    var i = s;
                    var end = Unsafe.Add(ref s, 1);
                    var h = (uint)x;
                    ref var entries = ref MemoryMarshal.GetArrayDataReference(Entries);
                    for (; i < end; ++i)
                    {
                        ref var e = ref Unsafe.Add(ref entries, i);
                        if ((e.Hash == h) && OrdinalStringKeys.KeyEquals(e.Key, key))
                            return ref e;
                    }
                }
            }
            return ref Unsafe.NullRef<StringKeyEntry<V>>();
        }
    }

    /// <summary>
    /// Creates the frozen dictionaries and sets with string keys using the ordinal comparer
    /// </summary>
    static class OrdinalStringKeys
    {
        /// <summary>
        /// The max number of keys that use the small (vectorized fingerprint) implementation
        /// </summary>
        public const int SmallMaxCount = SmallValueKeys<ulong>.MaxCount;

        /// <summary>
        /// Ordinal compare of a key with a (not null) string that has the same fingerprint.
        /// The last 4 chars are compared first: keys often share a long prefix (urls, paths, names), so a string that isn't a key often differs near the end.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool KeyEquals(string key, string s)
        {
            if (ReferenceEquals(key, s))
                return true;
            var l = key.Length;
            if (l != s.Length)
                return false;
            if (l >= 4)
            {
                var tail = (l - 4) * sizeof(char);
                if (Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref Unsafe.As<char, byte>(ref Unsafe.AsRef(in key.GetPinnableReference())), tail))
                    != Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref Unsafe.As<char, byte>(ref Unsafe.AsRef(in s.GetPinnableReference())), tail)))
                    return false;
            }
            return string.Equals(key, s);
        }

        /// <summary>
        /// The dictionaries throw for a null key (like a Dictionary and a FrozenDictionary), the sets don't find it (like a HashSet and a FrozenSet)
        /// </summary>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNullKey() => throw new ArgumentNullException("key");

        /// <summary>
        /// True if the comparer is an ordinal string comparer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOrdinal<K>(IEqualityComparer<K> comparer)
            => (typeof(K) == typeof(string)) && (ReferenceEquals(comparer, StringComparer.Ordinal) || ReferenceEquals(comparer, EqualityComparer<string>.Default));

        /// <summary>
        /// The min and max length of the keys (null if a key is null)
        /// </summary>
        static bool GetLengths(ReadOnlySpan<string> keys, out int min, out int max)
        {
            min = int.MaxValue;
            max = 0;
            foreach (var k in keys)
            {
                if (k == null)
                    return false;
                var l = k.Length;
                if (l < min)
                    min = l;
                if (l > max)
                    max = l;
            }
            return true;
        }

        /// <summary>
        /// Find a fingerprint for the keys (unique for a small set, at most 5% duplicates for a larger set)
        /// </summary>
        static bool TryAnalyze(ReadOnlySpan<string> keys, out StringFingerprint.Kind kind, out int offset, out int min, out int max)
        {
            kind = default;
            offset = 0;
            if (!GetLengths(keys, out min, out max))
                return false;
            var n = keys.Length;
            return StringFingerprint.TryFind(keys, min, n <= SmallMaxCount ? 0 : n / 20, out kind, out offset);
        }

        /// <summary>
        /// Create a frozen dictionary, null if no good fingerprint was found (the caller should use another implementation).
        /// The keys and values are copied to the stack (a few keys) or to pooled arrays while the keys are analyzed.
        /// </summary>
        public static IReadOnlyDictionary<string, V> TryCreateDictionary<V>(IReadOnlyDictionary<string, V> d, IEqualityComparer<string> comparer)
        {
            var n = d.Count;
            if (n <= SmallMaxCount)
            {
                Inline8<string> keys = default;
                Inline8<V> values = default;
                var ks = ((Span<string>)keys).Slice(0, n);
                var vs = ((Span<V>)values).Slice(0, n);
                FrozenCopy.To(d, ks, vs);
                return CreateDictionary<V>(ks, vs, comparer);
            }
            var ka = ArrayPool<string>.Shared.Rent(n);
            var va = ArrayPool<V>.Shared.Rent(n);
            try
            {
                var ks = ka.AsSpan(0, n);
                var vs = va.AsSpan(0, n);
                FrozenCopy.To(d, ks, vs);
                return CreateDictionary<V>(ks, vs, comparer);
            }
            finally
            {
                ArrayPool<string>.Shared.Return(ka, true);
                ArrayPool<V>.Shared.Return(va, RuntimeHelpers.IsReferenceOrContainsReferences<V>());
            }
        }

        static IReadOnlyDictionary<string, V> CreateDictionary<V>(ReadOnlySpan<string> keys, ReadOnlySpan<V> values, IEqualityComparer<string> comparer)
        {
            if (!TryAnalyze(keys, out var kind, out var offset, out var min, out var max))
                return null;
            return kind switch
            {
                StringFingerprint.Kind.Left4 => Dictionary<V, StringFingerprint.Left4>(keys, values, comparer, kind, offset, min, max),
                StringFingerprint.Kind.Right4 => Dictionary<V, StringFingerprint.Right4>(keys, values, comparer, kind, offset, min, max),
                StringFingerprint.Kind.Left2 => Dictionary<V, StringFingerprint.Left2>(keys, values, comparer, kind, offset, min, max),
                StringFingerprint.Kind.Right2 => Dictionary<V, StringFingerprint.Right2>(keys, values, comparer, kind, offset, min, max),
                StringFingerprint.Kind.Left1 => Dictionary<V, StringFingerprint.Left1>(keys, values, comparer, kind, offset, min, max),
                StringFingerprint.Kind.Right1 => Dictionary<V, StringFingerprint.Right1>(keys, values, comparer, kind, offset, min, max),
                _ => Dictionary<V, StringFingerprint.Length>(keys, values, comparer, kind, offset, min, max),
            };
        }

        static IReadOnlyDictionary<string, V> Dictionary<V, TFp>(ReadOnlySpan<string> keys, ReadOnlySpan<V> values, IEqualityComparer<string> comparer, StringFingerprint.Kind kind, int offset, int min, int max)
            where TFp : struct, IStringFingerprint
        {
            if (keys.Length <= SmallMaxCount)
                return new SmallStringKeyReadonlyDictionary<V, TFp>(new SmallStringKeys<TFp>(keys, comparer, kind, offset, min, max), values);
            return new StringKeyTableReadonlyDictionary<V, TFp>(new StringKeyTable<TFp, V>(keys, values, kind, offset, min, max), comparer);
        }

        /// <summary>
        /// Create a frozen set, null if no good fingerprint was found (the caller should use another implementation).
        /// The keys are copied to the stack (a few keys) or to a pooled array while the keys are analyzed.
        /// </summary>
        public static IReadOnlySet<string> TryCreateSet(IReadOnlySet<string> s, IEqualityComparer<string> comparer)
        {
            var n = s.Count;
            if (n <= SmallMaxCount)
            {
                Inline8<string> keys = default;
                var ks = ((Span<string>)keys).Slice(0, n);
                FrozenCopy.To(s, ks);
                return CreateSet(ks, comparer);
            }
            var ka = ArrayPool<string>.Shared.Rent(n);
            try
            {
                var ks = ka.AsSpan(0, n);
                FrozenCopy.To(s, ks);
                return CreateSet(ks, comparer);
            }
            finally
            {
                ArrayPool<string>.Shared.Return(ka, true);
            }
        }

        static IReadOnlySet<string> CreateSet(ReadOnlySpan<string> keys, IEqualityComparer<string> comparer)
        {
            if (!TryAnalyze(keys, out var kind, out var offset, out var min, out var max))
                return null;
            return kind switch
            {
                StringFingerprint.Kind.Left4 => Set<StringFingerprint.Left4>(keys, comparer, kind, offset, min, max),
                StringFingerprint.Kind.Right4 => Set<StringFingerprint.Right4>(keys, comparer, kind, offset, min, max),
                StringFingerprint.Kind.Left2 => Set<StringFingerprint.Left2>(keys, comparer, kind, offset, min, max),
                StringFingerprint.Kind.Right2 => Set<StringFingerprint.Right2>(keys, comparer, kind, offset, min, max),
                StringFingerprint.Kind.Left1 => Set<StringFingerprint.Left1>(keys, comparer, kind, offset, min, max),
                StringFingerprint.Kind.Right1 => Set<StringFingerprint.Right1>(keys, comparer, kind, offset, min, max),
                _ => Set<StringFingerprint.Length>(keys, comparer, kind, offset, min, max),
            };
        }

        static IReadOnlySet<string> Set<TFp>(ReadOnlySpan<string> keys, IEqualityComparer<string> comparer, StringFingerprint.Kind kind, int offset, int min, int max)
            where TFp : struct, IStringFingerprint
        {
            if (keys.Length <= SmallMaxCount)
                return new SmallStringKeyReadonlySet<TFp>(new SmallStringKeys<TFp>(keys, comparer, kind, offset, min, max));
            return new StringKeyTableReadonlySet<TFp>(new StringKeyTable<TFp, NoValue>(keys, default, kind, offset, min, max), comparer);
        }
    }

    /// <summary>
    /// The read only set members shared by the string key sets (the operations that need a real set use a HashSet with the same comparer)
    /// </summary>
    abstract class StringKeyReadonlySetBase : IReadOnlySet<string>, IHaveComparere<string>
    {
        public abstract IEqualityComparer<string> Comp { get; }

        public abstract int Count { get; }

        public abstract bool Contains(string item);

        public abstract IEnumerator<string> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsSupersetOf(IEnumerable<string> other)
        {
            // All the other items must be in the set (no allocation)
            foreach (var x in other)
                if (!Contains(x))
                    return false;
            return true;
        }

        public bool Overlaps(IEnumerable<string> other)
        {
            // Any of the other items is in the set (no allocation)
            foreach (var x in other)
                if (Contains(x))
                    return true;
            return false;
        }

        HashSet<string> ToSet() => new HashSet<string>(this, Comp);

        public bool IsProperSubsetOf(IEnumerable<string> other) => ToSet().IsProperSubsetOf(other);

        public bool IsProperSupersetOf(IEnumerable<string> other) => ToSet().IsProperSupersetOf(other);

        public bool IsSubsetOf(IEnumerable<string> other) => ToSet().IsSubsetOf(other);

        public bool SetEquals(IEnumerable<string> other) => ToSet().SetEquals(other);
    }

    /// <summary>
    /// A frozen dictionary with a few (2 to 8) string keys using the ordinal comparer (see <see cref="SmallStringKeys{TFp}"/>)
    /// </summary>
    sealed class SmallStringKeyReadonlyDictionary<V, TFp> : IReadOnlyDictionary<string, V>, IHaveComparere<string> where TFp : struct, IStringFingerprint
    {
        public SmallStringKeyReadonlyDictionary(in SmallStringKeys<TFp> keys, ReadOnlySpan<V> values)
        {
            K = keys;
            values.CopyTo(ValueSlots);
        }

        readonly SmallStringKeys<TFp> K;

        /// <summary>
        /// The values (the first Count slots)
        /// </summary>
        readonly Inline8<V> ValueSlots;

        public IEqualityComparer<string> Comp => K.Comparer;

        public V this[string key]
        {
            get
            {
                if (key == null)
                    OrdinalStringKeys.ThrowNullKey();
                var i = K.IndexOf(key);
                if (i < 0)
                    throw new KeyNotFoundException();
                return Unsafe.Add(ref Unsafe.AsRef(in ValueSlots[0]), i);
            }
        }

        public IEnumerable<string> Keys
        {
            get
            {
                for (int i = 0; i < K.Count; ++i)
                    yield return K.Keys[i];
            }
        }

        public IEnumerable<V> Values
        {
            get
            {
                for (int i = 0; i < K.Count; ++i)
                    yield return ValueSlots[i];
            }
        }

        public int Count => K.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(string key)
        {
            if (key == null)
                OrdinalStringKeys.ThrowNullKey();
            return K.IndexOf(key) >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out V value)
        {
            if (key == null)
                OrdinalStringKeys.ThrowNullKey();
            var i = K.IndexOf(key);
            if (i < 0)
            {
                value = default;
                return false;
            }
            value = Unsafe.Add(ref Unsafe.AsRef(in ValueSlots[0]), i);
            return true;
        }

        public IEnumerator<KeyValuePair<string, V>> GetEnumerator()
        {
            for (int i = 0; i < K.Count; ++i)
                yield return new KeyValuePair<string, V>(K.Keys[i], ValueSlots[i]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A frozen set with a few (2 to 8) string keys using the ordinal comparer (see <see cref="SmallStringKeys{TFp}"/>)
    /// </summary>
    sealed class SmallStringKeyReadonlySet<TFp> : StringKeyReadonlySetBase where TFp : struct, IStringFingerprint
    {
        public SmallStringKeyReadonlySet(in SmallStringKeys<TFp> keys)
        {
            K = keys;
        }

        public override IEqualityComparer<string> Comp => K.Comparer;

        readonly SmallStringKeys<TFp> K;

        public override int Count => K.Count;

        public override IEnumerator<string> GetEnumerator()
        {
            for (int i = 0; i < K.Count; ++i)
                yield return K.Keys[i];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Contains(string item) => K.IndexOf(item) >= 0;
    }

    /// <summary>
    /// A frozen dictionary with string keys using the ordinal comparer (see <see cref="StringKeyTable{TFp, V}"/>)
    /// </summary>
    sealed class StringKeyTableReadonlyDictionary<V, TFp> : IReadOnlyDictionary<string, V>, IHaveComparere<string> where TFp : struct, IStringFingerprint
    {
        public StringKeyTableReadonlyDictionary(StringKeyTable<TFp, V> table, IEqualityComparer<string> comparer)
        {
            T = table;
            Comp = comparer;
        }

        readonly StringKeyTable<TFp, V> T;

        public IEqualityComparer<string> Comp { get; }

        public V this[string key]
        {
            get
            {
                if (key == null)
                    OrdinalStringKeys.ThrowNullKey();
                ref var e = ref T.Find(key);
                if (Unsafe.IsNullRef(ref e))
                    throw new KeyNotFoundException();
                return e.Value;
            }
        }

        public IEnumerable<string> Keys
        {
            get
            {
                foreach (var e in T.Entries)
                    yield return e.Key;
            }
        }

        public IEnumerable<V> Values
        {
            get
            {
                foreach (var e in T.Entries)
                    yield return e.Value;
            }
        }

        public int Count => T.Entries.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(string key)
        {
            if (key == null)
                OrdinalStringKeys.ThrowNullKey();
            return !Unsafe.IsNullRef(ref T.Find(key));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out V value)
        {
            if (key == null)
                OrdinalStringKeys.ThrowNullKey();
            ref var e = ref T.Find(key);
            if (Unsafe.IsNullRef(ref e))
            {
                value = default;
                return false;
            }
            value = e.Value;
            return true;
        }

        public IEnumerator<KeyValuePair<string, V>> GetEnumerator()
        {
            foreach (var e in T.Entries)
                yield return new KeyValuePair<string, V>(e.Key, e.Value);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A frozen set with string keys using the ordinal comparer (see <see cref="StringKeyTable{TFp, V}"/>)
    /// </summary>
    sealed class StringKeyTableReadonlySet<TFp> : StringKeyReadonlySetBase where TFp : struct, IStringFingerprint
    {
        public StringKeyTableReadonlySet(StringKeyTable<TFp, NoValue> table, IEqualityComparer<string> comparer)
        {
            T = table;
            Comp = comparer;
        }

        public override IEqualityComparer<string> Comp { get; }

        readonly StringKeyTable<TFp, NoValue> T;

        public override int Count => T.Entries.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Contains(string item) => !Unsafe.IsNullRef(ref T.Find(item));

        public override IEnumerator<string> GetEnumerator()
        {
            foreach (var e in T.Entries)
                yield return e.Key;
        }
    }
}
