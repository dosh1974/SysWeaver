using System;
using System.Collections.Generic;

namespace SysWeaver.Data
{
    sealed class InternalTableAggregatorType
    {
        public readonly Func<IEnumerable<Object>, Object> Min;
        public readonly Func<IEnumerable<Object>, Object> Max;
        public readonly Func<IEnumerable<Object>, Object> Sum;
        public readonly Func<IEnumerable<Object>, Object> Avg;

        public InternalTableAggregatorType(Func<IEnumerable<object>, object> min, Func<IEnumerable<object>, object> max, Func<IEnumerable<object>, object> sum, Func<IEnumerable<object>, object> avg)
        {
            Min = min;
            Max = max;
            Sum = sum;
            Avg = avg;
        }
    }






}
