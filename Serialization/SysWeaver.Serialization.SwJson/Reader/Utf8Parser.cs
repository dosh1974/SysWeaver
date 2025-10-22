using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace SysWeaver.Serialization.SwJson.Reader
{


    static unsafe class Utf8Parser
    {

        /// <summary>
        /// Read one char and move to the next char
        /// </summary>
        /// <returns>A char</returns>
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
        /// Read one char and move to the next char
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
        /// <param name="d"></param>
        /// <param name="e"></param>
        /// <param name="s"></param>
        /// <returns></returns>
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
        /// Move to to the next non-whitespace char (or end of data)
        /// </summary>
        /// <returns>True if the end was reached</returns>
        public static bool SkipWhite(ref Byte* d, Byte* e)
        {
            while (d < e)
            {
                uint t = *d;
                if (t >= 128)
                {
                    if (IsUtf8White(t, ref d, e))
                        continue;
                    break;
                }
                if (t == '/')
                {
                    if (IsBlockWhite(ref d, e))
                        continue;
                    break;
                }
                if (t > 32)
                    break;
                ++d;
            }
            return d >= e;
        }


        #region Span

        /// <summary>
        /// Detect the range that make up a Utf8 string ending in a char, position is set to after the ending char
        /// </summary>
        public static void GetUtf8Range(ref ReadOnlySpan<Byte> ret, ref Byte* d, Byte* e, Char until)
        {
            var u = (uint)until;
#if DEBUG
            if (u >= 128)
                ReadException.ThrowOnlyAsciiInParameter(u);
#endif//DEBUG
            var s = d;
            while (d < e)
            {
                uint t = *d;
                ++d;
                if (t == u)
                {
                    ret = new ReadOnlySpan<byte>(s, (int)(d - s - 1));
                    return;
                }
            }
            ReadException.ThrowEndOfData(until);
        }


        /// <summary>
        /// Detect the range that make up a Utf8 string ending in a char, position is set to after the ending char
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DetectUtf8RangeEscaped(ref Byte* d, Byte* e, Char until)
        {
            uint u = until;
#if DEBUG
            if (u >= 128)
                ReadException.ThrowOnlyAsciiInParameter(u);
#endif//DEBUG
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
        /// Detect the range that make up a Utf8 string ending in a char, position is set to before the ending char
        /// </summary>
        public static void GetUtf8RangeNoLast(ref ReadOnlySpan<Byte> ret, ref Byte* d, Byte* e, Func<Char, bool> until)
        {
            var s = d;
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
        /// Detect the range that make up an ASCII string ending in a char, position is set to after the ending char
        /// </summary>
        public static void GetAsciiRange(ref ReadOnlySpan<Byte> ret, ref Byte* d, Byte* e, Char until)
        {
            var u = (uint)until;
#if DEBUG
            if (u >= 128)
                ReadException.ThrowOnlyAsciiInParameter(u);
#endif//DEBUG
            var s = d;
            while (d < e)
            {
                uint t = *d;
                ++d;
                if (t == u)
                {
                    ret = new ReadOnlySpan<byte>(s, (int)(d - s) - 1);
                    return;
                }
#if VALIDATE
                if (t >= 128)
                    ReadException.ThrowUnexpectedCharacter();
#endif//VALIDATE
            }
            ReadException.ThrowEndOfData(until);
        }

        /// <summary>
        /// Detect the range that make up an ASCII string ending in a char, position is set to before the ending char
        /// </summary>
        public static void GetAsciiRangeNoLast(ref ReadOnlySpan<Byte> ret, ref Byte* d, Byte* e, Func<Char, bool> until)
        {
            var s = d;
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
        /// Read a string until the supplied char is found, position is set to after the found char
        /// </summary>
        public static String ReadUtf8String(ref Char[] buf, ref Byte* d, Byte* e, Char until)
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
            return index <= 0 ? String.Empty : String.Create(index, buf, WriteUtf8StringAction);
        }

        /// <summary>
        /// Read a string until a char is found that meets the end condition, position is set to before the char that met the condition
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="d"></param>
        /// <param name="e"></param>
        /// <param name="until">Evaluated per char, return true to stop reading the string</param>
        /// <returns>The string read</returns>
        public static String ReadUtf8StringNoLast(ref Char[] buf, ref Byte* d, Byte* e, Func<Char, bool> until)
        {
            var bufLen = buf.Length;
            int index = 0;
            while (d < e)
            {
                var last = d;
                var c = ReadUtf8Char(out var s, ref d, e);
                if (until(c))
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
            return index <= 0 ? String.Empty : String.Create(index, buf, WriteUtf8StringAction);
        }

        /// <summary>
        /// Read a string, assuming only ASCII codes (char codes less than 128) until the supplied char is found, position is set to after the found char
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        /// <param name="until">The char that stops the string reading</param>
        /// <returns>The string read</returns>
        public static String ReadAsciiString(ref Byte* d, Byte* e, Char until)
        {
            var s = d;
            while (d < e)
            {
                var c = *d;
                ++d;
                if (c == until)
                    break;
#if VALIDATE
                if (c >= 128)
                    ReadException.ThrowUnexpectedCharacter();
#endif//VALIDATE
            }
            var l = (int)(d - s - 1);
            return l <= 0 ? String.Empty : String.Create(l, new IntPtr(s), WriteAciiStringAction);
        }

        /// <summary>
        /// Read a string until a char is found that meets the end condition, assuming only ASCII codes (char codes less than 128), position is set to before the char that met the condition
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        /// <param name="until">Evaluated per char, return true to stop reading the string</param>
        /// <returns>The string read</returns>
        public static String ReadAsciiStringNoLast(ref Byte* d, Byte* e, Func<Char, bool> until)
        {
            var s = d;
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
        /// Read a string until the supplied char is found, with JSON escape supprt \ is the escape char
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="d"></param>
        /// <param name="e"></param>
        /// <param name="until">The char that stops the string reading</param>
        /// <returns>The string read</returns>
        public static int ReadEscapedUtf8CharArray(ref Char[] buf, ref Byte* d, Byte* e, Char until)
        {
            var bufLen = buf.Length;
            int index = 0;
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
        /// Read a string until the supplied char is found, with JSON escape supprt \ is the escape char
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="d"></param>
        /// <param name="e"></param>
        /// <param name="until">The char that stops the string reading</param>
        /// <returns>The string read</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ReadEscapedUtf8String(ref Char[] buf, ref Byte* d, Byte* e, Char until)
        {
            var index = ReadEscapedUtf8CharArray(ref buf, ref d, e, until);
            return index <= 0 ? String.Empty : String.Create(index, buf, WriteUtf8StringAction);
        }

        /// <summary>
        /// Read a string until the supplied char is found, with JSON escape supprt \ is the escape char
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="d"></param>
        /// <param name="e"></param>
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
        /// Read a byte array from a Base64 encoded string
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        /// <param name="until">The char that stops the reading</param>
        /// <returns>The data read</returns>
        public static Byte[] ReadBase64Bytes(ref Byte* d, Byte* e, Char until)
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


        public static readonly Encoding UTF8 = Encoding.UTF8;
        public static readonly Encoding ASCII = Encoding.ASCII;

    }

}
