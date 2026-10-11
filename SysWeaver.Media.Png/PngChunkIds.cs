using System;

namespace SysWeaver.Media.Png
{
    /// <summary>
    /// Well known png chunk types as 32-bit integers (as used by <see cref="PngChunk.Id"/>)
    /// </summary>
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


        /// <summary>
        /// tEXt chunk (Latin-1 text) as a 32-bit integer
        /// </summary>
        public static readonly uint tEXt = Get("tEXt");
        /// <summary>
        /// zTXt chunk (compressed Latin-1 text) as a 32-bit integer
        /// </summary>
        public static readonly uint zTXt = Get("zTXt");
        /// <summary>
        /// iTXt chunk (international UTF-8 text) as a 32-bit integer
        /// </summary>
        public static readonly uint iTXt = Get("iTXt");

        /// <summary>
        /// Convert a chunk name as a string to the 32 bit value (the first char in the most significant byte, as stored in a png file).
        /// Only the first 4 chars are used, and only the low 8 bits of each char.
        /// </summary>
        /// <param name="s">The chunk name, ex: "IHDR"</param>
        /// <returns>The chunk name as a 32-bit integer</returns>
        /// <exception cref="IndexOutOfRangeException">The string is shorter than 4 chars</exception>
        /// <exception cref="NullReferenceException">The string is null</exception>
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
