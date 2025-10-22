using System;
using System.Collections.Generic;
using System.Numerics;

namespace SysWeaver.Data
{
    static class InternalHelpers
    {
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
