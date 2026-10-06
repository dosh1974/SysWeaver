using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SysWeaver.Serialization.SwJson.Reader
{


    /// <summary>
    /// Low level UTF8 parsing on raw pointers: white space / comment skipping, ranges, strings (with and without escapes), base64.
    /// </summary>
    /// <remarks>
    /// Positions are passed as <c>ref Byte*</c> and advanced; the data must be pinned. Callers must ensure that the position is before the end where a char is read.
    /// UTF8 is not validated (invalid sequences decode to garbage, except in the vectorized paths that replace them with U+FFFD).
    /// </remarks>
    static unsafe class Utf8Parser
    {

        /// <summary>
        /// Read one (UTF8 encoded) code point and move to the next char
        /// </summary>
        /// <param name="second">The low surrogate if the code point is above U+FFFF, else 0</param>
        /// <param name="d">The position, must be before <paramref name="e"/></param>
        /// <param name="e">The end of the data</param>
        /// <returns>The char (the high surrogate if the code point is above U+FFFF)</returns>
        public static Char ReadUtf8Char(out Char second, ref Byte* d, Byte* e)
        {
            uint t = *d;
            ++d;
            if (t >= 128)
                t = CompleteUtf8Char(t, ref d, e);
            if (t < 0x10000)
            {
                second = default;
                return (Char)t;
            }
            t -= 0x10000;
            var h = t >> 10;
            t &= 1023;
            h += 0xd800;
            t += 0xdc00;
            second = (Char)t;
            return (Char)h;
        }

        /// <summary>
        /// Read one byte as a char and move to the next (non ASCII bytes throw in VALIDATE builds only)
        /// </summary>
        /// <returns>A char</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Char ReadAsciiChar(ref Byte* d, Byte* e)
        {
            uint t = *d;
            ++d;
#if VALIDATE
            if (t >= 128)
                ReadException.ThrowUnexpectedCharacter();
#endif//VALIDATE
            return (Char)t;
        }


        /// <summary>
        /// Compare an ascii string to the data, if deemed equal the position is adjusted to the end
        /// </summary>
        /// <param name="d">The position, moved to after the string if equal</param>
        /// <param name="e">The end of the data</param>
        /// <param name="s">The ASCII string to compare</param>
        /// <returns>True if the data at the position starts with <paramref name="s"/></returns>
        public static bool CompareAscii(ref Byte* d, Byte* e, String s)
        {
            var pl = s.Length;
            var end = d + pl;
            if (end > e)
                return false;
            for (int i = 0; i < pl; ++i)
            {
                if ((Char)d[i] != s[i])
                    return false;
            }
            d = end;
            return true;
        }

        /// <summary>
        /// Check if a non ASCII char is white space (see <see cref="Char.IsWhiteSpace(char)"/>).
        /// </summary>
        /// <remarks>Decodes from the lead byte itself (the position isn't advanced past it first), so multi byte chars are decoded wrongly and the position is not set to after the char.</remarks>
        static bool IsUtf8White(uint t, ref Byte* d, Byte* e)
        {
            var test = d;
            ++d;
            t = CompleteUtf8Char(t, ref test, e);
            if (Char.IsWhiteSpace((Char)t))
                return true;
            d = test;
            return false;
        }

        /// <summary>
        /// At a '/': skip a <c>//</c> line comment or a <c>/* */</c> block comment.
        /// </summary>
        /// <returns>True if a comment was skipped</returns>
        static bool IsBlockWhite(ref Byte* d, Byte* e)
        {
            var no = d + 1;
            if (no >= e)
                return false;
            var t = *no;
            if (t == '/')
            {
                d += 2;
                SkipLineComment(ref d, e);
                return true;
            }
            if (t != '*')
                return false;
            d += 2;
            SkipBlockComment(ref d, e);
            return true;
        }

        /// <summary>
        /// Move to the next non white space char (or end of data), skipping <c>//</c> and <c>/* */</c> comments
        /// </summary>
        /// <remarks>All ASCII control chars and space (0-32) are white space.</remarks>
        /// <returns>True if the end was reached</returns>
        /// <exception cref="Exception">A block comment isn't terminated</exception>
        public static bool SkipWhite(ref Byte* d, Byte* e)
        {
            //  A local copy of the position, so that it can be kept in a register (d usually points to a field)
            var p = d;
            while (p < e)
            {
                uint t = *p;
                if (t > 32)
                {
                    if ((t < 128) && (t != '/'))
                        break;
                    //  Rare: non ascii white space or a comment
                    d = p;
                    if (t >= 128 ? IsUtf8White(t, ref d, e) : IsBlockWhite(ref d, e))
                    {
                        p = d;
                        continue;
                    }
                    return d >= e;
                }
                ++p;
            }
            d = p;
            return p >= e;
        }


        #region Span

        /// <summary>
        /// Detect the range that make up a Utf8 string ending in a char (escapes are not considered), position is set to after the ending char
        /// </summary>
        /// <exception cref="Exception">The end char wasn't found</exception>
        public static void GetUtf8Range(ref ReadOnlySpan<Byte> ret, ref Byte* d, Byte* e, Char until)
        {
            var u = (uint)until;
#if DEBUG
            if (u >= 128)
                ReadException.ThrowOnlyAsciiInParameter(u);
#endif//DEBUG
            var s = d;
            var p = s;
            while (p < e)
            {
                uint t = *p;
                ++p;
                if (t == u)
                {
                    d = p;
                    ret = new ReadOnlySpan<byte>(s, (int)(p - s - 1));
                    return;
                }
            }
            d = p;
            ReadException.ThrowEndOfData(until);
        }


        /// <summary>
        /// Find the end of a Utf8 string ending in a char (escaped end chars are skipped), position is set to after the ending char
        /// </summary>
        /// <returns>True if the string contains escapes</returns>
        /// <exception cref="Exception">The end char wasn't found, or an escape is invalid</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DetectUtf8RangeEscaped(ref Byte* d, Byte* e, Char until)
        {
            uint u = until;
#if DEBUG
            if (u >= 128)
                ReadException.ThrowOnlyAsciiInParameter(u);
#endif//DEBUG
            //  The common case: no escapes (vectorized search)
            var rem = new ReadOnlySpan<Byte>(d, (int)(e - d));
            var i = rem.IndexOfAny((Byte)u, (Byte)'\\');
            if ((i >= 0) && (rem[i] == u))
            {
                d += i + 1;
                return false;
            }
            bool gotEsc = false;
            while (d < e)
            {
                uint t = *d;
                ++d;
                if (t == u)
                    return gotEsc;
                if (t == '\\')
                {
                    SkipEsc(ref d, e);
                    gotEsc = true;
                }
            }
            ReadException.ThrowEndOfData(until);
            return gotEsc;
        }

        /// <summary>
        /// Detect the range that make up a Utf8 string ending in a char that meets the condition, position is set to before the ending char
        /// </summary>
        /// <exception cref="Exception">No end char was found</exception>
        public static void GetUtf8RangeNoLast(ref ReadOnlySpan<Byte> ret, ref Byte* d, Byte* e, Func<Char, bool> until)
        {
            var s = d;
            var tbl = Utf8JsonParser.GetEndTable(until);
            if (tbl != null)
            {
                ref var t = ref MemoryMarshal.GetArrayDataReference(tbl);
                var p = s;
                while (p < e)
                {
                    if (Unsafe.Add(ref t, *p) != 0)
                    {
                        d = p;
                        ret = new ReadOnlySpan<byte>(s, (int)(p - s));
                        return;
                    }
                    ++p;
                }
                d = p;
                ReadException.ThrowEndOfData();
            }
            while (d < e)
            {
                uint t = *d;
                ++d;
                if (until((Char)t))
                {
                    --d;
                    ret = new ReadOnlySpan<byte>(s, (int)(d - s));
                    return;
                }
            }
            ReadException.ThrowEndOfData();
        }

        /// <summary>
        /// Detect the range that make up an ASCII string ending in a char (escapes are not considered), position is set to after the ending char
        /// </summary>
        /// <exception cref="Exception">The end char wasn't found</exception>
        public static void GetAsciiRange(ref ReadOnlySpan<Byte> ret, ref Byte* d, Byte* e, Char until)
        {
            var u = (uint)until;
#if DEBUG
            if (u >= 128)
                ReadException.ThrowOnlyAsciiInParameter(u);
#endif//DEBUG
            var s = d;
            var p = s;
            while (p < e)
            {
                uint t = *p;
                ++p;
                if (t == u)
                {
                    d = p;
                    ret = new ReadOnlySpan<byte>(s, (int)(p - s) - 1);
                    return;
                }
#if VALIDATE
                if (t >= 128)
                {
                    d = p;
                    ReadException.ThrowUnexpectedCharacter();
                }
#endif//VALIDATE
            }
            d = p;
            ReadException.ThrowEndOfData(until);
        }

        /// <summary>
        /// Detect the range that make up an ASCII string ending in a char that meets the condition, position is set to before the ending char (or the end of data, no exception)
        /// </summary>
        public static void GetAsciiRangeNoLast(ref ReadOnlySpan<Byte> ret, ref Byte* d, Byte* e, Func<Char, bool> until)
        {
            var s = d;
            var tbl = Utf8JsonParser.GetEndTable(until);
            if (tbl != null)
            {
                ref var t = ref MemoryMarshal.GetArrayDataReference(tbl);
                var p = s;
                while (p < e)
                {
                    uint c = *p;
                    if (Unsafe.Add(ref t, c) != 0)
                        break;
#if VALIDATE
                    if (c >= 128)
                    {
                        d = p;
                        ReadException.ThrowUnexpectedCharacter();
                    }
#endif//VALIDATE
                    ++p;
                }
                d = p;
                ret = new ReadOnlySpan<byte>(s, (int)(d - s));
                return;
            }
            while (d < e)
            {
                uint t = *d;
                ++d;
                if (until((Char)t))
                {
                    --d;
                    ret = new ReadOnlySpan<byte>(s, (int)(d - s));
                    return;
                }
#if VALIDATE
                if (t >= 128)
                    ReadException.ThrowUnexpectedCharacter();
#endif//VALIDATE
            }
            ret = new ReadOnlySpan<byte>(s, (int)(d - s));
            //ReadException.ThrowEndOfData();
        }

        #endregion//Span

        #region String

        /// <summary>
        /// Read a string (no escapes) until the supplied char or the end of data is found, position is set to after the found char
        /// </summary>
        /// <param name="buf">A temp buffer, replaced by a larger one if needed</param>
        /// <param name="d">The current read position, advanced to after the found char (or to <paramref name="e"/>)</param>
        /// <param name="e">The end of the data (exclusive)</param>
        /// <param name="until">The char that terminates the read (not included)</param>
        public static String ReadUtf8String(ref Char[] buf, ref Byte* d, Byte* e, Char until)
        {
            var index = ReadUtf8Chars(ref buf, ref d, e, until);
            return index <= 0 ? String.Empty : new String(buf, 0, index);
        }

        /// <summary>
        /// Read chars (no escapes) into the buffer until the supplied char or the end of data is found, position is set to after the found char
        /// </summary>
        /// <param name="buf">A temp buffer, replaced by a larger one if needed</param>
        /// <param name="d">The current read position, advanced to after the found char (or to <paramref name="e"/>)</param>
        /// <param name="e">The end of the data (exclusive)</param>
        /// <param name="until">The char that terminates the read (not included)</param>
        /// <returns>The number of chars read</returns>
        public static int ReadUtf8Chars(ref Char[] buf, ref Byte* d, Byte* e, Char until)
        {
            var bufLen = buf.Length;
            int index = 0;
            while (d < e)
            {
                var c = ReadUtf8Char(out var s, ref d, e);
                if (c == until)
                    break;
                var ni = index + 1;
                if (ni >= bufLen)
                    bufLen = Grow(ref buf);
                buf[index] = c;
                index = ni;
                if (s != 0)
                {
                    buf[index] = s;
                    ++index;
                }
            }
            return index;
        }

        /// <summary>
        /// Read a string until a char is found that meets the end condition, position is set to before the char that met the condition
        /// </summary>
        /// <param name="buf">A temp buffer, replaced by a larger one if needed</param>
        /// <param name="d">The position</param>
        /// <param name="e">The end of the data</param>
        /// <param name="until">Evaluated per char, return true to stop reading the string</param>
        /// <returns>The string read</returns>
        public static String ReadUtf8StringNoLast(ref Char[] buf, ref Byte* d, Byte* e, Func<Char, bool> until)
        {
            var index = ReadUtf8CharsNoLast(ref buf, ref d, e, until);
            return index <= 0 ? String.Empty : new String(buf, 0, index);
        }

        /// <summary>
        /// Read chars into the buffer until a char is found that meets the end condition, position is set to before the char that met the condition
        /// </summary>
        /// <returns>The number of chars read</returns>
        public static int ReadUtf8CharsNoLast(ref Char[] buf, ref Byte* d, Byte* e, Func<Char, bool> until)
        {
            var bufLen = buf.Length;
            int index = 0;
            var tbl = Utf8JsonParser.GetEndTable(until);
            while (d < e)
            {
                var last = d;
                var c = ReadUtf8Char(out var s, ref d, e);
                if (tbl != null ? ((c < 256) && (tbl[c] != 0)) : until(c))
                {
                    d = last;
                    break;
                }
                var ni = index + 1;
                if (ni >= bufLen)
                    bufLen = Grow(ref buf);
                buf[index] = c;
                index = ni;
                if (s != 0)
                {
                    buf[index] = s;
                    ++index;
                }
            }
            return index;
        }

        /// <summary>
        /// Read a string, assuming only ASCII codes (char codes less than 128) until the supplied char is found, position is set to after the found char
        /// </summary>
        /// <param name="d">The position</param>
        /// <param name="e">The end of the data</param>
        /// <param name="until">The char that stops the string reading</param>
        /// <returns>The string read</returns>
        public static String ReadAsciiString(ref Byte* d, Byte* e, Char until)
        {
            var s = d;
            var p = s;
            while (p < e)
            {
                var c = *p;
                ++p;
                if (c == until)
                    break;
#if VALIDATE
                if (c >= 128)
                {
                    d = p;
                    ReadException.ThrowUnexpectedCharacter();
                }
#endif//VALIDATE
            }
            d = p;
            var l = (int)(p - s - 1);
            return l <= 0 ? String.Empty : String.Create(l, new IntPtr(s), WriteAciiStringAction);
        }

        /// <summary>
        /// Read a string until a char is found that meets the end condition, assuming only ASCII codes (char codes less than 128), position is set to before the char that met the condition
        /// </summary>
        /// <param name="d">The position</param>
        /// <param name="e">The end of the data</param>
        /// <param name="until">Evaluated per char, return true to stop reading the string</param>
        /// <returns>The string read</returns>
        public static String ReadAsciiStringNoLast(ref Byte* d, Byte* e, Func<Char, bool> until)
        {
            var s = d;
            var tbl = Utf8JsonParser.GetEndTable(until);
            if (tbl != null)
            {
                ref var t = ref MemoryMarshal.GetArrayDataReference(tbl);
                var p = s;
                while (p < e)
                {
                    uint c = *p;
                    if (Unsafe.Add(ref t, c) != 0)
                        break;
#if VALIDATE
                    if (c >= 128)
                    {
                        d = p;
                        ReadException.ThrowUnexpectedCharacter();
                    }
#endif//VALIDATE
                    ++p;
                }
                d = p;
                var tl = (int)(d - s);
                return tl <= 0 ? String.Empty : String.Create(tl, new IntPtr(s), WriteAciiStringAction);
            }
            while (d < e)
            {
                var c = *d;
                ++d;
                if (until((Char)c))
                {
                    --d;
                    break;
                }
#if VALIDATE
                if (c >= 128)
                    ReadException.ThrowUnexpectedCharacter();
#endif//VALIDATE
            }
            var l = (int)(d - s);
            return l <= 0 ? String.Empty : String.Create(l, new IntPtr(s), WriteAciiStringAction);
        }



        /// <summary>
        /// Read chars into the buffer until the supplied char (or the end of data, no exception) is found, decoding JSON escapes (\ is the escape char), position is set to after the found char
        /// </summary>
        /// <remarks>Supports \" \\ \/ \' \b \f \n \r \t and \uXXXX (surrogate pairs are two escapes, each decoded to one char).
        /// A \uXXXX escape must be followed by at least one more byte before <paramref name="e"/>.</remarks>
        /// <param name="buf">A temp buffer, replaced by a larger one if needed</param>
        /// <param name="d">The position</param>
        /// <param name="e">The end of the data</param>
        /// <param name="until">The char that stops the string reading</param>
        /// <returns>The number of chars read</returns>
        /// <exception cref="Exception">An escape sequence is invalid or truncated</exception>
        public static int ReadEscapedUtf8CharArray(ref Char[] buf, ref Byte* d, Byte* e, Char until)
        {
            int index = 0;
            if (until < 128)
            {
                //  Find the next end char or escape (vectorized), transcode everything before it
                var u = (Byte)until;
                for (; ; )
                {
                    var rem = new ReadOnlySpan<Byte>(d, (int)(e - d));
                    var i = rem.IndexOfAny(u, (Byte)'\\');
                    var chunk = i < 0 ? rem : rem.Slice(0, i);
                    if (!chunk.IsEmpty)
                    {
                        //  Max one char per byte
                        var need = index + chunk.Length + 2;
                        while (buf.Length < need)
                            Grow(ref buf);
                        System.Text.Unicode.Utf8.ToUtf16(chunk, buf.AsSpan(index), out _, out var written, true, true);
                        index += written;
                    }
                    if (i < 0)
                    {
                        d = e;
                        return index;
                    }
                    d += i + 1;
                    if (rem[i] == u)
                        return index;
                    var c = Esc(ref d, e);
                    if (index + 1 >= buf.Length)
                        Grow(ref buf);
                    buf[index] = c;
                    ++index;
                }
            }
            var bufLen = buf.Length;
            while (d < e)
            {
                var c = ReadUtf8Char(out var s, ref d, e);
                if (c == until)
                    break;
                if (c == '\\')
                    c = Esc(ref d, e);
                var ni = index + 1;
                if (ni >= bufLen)
                    bufLen = Grow(ref buf);
                buf[index] = c;
                index = ni;
                if (s != 0)
                {
                    buf[index] = s;
                    ++index;
                }
            }
            return index;
        }


        /// <summary>
        /// Read a string until the supplied char is found, decoding JSON escapes, see <see cref="ReadEscapedUtf8CharArray(ref char[], ref byte*, byte*, char)"/>
        /// </summary>
        /// <param name="buf">A temp buffer, replaced by a larger one if needed</param>
        /// <param name="d">The position</param>
        /// <param name="e">The end of the data</param>
        /// <param name="until">The char that stops the string reading</param>
        /// <returns>The string read</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ReadEscapedUtf8String(ref Char[] buf, ref Byte* d, Byte* e, Char until)
        {
            var index = ReadEscapedUtf8CharArray(ref buf, ref d, e, until);
            return index <= 0 ? String.Empty : new String(buf, 0, index);
        }

        /// <summary>
        /// Read a json string (after the opening quote), position is set to after the closing quote.
        /// Strings without escapes (the common case) are decoded directly from the data.
        /// </summary>
        /// <remarks>An unterminated string returns the rest of the data (no exception).</remarks>
        public static String ReadJsonString(ref Char[] buf, ref Byte* d, Byte* e)
        {
            var rem = new ReadOnlySpan<Byte>(d, (int)(e - d));
            // One vectorized search for the end quote, an escape or a non ASCII byte
            var i = rem.IndexOfAny(QuoteEscapeOrNonAscii);
            if ((i >= 0) && (rem[i] == '"'))
            {
                // ASCII without escapes (the common case)
                var s = i == 0 ? String.Empty : UTF8.GetString(d, i);
                d += i + 1;
                return s;
            }
            if ((i >= 0) && (rem[i] >= 0x80))
            {
                // Non ASCII, find the end quote (or an escape) from there
                var j = rem.Slice(i).IndexOfAny((Byte)'"', (Byte)'\\');
                if ((j >= 0) && (rem[i + j] == '"'))
                {
                    j += i;
                    var s = DecodeUtf8(d, j);
                    d += j + 1;
                    return s;
                }
            }
            return ReadEscapedUtf8String(ref buf, ref d, e, '"');
        }

        /// <summary>
        /// The end of a json string ('"'), an escape ('\\') and the bytes of non ASCII chars
        /// </summary>
        static readonly SearchValues<Byte> QuoteEscapeOrNonAscii = SearchValues.Create(CreateQuoteEscapeOrNonAscii());

        static Byte[] CreateQuoteEscapeOrNonAscii()
        {
            var b = new Byte[2 + 128];
            b[0] = (Byte)'"';
            b[1] = (Byte)'\\';
            for (int i = 0; i < 128; ++i)
                b[2 + i] = (Byte)(0x80 + i);
            return b;
        }

        /// <summary>
        /// UTF8 strings up to this number of bytes are decoded on the stack (max one char per byte, 2 KB)
        /// </summary>
        const int MaxStackDecodeBytes = 1024;

        /// <summary>
        /// Decode UTF8 (with non ASCII chars) to a string (invalid sequences are replaced with U+FFFD, like Encoding.UTF8.GetString).
        /// Short strings are decoded to a stack buffer and copied to the string, that is much faster than Encoding.GetString for non ASCII text (it counts the chars in a separate pass).
        /// </summary>
        [SkipLocalsInit]
        static String DecodeUtf8(Byte* d, int length)
        {
            if (length > MaxStackDecodeBytes)
                return UTF8.GetString(d, length);
            Span<Char> chars = stackalloc Char[length];
            System.Text.Unicode.Utf8.ToUtf16(new ReadOnlySpan<Byte>(d, length), chars, out _, out var written, true, true);
            return new String(chars.Slice(0, written));
        }

        /// <summary>
        /// Read an ASCII string until the supplied char is found, decoding JSON escapes (\ is the escape char)
        /// </summary>
        /// <param name="buf">A temp buffer, replaced by a larger one if needed</param>
        /// <param name="d">The position</param>
        /// <param name="e">The end of the data</param>
        /// <param name="until">The char that stops the string reading</param>
        /// <returns>The string read</returns>
        public static String ReadEscapedAsciiString(ref Char[] buf, ref Byte* d, Byte* e, Char until)
        {
            var bufLen = buf.Length;
            int index = 0;
            while (d < e)
            {
                var c = (Char)(*d);
                ++d;
                if (c == until)
                    break;
                if (c == '\\')
                    c = Esc(ref d, e);
#if VALIDATE
                if (c >= 128)
                    ReadException.ThrowUnexpectedCharacter();
#endif//VALIDATE
                if (index >= bufLen)
                    bufLen = Grow(ref buf);
                buf[index] = c;
                ++index;
            }
            return index <= 0 ? String.Empty : String.Create(index, buf, WriteUtf8StringAction);
        }

        #endregion//String

        /// <summary>
        /// Read a byte array from a Base64 encoded string (standard alphabet with padding, no white space or escapes)
        /// </summary>
        /// <remarks>Invalid base64 chars are only detected in VALIDATE (debug) builds, in release builds they silently produce wrong bytes.</remarks>
        /// <param name="d">The position (after the opening quote), set to after the end char</param>
        /// <param name="e">The end of the data</param>
        /// <param name="until">The char that stops the reading</param>
        /// <returns>The data read</returns>
        /// <exception cref="Exception">The length isn't a multiple of 4, or a non ASCII char was found</exception>
        public static Byte[] ReadBase64Bytes(ref Byte* d, Byte* e, Char until)
        {
            var rem = new ReadOnlySpan<Byte>(d, (int)(e - d));
            var end = rem.IndexOf((Byte)until);
            if ((end >= 0) && ((end & 3) == 0))
            {
                var data = rem.Slice(0, end);
                var bad = data.IndexOfAnyInRange((Byte)128, (Byte)255);
                if (bad >= 0)
                    ReadException.ThrowInvalidBase64Char(data[bad]);
                if (end == 0)
                {
                    d += 1;
                    return [];
                }
                int pad = 0;
                if (data[end - 1] == '=')
                {
                    ++pad;
                    if (data[end - 2] == '=')
                        ++pad;
                }
                var blen = ((end * 3) >> 2) - pad;
                var dta = GC.AllocateUninitializedArray<Byte>(blen);
                if ((System.Buffers.Text.Base64.DecodeFromUtf8(data, dta, out var consumed, out var written) == OperationStatus.Done) && (consumed == end) && (written == blen))
                {
                    d += end + 1;
                    return dta;
                }
            }
            return ReadBase64BytesSlow(ref d, e, until);
        }

        static Byte[] ReadBase64BytesSlow(ref Byte* d, Byte* e, Char until)
        {
            var s = d;
            while (d < e)
            {
                var c = *d;
                ++d;
                if (c == until)
                    break;
                if (c >= 128)
                    ReadException.ThrowInvalidBase64Char(c);
            }
            var len = (int)(d - s) - 1;
            if ((len & 3) != 0)
                ReadException.ThrowInvalidBase64Length(len);
            var k = d;
            if (len <= 0)
                return [];
            --k;
            int pad = 0;
            while (k > s)
            {
                --k;
                if (*k != '=')
                    break;
                ++pad;
            }
            var blen = (len * 3) >> 2;
            blen -= pad;
//            var dta = new Byte[blen];
            var dta = GC.AllocateUninitializedArray<Byte>(blen);
            var to = ToOctet;
            fixed (Byte* destPtrX = dta.AsSpan())
            {
                var destPtr = destPtrX;
                while (blen > 0)
                {
                    uint b = to[*s];
#if VALIDATE
                if (b >= 64)
                    ReadException.ThrowInvalidBase64Char(*s);
#endif//VALIDATE
                    ++s;
                    b <<= 6;
                    uint c = to[*s];
#if VALIDATE
                if (c >= 64)
                    ReadException.ThrowInvalidBase64Char(*s);
#endif//VALIDATE
                    b |= c;
                    ++s;
                    b <<= 6;
                    c = to[*s];
#if VALIDATE
                if (c >= 64)
                    ReadException.ThrowInvalidBase64Char(*s);
#endif//VALIDATE
                    b |= c;
                    ++s;
                    b <<= 6;
                    c = to[*s];
#if VALIDATE
                if (c >= 64)
                    ReadException.ThrowInvalidBase64Char(*s);
#endif//VALIDATE
                    ++s;
                    b |= c;

                    --blen;
                    *destPtr = (Byte)(b >> 16);
                    if (blen == 0)
                        break;
                    ++destPtr;

                    --blen;
                    *destPtr = (Byte)(b >> 8);
                    if (blen == 0)
                        break;
                    ++destPtr;

                    --blen;
                    *destPtr = (Byte)b;
                    ++destPtr;
                }
            }
            return dta;
        }

        #region UTF8
        
        /// <summary>
        /// Decode the continuation bytes of a multi byte UTF8 char.
        /// </summary>
        /// <param name="t">The lead byte</param>
        /// <param name="ptr">The position after the lead byte, set to after the char</param>
        /// <param name="end">The end of the data</param>
        /// <returns>The code point</returns>
        static uint CompleteUtf8Char(uint t, ref byte* ptr, byte* end)
        {
            var a = t;
            var utd = (uint)Masks[a >> 3];
            t &= (utd >> 2);
            utd &= 3;
            while (utd > 0)
            {
                if (ptr > end)
                    ReadException.ThrowEndOfDataUtf8();
                t <<= 6;
                a = *ptr;
                ++ptr;
                a &= 0x3f;
                --utd;
                t |= a;
            }
            return t;
        }

        static Byte[] GetUtf8Masks()
        {
            var t = GC.AllocateUninitializedArray<Byte>(32);
            var dt = t.AsSpan();
            for (int i = 0; i < 32; ++i)
            {
                var v = i << 3;
                if ((v & 0xe0) == 0xc0)
                {
                    dt[i] = (31 << 2) + 1;
                    continue;
                }
                if ((v & 0xf0) == 0xe0)
                {
                    dt[i] = (15 << 2) + 2;
                    continue;
                }
                dt[i] = (7 << 2) + 3;
            }
            return t;
        }

        static readonly Byte[] Masks = GetUtf8Masks();


        #endregion//UTF8

        static void SkipLineComment(ref Byte* d, Byte* e)
        {
            while (d < e)
            {
                var t = *d;
                ++d;
                if ((t == 10) || (t == 13))
                    break;
            }
        }

        static void SkipBlockComment(ref Byte* d, Byte* e)
        {
            while (d < e)
            {
                var t = *d;
                ++d;
                if (t == '*')
                {
                    if (d < e)
                    {
                        if (*d == '/')
                        {
                            ++d;
                            return;
                        }
                    }
                }
            }
            ReadException.ThrowExpectedEndOfBlockComment();
        }


        static int Grow(ref Char[] b)
        {
            var l = b.Length;
            var nl = l + l;
            var nb = GC.AllocateUninitializedArray<Char>(nl);
            b.AsSpan().CopyTo(nb.AsSpan().Slice(0, l));
            b = nb;
            return nl;
        }

        static uint HexValue(Char c)
        {
            if ((c >= '0') && (c <= '9'))
                return (uint)(c - '0');
            if ((c >= 'a') && (c <= 'f'))
                return (uint)(c - 'a' + 10);
            if ((c >= 'A') && (c <= 'F'))
                return (uint)(c - 'A' + 10);
            ReadException.ThrowInvalidHexChar(c);
            return 0;
        }

        static void SkipEsc(ref Byte* d, Byte* end)
        {
            if (d >= end)
                ReadException.ThrowEndOfDataEscape();
            var c = (Char)(*d);
            ++d;
            switch (c)
            {
                case '"':
                case '\\':
                case '/':
                case '\'':
                case 'b':
                case 'f':
                case 'n':
                case 'r':
                case 't':
                    return;
                case 'u':
                    if ((d + 4) >= end)
                        ReadException.ThrowEndOfDataEscape();
                    d += 4;
                    return;
                default:
                    ReadException.ThrowInvalidEscapeChar(c);
                    return;
            }
        }

        static Char Esc(ref Byte* d, Byte* end)
        {
            if (d >= end)
                ReadException.ThrowEndOfDataEscape();
            var c = (Char)(*d);
            ++d;
            switch (c)
            {
                case '"':
                    return '"';
                case '\\':
                    return '\\';
                case '/':
                    return '/';
                case '\'':
                    return '\'';
                case 'b':
                    return (Char)0x8;
                case 'f':
                    return (Char)0xc;
                case 'n':
                    return (Char)0xa;
                case 'r':
                    return (Char)0xd;
                case 't':
                    return (Char)0x9;
                case 'u':
                    if ((d + 4) >= end)
                        ReadException.ThrowEndOfDataEscape();
                    uint v = 0;
                    for (int j = 0; j < 4; ++j)
                    {
                        c = (Char)(*d);
                        ++d;
                        v <<= 4;
                        v |= HexValue(c);
                    }
                    return (Char)v;
                default:
                    ReadException.ThrowInvalidEscapeChar(c);
                    return default;
            }
        }

        static Byte[] GetToOctet()
        {
            var b = GC.AllocateUninitializedArray<Byte>(256);
            var db = b.AsSpan();
            for (int i = 0; i < 256; ++i)
            {
                if ((i >= 'A') && (i <= 'Z'))
                {
                    db[i] = (Byte)(i - 'A');
                    continue;
                }
                if ((i >= 'a') && (i <= 'z'))
                {
                    db[i] = (Byte)(i - 'a' + 26);
                    continue;
                }
                if ((i >= '0') && (i <= '9'))
                {
                    db[i] = (Byte)(i - '0' + 52);
                    continue;
                }
                if (i == '+')
                {
                    db[i] = 62;
                    continue;
                }
                if (i == '/')
                {
                    db[i] = 63;
                    continue;
                }
                if (i == '=')
                {
                    db[i] = 0;
                    continue;
                }
                db[i] = 0xff;
            }
            return b;
        }

        static readonly Byte[] ToOctet = GetToOctet();

        static void WriteUtf8String(Span<char> to, Char[] from)
        {
            from.AsSpan().Slice(0, to.Length).CopyTo(to);
        }

        static readonly SpanAction<char, Char[]> WriteUtf8StringAction = WriteUtf8String;

        static void WriteAsciiString(Span<char> to, IntPtr from)
        {
            var p = (Byte*)from.ToPointer();
            var l = to.Length;
            for (int i = 0; i < l; i++)
                to[i] = (Char)p[i];
        }

        static readonly SpanAction<char, IntPtr> WriteAciiStringAction = WriteAsciiString;


        /// <summary>
        /// <see cref="Encoding.UTF8"/>
        /// </summary>
        public static readonly Encoding UTF8 = Encoding.UTF8;
        /// <summary>
        /// <see cref="Encoding.ASCII"/>
        /// </summary>
        public static readonly Encoding ASCII = Encoding.ASCII;

    }

}
