using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SysWeaver
{


    /// <summary>
    /// Fast culture invariant char case conversions and tests (table based for the first 2048 chars), and hex digit parsing
    /// </summary>
    /// <remarks>
    /// All case conversions give the same result as <see cref="Char.ToLowerInvariant(char)"/> / <see cref="Char.ToUpperInvariant(char)"/>.
    /// </remarks>
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

        /// <summary>
        /// The invariant lower case of the first <see cref="TableCount"/> chars
        /// </summary>
        static readonly Char[] TableLower;

        /// <summary>
        /// The invariant upper case of the first <see cref="TableCount"/> chars
        /// </summary>
        static readonly Char[] TableUpper;

        /// <summary>
        /// One bit per char (of the first <see cref="TableCount"/> chars), set if the char is equal to its invariant lower case
        /// </summary>
        static readonly Byte[] TableIsLower;

        /// <summary>
        /// One bit per char (of the first <see cref="TableCount"/> chars), set if the char is equal to its invariant upper case
        /// </summary>
        static readonly Byte[] TableIsUpper;


        /// <summary>
        /// Make a culture invariant lower case version of a char (a delegate to TextInfo.ToLower of the invariant culture, prefer <see cref="FastToLower(char)"/> for direct calls)
        /// </summary>
        public static readonly Func<Char, Char> FastLower = CultureInfo.InvariantCulture.TextInfo.ToLower;

        /// <summary>
        /// Make a culture invariant upper case version of a char (a delegate to TextInfo.ToUpper of the invariant culture, prefer <see cref="FastToUpper(char)"/> for direct calls)
        /// </summary>
        public static readonly Func<Char, Char> FastUpper = CultureInfo.InvariantCulture.TextInfo.ToUpper;


        /// <summary>
        /// Check if a char is a lowercased letter or a non-letter (case independent), i.e. if <see cref="Char.ToLowerInvariant(char)"/> doesn't change it
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
        /// Check if a char is an uppercased letter or a non-letter (case independent), i.e. if <see cref="Char.ToUpperInvariant(char)"/> doesn't change it
        /// </summary>
        /// <param name="c">The char to test</param>
        /// <returns>True if the input is an uppercased letter or a non-letter (case independent)</returns>
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
        /// Make a culture invariant lower case version of a char (same as <see cref="Char.ToLowerInvariant(char)"/>, table based for the first 2048 chars)
        /// </summary>
        /// <param name="c">The char to transform into a culture invariant lower case</param>
        /// <returns>Culture invariant lower case char</returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Char FastToLower(this Char c) => c < TableCount ? TableLower[c] : Char.ToLowerInvariant(c);

        /// <summary>
        /// Make a culture invariant upper case version of a char (same as <see cref="Char.ToUpperInvariant(char)"/>, table based for the first 2048 chars).
        /// Used for case folding by the case in-sensitive string trees.
        /// </summary>
        /// <param name="c">The char to transform into a culture invariant upper case</param>
        /// <returns>Culture invariant upper case char</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Char FastToUpper(this Char c) => c < TableCount ? TableUpper[c] : Char.ToUpperInvariant(c);


        /// <summary>
        /// Convert a hexadecimal character to it's decimal value, only '0' - '9', 'a' - 'f' or 'A' - 'F' is valid, will throw on invalid input
        /// </summary>
        /// <param name="c">The hex digit</param>
        /// <returns>The value of the digit, 0 - 15</returns>
        /// <exception cref="Exception">The char isn't a hex digit</exception>
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
