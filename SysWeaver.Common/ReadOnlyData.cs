using System.Collections.Generic;


namespace SysWeaver
{
    public static class ReadOnlyData
    {

        public static IReadOnlySet<T> EmptySet<T>() => EmptyReadonlySet<T>.Default;
        public static IReadOnlyDictionary<K, V> EmptyDictionary<K, V>() => EmptyReadonlyDictionary<K, V>.Default;

        public static IReadOnlySet<T> Set<T>(IEqualityComparer<T> comparer, IEnumerable<T> data)
        {
            var t = comparer == null ? new HashSet<T>(data) : new HashSet<T>(data, comparer);
            return t.Freeze(comparer);
        }

        public static IReadOnlySet<T> Set<T>(IEnumerable<T> data)
        {
            var t = new HashSet<T>(data);
            return t.Freeze();
        }

        public static IReadOnlySet<T> Set<T>(IEqualityComparer<T> comparer, params T[] data)
        {
            var t = comparer == null ? new HashSet<T>(data) : new HashSet<T>(data, comparer);
            return t.Freeze(comparer);
        }

        public static IReadOnlySet<T> Set<T>(params T[] data)
        {
            var t = new HashSet<T>(data);
            return t.Freeze();
        }


        public static IReadOnlyDictionary<K, V> Dictionary<K, V>(IEqualityComparer<K> comparer, IEnumerable<KeyValuePair<K, V>> data)
        {
            var t = comparer == null ? new Dictionary<K, V>(data) : new Dictionary<K, V>(data, comparer);
            return t.Freeze(comparer);
        }

        public static IReadOnlyDictionary<K, V> Dictionary<K, V>(IEnumerable<KeyValuePair<K, V>> data)
        {
            var t = new Dictionary<K, V>(data);
            return t.Freeze();
        }

        public static IReadOnlyDictionary<K, V> Dictionary<K, V>(IEqualityComparer<K> comparer, params KeyValuePair<K, V>[] data)
        {
            var t = comparer == null ? new Dictionary<K, V>(data) : new Dictionary<K, V>(data, comparer);
            return t.Freeze(comparer);
        }

        public static IReadOnlyDictionary<K, V> Dictionary<K, V>(params KeyValuePair<K, V>[] data)
        {
            var t = new Dictionary<K, V>(data);
            return t.Freeze();
        }


    }


}
