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
