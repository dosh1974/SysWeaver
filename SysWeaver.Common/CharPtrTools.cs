using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace SysWeaver
{

    /// <summary>
    /// Low level string handling with char pointers.
    /// A range is [start, end), an empty range (or an end before the start) never contains anything (except for the search methods, the other methods expects end &gt;= start).
    /// The caller is responsible for keeping the memory pinned and valid for the duration of a call.
    /// </summary>
    public unsafe static class CharPtrTools
    {

        /// <summary>
        /// Find the first occurence of a string (ordinal) in a range
        /// </summary>
        /// <param name="s">The string to find (may not be null), an empty string is found at the start</param>
        /// <param name="start">The start of the range</param>
        /// <param name="end">The end of the range (exclusive)</param>
        /// <returns>A pointer to the first char of the first occurence, or null if not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Char* IndexOf(String s, Char* start, Char* end)
        {
            fixed (Char* ss = s)
                return IndexOf(ss, s.Length, start, end);
        }

        /// <summary>
        /// Find the first occurence of a string (ordinal) in a range
        /// </summary>
        /// <param name="str">The string to find</param>
        /// <param name="strLen">The number of chars in the string to find, an empty string is found at the start</param>
        /// <param name="start">The start of the range</param>
        /// <param name="end">The end of the range (exclusive)</param>
        /// <returns>A pointer to the first char of the first occurence, or null if not found</returns>
        public static Char* IndexOf(Char* str, int strLen, Char* start, Char* end)
        {
            if (end < start)
                return null;
            var i = new ReadOnlySpan<Char>(start, (int)(end - start)).IndexOf(new ReadOnlySpan<Char>(str, strLen));
            return i < 0 ? null : start + i;
        }

        /// <summary>
        /// Find the first occurence of a char in a range
        /// </summary>
        /// <param name="f">The char to find</param>
        /// <param name="start">The start of the range</param>
        /// <param name="end">The end of the range (exclusive)</param>
        /// <returns>A pointer to the first occurence, or null if not found</returns>
        public static Char* IndexOf(Char f, Char* start, Char* end)
        {
            if (end <= start)
                return null;
            var i = new ReadOnlySpan<Char>(start, (int)(end - start)).IndexOf(f);
            return i < 0 ? null : start + i;
        }

        /// <summary>
        /// Find the first occurence of any of the chars in a range
        /// </summary>
        /// <param name="vals">The chars to find</param>
        /// <param name="start">The start of the range</param>
        /// <param name="end">The end of the range (exclusive)</param>
        /// <returns>A pointer to the first occurence, or null if not found</returns>
        public static Char* IndexOfAny(SearchValues<Char> vals, Char* start, Char* end)
        {
            if (end <= start)
                return null;
            var i = new ReadOnlySpan<Char>(start, (int)(end - start)).IndexOfAny(vals);
            return i < 0 ? null : start + i;
        }

        /// <summary>
        /// Find the first occurence of any of the chars in a range
        /// </summary>
        /// <param name="c0">A char to find</param>
        /// <param name="c1">A char to find</param>
        /// <param name="start">The start of the range</param>
        /// <param name="end">The end of the range (exclusive)</param>
        /// <returns>A pointer to the first occurence, or null if not found</returns>
        public static Char* IndexOfAny(char c0, char c1, Char* start, Char* end)
        {
            if (end <= start)
                return null;
            var i = new ReadOnlySpan<Char>(start, (int)(end - start)).IndexOfAny(c0, c1);
            return i < 0 ? null : start + i;
        }

        /// <summary>
        /// Find the first occurence of any of the chars in a range
        /// </summary>
        /// <param name="c0">A char to find</param>
        /// <param name="c1">A char to find</param>
        /// <param name="c2">A char to find</param>
        /// <param name="start">The start of the range</param>
        /// <param name="end">The end of the range (exclusive)</param>
        /// <returns>A pointer to the first occurence, or null if not found</returns>
        public static Char* IndexOfAny(char c0, char c1, char c2, Char* start, Char* end)
        {
            if (end <= start)
                return null;
            var i = new ReadOnlySpan<Char>(start, (int)(end - start)).IndexOfAny(c0, c1, c2);
            return i < 0 ? null : start + i;
        }


        /// <summary>
        /// Trim away whitespaces (<see cref="Char.IsWhiteSpace(char)"/>) from a memory range, by moving the start and end pointers
        /// </summary>
        /// <param name="start">The start of the range, moved forward past any leading white spaces</param>
        /// <param name="end">The end of the range (exclusive), moved backwards past any trailing white spaces</param>
        public static void Trim(ref Char* start, ref Char* end)
        {
            while (start < end)
            {
                if (!Char.IsWhiteSpace(*start))
                    break;
                ++start;
            }
            while (end > start)
            {
                --end;
                if (!Char.IsWhiteSpace(*end))
                {
                    ++end;
                    break;
                }
            }
        }

        /// <summary>
        /// Create a trimmed string (see <c>Trim</c>) with zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="start">The start of the range</param>
        /// <param name="end">The end of the range (exclusive), must not be before the start</param>
        /// <returns>The trimmed string (a new instance, empty if the range only contains white spaces)</returns>
        /// <exception cref="ArgumentOutOfRangeException">The end is before the start</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToTrimmedString(Char* start, Char* end)
        {
            Trim(ref start, ref end);
            return new string(start, 0, (int)(end - start));
        }

        /// <summary>
        /// Create a string from some chars
        /// </summary>
        /// <param name="start">The first char</param>
        /// <param name="len">The number of chars</param>
        /// <returns>A new string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToString(Char* start, int len)
            => new string(start, 0, len);


        /// <summary>
        /// Create a lowercased string (using the invariant culture, same as <see cref="Char.ToLowerInvariant(char)"/> of every char, ASCII is converted vectorized).
        /// With zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="start">The first char</param>
        /// <param name="len">The number of chars</param>
        /// <returns>A new string (always allocated)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToLowerCaseString(Char* start, int len)
            => String.Create(len, (IntPtr)start, CreateLowerCasedString);


        /// <summary>
        /// Create an uppercased string (using the invariant culture, same as <see cref="Char.ToUpperInvariant(char)"/> of every char, ASCII is converted vectorized).
        /// With zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="start">The first char</param>
        /// <param name="len">The number of chars</param>
        /// <returns>A new string (always allocated)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToUpperCaseString(Char* start, int len)
            => String.Create(len, (IntPtr)start, CreateUpperCasedString);


        /// <summary>
        /// The text info of the invariant culture
        /// </summary>
        public static readonly TextInfo Ti = CultureInfo.InvariantCulture.TextInfo;


        /// <summary>
        /// Invariant culture lower casing of a char (a delegate to <see cref="TextInfo.ToLower(char)"/> of <see cref="Ti"/>)
        /// </summary>
        public static readonly Func<Char, Char> ToLower = Ti.ToLower;

        /// <summary>
        /// Invariant culture upper casing of a char (a delegate to <see cref="TextInfo.ToUpper(char)"/> of <see cref="Ti"/>)
        /// </summary>
        public static readonly Func<Char, Char> ToUpper = Ti.ToUpper;

        /// <summary>
        /// Copy some memory to a lowercased version (the same as Char.ToLowerInvariant of every char), the source and destination can be the same memory.
        /// Partially overlapping memory is supported (memmove semantics).
        /// </summary>
        /// <param name="dest">The destination</param>
        /// <param name="source">The source</param>
        /// <param name="length">The number of chars, nothing is done if it's zero or negative</param>
        public static void CopyLowerCased(Char* dest, Char* source, int length)
        {
            if (length <= 0)
                return;
            if (dest == source)
            {
                LowerInPlace(new Span<Char>(dest, length));
                return;
            }
            if (Overlaps(dest, source, length))
            {
                // Partially overlapping, process in the direction that reads every source char before it's overwritten (memmove semantics)
                if (dest < source)
                {
                    for (int i = 0; i < length; ++i)
                        dest[i] = Char.ToLowerInvariant(source[i]);
                    return;
                }
                while (length > 0)
                {
                    --length;
                    dest[length] = Char.ToLowerInvariant(source[length]);
                }
                return;
            }
            LowerCore(new ReadOnlySpan<Char>(source, length), new Span<Char>(dest, length));
        }

        /// <summary>
        /// Copy some memory to an uppercased version (the same as Char.ToUpperInvariant of every char), the source and destination can be the same memory.
        /// Partially overlapping memory is supported (memmove semantics).
        /// </summary>
        /// <param name="dest">The destination</param>
        /// <param name="source">The source</param>
        /// <param name="length">The number of chars, nothing is done if it's zero or negative</param>
        public static void CopyUpperCased(Char* dest, Char* source, int length)
        {
            if (length <= 0)
                return;
            if (dest == source)
            {
                UpperInPlace(new Span<Char>(dest, length));
                return;
            }
            if (Overlaps(dest, source, length))
            {
                // Partially overlapping, process in the direction that reads every source char before it's overwritten (memmove semantics)
                if (dest < source)
                {
                    for (int i = 0; i < length; ++i)
                        dest[i] = Char.ToUpperInvariant(source[i]);
                    return;
                }
                while (length > 0)
                {
                    --length;
                    dest[length] = Char.ToUpperInvariant(source[length]);
                }
                return;
            }
            UpperCore(new ReadOnlySpan<Char>(source, length), new Span<Char>(dest, length));
        }


        #region Implementation

        /// <summary>
        /// True if the destination and source overlaps (and isn't the same memory)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool Overlaps(Char* dest, Char* source, int length)
            => (dest != source) && (dest < (source + length)) && (source < (dest + length));

        /// <summary>
        /// Lower case (Char.ToLowerInvariant of every char), ASCII runs are vectorized.
        /// The source and destination must not overlap
        /// </summary>
        static void LowerCore(ReadOnlySpan<Char> source, Span<Char> dest)
        {
            for (; ; )
            {
                // ASCII chars (vectorized) up to the first non ASCII char
                if (Ascii.ToLower(source, dest, out int done) == OperationStatus.Done)
                    return;
                // Non ASCII chars, up to the next ASCII char
                var l = source.Length;
                while ((done < l) && (source[done] >= 128))
                {
                    dest[done] = source[done].FastToLower();
                    ++done;
                }
                source = source.Slice(done);
                dest = dest.Slice(done);
            }
        }

        /// <summary>
        /// Upper case (Char.ToUpperInvariant of every char), ASCII runs are vectorized.
        /// The source and destination must not overlap
        /// </summary>
        static void UpperCore(ReadOnlySpan<Char> source, Span<Char> dest)
        {
            for (; ; )
            {
                // ASCII chars (vectorized) up to the first non ASCII char
                if (Ascii.ToUpper(source, dest, out int done) == OperationStatus.Done)
                    return;
                // Non ASCII chars, up to the next ASCII char
                var l = source.Length;
                while ((done < l) && (source[done] >= 128))
                {
                    dest[done] = source[done].FastToUpper();
                    ++done;
                }
                source = source.Slice(done);
                dest = dest.Slice(done);
            }
        }

        /// <summary>
        /// Lower case in place (Char.ToLowerInvariant of every char), ASCII runs are vectorized
        /// </summary>
        static void LowerInPlace(Span<Char> data)
        {
            for (; ; )
            {
                if (Ascii.ToLowerInPlace(data, out int done) == OperationStatus.Done)
                    return;
                var l = data.Length;
                while ((done < l) && (data[done] >= 128))
                {
                    data[done] = data[done].FastToLower();
                    ++done;
                }
                data = data.Slice(done);
            }
        }

        /// <summary>
        /// Upper case in place (Char.ToUpperInvariant of every char), ASCII runs are vectorized
        /// </summary>
        static void UpperInPlace(Span<Char> data)
        {
            for (; ; )
            {
                if (Ascii.ToUpperInPlace(data, out int done) == OperationStatus.Done)
                    return;
                var l = data.Length;
                while ((done < l) && (data[done] >= 128))
                {
                    data[done] = data[done].FastToUpper();
                    ++done;
                }
                data = data.Slice(done);
            }
        }

        #endregion

        #region String creators

        /// <summary>
        /// Lower cases the chars at the pointer (the state) into a new string
        /// </summary>
        static readonly SpanAction<Char, IntPtr> CreateLowerCasedString = (to, src) => LowerCore(new ReadOnlySpan<Char>((Char*)src.ToPointer(), to.Length), to);

        /// <summary>
        /// Upper cases the chars at the pointer (the state) into a new string
        /// </summary>
        static readonly SpanAction<Char, IntPtr> CreateUpperCasedString = (to, src) => UpperCore(new ReadOnlySpan<Char>((Char*)src.ToPointer(), to.Length), to);

        #endregion//String creators


    }


}
