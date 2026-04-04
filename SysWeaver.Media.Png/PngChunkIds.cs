using System;

namespace SysWeaver.Media.Png
{
    public static class PngChunkIds
    {
        /// <summary>
        /// IHDR chunk as a 32-bit integer
        /// </summary>
        public static readonly uint IHDR = Get("IHDR");
        /// <summary>
        /// PLTE chunk as a 32-bit integer
        /// </summary>
        public static readonly uint PLTE = Get("PLTE");
        /// <summary>
        /// IDAT chunk as a 32-bit integer
        /// </summary>
        public static readonly uint IDAT = Get("IDAT");
        /// <summary>
        /// IEND chunk as a 32-bit integer
        /// </summary>
        public static readonly uint IEND = Get("IEND");
        /// <summary>
        /// tRNS chunk as a 32-bit integer
        /// </summary>
        public static readonly uint tRNS = Get("tRNS");


        public static readonly uint tEXt = Get("tEXt");
        public static readonly uint zTXt = Get("zTXt");
        public static readonly uint iTXt = Get("iTXt");

        /// <summary>
        /// Convert a chunk name as a string to the 32 bit value
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static uint Get(String s)
        {
            uint u = 0;
            u |= (Byte)s[0];
            u <<= 8;
            u |= (Byte)s[1];
            u <<= 8;
            u |= (Byte)s[2];
            u <<= 8;
            u |= (Byte)s[3];
            return u;
        }


    }

}
