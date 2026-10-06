using System;
using System.Runtime.CompilerServices;

namespace SysWeaver
{
    /// <summary>
    /// Math related helpers (greatest common divisor and clamping)
    /// </summary>
    public static class MathExt
    {
        /// <summary>
        /// Compute the greatest common divisor of two numbers (the largest integer that evenly divides both a and b)
        /// </summary>
        /// <param name="a">First number</param>
        /// <param name="b">Second number</param>
        /// <returns>The greatest common divisor of a and b (the largest integer that evenly divides both a and b).
        /// Gcd(0, x) = Gcd(x, 0) = x, so Gcd(0, 0) = 0</returns>
        /// <remarks>Uses the Euclidean algorithm (a binary GCD was measured to be slower)</remarks>
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
        /// Compute the greatest common divisor of two numbers (the largest integer that evenly divides both a and b)
        /// </summary>
        /// <param name="a">First number</param>
        /// <param name="b">Second number</param>
        /// <returns>The greatest common divisor of a and b (the largest integer that evenly divides both a and b), always positive or zero, except when the result is 2^63 (only possible if a or b is Int64.MinValue and the other is zero or Int64.MinValue), then Int64.MinValue is returned</returns>
        public static long Gcd(long a, long b)
        {
            static ulong Abs(long v) => v < 0 ? unchecked((ulong)-v) : (ulong)v;
            return unchecked((long)Gcd(Abs(a), Abs(b)));
        }

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value (min if value is less than min, max if value is greater than max, else value)</returns>
        /// <remarks>No validation is made (for performance), if min is greater than max, min is returned if value is less than min, else max is returned (Math.Clamp throws an exception)</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Decimal Clamp(this Decimal value, Decimal min, Decimal max)
            => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Clamp a value to be within the [0, 1] range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <returns>The clamped value (0 if value is less than 0, 1 if value is greater than 1, else value)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Decimal Clamp01(this Decimal value)
            => value < 0 ? 0 : (value > 1 ? 1 : value);

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value (min if value is less than min, max if value is greater than max, else value)</returns>
        /// <remarks>No validation is made (for performance), if min is greater than max, min is returned if value is less than min, else max is returned (Math.Clamp throws an exception). NaN is returned as NaN</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Double Clamp(this Double value, Double min, Double max)
            => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Clamp a value to be within the [0, 1] range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <returns>The clamped value (0 if value is less than 0, 1 if value is greater than 1, else value)</returns>
        /// <remarks>NaN is returned as NaN</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Double Clamp01(this Double value)
            => value < 0 ? 0 : (value > 1 ? 1 : value);

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value (min if value is less than min, max if value is greater than max, else value)</returns>
        /// <remarks>No validation is made (for performance), if min is greater than max, min is returned if value is less than min, else max is returned (Math.Clamp throws an exception). NaN is returned as NaN</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Single Clamp(this Single value, Single min, Single max)
            => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Clamp a value to be within the [0, 1] range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <returns>The clamped value (0 if value is less than 0, 1 if value is greater than 1, else value)</returns>
        /// <remarks>NaN is returned as NaN</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Single Clamp01(this Single value)
            => value < 0 ? 0 : (value > 1 ? 1 : value);


        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value (min if value is less than min, max if value is greater than max, else value)</returns>
        /// <remarks>No validation is made (for performance), if min is greater than max, min is returned if value is less than min, else max is returned (Math.Clamp throws an exception)</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int32 Clamp(this Int32 value, Int32 min, Int32 max)
            => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value (min if value is less than min, max if value is greater than max, else value)</returns>
        /// <remarks>No validation is made (for performance), if min is greater than max, min is returned if value is less than min, else max is returned (Math.Clamp throws an exception)</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int64 Clamp(this Int64 value, Int64 min, Int64 max)
            => value < min ? min : (value > max ? max : value);


        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value (min if value is less than min, max if value is greater than max, else value)</returns>
        /// <remarks>No validation is made (for performance), if min is greater than max, min is returned if value is less than min, else max is returned (Math.Clamp throws an exception)</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt32 Clamp(this UInt32 value, UInt32 min, UInt32 max)
            => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value (min if value is less than min, max if value is greater than max, else value)</returns>
        /// <remarks>No validation is made (for performance), if min is greater than max, min is returned if value is less than min, else max is returned (Math.Clamp throws an exception)</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 Clamp(this UInt64 value, UInt64 min, UInt64 max)
            => value < min ? min : (value > max ? max : value);


    }



}
