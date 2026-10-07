using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// Read-only Newtonsoft converter for <see cref="Double"/> and <see cref="Single"/> (not nullable) that reads a json null as NaN.
    /// </summary>
    /// <remarks>
    /// SysWeaver.Json (SwJson) writes NaN and infinities as null (json has no such numbers), so data written by it (like the compact output of the SafeJson serializer) can be read back.
    /// Numbers (and the NaN, Infinity and -Infinity tokens written by older versions) are read as before, nullable members are not affected (null is null).
    /// </remarks>
    sealed class NullAsNaNConverter : JsonConverter
    {
        /// <summary>
        /// The shared instance.
        /// </summary>
        public static readonly NullAsNaNConverter Instance = new NullAsNaNConverter();

        static readonly Object BoxedDoubleNaN = Double.NaN;
        static readonly Object BoxedSingleNaN = Single.NaN;

        /// <summary>
        /// Returns true for <see cref="Double"/> and <see cref="Single"/>.
        /// </summary>
        /// <param name="objectType">The type to test.</param>
        /// <returns>True if <paramref name="objectType"/> is a <see cref="Double"/> or a <see cref="Single"/>.</returns>
        public override bool CanConvert(Type objectType)
            => (objectType == typeof(Double)) || (objectType == typeof(Single));

        /// <summary>
        /// Read a <see cref="Double"/> or a <see cref="Single"/>, null is read as NaN.
        /// </summary>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var isSingle = objectType == typeof(Single);
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                    return isSingle ? BoxedSingleNaN : BoxedDoubleNaN;
                case JsonToken.Float:
                case JsonToken.Integer:
                    //  Double, Decimal or Int64 (a BigInteger isn't IConvertible, it's converted below), a float is rounded through a double (like Newtonsoft does)
                    if (reader.Value is IConvertible c)
                    {
                        var d = c.ToDouble(CultureInfo.InvariantCulture);
                        return isSingle ? (Object)(Single)d : (Object)d;
                    }
                    break;
            }
            //  Anything else (strings etc) is converted like Newtonsoft does for a JValue
            return JToken.Load(reader).ToObject(objectType);
        }

        /// <summary>
        /// Always false, this converter is read only.
        /// </summary>
        public override bool CanWrite => false;

        /// <summary>
        /// Not supported (<see cref="CanWrite"/> is false).
        /// </summary>
        /// <exception cref="NotImplementedException">Always thrown.</exception>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            => throw new NotImplementedException();
    }

}
