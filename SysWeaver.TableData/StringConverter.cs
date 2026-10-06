using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace SysWeaver.Data
{
    /// <summary>
    /// Lenient string to value conversions that never throw (invalid or null input gives the default value).
    /// The methods are looked up by return type using <see cref="GetMethod(Type)"/>, typically to build expression trees (see <see cref="StringConverterExp"/>).
    /// </summary>
    /// <remarks>
    /// Floating point, decimal, date/time and Guid values are parsed using the invariant culture, integers using the current culture.
    /// Compare with <see cref="StringToObject"/> that throws on invalid input.
    /// </remarks>
    public static class StringConverter
    {
        /// <summary>
        /// Parse a <see cref="SByte"/> using the current culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static SByte ToSByte(String value)
        {
            return ((value != null) && SByte.TryParse(value.Trim(), out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="Int16"/> using the current culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static Int16 ToInt16(String value)
        {
            return ((value != null) && Int16.TryParse(value.Trim(), out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="Int32"/> using the current culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static Int32 ToInt32(String value)
        {
            return ((value != null) && Int32.TryParse(value.Trim(), out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="Int64"/> using the current culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static Int64 ToInt64(String value)
        {
            return ((value != null) && Int64.TryParse(value.Trim(), out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="Byte"/> using the current culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static Byte ToByte(String value)
        {
            return ((value != null) && Byte.TryParse(value.Trim(), out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="UInt16"/> using the current culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static UInt16 ToUInt16(String value)
        {
            return ((value != null) && UInt16.TryParse(value.Trim(), out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="UInt32"/> using the current culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static UInt32 ToUInt32(String value)
        {
            return ((value != null) && UInt32.TryParse(value.Trim(), out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="UInt64"/> using the current culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static UInt64 ToUInt64(String value)
        {
            return ((value != null) && UInt64.TryParse(value.Trim(), out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="Single"/> using the invariant culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static Single ToSingle(String value)
        {
            return ((value != null) && Single.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="Double"/> using the invariant culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static Double ToDouble(String value)
        {
            return ((value != null) && Double.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="Decimal"/> using the invariant culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static Decimal ToDecimal(String value)
        {
            return ((value != null) && Decimal.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="DateTime"/> using the invariant culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static DateTime ToDateTime(String value)
        {
            return ((value != null) && DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="TimeSpan"/> using the invariant culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static TimeSpan ToTimeSpan(String value)
        {
            return ((value != null) && TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="DateOnly"/> using the invariant culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static DateOnly ToDateOnly(String value)
        {
            return ((value != null) && DateOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="TimeOnly"/> using the invariant culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static TimeOnly ToTimeOnly(String value)
        {
            return ((value != null) && TimeOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="Guid"/> using the invariant culture, ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static Guid ToGuid(String value)
        {
            return ((value != null) && Guid.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        /// <summary>
        /// Parse a <see cref="Boolean"/> (case insensitive "true"/"false"), ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static Boolean ToBoolean(String value)
        {
            return ((value != null) && Boolean.TryParse(value.Trim(), out var val)) ? val : default;
        }

        /// <summary>
        /// Parse an enum value by name or number (case insensitive), ignoring leading and trailing white space.
        /// Never throws, returns the default value if the text is null or invalid.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <param name="value">The text to parse, may be null.</param>
        /// <returns>The parsed value or <c>default</c>.</returns>
        public static T ToEnum<T>(String value) where T : struct
        {
            return ((value != null) && Enum.TryParse<T>(value.Trim(), true, out var val)) ? val : default;
        }


        static StringConverter()
        {
            var t = new Dictionary<Type, MethodInfo>();
            foreach (var x in typeof(StringConverter).GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                if (!x.Name.StartsWith("To"))
                    continue;
                if (x.IsGenericMethod)
                    continue;
                var ret = x.ReturnParameter.ParameterType;
                if (ret == typeof(void))
                    continue;
                t[ret] = x;
            }
            MapMethods = t.Freeze();
        }

        static readonly IReadOnlyDictionary<Type, MethodInfo> MapMethods;
        static readonly MethodInfo EnumMethod = typeof(StringConverter).GetMethod(nameof(ToEnum), BindingFlags.Static | BindingFlags.Public);

        /// <summary>
        /// Get the static conversion method (<c>T Method(String)</c>) for a type.
        /// </summary>
        /// <param name="t">The destination type.</param>
        /// <returns>The method for one of the supported primitive types, a closed <see cref="ToEnum{T}(string)"/> for enums, or null if the type isn't supported.</returns>
        public static MethodInfo GetMethod(Type t)
        {
            if (MapMethods.TryGetValue(t, out var m))
                return m;
            if (t.IsEnum)
                return EnumMethod.MakeGenericMethod(t);
            return null;
        }


    }

}
