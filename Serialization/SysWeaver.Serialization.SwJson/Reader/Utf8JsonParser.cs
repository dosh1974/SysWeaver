using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver.Serialization.SwJson.Reader
{
    static unsafe class Utf8JsonParser
    {

        static bool FuncEndOnColon(Char c) => (c == ':');
        static bool FuncEndOnObject(Char c) => (c == ',') || (c == '}') || (c == '/') || (c <= 32);
        static bool FuncEndOnArray(Char c) => (c == ',') || (c == ']') || (c == '/') || (c <= 32);
        static bool FuncEndOnAll(Char c) => (c == ',') || (c == ']') || (c == '}') || (c == '/') || (c <= 32);

        public static readonly Func<Char, bool> EndOnColon = FuncEndOnColon;
        public static readonly Func<Char, bool> EndOnObject = FuncEndOnObject;
        public static readonly Func<Char, bool> EndOnArray = FuncEndOnArray;
        public static readonly Func<Char, bool> EndOnAll = FuncEndOnAll;

        /// <summary>
        /// A table with the result of the end condition for all chars below 256 (1 = end), built from the delegate
        /// </summary>
        static Byte[] CreateEndTable(Func<Char, bool> f)
        {
            var t = new Byte[256];
            for (int i = 0; i < 256; ++i)
                t[i] = f((Char)i) ? (Byte)1 : (Byte)0;
            return t;
        }

        static readonly Byte[] TableEndOnColon = CreateEndTable(FuncEndOnColon);
        static readonly Byte[] TableEndOnObject = CreateEndTable(FuncEndOnObject);
        static readonly Byte[] TableEndOnArray = CreateEndTable(FuncEndOnArray);
        static readonly Byte[] TableEndOnAll = CreateEndTable(FuncEndOnAll);

        /// <summary>
        /// Get the lookup table for an end condition (instead of a delegate call per char), null if it's not one of the known end conditions.
        /// All known end conditions are false for chars of 256 and above.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Byte[] GetEndTable(Func<Char, bool> f)
        {
            if (ReferenceEquals(f, EndOnObject))
                return TableEndOnObject;
            if (ReferenceEquals(f, EndOnArray))
                return TableEndOnArray;
            if (ReferenceEquals(f, EndOnAll))
                return TableEndOnAll;
            if (ReferenceEquals(f, EndOnColon))
                return TableEndOnColon;
            return null;
        }


        const char Quote = '"';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullState(JsonParserState state) => IsNull(ref state.D, state.E);

        public static bool IsNull(ref Byte* d, Byte* e)
        {
            if (d >= e)
                return false;
            if ((*d) != 110) // 'n'
                return false;
            ++d;
            if (!Utf8Parser.CompareAscii(ref d, e, "ull"))
            {
                --d;
                return false;
            }
            if (d >= e)
                return true;
            if (!EndOnAll((Char)(*d)))
            {
                d -= 4;
                return false;
            }
            return true;
        }

        public static String ReadQuotedString(JsonParserState state)
        {
            ref var d = ref state.D;
            var e = state.E;
            var c = (Char)(*d);
            if (c != Quote)
            {
                if (IsNull(ref d, e))
                    return null;
                ReadException.ThrowExpectedQuoatedString();
            }
            ++d;
            return Utf8Parser.ReadJsonString(ref state.Temp, ref d, e);
        }

        public static Type ReadAndResolveType(ref Byte* d, Byte* e, JsonParserState state)
        {
            var c = (Char)(*d);
            if (c != Quote)
                ReadException.ThrowExpectedQuoatedString();
            ++d;
            var start = d;
            var isEscaped = Utf8Parser.DetectUtf8RangeEscaped(ref d, e, Quote);
            return ReadTypeCache.ResolveType(state, start, (int)(d - start - 1), isEscaped);
        }


        public static String ToUtf8String(JsonParserState s, ReadOnlySpan<Byte> d)
        {
            fixed (Byte* p = d)
            {
                var dd = p;
                var e = p + d.Length;
                return Utf8Parser.ReadUtf8String(ref s.Temp, ref dd, e, (Char)0);
            }
        }


        public static void SkipUnknown(ref Byte* d, Byte* e, Func<Char, bool> endOn)
        {
            var c = (Char)(*d);
            ReadOnlySpan<Byte> dummy = default;
            if (c == Quote)
            {
                ++d;
#if VALIDATE
                Utf8Parser.GetUtf8Range(ref dummy, ref d, e, c);
#else//VALIDATE
                Utf8Parser.GetAsciiRange(ref dummy, ref d, e, c);
#endif//VALIDATE
                return;
            }
            //  TODO: Handle objects? Arrays?
            if (c == '{')
                ReadException.ThrowUnhandledUknownObject();
            if (c == '[')
                ReadException.ThrowUnhandledUknownArray();
#if VALIDATE
            Utf8Parser.GetUtf8RangeNoLast(ref dummy, ref d, e, endOn);
#else//VALIDATE
            Utf8Parser.GetAsciiRangeNoLast(ref dummy, ref d, e, endOn);
#endif//VALIDATE
        }


        static bool ReadEscapedKey(JsonParserState state, ref ReadOnlySpan<Byte> ret, Byte* s, Byte* d)
        {
            //  Decode the escapes and encode as UTF8 (WriteUnescapedCharArray wrote an extra byte for every non ascii char, and outside of TempB for long keys)
            ref var buf = ref state.Temp;
            var len = Utf8Parser.ReadEscapedUtf8CharArray(ref buf, ref s, d - 1, (Char)0);
            var maxLen = Utf8Parser.UTF8.GetMaxByteCount(len);
            ref var tempB = ref state.TempB;
            if (tempB.Length < maxLen)
                tempB = GC.AllocateUninitializedArray<Byte>(maxLen);
            len = Utf8Parser.UTF8.GetBytes(buf, 0, len, tempB, 0);
            ret = new ReadOnlySpan<Byte>(tempB, 0, len);
            return true;
        }

        public static bool ReadKey(JsonParserState state, ref ReadOnlySpan<Byte> ret, ref Byte* d, Byte* e, Func<Char, bool> endOn)
        {
            var c = (Char)(*d);
            if (c == Quote)
            {
                ++d;
                Byte* start = d;
                if (Utf8Parser.DetectUtf8RangeEscaped(ref d, e, c))
                    return ReadEscapedKey(state, ref ret, start, d);
                ret = new ReadOnlySpan<byte>(start, (int)(d - start - 1));
                return true;
            }
            if (c == '}')
                return false;
#if VALIDATE
            Utf8Parser.GetUtf8RangeNoLast(ref ret, ref d, e, endOn);
#else//VALIDATE
            Utf8Parser.GetAsciiRangeNoLast(ref ret, ref d, e, endOn);
#endif//VALIDATE
            return true;
        }

        public static String ReadUtf8MaybeQuoted(JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            var c = (Char)(*d);
            if (c == Quote)
            {
                ++d;
                return Utf8Parser.ReadUtf8String(ref state.Temp, ref d, e, c);
            }
            return Utf8Parser.ReadUtf8StringNoLast(ref state.Temp, ref d, e, endOn);
        }

        /// <summary>
        /// Read an enum, like Enum.Parse&lt;T&gt;(ReadUtf8MaybeQuoted(state, endOn)) without allocating a string
        /// </summary>
        public static T ReadEnum<T>(JsonParserState state, Func<Char, bool> endOn) where T : struct, Enum
        {
            ref var d = ref state.D;
            var e = state.E;
            var c = (Char)(*d);
            int len;
            if (c == Quote)
            {
                ++d;
                //  Fast path: an exact name or a number (from the UTF8 bytes)
                var rem = new ReadOnlySpan<Byte>(d, (int)(e - d));
                var end = rem.IndexOf((Byte)Quote);
                if ((end >= 0) && EnumReader<T>.TryGet(rem.Slice(0, end), out var fv))
                {
                    d += end + 1;
                    return fv;
                }
                len = Utf8Parser.ReadUtf8Chars(ref state.Temp, ref d, e, c);
            }
            else
            {
                var tbl = GetEndTable(endOn);
                if (tbl != null)
                {
                    var p = d;
                    while ((p < e) && (tbl[*p] == 0))
                        ++p;
                    if (EnumReader<T>.TryGet(new ReadOnlySpan<Byte>(d, (int)(p - d)), out var fv))
                    {
                        d = p;
                        return fv;
                    }
                }
                len = Utf8Parser.ReadUtf8CharsNoLast(ref state.Temp, ref d, e, endOn);
            }
            return Enum.Parse<T>(new ReadOnlySpan<Char>(state.Temp, 0, len), false);
        }

        public static String ReadAsciiMaybeQuoted(JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            var c = (Char)(*d);
            if (c == Quote)
            {
                ++d;
                return Utf8Parser.ReadAsciiString(ref d, e, c);
            }
            return Utf8Parser.ReadAsciiStringNoLast(ref d, e, endOn);
        }

        public static String ReadAsciiQuotedString(JsonParserState state)
        {
            ref var d = ref state.D;
            var e = state.E;
            var c = (Char)(*d);
            if (c != Quote)
            {
                if (IsNull(ref d, e))
                    return null;
                ReadException.ThrowExpectedQuoatedString();
            }
            ++d;
            return Utf8Parser.ReadEscapedAsciiString(ref state.Temp, ref d, e, c);
        }


        public static ReadOnlySpan<Byte> ReadAsciiReadOnlyMemoryMaybeQuoted(JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            var c = (Char)(*d);
            ReadOnlySpan<Byte> ret = default;
            if (c == Quote)
            {
                ++d;
                Utf8Parser.GetAsciiRange(ref ret, ref d, e, c);
                return ret;
            }
            Utf8Parser.GetAsciiRangeNoLast(ref ret, ref d, e, endOn);
            return ret;
        }

        public static ReadOnlySpan<Byte> ReadAsciiReadOnlyMemoryQuoted(JsonParserState state, Func<Char, bool> endOn)
        {
            ref var d = ref state.D;
            var e = state.E;
            var c = (Char)(*d);
            ReadOnlySpan<Byte> ret = default;
            if (c != Quote)
                ReadException.ThrowExpectedQuoatedString();
            ++d;
            Utf8Parser.GetAsciiRange(ref ret, ref d, e, c);
            return ret;
        }


    }

}
