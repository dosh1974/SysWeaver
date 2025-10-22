using System;
using System.Buffers;

namespace SysWeaver
{
    public static class SpanExt
    {

        public unsafe static bool ContentEqual(this ReadOnlySpan<Byte> firstArray, ReadOnlySpan<Byte> secondArray)
        {
            var arrayLength = firstArray.Length;
            if (secondArray.Length != arrayLength)
                return false;
            var ulCount = arrayLength >> 3;
            fixed (byte* a = firstArray, b = secondArray)
            {
                ulong* aa = (ulong*)a;
                ulong* bb = (ulong*)b;
                for (int i = 0; i < ulCount; ++ i)
                {
                    if (aa[i] != bb[i])
                        return false;
                }
                for (int i = ulCount << 3; i < arrayLength; ++ i)
                {
                    if (a[i] != b[i]) 
                        return false;
                }
            }
            return true;
        }






        static readonly Char[] HexChars = "0123456789abcdef".ToCharArray();


        /// <summary>
        /// Create a hexadecimal string representation of the data, uses the stack so don't call on to large data blobs
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static String ToHexString(this Span<Byte> data) => ToHexString((ReadOnlySpan<Byte>)data);

        /// <summary>
        /// Create a hexadecimal string representation of the data, uses the stack so don't call on to large data blobs
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static String ToHexString(this ReadOnlySpan<Byte> data)
        {
            if (data == null)
                return null;
            var l = data.Length;
            if (l <= 0)
                return String.Empty;
            var h = HexChars;
            Span<Char> temp = stackalloc Char[l + l];
            for (int i = 0, o = 0; i < l; ++ i)
            {
                var b = data[i];
                temp[o] = h[b >> 4];
                ++o;
                temp[o] = h[b & 0xf];
                ++o;
            }
            return new String(temp);
        }


    }

}
