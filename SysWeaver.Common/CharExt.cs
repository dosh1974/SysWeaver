using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SysWeaver
{


    public static class CharExt
    {
        const int SafeLowerCount = 256;
        const int SafeUpperCount = 181;

        static CharExt()
        {
            var upper = GC.AllocateUninitializedArray<Byte>(SafeUpperCount);
            for (int i = 0; i < SafeUpperCount; ++ i)
            {
                var c = (Char)i;
                var cu = Char.ToUpperInvariant(c);
#if DEBUG
                if (cu >= 256)
                    throw new Exception("Internal error!");
#endif//DEBUG
                upper[i] = (Byte)cu;
            }
            TableUpper = upper;

            var lower = GC.AllocateUninitializedArray<Byte>(SafeLowerCount);
            for (int i = 0; i < SafeLowerCount; ++i)
            {
                var c = (Char)i;
                var cl = Char.ToLowerInvariant(c);
#if DEBUG
                if (cl >= 256)
                    throw new Exception("Internal error!");
#endif//DEBUG
                lower[i] = (Byte)cl;
            }
            TableLower = lower;


            var isLower = new Byte[256];
            var isUpper = new Byte[256];

            int mask = 1;
            for (int i = 0; i < (256 * 8); ++i)
            {
                var c = (Char)i;
                int bi = i >> 3;
                var cl = Char.ToLowerInvariant(c);
                var cu = Char.ToUpperInvariant(c);
                if (c == cl)
                    isLower[bi] |= (Byte)mask;
                if (c == cu)
                    isUpper[bi] |= (Byte)mask;
                mask += mask;
                if (mask > 255)
                    mask = 1;
            }
            TableIsLower = isLower;
            TableIsUpper = isUpper;
        }

        static readonly Byte[] TableLower;
        static readonly Byte[] TableUpper;
        static readonly Byte[] TableIsLower;
        static readonly Byte[] TableIsUpper;


        /// Make an culture invariant upper case version of a char
        public static readonly Func<Char, Char> FastLower = CultureInfo.InvariantCulture.TextInfo.ToLower;

        /// Make an culture invariant upper case version of a char
        public static readonly Func<Char, Char> FastUpper = CultureInfo.InvariantCulture.TextInfo.ToUpper;


        /// <summary>
        /// A method to check if a char is a lowercased letter or a non-letter (case independent)
        /// </summary>
        /// <param name="c">The char to test</param>
        /// <returns>True if the input is a lowercased letter or a non-letter (case independent)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastIsLowerOrNonLetter(this Char c)
        {
            int i = c;
            return
                i < (256 * 8)
                ?
                ((TableIsLower[i >> 3] & (1 << (i & 7))) != 0)
                :
                c == FastLower(c)
                ;
        }

        /// <summary>
        /// A method to check if a char is a uppercased letter or a non-letter (case independent)
        /// </summary>
        /// <param name="c">The char to test</param>
        /// <returns>True if the input is a uppercased letter or a non-letter (case independent)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastIsUpperOrNonLetter(this Char c)
        {
            int i = c;
            return
                i < (256 * 8)
                ?
                ((TableIsUpper[i >> 3] & (1 << (i & 7))) != 0)
                :
                c == FastUpper(c)
                ;
        }


        /// <summary>
        /// Make an culture invariant lower case version of a char
        /// </summary>
        /// <param name="c">The char to transform into a culture invariant lower case</param>
        /// <returns>Culture invariant lower case char</returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Char FastToLower(this Char c) => c < SafeLowerCount ? (Char)TableLower[c] : FastLower(c);

        /// <summary>
        /// Make an culture invariant upper case version of a char
        /// </summary>
        /// <param name="c">The char to transform into a culture invariant upper case</param>
        /// <returns>Culture invariant upper case char</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Char FastToUpper(this Char c) => c < SafeUpperCount ? (Char)TableUpper[c] : FastUpper(c);


        /// <summary>
        /// Convert a hexadecimal character to it's decimal value, only '0' - '9', 'a' - 'f' or 'A' - 'F' is valid, will throw on invalid input
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static int HexValue(this Char c)
        {
            if (c < '0')
                throw new Exception("'" + c + "' is not a valid hex char!");
            if (c <= '9')
                return c - '0';
            if (c < 'A')
                throw new Exception("'" + c + "' is not a valid hex char!");
            if (c <= 'F')
                return c - ('A' - 10);
            if (c < 'a')
                throw new Exception("'" + c + "' is not a valid hex char!");
            if (c <= 'f')
                return c - ('a' - 10);
            throw new Exception("'" + c + "' is not a valid hex char!");
        }

    }

}
