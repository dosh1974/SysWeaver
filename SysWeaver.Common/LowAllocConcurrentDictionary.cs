using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Threading;

namespace SysWeaver
{

    /// <summary>
    /// A concurrent dictionary designed for (very) large data sets, with as few and as small memory allocations as possible.
    /// No memory is allocated per item, each segment stores the items inline in a few arrays (allocated when growing),
    /// so the GC only sees a few hundred objects regardless of the number of items.
    ///
    /// The keys are split into segments, each segment is an open addressing hash table protected by a spin lock.
    /// All modifications are done while holding the segment lock.
    /// Reads are lock free, they are validated using a per segment version (seqlock) and retried if a write happened during the read.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public sealed class LowAllocConcurrentDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        const int SegmentBits = 6;
        const int SegmentCount = 1 << SegmentBits;
        const int BlockBits = 4;
        const int BlockSize = 1 << BlockBits;
        /// <summary>
        /// Max number of slots in a segment
        /// </summary>
        const int MaxSegmentSlots = 1 << 30;
        const int MaxBlocks = MaxSegmentSlots >> BlockBits;

        /// <summary>
        /// Number of lock free read attempts before falling back to reading under the lock
        /// </summary>
        const int MaxOptimisticReads = 8;

        const byte TagEmpty = 0;
        const byte TagTombstone = 1;

        /// <summary>
        /// If an insert sees this number of slots with the same tag (but a different key), the hash function is assumed to be under attack.
        /// With a working hash the probability is astronomically small (tags are 8 bits), while full hash collisions always have the same tag.
        /// </summary>
        const int MaxFalseTagMatches = 32;

        /// <summary>
        /// If an insert has to scan this many blocks, the hash function is assumed to be under attack (4096 slots, extremely unlikely with a working hash)
        /// </summary>
        const int MaxProbeBlocks = 256;

        readonly LowAllocDictionarySegment[] Segments;
        /// <summary>
        /// The comparer, null for value types using the default comparer (so that calls can be devirtualized and inlined).
        /// For fast string keys this is the (randomized) ordinal comparer used as a fallback.
        /// </summary>
        readonly IEqualityComparer<TKey> Comparer;

        /// <summary>
        /// True for string keys using ordinal comparison, these use a fast seeded hash, with a fallback to the randomized ordinal hash if collisions are detected
        /// </summary>
        readonly bool FastStrings;

        /// <summary>
        /// Create a new dictionary
        /// </summary>
        /// <param name="capacity">The number of items the dictionary can hold before it needs to grow, for large data sets set this to avoid growing (and the allocations it causes)</param>
        /// <param name="comparer">The key comparer to use, null to use the default comparer</param>
        public LowAllocConcurrentDictionary(int capacity = 1024, IEqualityComparer<TKey> comparer = default)
        {
            Comparer = typeof(TKey).IsValueType && ((comparer == null) || ReferenceEquals(comparer, EqualityComparer<TKey>.Default))
                ? null
                : comparer ?? EqualityComparer<TKey>.Default;
            // The default string comparer is ordinal
            if ((typeof(TKey) == typeof(string)) && (ReferenceEquals(Comparer, EqualityComparer<string>.Default) || ReferenceEquals(Comparer, StringComparer.Ordinal)))
            {
                FastStrings = true;
                Comparer = (IEqualityComparer<TKey>)(object)StringComparer.Ordinal;
            }
            Segments = new LowAllocDictionarySegment[SegmentCount];
            // Room for capacity items in total without growing (taking the max load factor into account)
            var perSegment = (Math.Max(0L, capacity) + SegmentCount - 1) / SegmentCount;
            // Keys are randomly distributed between segments, for large capacities add 4 standard deviations of headroom (~1% at 10M items),
            // else about half the segments would have to grow (doubling their size)
            if (perSegment >= 1024)
                perSegment += 4 * (long)Math.Sqrt(perSegment);
            var slots = perSegment * 8 / 7 + 1;
            var blocks = (int)Math.Clamp((slots + BlockSize - 1) >> BlockBits, 1, MaxBlocks);
            for (int i = 0; i < SegmentCount; i++)
                Segments[i].Table = new Table(blocks);
        }

        public LowAllocConcurrentDictionary(IEqualityComparer<TKey> comparer)
             : this(1024, comparer)
        {
        }


        public void Add(TKey key, TValue value)
        {
            if (!TryAdd(key, value))
                throw new ArgumentException("An item with the same key has already been added", nameof(key));
        }

        public bool ContainsKey(TKey key) => TryGetValue(key, out _);
        public bool Remove(TKey key) => TryRemove(key, out _);

        public TValue this[TKey key]
        {
            get => TryGetValue(key, out var val) ? val : throw new KeyNotFoundException();
            set => Set(key, value);
        }

        /// <summary>
        /// The number of items in the dictionary (a snapshot, other threads may modify the dictionary concurrently)
        /// </summary>
        public int Count
        {
            get
            {
                int total = 0;
                var segs = Segments;
                for (int s = 0; s < SegmentCount; s++)
                    total += Volatile.Read(ref GetTable(ref segs[s]).Live);
                return total;
            }
        }

        public bool IsReadOnly => false;
        public ICollection<TKey> Keys => GetKeysCollection();

        public ICollection<TValue> Values => GetValuesCollection();

        // The public operations selects the comparer strategy (devirtualized default comparer for value types, fast strings or a comparer instance)
        // and the probing strategy (constant for the JIT), so that the core operations are specialized without any runtime checks

        /// <summary>
        /// True if the default comparer is used for a value type key (all calls can be devirtualized and inlined)
        /// </summary>
        bool UseDefaultComparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => typeof(TKey).IsValueType && (Comparer == null);
        }

        /// <summary>
        /// True if string keys are compared ordinal (using the fast seeded hash)
        /// </summary>
        bool UseFastStrings
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !typeof(TKey).IsValueType && FastStrings;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(TKey key, TValue value)
        {
            if (IsNull(key)) ThrowKeyNull();
            if (UseDefaultComparer)
                return Add<DefaultKeyComparer>(key, value, false);
            if (UseFastStrings)
                return Add<FastStringKeyComparer>(key, value, false);
            return Add<CustomKeyComparer>(key, value, false);
        }

        /// <summary>
        /// Add a key, or update the value if the key already exists
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Set(TKey key, TValue value)
        {
            if (IsNull(key)) ThrowKeyNull();
            if (UseDefaultComparer)
                Add<DefaultKeyComparer>(key, value, true);
            else if (UseFastStrings)
                Add<FastStringKeyComparer>(key, value, true);
            else
                Add<CustomKeyComparer>(key, value, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool Add<TCmp>(TKey key, TValue value, bool overwrite) where TCmp : struct, IKeyComparer
            => Vector128.IsHardwareAccelerated ? AddCore<VectorProbe, TCmp>(key, value, overwrite) : AddCore<FallbackProbe, TCmp>(key, value, overwrite);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (IsNull(key)) ThrowKeyNull();
            if (UseDefaultComparer)
                return Get<DefaultKeyComparer>(key, out value);
            if (UseFastStrings)
                return Get<FastStringKeyComparer>(key, out value);
            return Get<CustomKeyComparer>(key, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool Get<TCmp>(TKey key, [MaybeNullWhen(false)] out TValue value) where TCmp : struct, IKeyComparer
            => Vector128.IsHardwareAccelerated ? TryGetValueCore<VectorProbe, TCmp>(key, out value) : TryGetValueCore<FallbackProbe, TCmp>(key, out value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemove(TKey key, [MaybeNullWhen(false)] out TValue value)
            => Remove(key, out value, false, default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value, bool matchValue, TValue expected)
        {
            if (IsNull(key)) ThrowKeyNull();
            if (UseDefaultComparer)
                return Remove<DefaultKeyComparer>(key, out value, matchValue, expected);
            if (UseFastStrings)
                return Remove<FastStringKeyComparer>(key, out value, matchValue, expected);
            return Remove<CustomKeyComparer>(key, out value, matchValue, expected);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool Remove<TCmp>(TKey key, [MaybeNullWhen(false)] out TValue value, bool matchValue, TValue expected) where TCmp : struct, IKeyComparer
            => Vector128.IsHardwareAccelerated ? RemoveCore<VectorProbe, TCmp>(key, out value, matchValue, expected) : RemoveCore<FallbackProbe, TCmp>(key, out value, matchValue, expected);

        [DoesNotReturn]
        static void ThrowKeyNull() => throw new ArgumentNullException("key");


        #region Hashing

        /// <summary>
        /// How keys are hashed and compared
        /// </summary>
        interface IKeyComparer
        {
            /// <summary>
            /// Get the 64 bit hash used for selecting the segment (and the slot, unless the table is randomized)
            /// </summary>
            static abstract ulong GetHash(IEqualityComparer<TKey> comparer, TKey key);

            /// <summary>
            /// Get the 64 bit hash used for selecting the slot in a table (the tag and home block)
            /// </summary>
            /// <param name="table">The table</param>
            /// <param name="hash">The hash from GetHash</param>
            /// <param name="key">The key</param>
            /// <param name="comparer">The comparer instance</param>
            static abstract ulong GetTableHash(Table table, ulong hash, TKey key, IEqualityComparer<TKey> comparer);

            static abstract bool Equals(IEqualityComparer<TKey> comparer, TKey a, TKey b);

            /// <summary>
            /// True if tables can be switched to a randomized hash when collisions are detected
            /// </summary>
            static abstract bool CanRandomize { get; }
        }

        /// <summary>
        /// The default comparer for value types (devirtualized and inlined by the JIT), the comparer argument is null
        /// </summary>
        struct DefaultKeyComparer : IKeyComparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong GetHash(IEqualityComparer<TKey> comparer, TKey key) => Mix(EqualityComparer<TKey>.Default.GetHashCode(key));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong GetTableHash(Table table, ulong hash, TKey key, IEqualityComparer<TKey> comparer) => hash;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Equals(IEqualityComparer<TKey> comparer, TKey a, TKey b) => EqualityComparer<TKey>.Default.Equals(a, b);

            public static bool CanRandomize => false;
        }

        /// <summary>
        /// A comparer instance
        /// </summary>
        struct CustomKeyComparer : IKeyComparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong GetHash(IEqualityComparer<TKey> comparer, TKey key) => Mix(comparer.GetHashCode(key));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong GetTableHash(Table table, ulong hash, TKey key, IEqualityComparer<TKey> comparer) => hash;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Equals(IEqualityComparer<TKey> comparer, TKey a, TKey b) => comparer.Equals(a, b);

            public static bool CanRandomize => false;
        }

        /// <summary>
        /// Ordinal string keys (TKey is string), using a fast seeded hash.
        /// If collisions are detected in a table, it's rebuilt using the (randomized) ordinal comparer for the slots, 
        /// the fast hash is still used for selecting the segment (an attack can at worst put all keys in the same segment).
        /// </summary>
        struct FastStringKeyComparer : IKeyComparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong GetHash(IEqualityComparer<TKey> comparer, TKey key) => LowAllocStringHash.GetHash(Unsafe.As<TKey, string>(ref key));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ulong GetTableHash(Table table, ulong hash, TKey key, IEqualityComparer<TKey> comparer)
                => table.Randomized ? Mix(comparer.GetHashCode(key)) : hash;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Equals(IEqualityComparer<TKey> comparer, TKey a, TKey b) => string.Equals(Unsafe.As<TKey, string>(ref a), Unsafe.As<TKey, string>(ref b));

            public static bool CanRandomize => true;
        }

        /// <summary>
        /// Mix a hash code into a 64 bit hash (fibonacci hashing), the high bits depends on all bits of the hash code.
        /// Bits 58-63 selects the segment, bits 50-57 is the tag and bits 18-49 selects the home block.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Mix(int hashCode) => unchecked((uint)hashCode * 0x9E3779B97F4A7C15UL);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ulong GetHash<TCmp>(TKey key) where TCmp : struct, IKeyComparer => TCmp.GetHash(Comparer, key);

        /// <summary>
        /// Get the hash used for the slots of a table, without a comparer strategy (for the slow paths)
        /// </summary>
        ulong GetSlotHash(TKey key, bool randomized)
        {
            if (UseDefaultComparer)
                return GetHash<DefaultKeyComparer>(key);
            if (UseFastStrings)
                return randomized ? Mix(Comparer.GetHashCode(key)) : GetHash<FastStringKeyComparer>(key);
            return GetHash<CustomKeyComparer>(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ref LowAllocDictionarySegment GetSegment(ulong h)
            // The index is always less than SegmentCount (no bounds check needed)
            => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Segments), GetSegmentIndex(h));

        static readonly bool KeyIsNullable = Nullable.GetUnderlyingType(typeof(TKey)) != null;

        /// <summary>
        /// Check if a key is null, without boxing value types (a "key == null" check boxes in unoptimized code)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsNull(TKey key) => typeof(TKey).IsValueType ? (KeyIsNullable && (key == null)) : (key == null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetSegmentIndex(ulong h) => (int)(h >> (64 - SegmentBits));

        /// <summary>
        /// 0 and 1 are reserved for empty and tombstone
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static byte GetKeyTag(ulong h)
        {
            byte tag = (byte)(h >> (56 - SegmentBits));
            return tag <= TagTombstone ? (byte)(tag + 2) : tag;
        }

        /// <summary>
        /// Map the hash to a block, works for any number of blocks (not just a power of two)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetHomeBlock(ulong h, int blocks) => (int)((((h >> 18) & 0xffffffffUL) * (uint)blocks) >> 32);

        #endregion//Hashing

        #region Storage

        struct Entry
        {
            public TKey Key;
            public TValue Value;
        }

        /// <summary>
        /// The storage of a segment, the tags and entries are always consistent with each other.
        /// A table is never resized, a new table is created and published instead.
        /// </summary>
        sealed class Table
        {
            public readonly byte[] Tags;
            /// <summary>
            /// Only valid where the tag is a live tag (may be uninitialized memory elsewhere)
            /// </summary>
            public readonly Entry[] Entries;
            /// <summary>
            /// Number of blocks of BlockSize slots
            /// </summary>
            public readonly int Blocks;
            /// <summary>
            /// When the number of used slots reaches this, the table must be rebuilt (7/8 load factor)
            /// </summary>
            public readonly int MaxUsed;
            /// <summary>
            /// True if the slots are selected using the randomized fallback hash (see <see cref="FastStringKeyComparer"/>)
            /// </summary>
            public readonly bool Randomized;
            /// <summary>
            /// Number of live entries (only modified while holding the segment lock)
            /// </summary>
            public int Live;
            /// <summary>
            /// Number of non empty slots, live + tombstones (only modified while holding the segment lock)
            /// </summary>
            public int Used;

            public Table(int blocks, bool randomized = false)
            {
                Randomized = randomized;
                var slots = blocks << BlockBits;
                Tags = new byte[slots];
                // No need to clear unmanaged memory, the tags tells what entries are valid
                Entries = GC.AllocateUninitializedArray<Entry>(slots);
                Blocks = blocks;
                MaxUsed = slots - (slots >> 3);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Table GetTable(ref LowAllocDictionarySegment seg) => Unsafe.As<Table>(Volatile.Read(ref seg.Table));

        /// <summary>
        /// Must hold the lock, creates and publishes a new table without any tombstones.
        /// The old table is never modified after this, so concurrent readers of it are safe.
        /// </summary>
        /// <param name="seg">The segment to rebuild</param>
        /// <param name="grow">True to force the table to grow, else it only grows if many entries are live</param>
        /// <param name="randomized">True to use the randomized fallback hash for the slots of the new table</param>
        /// <returns>The new table</returns>
        Table Rebuild(ref LowAllocDictionarySegment seg, bool grow, bool randomized)
        {
            var old = Unsafe.As<Table>(seg.Table);
            var oldSlots = old.Blocks << BlockBits;
            // Only grow if there are many live entries, else just get rid of the tombstones
            long newBlocks = (grow || (old.Live >= (oldSlots >> 1))) ? old.Blocks * 2L : old.Blocks;
            if (newBlocks > MaxBlocks)
            {
                if ((old.Blocks < MaxBlocks) || (!grow && (old.Live < old.MaxUsed)))
                    newBlocks = MaxBlocks;
                else
                    throw new InvalidOperationException("Dictionary segment is full");
            }
            var t = new Table((int)newBlocks, randomized);
            var newTags = t.Tags;
            var newEntries = t.Entries;
            var newSlots = newTags.Length;
            var blocks = t.Blocks;
            var oldTags = old.Tags;
            var oldEntries = old.Entries;
            int live = 0;
            for (int i = 0; i < oldSlots; i++)
            {
                if (oldTags[i] <= TagTombstone)
                    continue;
                ref var e = ref oldEntries[i];
                // The tag must be recomputed, since the slot hash changes if the table is randomized
                var h = GetSlotHash(e.Key, randomized);
                int idx = GetHomeBlock(h, blocks) << BlockBits;
                while (newTags[idx] != TagEmpty)
                {
                    if (++idx == newSlots)
                        idx = 0;
                }
                newTags[idx] = GetKeyTag(h);
                newEntries[idx] = e;
                ++live;
            }
            t.Live = live;
            t.Used = live;
            Volatile.Write(ref seg.Table, t);
            return t;
        }

        #endregion//Storage

        #region Probing

        /// <summary>
        /// Probing strategy, both strategies must use the same probe order.
        /// A key is always located before the first empty slot of its probe sequence,
        /// the probe sequence starts at the beginning of the home block and is linear (wrapping around).
        /// </summary>
        interface IProbe
        {
            /// <summary>
            /// Find the index of the key, -1 if not found.
            /// May be called without holding the lock (the result must then be validated).
            /// </summary>
            static abstract int Find<TCmp>(Table table, int homeBlock, byte tag, TKey key, IEqualityComparer<TKey> comparer) where TCmp : struct, IKeyComparer;

            /// <summary>
            /// Same as Find, but also measures the probe (to detect hash collision attacks).
            /// Must hold the lock.
            /// </summary>
            /// <param name="table">The table to search</param>
            /// <param name="homeBlock">The block where the probe sequence starts</param>
            /// <param name="tag">The tag of the key</param>
            /// <param name="key">The key to find</param>
            /// <param name="comparer">The comparer instance (null for the default comparer)</param>
            /// <param name="falseTagMatches">The number of slots with a matching tag but a different key</param>
            /// <param name="blocksScanned">The number of blocks scanned</param>
            static abstract int FindForInsert<TCmp>(Table table, int homeBlock, byte tag, TKey key, IEqualityComparer<TKey> comparer, out int falseTagMatches, out int blocksScanned) where TCmp : struct, IKeyComparer;

            /// <summary>
            /// Find the first free (empty or tombstone) slot, -1 if none exist.
            /// Must hold the lock.
            /// </summary>
            static abstract int FindFree(Table table, int homeBlock);
        }

        /// <summary>
        /// Checks the tags of a block (16 slots) at a time using SIMD (SSE2, AdvSimd etc)
        /// </summary>
        struct VectorProbe : IProbe
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int Find<TCmp>(Table table, int homeBlock, byte tag, TKey key, IEqualityComparer<TKey> comparer) where TCmp : struct, IKeyComparer
            {
                ref byte tags = ref MemoryMarshal.GetArrayDataReference(table.Tags);
                ref Entry entries = ref MemoryMarshal.GetArrayDataReference(table.Entries);
                int blocks = table.Blocks;
                int block = homeBlock;
                var tagVector = Vector128.Create(tag);
                for (int attempts = 0; attempts < blocks; ++attempts)
                {
                    int first = block << BlockBits;
                    var loaded = Vector128.LoadUnsafe(ref tags, (nuint)first);
                    uint matchMask = Vector128.Equals(loaded, tagVector).ExtractMostSignificantBits();
                    while (matchMask != 0)
                    {
                        int idx = first + BitOperations.TrailingZeroCount(matchMask);
                        var k = Unsafe.Add(ref entries, idx).Key;
                        // A concurrent remove may have cleared the key (lock free read), the read is retried in that case
                        if (!IsNull(k) && TCmp.Equals(comparer, k, key))
                            return idx;
                        matchMask &= matchMask - 1;
                    }
                    if (Vector128.Equals(loaded, Vector128<byte>.Zero).ExtractMostSignificantBits() != 0)
                        return -1;
                    if (++block == blocks)
                        block = 0;
                }
                return -1;
            }

            public static int FindForInsert<TCmp>(Table table, int homeBlock, byte tag, TKey key, IEqualityComparer<TKey> comparer, out int falseTagMatches, out int blocksScanned) where TCmp : struct, IKeyComparer
            {
                ref byte tags = ref MemoryMarshal.GetArrayDataReference(table.Tags);
                ref Entry entries = ref MemoryMarshal.GetArrayDataReference(table.Entries);
                int blocks = table.Blocks;
                int block = homeBlock;
                var tagVector = Vector128.Create(tag);
                int falseMatches = 0;
                int attempts = 0;
                int result = -1;
                while (attempts < blocks)
                {
                    ++attempts;
                    int first = block << BlockBits;
                    var loaded = Vector128.LoadUnsafe(ref tags, (nuint)first);
                    uint matchMask = Vector128.Equals(loaded, tagVector).ExtractMostSignificantBits();
                    while (matchMask != 0)
                    {
                        int idx = first + BitOperations.TrailingZeroCount(matchMask);
                        if (TCmp.Equals(comparer, Unsafe.Add(ref entries, idx).Key, key))
                        {
                            result = idx;
                            goto done;
                        }
                        ++falseMatches;
                        matchMask &= matchMask - 1;
                    }
                    if (Vector128.Equals(loaded, Vector128<byte>.Zero).ExtractMostSignificantBits() != 0)
                        break;
                    if (++block == blocks)
                        block = 0;
                }
            done:
                falseTagMatches = falseMatches;
                blocksScanned = attempts;
                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int FindFree(Table table, int homeBlock)
            {
                ref byte tags = ref MemoryMarshal.GetArrayDataReference(table.Tags);
                int blocks = table.Blocks;
                int block = homeBlock;
                var tombstone = Vector128.Create(TagTombstone);
                for (int attempts = 0; attempts < blocks; ++attempts)
                {
                    int first = block << BlockBits;
                    var loaded = Vector128.LoadUnsafe(ref tags, (nuint)first);
                    // tag <= 1 <=> min(tag, 1) == tag
                    uint freeMask = Vector128.Equals(Vector128.Min(loaded, tombstone), loaded).ExtractMostSignificantBits();
                    if (freeMask != 0)
                        return first + BitOperations.TrailingZeroCount(freeMask);
                    if (++block == blocks)
                        block = 0;
                }
                return -1;
            }
        }

        /// <summary>
        /// Checks one slot at a time (for platforms without SIMD)
        /// </summary>
        struct FallbackProbe : IProbe
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int Find<TCmp>(Table table, int homeBlock, byte tag, TKey key, IEqualityComparer<TKey> comparer) where TCmp : struct, IKeyComparer
            {
                var tags = table.Tags;
                var entries = table.Entries;
                int slots = tags.Length;
                int idx = homeBlock << BlockBits;
                for (int attempts = 0; attempts < slots; ++attempts)
                {
                    byte t = tags[idx];
                    if (t == TagEmpty)
                        return -1;
                    if (t == tag)
                    {
                        var k = entries[idx].Key;
                        // A concurrent remove may have cleared the key (lock free read), the read is retried in that case
                        if (!IsNull(k) && TCmp.Equals(comparer, k, key))
                            return idx;
                    }
                    if (++idx == slots)
                        idx = 0;
                }
                return -1;
            }

            public static int FindForInsert<TCmp>(Table table, int homeBlock, byte tag, TKey key, IEqualityComparer<TKey> comparer, out int falseTagMatches, out int blocksScanned) where TCmp : struct, IKeyComparer
            {
                var tags = table.Tags;
                var entries = table.Entries;
                int slots = tags.Length;
                int start = homeBlock << BlockBits;
                int idx = start;
                int falseMatches = 0;
                int scanned = 0;
                int result = -1;
                while (scanned < slots)
                {
                    ++scanned;
                    byte t = tags[idx];
                    if (t == TagEmpty)
                        break;
                    if (t == tag)
                    {
                        if (TCmp.Equals(comparer, entries[idx].Key, key))
                        {
                            result = idx;
                            break;
                        }
                        ++falseMatches;
                    }
                    if (++idx == slots)
                        idx = 0;
                }
                falseTagMatches = falseMatches;
                blocksScanned = (scanned + BlockSize - 1) >> BlockBits;
                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int FindFree(Table table, int homeBlock)
            {
                var tags = table.Tags;
                int slots = tags.Length;
                int idx = homeBlock << BlockBits;
                for (int attempts = 0; attempts < slots; ++attempts)
                {
                    if (tags[idx] <= TagTombstone)
                        return idx;
                    if (++idx == slots)
                        idx = 0;
                }
                return -1;
            }
        }

        #endregion//Probing

        #region Operations

        /// <summary>
        /// Add a key
        /// </summary>
        /// <param name="key">The key to add</param>
        /// <param name="value">The value to add</param>
        /// <param name="overwrite">If true, an existing value is overwritten (and true is returned)</param>
        /// <returns>True if the value was added (or overwritten), false if the key exists (and overwrite is false)</returns>
        bool AddCore<TProbe, TCmp>(TKey key, TValue value, bool overwrite) where TProbe : struct, IProbe where TCmp : struct, IKeyComparer
        {
            var comparer = Comparer;
            var h = GetHash<TCmp>(key);
            ref var seg = ref GetSegment(h);

            seg.EnterLock();
            try
            {
                var table = Unsafe.As<Table>(seg.Table);
                var th = TCmp.GetTableHash(table, h, key, comparer);
                int idx = TProbe.FindForInsert<TCmp>(table, GetHomeBlock(th, table.Blocks), GetKeyTag(th), key, comparer, out var falseTagMatches, out var blocksScanned);
                if (idx >= 0)
                {
                    if (!overwrite)
                        return false;
                    seg.BeginWrite();
                    table.Entries[idx].Value = value;
                    seg.EndWrite();
                    return true;
                }
                if (TCmp.CanRandomize && !table.Randomized && ((falseTagMatches >= MaxFalseTagMatches) || (blocksScanned >= MaxProbeBlocks)))
                {
                    // Hash collisions (an attack?), switch the table to the randomized hash
                    table = Rebuild(ref seg, false, true);
                    th = TCmp.GetTableHash(table, h, key, comparer);
                }
                else if (table.Used >= table.MaxUsed)
                {
                    table = Rebuild(ref seg, false, table.Randomized);
                }
                for (; ; )
                {
                    idx = TProbe.FindFree(table, GetHomeBlock(th, table.Blocks));
                    if (idx >= 0)
                        break;
                    table = Rebuild(ref seg, true, table.Randomized);
                }
                seg.BeginWrite();
                ref var e = ref table.Entries[idx];
                e.Key = key;
                e.Value = value;
                ref var t = ref table.Tags[idx];
                if (t == TagEmpty)
                    ++table.Used;
                t = GetKeyTag(th);
                ++table.Live;
                seg.EndWrite();
                return true;
            }
            finally
            {
                seg.ExitLock();
            }
        }

        /// <summary>
        /// The lookup fast path, a single lock free attempt (the common case), retries are done in <see cref="TryGetValueSlow"/>.
        /// Kept small, so that the JIT doesn't need to save and spill many registers.
        /// </summary>
        bool TryGetValueCore<TProbe, TCmp>(TKey key, [MaybeNullWhen(false)] out TValue value) where TProbe : struct, IProbe where TCmp : struct, IKeyComparer
        {
            var h = GetHash<TCmp>(key);
            ref var seg = ref GetSegment(h);
            int version = Volatile.Read(ref seg.Version);
            var table = GetTable(ref seg);
            var comparer = Comparer;
            var th = TCmp.GetTableHash(table, h, key, comparer);
            // The table is always consistent (never torn), so it's safe to search even if a write is in progress (the result is then discarded)
            int idx = TProbe.Find<TCmp>(table, GetHomeBlock(th, table.Blocks), GetKeyTag(th), key, comparer);
            value = idx >= 0 ? Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(table.Entries), idx).Value : default;
            // Make sure that all reads above are done before validating the version
            Volatile.ReadBarrier();
            if (((version & 1) == 0) && (Volatile.Read(ref seg.Version) == version))
                return idx >= 0;
            return TryGetValueSlow<TProbe, TCmp>(ref seg, h, key, out value);
        }

        /// <summary>
        /// A write happened during the lock free read, retry (and eventually read under the lock)
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        bool TryGetValueSlow<TProbe, TCmp>(ref LowAllocDictionarySegment seg, ulong h, TKey key, [MaybeNullWhen(false)] out TValue value) where TProbe : struct, IProbe where TCmp : struct, IKeyComparer
        {
            var comparer = Comparer;
            for (int attempt = 0; attempt < MaxOptimisticReads; ++attempt)
            {
                Thread.SpinWait(attempt + 1);
                int version = Volatile.Read(ref seg.Version);
                if ((version & 1) == 0)
                {
                    var table = GetTable(ref seg);
                    var th = TCmp.GetTableHash(table, h, key, comparer);
                    int idx = TProbe.Find<TCmp>(table, GetHomeBlock(th, table.Blocks), GetKeyTag(th), key, comparer);
                    value = idx >= 0 ? table.Entries[idx].Value : default;
                    Volatile.ReadBarrier();
                    if (Volatile.Read(ref seg.Version) == version)
                        return idx >= 0;
                }
            }
            // Heavy write contention, read while holding the lock
            seg.EnterLock();
            try
            {
                var table = Unsafe.As<Table>(seg.Table);
                var th = TCmp.GetTableHash(table, h, key, comparer);
                int idx = TProbe.Find<TCmp>(table, GetHomeBlock(th, table.Blocks), GetKeyTag(th), key, comparer);
                value = idx >= 0 ? table.Entries[idx].Value : default;
                return idx >= 0;
            }
            finally
            {
                seg.ExitLock();
            }
        }

        /// <summary>
        /// Remove a key
        /// </summary>
        /// <param name="key">The key to remove</param>
        /// <param name="value">The value of the removed key</param>
        /// <param name="matchValue">If true, the key is only removed if the current value equals the expected value</param>
        /// <param name="expected">The expected value (if matchValue is true)</param>
        /// <returns>True if the key was removed</returns>
        bool RemoveCore<TProbe, TCmp>(TKey key, [MaybeNullWhen(false)] out TValue value, bool matchValue, TValue expected) where TProbe : struct, IProbe where TCmp : struct, IKeyComparer
        {
            var comparer = Comparer;
            var h = GetHash<TCmp>(key);
            ref var seg = ref GetSegment(h);

            seg.EnterLock();
            try
            {
                var table = Unsafe.As<Table>(seg.Table);
                var th = TCmp.GetTableHash(table, h, key, comparer);
                int idx = TProbe.Find<TCmp>(table, GetHomeBlock(th, table.Blocks), GetKeyTag(th), key, comparer);
                if (idx < 0)
                {
                    value = default;
                    return false;
                }
                ref var e = ref table.Entries[idx];
                if (matchValue && !EqualityComparer<TValue>.Default.Equals(e.Value, expected))
                {
                    value = default;
                    return false;
                }
                seg.BeginWrite();
                value = e.Value;
                table.Tags[idx] = TagTombstone;
                // Release the references, so that the key and value can be collected
                if (RuntimeHelpers.IsReferenceOrContainsReferences<Entry>())
                    e = default;
                --table.Live;
                seg.EndWrite();
                return true;
            }
            finally
            {
                seg.ExitLock();
            }
        }
        /// <summary>
        /// Read a slot of a table, the result is consistent (not torn) even if the slot is modified concurrently
        /// </summary>
        static bool TryReadSlot(ref LowAllocDictionarySegment seg, Table table, int idx, out KeyValuePair<TKey, TValue> pair)
        {
            for (int attempt = 0; attempt < MaxOptimisticReads; ++attempt)
            {
                int version = Volatile.Read(ref seg.Version);
                if ((version & 1) == 0)
                {
                    bool live = table.Tags[idx] > TagTombstone;
                    var e = table.Entries[idx];
                    Volatile.ReadBarrier();
                    if (Volatile.Read(ref seg.Version) == version)
                    {
                        pair = live ? new KeyValuePair<TKey, TValue>(e.Key, e.Value) : default;
                        return live;
                    }
                }
                Thread.SpinWait(attempt + 1);
            }
            // The table is either the current one (modified under the lock) or an old one (never modified)
            seg.EnterLock();
            try
            {
                if (table.Tags[idx] > TagTombstone)
                {
                    var e = table.Entries[idx];
                    pair = new KeyValuePair<TKey, TValue>(e.Key, e.Value);
                    return true;
                }
            }
            finally
            {
                seg.ExitLock();
            }
            pair = default;
            return false;
        }

        #endregion//Operations

        #region Alternate lookup

        /// <summary>
        /// Get a lookup that uses an alternate key type, like ReadOnlySpan&lt;char&gt; for string keys (lookups without allocating a string).
        /// The comparer of the dictionary must implement IAlternateEqualityComparer&lt;TAlternate, TKey&gt; (StringComparer.Ordinal and the default string comparer does for ReadOnlySpan&lt;char&gt;).
        /// </summary>
        /// <typeparam name="TAlternate">The alternate key type</typeparam>
        /// <returns>The lookup</returns>
        /// <exception cref="InvalidOperationException">The comparer doesn't support the alternate key type</exception>
        public AlternateLookup<TAlternate> GetAlternateLookup<TAlternate>() where TAlternate : notnull, allows ref struct
        {
            if (!TryGetAlternateLookup<TAlternate>(out var lookup))
                throw new InvalidOperationException("The comparer of the dictionary doesn't support lookups using the key type " + typeof(TAlternate).Name);
            return lookup;
        }

        /// <summary>
        /// Try to get a lookup that uses an alternate key type, like ReadOnlySpan&lt;char&gt; for string keys (lookups without allocating a string).
        /// The comparer of the dictionary must implement IAlternateEqualityComparer&lt;TAlternate, TKey&gt;.
        /// </summary>
        /// <typeparam name="TAlternate">The alternate key type</typeparam>
        /// <param name="lookup">The lookup</param>
        /// <returns>True if the comparer supports the alternate key type</returns>
        public bool TryGetAlternateLookup<TAlternate>(out AlternateLookup<TAlternate> lookup) where TAlternate : notnull, allows ref struct
        {
            if (Comparer is IAlternateEqualityComparer<TAlternate, TKey> c)
            {
                // The fast string hash is only available for ReadOnlySpan<char>
                if (!UseFastStrings || (typeof(TAlternate) == typeof(ReadOnlySpan<char>)))
                {
                    lookup = new AlternateLookup<TAlternate>(this, c);
                    return true;
                }
            }
            lookup = default;
            return false;
        }

        /// <summary>
        /// Lookups using an alternate key type (see <see cref="GetAlternateLookup{TAlternate}"/>)
        /// </summary>
        /// <typeparam name="TAlternate">The alternate key type</typeparam>
        public readonly struct AlternateLookup<TAlternate> where TAlternate : notnull, allows ref struct
        {
            internal AlternateLookup(LowAllocConcurrentDictionary<TKey, TValue> dictionary, IAlternateEqualityComparer<TAlternate, TKey> comparer)
            {
                Dictionary = dictionary;
                AltComparer = comparer;
            }

            readonly IAlternateEqualityComparer<TAlternate, TKey> AltComparer;

            /// <summary>
            /// The dictionary
            /// </summary>
            public LowAllocConcurrentDictionary<TKey, TValue> Dictionary { get; }

            /// <summary>
            /// Get the value of a key
            /// </summary>
            public bool TryGetValue(TAlternate key, [MaybeNullWhen(false)] out TValue value) => Dictionary.TryGetValueAlternate(key, AltComparer, out _, out value);

            /// <summary>
            /// Get the value of a key, and the actual key stored in the dictionary
            /// </summary>
            public bool TryGetValue(TAlternate key, [MaybeNullWhen(false)] out TKey actualKey, [MaybeNullWhen(false)] out TValue value) => Dictionary.TryGetValueAlternate(key, AltComparer, out actualKey, out value);

            public bool ContainsKey(TAlternate key) => Dictionary.TryGetValueAlternate(key, AltComparer, out _, out _);

            /// <summary>
            /// Get or set a value (setting creates a key from the alternate key if needed)
            /// </summary>
            public TValue this[TAlternate key]
            {
                get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
                set => Dictionary[AltComparer.Create(key)] = value;
            }

            /// <summary>
            /// Add a key (created from the alternate key) if it doesn't exist
            /// </summary>
            /// <returns>True if the key was added</returns>
            public bool TryAdd(TAlternate key, TValue value)
            {
                if (ContainsKey(key))
                    return false;
                return Dictionary.TryAdd(AltComparer.Create(key), value);
            }

            /// <summary>
            /// Remove a key
            /// </summary>
            /// <returns>True if the key was removed</returns>
            public bool TryRemove(TAlternate key, [MaybeNullWhen(false)] out TValue value)
            {
                // Find the actual key and remove it (retry if it was removed or replaced concurrently)
                while (Dictionary.TryGetValueAlternate(key, AltComparer, out var actualKey, out _))
                {
                    if (Dictionary.TryRemove(actualKey, out value))
                        return true;
                }
                value = default;
                return false;
            }
        }

        /// <summary>
        /// The hash of an alternate key (the same as for the key it represents)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ulong GetAlternateHash<TAlternate>(TAlternate key, IAlternateEqualityComparer<TAlternate, TKey> comparer) where TAlternate : notnull, allows ref struct
        {
            // Fast strings are only used with ReadOnlySpan<char> (see TryGetAlternateLookup)
            if (UseFastStrings)
                return LowAllocStringHash.GetHash(Unsafe.As<TAlternate, ReadOnlySpan<char>>(ref key));
            return Mix(comparer.GetHashCode(key));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool AlternateEquals<TAlternate>(IAlternateEqualityComparer<TAlternate, TKey> comparer, TAlternate key, TKey k) where TAlternate : notnull, allows ref struct
        {
            if (UseFastStrings)
            {
                ReadOnlySpan<char> a = Unsafe.As<TAlternate, ReadOnlySpan<char>>(ref key);
                ReadOnlySpan<char> b = Unsafe.As<TKey, string>(ref k);
                return a.SequenceEqual(b);
            }
            return comparer.Equals(key, k);
        }

        /// <summary>
        /// Find an alternate key (same probe sequence as <see cref="VectorProbe.Find"/> / <see cref="FallbackProbe.Find"/>), -1 if not found
        /// </summary>
        int FindAlternate<TAlternate>(Table table, int homeBlock, byte tag, TAlternate key, IAlternateEqualityComparer<TAlternate, TKey> comparer) where TAlternate : notnull, allows ref struct
        {
            ref byte tags = ref MemoryMarshal.GetArrayDataReference(table.Tags);
            ref Entry entries = ref MemoryMarshal.GetArrayDataReference(table.Entries);
            int blocks = table.Blocks;
            if (Vector128.IsHardwareAccelerated)
            {
                int block = homeBlock;
                var tagVector = Vector128.Create(tag);
                for (int attempts = 0; attempts < blocks; ++attempts)
                {
                    int first = block << BlockBits;
                    var loaded = Vector128.LoadUnsafe(ref tags, (nuint)first);
                    uint matchMask = Vector128.Equals(loaded, tagVector).ExtractMostSignificantBits();
                    while (matchMask != 0)
                    {
                        int idx = first + BitOperations.TrailingZeroCount(matchMask);
                        var k = Unsafe.Add(ref entries, idx).Key;
                        // A concurrent remove may have cleared the key (lock free read), the read is retried in that case
                        if (!IsNull(k) && AlternateEquals(comparer, key, k))
                            return idx;
                        matchMask &= matchMask - 1;
                    }
                    if (Vector128.Equals(loaded, Vector128<byte>.Zero).ExtractMostSignificantBits() != 0)
                        return -1;
                    if (++block == blocks)
                        block = 0;
                }
                return -1;
            }
            int slots = blocks << BlockBits;
            int i = homeBlock << BlockBits;
            for (int attempts = 0; attempts < slots; ++attempts)
            {
                byte t = Unsafe.Add(ref tags, i);
                if (t == TagEmpty)
                    return -1;
                if (t == tag)
                {
                    var k = Unsafe.Add(ref entries, i).Key;
                    if (!IsNull(k) && AlternateEquals(comparer, key, k))
                        return i;
                }
                if (++i == slots)
                    i = 0;
            }
            return -1;
        }

        /// <summary>
        /// Lookup using an alternate key, lock free (validated using the segment version, like <see cref="TryGetValueCore"/>)
        /// </summary>
        bool TryGetValueAlternate<TAlternate>(TAlternate key, IAlternateEqualityComparer<TAlternate, TKey> comparer, [MaybeNullWhen(false)] out TKey actualKey, [MaybeNullWhen(false)] out TValue value) where TAlternate : notnull, allows ref struct
        {
            var h = GetAlternateHash(key, comparer);
            ref var seg = ref GetSegment(h);
            for (int attempt = 0; attempt < MaxOptimisticReads; ++attempt)
            {
                if (attempt > 0)
                    Thread.SpinWait(attempt);
                int version = Volatile.Read(ref seg.Version);
                if ((version & 1) != 0)
                    continue;
                var table = GetTable(ref seg);
                // Randomized tables use the comparer hash for the slots (alternate comparers gives the same hash as for the key)
                var th = table.Randomized ? Mix(comparer.GetHashCode(key)) : h;
                int idx = FindAlternate(table, GetHomeBlock(th, table.Blocks), GetKeyTag(th), key, comparer);
                var e = idx >= 0 ? Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(table.Entries), idx) : default;
                // Make sure that all reads above are done before validating the version
                Volatile.ReadBarrier();
                if (Volatile.Read(ref seg.Version) == version)
                {
                    actualKey = e.Key;
                    value = e.Value;
                    return idx >= 0;
                }
            }
            // Heavy write contention, read while holding the lock
            seg.EnterLock();
            try
            {
                var table = Unsafe.As<Table>(seg.Table);
                var th = table.Randomized ? Mix(comparer.GetHashCode(key)) : h;
                int idx = FindAlternate(table, GetHomeBlock(th, table.Blocks), GetKeyTag(th), key, comparer);
                var e = idx >= 0 ? table.Entries[idx] : default;
                actualKey = e.Key;
                value = e.Value;
                return idx >= 0;
            }
            finally
            {
                seg.ExitLock();
            }
        }

        #endregion//Alternate lookup

        public void Clear()
        {
            var clearEntries = RuntimeHelpers.IsReferenceOrContainsReferences<Entry>();
            var segs = Segments;
            for (int s = 0; s < SegmentCount; s++)
            {
                ref var seg = ref segs[s];
                seg.EnterLock();
                try
                {
                    var table = Unsafe.As<Table>(seg.Table);
                    if (table.Used <= 0)
                        continue;
                    seg.BeginWrite();
                    table.Tags.AsSpan().Clear();
                    // Release the references, so that the keys and values can be collected
                    if (clearEntries)
                        table.Entries.AsSpan().Clear();
                    table.Live = 0;
                    table.Used = 0;
                    seg.EndWrite();
                }
                finally
                {
                    seg.ExitLock();
                }
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
        public bool Contains(KeyValuePair<TKey, TValue> item) => TryGetValue(item.Key, out var val) && EqualityComparer<TValue>.Default.Equals(val, item.Value);

        /// <summary>
        /// Remove the key, only if the value matches (atomically)
        /// </summary>
        public bool Remove(KeyValuePair<TKey, TValue> item)
            => Remove(item.Key, out _, true, item.Value);

        public List<KeyValuePair<TKey, TValue>> ToList()
        {
            // Don't use the List constructor, it would call Count and CopyTo (that uses ToList)
            var list = new List<KeyValuePair<TKey, TValue>>(Count);
            foreach (var p in this)
                list.Add(p);
            return list;
        }

        public KeyValuePair<TKey, TValue>[] ToArray()
            => ToList().ToArray();

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
            // Take a snapshot, the number of items may change while copying
            var items = ToList();
            if (items.Count > array.Length - arrayIndex)
                throw new ArgumentException("The destination array is too small", nameof(array));
            items.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Enumerates the items without allocating any memory (when used with foreach on the dictionary type).
        /// The enumeration is weakly consistent (like ConcurrentDictionary):
        /// Items that exist during the whole enumeration are returned exactly once.
        /// Items added or removed during the enumeration may or may not be returned, a key that is removed and added again during the enumeration may be returned twice.
        /// A returned key value pair is never torn.
        /// </summary>
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            readonly LowAllocConcurrentDictionary<TKey, TValue> Dict;
            int SegmentIdx;
            int SlotIdx;
            Table CurrentTable;
            KeyValuePair<TKey, TValue> C;

            internal Enumerator(LowAllocConcurrentDictionary<TKey, TValue> dict)
            {
                Dict = dict;
                SegmentIdx = 0;
                SlotIdx = -1;
                CurrentTable = null;
                C = default;
            }

            public bool MoveNext()
            {
                var segs = Dict.Segments;
                for (; ; )
                {
                    var table = CurrentTable;
                    if (table == null)
                    {
                        if (SegmentIdx >= SegmentCount)
                            return false;
                        // A table is either the current one or an old one (never modified), so items never move while enumerating it
                        table = GetTable(ref segs[SegmentIdx]);
                        CurrentTable = table;
                        SlotIdx = -1;
                    }
                    var tags = table.Tags;
                    var idx = SlotIdx;
                    while (++idx < tags.Length)
                    {
                        if (tags[idx] <= TagTombstone)
                            continue;
                        if (TryReadSlot(ref segs[SegmentIdx], table, idx, out C))
                        {
                            SlotIdx = idx;
                            return true;
                        }
                    }
                    CurrentTable = null;
                    ++SegmentIdx;
                }
            }

            public readonly KeyValuePair<TKey, TValue> Current => C;
            readonly object IEnumerator.Current => C;

            public void Reset()
            {
                SegmentIdx = 0;
                SlotIdx = -1;
                CurrentTable = null;
                C = default;
            }

            public readonly void Dispose() { }
        }

        /// <summary>
        /// Get an enumerator (a struct, so foreach on the dictionary type doesn't allocate)
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        List<TKey> GetKeysCollection()
        {
            var list = new List<TKey>(Count);
            foreach (var p in this) list.Add(p.Key);
            return list;
        }

        List<TValue> GetValuesCollection()
        {
            var list = new List<TValue>(Count);
            foreach (var p in this) list.Add(p.Value);
            return list;
        }

    }


    /// <summary>
    /// A fast (non cryptographic) ordinal string hash, randomly seeded per process (wyhash style, 128 bit multiply mixing).
    /// The secret seed is mixed into every multiplication, so that inputs that zero a multiplier can't be crafted without knowing it.
    /// A seed alone doesn't guarantee that collisions can't be found, so dictionaries using it must detect collisions and fall back to a randomized hash.
    /// </summary>
    static class LowAllocStringHash
    {
        static readonly ulong S0;
        static readonly ulong S1;
        static readonly ulong S2;

        static LowAllocStringHash()
        {
            Span<ulong> s = stackalloc ulong[3];
            System.Security.Cryptography.RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(s));
            S0 = s[0];
            S1 = s[1];
            S2 = s[2];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Mum(ulong a, ulong b)
        {
            ulong hi = Math.BigMul(a, b, out ulong lo);
            return hi ^ lo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Read64(ref byte p, nuint offset) => Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref p, offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Read32(ref byte p, nuint offset) => Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref p, offset));

        /// <summary>
        /// Get a 64 bit hash of the string (all bits are well mixed)
        /// </summary>
        /// <param name="s">The string to hash (not null)</param>
        /// <returns>A 64 bit hash, only valid during the life time of the process</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetHash(string s) => GetHash(s.AsSpan());

        /// <summary>
        /// Get a 64 bit hash of the chars (all bits are well mixed), the same as for a string with the same chars
        /// </summary>
        /// <param name="s">The chars to hash</param>
        /// <returns>A 64 bit hash, only valid during the life time of the process</returns>
        public static ulong GetHash(ReadOnlySpan<char> s)
        {
            ref byte p = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(s));
            nuint len = (nuint)s.Length << 1;
            ulong h = S0 ^ len;
            ulong a, b;
            if (len <= 16)
            {
                if (len >= 8)
                {
                    a = Read64(ref p, 0);
                    b = Read64(ref p, len - 8);
                }
                else if (len >= 4)
                {
                    a = Read32(ref p, 0);
                    b = Read32(ref p, len - 4);
                }
                else
                {
                    // 0 or 1 char
                    a = len > 0 ? Unsafe.ReadUnaligned<ushort>(ref p) : 0UL;
                    b = 0;
                }
            }
            else
            {
                nuint i = 0;
                // Two independent lanes for 32 bytes at a time (more instruction level parallelism)
                if (len > 32)
                {
                    ulong h2 = h ^ S2;
                    do
                    {
                        h = Mum(Read64(ref p, i) ^ S1, Read64(ref p, i + 8) ^ h);
                        h2 = Mum(Read64(ref p, i + 16) ^ S2, Read64(ref p, i + 24) ^ h2);
                        i += 32;
                    }
                    while (len - i > 32);
                    h ^= h2;
                }
                if (len - i > 16)
                {
                    h = Mum(Read64(ref p, i) ^ S1, Read64(ref p, i + 8) ^ h);
                }
                // The last 16 bytes (may overlap with already processed bytes)
                a = Read64(ref p, len - 16);
                b = Read64(ref p, len - 8);
            }
            return Mum(S2 ^ len, Mum(a ^ S1, b ^ h));
        }
    }


    /// <summary>
    /// A segment of a LowAllocConcurrentDictionary (not generic, since generic types can't have an explicit layout).
    /// Each segment occupies 128 bytes, with the hot fields in the middle, so that different segments never share a cache line (no false sharing).
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    struct LowAllocDictionarySegment
    {
        /// <summary>
        /// The current table, replaced (never modified) when growing
        /// </summary>
        [FieldOffset(64)]
        public object Table;
        /// <summary>
        /// Incremented before and after every modification of the current table, odd while a modification is in progress
        /// </summary>
        [FieldOffset(72)]
        public int Version;
        [FieldOffset(76)]
        int LockState;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterLock()
        {
            if (Interlocked.CompareExchange(ref LockState, 1, 0) == 0) return;

            int spinCount = 1;
            while (Interlocked.CompareExchange(ref LockState, 1, 0) != 0)
            {
                Thread.SpinWait(spinCount);
                if (spinCount < 64) spinCount <<= 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitLock()
        {
            Volatile.Write(ref LockState, 0);
        }

        /// <summary>
        /// Must hold the lock, makes concurrent readers retry
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginWrite()
        {
            // Only one writer (holding the lock), so no atomic increment is needed
            Volatile.Write(ref Version, Version + 1);
            // The odd version must be visible before any of the following writes
            Volatile.WriteBarrier();
        }

        /// <summary>
        /// Must hold the lock
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndWrite()
        {
            // Release semantics, all previous writes are visible before the even version
            Volatile.Write(ref Version, Version + 1);
        }
    }

}
