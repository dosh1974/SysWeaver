using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver.Serialization.SwJson.Reader
{
    /// <summary>
    /// Readers of unquoted numbers and booleans that find the end of the value and parse it in one pass (the common case),
    /// with exactly the same results (and exceptions) as the <see cref="SpanParsers"/> method of the type applied to <see cref="Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted"/>,
    /// that they fall back to for anything else (quoted values, exponents, too many digits, an unknown end condition, invalid values..).
    /// </summary>
    [SkipLocalsInit]
    unsafe static class FusedParsers
    {
        /// <summary>
        /// The end of a value: the end of the data or a char that meets the end condition (given as its table)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsEnd(Byte* p, Byte* e, ref Byte tbl) => (p >= e) || (Unsafe.Add(ref tbl, *p) != 0);

        /// <summary>
        /// Same as SpanParsers.ToInt64(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn)), [-]digits with at most 18 digits are parsed directly
        /// </summary>
        public static Int64 ReadInt64(JsonParserState state, Func<Char, bool> endOn)
        {
            var tbl = Utf8JsonParser.GetEndTable(endOn);
            if (tbl != null)
            {
                var p = state.D;
                var e = state.E;
                var neg = (p < e) && (*p == '-');
                if (neg)
                    ++p;
                var s = p;
                var lim = (e - p) > 18 ? p + 18 : e;
                ulong v = 0;
                while (p < lim)
                {
                    var c = (uint)*p - '0';
                    if (c > 9)
                        break;
                    v = v * 10 + c;
                    ++p;
                }
                if ((p > s) && IsEnd(p, e, ref MemoryMarshal.GetArrayDataReference(tbl)))
                {
                    state.D = p;
                    return neg ? -(Int64)v : (Int64)v;
                }
            }
            return SpanParsers.ToInt64(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn));
        }

        /// <summary>
        /// Same as SpanParsers.ToUInt64(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn)), digits (at most 19) are parsed directly
        /// </summary>
        public static UInt64 ReadUInt64(JsonParserState state, Func<Char, bool> endOn)
        {
            var tbl = Utf8JsonParser.GetEndTable(endOn);
            if (tbl != null)
            {
                var p = state.D;
                var e = state.E;
                var s = p;
                var lim = (e - p) > 19 ? p + 19 : e;
                ulong v = 0;
                while (p < lim)
                {
                    var c = (uint)*p - '0';
                    if (c > 9)
                        break;
                    v = v * 10 + c;
                    ++p;
                }
                if ((p > s) && IsEnd(p, e, ref MemoryMarshal.GetArrayDataReference(tbl)))
                {
                    state.D = p;
                    return v;
                }
            }
            return SpanParsers.ToUInt64(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn));
        }

        /// <summary>
        /// Parse [-]digits[.digits] (at most 19 digits, at least one before and after the dot) followed by the end, like SpanParsers.TryParseSimpleDecimal applied to the value
        /// </summary>
        /// <returns>The position after the number, null if the value isn't such a number</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Byte* TryParseSimpleDecimal(Byte* p, Byte* e, Byte[] tbl, out ulong mantissa, out int decimals, out bool negative)
        {
            mantissa = 0;
            decimals = 0;
            negative = (p < e) && (*p == '-');
            if (negative)
                ++p;
            ulong n = 0;
            int digits = 0;
            int intDigits = 0;
            bool dot = false;
            while (p < e)
            {
                uint c = *p;
                var v = c - '0';
                if (v <= 9)
                {
                    if (digits >= 19)
                        return null;
                    n = n * 10 + v;
                    ++digits;
                    if (dot)
                        ++decimals;
                    else
                        ++intDigits;
                    ++p;
                    continue;
                }
                if ((c != '.') || dot)
                    break;
                dot = true;
                ++p;
            }
            if ((intDigits <= 0) || (dot && (decimals <= 0)) || !IsEnd(p, e, ref MemoryMarshal.GetArrayDataReference(tbl)))
                return null;
            mantissa = n;
            return p;
        }

        static readonly double[] Pow10Double = [1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22];
        static readonly float[] Pow10Single = [1e0f, 1e1f, 1e2f, 1e3f, 1e4f, 1e5f, 1e6f, 1e7f, 1e8f, 1e9f, 1e10f];

        /// <summary>
        /// Same as SpanParsers.ToDouble(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn)), the simple decimal numbers of its fast path are parsed directly
        /// </summary>
        public static Double ReadDouble(JsonParserState state, Func<Char, bool> endOn)
        {
            var tbl = Utf8JsonParser.GetEndTable(endOn);
            if (tbl != null)
            {
                var p = TryParseSimpleDecimal(state.D, state.E, tbl, out var n, out var k, out var neg);
                if ((p != null) && (n < (1UL << 53)))
                {
                    state.D = p;
                    var v = (double)n;
                    if (k > 0)
                        v /= Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Pow10Double), k);
                    return neg ? -v : v;
                }
            }
            return SpanParsers.ToDouble(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn));
        }

        /// <summary>
        /// Same as SpanParsers.ToSingle(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn)), the simple decimal numbers of its fast path are parsed directly
        /// </summary>
        public static Single ReadSingle(JsonParserState state, Func<Char, bool> endOn)
        {
            var tbl = Utf8JsonParser.GetEndTable(endOn);
            if (tbl != null)
            {
                var p = TryParseSimpleDecimal(state.D, state.E, tbl, out var n, out var k, out var neg);
                if ((p != null) && (n < (1UL << 24)) && (k <= 10))
                {
                    state.D = p;
                    var v = (float)n;
                    if (k > 0)
                        v /= Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Pow10Single), k);
                    return neg ? -v : v;
                }
            }
            return SpanParsers.ToSingle(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn));
        }

        /// <summary>
        /// Same as SpanParsers.ToDecimal(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn)), the simple decimal numbers of its fast path are parsed directly
        /// </summary>
        public static Decimal ReadDecimal(JsonParserState state, Func<Char, bool> endOn)
        {
            var tbl = Utf8JsonParser.GetEndTable(endOn);
            if (tbl != null)
            {
                var p = TryParseSimpleDecimal(state.D, state.E, tbl, out var n, out var k, out var neg);
                //  Zero is left to Decimal.Parse (negative zero)
                if ((p != null) && (n != 0) && (k <= 28))
                {
                    state.D = p;
                    return new Decimal((int)(uint)n, (int)(uint)(n >> 32), 0, neg, (Byte)k);
                }
            }
            return SpanParsers.ToDecimal(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn));
        }

        /// <summary>
        /// Same as SpanParsers.ToBoolean(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn)), unquoted true and false are parsed directly
        /// </summary>
        public static Boolean ReadBoolean(JsonParserState state, Func<Char, bool> endOn)
        {
            var tbl = Utf8JsonParser.GetEndTable(endOn);
            if (tbl != null)
            {
                var p = state.D;
                var e = state.E;
                if ((e - p) >= 4)
                {
                    var x = Unsafe.ReadUnaligned<uint>(p);
                    //  "true"
                    if ((x == 0x65757274u) && IsEnd(p + 4, e, ref MemoryMarshal.GetArrayDataReference(tbl)))
                    {
                        state.D = p + 4;
                        return true;
                    }
                    //  "false"
                    if ((x == 0x736c6166u) && ((e - p) >= 5) && (p[4] == 'e') && IsEnd(p + 5, e, ref MemoryMarshal.GetArrayDataReference(tbl)))
                    {
                        state.D = p + 5;
                        return false;
                    }
                }
            }
            return SpanParsers.ToBoolean(Utf8JsonParser.ReadAsciiReadOnlyMemoryMaybeQuoted(state, endOn));
        }

        #region Expressions

        static MethodInfo Get(String name) => Helper.SafeGetMethod(typeof(FusedParsers), name, BindingFlags.Static | BindingFlags.Public);

        /// <summary>
        /// An expression that reads a value of the type (using the <see cref="ReadTypeCache.ParState"/> and <see cref="ReadTypeCache.ParEndOn"/> parameters), null if there is no fused reader for the type
        /// </summary>
        public static Expression GetExpression(Type t)
        {
            if (!TypeToExp.TryGetValue(t, out var fn))
                return null;
            return fn();
        }

        static readonly IReadOnlyDictionary<Type, Func<Expression>> TypeToExp = CreateTypeToExp();

        static IReadOnlyDictionary<Type, Func<Expression>> CreateTypeToExp()
        {
            var d = new Dictionary<Type, Func<Expression>>();
            //  The same conversions as SpanParsers on 64 bit processes (the 32 bit parsers are used on 32 bit processes)
            if (Environment.Is64BitProcess)
            {
                var s64 = Get(nameof(ReadInt64));
                var u64 = Get(nameof(ReadUInt64));
                Expression S64() => Expression.Call(s64, ReadTypeCache.ParState, ReadTypeCache.ParEndOn);
                Expression U64() => Expression.Call(u64, ReadTypeCache.ParState, ReadTypeCache.ParEndOn);
                d[typeof(Int64)] = S64;
                d[typeof(UInt64)] = U64;
                d[typeof(Int32)] = () => Expression.ConvertChecked(S64(), typeof(Int32));
                d[typeof(Int16)] = () => Expression.ConvertChecked(S64(), typeof(Int16));
                d[typeof(SByte)] = () => Expression.ConvertChecked(S64(), typeof(SByte));
                d[typeof(UInt32)] = () => Expression.ConvertChecked(U64(), typeof(UInt32));
                d[typeof(UInt16)] = () => Expression.ConvertChecked(U64(), typeof(UInt16));
                d[typeof(Byte)] = () => Expression.ConvertChecked(U64(), typeof(Byte));
            }
            var dbl = Get(nameof(ReadDouble));
            var sgl = Get(nameof(ReadSingle));
            var dec = Get(nameof(ReadDecimal));
            var bln = Get(nameof(ReadBoolean));
            d[typeof(Double)] = () => Expression.Call(dbl, ReadTypeCache.ParState, ReadTypeCache.ParEndOn);
            d[typeof(Single)] = () => Expression.Call(sgl, ReadTypeCache.ParState, ReadTypeCache.ParEndOn);
            d[typeof(Decimal)] = () => Expression.Call(dec, ReadTypeCache.ParState, ReadTypeCache.ParEndOn);
            d[typeof(Boolean)] = () => Expression.Call(bln, ReadTypeCache.ParState, ReadTypeCache.ParEndOn);
            return d.ToFrozenDictionary();
        }

        #endregion//Expressions
    }
}
