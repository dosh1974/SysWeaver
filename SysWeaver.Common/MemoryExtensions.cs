using System;
using System.Buffers;

namespace SysWeaver
{

    public static class MemoryExtensions
    {


        /// <summary>
        /// Converts some data into a hexadecimal string
        /// </summary>
        /// <param name="bytes">The data</param>
        /// <returns>A hexadecimal string</returns>
        public static String ToHex(this ReadOnlyMemory<Byte> bytes) => String.Create(bytes.Length * 2, bytes, WriteHexAction);


        static readonly Char[] ToHexDigits = "0123456789abcdef".ToCharArray();

        static void WriteHex(Span<Char> to, ReadOnlyMemory<Byte> data)
        {
            var ch = ToHexDigits;
            var s = data.Span;
            var l = data.Length;
            int o = 0;            
            for (int i = 0; i < l; ++ i)
            {
                var b = s[i];
                var t = b;
                b >>= 4;
                t &= 0xf;
                to[o] = ch[b];
                ++o;
                to[o] = ch[t];
                ++o;
            }
        }

        internal static readonly SpanAction<Char, ReadOnlyMemory<Byte>> WriteHexAction = WriteHex;


    }

}
