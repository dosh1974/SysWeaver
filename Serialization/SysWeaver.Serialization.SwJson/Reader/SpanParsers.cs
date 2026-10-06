using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SysWeaver.Serialization.SwJson.Reader
{
    /// <summary>
    /// Parsers from raw UTF8/ASCII bytes to primitive types (integers, floating point, decimal, dates and times, guid and boolean),
    /// and the expressions that call them for the compiled readers.
    /// </summary>
    /// <remarks>
    /// All parsing is culture invariant. Fast paths handle the common formats directly, anything else uses the .NET parse methods
    /// (with <see cref="NumberStyles.Float"/>, <see cref="CultureInfo.InvariantCulture"/> and <see cref="DateTimeStyles.RoundtripKind"/>).
    /// Integers must be [-]digits (no fraction or exponent), out of range values throw an <see cref="OverflowException"/>.
    /// The date/time/guid fallbacks copy the text to a buffer of the same length (on the stack for short texts).
    /// </remarks>
    static class SpanParsers
    {

        /// <summary>
        /// Return an expression that parses a ReadOnlySpan&lt;Byte&gt; of UTF8 bytes to the desired type
        /// </summary>
        /// <param name="t">The type to parse to</param>
        /// <param name="e">The parameter expression, must be of the ReadOnlySpan&lt;Byte&gt; type</param>
        /// <returns>An expression to convert or null if the type isn't supported</returns>
        public static Expression GetExpression(Type t, Expression e)
        {
            if (!TypeToExp.TryGetValue(t, out var fn))
                return null;
            return fn(e);
        }

        /// <summary>
        /// The types that <see cref="GetExpression(Type, Expression)"/> supports.
        /// </summary>
        public static IEnumerable<Type> SupportedTypes => TypeToExp.Keys;


        /// <summary>
        /// Parse decimal digits (no sign), empty is 0.
        /// </summary>
        /// <exception cref="Exception">A char isn't a digit</exception>
        /// <exception cref="OverflowException">The value is out of range</exception>
        public static UInt32 ToUInt32(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            UInt32 v = 0;
            if (l <= 0)
                return v;
            for (int o = 0; ; )
            {
                var i = d[o];
                if ((i < '0') || (i > '9'))
                    ReadException.ThrowExpectedNumberChar(i);
                ++o;
                v = checked(v + (UInt32)(i - '0'));
                if (o >= l)
                    break;
                v = checked(v * 10);
            }
            return v;
        }

        /// <summary>
        /// Parse decimal digits (no sign), empty is 0.
        /// </summary>
        /// <exception cref="Exception">A char isn't a digit</exception>
        /// <exception cref="OverflowException">The value is out of range</exception>
        public static UInt64 ToUInt64(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            UInt64 v = 0;
            if (l <= 0)
                return v;
            for (int o = 0; ;)
            {
                var i = d[o];
                if ((i < '0') || (i > '9'))
                    ReadException.ThrowExpectedNumberChar(i);
                ++o;
                v = checked(v + (UInt64)(i - '0'));
                if (o >= l)
                    break;
                v = checked(v * 10);
            }
            return v;
        }


        /// <summary>
        /// Parse [-]digits, empty is 0.
        /// </summary>
        /// <exception cref="Exception">A char isn't a digit</exception>
        /// <exception cref="OverflowException">The value is out of range</exception>
        public static Int32 ToInt32(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            Int32 v = 0;
            if (l <= 0)
                return v;
            var sign = d[0];
            if (sign == '-')
            {
                if (l < 2)
                    ReadException.ThrowExpectedNumberChar(sign);
                var n = ToUInt32(d.Slice(1));
                if (n > 0x80000000U)
                    throw new OverflowException();
                return unchecked(-(Int32)n);
            }
            return checked((Int32)ToUInt32(d));
        }

        /// <summary>
        /// Parse [-]digits, empty is 0.
        /// </summary>
        /// <exception cref="Exception">A char isn't a digit</exception>
        /// <exception cref="OverflowException">The value is out of range</exception>
        public static Int64 ToInt64(ReadOnlySpan<Byte> d)
        {
            var l = d.Length;
            Int64 v = 0;
            if (l <= 0)
                return v;
            var sign = d[0];
            if (sign == '-')
            {
                if (l < 2)
                    ReadException.ThrowExpectedNumberChar(sign);
                var n = ToUInt64(d.Slice(1));
                if (n > 0x8000000000000000UL)
                    throw new OverflowException();
                return unchecked(-(Int64)n);
            }
            return checked((Int64)ToUInt64(d));
        }

        #region Fast paths

        //  The fast paths below handle the common formats exactly (same result as the .NET parsing), anything else uses the .NET parsing

        /// <summary>
        /// Parse [-]digits[.digits] (no exponent, max 19 digits) into a mantissa and the number of decimals
        /// </summary>
        static bool TryParseSimpleDecimal(ReadOnlySpan<Byte> d, out ulong mantissa, out int decimals, out bool negative)
        {
            mantissa = 0;
            decimals = 0;
            var l = d.Length;
            int i = 0;
            negative = (l > 0) && (d[0] == '-');
            if (negative)
                ++i;
            int digits = 0;
            int intDigits = 0;
            bool dot = false;
            for (; i < l; ++i)
            {
                uint c = d[i];
                var v = c - '0';
                if (v <= 9)
                {
                    if (digits >= 19)
                        return false;
                    mantissa = mantissa * 10 + v;
                    ++digits;
                    if (dot)
                        ++decimals;
                    else
                        ++intDigits;
                    continue;
                }
                if ((c != '.') || dot)
                    return false;
                dot = true;
            }
            //  At least one digit before and after the dot
            return (intDigits > 0) && (!dot || (decimals > 0));
        }

        static readonly double[] Pow10Double = [1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22];
        static readonly float[] Pow10Single = [1e0f, 1e1f, 1e2f, 1e3f, 1e4f, 1e5f, 1e6f, 1e7f, 1e8f, 1e9f, 1e10f];

        /// <summary>
        /// n / 10^k is correctly rounded when n and 10^k are exactly representable (n &lt; 2^53, k &lt;= 22), the same as Double.Parse
        /// </summary>
        public static Double ToDouble(ReadOnlySpan<Byte> d)
        {
            if (TryParseSimpleDecimal(d, out var n, out var k, out var neg) && (n < (1UL << 53)))
            {
                var v = (double)n;
                if (k > 0)
                    v /= Pow10Double[k];
                return neg ? -v : v;
            }
            return Double.Parse(d, ParseStyle, ParseCulture);
        }

        /// <summary>
        /// n / 10^k is correctly rounded when n and 10^k are exactly representable (n &lt; 2^24, k &lt;= 10), the same as Single.Parse
        /// </summary>
        public static Single ToSingle(ReadOnlySpan<Byte> d)
        {
            if (TryParseSimpleDecimal(d, out var n, out var k, out var neg) && (n < (1UL << 24)) && (k <= 10))
            {
                var v = (float)n;
                if (k > 0)
                    v /= Pow10Single[k];
                return neg ? -v : v;
            }
            return Single.Parse(d, ParseStyle, ParseCulture);
        }

        /// <summary>
        /// A decimal is the mantissa and the scale (the number of decimals, trailing zeros are kept), the same as Decimal.Parse
        /// </summary>
        public static Decimal ToDecimal(ReadOnlySpan<Byte> d)
        {
            //  Zero is left to Decimal.Parse (negative zero)
            if (TryParseSimpleDecimal(d, out var n, out var k, out var neg) && (n != 0) && (k <= 28))
                return new Decimal((int)(uint)n, (int)(uint)(n >> 32), 0, neg, (Byte)k);
            return Decimal.Parse(d, ParseStyle, ParseCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryDigits(ReadOnlySpan<Byte> d, int offset, int count, out int value)
        {
            value = 0;
            for (int i = 0; i < count; ++i)
            {
                var v = (uint)d[offset + i] - '0';
                if (v > 9)
                    return false;
                value = value * 10 + (int)v;
            }
            return true;
        }

        /// <summary>
        /// Parse an optional fraction of a second ".f" with 1-7 digits at the offset, to ticks
        /// </summary>
        static bool TryFraction(ReadOnlySpan<Byte> d, ref int offset, out long ticks)
        {
            ticks = 0;
            if ((offset >= d.Length) || (d[offset] != '.'))
                return true;
            ++offset;
            int count = 0;
            long v = 0;
            while ((offset < d.Length) && (((uint)d[offset] - '0') <= 9))
            {
                if (count >= 7)
                    return false;
                v = v * 10 + (d[offset] - '0');
                ++count;
                ++offset;
            }
            if (count == 0)
                return false;
            for (int i = count; i < 7; ++i)
                v *= 10;
            ticks = v;
            return true;
        }

        /// <summary>
        /// Parse "HH:mm:ss" at the offset (valid ranges only)
        /// </summary>
        static bool TryTimeOfDay(ReadOnlySpan<Byte> d, int offset, out long ticks)
        {
            ticks = 0;
            if ((d.Length < offset + 8) || (d[offset + 2] != ':') || (d[offset + 5] != ':'))
                return false;
            if (!TryDigits(d, offset, 2, out var h) || !TryDigits(d, offset + 3, 2, out var m) || !TryDigits(d, offset + 6, 2, out var sec))
                return false;
            if ((h > 23) || (m > 59) || (sec > 59))
                return false;
            ticks = ((h * 60L + m) * 60L + sec) * TimeSpan.TicksPerSecond;
            return true;
        }

        /// <summary>
        /// Parse "yyyy-MM-dd" (valid dates only)
        /// </summary>
        static bool TryDate(ReadOnlySpan<Byte> d, out DateTime date)
        {
            date = default;
            if ((d.Length < 10) || (d[4] != '-') || (d[7] != '-'))
                return false;
            if (!TryDigits(d, 0, 4, out var y) || !TryDigits(d, 5, 2, out var mo) || !TryDigits(d, 8, 2, out var day))
                return false;
            if ((y < 1) || (mo < 1) || (mo > 12) || (day < 1) || (day > DateTime.DaysInMonth(y, mo)))
                return false;
            date = new DateTime(y, mo, day);
            return true;
        }

        /// <summary>
        /// Parse "yyyy-MM-ddTHH:mm:ss[.fffffff]", returns the offset after the parsed text
        /// </summary>
        static bool TryDateTimeCore(ReadOnlySpan<Byte> d, out DateTime value, out int end)
        {
            value = default;
            end = 0;
            if ((d.Length < 19) || (d[10] != 'T') || !TryDate(d, out var date) || !TryTimeOfDay(d, 11, out var time))
                return false;
            end = 19;
            if (!TryFraction(d, ref end, out var fraction))
                return false;
            value = new DateTime(date.Ticks + time + fraction);
            return true;
        }

        #endregion//Fast paths

        /// <summary>
        /// The fallback parsers copy texts up to this length to a stack buffer, longer texts are copied to a heap buffer
        /// </summary>
        const int MaxStackChars = 256;

        /// <summary>
        /// Parse a <see cref="DateTime"/> like <see cref="DateTime.Parse(string, IFormatProvider, DateTimeStyles)"/> with the invariant culture and <see cref="DateTimeStyles.RoundtripKind"/>.
        /// "yyyy-MM-ddTHH:mm:ss[.fffffff][Z]" is parsed directly.
        /// </summary>
        /// <exception cref="FormatException">The text isn't a valid date and time</exception>
        public static DateTime ToDateTime(ReadOnlySpan<Byte> d)
        {
            //  Like DateTime.Parse with RoundtripKind: no suffix is Unspecified, Z is Utc (an offset is converted to local time, left to DateTime.Parse)
            if (TryDateTimeCore(d, out var dt, out var end))
            {
                if (end == d.Length)
                    return dt;
                if ((end == d.Length - 1) && (d[end] == 'Z'))
                    return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            var l = d.Length;
            Span<Char> t = l <= MaxStackChars ? stackalloc Char[l] : new Char[l];
            for (int i = 0; i < l; ++i)
                t[i] = (Char)d[i];
            return DateTime.Parse(t, ParseCulture, DateTimeStyle);
        }

        /// <summary>
        /// Parse a <see cref="TimeSpan"/> like <see cref="TimeSpan.Parse(string, IFormatProvider)"/> with the invariant culture.
        /// "[-][d.]hh:mm:ss[.fffffff]" is parsed directly.
        /// </summary>
        /// <exception cref="FormatException">The text isn't a valid time span</exception>
        /// <exception cref="OverflowException">The time span is out of range</exception>
        public static TimeSpan ToTimeSpan(ReadOnlySpan<Byte> d)
        {
            //  [-][d.]hh:mm:ss[.fffffff]
            {
                var len = d.Length;
                int o = 0;
                var neg = (len > 0) && (d[0] == '-');
                if (neg)
                    ++o;
                ulong days = 0;
                var colon = d.IndexOf((Byte)':');
                var dot = d.Slice(o).IndexOf((Byte)'.');
                bool ok = colon > 0;
                if (ok && (dot >= 0) && ((o + dot) < colon))
                {
                    //  Days
                    var dl = dot;
                    if ((dl < 1) || (dl > 8) || !TryDigits(d, o, dl, out var dv))
                        ok = false;
                    else
                    {
                        days = (ulong)dv;
                        o += dl + 1;
                    }
                }
                if (ok && (colon == o + 2) && TryTimeOfDay(d, o, out var time))
                {
                    o += 8;
                    if (TryFraction(d, ref o, out var fraction) && (o == len))
                    {
                        var ticks = days * TimeSpan.TicksPerDay + (ulong)time + (ulong)fraction;
                        if (neg)
                        {
                            if (ticks <= (1UL << 63))
                                return new TimeSpan(unchecked(-(long)ticks));
                        }
                        else if (ticks <= long.MaxValue)
                        {
                            return new TimeSpan((long)ticks);
                        }
                    }
                }
            }
            var l = d.Length;
            Span<Char> t = l <= MaxStackChars ? stackalloc Char[l] : new Char[l];
            for (int i = 0; i < l; ++i)
                t[i] = (Char)d[i];
            return TimeSpan.Parse(t, ParseCulture);
        }

        /// <summary>
        /// Parse a <see cref="DateOnly"/> ("yyyy-MM-dd" is parsed directly, else <see cref="DateOnly.Parse(ReadOnlySpan{char}, IFormatProvider, DateTimeStyles)"/> with the invariant culture).
        /// </summary>
        /// <exception cref="FormatException">The text isn't a valid date</exception>
        public static DateOnly ToDateOnly(ReadOnlySpan<Byte> d)
        {
            if ((d.Length == 10) && TryDate(d, out var date))
                return DateOnly.FromDateTime(date);
            var l = d.Length;
            Span<Char> t = l <= MaxStackChars ? stackalloc Char[l] : new Char[l];
            for (int i = 0; i < l; ++i)
                t[i] = (Char)d[i];
            return DateOnly.Parse(t, ParseCulture, DateTimeStyles.AllowWhiteSpaces);
        }

        /// <summary>
        /// Parse a <see cref="TimeOnly"/> ("HH:mm:ss[.fffffff]" is parsed directly, else <see cref="TimeOnly.Parse(ReadOnlySpan{char}, IFormatProvider, DateTimeStyles)"/> with the invariant culture).
        /// </summary>
        /// <exception cref="FormatException">The text isn't a valid time</exception>
        public static TimeOnly ToTimeOnly(ReadOnlySpan<Byte> d)
        {
            if (TryTimeOfDay(d, 0, out var time))
            {
                int o = 8;
                if (TryFraction(d, ref o, out var fraction) && (o == d.Length))
                    return new TimeOnly(time + fraction);
            }
            var l = d.Length;
            Span<Char> t = l <= MaxStackChars ? stackalloc Char[l] : new Char[l];
            for (int i = 0; i < l; ++i)
                t[i] = (Char)d[i];
            return TimeOnly.Parse(t, ParseCulture, DateTimeStyles.AllowWhiteSpaces);
        }

        /// <summary>
        /// Parse a <see cref="DateTimeOffset"/> like <see cref="DateTimeOffset.Parse(string, IFormatProvider, DateTimeStyles)"/> with the invariant culture and <see cref="DateTimeStyles.RoundtripKind"/>.
        /// "yyyy-MM-ddTHH:mm:ss[.fffffff](Z|+hh:mm|-hh:mm)" is parsed directly, no offset means the local offset.
        /// </summary>
        /// <exception cref="FormatException">The text isn't a valid date and time</exception>
        public static DateTimeOffset ToDateTimeOffset(ReadOnlySpan<Byte> d)
        {
            //  yyyy-MM-ddTHH:mm:ss[.fffffff](Z|+hh:mm|-hh:mm), no suffix is the local offset (left to DateTimeOffset.Parse)
            if (TryDateTimeCore(d, out var dt, out var end))
            {
                var rem = d.Length - end;
                if ((rem == 1) && (d[end] == 'Z'))
                    return new DateTimeOffset(dt, TimeSpan.Zero);
                if ((rem == 6) && ((d[end] == '+') || (d[end] == '-')) && (d[end + 3] == ':') && TryDigits(d, end + 1, 2, out var oh) && TryDigits(d, end + 4, 2, out var om) && (om <= 59))
                {
                    var offset = oh * 60 + om;
                    if (offset <= 14 * 60)
                    {
                        var ot = TimeSpan.FromMinutes(d[end] == '-' ? -offset : offset);
                        var utc = dt.Ticks - ot.Ticks;
                        if ((utc >= DateTime.MinValue.Ticks) && (utc <= DateTime.MaxValue.Ticks))
                            return new DateTimeOffset(dt, ot);
                    }
                }
            }
            var l = d.Length;
            Span<Char> t = l <= MaxStackChars ? stackalloc Char[l] : new Char[l];
            for (int i = 0; i < l; ++i)
                t[i] = (Char)d[i];
            return DateTimeOffset.Parse(t, ParseCulture, DateTimeStyle);
        }

        /// <summary>
        /// Parse a <see cref="Guid"/> in any format <see cref="Guid.Parse(ReadOnlySpan{char})"/> accepts ("D" is parsed directly).
        /// </summary>
        /// <exception cref="FormatException">The text isn't a valid guid</exception>
        public static Guid ToGuid(ReadOnlySpan<Byte> d)
        {
            //  The "D" format (the common one), directly from the UTF8
            if ((d.Length == 36) && System.Buffers.Text.Utf8Parser.TryParse(d, out Guid g, out var consumed, 'D') && (consumed == 36))
                return g;
            var l = d.Length;
            Span<Char> t = l <= MaxStackChars ? stackalloc Char[l] : new Char[l];
            for (int i = 0; i < l; ++i)
                t[i] = (Char)d[i];
            return Guid.Parse(t);
        }

        /// <summary>
        /// Parse <c>true</c>, <c>false</c>, <c>1</c> or <c>0</c> (case sensitive).
        /// </summary>
        /// <exception cref="Exception">Any other value</exception>
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
                { typeof(Double), e => Expression.Call(Helper.SafeGetMethod(t, nameof(ToDouble), BindingFlags.Static | BindingFlags.Public), e) },
                { typeof(Single), e => Expression.Call(Helper.SafeGetMethod(t, nameof(ToSingle), BindingFlags.Static | BindingFlags.Public), e) },
                { typeof(Decimal), e => Expression.Call(Helper.SafeGetMethod(t, nameof(ToDecimal), BindingFlags.Static | BindingFlags.Public), e) },
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
                d[typeof(Byte)] = e => Expression.ConvertChecked(Expression.Call(u64, e), typeof(Byte));
                d[typeof(UInt16)] = e => Expression.ConvertChecked(Expression.Call(u64, e), typeof(UInt16));
                d[typeof(UInt32)] = e => Expression.ConvertChecked(Expression.Call(u64, e), typeof(UInt32));
                d[typeof(SByte)] = e => Expression.ConvertChecked(Expression.Call(s64, e), typeof(SByte));
                d[typeof(Int16)] = e => Expression.ConvertChecked(Expression.Call(s64, e), typeof(Int16));
                d[typeof(Int32)] = e => Expression.ConvertChecked(Expression.Call(s64, e), typeof(Int32));
            }
            else
            {
                d[typeof(Byte)] = e => Expression.ConvertChecked(Expression.Call(u32, e), typeof(Byte));
                d[typeof(UInt16)] = e => Expression.ConvertChecked(Expression.Call(u32, e), typeof(UInt16));
                d[typeof(UInt32)] = e => Expression.Call(u32, e);
                d[typeof(SByte)] = e => Expression.ConvertChecked(Expression.Call(s32, e), typeof(SByte));
                d[typeof(Int16)] = e => Expression.ConvertChecked(Expression.Call(s32, e), typeof(Int16));
                d[typeof(Int32)] = e => Expression.Call(s32, e);
            }
            TypeToExp = d.ToFrozenDictionary();
        }


        static readonly IReadOnlyDictionary<Type, Func<Expression, Expression>> TypeToExp;






        #endregion//Expressions


    }

}
