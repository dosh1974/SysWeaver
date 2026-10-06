using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// Concurrent collection for keeping track of some counts (a 64-bit counter per key), all operations on a counter are atomic.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys</typeparam>
    /// <remarks>
    /// Counters are created on first use (starting at zero) by the modifying methods, there is no way to remove a single key (use <see cref="Clear"/>).
    /// Concurrent modifications of a key while <see cref="Clear"/> is executing may be lost.
    /// </remarks>
    public sealed class ConcurrentCount<TKey> : ICollection<KeyValuePair<TKey, long>>
    {

        /// <summary>
        /// Create a new empty count collection
        /// </summary>
        /// <param name="comparer">The comparer to use for the keys, null to use the default equality comparer</param>
        public ConcurrentCount(IEqualityComparer<TKey> comparer = null)
        {
            Counts = comparer == null ? new ConcurrentDictionary<TKey, ConCount>() : new ConcurrentDictionary<TKey, ConCount>(comparer);
        }

        ConCount Get(TKey key)
        {
            var c = Counts;
            if (c.TryGetValue(key, out var cc))
                return cc;
            cc = new ConCount();
            if (c.TryAdd(key, cc))
                return cc;
            if (!c.TryGetValue(key, out cc))
                throw new Exception("Internal error!");
            return cc;
        }


        sealed class ConCount
        {
            public long Value;
        }

        readonly ConcurrentDictionary<TKey, ConCount> Counts;

        /// <summary>
        /// Increment the value at key
        /// </summary>
        /// <param name="key">The key of the counter (created with a value of zero if it doesn't exist)</param>
        /// <returns>The incremented value</returns>
        public long IncValue(TKey key) =>
            Interlocked.Increment(ref Get(key).Value);

        /// <summary>
        /// Decrement the value at key
        /// </summary>
        /// <param name="key">The key of the counter (created with a value of zero if it doesn't exist)</param>
        /// <returns>The decremented value</returns>
        public long DecValue(TKey key) =>
            Interlocked.Decrement(ref Get(key).Value);

        /// <summary>
        /// Add the value at key
        /// </summary>
        /// <param name="key">The key of the counter (created with a value of zero if it doesn't exist)</param>
        /// <param name="value">The value to add</param>
        /// <returns>The value after addition</returns>
        public long AddValue(TKey key, long value) =>
            Interlocked.Add(ref Get(key).Value, value);

        /// <summary>
        /// Bitwise and a value with the value at the key
        /// </summary>
        /// <param name="key">The key of the counter (created with a value of zero if it doesn't exist)</param>
        /// <param name="value">The value to and</param>
        /// <returns>The original value (before the and)</returns>
        public long AndValue(TKey key, long value) =>
            Interlocked.And(ref Get(key).Value, value);

        /// <summary>
        /// Bitwise or a value with the value at the key
        /// </summary>
        /// <param name="key">The key of the counter (created with a value of zero if it doesn't exist)</param>
        /// <param name="value">The value to or</param>
        /// <returns>The original value (before the or)</returns>
        public long OrValue(TKey key, long value) =>
            Interlocked.Or(ref Get(key).Value, value);

        /// <summary>
        /// Exchange a value if the current value matches a comparand.
        /// newValue = currentValue == comparand ? value : currentValue
        /// </summary>
        /// <param name="key">The key of the counter (created with a value of zero if it doesn't exist)</param>
        /// <param name="value">The value to replace with</param>
        /// <param name="comparand">The value to compare with</param>
        /// <returns>The original value</returns>
        public long CompareExchangeValue(TKey key, long value, long comparand) =>
            Interlocked.CompareExchange(ref Get(key).Value, value, comparand);

        /// <summary>
        /// Replace the value at the key
        /// </summary>
        /// <param name="key">The key of the counter (created with a value of zero if it doesn't exist)</param>
        /// <param name="value">The value to replace to</param>
        /// <returns>The original value</returns>
        public long ExchangeValue(TKey key, long value) =>
            Interlocked.Exchange(ref Get(key).Value, value);

        /// <summary>
        /// Takes the maximum value of the current and supplied value
        /// </summary>
        /// <param name="key">The key of the counter (created with a value of zero if it doesn't exist)</param>
        /// <param name="value">The value to max with</param>
        /// <returns>The value after max</returns>        
        public long MaxValue(TKey key, long value) =>
            InterlockedEx.Max(ref Get(key).Value, value);

        /// <summary>
        /// Takes the minimum value of the current and supplied value
        /// </summary>
        /// <param name="key">The key of the counter (created with a value of zero if it doesn't exist)</param>
        /// <param name="value">The value to min with</param>
        /// <returns>The value after min</returns>        
        public long MinValue(TKey key, long value) =>
            InterlockedEx.Min(ref Get(key).Value, value);

        /// <summary>
        /// Returns the current count of an item, 0 if not found
        /// </summary>
        /// <param name="key">The key of the counter</param>
        /// <returns>The current value, 0 if the key doesn't exist (the key is not created)</returns>
        public long GetValue(TKey key)
        {
            if (!Counts.TryGetValue(key, out var c))
                return 0;
            return Interlocked.Read(ref c.Value);
        }
        
        /// <summary>
        /// Get the current value of a counter (without creating it)
        /// </summary>
        /// <param name="key">The key of the counter</param>
        /// <param name="value">The current value, or zero if the key doesn't exist</param>
        /// <returns>True if the key exists</returns>
        public bool TryGetValue(TKey key, out long value)
        {
            if (!Counts.TryGetValue(key, out var c))
            {
                value = default;
                return false;
            }
            value = Interlocked.Read(ref c.Value);
            return true;
        }

        /// <summary>
        /// Add a new counter with an initial value
        /// </summary>
        /// <param name="key">The key of the counter</param>
        /// <param name="value">The initial value</param>
        /// <exception cref="ArgumentException">The key already exists</exception>
        public void Add(TKey key, long value)
        {
            if (!Counts.TryAdd(key, new ConCount { Value = value }))
                throw new ArgumentException("The key already exists in the collection");
        }

        #region ICollection

        /// <summary>
        /// Add a new counter with an initial value
        /// </summary>
        /// <param name="item">The key and the initial value</param>
        /// <exception cref="ArgumentException">The key already exists</exception>
        public void Add(KeyValuePair<TKey, long> item)
        {
            if (!Counts.TryAdd(item.Key, new ConCount { Value = item.Value }))
                throw new ArgumentException("The key already exists in the collection");
        }

        /// <summary>
        /// Remove all counters
        /// </summary>
        public void Clear()
            => Counts.Clear();

        /// <summary>
        /// Check if a key exists and it's counter have a specific value
        /// </summary>
        /// <param name="item">The key and the value to compare with</param>
        /// <returns>True if the key exists and have the given value</returns>
        public bool Contains(KeyValuePair<TKey, long> item)
        {
            if (!Counts.TryGetValue(item.Key, out var c))
                return false;
            return Interlocked.Read(ref c.Value) == item.Value;
        }
            
        /// <summary>
        /// Copy a snapshot of all keys and values to an array
        /// </summary>
        /// <param name="array">The array to copy to</param>
        /// <param name="arrayIndex">The index of the first element to write</param>
        /// <exception cref="IndexOutOfRangeException">The array is too small (no up front validation is performed, elements may have been written)</exception>
        public void CopyTo(KeyValuePair<TKey, long>[] array, int arrayIndex)
        {
            foreach (var x in Counts)
            {
                array[arrayIndex] = new KeyValuePair<TKey, long>(x.Key, Interlocked.Read(ref x.Value.Value));
                ++arrayIndex;
            }
        }

        /// <summary>
        /// Not supported
        /// </summary>
        /// <param name="item">Ignored</param>
        /// <returns>Never returns</returns>
        /// <exception cref="NotImplementedException">Always thrown</exception>
        public bool Remove(KeyValuePair<TKey, long> item)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Enumerate all keys and their current values (a moment in time snapshot is not guaranteed, concurrent changes may or may not be seen)
        /// </summary>
        /// <returns>An enumerator</returns>
        public IEnumerator<KeyValuePair<TKey, long>> GetEnumerator()
        {
            foreach (var x in Counts)
                yield return new KeyValuePair<TKey, long>(x.Key, Interlocked.Read(ref x.Value.Value));
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        /// <summary>
        /// Number of keys
        /// </summary>
        public int Count => Counts.Count;

        /// <summary>
        /// Always false
        /// </summary>
        public bool IsReadOnly => false;

        #endregion//IDictionary 

    }

}
