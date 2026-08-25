using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;

namespace SysWeaver
{

    /// <summary>
    /// A concurrent dictionary that doesn't allocate internal nodes as frequent as the regular ConcurrentDictionary.
    /// 
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public sealed class LowAllocConcurrentDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        const int SegmentCount = 64;
        const int SegmentMask = SegmentCount - 1;
        const int BlockSize = 16;

        readonly Segment[] Segments;
        readonly IEqualityComparer<TKey> Comparer;

        public LowAllocConcurrentDictionary(int capacity = 1024, IEqualityComparer<TKey> comparer = default)
        {
            Comparer = comparer ?? EqualityComparer<TKey>.Default;
            Segments = GC.AllocateUninitializedArray<Segment>(SegmentCount);
            var cap = Math.Max(16, ((capacity + SegmentCount - 1) / SegmentCount).EnsurePow2());
            for (int i = 0; i < SegmentCount; i++)
                Segments[i] = new Segment(cap);
        }

        public LowAllocConcurrentDictionary(IEqualityComparer<TKey> comparer)
             : this(1024, comparer)
        {
        }


        public void Add(TKey key, TValue value) { if (!TryAdd(key, value)) throw new ArgumentException(); }
        public bool ContainsKey(TKey key) => TryGetValue(key, out _);
        public bool Remove(TKey key) => TryRemove(key, out _);

        public TValue this[TKey key]
        {
            get => TryGetValue(key, out var val) ? val : throw new KeyNotFoundException();
            set {
                for (; ; )
                {
                    if (TryAdd(key, value))
                        break;
                    TryRemove(key, out _);
                }
            }
        }

        public int Count
        {
            get
            {
                int total = 0;
                for (int s = 0; s < SegmentCount; s++)
                {
                    var tags = Volatile.Read(ref Segments[s].Tags);
                    for (int i = 0; i < tags.Length; i++)
                    {
                        if (tags[i] > 1) total++;
                    }
                }
                return total;
            }
        }

        public bool IsReadOnly => false;
        public ICollection<TKey> Keys => GetKeysCollection();

        public ICollection<TValue> Values => GetValuesCollection();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(TKey key, TValue value)
            => Sse2.IsSupported ? Sse2_TryAdd(key, value) : Fallback_TryAdd(key, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
            => Sse2.IsSupported ? Sse2_TryGetValue(key, out value) : Fallback_TryGetValue(key, out value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemove(TKey key, [MaybeNullWhen(false)] out TValue value)
            => Sse2.IsSupported ? Sse2_TryRemove(key, out value) : Fallback_TryRemove(key, out value);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        byte GetKeyTag(int hashCode)
        {
            byte tag = (byte)((hashCode >> 24) & 0xFF);
            return tag < 2 ? (byte)2 : tag;
        }

        sealed class Segment
        {
            public int LockState;
            public byte[] Tags;
            public Entry[] Entries;
            public int CapacityMask;

            public struct Entry
            {
                public TKey Key;
                public TValue Value;
            }

            public Segment(int initialCapacity)
            {
                int powerOfTwoCap = BlockSize;
                while (powerOfTwoCap < initialCapacity) powerOfTwoCap <<= 1;

                Tags = new byte[powerOfTwoCap];
                Entries = GC.AllocateUninitializedArray<Entry>(powerOfTwoCap); // new Entry[powerOfTwoCap];
                CapacityMask = powerOfTwoCap - 1;
            }

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

            public void EnsureCapacity(IEqualityComparer<TKey> comparer)
            {
                int oldCapacity = Tags.Length;
                int newCapacity = oldCapacity * 2;

                var newTags = new byte[newCapacity];
                var newEntries = GC.AllocateUninitializedArray<Entry>(newCapacity); // new Entry[newCapacity];

                int newMask = newCapacity - 1;

                var tags = Tags;
                var entries = Entries;
                for (int i = 0; i < oldCapacity; i++)
                {
                    byte tag = tags[i];
                    if (tag > 1)
                    {
                        int hashCode = comparer.GetHashCode(entries[i].Key);
                        int targetBucket = (hashCode & int.MaxValue) & newMask;
                        int currentIdx = targetBucket & ~(BlockSize - 1);

                        while (true)
                        {
                            if (newTags[currentIdx] == 0)
                            {
                                newTags[currentIdx] = tag;
                                newEntries[currentIdx] = Entries[i];
                                break;
                            }
                            currentIdx = (currentIdx + 1) & newMask;
                        }
                    }
                }

                Tags = newTags;
                Entries = newEntries;
                CapacityMask = newMask;
            }
        }

        public void Clear()
        {
            for (int s = 0; s < SegmentCount; s++)
            {
                var seg = Segments[s];
                seg.EnterLock();
                try
                {
                    seg.Tags.AsSpan().Clear();
                    //Array.Clear(seg.Tags);
                    //Array.Clear(seg.Entries);
                }
                finally
                {
                    seg.ExitLock();
                }
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
        public bool Contains(KeyValuePair<TKey, TValue> item) => TryGetValue(item.Key, out var val) && EqualityComparer<TValue>.Default.Equals(val, item.Value);
        public bool Remove(KeyValuePair<TKey, TValue> item) => Contains(item) && Remove(item.Key);

        public List<KeyValuePair<TKey, TValue>> ToList()
            => new List<KeyValuePair<TKey, TValue>>(this);

        public KeyValuePair<TKey, TValue>[] ToArray()
            => new List<KeyValuePair<TKey, TValue>>(this).ToArray();

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            int cursor = arrayIndex;
            var mx = array.Length;
            foreach (var pair in this)
            {
                if (cursor >= mx)
                    break;
                array[cursor] = pair;
                ++cursor;
            }
        }

        struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            readonly LowAllocConcurrentDictionary<TKey, TValue> Dict;
            int SegmentIdx;
            int SlotIdx;
            byte[] CurrentTags;
            Segment.Entry[] CurrentEntries;
            KeyValuePair<TKey, TValue> C;

            internal Enumerator(LowAllocConcurrentDictionary<TKey, TValue> dict)
            {
                Dict = dict;
                SegmentIdx = 0;
                SlotIdx = -1;
                CurrentTags = Volatile.Read(ref dict.Segments[0].Tags);
                CurrentEntries = Volatile.Read(ref Dict.Segments[0].Entries);
                C = default;
            }

            public bool MoveNext()
            {
                var segmentIdx = SegmentIdx;
                var slotIdx = SlotIdx;
                var segCount = SegmentCount;
                var ds = Dict.Segments;
                var currentTags = CurrentTags;
                var currentEntries = CurrentEntries;
                while (segmentIdx < segCount)
                {
                    slotIdx++;
                    if (slotIdx >= currentTags.Length)
                    {
                        segmentIdx++;
                        if (segmentIdx >= segCount) break;
                        currentTags = Volatile.Read(ref ds[segmentIdx].Tags);
                        currentEntries = Volatile.Read(ref ds[segmentIdx].Entries);
                        slotIdx = 0;
                    }

                    if (currentTags[slotIdx] > 1)
                    {
                        var entry = currentEntries[slotIdx];
                        C = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
                        CurrentEntries = currentEntries;
                        CurrentTags = currentTags;
                        SegmentIdx = segmentIdx;
                        SlotIdx = slotIdx;
                        return true;
                    }
                }
                        CurrentEntries = currentEntries;
                        CurrentTags = currentTags;
                        SegmentIdx = segmentIdx;
                        SlotIdx = slotIdx;
                return false;
            }

            public KeyValuePair<TKey, TValue> Current => C;
            object IEnumerator.Current => C;
            public void Reset() { SegmentIdx = 0; SlotIdx = -1; }
            public void Dispose() { }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => new Enumerator(this);
        

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        List<TKey> GetKeysCollection()
        {
            var list = new List<TKey>();
            foreach (var p in this) list.Add(p.Key);
            return list;
        }

        List<TValue> GetValuesCollection()
        {
            var list = new List<TValue>();
            foreach (var p in this) list.Add(p.Value);
            return list;
        }


        #region SSE2

        unsafe bool Sse2_TryAdd(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var comparer = Comparer;
            int hashCode = comparer.GetHashCode(key);
            Segment seg = Segments[(hashCode & int.MaxValue) & SegmentMask];
            byte targetTag = GetKeyTag(hashCode);
            int firstAvailableSlot = -1;
            int attempts = 0;

            seg.EnterLock();
            try
            {
                int mask = seg.CapacityMask;
                int initialBucket = (hashCode & int.MaxValue) & mask;
                int currentBlockIdx = initialBucket & ~(BlockSize - 1);

                int capacity = mask + 1;

                fixed (byte* baseTagsPtr = seg.Tags)
                {
                    while (attempts < capacity)
                    {
                        byte* tagsPtr = baseTagsPtr + currentBlockIdx;
                        Vector128<byte> loadedTags = Sse2.LoadVector128(tagsPtr);
                        Vector128<byte> targetTagVector = Vector128.Create(targetTag);
                        int matchMask = Sse2.MoveMask(Sse2.CompareEqual(loadedTags, targetTagVector));

                        while (matchMask != 0)
                        {
                            int bitIndex = System.Numerics.BitOperations.TrailingZeroCount(matchMask);
                            if (comparer.Equals(seg.Entries[currentBlockIdx + bitIndex].Key, key))
                            {
                                return false;
                            }
                            matchMask &= ~(1 << bitIndex);
                        }

                        if (firstAvailableSlot == -1)
                        {
                            int emptyMask = Sse2.MoveMask(Sse2.CompareEqual(loadedTags, Vector128<byte>.Zero));
                            int tombstoneMask = Sse2.MoveMask(Sse2.CompareEqual(loadedTags, Vector128.Create((byte)1)));
                            int freeMask = emptyMask | tombstoneMask;

                            if (freeMask != 0)
                            {
                                firstAvailableSlot = currentBlockIdx + System.Numerics.BitOperations.TrailingZeroCount(freeMask);
                            }
                        }

                        int hasEmpty = Sse2.MoveMask(Sse2.CompareEqual(loadedTags, Vector128<byte>.Zero));
                        if (hasEmpty != 0) break;
                        currentBlockIdx = (currentBlockIdx + BlockSize) & mask;
                        attempts += BlockSize;
                    }
                }
                if (firstAvailableSlot == -1)
                {
                    seg.EnsureCapacity(Comparer);
                    seg.ExitLock();
                    return TryAdd(key, value);
                }

                seg.Tags[firstAvailableSlot] = targetTag;
                seg.Entries[firstAvailableSlot].Key = key;
                seg.Entries[firstAvailableSlot].Value = value;
                return true;
            }
            finally
            {
                seg.ExitLock();
            }
        }

        unsafe bool Sse2_TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            int hashCode = Comparer.GetHashCode(key);
            Segment seg = Segments[(hashCode & int.MaxValue) & SegmentMask];
            byte targetTag = GetKeyTag(hashCode);

            // Optimistic completely lock-free read sequence using Volatile memory snapshots
            byte[] tags = Volatile.Read(ref seg.Tags);
            Segment.Entry[] entries = Volatile.Read(ref seg.Entries);
            int mask = tags.Length - 1;

            int initialBucket = (hashCode & int.MaxValue) & mask;
            int currentBlockIdx = initialBucket & ~(BlockSize - 1);
            int attempts = 0;
            int capacity = mask + 1;

            Vector128<byte> targetTagVector = Vector128.Create(targetTag);
            Vector128<byte> zeroVector = Vector128.Create((byte)0);
            var comparer = Comparer;
            fixed (byte* baseTagsPtr = tags)
            {
                while (attempts < capacity)
                {
                    byte* tagsPtr = baseTagsPtr + currentBlockIdx;
                    Vector128<byte> loadedTags = Sse2.LoadVector128(tagsPtr);
                    int matchMask = Sse2.MoveMask(Sse2.CompareEqual(loadedTags, targetTagVector));
                    while (matchMask != 0)
                    {
                        int bitIndex = System.Numerics.BitOperations.TrailingZeroCount(matchMask);
                        int exactIdx = currentBlockIdx + bitIndex;

                        if (comparer.Equals(entries[exactIdx].Key, key))
                        {
                            value = entries[exactIdx].Value;
                            return true;
                        }
                        matchMask &= ~(1 << bitIndex);
                    }
                    int emptyMask = Sse2.MoveMask(Sse2.CompareEqual(loadedTags, zeroVector));
                    if (emptyMask != 0)
                        break;

                    currentBlockIdx = (currentBlockIdx + BlockSize) & mask;
                    attempts += BlockSize;
                }
            }
            value = default;
            return false;
        }

        unsafe bool Sse2_TryRemove(TKey key,  out TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var comparer = Comparer;
            int hashCode = comparer.GetHashCode(key);
            Segment seg = Segments[(hashCode & int.MaxValue) & SegmentMask];
            byte targetTag = GetKeyTag(hashCode);

            // OPTIMIZATION: Hoist the mask directly from the segment field to avoid array header heap lookups
            int mask = seg.CapacityMask;
            byte[] tags = Volatile.Read(ref seg.Tags);
            var entries = seg.Entries;

            int initialBucket = (hashCode & int.MaxValue) & mask;
            int currentBlockIdx = initialBucket & ~(BlockSize - 1);
            int attempts = 0;
            int capacity = mask + 1;

            Vector128<byte> targetTagVector = Vector128.Create(targetTag);
            Vector128<byte> zeroVector = Vector128.Create((byte)0);

            fixed (byte* baseTagsPtr = tags)
            {
                while (attempts < capacity)
                {
                    byte* tagsPtr = baseTagsPtr + currentBlockIdx;

                    Vector128<byte> loadedTags = Sse2.LoadVector128(tagsPtr);
                    int matchMask = Sse2.MoveMask(Sse2.CompareEqual(loadedTags, targetTagVector));

                    while (matchMask != 0)
                    {
                        int bitIndex = System.Numerics.BitOperations.TrailingZeroCount(matchMask);
                        int exactIdx = currentBlockIdx + bitIndex;

                        if (comparer.Equals(entries[exactIdx].Key, key))
                        {
                            seg.EnterLock();
                            try
                            {
                                if (seg.Tags[exactIdx] == targetTag)
                                {
                                    ref var e = ref seg.Entries[exactIdx];
                                    if (comparer.Equals(e.Key, key))
                                    {
                                        value = e.Value;
                                        seg.Tags[exactIdx] = 1; // Mark as structural Tombstone
                                        seg.Entries[exactIdx] = default; // Clear Key and Value atomically
                                        return true;
                                    }
                                }
                            }
                            finally
                            {
                                seg.ExitLock();
                            }
                        }
                        matchMask &= ~(1 << bitIndex);
                    }

                    // OPTIMIZATION: If this 16-element block has an empty slot (0), the search chain 
                    // terminates here. The key cannot exist past this point.
                    int emptyMask = Sse2.MoveMask(Sse2.CompareEqual(loadedTags, zeroVector));
                    if (emptyMask != 0) break;

                    currentBlockIdx = (currentBlockIdx + BlockSize) & mask;
                    attempts += BlockSize;
                }
            }
            value = default;
            return false;
        }

        #endregion//SSE2


        #region Fallback



        unsafe bool Fallback_TryAdd(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var comparer = Comparer;
            int hashCode = comparer.GetHashCode(key);
            Segment seg = Segments[(hashCode & int.MaxValue) & SegmentMask];
            byte targetTag = GetKeyTag(hashCode);
            int firstAvailableSlot = -1;
            int attempts = 0;

            seg.EnterLock();
            try
            {
                int mask = seg.CapacityMask;
                int initialBucket = (hashCode & int.MaxValue) & mask;
                int currentBlockIdx = initialBucket & ~(BlockSize - 1);

                int capacity = mask + 1;

                fixed (byte* baseTagsPtr = seg.Tags)
                {
                    while (attempts < capacity)
                    {
                        byte* tagsPtr = baseTagsPtr + currentBlockIdx;
                        for (int i = 0; i < BlockSize; i++)
                        {
                            int exactIdx = currentBlockIdx + i;
                            byte t = tagsPtr[i];
                            if (t == targetTag && comparer.Equals(seg.Entries[exactIdx].Key, key))
                                return false;
                            if (firstAvailableSlot == -1 && t <= 1) firstAvailableSlot = exactIdx;
                            if (t == 0) break;
                        }
                        if (firstAvailableSlot != -1 && tagsPtr[firstAvailableSlot - currentBlockIdx] == 0) break;

                        currentBlockIdx = (currentBlockIdx + BlockSize) & mask;
                        attempts += BlockSize;
                    }
                }

                if (firstAvailableSlot == -1)
                {
                    seg.EnsureCapacity(Comparer);
                    seg.ExitLock();
                    return TryAdd(key, value);
                }

                seg.Tags[firstAvailableSlot] = targetTag;
                seg.Entries[firstAvailableSlot].Key = key;
                seg.Entries[firstAvailableSlot].Value = value;
                return true;
            }
            finally
            {
                seg.ExitLock();
            }
        }

        unsafe bool Fallback_TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            int hashCode = Comparer.GetHashCode(key);
            Segment seg = Segments[(hashCode & int.MaxValue) & SegmentMask];
            byte targetTag = GetKeyTag(hashCode);

            // Optimistic completely lock-free read sequence using Volatile memory snapshots
            byte[] tags = Volatile.Read(ref seg.Tags);
            Segment.Entry[] entries = Volatile.Read(ref seg.Entries);
            int mask = tags.Length - 1;

            int initialBucket = (hashCode & int.MaxValue) & mask;
            int currentBlockIdx = initialBucket & ~(BlockSize - 1);
            int attempts = 0;
            int capacity = mask + 1;

            var comparer = Comparer;
            fixed (byte* baseTagsPtr = tags)
            {
                while (attempts < capacity)
                {
                    byte* tagsPtr = baseTagsPtr + currentBlockIdx;

                    for (int i = 0; i < BlockSize; i++)
                    {
                        int exactIdx = currentBlockIdx + i;
                        byte t = tagsPtr[i];
                        if (t == 0) { value = default; return false; }
                        if (t == targetTag && comparer.Equals(entries[exactIdx].Key, key))
                        {
                            value = entries[exactIdx].Value;
                            return true;
                        }
                    }

                    currentBlockIdx = (currentBlockIdx + BlockSize) & mask;
                    attempts += BlockSize;
                }
            }
            value = default;
            return false;
        }

        unsafe bool Fallback_TryRemove(TKey key, out TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var comparer = Comparer;
            int hashCode = comparer.GetHashCode(key);
            Segment seg = Segments[(hashCode & int.MaxValue) & SegmentMask];
            byte targetTag = GetKeyTag(hashCode);

            // OPTIMIZATION: Hoist the mask directly from the segment field to avoid array header heap lookups
            int mask = seg.CapacityMask;
            byte[] tags = Volatile.Read(ref seg.Tags);
            var entries = seg.Entries;

            int initialBucket = (hashCode & int.MaxValue) & mask;
            int currentBlockIdx = initialBucket & ~(BlockSize - 1);
            int attempts = 0;
            int capacity = mask + 1;

            fixed (byte* baseTagsPtr = tags)
            {
                while (attempts < capacity)
                {
                    byte* tagsPtr = baseTagsPtr + currentBlockIdx;

                    for (int i = 0; i < BlockSize; i++)
                    {
                        int exactIdx = currentBlockIdx + i;
                        byte t = tagsPtr[i];
                        if (t == 0)
                        {
                            value = default;
                            return false;
                        }
                        if (t == targetTag)
                        {
                            if (comparer.Equals(entries[exactIdx].Key, key))
                            {
                                seg.EnterLock();
                                try
                                {
                                    if (seg.Tags[exactIdx] == targetTag)
                                    {
                                        ref var e = ref seg.Entries[exactIdx];
                                        if (comparer.Equals(e.Key, key))
                                        {
                                            value = e.Value;
                                            seg.Tags[exactIdx] = 1;
                                            e = default;
                                            return true;
                                        }
                                    }
                                }
                                finally
                                {
                                    seg.ExitLock();
                                }
                            }
                        }
                    }
                    currentBlockIdx = (currentBlockIdx + BlockSize) & mask;
                    attempts += BlockSize;
                }
            }
            value = default;
            return false;
        }




        #endregion//Fallback


    }



}