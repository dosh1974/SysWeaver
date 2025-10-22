using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Numerics;

namespace SysWeaver.Data
{
    static class InternalTableAggregatorT<T>
    {
        

        public static InternalTableAggregatorType Create()
        {
            var type = typeof(T);
            Func<IEnumerable<Object>, Object> min = null;
            Func<IEnumerable<Object>, Object> max = null;
            Func<IEnumerable<Object>, Object> sum = null;
            Func<IEnumerable<Object>, Object> avg = null;
            var vals = InternalTableAggregator.Inp;
            if (type.IsAssignableTo(typeof(IComparer<T>)))
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
