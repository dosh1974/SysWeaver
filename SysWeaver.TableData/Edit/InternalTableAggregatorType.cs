using System;
using System.Collections.Generic;

namespace SysWeaver.Data
{
    /// <summary>
    /// The set of aggregation functions available for a single column value type.
    /// Each function takes the boxed column values and returns the boxed aggregated value, a null function means that the aggregation isn't supported.
    /// </summary>
    sealed class InternalTableAggregatorType
    {
        /// <summary>
        /// Computes the minimum value, null if not supported.
        /// </summary>
        public readonly Func<IEnumerable<Object>, Object> Min;
        /// <summary>
        /// Computes the maximum value, null if not supported.
        /// </summary>
        public readonly Func<IEnumerable<Object>, Object> Max;
        /// <summary>
        /// Computes the sum of all values, null if not supported.
        /// </summary>
        public readonly Func<IEnumerable<Object>, Object> Sum;
        /// <summary>
        /// Computes the average of all values, null if not supported.
        /// </summary>
        public readonly Func<IEnumerable<Object>, Object> Avg;

        /// <summary>
        /// Create a set of aggregation functions.
        /// </summary>
        /// <param name="min">The minimum function, or null if not supported</param>
        /// <param name="max">The maximum function, or null if not supported</param>
        /// <param name="sum">The sum function, or null if not supported</param>
        /// <param name="avg">The average function, or null if not supported</param>
        public InternalTableAggregatorType(Func<IEnumerable<object>, object> min, Func<IEnumerable<object>, object> max, Func<IEnumerable<object>, object> sum, Func<IEnumerable<object>, object> avg)
        {
            Min = min;
            Max = max;
            Sum = sum;
            Avg = avg;
        }
    }






}
