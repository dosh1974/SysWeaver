using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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
    /// Writes also search for the key lock free before taking the lock (so that the cache misses don't happen while holding it),
    /// the result is used if the segment version is the same when the lock is taken.
    /// </summary>
    /// <remarks>
    /// Thread safe, all operations can be called concurrently.
    /// Null keys are not allowed (<see cref="ArgumentNullException"/>).
    /// Ordinal string keys (the default string comparer or <see cref="StringComparer.Ordinal"/>) use a fast, randomly seeded hash,
    /// a segment that sees many collisions switches to the randomized ordinal hash (hash flooding protection).
    /// The segment locks are spin locks, so keep comparers and hash functions cheap.
    /// <see cref="Keys"/>, <see cref="Values"/>, <see cref="ToList"/> and <see cref="CopyTo"/> return snapshots (new collections), enumeration is weakly consistent (see <see cref="Enumerator"/>).
    /// Removed entries leave tombstones that are cleaned up when a segment is rebuilt (memory is not returned until then).
    /// </remarks>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    public sealed class LowAllocConcurrentDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        const int SegmentBits = 6;
        const int SegmentCount = LowAllocDictionarySegments.Count;
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

        /// <summary>
        /// The segments are stored inline (no array, so no bounds or null checks when selecting a segment)
        /// </summary>
        LowAllocDictionarySegments Segments;
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
        /// <remarks>
        /// All segment tables are allocated up front (64 segments of at least 16 slots each, even for a small capacity).
        /// </remarks>
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
            // Room for capacity items in total without growing (taking the max load factor into account)
            var perSegment = (Math.Max(0L, capacity) + SegmentCount - 1) / SegmentCount;
            // Keys are randomly distributed between segments, for large capacities add 4 standard deviations of headroom (~1% at 10M items),
            // else about half the segments would have to grow (doubling their size)
            Debug.Assert(SegmentCount == 1 << SegmentBits);
            if (perSegment >= 1024)
                perSegment += 4 * (long)Math.Sqrt(perSegment);
            var slots = perSegment * 8 / 7 + 1;
            var blocks = (int)Math.Clamp((slots + BlockSize - 1) >> BlockBits, 1, MaxBlocks);
            for (int i = 0; i < SegmentCount; i++)
                GetSegmentAt(i).Table = new Table(blocks);
        }

        /// <summary>
        /// Create a new dictionary with room for 1024 items before growing
        /// </summary>
        /// <param name="comparer">The key comparer to use, null to use the default comparer</param>
        public LowAllocConcurrentDictionary(IEqualityComparer<TKey> comparer)
             : this(1024, comparer)
        {
        }


        /// <summary>
        /// Add a new key
        /// </summary>
        /// <param name="key">The key, must not be null</param>
        /// <param name="value">The value</param>
        /// <exception cref="ArgumentException">Thrown if the key already exists</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
        public void Add(TKey key, TValue value)
        {
            if (!TryAdd(key, value))
                throw new ArgumentException("An item with the same key has already been added", nameof(key));
        }

        /// <summary>
        /// Check if a key exists (lock free)
        /// </summary>
        /// <param name="key">The key, must not be null</param>
        /// <returns>True if the key exists</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
        public bool ContainsKey(TKey key) => TryGetValue(key, out _);

        /// <summary>
        /// Remove a key
        /// </summary>
        /// <param name="key">The key, must not be null</param>
        /// <returns>True if the key was removed</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
        public bool Remove(TKey key) => TryRemove(key, out _);

        /// <summary>
        /// Get or set the value of a key (setting adds the key or overwrites its value)
        /// </summary>
        /// <param name="key">The key, must not be null</param>
        /// <exception cref="KeyNotFoundException">Thrown by the getter if the key doesn't exist</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
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
                for (int s = 0; s < SegmentCount; s++)
                    total += Volatile.Read(ref GetSegmentAt(s).Live);
                return total;
            }
        }

        /// <summary>
        /// Always false
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// A snapshot of the keys (a new list on every call, modifying it doesn't affect the dictionary)
        /// </summary>
        public ICollection<TKey> Keys => GetKeysCollection();

        /// <summary>
        /// A snapshot of the values (a new list on every call, modifying it doesn't affect the dictionary)
        /// </summary>
        public ICollection<TValue> Values => GetValuesCollection();

        // The public operations select how keys are hashed and compared (see LowAllocKeyMode): the default comparer for value types (devirtualized and inlined),
        // fast ordinal strings or a comparer instance. The core operations are inlined with the mode as a constant, so that they are specialized without any runtime checks.
        // The mode isn't a generic type argument: all reference type keys share the same code, and calling (or inlining) generic methods
        // from shared code requires runtime lookups.

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

        /// <summary>
        /// Add a key if it doesn't exist
        /// </summary>
        /// <param name="key">The key, must not be null</param>
        /// <param name="value">The value</param>
        /// <returns>True if the key was added, false if it already existed (the value is not updated)</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
        /// <exception cref="InvalidOperationException">Thrown if a segment can't grow any further (2^30 slots)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(TKey key, TValue value)
        {
            if (IsNull(key)) ThrowKeyNull();
            if (UseDefaultComparer)
                return AddDefault(key, value, false);
            return AddOther(key, value, false);
        }

        /// <summary>
        /// Add a key, or update the value if the key already exists
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Set(TKey key, TValue value)
        {
            if (IsNull(key)) ThrowKeyNull();
            if (UseDefaultComparer)
                AddDefault(key, value, true);
            else
                AddOther(key, value, true);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        bool AddDefault(TKey key, TValue value, bool overwrite)
            => AddInline(key, value, overwrite, LowAllocKeyMode.Default);

        [MethodImpl(MethodImplOptions.NoInlining)]
        bool AddOther(TKey key, TValue value, bool overwrite)
            => UseFastStrings ? AddInline(key, value, overwrite, LowAllocKeyMode.FastString) : AddInline(key, value, overwrite, LowAllocKeyMode.Custom);

        /// <summary>
        /// Get the value of a key.
        /// Lock free (falls back to reading under the segment lock if the segment is modified continuously), doesn't allocate.
        /// </summary>
        /// <param name="key">The key, must not be null</param>
        /// <param name="value">The value, or default if the key doesn't exist</param>
        /// <returns>True if the key exists</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (IsNull(key)) ThrowKeyNull();
            if (UseDefaultComparer)
                return TryGetValueDefault(key, out value);
            return TryGetValueOther(key, out value);
        }

        /// <summary>
        /// Lookup for value type keys with the default comparer, inlined into the caller (the lookup fast path is small and has no calls, the slow paths are calls)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryGetValueDefault(TKey key, [MaybeNullWhen(false)] out TValue value)
            => TryGetValueInline(key, out value, LowAllocKeyMode.Default);

        [MethodImpl(MethodImplOptions.NoInlining)]
        bool TryGetValueOther(TKey key, [MaybeNullWhen(false)] out TValue value)
            => UseFastStrings ? TryGetValueInline(key, out value, LowAllocKeyMode.FastString) : TryGetValueInline(key, out value, LowAllocKeyMode.Custom);

        /// <summary>
        /// Remove a key (a missing key doesn't take the lock)
        /// </summary>
        /// <param name="key">The key, must not be null</param>
        /// <param name="value">The removed value, or default if the key didn't exist</param>
        /// <returns>True if the key was removed</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemove(TKey key, [MaybeNullWhen(false)] out TValue value)
            => Remove(key, out value, false, default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value, bool matchValue, TValue expected)
        {
            if (IsNull(key)) ThrowKeyNull();
            if (UseDefaultComparer)
                return RemoveDefault(key, out value, matchValue, expected);
            return RemoveOther(key, out value, matchValue, expected);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        bool RemoveDefault(TKey key, [MaybeNullWhen(false)] out TValue value, bool matchValue, TValue expected)
            => RemoveInline(key, out value, matchValue, expected, LowAllocKeyMode.Default);

        [MethodImpl(MethodImplOptions.NoInlining)]
        bool RemoveOther(TKey key, [MaybeNullWhen(false)] out TValue value, bool matchValue, TValue expected)
            => UseFastStrings ? RemoveInline(key, out value, matchValue, expected, LowAllocKeyMode.FastString) : RemoveInline(key, out value, matchValue, expected, LowAllocKeyMode.Custom);

        /// <summary>
        /// The key comparison part of a mode (without the flags)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static LowAllocKeyMode KeyMode(LowAllocKeyMode mode) => mode & LowAllocKeyMode.KeyMask;

        /// <summary>
        /// True if the tags are probed using SIMD
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool UseVector(LowAllocKeyMode mode) => Vector128.IsHardwareAccelerated && ((mode & LowAllocKeyMode.Scalar) == 0);

        [DoesNotReturn]
        static void ThrowKeyNull() => throw new ArgumentNullException("key");


        #region Hashing

        /// <summary>
        /// Mix a hash code into a 64 bit hash (fibonacci hashing), the high bits depends on all bits of the hash code.
        /// Bits 58-63 selects the segment, bits 50-57 is the tag and bits 0-49 selects the home block (mostly the high bits).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Mix(int hashCode) => unchecked((uint)hashCode * 0x9E3779B97F4A7C15UL);

        /// <summary>
        /// Get the 64 bit hash used for selecting the segment (and the slot, unless the table is randomized)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ulong GetHash(TKey key, LowAllocKeyMode mode)
        {
            if (KeyMode(mode) == LowAllocKeyMode.Default)
                return Mix(EqualityComparer<TKey>.Default.GetHashCode(key));
            if (KeyMode(mode) == LowAllocKeyMode.FastString)
                return LowAllocStringHash.GetHash(Unsafe.As<TKey, string>(ref key));
            return Mix(Comparer.GetHashCode(key));
        }

        /// <summary>
        /// Get the 64 bit hash used for selecting the slot in a table (the tag and home block)
        /// </summary>
        /// <param name="table">The table</param>
        /// <param name="hash">The hash from GetHash</param>
        /// <param name="key">The key</param>
        /// <param name="mode">How keys are hashed and compared</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ulong GetTableHash(Table table, ulong hash, TKey key, LowAllocKeyMode mode)
            => (KeyMode(mode) == LowAllocKeyMode.FastString) && table.Randomized ? Mix(Comparer.GetHashCode(key)) : hash;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool KeyEquals(TKey a, TKey b, LowAllocKeyMode mode)
        {
            if (KeyMode(mode) == LowAllocKeyMode.Default)
                return EqualityComparer<TKey>.Default.Equals(a, b);
            if (KeyMode(mode) == LowAllocKeyMode.FastString)
                return string.Equals(Unsafe.As<TKey, string>(ref a), Unsafe.As<TKey, string>(ref b));
            return Comparer.Equals(a, b);
        }

        /// <summary>
        /// Get the hash used for the slots of a table (for the slow paths)
        /// </summary>
        ulong GetSlotHash(TKey key, bool randomized)
        {
            if (UseDefaultComparer)
                return GetHash(key, LowAllocKeyMode.Default);
            if (UseFastStrings)
                return randomized ? Mix(Comparer.GetHashCode(key)) : GetHash(key, LowAllocKeyMode.FastString);
            return GetHash(key, LowAllocKeyMode.Custom);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ref LowAllocDictionarySegment GetSegment(ulong h)
            // The index is always less than SegmentCount (no bounds check needed)
            => ref Unsafe.Add(ref Unsafe.As<LowAllocDictionarySegments, LowAllocDictionarySegment>(ref Segments), (nuint)(h >> (64 - SegmentBits)));

        /// <summary>
        /// Get a segment by index (0 to SegmentCount - 1)
        /// </summary>
        ref LowAllocDictionarySegment GetSegmentAt(int index) => ref Segments[index];

        static readonly bool KeyIsNullable = Nullable.GetUnderlyingType(typeof(TKey)) != null;

        /// <summary>
        /// Check if a key is null, without boxing value types (a "key == null" check boxes in unoptimized code)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsNull(TKey key) => typeof(TKey).IsValueType ? (KeyIsNullable && (key == null)) : (key == null);

        /// <summary>
        /// The smallest tag of a key, 0 and 1 are reserved for empty and tombstone
        /// </summary>
        const byte MinKeyTag = TagTombstone + 1;

        /// <summary>
        /// Get the tag of a key (0, 1 and 2 are all mapped to 2, since 0 and 1 are reserved for empty and tombstone)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static byte GetKeyTag(ulong h) => Math.Max((byte)(h >> (56 - SegmentBits)), MinKeyTag);

        /// <summary>
        /// Get the tag of a key in all lanes (same as <see cref="GetKeyTag"/>)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector128<byte> GetKeyTagVector(ulong h) => Vector128.Max(Vector128.Create((byte)(h >> (56 - SegmentBits))), Vector128.Create(MinKeyTag));

        /// <summary>
        /// Map the hash to a block, works for any number of blocks (not just a power of two)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetHomeBlock(ulong h, int blocks) => (int)MultiplyHigh(h << (8 + SegmentBits), (uint)blocks);

        /// <summary>
        /// Get the first slot of the home block
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static nuint GetHomeSlot(ulong h, int blocks) => (nuint)MultiplyHigh(h << (8 + SegmentBits), (uint)blocks) << BlockBits;

        /// <summary>
        /// The high 64 bits of the 128 bit product
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong MultiplyHigh(ulong a, ulong b)
        {
            if (Bmi2.X64.IsSupported)
                return Bmi2.X64.MultiplyNoFlags(a, b);
            return Math.BigMul(a, b, out _);
        }

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
        /// The fields are never modified (the counters are in the segment), so that writers never invalidate the cache line that all readers of the segment use.
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
            /// True if the slots are selected using the randomized fallback hash (see <see cref="LowAllocKeyMode.FastString"/>)
            /// </summary>
            public readonly bool Randomized;

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
            long newBlocks = (grow || (seg.Live >= (oldSlots >> 1))) ? old.Blocks * 2L : old.Blocks;
            if (newBlocks > MaxBlocks)
            {
                if ((old.Blocks < MaxBlocks) || (!grow && (seg.Live < old.MaxUsed)))
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
            Volatile.Write(ref seg.Table, t);
            seg.Live = live;
            seg.Used = live;
            return t;
        }

        #endregion//Storage

        #region Probing

        // The probe sequence of a key starts at the beginning of its home block and is linear (wrapping around),
        // a key is always located before the first empty slot of its probe sequence.
        // With SIMD (SSE2, AdvSimd etc) the tags of a block (16 slots) are checked at a time, else one slot at a time (same probe order).
        // A table always has empty slots (the max load factor is 7/8), so a probe always ends at an empty slot (but the probes are bounded anyway).

        /// <summary>
        /// Check if a key read from a slot (with a matching tag) is the key.
        /// May be called without holding the lock.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsKey(TKey slotKey, TKey key, LowAllocKeyMode mode)
        {
            // A concurrent remove may have cleared the key (lock free read), the read is then retried.
            // The default comparer and string.Equals handles null, a comparer instance may not
            if ((KeyMode(mode) == LowAllocKeyMode.Custom) && IsNull(slotKey))
                return false;
            return KeyEquals(slotKey, key, mode);
        }

        /// <summary>
        /// Find the index of the key, -1 if not found.
        /// May be called without holding the lock (the result must then be validated).
        /// The home block is checked inline (where almost all keys are), the rest of the probe sequence in <see cref="FindInNextBlocks"/>.
        /// </summary>
        /// <param name="table">The table to search</param>
        /// <param name="th">The table hash of the key</param>
        /// <param name="key">The key to find</param>
        /// <param name="mode">How keys are hashed and compared</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int Find(Table table, ulong th, TKey key, LowAllocKeyMode mode)
        {
            if (!UseVector(mode))
                return FindFallback(table, th, key, mode);
            nuint first = GetHomeSlot(th, table.Blocks);
            var loaded = Vector128.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(table.Tags), first);
            uint matches = Vector128.Equals(loaded, GetKeyTagVector(th)).ExtractMostSignificantBits();
            while (matches != 0)
            {
                nuint idx = first + (uint)BitOperations.TrailingZeroCount(matches);
                if (IsKey(Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(table.Entries), idx).Key, key, mode))
                    return (int)idx;
                matches &= matches - 1;
            }
            if (Vector128.Equals(loaded, Vector128<byte>.Zero).ExtractMostSignificantBits() != 0)
                return -1;
            return FindInNextBlocks(table, (int)first, th, key, mode);
        }

        /// <summary>
        /// Continue a probe after the home block (rare)
        /// </summary>
        /// <param name="table">The table to search</param>
        /// <param name="first">The first slot of the home block</param>
        /// <param name="th">The table hash of the key</param>
        /// <param name="key">The key to find</param>
        /// <param name="mode">How keys are hashed and compared</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        int FindInNextBlocks(Table table, int first, ulong th, TKey key, LowAllocKeyMode mode)
        {
            ref byte tags = ref MemoryMarshal.GetArrayDataReference(table.Tags);
            ref Entry entries = ref MemoryMarshal.GetArrayDataReference(table.Entries);
            int slots = table.Tags.Length;
            var tagVector = GetKeyTagVector(th);
            for (int remaining = table.Blocks - 1; remaining > 0; --remaining)
            {
                first += BlockSize;
                if (first == slots)
                    first = 0;
                var loaded = Vector128.LoadUnsafe(ref tags, (nuint)first);
                uint matches = Vector128.Equals(loaded, tagVector).ExtractMostSignificantBits();
                while (matches != 0)
                {
                    int idx = first + BitOperations.TrailingZeroCount(matches);
                    if (IsKey(Unsafe.Add(ref entries, idx).Key, key, mode))
                        return idx;
                    matches &= matches - 1;
                }
                if (Vector128.Equals(loaded, Vector128<byte>.Zero).ExtractMostSignificantBits() != 0)
                    return -1;
            }
            return -1;
        }

        /// <summary>
        /// Same as <see cref="Find"/>, for platforms without SIMD
        /// </summary>
        int FindFallback(Table table, ulong th, TKey key, LowAllocKeyMode mode)
        {
            var tags = table.Tags;
            var entries = table.Entries;
            int slots = tags.Length;
            byte tag = GetKeyTag(th);
            int idx = GetHomeBlock(th, table.Blocks) << BlockBits;
            for (int attempts = 0; attempts < slots; ++attempts)
            {
                byte t = tags[idx];
                if (t == TagEmpty)
                    return -1;
                if ((t == tag) && IsKey(entries[idx].Key, key, mode))
                    return idx;
                if (++idx == slots)
                    idx = 0;
            }
            return -1;
        }

        /// <summary>
        /// Find the index of the key (-1 if not found), and the first free slot of its probe sequence (where it should be inserted).
        /// May be called without holding the lock (the result must then be validated).
        /// </summary>
        /// <param name="table">The table to search</param>
        /// <param name="th">The table hash of the key</param>
        /// <param name="key">The key to find</param>
        /// <param name="mode">How keys are hashed and compared</param>
        /// <param name="free">The first free (empty or tombstone) slot of the probe sequence, -1 if there are none (only valid if the key isn't found)</param>
        /// <param name="collisions">True if the probe found many slots with the same tag (but a different key) or was very long, the hash function is then assumed to be under attack (only checked for fast strings in a table that isn't randomized)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int FindForInsert(Table table, ulong th, TKey key, LowAllocKeyMode mode, out int free, out bool collisions)
        {
            if (UseVector(mode))
            {
                // The home block (the common case), the probe ends there if it has an empty slot
                nuint first = GetHomeSlot(th, table.Blocks);
                var loaded = Vector128.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(table.Tags), first);
                uint matches = Vector128.Equals(loaded, GetKeyTagVector(th)).ExtractMostSignificantBits();
                while (matches != 0)
                {
                    nuint idx = first + (uint)BitOperations.TrailingZeroCount(matches);
                    if (IsKey(Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(table.Entries), idx).Key, key, mode))
                    {
                        free = -1;
                        collisions = false;
                        return (int)idx;
                    }
                    matches &= matches - 1;
                }
                if (Vector128.Equals(loaded, Vector128<byte>.Zero).ExtractMostSignificantBits() != 0)
                {
                    // tag <= 1 <=> min(tag, 1) == tag
                    uint freeMask = Vector128.Equals(Vector128.Min(loaded, Vector128.Create(TagTombstone)), loaded).ExtractMostSignificantBits();
                    free = (int)first + BitOperations.TrailingZeroCount(freeMask);
                    // A single block can't have enough false matches
                    collisions = false;
                    return -1;
                }
            }
            // Copied, so that the caller's variables aren't address exposed
            int result = FindForInsertSlow(table, th, key, mode, out var f, out var c);
            free = f;
            collisions = c;
            return result;
        }

        /// <summary>
        /// Same as <see cref="FindForInsert"/>, searches the whole probe sequence
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        int FindForInsertSlow(Table table, ulong th, TKey key, LowAllocKeyMode mode, out int free, out bool collisions)
        {
            ref byte tags = ref MemoryMarshal.GetArrayDataReference(table.Tags);
            ref Entry entries = ref MemoryMarshal.GetArrayDataReference(table.Entries);
            int freeIdx = -1;
            int falseMatches = 0;
            int result = -1;
            int scanned = 0;
            if (UseVector(mode))
            {
                int blocks = table.Blocks;
                int block = GetHomeBlock(th, blocks);
                var tagVector = GetKeyTagVector(th);
                while (scanned < blocks)
                {
                    ++scanned;
                    int first = block << BlockBits;
                    var loaded = Vector128.LoadUnsafe(ref tags, (nuint)first);
                    uint matches = Vector128.Equals(loaded, tagVector).ExtractMostSignificantBits();
                    while (matches != 0)
                    {
                        int idx = first + BitOperations.TrailingZeroCount(matches);
                        if (IsKey(Unsafe.Add(ref entries, idx).Key, key, mode))
                        {
                            result = idx;
                            goto done;
                        }
                        ++falseMatches;
                        matches &= matches - 1;
                    }
                    if (freeIdx < 0)
                    {
                        // tag <= 1 <=> min(tag, 1) == tag
                        uint freeMask = Vector128.Equals(Vector128.Min(loaded, Vector128.Create(TagTombstone)), loaded).ExtractMostSignificantBits();
                        if (freeMask != 0)
                            freeIdx = first + BitOperations.TrailingZeroCount(freeMask);
                    }
                    if (Vector128.Equals(loaded, Vector128<byte>.Zero).ExtractMostSignificantBits() != 0)
                        break;
                    if (++block == blocks)
                        block = 0;
                }
            }
            else
            {
                int slots = table.Tags.Length;
                int idx = GetHomeBlock(th, table.Blocks) << BlockBits;
                byte tag = GetKeyTag(th);
                int slotsScanned = 0;
                while (slotsScanned < slots)
                {
                    ++slotsScanned;
                    byte t = Unsafe.Add(ref tags, idx);
                    if (t <= TagTombstone)
                    {
                        if (freeIdx < 0)
                            freeIdx = idx;
                        if (t == TagEmpty)
                            break;
                    }
                    else if (t == tag)
                    {
                        if (IsKey(Unsafe.Add(ref entries, idx).Key, key, mode))
                        {
                            result = idx;
                            break;
                        }
                        ++falseMatches;
                    }
                    if (++idx == slots)
                        idx = 0;
                }
                scanned = (slotsScanned + BlockSize - 1) >> BlockBits;
            }
        done:
            free = freeIdx;
            collisions = (KeyMode(mode) == LowAllocKeyMode.FastString) && !table.Randomized && ((falseMatches >= MaxFalseTagMatches) || (scanned >= MaxProbeBlocks));
            return result;
        }

        /// <summary>
        /// Find the first free (empty or tombstone) slot, -1 if none exist.
        /// Must hold the lock.
        /// </summary>
        static int FindFree(Table table, int homeBlock, LowAllocKeyMode mode)
        {
            ref byte tags = ref MemoryMarshal.GetArrayDataReference(table.Tags);
            if (UseVector(mode))
            {
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
            int slots = table.Tags.Length;
            int idx = homeBlock << BlockBits;
            for (int attempts = 0; attempts < slots; ++attempts)
            {
                if (Unsafe.Add(ref tags, idx) <= TagTombstone)
                    return idx;
                if (++idx == slots)
                    idx = 0;
            }
            return -1;
        }

        #endregion//Probing

        #region Operations

        /// <summary>
        /// True if the version is even (no write in progress) and still the current version of the segment (no write happened since it was read).
        /// If the version is odd, version &amp; ~1 is a version before it (the version only increases), so it never equals the current version.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsValid(ref LowAllocDictionarySegment seg, int version)
            => Volatile.Read(ref seg.Version) == (version & ~1);

        /// <summary>
        /// Add a key.
        /// The key is first searched for without holding the lock, so that the cache misses happens before taking the lock (shorter lock hold times).
        /// If the segment isn't modified before the lock is taken (same version), the result of that search is still valid.
        /// The common case can't throw, so it doesn't need a try/finally (that makes the JIT keep variables on the stack), everything else is done by <see cref="AddLocked"/>.
        /// </summary>
        /// <param name="key">The key to add</param>
        /// <param name="value">The value to add</param>
        /// <param name="overwrite">If true, an existing value is overwritten (and true is returned)</param>
        /// <param name="mode">How keys are hashed and compared</param>
        /// <returns>True if the value was added (or overwritten), false if the key exists (and overwrite is false)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool AddInline(TKey key, TValue value, bool overwrite, LowAllocKeyMode mode)
        {
            var h = GetHash(key, mode);
            ref var seg = ref GetSegment(h);
            int version = Volatile.Read(ref seg.Version);
            var table = GetTable(ref seg);
            var th = GetTableHash(table, h, key, mode);
            int idx = FindForInsert(table, th, key, mode, out var free, out var collisions);
            if ((idx >= 0) && !overwrite)
            {
                // The key exists, no need to take the lock (unless a write happened during the search)
                Volatile.ReadBarrier();
                if (IsValid(ref seg, version))
                    return false;
            }
            seg.EnterLock();
            if (IsValid(ref seg, version) && ReferenceEquals(seg.Table, table) && !collisions)
            {
                if (idx >= 0)
                {
                    if (overwrite)
                    {
                        seg.BeginWrite();
                        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(table.Entries), idx).Value = value;
                        seg.EndWrite();
                        seg.ExitLock();
                        return true;
                    }
                }
                else if ((free >= 0) && (seg.Used < table.MaxUsed))
                {
                    seg.BeginWrite();
                    ref var e = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(table.Entries), free);
                    e.Key = key;
                    e.Value = value;
                    ref var t = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(table.Tags), free);
                    if (t == TagEmpty)
                        ++seg.Used;
                    t = GetKeyTag(th);
                    ++seg.Live;
                    seg.EndWrite();
                    seg.ExitLock();
                    return true;
                }
            }
            return AddLocked(ref seg, key, value, overwrite, mode);
        }

        /// <summary>
        /// Add a key while holding the lock (the lock is released), when the table was modified concurrently or must be rebuilt
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        bool AddLocked(ref LowAllocDictionarySegment seg, TKey key, TValue value, bool overwrite, LowAllocKeyMode mode)
        {
            try
            {
                var h = GetHash(key, mode);
                var table = Unsafe.As<Table>(seg.Table);
                var th = GetTableHash(table, h, key, mode);
                int idx = FindForInsertSlow(table, th, key, mode, out var free, out var collisions);
                if (idx >= 0)
                {
                    if (!overwrite)
                        return false;
                    seg.BeginWrite();
                    table.Entries[idx].Value = value;
                    seg.EndWrite();
                    return true;
                }
                if (collisions)
                {
                    // Hash collisions (an attack?), switch the table to the randomized hash
                    table = Rebuild(ref seg, false, true);
                    th = GetTableHash(table, h, key, mode);
                    free = -1;
                }
                else if (seg.Used >= table.MaxUsed)
                {
                    table = Rebuild(ref seg, false, table.Randomized);
                    free = -1;
                }
                while (free < 0)
                {
                    free = FindFree(table, GetHomeBlock(th, table.Blocks), mode);
                    if (free < 0)
                        table = Rebuild(ref seg, true, table.Randomized);
                }
                seg.BeginWrite();
                ref var e = ref table.Entries[free];
                e.Key = key;
                e.Value = value;
                ref var t = ref table.Tags[free];
                if (t == TagEmpty)
                    ++seg.Used;
                t = GetKeyTag(th);
                ++seg.Live;
                seg.EndWrite();
                return true;
            }
            finally
            {
                seg.ExitLock();
            }
        }

        /// <summary>
        /// The lookup fast path: a single lock free attempt, only checking the home block (where almost all keys are).
        /// Everything else is done by <see cref="TryGetValueSlow"/>, so that the fast path doesn't need to preserve many values across calls.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryGetValueInline(TKey key, [MaybeNullWhen(false)] out TValue value, LowAllocKeyMode mode)
        {
            if (UseVector(mode))
            {
                var h = GetHash(key, mode);
                ref var seg = ref GetSegment(h);
                int version = Volatile.Read(ref seg.Version);
                var table = GetTable(ref seg);
                // The table is always consistent (never torn), so it's safe to search even if a write is in progress (the result is then discarded)
                var th = GetTableHash(table, h, key, mode);
                nuint first = GetHomeSlot(th, table.Blocks);
                var loaded = Vector128.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(table.Tags), first);
                uint matches = Vector128.Equals(loaded, GetKeyTagVector(th)).ExtractMostSignificantBits();
                while (matches != 0)
                {
                    ref var e = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(table.Entries), first + (uint)BitOperations.TrailingZeroCount(matches));
                    if (IsKey(e.Key, key, mode))
                    {
                        value = e.Value;
                        // Make sure that all reads above are done before validating the version
                        Volatile.ReadBarrier();
                        if (IsValid(ref seg, version))
                            return true;
                        goto slow;
                    }
                    matches &= matches - 1;
                }
                if (Vector128.Equals(loaded, Vector128<byte>.Zero).ExtractMostSignificantBits() != 0)
                {
                    Volatile.ReadBarrier();
                    if (IsValid(ref seg, version))
                    {
                        value = default;
                        return false;
                    }
                }
            }
        slow:
            return TryGetValueSlow(key, out value, mode);
        }

        /// <summary>
        /// The key wasn't in its home block, or a write happened during the lock free read: search the whole probe sequence, retry (and eventually read under the lock)
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        bool TryGetValueSlow(TKey key, [MaybeNullWhen(false)] out TValue value, LowAllocKeyMode mode)
        {
            var h = GetHash(key, mode);
            ref var seg = ref GetSegment(h);
            for (int attempt = 0; attempt < MaxOptimisticReads; ++attempt)
            {
                if (attempt > 0)
                    Thread.SpinWait(attempt);
                int version = Volatile.Read(ref seg.Version);
                if ((version & 1) == 0)
                {
                    var table = GetTable(ref seg);
                    int idx = Find(table, GetTableHash(table, h, key, mode), key, mode);
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
                int idx = Find(table, GetTableHash(table, h, key, mode), key, mode);
                value = idx >= 0 ? table.Entries[idx].Value : default;
                return idx >= 0;
            }
            finally
            {
                seg.ExitLock();
            }
        }

        /// <summary>
        /// Remove a key.
        /// The key is first searched for without holding the lock, so that the cache misses happens before taking the lock (shorter lock hold times),
        /// and a missing key doesn't need the lock at all.
        /// If the segment isn't modified before the lock is taken (same version), the result of that search is still valid.
        /// The common case can't throw, so it doesn't need a try/finally (that makes the JIT keep variables on the stack), everything else is done by <see cref="RemoveLocked"/>.
        /// </summary>
        /// <param name="key">The key to remove</param>
        /// <param name="value">The value of the removed key</param>
        /// <param name="matchValue">If true, the key is only removed if the current value equals the expected value</param>
        /// <param name="expected">The expected value (if matchValue is true)</param>
        /// <param name="mode">How keys are hashed and compared</param>
        /// <returns>True if the key was removed</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool RemoveInline(TKey key, [MaybeNullWhen(false)] out TValue value, bool matchValue, TValue expected, LowAllocKeyMode mode)
        {
            var h = GetHash(key, mode);
            ref var seg = ref GetSegment(h);
            int version = Volatile.Read(ref seg.Version);
            var table = GetTable(ref seg);
            int idx = Find(table, GetTableHash(table, h, key, mode), key, mode);
            if (idx < 0)
            {
                // The key doesn't exist, no need to take the lock (unless a write happened during the search)
                Volatile.ReadBarrier();
                if (IsValid(ref seg, version))
                {
                    value = default;
                    return false;
                }
            }
            seg.EnterLock();
            // Comparing the values may throw, so it's done by RemoveLocked
            if ((idx >= 0) && !matchValue && IsValid(ref seg, version) && ReferenceEquals(seg.Table, table))
            {
                seg.BeginWrite();
                ref var e = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(table.Entries), idx);
                value = e.Value;
                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(table.Tags), idx) = TagTombstone;
                // Release the references, so that the key and value can be collected
                if (RuntimeHelpers.IsReferenceOrContainsReferences<Entry>())
                    e = default;
                --seg.Live;
                seg.EndWrite();
                seg.ExitLock();
                return true;
            }
            return RemoveLocked(ref seg, key, out value, matchValue, expected, mode);
        }

        /// <summary>
        /// Remove a key while holding the lock (the lock is released), when the table was modified concurrently or the value must match
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        bool RemoveLocked(ref LowAllocDictionarySegment seg, TKey key, [MaybeNullWhen(false)] out TValue value, bool matchValue, TValue expected, LowAllocKeyMode mode)
        {
            try
            {
                var h = GetHash(key, mode);
                var table = Unsafe.As<Table>(seg.Table);
                int idx = Find(table, GetTableHash(table, h, key, mode), key, mode);
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
                --seg.Live;
                seg.EndWrite();
                return true;
            }
            finally
            {
                seg.ExitLock();
            }
        }

        /// <summary>
        /// The current table of a segment (only used by the unit tests, through reflection)
        /// </summary>
        object GetSegmentTable(int index) => GetSegmentAt(index).Table;

        /// <summary>
        /// Rebuild the table of a segment, optionally switching it to the randomized hash (only used by the unit tests, through reflection)
        /// </summary>
        void RebuildSegment(int index, bool randomized)
        {
            ref var seg = ref GetSegmentAt(index);
            seg.EnterLock();
            try
            {
                Rebuild(ref seg, false, randomized);
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

            /// <summary>
            /// Check if a key exists
            /// </summary>
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
            /// Add a key (created from the alternate key) if it doesn't exist, the key is only created (allocated) if it doesn't exist
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
        /// Find an alternate key (same probe sequence as <see cref="Find"/> / <see cref="FindFallback"/>), -1 if not found
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
        /// Lookup using an alternate key, lock free (validated using the segment version, like <see cref="TryGetValueInline"/>)
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

        /// <summary>
        /// Remove all items (one segment at a time, so it's not atomic: items added concurrently may survive).
        /// The tables keep their size (no memory is released, except the references to the keys and values).
        /// </summary>
        public void Clear()
        {
            var clearEntries = RuntimeHelpers.IsReferenceOrContainsReferences<Entry>();
            for (int s = 0; s < SegmentCount; s++)
            {
                ref var seg = ref GetSegmentAt(s);
                seg.EnterLock();
                try
                {
                    var table = Unsafe.As<Table>(seg.Table);
                    if (seg.Used <= 0)
                        continue;
                    seg.BeginWrite();
                    table.Tags.AsSpan().Clear();
                    // Release the references, so that the keys and values can be collected
                    if (clearEntries)
                        table.Entries.AsSpan().Clear();
                    seg.Live = 0;
                    seg.Used = 0;
                    seg.EndWrite();
                }
                finally
                {
                    seg.ExitLock();
                }
            }
        }

        /// <summary>
        /// Add a new key (see <see cref="Add(TKey, TValue)"/>)
        /// </summary>
        /// <param name="item">The key and value</param>
        /// <exception cref="ArgumentException">Thrown if the key already exists</exception>
        /// <exception cref="ArgumentNullException">Thrown if the key is null</exception>
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        /// <summary>
        /// Check if a key exists with a specific value (the value is compared using <see cref="EqualityComparer{T}.Default"/>)
        /// </summary>
        /// <param name="item">The key and value to find</param>
        /// <returns>True if the key exists and has the value</returns>
        /// <exception cref="ArgumentNullException">Thrown if the key is null</exception>
        public bool Contains(KeyValuePair<TKey, TValue> item) => TryGetValue(item.Key, out var val) && EqualityComparer<TValue>.Default.Equals(val, item.Value);

        /// <summary>
        /// Remove the key, only if the value matches (atomically, the value is compared using <see cref="EqualityComparer{T}.Default"/>)
        /// </summary>
        /// <param name="item">The key and the expected value</param>
        /// <returns>True if the key was removed</returns>
        /// <exception cref="ArgumentNullException">Thrown if the key is null</exception>
        public bool Remove(KeyValuePair<TKey, TValue> item)
            => Remove(item.Key, out _, true, item.Value);

        /// <summary>
        /// A snapshot of the items (weakly consistent, see <see cref="Enumerator"/>)
        /// </summary>
        /// <returns>A new list with the items</returns>
        public List<KeyValuePair<TKey, TValue>> ToList()
        {
            // Don't use the List constructor, it would call Count and CopyTo (that uses ToList)
            var list = new List<KeyValuePair<TKey, TValue>>(Count);
            foreach (var p in this)
                list.Add(p);
            return list;
        }

        /// <summary>
        /// A snapshot of the items (weakly consistent, see <see cref="Enumerator"/>)
        /// </summary>
        /// <returns>A new array with the items</returns>
        public KeyValuePair<TKey, TValue>[] ToArray()
            => ToList().ToArray();

        /// <summary>
        /// Copy a snapshot of the items to an array
        /// </summary>
        /// <param name="array">The destination array</param>
        /// <param name="arrayIndex">The index in <paramref name="array"/> to start writing at</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="array"/> is null (in release builds thrown by the internal copy, for the destination array)</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="arrayIndex"/> is negative (in release builds thrown by the internal copy, for the destination index)</exception>
        /// <exception cref="ArgumentException">Thrown if the destination is too small for the snapshot</exception>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
#endif//DEBUG
            // Take a snapshot, the number of items may change while copying
            var items = ToList();
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

            /// <inheritdoc/>
            public bool MoveNext()
            {
                var dict = Dict;
                for (; ; )
                {
                    var table = CurrentTable;
                    if (table == null)
                    {
                        if (SegmentIdx >= SegmentCount)
                            return false;
                        // A table is either the current one or an old one (never modified), so items never move while enumerating it
                        table = GetTable(ref dict.GetSegmentAt(SegmentIdx));
                        CurrentTable = table;
                        SlotIdx = -1;
                    }
                    var tags = table.Tags;
                    var idx = SlotIdx;
                    while (++idx < tags.Length)
                    {
                        if (tags[idx] <= TagTombstone)
                            continue;
                        if (TryReadSlot(ref dict.GetSegmentAt(SegmentIdx), table, idx, out C))
                        {
                            SlotIdx = idx;
                            return true;
                        }
                    }
                    CurrentTable = null;
                    ++SegmentIdx;
                }
            }

            /// <inheritdoc/>
            public readonly KeyValuePair<TKey, TValue> Current => C;
            readonly object IEnumerator.Current => C;

            /// <inheritdoc/>
            public void Reset()
            {
                SegmentIdx = 0;
                SlotIdx = -1;
                CurrentTable = null;
                C = default;
            }

            /// <summary>
            /// Does nothing
            /// </summary>
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
    /// How the keys of a <see cref="LowAllocConcurrentDictionary{TKey, TValue}"/> are hashed and compared (and flags).
    /// The core operations are inlined with a constant mode, so that all checks of it are removed by the JIT.
    /// </summary>
    enum LowAllocKeyMode
    {
        /// <summary>
        /// The default comparer for value types (devirtualized and inlined by the JIT)
        /// </summary>
        Default,
        /// <summary>
        /// Ordinal string keys (TKey is string), using a fast seeded hash.
        /// If collisions are detected in a table, it's rebuilt using the (randomized) ordinal comparer for the slots,
        /// the fast hash is still used for selecting the segment (an attack can at worst put all keys in the same segment).
        /// </summary>
        FastString,
        /// <summary>
        /// A comparer instance
        /// </summary>
        Custom,
        /// <summary>
        /// The key comparison part of a mode
        /// </summary>
        KeyMask = 3,
        /// <summary>
        /// A flag: probe one slot at a time even if SIMD is available (only used by the unit tests, so that the scalar probing can be tested on any hardware)
        /// </summary>
        Scalar = 4,
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
            // Math.BigMul returns the low part through memory (a store and a load in the dependency chain), two multiplications are faster
            if (Bmi2.X64.IsSupported)
                return Bmi2.X64.MultiplyNoFlags(a, b) ^ (a * b);
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
        public static ulong GetHash(string s) => GetHash(ref MemoryMarshal.GetReference(s.AsSpan()), s.Length);

        /// <summary>
        /// Get a 64 bit hash of the chars (all bits are well mixed), the same as for a string with the same chars
        /// </summary>
        /// <param name="s">The chars to hash</param>
        /// <returns>A 64 bit hash, only valid during the life time of the process</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetHash(ReadOnlySpan<char> s) => GetHash(ref MemoryMarshal.GetReference(s), s.Length);

        /// <summary>
        /// Get a 64 bit hash of the chars (all bits are well mixed)
        /// </summary>
        /// <param name="c">The first char</param>
        /// <param name="length">The number of chars</param>
        /// <returns>A 64 bit hash, only valid during the life time of the process</returns>
        static ulong GetHash(ref char c, int length)
        {
            ref byte p = ref Unsafe.As<char, byte>(ref c);
            nuint len = (nuint)length << 1;
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
    /// The segments of a LowAllocConcurrentDictionary, stored inline in the dictionary object
    /// </summary>
    [InlineArray(Count)]
    struct LowAllocDictionarySegments
    {
        /// <summary>
        /// The number of segments, must be 1 &lt;&lt; LowAllocConcurrentDictionary.SegmentBits
        /// </summary>
        public const int Count = 64;

        LowAllocDictionarySegment Segment;
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
        /// <summary>
        /// Number of live entries in the current table (only modified while holding the lock)
        /// </summary>
        [FieldOffset(80)]
        public int Live;
        /// <summary>
        /// Number of non empty slots in the current table, live + tombstones (only modified while holding the lock)
        /// </summary>
        [FieldOffset(84)]
        public int Used;

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

