using System.Collections.Generic;

namespace SysWeaver
{

    /// <summary>
    /// A multi-map: a dictionary that maps each key to a <see cref="List{T}"/> of values.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <remarks>
    /// Not thread safe.
    /// The lists returned by <see cref="Get"/>, <see cref="TryGet"/> and <see cref="TryRemove"/> are the live internal lists (no copy is made), modifying them modifies the collection.
    /// Values are kept in insertion order per key, and duplicate values are allowed.
    /// </remarks>
    public sealed class DictionaryList<TKey, TValue>
    {
        /// <summary>
        /// Create an empty multi-map.
        /// </summary>
        /// <param name="comparer">The key comparer to use, if null the default comparer is used.</param>
        public DictionaryList(IEqualityComparer<TKey> comparer = null)
        {
            Values = comparer == null ? new Dictionary<TKey, List<TValue>>() : new Dictionary<TKey, List<TValue>>(comparer);
        }

        /// <summary>
        /// Append a value to the list of values for a key, creating the list if the key is new.
        /// </summary>
        /// <param name="key">The key, must not be null.</param>
        /// <param name="value">The value to append.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="key"/> is null.</exception>
        public void Add(TKey key, TValue value)
        {
            if (!Values.TryGetValue(key, out var v))
            {
                v = new List<TValue>();
                Values.Add(key, v);
            }
            v.Add(value);
        }

        /// <summary>
        /// The number of distinct keys.
        /// </summary>
        public int KeyCount => Values.Count;

        /// <summary>
        /// The distinct keys (a live view of the underlying dictionary keys).
        /// </summary>
        public ICollection<TKey> Keys => Values.Keys;

        /// <summary>
        /// Get the values for a key.
        /// </summary>
        /// <param name="key">The key to look up, must not be null.</param>
        /// <returns>The live list of values for the key, or null if the key isn't present.</returns>
        public List<TValue> Get(TKey key)
            =>
            Values.TryGetValue(key, out var v) ? v : null;

        /// <summary>
        /// Try to get the values for a key.
        /// </summary>
        /// <param name="key">The key to look up, must not be null.</param>
        /// <param name="values">The live list of values for the key, or null if the key isn't present.</param>
        /// <returns>True if the key was found.</returns>
        public bool TryGet(TKey key, out List<TValue> values) 
            => Values.TryGetValue(key, out values);

        /// <summary>
        /// Remove a key and all of its values.
        /// </summary>
        /// <param name="key">The key to remove, must not be null.</param>
        /// <param name="values">The list of values that was associated with the key, or null if the key wasn't present.</param>
        /// <returns>True if the key was found and removed.</returns>
        public bool TryRemove(TKey key, out List<TValue> values)
        {
            var v = Values;
            if (!v.TryGetValue(key, out values))
                return false;
            v.Remove(key);
            return true;
        }

        /// <summary>
        /// The underlying dictionary, mapping each key to its list of values.
        /// Can be used directly for enumeration or advanced manipulation.
        /// </summary>
        public readonly Dictionary<TKey, List<TValue>> Values;
    }


}
