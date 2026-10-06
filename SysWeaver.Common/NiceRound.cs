using System;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// Round values to "nice" (human friendly) numbers, such as 1, 2, 5, 10, 25, 100, 250 etc
    /// </summary>
    public static class NiceRound
    {
        /// <summary>
        /// Nice numbers in the [1, 1000] interval (sorted in ascending order).
        /// </summary>
        public static IReadOnlyList<int> Nice1000 = new int[]
        {
            1,
            2,
            3,
            4,
            5,
            10,
            15,
            20,
            25,
            30,
            40,
            50,
            60,
            70,
            75,
            80,
            90,
            100,
            125,
            150,
            175,
            200,
            225,
            250,
            300,
            350,
            375,
            400,
            450,
            500,
            550,
            600,
            700,
            750,
            800,
            900,
            1000,
        };

        /// <summary>
        /// The largest absolute value that can be rounded (the result is a long)
        /// </summary>
        const double MaxDouble = 9223372036854775807.0;

        /// <summary>
        /// Round a value to the closest nice number (times a power of ten).
        /// The value is scaled down by powers of ten until it's less than or equal to <paramref name="niceMax"/>, the closest nice number times the same power of ten is returned.
        /// </summary>
        /// <param name="value">The value to round</param>
        /// <param name="nice">The nice numbers to use, null to use <see cref="Nice1000"/>. If two nice numbers are equally close, the first one in the list is used</param>
        /// <param name="niceMax">The largest nice number (the value is scaled down until it's less than or equal to this value)</param>
        /// <returns>The nice value (with the same sign as the input value), zero if the absolute value is less than 0.5</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="niceMax"/> is less than 1, or the value is NaN or the absolute value is too large to fit in a long</exception>
        public static long Round(double value, IReadOnlyList<int> nice = null, int niceMax = 1000)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(niceMax, 1);
            nice = nice ?? Nice1000;
            var neg = value < 0;
            if (neg)
                value = -value;
            if (!(value < MaxDouble))
                throw new ArgumentOutOfRangeException(nameof(value), neg ? -value : value, "Value is NaN or too large!");
            if (value < 0.5)
                return 0;
            long scale = 1;
            double nmd = niceMax;
            while (((value / scale) > nmd) && (scale <= (long.MaxValue / 10)))
                scale *= 10;
            double minErr = double.MaxValue;
            long minRes = 0;
            var maxNice = long.MaxValue / scale;
            var c = nice.Count;
            for (int i = 0; i < c; ++i)
            {
                long v = nice[i];
                if (v > maxNice)
                    continue;
                v *= scale;
                var d = value - v;
                if (d < 0)
                    d = -d;
                if (d >= minErr)
                    continue;
                minErr = d;
                minRes = v;
            }
            return neg ? -minRes : minRes;
        }

        /// <summary>
        /// Round a value to the closest nice number (times a power of ten).
        /// The value is scaled down by powers of ten until it's less than or equal to <paramref name="niceMax"/>, the closest nice number times the same power of ten is returned.
        /// </summary>
        /// <param name="value">The value to round</param>
        /// <param name="nice">The nice numbers to use, null to use <see cref="Nice1000"/>. If two nice numbers are equally close, the first one in the list is used</param>
        /// <param name="niceMax">The largest nice number (the value is scaled down until it's less than or equal to this value)</param>
        /// <returns>The nice value (with the same sign as the input value), zero if the absolute value is less than 0.5</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="niceMax"/> is less than 1, or the absolute value is too large to fit in a long</exception>
        public static long Round(decimal value, IReadOnlyList<int> nice = null, int niceMax = 1000)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(niceMax, 1);
            nice = nice ?? Nice1000;
            var neg = value < 0;
            if (neg)
                value = -value;
            if (value > long.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), neg ? -value : value, "Value is too large!");
            if (value < 0.5M)
                return 0;
            long scale = 1;
            decimal nmd = niceMax;
            while (((value / scale) > nmd) && (scale <= (long.MaxValue / 10)))
                scale *= 10;
            decimal minErr = decimal.MaxValue;
            long minRes = 0;
            var maxNice = long.MaxValue / scale;
            var c = nice.Count;
            for (int i = 0; i < c; ++i)
            {
                long v = nice[i];
                if (v > maxNice)
                    continue;
                v *= scale;
                var d = value - v;
                if (d < 0)
                    d = -d;
                if (d >= minErr)
                    continue;
                minErr = d;
                minRes = v;
            }
            return neg ? -minRes : minRes;
        }

        /// <summary>
        /// Round a value to the closest nice number (times a power of ten).
        /// The value is scaled down by powers of ten until it's less than or equal to <paramref name="niceMax"/>, the closest nice number times the same power of ten is returned.
        /// </summary>
        /// <param name="value">The value to round</param>
        /// <param name="nice">The nice numbers to use, null to use <see cref="Nice1000"/>. If two nice numbers are equally close, the first one in the list is used</param>
        /// <param name="niceMax">The largest nice number (the value is scaled down until it's less than or equal to this value)</param>
        /// <returns>The nice value (with the same sign as the input value), zero if the value is zero</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="niceMax"/> is less than 1</exception>
        public static long Round(long value, IReadOnlyList<int> nice = null, int niceMax = 1000)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(niceMax, 1);
            nice = nice ?? Nice1000;
            var neg = value < 0;
            // Works for long.MinValue too
            ulong a = neg ? unchecked((ulong)-value) : (ulong)value;
            if (a < 1)
                return 0;
            ulong scale = 1;
            ulong nmd = (ulong)niceMax;
            while (((a / scale) > nmd) && (scale <= (ulong)(long.MaxValue / 10)))
                scale *= 10;
            ulong minErr = ulong.MaxValue;
            long minRes = 0;
            var maxNice = (ulong)long.MaxValue / scale;
            var c = nice.Count;
            for (int i = 0; i < c; ++i)
            {
                long n = nice[i];
                if ((n < 0) || ((ulong)n > maxNice))
                    continue;
                var v = (ulong)n * scale;
                var d = a > v ? a - v : v - a;
                if (d >= minErr)
                    continue;
                minErr = d;
                minRes = (long)v;
            }
            return neg ? -minRes : minRes;
        }



        /// <summary>
        /// Compute how "ugly" an integer is, the number of significant digits (trailing zeros are ignored), minus 0.5 if the last significant digit is a 5.
        /// Ex: 1 = 1, 5 = 0.5, 10 = 1, 15 = 1.5, 25 = 1.5, 125 = 2.5, 123 = 3.
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>The ugliness, a lower value is "nicer", zero for zero and negative values</returns>
        public static double Uggliness(long value)
        {
            if (value <= 0)
                return 0;

            int count = 0;
            int firstNonZero = -1;
            bool lastIsFive = false;
            while (value > 0)
            {
                if (firstNonZero < 0)
                {
                    var dec = value % 10;
                    if (dec != 0)
                    {
                        lastIsFive = dec == 5;
                        firstNonZero = count;
                    }
                }
                ++count;
                value /= 10;
            }
            return count - firstNonZero - (lastIsFive ? 0.5 : 0);
        }



    }



}
