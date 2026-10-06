using System;
using System.Runtime.CompilerServices;

namespace SysWeaver
{

    /// <summary>
    /// Hexadecimal conversions of byte arrays
    /// </summary>
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Converts some data into a lower case hexadecimal string (two chars per byte).
        /// Only the returned string is allocated.
        /// </summary>
        /// <param name="bytes">The data</param>
        /// <returns>A lower case hexadecimal string (empty if the data is empty)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">The data is too large (the string would be longer than int.MaxValue chars)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToHex(this Byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            return Convert.ToHexStringLower(bytes);
        }

        /// <summary>
        /// Convert a hexadecimal string to a byte array.
        /// Lower case, upper case and mixed case digits are accepted, no prefix ("0x"), white spaces or separators are allowed.
        /// </summary>
        /// <param name="hex">Hexadecimal string (two hexadecimal digits for every byte)</param>
        /// <returns>The bytes encoded in the hexadecimal string (an empty array if the text is empty)</returns>
        /// <exception cref="FormatException">The length of the text is odd, or the text contains a char that isn't a hexadecimal digit</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Byte[] FromHex(ReadOnlySpan<Char> hex) => Convert.FromHexString(hex);

    }



}
