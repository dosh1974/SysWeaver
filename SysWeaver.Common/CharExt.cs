using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SysWeaver
{


    public static class CharExt
    {
        /// <summary>
        /// The number of chars in the case conversion tables (same as the IsLower / IsUpper bit tables), covers Latin, Greek, Cyrillic etc
        /// </summary>
        const int TableCount = 256 * 8;

        static CharExt()
        {
            var upper = GC.AllocateUninitializedArray<Char>(TableCount);
            var lower = GC.AllocateUninitializedArray<Char>(TableCount);
            for (int i = 0; i < TableCount; ++i)
            {
                var c = (Char)i;
                upper[i] = Char.ToUpperInvariant(c);
                lower[i] = Char.ToLowerInvariant(c);
            }
            TableUpper = upper;
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

        static readonly Char[] TableLower;
        static readonly Char[] TableUpper;
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
                c == Char.ToLowerInvariant(c)
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
                c == Char.ToUpperInvariant(c)
                ;
        }


        /// <summary>
        /// Make an culture invariant lower case version of a char
        /// </summary>
        /// <param name="c">The char to transform into a culture invariant lower case</param>
        /// <returns>Culture invariant lower case char</returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Char FastToLower(this Char c) => c < TableCount ? TableLower[c] : Char.ToLowerInvariant(c);

        /// <summary>
        /// Make an culture invariant upper case version of a char
        /// </summary>
        /// <param name="c">The char to transform into a culture invariant upper case</param>
        /// <returns>Culture invariant upper case char</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Char FastToUpper(this Char c) => c < TableCount ? TableUpper[c] : Char.ToUpperInvariant(c);


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
