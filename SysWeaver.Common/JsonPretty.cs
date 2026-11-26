using System;
using System.Buffers;
using System.Numerics;

namespace SysWeaver
{
    /// <summary>
    /// Formats existing json text to be more human readable (4 spaces of indentation, one member or item per line, "\n" as new line).
    /// Same rules as Sw.Format.JsonPretty of the web (core/format.js), but the values are never changed: strings, numbers and dates are written exactly as they are in the source.
    /// </summary>
    /// <remarks>
    /// Json that is cut off (ex: capped in the audit) is formatted as far as it goes and ends with "…".
    /// Comments and trailing commas are not supported, and there can be at most <see cref="MaxDepth"/> nested levels.
    /// Escape sequences and control chars in strings are not validated.
    /// </remarks>
    public static class JsonPretty
    {
        /// <summary>
        /// The maximum number of nested objects and arrays, deeper json is not formatted (the size of the indentation is the depth times the number of lines).
        /// </summary>
        public const int MaxDepth = 256;

        /// <summary>
        /// Format some json text.
        /// </summary>
        /// <param name="json">The json text, may be cut off</param>
        /// <returns>The formatted json, or null if the text is null or isn't json</returns>
        public static String Format(String json)
        {
            if (String.IsNullOrEmpty(json))
                return null;
            var o = new ArrayBufferWriter<Char>(GetCapacity(json.Length));
            return TryWrite<Char>(json, o) ? new String(o.WrittenSpan) : null;
        }

        /// <summary>
        /// Format some UTF-8 encoded json.
        /// </summary>
        /// <param name="json">The UTF-8 encoded json (without a byte order mark), may be cut off</param>
        /// <returns>The formatted UTF-8 encoded json, or null if the data isn't json</returns>
        public static Byte[] FormatUtf8(ReadOnlySpan<Byte> json)
        {
            var o = new ArrayBufferWriter<Byte>(GetCapacity(json.Length));
            return TryWrite(json, o) ? o.WrittenSpan.ToArray() : null;
        }

        /// <summary>
        /// Format some UTF-8 encoded json.
        /// </summary>
        /// <param name="json">The UTF-8 encoded json (without a byte order mark), may be cut off</param>
        /// <param name="output">The formatted UTF-8 encoded json is written to this, if false is returned a part of the json may have been written</param>
        /// <returns>True if the json was formatted, false if the data isn't json</returns>
        public static bool TryFormat(ReadOnlySpan<Byte> json, IBufferWriter<Byte> output)
            => TryWrite(json, output);

        /// <summary>
        /// Get an initial buffer size for the formatted json.
        /// </summary>
        /// <param name="length">The length of the source json</param>
        /// <returns>The initial capacity (in the same unit as the length)</returns>
        static int GetCapacity(int length)
            => (int)Math.Min(Math.Max(256L, length * 2L), Array.MaxLength);


        /// <summary>
        /// What must follow
        /// </summary>
        enum Next
        {
            Value,
            ValueOrEnd,
            Key,
            KeyOrEnd,
            Colon,
            CommaOrEnd,
            Nothing,
        }

        enum WordTypes
        {
            Invalid,
            Complete,
            /// <summary>
            /// The start of a valid word
            /// </summary>
            CutOff,
        }

        /// <summary>
        /// Write the formatted json, T is a Byte (UTF-8) or a Char (UTF-16)
        /// </summary>
        static bool TryWrite<T>(ReadOnlySpan<T> json, IBufferWriter<T> output) where T : unmanaged, IBinaryInteger<T>
        {
            var l = json.Length;
            //  True for an object
            Span<bool> stack = stackalloc bool[MaxDepth];
            int depth = 0;
            bool cut = false;
            //  Only a text that starts as an object, array or string is recognized as cut off json (ex: "-" isn't json)
            bool canBeCut = false;
            bool first = true;
            var next = Next.Value;
            int i = 0;
            while (i < l)
            {
                var c = uint.CreateTruncating(json[i]);
                if (c is ' ' or '\t' or '\r' or '\n')
                {
                    ++i;
                    continue;
                }
                if (first)
                {
                    first = false;
                    canBeCut = c is '{' or '[' or '"';
                }
                if (c is ']' or '}')
                {
                    var isObject = c == '}';
                    var empty = next == (isObject ? Next.KeyOrEnd : Next.ValueOrEnd);
                    if ((!empty && (next != Next.CommaOrEnd)) || (depth <= 0))
                        return false;
                    --depth;
                    if (stack[depth] != isObject)
                        return false;
                    if (!empty)
                        NewLine(output, depth);
                    Put(output, c);
                    next = depth > 0 ? Next.CommaOrEnd : Next.Nothing;
                    ++i;
                    continue;
                }
                if (c == ',')
                {
                    if (next != Next.CommaOrEnd)
                        return false;
                    Put(output, c);
                    NewLine(output, depth);
                    next = stack[depth - 1] ? Next.Key : Next.Value;
                    ++i;
                    continue;
                }
                if (c == ':')
                {
                    if (next != Next.Colon)
                        return false;
                    Put(output, c);
                    Put(output, ' ');
                    next = Next.Value;
                    ++i;
                    continue;
                }
                var isKey = next is Next.Key or Next.KeyOrEnd;
                if (!isKey && (next is not (Next.Value or Next.ValueOrEnd)))
                    return false;
                //  The first member or item of an object or array
                if (next is Next.KeyOrEnd or Next.ValueOrEnd)
                    NewLine(output, depth);
                if (c == '"')
                {
                    var e = FindStringEnd(json, i + 1);
                    cut = e < 0;
                    if (cut)
                        e = TrimCutOff(json);
                    output.Write(json.Slice(i, e - i));
                    if (cut)
                        break;
                    i = e;
                    next = isKey ? Next.Colon : depth > 0 ? Next.CommaOrEnd : Next.Nothing;
                    continue;
                }
                if (isKey)
                    return false;
                if (c is '{' or '[')
                {
                    if (depth >= MaxDepth)
                        return false;
                    var isObject = c == '{';
                    stack[depth] = isObject;
                    ++depth;
                    Put(output, c);
                    next = isObject ? Next.KeyOrEnd : Next.ValueOrEnd;
                    ++i;
                    continue;
                }
                var end = i;
                while ((end < l) && IsWordChar(uint.CreateTruncating(json[end])))
                    ++end;
                var word = json.Slice(i, end - i);
                var type = GetWordType(word);
                if (type != WordTypes.Complete)
                {
                    //  Only the last word can be cut off
                    if ((type == WordTypes.Invalid) || (end < l))
                        return false;
                    cut = true;
                }
                output.Write(word);
                next = depth > 0 ? Next.CommaOrEnd : Next.Nothing;
                i = end;
            }
            if (!cut && (next == Next.Nothing))
                return true;
            if (!canBeCut)
                return false;
            if (typeof(T) == typeof(Byte))
            {
                Put(output, 0xe2);
                Put(output, 0x80);
                Put(output, 0xa6);
            }
            else
                Put(output, '…');
            return true;
        }

        static void Put<T>(IBufferWriter<T> output, uint c) where T : unmanaged, IBinaryInteger<T>
        {
            output.GetSpan(1)[0] = T.CreateTruncating(c);
            output.Advance(1);
        }

        static void NewLine<T>(IBufferWriter<T> output, int depth) where T : unmanaged, IBinaryInteger<T>
        {
            var size = 1 + depth * 4;
            var d = output.GetSpan(size).Slice(0, size);
            d[0] = T.CreateTruncating('\n');
            d.Slice(1).Fill(T.CreateTruncating(' '));
            output.Advance(size);
        }

        /// <summary>
        /// Find the end of a string
        /// </summary>
        /// <param name="json">The json</param>
        /// <param name="i">The index after the first quote</param>
        /// <returns>The index after the last quote, -1 if the string is cut off</returns>
        static int FindStringEnd<T>(ReadOnlySpan<T> json, int i) where T : unmanaged, IBinaryInteger<T>
        {
            var quote = T.CreateTruncating('"');
            var escape = T.CreateTruncating('\\');
            while (i < json.Length)
            {
                var n = json.Slice(i).IndexOfAny(quote, escape);
                if (n < 0)
                    return -1;
                i += n;
                if (json[i] == quote)
                    return i + 1;
                i += 2;
            }
            return -1;
        }

        /// <summary>
        /// Get the length without a cut off character (an incomplete UTF-8 sequence or the first half of a surrogate pair) at the end
        /// </summary>
        static int TrimCutOff<T>(ReadOnlySpan<T> json) where T : unmanaged, IBinaryInteger<T>
        {
            var l = json.Length;
            if (typeof(T) != typeof(Byte))
                return Char.IsHighSurrogate((Char)uint.CreateTruncating(json[l - 1])) ? l - 1 : l;
            for (int n = 1; (n <= 3) && (n <= l); ++n)
            {
                var c = uint.CreateTruncating(json[l - n]);
                if ((c & 0xc0) == 0x80)
                    continue;
                var size = c >= 0xf0 ? 4 : c >= 0xe0 ? 3 : c >= 0xc0 ? 2 : 1;
                return n < size ? l - n : l;
            }
            return l;
        }

        static bool IsDigit(uint c) => (c >= '0') && (c <= '9');

        static bool IsWordChar(uint c)
            => IsDigit(c) || ((c >= 'a') && (c <= 'z')) || ((c >= 'A') && (c <= 'Z')) || (c is '-' or '+' or '.' or '_');

        static WordTypes GetWordType<T>(ReadOnlySpan<T> word) where T : unmanaged, IBinaryInteger<T>
        {
            if (word.IsEmpty)
                return WordTypes.Invalid;
            switch (uint.CreateTruncating(word[0]))
            {
                case 't':
                    return GetLiteralType(word, "true");
                case 'f':
                    return GetLiteralType(word, "false");
                case 'n':
                    return GetLiteralType(word, "null");
            }
            return GetNumberType(word);
        }

        static WordTypes GetLiteralType<T>(ReadOnlySpan<T> word, String literal) where T : unmanaged, IBinaryInteger<T>
        {
            var l = word.Length;
            if (l > literal.Length)
                return WordTypes.Invalid;
            for (int i = 0; i < l; ++i)
            {
                if (uint.CreateTruncating(word[i]) != literal[i])
                    return WordTypes.Invalid;
            }
            return l == literal.Length ? WordTypes.Complete : WordTypes.CutOff;
        }

        static WordTypes GetNumberType<T>(ReadOnlySpan<T> word) where T : unmanaged, IBinaryInteger<T>
        {
            var l = word.Length;
            int i = 0;
            if (uint.CreateTruncating(word[0]) == '-')
                ++i;
            if (i >= l)
                return WordTypes.CutOff;
            //  No leading zeros
            if (uint.CreateTruncating(word[i]) == '0')
                ++i;
            else if (!SkipDigits(word, ref i))
                return WordTypes.Invalid;
            if (i >= l)
                return WordTypes.Complete;
            if (uint.CreateTruncating(word[i]) == '.')
            {
                ++i;
                if (i >= l)
                    return WordTypes.CutOff;
                if (!SkipDigits(word, ref i))
                    return WordTypes.Invalid;
                if (i >= l)
                    return WordTypes.Complete;
            }
            if (uint.CreateTruncating(word[i]) is not ('e' or 'E'))
                return WordTypes.Invalid;
            ++i;
            if ((i < l) && (uint.CreateTruncating(word[i]) is '-' or '+'))
                ++i;
            if (i >= l)
                return WordTypes.CutOff;
            return SkipDigits(word, ref i) && (i >= l) ? WordTypes.Complete : WordTypes.Invalid;
        }

        /// <summary>
        /// Skip digits
        /// </summary>
        /// <returns>False if there are none</returns>
        static bool SkipDigits<T>(ReadOnlySpan<T> word, ref int i) where T : unmanaged, IBinaryInteger<T>
        {
            var s = i;
            while ((i < word.Length) && IsDigit(uint.CreateTruncating(word[i])))
                ++i;
            return i > s;
        }

    }

}
