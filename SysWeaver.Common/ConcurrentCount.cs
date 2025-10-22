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

        public long IncValue(TKey key) =>
            Interlocked.Increment(ref Get(key).Value);

        public long DecValue(TKey key) =>
            Interlocked.Decrement(ref Get(key).Value);

        public long AddValue(TKey key, long value) =>
            Interlocked.Add(ref Get(key).Value, value);

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
