namespace SysWeaver
{
    public static class MathExt
    {
        /// <summary>
        /// Compute the greatest common divisor of two number (the larges integer that evenly diveds both a and b)
        /// </summary>
        /// <param name="a">One numbers</param>
        /// <param name="b">Second number</param>
        /// <returns>The greatest common divisor of a and b (the larges integer that evenly diveds both a and b)</returns>
        public static ulong Gcd(ulong a, ulong b)
        {
            while (a != 0 && b != 0)
            {
                if (a > b)
                    a %= b;
                else
                    b %= a;
            }
            return a | b;
        }

        /// <summary>
        /// Compute the greatest common divisor of two number (the larges integer that evenly diveds both a and b)
        /// </summary>
        /// <param name="a">One numbers</param>
        /// <param name="b">Second number</param>
        /// <returns>The greatest common divisor of a and b (the larges integer that evenly diveds both a and b)</returns>
        public static long Gcd(long a, long b)
        {
            while (a != 0 && b != 0)
            {
                if (a > b)
                    a %= b;
                else
                    b %= a;
            }
            return a | b;
        }

    }



}
