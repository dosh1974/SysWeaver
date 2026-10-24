using System;
using System.Runtime.CompilerServices;

namespace SysWeaver
{
    /// <summary>
    /// Extension methods for byte spans (content compare and hexadecimal formatting)
    /// </summary>
    public static class SpanExt
    {

        /// <summary>
        /// Check if two byte spans have the same length and content.
        /// </summary>
        /// <param name="firstArray">The first data</param>
        /// <param name="secondArray">The second data</param>
        /// <returns>True if both spans have the same length and the same bytes (two empty spans are equal)</returns>
        /// <remarks>
        /// The compare is vectorized and exits on the first difference, so it is NOT a constant time compare
        /// (use System.Security.Cryptography.CryptographicOperations.FixedTimeEquals for secrets if timing attacks are a concern).
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContentEqual(this ReadOnlySpan<Byte> firstArray, ReadOnlySpan<Byte> secondArray)
            => firstArray.SequenceEqual(secondArray);


        /// <summary>
        /// The lower case hexadecimal digits, indexed by the nibble value (0 - 15).
        /// </summary>
        public static readonly Char[] HexChars = "0123456789abcdef".ToCharArray();


        /// <summary>
        /// Write a Byte as hex chars to a span
        /// </summary>
        /// <param name="dest">The target span</param>
        /// <param name="value">The value to write</param>
        /// <param name="offset">An offest into the span</param>
        public static void WriteHex(this Span<Char> dest, Byte value, int offset = 0)
        {
            var h = HexChars;
            dest[offset] = h[value >> 4];
            ++offset;
            dest[offset] = h[value & 0xf];
        }

        /// <summary>
        /// Write a UInt16 as hex chars to a span
        /// </summary>
        /// <param name="dest">The target span</param>
        /// <param name="value">The value to write</param>
        /// <param name="offset">An offest into the span</param>
        public static void WriteHex(this Span<Char> dest, UInt16 value, int offset = 0)
        {
            var h = HexChars;
            dest[offset] = h[value >> 12];
            ++offset;
            dest[offset] = h[(value >> 8) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 4) & 0xf];
            ++offset;
            dest[offset] = h[value & 0xf];
        }

        /// <summary>
        /// Write a UInt32 as hex chars to a span
        /// </summary>
        /// <param name="dest">The target span</param>
        /// <param name="value">The value to write</param>
        /// <param name="offset">An offest into the span</param>
        public static void WriteHex(this Span<Char> dest, UInt32 value, int offset = 0)
        {
            var h = HexChars;
            dest[offset] = h[value >> 28];
            ++offset;
            dest[offset] = h[(value >> 24) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 20) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 16) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 12) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 8) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 4) & 0xf];
            ++offset;
            dest[offset] = h[value & 0xf];
        }

        /// <summary>
        /// Write a UInt64 as hex chars to a span
        /// </summary>
        /// <param name="dest">The target span</param>
        /// <param name="value">The value to write</param>
        /// <param name="offset">An offest into the span</param>
        public static void WriteHex(this Span<Char> dest, UInt64 value, int offset = 0)
        {
            var h = HexChars;
            dest[offset] = h[value >> 60];
            ++offset;
            dest[offset] = h[(value >> 56) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 52) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 48) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 44) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 40) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 36) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 32) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 28) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 24) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 20) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 16) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 12) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 8) & 0xf];
            ++offset;
            dest[offset] = h[(value >> 4) & 0xf];
            ++offset;
            dest[offset] = h[value & 0xf];
        }

        /// <summary>
        /// Create a lower case hexadecimal string representation of the data (two chars per byte).
        /// Only the returned string is allocated.
        /// </summary>
        /// <param name="data">The data to format</param>
        /// <returns>A lower case hexadecimal string with two chars for every byte (empty if the data is empty)</returns>
        /// <exception cref="ArgumentOutOfRangeException">The data is too large (the string would be longer than int.MaxValue chars)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToHexString(this Span<Byte> data) => Convert.ToHexStringLower(data);

        /// <summary>
        /// Create a lower case hexadecimal string representation of the data (two chars per byte).
        /// Only the returned string is allocated.
        /// </summary>
        /// <param name="data">The data to format</param>
        /// <returns>A lower case hexadecimal string with two chars for every byte (empty if the data is empty)</returns>
        /// <exception cref="ArgumentOutOfRangeException">The data is too large (the string would be longer than int.MaxValue chars)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToHexString(this ReadOnlySpan<Byte> data) => Convert.ToHexStringLower(data);


    }


}
