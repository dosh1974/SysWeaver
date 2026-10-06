using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace SysWeaver.Data
{
    /// <summary>
    /// Strict string to boxed value conversions, used to convert filter values and cell texts to typed values.
    /// The individual <c>ToXxx</c> methods throw on invalid input, <see cref="ToType(Type, string)"/> doesn't.
    /// </summary>
    /// <remarks>
    /// <see cref="Single"/>, <see cref="Double"/> and <see cref="Decimal"/> are parsed using the invariant culture,
    /// other numbers and date/time values using the current culture.
    /// Compare with <see cref="StringConverter"/> that never throws.
    /// </remarks>
    public static class StringToObject
    {

        /// <summary>
        /// Parse a <see cref="SByte"/> using the current culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        /// <exception cref="OverflowException">The value is out of range for the type.</exception>
        public static SByte ToSByte(String text)
        {
            return SByte.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="Byte"/> using the current culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        /// <exception cref="OverflowException">The value is out of range for the type.</exception>
        public static Byte ToByte(String text)
        {
            return Byte.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="Int16"/> using the current culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        /// <exception cref="OverflowException">The value is out of range for the type.</exception>
        public static Int16 ToInt16(String text)
        {
            return Int16.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="UInt16"/> using the current culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        /// <exception cref="OverflowException">The value is out of range for the type.</exception>
        public static UInt16 ToUInt16(String text)
        {
            return UInt16.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="Int32"/> using the current culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        /// <exception cref="OverflowException">The value is out of range for the type.</exception>
        public static Int32 ToInt32(String text)
        {
            return Int32.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="UInt32"/> using the current culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        /// <exception cref="OverflowException">The value is out of range for the type.</exception>
        public static UInt32 ToUInt32(String text)
        {
            return UInt32.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="Int64"/> using the current culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        /// <exception cref="OverflowException">The value is out of range for the type.</exception>
        public static Int64 ToInt64(String text)
        {
            return Int64.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="UInt64"/> using the current culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        /// <exception cref="OverflowException">The value is out of range for the type.</exception>
        public static UInt64 ToUInt64(String text)
        {
            return UInt64.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="Single"/> using the invariant culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        /// <exception cref="OverflowException">The value is out of range for the type.</exception>
        public static Single ToSingle(String text)
        {
            return Single.Parse(text, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parse a <see cref="Double"/> using the invariant culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        /// <exception cref="OverflowException">The value is out of range for the type.</exception>
        public static Double ToDouble(String text)
        {
            return Double.Parse(text, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parse a <see cref="Decimal"/> using the invariant culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        /// <exception cref="OverflowException">The value is out of range for the type.</exception>
        public static Decimal ToDecimal(String text)
        {
            return Decimal.Parse(text, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parse a <see cref="Boolean"/> (case insensitive "true"/"false").
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        public static Boolean ToBoolean(String text)
        {
            return Boolean.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="TimeSpan"/> using the current culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        /// <exception cref="OverflowException">The value is out of range for the type.</exception>
        public static TimeSpan ToTimeSpan(String text)
        {
            return TimeSpan.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="DateTime"/> using the current culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        public static DateTime ToDateTime(String text)
        {
            return DateTime.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="TimeOnly"/> using the current culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        public static TimeOnly ToTimeOnly(String text)
        {
            return TimeOnly.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="DateOnly"/> using the current culture.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        public static DateOnly ToDateOnly(String text)
        {
            return DateOnly.Parse(text);
        }

        /// <summary>
        /// Parse a <see cref="Guid"/>.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="FormatException"><paramref name="text"/> is not in a valid format.</exception>
        public static Guid ToGuid(String text)
        {
            return Guid.Parse(text);
        }


        /// <summary>
        /// Convert a text to a boxed value of the given type.
        /// </summary>
        /// <param name="t">The destination type.</param>
        /// <param name="text">The text to convert.</param>
        /// <returns>The converted value, the default value of <paramref name="t"/> if the conversion failed,
        /// or <paramref name="text"/> itself if the type isn't supported.</returns>
        public static Object ToType(Type t, String text)
        {
            if (!Internal.TryGetValue(t, out var c))
                return text;
            try
            {
                return c(text);
            }
            catch
            {
            }
            return GetDefault(t);
        }

        /// <summary>
        /// Get the (cached) boxed default value of a type.
        /// </summary>
        /// <param name="t">The type.</param>
        /// <returns>Null for reference types and <see cref="Nullable{T}"/>, else a cached boxed default instance (shared, don't mutate it).</returns>
        public static Object GetDefault(Type t)
        {
            if (!t.IsValueType)
                return null;
            var d = Defaults;
            if (d.TryGetValue(t, out var ro))
                return ro;
            if (Nullable.GetUnderlyingType(t) == null)
                ro = Activator.CreateInstance(t);
            d.TryAdd(t, ro);
            return ro;
        }

        static readonly ConcurrentDictionary<Type, Object> Defaults = new();


        /// <summary>
        /// Get the boxed default value of a type (not cached, a new instance is created for value types).
        /// </summary>
        /// <param name="t">The type.</param>
        /// <returns>Null for reference types and <see cref="Nullable{T}"/>, else a new boxed default instance.</returns>
        public static object GetDefaultValue(this Type t)
        {
            if (t.IsValueType && Nullable.GetUnderlyingType(t) == null)
                return Activator.CreateInstance(t);
            else
                return null;
        }

        /// <summary>
        /// Check if a text can be converted to the given type using <see cref="ToType(Type, string)"/>.
        /// </summary>
        /// <param name="t">The destination type.</param>
        /// <returns>True if the type is supported.</returns>
        public static bool CanConvert(Type t) => Internal.ContainsKey(t);

        /// <summary>
        /// Get the (throwing) conversion function for a type.
        /// </summary>
        /// <param name="t">The destination type.</param>
        /// <param name="converter">The conversion function, or null if the type isn't supported.</param>
        /// <returns>True if the type is supported.</returns>
        public static bool TryGetConverter(Type t, out Func<String, Object> converter) => Internal.TryGetValue(t, out converter);

        static readonly IReadOnlyDictionary<Type, Func<String, Object>> Internal = new Dictionary<Type, Func<String, Object>>
        {
            { typeof(SByte), new Func<String, Object>(x => ToSByte(x)) },
            { typeof(Byte), new Func<String, Object>(x => ToByte(x)) },
            { typeof(Int16), new Func<String, Object>(x => ToInt16(x)) },
            { typeof(UInt16), new Func<String, Object>(x => ToUInt16(x)) },
            { typeof(Int32), new Func<String, Object>(x => ToInt32(x)) },
            { typeof(UInt32), new Func<String, Object>(x => ToUInt32(x)) },
            { typeof(Int64), new Func<String, Object>(x => ToInt64(x)) },
            { typeof(UInt64), new Func<String, Object>(x => ToUInt64(x)) },
            { typeof(Single), new Func<String, Object>(x => ToSingle(x)) },
            { typeof(Double), new Func<String, Object>(x => ToDouble(x)) },
            { typeof(Decimal), new Func<String, Object>(x => ToDecimal(x)) },

            { typeof(Boolean), new Func<String, Object>(x => ToBoolean(x)) },
            { typeof(TimeSpan), new Func<String, Object>(x => ToTimeSpan(x)) },
            { typeof(DateTime), new Func<String, Object>(x => ToDateTime(x)) },
            { typeof(TimeOnly), new Func<String, Object>(x => ToTimeOnly(x)) },
            { typeof(DateOnly), new Func<String, Object>(x => ToDateOnly(x)) },
            { typeof(Guid), new Func<String, Object>(x => ToGuid(x)) },
        }.Freeze();

    }


    /// <summary>
    /// Creates (cached) functions that convert boxed values between types using <see cref="Convert.ChangeType(object, Type)"/>.
    /// </summary>
    public static class ObjectConverter
    {

        static readonly ConcurrentDictionary<Tuple<Type, Type>, Func<Object, Object>> TypeConverters = new ConcurrentDictionary<Tuple<Type, Type>, Func<object, object>>();

        /// <summary>
        /// Get a function that converts a boxed value of type <paramref name="from"/> to type <paramref name="to"/>.
        /// </summary>
        /// <param name="from">The source type, only non-nullable value types are supported.</param>
        /// <param name="to">The destination type.</param>
        /// <returns>A conversion function (using <see cref="Convert.ChangeType(object, Type)"/> with the current culture), or null if
        /// <paramref name="from"/> isn't a non-nullable value type or if converting its default value to <paramref name="to"/> fails.
        /// The result (including null) is cached per type pair.</returns>
        /// <remarks>The returned function may still throw for individual values (ex: overflow).</remarks>
        public static Func<Object, Object> TryGetConverter(Type from, Type to)        
        {
            var key = Tuple.Create(from, to);
            if (TypeConverters.TryGetValue(key, out var cc))
                return cc;
            /*if (from == typeof(String))
            {
                if (StringToObject.TryGetConverter(to, out var func))
                {
                    cc = value => func((String)value);
                    TypeConverters.TryAdd(key, cc);
                    return cc;
                }
            }*/
            var def = StringToObject.GetDefault(from);
            if ((def == null) || (def.GetType() != from))
            {
                TypeConverters.TryAdd(key, cc);
                return cc;
            }
            try
            {
                var cv = Convert.ChangeType(def, to);
                if ((cv == null) || (cv.GetType() != to))
                {
                    TypeConverters.TryAdd(key, cc);
                    return cc;
                }
                cc = value => Convert.ChangeType(value, to);
            }
            catch
            {
            }
            TypeConverters.TryAdd(key, cc);
            return cc;
        }

    }

}
