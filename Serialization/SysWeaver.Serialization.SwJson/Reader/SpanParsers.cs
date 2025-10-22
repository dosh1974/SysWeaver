using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace SysWeaver.Serialization.SwJson.Reader
{
    static class SpanParsers
    {

        /// <summary>
        /// Return an expression that parses a ReadOnlySpan´Byte array of UTF8-bytes to the desired type
        /// </summary>
        /// <param name="t">The type to parse to</param>
        /// <param name="e">The parametyer expression, must be of the ReadOnlySpan´Byte  type</param>
        /// <returns>An expression to convert or null if the type isn't supported</returns>
        public static Expression GetExpression(Type t, Expression e)
        {
            if (!TypeToExp.TryGetValue(t, out var fn))
                return null;
            return fn(e);
        }

        public static IEnumerable<Type> SupportedTypes => TypeToExp.Keys;


        public static UInt32 ToUInt32(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            UInt32 v = 0;
            if (l <= 0)
                return v;
            for (int o = 0; ; )
            {
                var i = d[o];
#if VALIDATE
                if ((i < '0') || (i > '9'))
                    ReadException.ThrowExpectedNumberChar(i);
#endif//VALIDATE
                ++o;
                v += (UInt32)(i - '0');
                if (o >= l)
                    break;
                v *= 10;
            }
            return v;
        }

        public static UInt64 ToUInt64(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            UInt64 v = 0;
            if (l <= 0)
                return v;
            for (int o = 0; ;)
            {
                var i = d[o];
#if VALIDATE
                if ((i < '0') || (i > '9'))
                    ReadException.ThrowExpectedNumberChar(i);
#endif//VALIDATE
                ++o;
                v += (UInt64)(i - '0');
                if (o >= l)
                    break;
                v *= 10;
            }
            return v;
        }


        public static Int32 ToInt32(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            Int32 v = 0;
            if (l <= 0)
                return v;
            var sign = d[0];
            if (sign == '-')
            {
#if VALIDATE
                if (l < 2)
                    ReadException.ThrowExpectedNumberChar(sign);
#endif//VALIDATE
                return -(Int32)ToUInt32(d.Slice(1));
            }
            return (Int32)ToUInt32(d);
        }

        public static Int64 ToInt64(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            Int64 v = 0;
            if (l <= 0)
                return v;
            var sign = d[0];
            if (sign == '-')
            {
#if VALIDATE
                if (l < 2)
                    ReadException.ThrowExpectedNumberChar(sign);
#endif//VALIDATE
                return -(Int64)ToUInt64(d.Slice(1));
            }
            return (Int64)ToUInt64(d);
        }

        public static Double ToDouble(ReadOnlySpan<Byte> d) => Double.Parse(d, ParseStyle, ParseCulture);
        public static Single ToSingle(ReadOnlySpan<Byte> d) => Single.Parse(d, ParseStyle, ParseCulture);
        public static Decimal ToDecimal(ReadOnlySpan<Byte> d) => Decimal.Parse(d, ParseStyle, ParseCulture);

        public static DateTime ToDateTime(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            Span<Char> t = stackalloc Char[l];
            for (int i = 0; i < l; ++i)
                t[i] = (Char)d[i];
            return DateTime.Parse(t, ParseCulture, DateTimeStyle);
        }

        public static TimeSpan ToTimeSpan(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            Span<Char> t = stackalloc Char[l];
            for (int i = 0; i < l; ++i)
                t[i] = (Char)d[i];
            return TimeSpan.Parse(t, ParseCulture);
        }

        public static DateOnly ToDateOnly(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            Span<Char> t = stackalloc Char[l];
            for (int i = 0; i < l; ++i)
                t[i] = (Char)d[i];
            return DateOnly.Parse(t, ParseCulture, DateTimeStyles.AllowWhiteSpaces);
        }

        public static TimeOnly ToTimeOnly(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            Span<Char> t = stackalloc Char[l];
            for (int i = 0; i < l; ++i)
                t[i] = (Char)d[i];
            return TimeOnly.Parse(t, ParseCulture, DateTimeStyles.AllowWhiteSpaces);
        }

        public static DateTimeOffset ToDateTimeOffset(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            Span<Char> t = stackalloc Char[l];
            for (int i = 0; i < l; ++i)
                t[i] = (Char)d[i];
            return DateTimeOffset.Parse(t, ParseCulture, DateTimeStyle);
        }

        public static Guid ToGuid(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            Span<Char> t = stackalloc Char[l];
            for (int i = 0; i < l; ++i)
                t[i] = (Char)d[i];
            return Guid.Parse(t);
        }

        public static Boolean ToBoolean(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            if (l == 1)
            {
                var c = d[0];
                if (c == 48)
                    return false;
                if (c == 49)
                    return true;
                ReadException.ThrowExpectedBoolean(d);
            }
            if (l < 4)
                ReadException.ThrowExpectedBoolean(d);
            uint a = d[0];
            uint b = d[1];
            a <<= 16;
            b <<= 16;
            a |= d[2];
            b |= d[3];
            a <<= 8;
            a |= b;
            if (l == 4)
            {
                if (a == 0x74727565) // "true"
                    return true;
                ReadException.ThrowExpectedBoolean(d);
            }
            if (l != 5)
                ReadException.ThrowExpectedBoolean(d);
            if (a == 0x66616c73) // "fals"
                if (d[4] == 0x65) // 'e'
                    return false;
            ReadException.ThrowExpectedBoolean(d);
            return default;
        }




        #region Options

        static readonly CultureInfo ParseCulture = CultureInfo.InvariantCulture;
        const NumberStyles ParseStyle = NumberStyles.Float;
        const DateTimeStyles DateTimeStyle = DateTimeStyles.RoundtripKind;

        #endregion//Options

        #region Expressions


        static readonly ConstantExpression ExpParseCulture = Expression.Constant(ParseCulture);
        static readonly ConstantExpression ExpParseStyle = Expression.Constant(ParseStyle);

        static readonly Type[] NumberParams = [typeof(ReadOnlySpan<Byte>), typeof(NumberStyles), typeof(IFormatProvider)];

        static readonly MethodInfo MethodDouble = Helper.SafeGetMethod(typeof(Double), nameof(Double.Parse), BindingFlags.Static | BindingFlags.Public, NumberParams);

        static readonly MethodInfo MethodSingle = Helper.SafeGetMethod(typeof(Single), nameof(Single.Parse), BindingFlags.Static | BindingFlags.Public, NumberParams);
        
        static readonly MethodInfo MethodDecimal = Helper.SafeGetMethod(typeof(Decimal), nameof(Decimal.Parse), BindingFlags.Static | BindingFlags.Public, NumberParams);


        static readonly MethodInfo MethodInt32 = Helper.SafeGetMethod(typeof(SpanParsers), nameof(ToInt32), BindingFlags.Static | BindingFlags.Public);
        static readonly MethodInfo MethodInt64 = Helper.SafeGetMethod(typeof(SpanParsers), nameof(ToInt64), BindingFlags.Static | BindingFlags.Public);
        static readonly MethodInfo MethodUInt32 = Helper.SafeGetMethod(typeof(SpanParsers), nameof(ToUInt32), BindingFlags.Static | BindingFlags.Public);
        static readonly MethodInfo MethodUInt64 = Helper.SafeGetMethod(typeof(SpanParsers), nameof(ToUInt64), BindingFlags.Static | BindingFlags.Public);


        static SpanParsers()
        {
            var u64 = MethodUInt64;
            var u32 = MethodUInt32;
            var s64 = MethodInt64;
            var s32 = MethodInt32;
            var eps = ExpParseStyle;
            var epc = ExpParseCulture;
            var t = typeof(SpanParsers);
            var d  = new Dictionary<Type, Func<Expression, Expression>>()
            {
                { typeof(UInt64), e => Expression.Call(u64, e) },
                { typeof(Int64), e => Expression.Call(s64, e) },
                { typeof(Double), e => Expression.Call(MethodDouble, e, eps, epc) },
                { typeof(Single), e => Expression.Call(MethodSingle, e, eps, epc) },
                { typeof(Decimal), e => Expression.Call(MethodDecimal, e, eps, epc) },
                { typeof(DateTime), e => Expression.Call(Helper.SafeGetMethod(t, nameof(ToDateTime), BindingFlags.Static | BindingFlags.Public), e) },
                { typeof(TimeSpan), e => Expression.Call(Helper.SafeGetMethod(t, nameof(ToTimeSpan), BindingFlags.Static | BindingFlags.Public), e) },
                { typeof(DateOnly), e => Expression.Call(Helper.SafeGetMethod(t, nameof(ToDateOnly), BindingFlags.Static | BindingFlags.Public), e) },
                { typeof(TimeOnly), e => Expression.Call(Helper.SafeGetMethod(t, nameof(ToTimeOnly), BindingFlags.Static | BindingFlags.Public), e) },
                { typeof(DateTimeOffset), e => Expression.Call(Helper.SafeGetMethod(t, nameof(ToDateTimeOffset), BindingFlags.Static | BindingFlags.Public), e) },
                { typeof(Guid), e => Expression.Call(Helper.SafeGetMethod(t, nameof(ToGuid), BindingFlags.Static | BindingFlags.Public), e) },
                { typeof(Boolean), e => Expression.Call(Helper.SafeGetMethod(t, nameof(ToBoolean), BindingFlags.Static | BindingFlags.Public), e) },
            };
            if (Environment.Is64BitProcess)
            {
                d[typeof(Byte)] = e => Expression.Convert(Expression.Call(u64, e), typeof(Byte));
                d[typeof(UInt16)] = e => Expression.Convert(Expression.Call(u64, e), typeof(UInt16));
                d[typeof(UInt32)] = e => Expression.Convert(Expression.Call(u64, e), typeof(UInt32));
                d[typeof(SByte)] = e => Expression.Convert(Expression.Call(s64, e), typeof(SByte));
                d[typeof(Int16)] = e => Expression.Convert(Expression.Call(s64, e), typeof(Int16));
                d[typeof(Int32)] = e => Expression.Convert(Expression.Call(s64, e), typeof(Int32));
            }
            else
            {
                d[typeof(Byte)] = e => Expression.Convert(Expression.Call(u32, e), typeof(Byte));
                d[typeof(UInt16)] = e => Expression.Convert(Expression.Call(u32, e), typeof(UInt16));
                d[typeof(UInt32)] = e => Expression.Call(u32, e);
                d[typeof(SByte)] = e => Expression.Convert(Expression.Call(s32, e), typeof(Byte));
                d[typeof(Int16)] = e => Expression.Convert(Expression.Call(s32, e), typeof(UInt16));
                d[typeof(Int32)] = e => Expression.Call(s32, e);
            }
            TypeToExp = d.ToFrozenDictionary();
        }


        static readonly IReadOnlyDictionary<Type, Func<Expression, Expression>> TypeToExp;






        #endregion//Expressions


    }

}
