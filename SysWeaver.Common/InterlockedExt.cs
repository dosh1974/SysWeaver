using System;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// Some efficient lock free interlocked methods
    /// </summary>
    /// <remarks>
    /// All methods are lock free (compare and swap loops) and can be called concurrently with any other atomic operation on the same memory location.
    /// The memory location is only written if the stored value actually changes, if it doesn't the call is a single volatile read.
    /// </remarks>
    public static class InterlockedEx
    {
        /// <summary>
        /// Updates a memory location with the maximum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The maximum of the two values (always greater than or equal to <paramref name="c"/>)</returns>
        public static long Max(ref long value, long c)
        {
            var r = Volatile.Read(ref value);
            while (c > r)
            {
                var p = Interlocked.CompareExchange(ref value, c, r);
                if (p == r)
                    return c;
                r = p;
            }
            return r;
        }

        /// <summary>
        /// Updates a memory location with the minimum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The minimum of the two values (always less than or equal to <paramref name="c"/>)</returns>
        public static long Min(ref long value, long c)
        {
            var r = Volatile.Read(ref value);
            while (c < r)
            {
                var p = Interlocked.CompareExchange(ref value, c, r);
                if (p == r)
                    return c;
                r = p;
            }
            return r;
        }

        /// <summary>
        /// Updates a memory location with the maximum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The maximum of the two values (always greater than or equal to <paramref name="c"/>)</returns>
        public static int Max(ref int value, int c)
        {
            var r = Volatile.Read(ref value);
            while (c > r)
            {
                var p = Interlocked.CompareExchange(ref value, c, r);
                if (p == r)
                    return c;
                r = p;
            }
            return r;
        }

        /// <summary>
        /// Updates a memory location with the minimum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The minimum of the two values (always less than or equal to <paramref name="c"/>)</returns>
        public static int Min(ref int value, int c)
        {
            var r = Volatile.Read(ref value);
            while (c < r)
            {
                var p = Interlocked.CompareExchange(ref value, c, r);
                if (p == r)
                    return c;
                r = p;
            }
            return r;
        }

        /// <summary>
        /// Updates a memory location with the maximum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The maximum of the two values</returns>
        /// <remarks>
        /// If <paramref name="c"/> is NaN the location is left unchanged (and it's current value is returned).
        /// If the location contains NaN it is replaced by <paramref name="c"/>.
        /// </remarks>
        public static double Max(ref double value, double c)
        {
            var r = Volatile.Read(ref value);
            if (double.IsNaN(c))
                return r;
            // Note: !(c <= r) is also true if r is NaN (a stored NaN is replaced)
            while (!(c <= r))
            {
                var p = Interlocked.CompareExchange(ref value, c, r);
                // CompareExchange compares the bits (so it works with a NaN), do the same
                if (BitConverter.DoubleToInt64Bits(p) == BitConverter.DoubleToInt64Bits(r))
                    return c;
                r = p;
            }
            return r;
        }

        /// <summary>
        /// Updates a memory location with the minimum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The minimum of the two values</returns>
        /// <remarks>
        /// If <paramref name="c"/> is NaN the location is left unchanged (and it's current value is returned).
        /// If the location contains NaN it is replaced by <paramref name="c"/>.
        /// </remarks>
        public static double Min(ref double value, double c)
        {
            var r = Volatile.Read(ref value);
            if (double.IsNaN(c))
                return r;
            // Note: !(c >= r) is also true if r is NaN (a stored NaN is replaced)
            while (!(c >= r))
            {
                var p = Interlocked.CompareExchange(ref value, c, r);
                // CompareExchange compares the bits (so it works with a NaN), do the same
                if (BitConverter.DoubleToInt64Bits(p) == BitConverter.DoubleToInt64Bits(r))
                    return c;
                r = p;
            }
            return r;
        }

        /// <summary>
        /// Updates a memory location with the maximum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The maximum of the two values</returns>
        /// <remarks>
        /// If <paramref name="c"/> is NaN the location is left unchanged (and it's current value is returned).
        /// If the location contains NaN it is replaced by <paramref name="c"/>.
        /// </remarks>
        public static float Max(ref float value, float c)
        {
            var r = Volatile.Read(ref value);
            if (float.IsNaN(c))
                return r;
            // Note: !(c <= r) is also true if r is NaN (a stored NaN is replaced)
            while (!(c <= r))
            {
                var p = Interlocked.CompareExchange(ref value, c, r);
                // CompareExchange compares the bits (so it works with a NaN), do the same
                if (BitConverter.SingleToInt32Bits(p) == BitConverter.SingleToInt32Bits(r))
                    return c;
                r = p;
            }
            return r;
        }

        /// <summary>
        /// Updates a memory location with the minimum of that location and the specified value
        /// </summary>
        /// <param name="value">The location of the value to update</param>
        /// <param name="c">The value to update with</param>
        /// <returns>The minimum of the two values</returns>
        /// <remarks>
        /// If <paramref name="c"/> is NaN the location is left unchanged (and it's current value is returned).
        /// If the location contains NaN it is replaced by <paramref name="c"/>.
        /// </remarks>
        public static float Min(ref float value, float c)
        {
            var r = Volatile.Read(ref value);
            if (float.IsNaN(c))
                return r;
            // Note: !(c >= r) is also true if r is NaN (a stored NaN is replaced)
            while (!(c >= r))
            {
                var p = Interlocked.CompareExchange(ref value, c, r);
                // CompareExchange compares the bits (so it works with a NaN), do the same
                if (BitConverter.SingleToInt32Bits(p) == BitConverter.SingleToInt32Bits(r))
                    return c;
                r = p;
            }
            return r;
        }
    }
}
