using System;
using System.Collections.Generic;


namespace SysWeaver
{
    /// <summary>
    /// Creates read only (frozen) sets and dictionaries
    /// </summary>
    /// <remarks>
    /// The returned instances are immutable, thread safe and optimized for look ups
    /// (see <see cref="SetExt.Freeze{K}(IReadOnlySet{K}, IEqualityComparer{K})"/> and <see cref="DictionaryExt.Freeze{K, V}(IReadOnlyDictionary{K, V}, IEqualityComparer{K})"/>).
    /// </remarks>
    public static class ReadOnlyData
    {

        /// <summary>
        /// Get an empty read only set (using the default comparer)
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <returns>A shared empty set instance (no allocation is made)</returns>
        public static IReadOnlySet<T> EmptySet<T>() => EmptyReadonlySet<T>.Default;

        /// <summary>
        /// Get an empty read only dictionary (using the default comparer)
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <returns>A shared empty dictionary instance (no allocation is made)</returns>
        public static IReadOnlyDictionary<K, V> EmptyDictionary<K, V>() => EmptyReadonlyDictionary<K, V>.Default;

        /// <summary>
        /// Create a read only (frozen) set from some data
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="comparer">The comparer to use, if null the default comparer is used</param>
        /// <param name="data">The elements to add to the set, duplicates (according to the comparer) are ignored</param>
        /// <returns>A read only set containing the unique elements</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null</exception>
        public static IReadOnlySet<T> Set<T>(IEqualityComparer<T> comparer, IEnumerable<T> data)
        {
            var t = comparer == null ? new HashSet<T>(data) : new HashSet<T>(data, comparer);
            return t.Freeze(t.Comparer);
        }

        /// <summary>
        /// Create a read only (frozen) set from some data, using the default comparer
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="data">The elements to add to the set, duplicates are ignored</param>
        /// <returns>A read only set containing the unique elements</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null</exception>
        public static IReadOnlySet<T> Set<T>(IEnumerable<T> data)
        {
            var t = new HashSet<T>(data);
            return t.Freeze();
        }

        /// <summary>
        /// Create a read only (frozen) set from some data
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="comparer">The comparer to use, if null the default comparer is used</param>
        /// <param name="data">The elements to add to the set, duplicates (according to the comparer) are ignored</param>
        /// <returns>A read only set containing the unique elements</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null</exception>
        public static IReadOnlySet<T> Set<T>(IEqualityComparer<T> comparer, params T[] data)
        {
            var t = comparer == null ? new HashSet<T>(data) : new HashSet<T>(data, comparer);
            return t.Freeze(t.Comparer);
        }

        /// <summary>
        /// Create a read only (frozen) set from some data, using the default comparer
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="data">The elements to add to the set, duplicates are ignored</param>
        /// <returns>A read only set containing the unique elements</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null</exception>
        public static IReadOnlySet<T> Set<T>(params T[] data)
        {
            var t = new HashSet<T>(data);
            return t.Freeze();
        }


        /// <summary>
        /// Create a read only (frozen) dictionary from some key-value pairs
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="comparer">The key comparer to use, if null the default comparer is used</param>
        /// <param name="data">The key-value pairs to add</param>
        /// <returns>A read only dictionary containing the key-value pairs</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null or if any key is null</exception>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="data"/> contains duplicate keys (according to the comparer)</exception>
        public static IReadOnlyDictionary<K, V> Dictionary<K, V>(IEqualityComparer<K> comparer, IEnumerable<KeyValuePair<K, V>> data)
        {
            var t = comparer == null ? new Dictionary<K, V>(data) : new Dictionary<K, V>(data, comparer);
            return t.Freeze(t.Comparer);
        }

        /// <summary>
        /// Create a read only (frozen) dictionary from some key-value pairs, using the default key comparer
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="data">The key-value pairs to add</param>
        /// <returns>A read only dictionary containing the key-value pairs</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null or if any key is null</exception>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="data"/> contains duplicate keys</exception>
        public static IReadOnlyDictionary<K, V> Dictionary<K, V>(IEnumerable<KeyValuePair<K, V>> data)
        {
            var t = new Dictionary<K, V>(data);
            return t.Freeze();
        }

        /// <summary>
        /// Create a read only (frozen) dictionary from some key-value pairs
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="comparer">The key comparer to use, if null the default comparer is used</param>
        /// <param name="data">The key-value pairs to add</param>
        /// <returns>A read only dictionary containing the key-value pairs</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null or if any key is null</exception>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="data"/> contains duplicate keys (according to the comparer)</exception>
        public static IReadOnlyDictionary<K, V> Dictionary<K, V>(IEqualityComparer<K> comparer, params KeyValuePair<K, V>[] data)
        {
            var t = comparer == null ? new Dictionary<K, V>(data) : new Dictionary<K, V>(data, comparer);
            return t.Freeze(t.Comparer);
        }

        /// <summary>
        /// Create a read only (frozen) dictionary from some key-value pairs, using the default key comparer
        /// </summary>
        /// <typeparam name="K">The key type</typeparam>
        /// <typeparam name="V">The value type</typeparam>
        /// <param name="data">The key-value pairs to add</param>
        /// <returns>A read only dictionary containing the key-value pairs</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null or if any key is null</exception>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="data"/> contains duplicate keys</exception>
        public static IReadOnlyDictionary<K, V> Dictionary<K, V>(params KeyValuePair<K, V>[] data)
        {
            var t = new Dictionary<K, V>(data);
            return t.Freeze();
        }


    }


}
