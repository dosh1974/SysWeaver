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
    /// A dictionary for data where the number of reads far exceeds the number of modifications.
    /// This is thread safe in the same sense as a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// Reads are done on a frozen copy of the underlying dictionary (see <see cref="DictionaryExt.Freeze{K, V}(IReadOnlyDictionary{K, V}, IEqualityComparer{K})"/>).
    /// Modifications of the underlying dictionary are done while holding a lock, and the frozen copy is invalidated.
    /// After a modification, reads are done on the underlying (concurrent) dictionary until it has been read enough times without modifications
    /// to make freezing it worth the cost (freezing allocates a copy, so freezing after every modification of a growing cache would be O(n^2)).
    /// </summary>
    /// <remarks>
    /// Reads are lock free, modifications are serialized (a single lock per dictionary).
    /// A read that races with a modification may see the state before the modification.
    /// <see cref="Keys"/>, <see cref="Values"/> and enumeration operate on a snapshot (they force a freeze if needed).
    /// Null keys are not allowed (<see cref="ArgumentNullException"/>, frozen or not).
    /// </remarks>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
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

        /// <summary>
        /// Create an empty dictionary using the default key comparer
        /// </summary>
        public SemiFrozenDictionary()
            : this(DefaultCapacity, null)
        {
        }

        /// <summary>
        /// Create a dictionary with a copy of the entries of another dictionary, using the default key comparer (not the comparer of <paramref name="other"/>)
        /// </summary>
        /// <param name="other">The entries to copy</param>
        /// <exception cref="ArgumentNullException">Thrown if a key is null, or if <paramref name="other"/> is null (for <paramref name="other"/>: debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="other"/> contains duplicate keys (according to the default comparer)</exception>
        public SemiFrozenDictionary(IDictionary<TKey, TValue> other)
            : this(other, null)
        {
        }

        /// <summary>
        /// Create a dictionary with a copy of some key-value pairs, using the default key comparer
        /// </summary>
        /// <param name="other">The entries to copy</param>
        /// <exception cref="ArgumentNullException">Thrown if a key is null, or if <paramref name="other"/> is null (for <paramref name="other"/>: debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="other"/> contains duplicate keys</exception>
        public SemiFrozenDictionary(IEnumerable<KeyValuePair<TKey, TValue>> other)
            : this(other, null)
        {
        }

        /// <summary>
        /// Create an empty dictionary using the default key comparer
        /// </summary>
        /// <param name="size">The initial capacity of the underlying dictionary</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="size"/> is negative (release builds report the parameter of the underlying dictionary, "capacity")</exception>
        public SemiFrozenDictionary(int size)
            : this(size, null)
        {
        }

        /// <summary>
        /// Create an empty dictionary
        /// </summary>
        /// <param name="comparer">The key comparer, if null the default comparer is used</param>
        public SemiFrozenDictionary(IEqualityComparer<TKey> comparer)
            : this(DefaultCapacity, comparer)
        {
        }

        /// <summary>
        /// Create a dictionary with a copy of the entries of another dictionary
        /// </summary>
        /// <param name="other">The entries to copy</param>
        /// <param name="comparer">The key comparer, if null the default comparer is used</param>
        /// <exception cref="ArgumentNullException">Thrown if a key is null, or if <paramref name="other"/> is null (for <paramref name="other"/>: debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="other"/> contains duplicate keys (according to the comparer)</exception>
        public SemiFrozenDictionary(IDictionary<TKey, TValue> other, IEqualityComparer<TKey> comparer)
            : this((IEnumerable<KeyValuePair<TKey, TValue>>)other, comparer)
        {
        }

        /// <summary>
        /// Create a dictionary with a copy of some key-value pairs
        /// </summary>
        /// <param name="other">The entries to copy</param>
        /// <param name="comparer">The key comparer, if null the default comparer is used</param>
        /// <exception cref="ArgumentNullException">Thrown if a key is null, or if <paramref name="other"/> is null (for <paramref name="other"/>: debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="other"/> contains duplicate keys (according to the comparer)</exception>
        public SemiFrozenDictionary(IEnumerable<KeyValuePair<TKey, TValue>> other, IEqualityComparer<TKey> comparer)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(other);
#endif//DEBUG
            Comparer = comparer ?? EqualityComparer<TKey>.Default;
            var u = new ConcurrentDictionary<TKey, TValue>(ConcurrencyLevel, other is ICollection<KeyValuePair<TKey, TValue>> c ? Math.Max(c.Count, DefaultCapacity) : DefaultCapacity, Comparer);
            // Duplicate keys throws (like the Dictionary constructor)
            foreach (var x in other)
                if (!u.TryAdd(x.Key, x.Value))
                    throw new ArgumentException("An item with the same key has already been added. Key: " + x.Key);
            Underlaying = u;
            FreezeAfter = MinReadsBeforeFreeze + ReadsPerItemBeforeFreeze * u.Count;
        }

        /// <summary>
        /// Create an empty dictionary
        /// </summary>
        /// <param name="size">The initial capacity of the underlying dictionary</param>
        /// <param name="comparer">The key comparer, if null the default comparer is used</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="size"/> is negative (release builds report the parameter of the underlying dictionary, "capacity")</exception>
        public SemiFrozenDictionary(int size, IEqualityComparer<TKey> comparer)
        {
#if DEBUG
            ArgumentOutOfRangeException.ThrowIfNegative(size);
#endif//DEBUG
            Comparer = comparer ?? EqualityComparer<TKey>.Default;
            Underlaying = new ConcurrentDictionary<TKey, TValue>(ConcurrencyLevel, size, Comparer);
        }


        /// <summary>
        /// Get or set the value of a key.
        /// Setting adds or replaces the value (takes the lock and invalidates the frozen copy).
        /// </summary>
        /// <param name="key">The key, must not be null</param>
        /// <exception cref="KeyNotFoundException">Thrown by the getter if the key doesn't exist</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
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

        /// <summary>
        /// A snapshot of the keys (a new list on every call, freezes the dictionary if needed)
        /// </summary>
        public ICollection<TKey> Keys => Get().Keys.ToList();

        /// <summary>
        /// A snapshot of the values (a new list on every call, freezes the dictionary if needed)
        /// </summary>
        public ICollection<TValue> Values => Get().Values.ToList();

        /// <summary>
        /// The number of entries (lock free if frozen, else read under the lock)
        /// </summary>
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

        /// <summary>
        /// Always false
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Add a new key (takes the lock and invalidates the frozen copy)
        /// </summary>
        /// <param name="key">The key, must not be null</param>
        /// <param name="value">The value</param>
        /// <exception cref="ArgumentException">Thrown if the key already exists</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
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

        /// <summary>
        /// Add a new key (see <see cref="Add(TKey, TValue)"/>)
        /// </summary>
        /// <param name="item">The key and value</param>
        /// <exception cref="ArgumentException">Thrown if the key already exists</exception>
        /// <exception cref="ArgumentNullException">Thrown if the key is null</exception>
        public void Add(KeyValuePair<TKey, TValue> item)
            => Add(item.Key, item.Value);

        /// <summary>
        /// Add a new key if it doesn't exist (takes the lock, the frozen copy is only invalidated if the key was added)
        /// </summary>
        /// <param name="key">The key, must not be null</param>
        /// <param name="value">The value</param>
        /// <returns>True if the key was added, false if it already existed (the value is not updated)</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
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

        /// <summary>
        /// Add a new key if it doesn't exist (see <see cref="TryAdd(TKey, TValue)"/>)
        /// </summary>
        /// <param name="item">The key and value</param>
        /// <returns>True if the key was added, false if it already existed</returns>
        public bool TryAdd(KeyValuePair<TKey, TValue> item)
            => TryAdd(item.Key, item.Value);

        /// <summary>
        /// Remove all entries (takes the lock and invalidates the frozen copy)
        /// </summary>
        public void Clear()
        {
            var u = Underlaying;
            lock (u)
            {
                u.Clear();
                Changed();
            }
        }

        /// <summary>
        /// Check if a key exists with a specific value (the value is compared using <see cref="EqualityComparer{T}.Default"/>)
        /// </summary>
        /// <param name="item">The key and value to find</param>
        /// <returns>True if the key exists and has the value</returns>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (!TryGetValue(item.Key, out var result))
                return false;
            return EqualityComparer<TValue>.Default.Equals(item.Value, result);
        }

        /// <summary>
        /// Check if a key exists (lock free)
        /// </summary>
        /// <param name="key">The key to find</param>
        /// <returns>True if the key exists</returns>
        public bool ContainsKey(TKey key)
            => TryGetValue(key, out _);

        /// <summary>
        /// Copy a snapshot of the entries to an array (freezes the dictionary if needed)
        /// </summary>
        /// <param name="array">The destination array</param>
        /// <param name="arrayIndex">The index in <paramref name="array"/> to start writing at</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="array"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="arrayIndex"/> is negative or greater than the length of the array</exception>
        /// <exception cref="ArgumentException">Thrown if the destination is too small</exception>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(array);
#endif//DEBUG
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

        /// <summary>
        /// Enumerate a snapshot of the entries (freezes the dictionary if needed), modifications during the enumeration are not visible
        /// </summary>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            => Get().GetEnumerator();

        /// <summary>
        /// Remove a key (takes the lock, the frozen copy is only invalidated if the key was removed)
        /// </summary>
        /// <param name="key">The key to remove, must not be null</param>
        /// <returns>True if the key was removed</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
        public bool Remove(TKey key)
            => TryRemove(key, out _);

        /// <summary>
        /// Remove a key only if it has a specific value (the value is compared using <see cref="EqualityComparer{T}.Default"/>), atomically
        /// </summary>
        /// <param name="item">The key and the expected value</param>
        /// <returns>True if the key was removed</returns>
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

        /// <summary>
        /// Remove a key (takes the lock, the frozen copy is only invalidated if the key was removed)
        /// </summary>
        /// <param name="key">The key to remove, must not be null</param>
        /// <param name="value">The removed value, or default if the key wasn't found</param>
        /// <returns>True if the key was removed</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> is null</exception>
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

        /// <summary>
        /// Get the value of a key (lock free).
        /// Reads the frozen copy if available, else the underlying dictionary (which is frozen after enough reads without modifications).
        /// </summary>
        /// <param name="key">The key to find</param>
        /// <param name="value">The value, or default if the key wasn't found</param>
        /// <returns>True if the key was found</returns>
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
