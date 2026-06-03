using System;
using System.Runtime.CompilerServices;

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

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Decimal Clamp(this Decimal value, Decimal min, Decimal max)
            => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <returns>The clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Decimal Clamp01(this Decimal value)
            => value < 0 ? 0 : (value > 1 ? 1 : value);

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Double Clamp(this Double value, Double min, Double max)
            => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <returns>The clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Double Clamp01(this Double value)
            => value < 0 ? 0 : (value > 1 ? 1 : value);

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Single Clamp(this Single value, Single min, Single max)
            => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <returns>The clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Single Clamp01(this Single value)
            => value < 0 ? 0 : (value > 1 ? 1 : value);


        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int32 Clamp(this Int32 value, Int32 min, Int32 max)
            => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int64 Clamp(this Int64 value, Int64 min, Int64 max)
            => value < min ? min : (value > max ? max : value);


        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt32 Clamp(this UInt32 value, UInt32 min, UInt32 max)
            => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Clamp a value to be within a range
        /// </summary>
        /// <param name="value">The value to clamp</param>
        /// <param name="min">The minimum allowed value</param>
        /// <param name="max">The maximum allowed value</param>
        /// <returns>The clamped value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 Clamp(this UInt64 value, UInt64 min, UInt64 max)
            => value < min ? min : (value > max ? max : value);


    }



}
