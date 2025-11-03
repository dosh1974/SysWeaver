using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// Concurrent collection for keeping track of some counts
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public sealed class ConcurrentCount<TKey> : ICollection<KeyValuePair<TKey, long>>
    {

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
        /// <param name="key"></param>
        /// <returns>The incremented value</returns>
        public long IncValue(TKey key) =>
            Interlocked.Increment(ref Get(key).Value);

        /// <summary>
        /// Decrement the value at key
        /// </summary>
        /// <param name="key"></param>
        /// <returns>The decremented value</returns>
        public long DecValue(TKey key) =>
            Interlocked.Decrement(ref Get(key).Value);

        /// <summary>
        /// Add the value at key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">The value to add</param>
        /// <returns>The value after addition</returns>
        public long AddValue(TKey key, long value) =>
            Interlocked.Add(ref Get(key).Value, value);

        /// <summary>
        /// And a value to the value at the key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">The value to and</param>
        /// <returns>The value after and</returns>
        public long AndValue(TKey key, long value) =>
            Interlocked.And(ref Get(key).Value, value);

        /// <summary>
        /// Or a value to the value at the key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">The value to or</param>
        /// <returns>The value after or</returns>
        public long OrValue(TKey key, long value) =>
            Interlocked.Or(ref Get(key).Value, value);

        /// <summary>
        /// Exchange a value if the current value matches a comparand.
        /// newValue = currentValue == comparand ? value : curretValue
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">The value to replace with</param>
        /// <param name="comparand">The value to compare with</param>
        /// <returns>The orignal value</returns>
        public long CompareExchangeValue(TKey key, long value, long comparand) =>
            Interlocked.CompareExchange(ref Get(key).Value, value, comparand);

        /// <summary>
        /// Replace the value at the key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">The value to replace to</param>
        /// <returns>The orignal value</returns>
        public long ExchangeValue(TKey key, long value) =>
            Interlocked.Exchange(ref Get(key).Value, value);

        /// <summary>
        /// Takes the maximum value of the current and supplied value
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">The value to max with</param>
        /// <returns>The value after max</returns>        
        public long MaxValue(TKey key, long value) =>
            InterlockedEx.Max(ref Get(key).Value, value);

        /// <summary>
        /// Takes the minimum value of the current and supplied value
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">The value to min with</param>
        /// <returns>The value after min</returns>        
        public long MinValue(TKey key, long value) =>
            InterlockedEx.Min(ref Get(key).Value, value);

        /// <summary>
        /// Returns the current count of an item, 0 if not found
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public long GetValue(TKey key)
        {
            if (!Counts.TryGetValue(key, out var c))
                return 0;
            return Interlocked.Read(ref c.Value);
        }
        
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

        public void Add(TKey key, long value)
        {
            if (!Counts.TryAdd(key, new ConCount { Value = value }))
                throw new ArgumentException("The key already exists in the collection");
        }

        #region ICollection

        public void Add(KeyValuePair<TKey, long> item)
        {
            if (!Counts.TryAdd(item.Key, new ConCount { Value = item.Value }))
                throw new ArgumentException("The key already exists in the collection");
        }

        public void Clear()
            => Counts.Clear();

        public bool Contains(KeyValuePair<TKey, long> item)
        {
            if (!Counts.TryGetValue(item.Key, out var c))
                return false;
            return Interlocked.Read(ref c.Value) == item.Value;
        }
            
        public void CopyTo(KeyValuePair<TKey, long>[] array, int arrayIndex)
        {
            foreach (var x in Counts)
            {
                array[arrayIndex] = new KeyValuePair<TKey, long>(x.Key, Interlocked.Read(ref x.Value.Value));
                ++arrayIndex;
            }
        }

        public bool Remove(KeyValuePair<TKey, long> item)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<TKey, long>> GetEnumerator()
        {
            foreach (var x in Counts)
                yield return new KeyValuePair<TKey, long>(x.Key, Interlocked.Read(ref x.Value.Value));
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        public int Count => Counts.Count;

        public bool IsReadOnly => false;

        #endregion//IDictionary 

    }

}
