using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// Some efficient lock free interlocked methods
    /// </summary>
    public static class InterlockedEx
    {
        /// <summary>
        /// Updates a memory location with the maximum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The maximum of the two values</returns>
        public static long Max(ref long value, long c)
        {
            for (;;)
            {
                var r = Interlocked.Read(ref value);
                if (c <= r)
                    return r;
                Interlocked.CompareExchange(ref value, c, r);
            }
        }

        /// <summary>
        /// Updates a memory location with the minimum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The minimum of the two values</returns>
        public static long Min(ref long value, long c)
        {
            for (;;)
            {
                var r = Interlocked.Read(ref value);
                if (c >= r)
                    return r;
                Interlocked.CompareExchange(ref value, c, r);
            }
        }

        /// <summary>
        /// Updates a memory location with the maximum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The maximum of the two values</returns>
        public static int Max(ref int value, int c)
        {
            for (;;)
            {
                var r = Interlocked.CompareExchange(ref value, 0, 0);
                if (c <= r)
                    return r;
                Interlocked.CompareExchange(ref value, c, r);
            }
        }

        /// <summary>
        /// Updates a memory location with the minimum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The minimum of the two values</returns>
        public static int Min(ref int value, int c)
        {
            for (;;)
            {
                var r = Interlocked.CompareExchange(ref value, 0, 0);
                if (c >= r)
                    return r;
                Interlocked.CompareExchange(ref value, c, r);
            }
        }

        /// <summary>
        /// Updates a memory location with the maximum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The maximum of the two values</returns>
        public static double Max(ref double value, double c)
        {
            for (;;)
            {
                var r = Interlocked.CompareExchange(ref value, 0, 0);
                if (c <= r)
                    return r;
                Interlocked.CompareExchange(ref value, c, r);
            }
        }

        /// <summary>
        /// Updates a memory location with the minimum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The minimum of the two values</returns>
        public static double Min(ref double value, double c)
        {
            for (;;)
            {
                var r = Interlocked.CompareExchange(ref value, 0, 0);
                if (c >= r)
                    return r;
                Interlocked.CompareExchange(ref value, c, r);
            }
        }

        /// <summary>
        /// Updates a memory location with the maximum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The maximum of the two values</returns>
        public static float Max(ref float value, float c)
        {
            for (;;)
            {
                var r = Interlocked.CompareExchange(ref value, 0, 0);
                if (c <= r)
                    return r;
                Interlocked.CompareExchange(ref value, c, r);
            }
        }

        /// <summary>
        /// Updates a memory location with the minimum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The minimum of the two values</returns>
        public static float Min(ref float value, float c)
        {
            for (;;)
            {
                var r = Interlocked.CompareExchange(ref value, 0, 0);
                if (c >= r)
                    return r;
                Interlocked.CompareExchange(ref value, c, r);
            }
        }
    }
}
