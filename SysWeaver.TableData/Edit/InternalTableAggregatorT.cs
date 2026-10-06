using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Numerics;

namespace SysWeaver.Data
{
    /// <summary>
    /// Builds aggregation functions for types that aren't pre-registered in <see cref="InternalTableAggregator"/> (invoked through reflection).
    /// </summary>
    /// <typeparam name="T">The column value type</typeparam>
    static class InternalTableAggregatorT<T>
    {


        /// <summary>
        /// Create the aggregation functions for <typeparamref name="T"/> using the generic math interfaces it implements.
        /// Sum is created if <typeparamref name="T"/> implements <see cref="IAdditionOperators{TSelf, TOther, TResult}"/>,
        /// average if it also implements <see cref="IDivisionOperators{TSelf, TOther, TResult}"/>.
        /// </summary>
        /// <returns>The aggregation functions, unsupported operations are null</returns>
        public static InternalTableAggregatorType Create()
        {
            var type = typeof(T);
            Func<IEnumerable<Object>, Object> min = null;
            Func<IEnumerable<Object>, Object> max = null;
            Func<IEnumerable<Object>, Object> sum = null;
            Func<IEnumerable<Object>, Object> avg = null;
            var vals = InternalTableAggregator.Inp;
            if (type.IsAssignableTo(typeof(IComparable<T>)))
            {
                min = Expression.Lambda<Func<IEnumerable<Object>, Object>>(Expression.Call(InternalTableAggregator.Min.MakeGenericMethod(type), vals), vals).Compile();
                max = Expression.Lambda<Func<IEnumerable<Object>, Object>>(Expression.Call(InternalTableAggregator.Max.MakeGenericMethod(type), vals), vals).Compile();
            }
            Type[] tr = [type, type, type];
            if (type.IsAssignableTo(typeof(IAdditionOperators<,,>).MakeGenericType(tr)))
            {
                sum = Expression.Lambda<Func<IEnumerable<Object>, Object>>(Expression.Call(InternalTableAggregator.Sum.MakeGenericMethod(type, type), vals), vals).Compile();
                if (type.IsAssignableTo(typeof(IDivisionOperators<,,>).MakeGenericType(tr)))
                    avg = Expression.Lambda<Func<IEnumerable<Object>, Object>>(Expression.Call(InternalTableAggregator.Avg.MakeGenericMethod(type, type), vals), vals).Compile();
            }
            return new InternalTableAggregatorType(min, max, sum, avg);
        }
    }






}
