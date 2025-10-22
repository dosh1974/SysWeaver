using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace SysWeaver.Data
{
    public static class StringToObject
    {

        public static SByte ToSByte(String text)
        {
            return SByte.Parse(text);
        }

        public static Byte ToByte(String text)
        {
            return Byte.Parse(text);
        }

        public static Int16 ToInt16(String text)
        {
            return Int16.Parse(text);
        }

        public static UInt16 ToUInt16(String text)
        {
            return UInt16.Parse(text);
        }

        public static Int32 ToInt32(String text)
        {
            return Int32.Parse(text);
        }

        public static UInt32 ToUInt32(String text)
        {
            return UInt32.Parse(text);
        }

        public static Int64 ToInt64(String text)
        {
            return Int64.Parse(text);
        }

        public static UInt64 ToUInt64(String text)
        {
            return UInt64.Parse(text);
        }

        public static Single ToSingle(String text)
        {
            return Single.Parse(text, CultureInfo.InvariantCulture);
        }

        public static Double ToDouble(String text)
        {
            return Double.Parse(text, CultureInfo.InvariantCulture);
        }

        public static Decimal ToDecimal(String text)
        {
            return Decimal.Parse(text, CultureInfo.InvariantCulture);
        }

        public static Boolean ToBoolean(String text)
        {
            return Boolean.Parse(text);
        }

        public static TimeSpan ToTimeSpan(String text)
        {
            return TimeSpan.Parse(text);
        }

        public static DateTime ToDateTime(String text)
        {
            return DateTime.Parse(text);
        }

        public static TimeOnly ToTimeOnly(String text)
        {
            return TimeOnly.Parse(text);
        }

        public static DateOnly ToDateOnly(String text)
        {
            return DateOnly.Parse(text);
        }

        public static Guid ToGuid(String text)
        {
            return Guid.Parse(text);
        }


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


        public static object GetDefaultValue(this Type t)
        {
            if (t.IsValueType && Nullable.GetUnderlyingType(t) == null)
                return Activator.CreateInstance(t);
            else
                return null;
        }

        public static bool CanConvert(Type t) => Internal.ContainsKey(t);

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


    public static class ObjectConverter
    {

        static readonly ConcurrentDictionary<Tuple<Type, Type>, Func<Object, Object>> TypeConverters = new ConcurrentDictionary<Tuple<Type, Type>, Func<object, object>>();

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
