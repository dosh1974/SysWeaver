using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace SysWeaver.Data
{
    public static class StringConverter
    {
        public static SByte ToSByte(String value)
        {
            return ((value != null) && SByte.TryParse(value.Trim(), out var val)) ? val : default;
        }

        public static Int16 ToInt16(String value)
        {
            return ((value != null) && Int16.TryParse(value.Trim(), out var val)) ? val : default;
        }

        public static Int32 ToInt32(String value)
        {
            return ((value != null) && Int32.TryParse(value.Trim(), out var val)) ? val : default;
        }

        public static Int64 ToInt64(String value)
        {
            return ((value != null) && Int64.TryParse(value.Trim(), out var val)) ? val : default;
        }

        public static Byte ToByte(String value)
        {
            return ((value != null) && Byte.TryParse(value.Trim(), out var val)) ? val : default;
        }

        public static UInt16 ToUInt16(String value)
        {
            return ((value != null) && UInt16.TryParse(value.Trim(), out var val)) ? val : default;
        }

        public static UInt32 ToUInt32(String value)
        {
            return ((value != null) && UInt32.TryParse(value.Trim(), out var val)) ? val : default;
        }

        public static UInt64 ToUInt64(String value)
        {
            return ((value != null) && UInt64.TryParse(value.Trim(), out var val)) ? val : default;
        }

        public static Single ToSingle(String value)
        {
            return ((value != null) && Single.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        public static Double ToDouble(String value)
        {
            return ((value != null) && Double.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        public static Decimal ToDecimal(String value)
        {
            return ((value != null) && Decimal.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        public static DateTime ToDateTime(String value)
        {
            return ((value != null) && DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        public static TimeSpan ToTimeSpan(String value)
        {
            return ((value != null) && TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        public static DateOnly ToDateOnly(String value)
        {
            return ((value != null) && DateOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        public static TimeOnly ToTimeOnly(String value)
        {
            return ((value != null) && TimeOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        public static Guid ToGuid(String value)
        {
            return ((value != null) && Guid.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var val)) ? val : default;
        }

        public static Boolean ToBoolean(String value)
        {
            return ((value != null) && Boolean.TryParse(value.Trim(), out var val)) ? val : default;
        }

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
        /// Get the method to call
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
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
