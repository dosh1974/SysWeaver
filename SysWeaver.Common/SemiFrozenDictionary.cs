using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SysWeaver
{

    /// <summary>
    /// Use this dictionary when number of reads far exceeds the number of modificatiions.
    /// This is thread safe in the same sense as a ConcurrentDictionary.
    /// Aall reads are done on a frozen copy of the underlaying dictionary.
    /// Mutating underlaying dictionary is done using locks and the frozen copy is invalidated.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public sealed class SemiFrozenDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        IReadOnlyDictionary<TKey, TValue> Internal;

        IReadOnlyDictionary<TKey, TValue> Get()
        {
            var i = Internal;
            if (i != null)
                return i;
            lock (Underlaying)
            {
                i = Internal;
                if (i != null)
                    return i;
                i = Underlaying.Freeze();
                Internal = i;
                return i;
            }
        }


        readonly Dictionary<TKey, TValue> Underlaying;


        public SemiFrozenDictionary()
        {
            Underlaying = new Dictionary<TKey, TValue>();
        }

        public SemiFrozenDictionary(IDictionary<TKey, TValue> other)
        {
            Underlaying = new Dictionary<TKey, TValue>(other);
        }

        public SemiFrozenDictionary(IEnumerable<KeyValuePair<TKey, TValue>> other)
        {
            Underlaying = new Dictionary<TKey, TValue>(other);
        }

        public SemiFrozenDictionary(int size)
        {
            Underlaying = new Dictionary<TKey, TValue>(size);
        }

        public SemiFrozenDictionary(IEqualityComparer<TKey> comparer)
        {
            Underlaying = new Dictionary<TKey, TValue>(comparer);
        }

        public SemiFrozenDictionary(IDictionary<TKey, TValue> other, IEqualityComparer<TKey> comparer)
        {
            Underlaying = new Dictionary<TKey, TValue>(other, comparer);
        }

        public SemiFrozenDictionary(IEnumerable<KeyValuePair<TKey, TValue>> other, IEqualityComparer<TKey> comparer)
        {
            Underlaying = new Dictionary<TKey, TValue>(other, comparer);
        }

        public SemiFrozenDictionary(int size, IEqualityComparer<TKey> comparer)
        {
            Underlaying = new Dictionary<TKey, TValue>(size, comparer);
        }


        public TValue this[TKey key] 
        { 
            get => Get()[key]; 
            set
            {
                var u = Underlaying;
                lock (u)
                {
                    u[key] = value;
                    Internal = null;
                }
            }
        }

        public ICollection<TKey> Keys => Get().Keys.ToList();

        public ICollection<TValue> Values => Get().Values.ToList();

        public int Count => Get().Count;

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            var u = Underlaying;
            lock (u)
            {
                u.Add(key, value);
                Internal = null;
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            var u = Underlaying;
            lock (u)
            {
                u.Add(item.Key, item.Value);
                Internal = null;
            }
        }

        public void Clear()
        {
            var u = Underlaying;
            lock (u)
            {
                u.Clear();
                Internal = null;
            }
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (!Get().TryGetValue(item.Key, out var result))
                return false;
            return Object.Equals(item.Value, result);
        }

        public bool ContainsKey(TKey key) 
            => Get().ContainsKey(key);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            foreach (var x in Get())
            {
                array[arrayIndex] = x;
                ++arrayIndex;
            }
                
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            => Get().GetEnumerator();

        public bool Remove(TKey key)
        {
            var u = Underlaying;
            lock (u)
            {
                if (!u.Remove(key))
                    return false;
                Internal = null;
            }
            return true;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            var u = Underlaying;
            lock (u)
            {
                if (!u.Contains(item))
                    return false;
                if (!u.Remove(item.Key))
                    return false;
                Internal = null;
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
                Internal = null;
            }
            return true;
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
            => Get().TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator()
            => Get().GetEnumerator();

    }
}
