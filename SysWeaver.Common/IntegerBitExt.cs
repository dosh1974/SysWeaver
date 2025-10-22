using System;


namespace SysWeaver
{
    /// <summary>
    /// Extensions to integer types to handle power of two scenarios
    /// </summary>
    public static class IntegerBitExt
    {

        /// <summary>
        /// Create a mask that includes the value minue one.
        /// Examples:
        ///  5 returns 0x07.
        /// 15 returns 0x0f.
        /// 16 returns 0x0f.
        /// 17 returns 0x1f.
        /// </summary>
        /// <param name="v">The max value (non-exclusive)</param>
        /// <returns>The bit mask</returns>
        public static UInt64 MaxMask(this UInt64 v)
        {
            if ((v & (v - 1)) == 0)
                return v - 1;
            return NextPow2Minus1(v);
        }

        /// <summary>
        /// Test if an integer value is a power of two
        /// </summary>
        /// <param name="v">The value to test</param>
        /// <returns>True if the value is a power of two, else false</returns>
        public static bool IsPow2(this UInt64 v)
        {
            return (v & (v - 1)) == 0;
        }

        /// <summary>
        /// Find the next value that is a power of two and return one minus that (aka mask)
        /// </summary>
        /// <param name="v">The value to test</param>
        /// <returns>A value that is the next power of two minus one</returns>
        public static UInt64 NextPow2Minus1(this UInt64 v)
        {
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v |= v >> 32;
            return v;
        }

        /// <summary>
        /// Make sure that a value is a power of two, else find the next greater power of two
        /// </summary>
        /// <param name="v">The value to test</param>
        /// <returns>A value that is the a power of two</returns>
        public static UInt64 EnsurePow2(this UInt64 v)
        {
            if (v != 0)
                --v;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v |= v >> 32;
            ++v;
            return v;
        }


        /// <summary>
        /// Create a mask that includes the value minue one.
        /// Examples:
        ///  5 returns 0x07.
        /// 15 returns 0x0f.
        /// 16 returns 0x0f.
        /// 17 returns 0x1f.
        /// </summary>
        /// <param name="v">The max value (non-exclusive)</param>
        /// <returns>The bit mask</returns>
        public static UInt32 MaxMask(this UInt32 v)
        {
            if ((v & (v - 1)) == 0)
                return v - 1;
            return NextPow2Minus1(v);
        }


        /// <summary>
        /// Test if an integer value is a power of two
        /// </summary>
        /// <param name="v">The value to test</param>
        /// <returns>True if the value is a power of two, else false</returns>
        public static bool IsPow2(this UInt32 v)
        {
            return (v & (v - 1)) == 0;
        }

        /// <summary>
        /// Find the next value that is a power of two and return one minus that (aka mask)
        /// </summary>
        /// <param name="v">The value to test</param>
        /// <returns>A value that is the next power of two minus one</returns>
        public static UInt32 NextPow2Minus1(this UInt32 v)
        {
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            return v;
        }

        /// <summary>
        /// Make sure that a value is a power of two, else find the next greater power of two
        /// </summary>
        /// <param name="v">The value to test</param>
        /// <returns>A value that is the a power of two</returns>
        public static UInt32 EnsurePow2(this UInt32 v)
        {
            if (v != 0)
                --v;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            ++v;
            return v;
        }


        /// <summary>
        /// Create a mask that includes the value minue one.
        /// Examples:
        ///  5 returns 0x07.
        /// 15 returns 0x0f.
        /// 16 returns 0x0f.
        /// 17 returns 0x1f.
        /// </summary>
        /// <param name="vv">The max value (non-exclusive)</param>
        /// <returns>The bit mask</returns>
        public static Int64 MaxMask(this Int64 vv)
        {
            var v = unchecked((UInt64)vv);
            if ((v & (v - 1)) == 0)
                return unchecked((Int64)(v - 1));
            return unchecked((Int64)NextPow2Minus1(v));
        }

        /// <summary>
        /// Test if an integer value is a power of two
        /// </summary>
        /// <param name="vv">The value to test</param>
        /// <returns>True if the value is a power of two, else false</returns>
        public static bool IsPow2(this Int64 vv)
        {
            var v = unchecked ((UInt64)vv);
            return (v & (v - 1)) == 0;
        }

        /// <summary>
        /// Find the next value that is a power of two and return one minus that (aka mask)
        /// </summary>
        /// <param name="vv">The value to test</param>
        /// <returns>A value that is the next power of two minus one</returns>
        public static Int64 NextPow2Minus1(this Int64 vv)
        {
            var v = unchecked((UInt64)vv);
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v |= v >> 32;
            return unchecked((Int64)v);
        }

        /// <summary>
        /// Make sure that a value is a power of two, else find the next greater power of two
        /// </summary>
        /// <param name="vv">The value to test</param>
        /// <returns>A value that is the a power of two</returns>
        public static Int64 EnsurePow2(this Int64 vv)
        {
            var v = unchecked((UInt64)vv);
            if (v != 0)
                --v;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v |= v >> 32;
            ++v;
            return unchecked((Int64)v);
        }



        /// <summary>
        /// Create a mask that includes the value minue one.
        /// Examples:
        ///  5 returns 0x07.
        /// 15 returns 0x0f.
        /// 16 returns 0x0f.
        /// 17 returns 0x1f.
        /// </summary>
        /// <param name="vv">The max value (non-exclusive)</param>
        /// <returns>The bit mask</returns>
        public static Int32 MaxMask(this Int32 vv)
        {
            var v = unchecked((UInt32)vv);
            if ((v & (v - 1)) == 0)
                return unchecked((Int32)(v - 1));
            return unchecked((Int32)NextPow2Minus1(v));
        }

        /// <summary>
        /// Test if an integer value is a power of two
        /// </summary>
        /// <param name="vv">The value to test</param>
        /// <returns>True if the value is a power of two, else false</returns>
        public static bool IsPow2(this Int32 vv)
        {
            var v = unchecked((UInt32)vv);
            return (v & (v - 1)) == 0;
        }

        /// <summary>
        /// Find the next value that is a power of two and return one minus that (aka mask)
        /// </summary>
        /// <param name="vv">The value to test</param>
        /// <returns>A value that is the next power of two minus one</returns>
        public static Int32 NextPow2Minus1(this Int32 vv)
        {
            var v = unchecked((UInt32)vv);
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            return unchecked((Int32)v);
        }

        /// <summary>
        /// Make sure that a value is a power of two, else find the next greater power of two
        /// </summary>
        /// <param name="vv">The value to test</param>
        /// <returns>A value that is the a power of two</returns>
        public static Int32 EnsurePow2(this Int32 vv)
        {
            var v = unchecked((UInt32)vv);
            if (v != 0)
                --v;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            ++v;
            return unchecked((Int32)v);
        }



        static void CreateBin(Span<Char> data, UInt64 v)
        {
            int i = data.Length;
            while (i > 0)
            {
                --i;
                data[i] = (Char)((v & 1) + '0');
                v >>= 1;
            }
        }

        static void CreateBin(Span<Char> data, UInt32 v)
        {
            int i = data.Length;
            while (i > 0)
            {
                --i;
                data[i] = (Char)((v & 1) + '0');
                v >>= 1;
            }
        }

        /// <summary>
        /// Get a binary string representation of the value
        /// </summary>
        /// <param name="v">The value to get the binary string representation from</param>
        /// <param name="bitCount">Number of bits to show (string length)</param>
        /// <returns>A binary string representation of the value</returns>
        public static String AsBinary(this UInt64 v, int bitCount = 0)
        {
            if (bitCount <= 0)
                bitCount = 64;
            if (bitCount > 64)
                bitCount = 64;
            return String.Create(bitCount, v, CreateBin);
        }

        /// <summary>
        /// Get a binary string representation of the value
        /// </summary>
        /// <param name="v">The value to get the binary string representation from</param>
        /// <param name="bitCount">Number of bits to show (string length)</param>
        /// <returns>A binary string representation of the value</returns>
        public static String AsBinary(this Int64 v, int bitCount = 0) => AsBinary((UInt64)v, bitCount);


        /// <summary>
        /// Get a binary string representation of the value
        /// </summary>
        /// <param name="v">The value to get the binary string representation from</param>
        /// <param name="bitCount">Number of bits to show (string length)</param>
        /// <returns>A binary string representation of the value</returns>
        public static String AsBinary(this UInt32 v, int bitCount = 0)
        {
            if (bitCount <= 0)
                bitCount = 32;
            if (bitCount > 32)
                bitCount = 32;
            return String.Create(bitCount, v, CreateBin);
        }

        /// <summary>
        /// Get a binary string representation of the value
        /// </summary>
        /// <param name="v">The value to get the binary string representation from</param>
        /// <param name="bitCount">Number of bits to show (string length)</param>
        /// <returns>A binary string representation of the value</returns>
        public static String AsBinary(this Int32 v, int bitCount = 0) => AsBinary((UInt32)v, bitCount);


    }

}
