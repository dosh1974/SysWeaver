using System;
using System.Globalization;

namespace SysWeaver
{
    public static class CharExt
    {

        /// Make an culture invariant upper case version of a char
        public static readonly Func<Char, Char> FastLower = CultureInfo.InvariantCulture.TextInfo.ToLower;

        /// Make an culture invariant upper case version of a char
        public static readonly Func<Char, Char> FastUpper = CultureInfo.InvariantCulture.TextInfo.ToUpper;

        /// <summary>
        /// Make an culture invariant lower case version of a char
        /// </summary>
        /// <param name="c">The char to transform into a culture invariant lower case</param>
        /// <returns>Culture invariant lower case char</returns>
        public static Char FastToLower(this Char c) => FastLower(c);

        /// <summary>
        /// Make an culture invariant upper case version of a char
        /// </summary>
        /// <param name="c">The char to transform into a culture invariant upper case</param>
        /// <returns>Culture invariant upper case char</returns>
        public static Char FastToUpper(this Char c) => FastUpper(c);


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
