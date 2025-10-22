using System;

namespace SysWeaver
{

    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Converts some data into a hexadecimal string
        /// </summary>
        /// <param name="bytes">The data</param>
        /// <returns>A hexadecimal string</returns>
        public static String ToHex(this Byte[] bytes) => String.Create(bytes.Length * 2, bytes, MemoryExtensions.WriteHexAction);

    



        static Byte ReadNibble(char c)
        {
            if (c >= '0' && c <= '9')
                return (Byte)(c - '0');
            if (c >= 'a' && c <= 'f')
                return (Byte)(c - ('a' - 10));
            if (c >= 'A' && c <= 'F')
                return (Byte)(c - ('A' - 10));
            throw new FormatException("Expected a hex nibble, found '" + c + "'");
        }

        /// <summary>
        /// Convert a hexadecimal string to a byte array
        /// </summary>
        /// <param name="hex">Hexadecimal string</param>
        /// <returns>The bytes encoded in the hexadecimal string</returns>
        /// <exception cref="FormatException"></exception>
        public static Byte[] FromHex(ReadOnlySpan<Char> hex)
        {
            var len = hex.Length;
            if ((len & 1) != 0)
                throw new FormatException("Length of text must be even!");
            var size = len >> 1;
            var d = GC.AllocateUninitializedArray<Byte>(size);
            var dp = d.AsSpan();
            for (int i = 0, o = 0; i < len; ++i, ++o)
            {
                var b = ReadNibble(hex[i]);
                ++i;
                b <<= 4;
                b |= ReadNibble(hex[i]);
                dp[o] = b;
            }
            return d;
        }

   
    }



}
