using System.Collections.Generic;

namespace SysWeaver
{
    public sealed class List<TKey, TValue>
    {
        public List(IEqualityComparer<TKey> comparer = null)
        {
            Values = comparer == null ? new Dictionary<TKey, List<TValue>>() : new Dictionary<TKey, List<TValue>>(comparer);
        }

        public void Add(TKey key, TValue value)
        {
            if (!Values.TryGetValue(key, out var v))
            {
                v = new List<TValue>();
                Values.Add(key, v);
            }
            v.Add(value);
        }

        public int KeyCount => Values.Count;

        public ICollection<TKey> Keys => Values.Keys;

        public List<TValue> Get(TKey key)
            =>
            Values.TryGetValue(key, out var v) ? v : null;

        public bool TryGet(TKey key, out List<TValue> values) 
            => Values.TryGetValue(key, out values);

        public bool TryRemove(TKey key, out List<TValue> values)
        {
            var v = Values;
            if (!v.TryGetValue(key, out values))
                return false;
            v.Remove(key);
            return true;
        }

        public readonly Dictionary<TKey, List<TValue>> Values;
    }
}
