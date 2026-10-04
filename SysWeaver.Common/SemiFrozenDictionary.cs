using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SysWeaver
{

    /// <summary>
    /// Use this dictionary when number of reads far exceeds the number of modificatiions.
    /// This is thread safe in the same sense as a ConcurrentDictionary.
    /// Reads are done on a frozen copy of the underlaying dictionary.
    /// Mutating underlaying dictionary is done using locks and the frozen copy is invalidated.
    /// After a modification, reads are done on the underlaying (concurrent) dictionary until it has been read enough times without modifications
    /// to make freezing it worth the cost (freezing allocates a copy, so freezing after every modification of a growing cache was O(n^2)).
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public sealed class SemiFrozenDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        /// <summary>
        /// The frozen copy, null after a modification (until it has been read enough times to freeze it again)
        /// </summary>
        IReadOnlyDictionary<TKey, TValue> Internal;

        /// <summary>
        /// The number of reads of the underlaying dictionary since the last modification (not exact, it's incremented without synchronization)
        /// </summary>
        int Reads;

        /// <summary>
        /// Freeze when this number of reads have been done since the last modification.
        /// A frozen copy is faster to read, but it costs about as much to create as 16 extra reads per item (of the underlaying dictionary),
        /// so freezing when the extra cost of the reads equals the cost of freezing is never more than twice as expensive as the best choice.
        /// </summary>
        int FreezeAfter = MinReadsBeforeFreeze;

        const int MinReadsBeforeFreeze = 64;
        const int ReadsPerItemBeforeFreeze = 16;

        /// <summary>
        /// Get the frozen copy (freeze it if needed)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IReadOnlyDictionary<TKey, TValue> Get()
            => Internal ?? Freeze();

        /// <summary>
        /// Freeze the underlaying dictionary
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        IReadOnlyDictionary<TKey, TValue> Freeze()
        {
            lock (Underlaying)
            {
                var i = Internal;
                if (i != null)
                    return i;
                i = Underlaying.Freeze(Comparer);
                Internal = i;
                return i;
            }
        }

        /// <summary>
        /// Must be called (in the lock) after a modification
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Changed()
        {
            Internal = null;
            Reads = 0;
            FreezeAfter = MinReadsBeforeFreeze + ReadsPerItemBeforeFreeze * Underlaying.Count;
        }

        /// <summary>
        /// The underlaying dictionary, all modifications are done while holding a lock on it (so a concurrency level of 1 is enough), reads are lock free
        /// </summary>
        readonly ConcurrentDictionary<TKey, TValue> Underlaying;

        readonly IEqualityComparer<TKey> Comparer;

        /// <summary>
        /// The number of lock stripes of the underlaying dictionary (modifications are serialized anyway)
        /// </summary>
        const int ConcurrencyLevel = 1;

        /// <summary>
        /// The default capacity of a ConcurrentDictionary
        /// </summary>
        const int DefaultCapacity = 31;

        public SemiFrozenDictionary()
            : this(DefaultCapacity, null)
        {
        }

        public SemiFrozenDictionary(IDictionary<TKey, TValue> other)
            : this(other, null)
        {
        }

        public SemiFrozenDictionary(IEnumerable<KeyValuePair<TKey, TValue>> other)
            : this(other, null)
        {
        }

        public SemiFrozenDictionary(int size)
            : this(size, null)
        {
        }

        public SemiFrozenDictionary(IEqualityComparer<TKey> comparer)
            : this(DefaultCapacity, comparer)
        {
        }

        public SemiFrozenDictionary(IDictionary<TKey, TValue> other, IEqualityComparer<TKey> comparer)
            : this((IEnumerable<KeyValuePair<TKey, TValue>>)other, comparer)
        {
        }

        public SemiFrozenDictionary(IEnumerable<KeyValuePair<TKey, TValue>> other, IEqualityComparer<TKey> comparer)
        {
            ArgumentNullException.ThrowIfNull(other);
            Comparer = comparer ?? EqualityComparer<TKey>.Default;
            var u = new ConcurrentDictionary<TKey, TValue>(ConcurrencyLevel, other is ICollection<KeyValuePair<TKey, TValue>> c ? Math.Max(c.Count, DefaultCapacity) : DefaultCapacity, Comparer);
            // Duplicate keys throws (like the Dictionary constructor)
            foreach (var x in other)
                if (!u.TryAdd(x.Key, x.Value))
                    throw new ArgumentException("An item with the same key has already been added. Key: " + x.Key);
            Underlaying = u;
            FreezeAfter = MinReadsBeforeFreeze + ReadsPerItemBeforeFreeze * u.Count;
        }

        public SemiFrozenDictionary(int size, IEqualityComparer<TKey> comparer)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(size);
            Comparer = comparer ?? EqualityComparer<TKey>.Default;
            Underlaying = new ConcurrentDictionary<TKey, TValue>(ConcurrencyLevel, size, Comparer);
        }


        public TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                    return value;
                throw new KeyNotFoundException("The given key '" + key + "' was not present in the dictionary.");
            }
            set
            {
                var u = Underlaying;
                lock (u)
                {
                    u[key] = value;
                    Changed();
                }
            }
        }

        public ICollection<TKey> Keys => Get().Keys.ToList();

        public ICollection<TValue> Values => Get().Values.ToList();

        public int Count
        {
            get
            {
                var i = Internal;
                if (i != null)
                    return i.Count;
                var u = Underlaying;
                lock (u)
                    return u.Count;
            }
        }

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            var u = Underlaying;
            lock (u)
            {
                if (!u.TryAdd(key, value))
                    throw new ArgumentException("An item with the same key has already been added. Key: " + key);
                Changed();
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
            => Add(item.Key, item.Value);

        public bool TryAdd(TKey key, TValue value)
        {
            var u = Underlaying;
            lock (u)
            {
                if (!u.TryAdd(key, value))
                    return false;
                Changed();
                return true;
            }
        }

        public bool TryAdd(KeyValuePair<TKey, TValue> item)
            => TryAdd(item.Key, item.Value);

        public void Clear()
        {
            var u = Underlaying;
            lock (u)
            {
                u.Clear();
                Changed();
            }
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (!TryGetValue(item.Key, out var result))
                return false;
            return EqualityComparer<TValue>.Default.Equals(item.Value, result);
        }

        public bool ContainsKey(TKey key)
            => TryGetValue(key, out _);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(arrayIndex, array.Length);
            var d = Get();
            if ((array.Length - arrayIndex) < d.Count)
                throw new ArgumentException("Destination array is not long enough to copy all the items in the collection. Check array index and length.");
            foreach (var x in d)
            {
                array[arrayIndex] = x;
                ++arrayIndex;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            => Get().GetEnumerator();

        public bool Remove(TKey key)
            => TryRemove(key, out _);

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            var u = Underlaying;
            lock (u)
            {
                // ICollection.Remove, the key (using the comparer) and the value must match
                if (!((ICollection<KeyValuePair<TKey, TValue>>)u).Remove(item))
                    return false;
                Changed();
            }
            return true;
        }

        public bool TryRemove(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            var u = Underlaying;
            lock (u)
            {
                if (!u.TryRemove(key, out value))
                    return false;
                Changed();
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            var i = Internal;
            if (i != null)
                return i.TryGetValue(key, out value);
            return TryGetValueNotFrozen(key, out value);
        }

        /// <summary>
        /// Read the underlaying dictionary (lock free), and freeze it when it has been read enough times since the last modification
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        bool TryGetValueNotFrozen(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (++Reads >= FreezeAfter)
                return Freeze().TryGetValue(key, out value);
            return Underlaying.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
            => Get().GetEnumerator();

    }
}
