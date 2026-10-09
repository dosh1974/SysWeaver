using System.Collections.Generic;

namespace SysWeaver.Inspection.Implementation
{

    /// <summary>
    /// Describes value types that can't be described by their fields (readonly fields)
    /// </summary>
    static class ValueTypeHandlers
    {
        /// <summary>
        /// Nullable&lt;U&gt;, only called with a value (null is handled by the inspector), the value is always set (has a value after reading)
        /// </summary>
        public static void Describe_Nullable<U>(IInspectorImplementation i, ref U? value, RegFieldDelegate<U> regValue) where U : struct
        {
            var v = value.GetValueOrDefault();
            regValue(i, ref v);
            value = v;
        }

        /// <summary>
        /// KeyValuePair&lt;K, V&gt;, the key and then the value
        /// </summary>
        public static void Describe_KeyValuePair<K, V>(IInspectorImplementation i, ref KeyValuePair<K, V> value, RegFieldDelegate<K> regKey, RegFieldDelegate<V> regValue)
        {
            var k = value.Key;
            var v = value.Value;
            regKey(i, ref k);
            regValue(i, ref v);
            value = new KeyValuePair<K, V>(k, v);
        }
    }

}
