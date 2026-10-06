using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver.Serialization.SwJson.Reader
{
    /// <summary>
    /// Json token level reading on top of <see cref="Utf8Parser"/>: null detection, strings, keys, enums, type names, maybe quoted scalars and skipping unknown values.
    /// </summary>
    /// <remarks>
    /// Most methods read at <see cref="JsonParserState.D"/> (that must be at a non white space char, before the end) and advance it.
    /// End conditions (<see cref="EndOnColon"/>, <see cref="EndOnObject"/>, <see cref="EndOnArray"/>, <see cref="EndOnAll"/>) decide where an unquoted value ends,
    /// they are compared by reference to use a lookup table instead of a delegate call per char.
    /// </remarks>
    static unsafe class Utf8JsonParser
    {

        static bool FuncEndOnColon(Char c) => (c == ':');
        static bool FuncEndOnObject(Char c) => (c == ',') || (c == '}') || (c == '/') || (c <= 32);
        static bool FuncEndOnArray(Char c) => (c == ',') || (c == ']') || (c == '/') || (c <= 32);
        static bool FuncEndOnAll(Char c) => (c == ',') || (c == ']') || (c == '}') || (c == '/') || (c <= 32);

        /// <summary>
        /// End of an unquoted key: ':'.
        /// </summary>
        public static readonly Func<Char, bool> EndOnColon = FuncEndOnColon;
        /// <summary>
        /// End of an unquoted value in an object: ',', '}', '/' (a comment) or a control char / space.
        /// </summary>
        public static readonly Func<Char, bool> EndOnObject = FuncEndOnObject;
        /// <summary>
        /// End of an unquoted value in an array: ',', ']', '/' (a comment) or a control char / space.
        /// </summary>
        public static readonly Func<Char, bool> EndOnArray = FuncEndOnArray;
        /// <summary>
        /// End of an unquoted value anywhere: ',', ']', '}', '/' (a comment) or a control char / space.
        /// </summary>
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

        /// <summary>
        /// Check for a <c>null</c> at the state's position, see <see cref="IsNull(ref byte*, byte*)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullState(JsonParserState state) => IsNull(ref state.D, state.E);

        /// <summary>
        /// Check for the literal <c>null</c> (followed by the end of data or an <see cref="EndOnAll"/> char).
        /// </summary>
        /// <param name="d">The position, moved to after the <c>null</c> if found, else unchanged</param>
        /// <param name="e">The end of the data</param>
        /// <returns>True if a <c>null</c> was read</returns>
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

        /// <summary>
        /// Read a quoted json string (with escapes) or <c>null</c>.
        /// </summary>
        /// <remarks>An unterminated string at the end of the data is returned as is (no exception).</remarks>
        /// <param name="state">The parser state, positioned at the opening quote</param>
        /// <returns>The string, null for a json <c>null</c></returns>
        /// <exception cref="Exception">The value isn't a string or <c>null</c></exception>
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

        /// <summary>
        /// Read a quoted type name (the value of <c>"$type"</c>) and resolve it, see <see cref="ReadTypeCache.ResolveType(JsonParserState, byte*, int, bool)"/>.
        /// </summary>
        /// <exception cref="Exception">The value isn't quoted, or the type can't be found</exception>
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


        /// <summary>
        /// Decode an (already unescaped) UTF8 span to a string, used for string dictionary keys.
        /// </summary>
        /// <remarks>Decoding stops at the first NUL char.</remarks>
        public static String ToUtf8String(JsonParserState s, ReadOnlySpan<Byte> d)
        {
            fixed (Byte* p = d)
            {
                var dd = p;
                var e = p + d.Length;
                return Utf8Parser.ReadUtf8String(ref s.Temp, ref dd, e, (Char)0);
            }
        }


        /// <summary>
        /// Skip the value of an unknown member.
        /// </summary>
        /// <remarks>
        /// Strings are skipped to the next quote char without considering escapes (so a string containing <c>\"</c> isn't skipped correctly).
        /// Object and array values are not supported.
        /// </remarks>
        /// <param name="d">The position (at the value), set to after the value</param>
        /// <param name="e">The end of the data</param>
        /// <param name="endOn">The end condition for an unquoted value</param>
        /// <exception cref="Exception">The value is an object or an array, or a string isn't terminated</exception>
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


        /// <summary>
        /// Unescape a quoted key into <see cref="JsonParserState.TempB"/> as UTF8.
        /// </summary>
        /// <param name="state">The parser state</param>
        /// <param name="ret">Set to the unescaped UTF8 key (valid until the temp buffer is used again)</param>
        /// <param name="s">The start of the key (after the opening quote)</param>
        /// <param name="d">The position after the closing quote</param>
        /// <returns>Always true</returns>
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

        /// <summary>
        /// Read an object key, quoted (escapes are decoded) or unquoted (up to the <paramref name="endOn"/> condition).
        /// </summary>
        /// <param name="state">The parser state</param>
        /// <param name="ret">Set to the UTF8 key, points into the data or (for escaped keys) into a temp buffer</param>
        /// <param name="d">The position (at the key), set to after the key (after the closing quote, or before the end char)</param>
        /// <param name="e">The end of the data</param>
        /// <param name="endOn">The end condition for an unquoted key</param>
        /// <returns>False if the position is at a '}' (no more keys, the position isn't moved)</returns>
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

        /// <summary>
        /// Read a quoted or unquoted string, escapes are NOT decoded.
        /// </summary>
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
        /// <remarks>Names are case sensitive, numbers and flag combinations ("A, B") are accepted, escapes are not decoded.</remarks>
        /// <exception cref="ArgumentException">The value isn't a valid name or number</exception>
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

        /// <summary>
        /// Read a quoted or unquoted ASCII string, escapes are NOT decoded.
        /// </summary>
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

        /// <summary>
        /// Read a quoted ASCII string (with escapes) or <c>null</c>.
        /// </summary>
        /// <exception cref="Exception">The value isn't a string or <c>null</c></exception>
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


        /// <summary>
        /// Get the raw bytes of a quoted (up to the next quote, escapes are not considered) or unquoted scalar value, used for numbers, booleans etc.
        /// </summary>
        /// <returns>A span into the data (without the quotes)</returns>
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

        /// <summary>
        /// Get the raw bytes of a quoted value (up to the next quote, escapes are not considered), used for dates, time spans and guids.
        /// </summary>
        /// <returns>A span into the data (without the quotes)</returns>
        /// <exception cref="Exception">The value isn't quoted (note: null isn't accepted)</exception>
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
