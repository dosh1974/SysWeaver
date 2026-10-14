using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SysWeaver
{

    /// <summary>
    /// Low level string functions for char spans
    /// </summary>
    public static unsafe class SpanStringExt
    {
        /// <summary>
        /// Create a lowercase string from a span (invariant culture, char by char)
        /// </summary>
        /// <param name="text">The chars to convert</param>
        /// <returns>A new string (always allocated)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToLowerCaseString(this ReadOnlySpan<Char> text)
            => String.Create(text.Length, text, CreateLowerCasedString);

        /// <summary>
        /// Create an uppercase string from a span (invariant culture, char by char)
        /// </summary>
        /// <param name="text">The chars to convert</param>
        /// <returns>A new string (always allocated)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToUpperCaseString(this ReadOnlySpan<Char> text)
            => String.Create(text.Length, text, CreateUpperCasedString);


        /// <summary>
        /// Concat spans into a string with zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="a">The first part</param>
        /// <param name="b">The second part</param>
        /// <returns>A new string with all the parts (empty if all parts are empty)</returns>
        /// <remarks>A total length above Int32.MaxValue throws an OutOfMemoryException (String.Concat)</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ConcatToString(this ReadOnlySpan<char> a, ReadOnlySpan<char> b)
            => String.Concat(a, b);

        /// <summary>
        /// Concat spans into a string with zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="a">The first part</param>
        /// <param name="b">The second part</param>
        /// <param name="c">The third part</param>
        /// <returns>A new string with all the parts (empty if all parts are empty)</returns>
        /// <remarks>A total length above Int32.MaxValue throws an OutOfMemoryException (String.Concat)</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ConcatToString(this ReadOnlySpan<char> a, ReadOnlySpan<char> b, ReadOnlySpan<char> c)
            => String.Concat(a, b, c);

        /// <summary>
        /// Concat spans into a string with zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="a">The first part</param>
        /// <param name="b">The second part</param>
        /// <param name="c">The third part</param>
        /// <param name="d">The fourth part</param>
        /// <returns>A new string with all the parts (empty if all parts are empty)</returns>
        /// <remarks>A total length above Int32.MaxValue throws an OutOfMemoryException (String.Concat)</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ConcatToString(this ReadOnlySpan<char> a, ReadOnlySpan<char> b, ReadOnlySpan<char> c, ReadOnlySpan<char> d)
            => String.Concat(a, b, c, d);


        /// <summary>
        /// Upper cases the source (the state) into a new string, using the invariant culture
        /// </summary>
        static readonly SpanAction<Char, ReadOnlySpan<Char>> CreateUpperCasedString = (to, src) =>
        {
            var t = CharPtrTools.Ti;
            var l = to.Length;
            while (l > 0)
            {
                --l;
                to[l] = t.ToUpper(src[l]);
            }
        };


        /// <summary>
        /// Lower cases the source (the state) into a new string, using the invariant culture
        /// </summary>
        static readonly SpanAction<Char, ReadOnlySpan<Char>> CreateLowerCasedString = (to, src) =>
        {
            var t = CharPtrTools.Ti;
            var l = to.Length;
            while (l > 0)
            {
                --l;
                to[l] = t.ToLower(src[l]);
            }
        };



    }


}
