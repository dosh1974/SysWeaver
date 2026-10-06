using System;
using System.Collections.Generic;
using System.Numerics;

namespace SysWeaver.Data
{
    /// <summary>
    /// Generic aggregation helpers used by <see cref="InternalTableAggregator"/> (column aggregation, currently not exposed since
    /// <see cref="TableDataEdit.Aggregate(BaseTableData, TableColumnAggregation[])"/> is not implemented).
    /// All methods take boxed values and return a boxed result; an empty sequence yields the default value (or throws for averages).
    /// </summary>
    static class InternalHelpers
    {
        /// <summary>
        /// Intended to return the smallest value.
        /// </summary>
        /// <typeparam name="T">The (exact) type of the boxed values</typeparam>
        /// <param name="values">The boxed values</param>
        /// <returns>The boxed result, default(T) if <paramref name="values"/> is empty</returns>
        /// <remarks>
        /// NOTE: The comparison is inverted, so this currently returns the LARGEST value (see bug report).
        /// </remarks>
        internal static Object Min<T>(IEnumerable<Object> values) where T : IComparable<T>
        {
            T c = default;
            bool first = true;
            foreach (var v in values)
            {
                var t = (T)v;
                if (first || (c.CompareTo(t) < 0))
                    c = t;
                first = false;
            }
            return c;
        }

        /// <summary>
        /// Intended to return the largest value.
        /// </summary>
        /// <typeparam name="T">The (exact) type of the boxed values</typeparam>
        /// <param name="values">The boxed values</param>
        /// <returns>The boxed result, default(T) if <paramref name="values"/> is empty</returns>
        /// <remarks>
        /// NOTE: The comparison is inverted, so this currently returns the SMALLEST value (see bug report).
        /// </remarks>
        internal static Object Max<T>(IEnumerable<Object> values) where T : IComparable<T>
        {
            T c = default;
            bool first = true;
            foreach (var v in values)
            {
                var t = (T)v;
                if (first || (c.CompareTo(t) > 0))
                    c = t;
                first = false;
            }
            return c;
        }

        /// <summary>
        /// Sums all values using the (wider) accumulator type <typeparamref name="W"/>.
        /// </summary>
        /// <typeparam name="T">The column value type</typeparam>
        /// <typeparam name="W">The accumulator type, also the type of the returned value</typeparam>
        /// <param name="values">The boxed values</param>
        /// <returns>The boxed sum (of type <typeparamref name="W"/>), default(W) if <paramref name="values"/> is empty</returns>
        /// <exception cref="InvalidCastException">Values are unboxed directly as <typeparamref name="W"/>, so this throws whenever <typeparamref name="T"/> != <typeparamref name="W"/> (ex: Int32 values with an Int64 accumulator).</exception>
        internal static Object Sum<T, W>(IEnumerable<Object> values) where W : IAdditionOperators<W, W, W>
        {
            W c = default;
            bool first = true;
            foreach (var v in values)
            {
                var t = (W)v;
                if (first)
                    c = t;
                else
                    c += t;
                first = false;
            }
            return c;
        }

        /// <summary>
        /// Computes the average of all values using the (wider) accumulator type <typeparamref name="W"/>, the result is converted back to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The column value type, also the type of the returned value</typeparam>
        /// <typeparam name="W">The accumulator type</typeparam>
        /// <param name="values">The boxed values</param>
        /// <returns>The boxed average (of type <typeparamref name="T"/>)</returns>
        /// <exception cref="InvalidCastException">Values are unboxed directly as <typeparamref name="W"/>, so this throws whenever <typeparamref name="T"/> != <typeparamref name="W"/>.</exception>
        /// <exception cref="DivideByZeroException">For integer accumulators when <paramref name="values"/> is empty.</exception>
        internal static Object Avg<T, W>(IEnumerable<Object> values) where W : IAdditionOperators<W, W, W>, IDivisionOperators<W, W, W>
        {
            W c = default;
            long count = 0;
            bool first = true;
            foreach (var v in values)
            {
                var t = (W)v;
                if (first)
                    c = t;
                else
                    c += t;
                ++count;
                first = false;
            }
            var cc = (W)Convert.ChangeType(count, typeof(W));
            var res = c / cc;
            return Convert.ChangeType(res, typeof(T));
        }

    }






}
