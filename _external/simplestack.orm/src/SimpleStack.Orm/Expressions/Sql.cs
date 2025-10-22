using System;

namespace SimpleStack.Orm.Expressions
{
    /// <summary>A sql.</summary>
    public static class Sql
    {
        /// <summary>Descriptions the given value.</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>A string.</returns>
        public static string Desc<T>(T value)
        {
            return value == null ? string.Empty : value + " DESC";
        }

        /// <summary>As.</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">  The value.</param>
        /// <param name="asValue">as value.</param>
        /// <returns>A string.</returns>
        public static T As<T>(T value, object asValue)
        {
            return value;
        }

        /// <summary>Sums the given value.</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>A T.</returns>
        public static T Sum<T>(T value)
        {
            return value;
        }

        /// <summary>Sums the given value.</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>A T.</returns>
        public static T Sum<T>(T? value) where T : struct
        {
            return default(T);
        }

        /// <summary>Counts the given value.</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>A T.</returns>
        public static long Count<T>(T value)
        {
            return 1;
        }

        /// <summary>Determines the minimum of the given parameters.</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>The minimum value.</returns>
        public static T Min<T>(T value)
        {
            return value;
        }

        /// <summary>Determines the minimum of the given parameters.</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>The minimum value.</returns>
        public static T Min<T>(T? value) where T : struct
        {
            return default(T);
        }

        /// <summary>Determines the maximum of the given parameters.</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>The maximum value.</returns>
        public static T Max<T>(T value)
        {
            return value;
        }

        /// <summary>Determines the maximum of the given parameters.</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>The maximum value.</returns>
        public static T Max<T>(T? value) where T : struct
        {
            return default(T);
        }

        /// <summary>Determines the average of the given parameters.</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>The average value.</returns>
        public static T Avg<T>(T value)
        {
            return value;
        }

        /// <summary>Determines the average of the given parameters.</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>The average value.</returns>
        public static T Avg<T>(T? value) where T : struct
        {
            return default(T);
        }

        /// <summary>Determines the average of the given parameters.</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>The average value.</returns>
        public static T If<T>(Boolean value, T @then, T @else)
        {
            return default(T);
        }

        /// <summary>Return true if the value is null, else false</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>True if the value was null</returns>
        public static bool IsNull<T>(T value) where T : class
        {
            return true;
        }

        /// <summary>Return true if the value is null, else false</summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>True if the value was null</returns>
        public static bool IsNull<T>(T? value) where T : struct
        {
            return true;
        }

        [Obsolete("Use DateTime property instead")]
        public static int Year(DateTime value)
        {
            return value.Year;
        }

        [Obsolete("Use DateTime property instead")]
        public static int Month(DateTime value)
        {
            return value.Month;
        }

        [Obsolete("Use DateTime property instead")]
        public static int Day(DateTime value)
        {
            return value.Day;
        }

        [Obsolete("Use DateTime property instead")]
        public static int Hour(DateTime value)
        {
            return value.Hour;
        }

        [Obsolete("Use DateTime property instead")]
        public static int Minute(DateTime value)
        {
            return value.Minute;
        }

        [Obsolete("Use DateTime property instead")]
        public static int Second(DateTime value)
        {
            return value.Second;
        }

        public static int Quarter(this DateTime value)
        {
            return (value.Month - 1) / 3 + 1;
        }
    }
}