using System;
using System.Collections;
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
    /// The first read after a modification freezes it again (the intended usage is a few modifications at startup / shutdown, and reads only at runtime).
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public sealed class SemiFrozenDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        /// <summary>
        /// The frozen copy, null after a modification (until the next read freezes it again)
        /// </summary>
        IReadOnlyDictionary<TKey, TValue> Internal;

        /// <summary>
        /// Get the frozen copy (freeze it if needed)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IReadOnlyDictionary<TKey, TValue> Get()
            => Internal ?? Freeze();

        /// <summary>
        /// Freeze the underlaying dictionary (the first read after a modification)
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        IReadOnlyDictionary<TKey, TValue> Freeze()
        {
            lock (Underlaying)
            {
                var i = Internal;
                if (i != null)
                    return i;
                i = Underlaying.Freeze();
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
                u.Add(key, value);
                Changed();
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            var u = Underlaying;
            lock (u)
            {
                u.Add(item.Key, item.Value);
                Changed();
            }
        }

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
        {
            var u = Underlaying;
            lock (u)
            {
                if (!u.TryAdd(item.Key, item.Value))
                    return false;
                Changed();
                return true;
            }
        }

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
            => Get().ContainsKey(key);

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
        {
            var u = Underlaying;
            lock (u)
            {
                if (!u.Remove(key))
                    return false;
                Changed();
            }
            return true;
        }

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
                if (!u.Remove(key, out value))
                    return false;
                Changed();
            }
            return true;
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
            => Get().TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator()
            => Get().GetEnumerator();

    }
}
