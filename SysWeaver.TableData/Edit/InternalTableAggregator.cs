using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace SysWeaver.Data
{
    static class InternalTableAggregator
    {

        internal static readonly ParameterExpression Inp = Expression.Parameter(typeof(IEnumerable<Object>), "values");
        internal static readonly MethodInfo Min = typeof(InternalHelpers).GetMethod(nameof(InternalHelpers.Min), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        internal static readonly MethodInfo Max = typeof(InternalHelpers).GetMethod(nameof(InternalHelpers.Max), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        internal static readonly MethodInfo Sum = typeof(InternalHelpers).GetMethod(nameof(InternalHelpers.Sum), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        internal static readonly MethodInfo Avg = typeof(InternalHelpers).GetMethod(nameof(InternalHelpers.Avg), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);


        public static InternalTableAggregatorType Get(Type type)
        {
            var c = Cache;
            if (c.TryGetValue(type, out var x))
                return x;
            try
            {
                x = typeof(InternalTableAggregatorT<>).MakeGenericType(type).InvokeMember(nameof(InternalTableAggregatorT<int>.Create), BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic, null, null, null) as InternalTableAggregatorType;
            }
            catch
            {
            }
            c.TryAdd(type, x);
            return x;
        }
        
        static InternalTableAggregator()
        {

            var c = Cache;
            c[typeof(Char)] = new InternalTableAggregatorType(
                InternalHelpers.Min<Char>,
                InternalHelpers.Max<Char>,
                InternalHelpers.Sum<Char, UInt64>,
                InternalHelpers.Avg<Char, UInt64>);

            c[typeof(Byte)] = new InternalTableAggregatorType(
                InternalHelpers.Min<Byte>,
                InternalHelpers.Max<Byte>,
                InternalHelpers.Sum<Byte, UInt64>,
                InternalHelpers.Avg<Byte, UInt64>);
            c[typeof(UInt16)] = new InternalTableAggregatorType(
                InternalHelpers.Min<UInt16>,
                InternalHelpers.Max<UInt16>,
                InternalHelpers.Sum<UInt16, UInt64>,
                InternalHelpers.Avg<UInt16, UInt64>);
            c[typeof(UInt32)] = new InternalTableAggregatorType(
                InternalHelpers.Min<UInt32>,
                InternalHelpers.Max<UInt32>,
                InternalHelpers.Sum<UInt32, UInt64>,
                InternalHelpers.Avg<UInt32, UInt64>);
            c[typeof(UInt64)] = new InternalTableAggregatorType(
                InternalHelpers.Min<UInt64>,
                InternalHelpers.Max<UInt64>,
                InternalHelpers.Sum<UInt64, UInt64>,
                InternalHelpers.Avg<UInt64, UInt64>);

            c[typeof(SByte)] = new InternalTableAggregatorType(
                InternalHelpers.Min<SByte>,
                InternalHelpers.Max<SByte>,
                InternalHelpers.Sum<SByte, Int64>,
                InternalHelpers.Avg<SByte, Int64>);
            c[typeof(Int16)] = new InternalTableAggregatorType(
                InternalHelpers.Min<Int16>,
                InternalHelpers.Max<Int16>,
                InternalHelpers.Sum<Int16, Int64>,
                InternalHelpers.Avg<Int16, Int64>);
            c[typeof(Int32)] = new InternalTableAggregatorType(
                InternalHelpers.Min<Int32>,
                InternalHelpers.Max<Int32>,
                InternalHelpers.Sum<Int32, Int64>,
                InternalHelpers.Avg<Int32, Int64>);
            c[typeof(Int64)] = new InternalTableAggregatorType(
                InternalHelpers.Min<Int64>,
                InternalHelpers.Max<Int64>,
                InternalHelpers.Sum<Int64, Int64>,
                InternalHelpers.Avg<Int64, Int64>);

            c[typeof(Single)] = new InternalTableAggregatorType(
                InternalHelpers.Min<Single>,
                InternalHelpers.Max<Single>,
                InternalHelpers.Sum<Single, Double>,
                InternalHelpers.Avg<Single, Double>);
            c[typeof(Double)] = new InternalTableAggregatorType(
                  InternalHelpers.Min<Double>,
                  InternalHelpers.Max<Double>,
                  InternalHelpers.Sum<Double, Double>,
                  InternalHelpers.Avg<Double, Double>);
            c[typeof(Decimal)] = new InternalTableAggregatorType(
                InternalHelpers.Min<Decimal>,
                InternalHelpers.Max<Decimal>,
                InternalHelpers.Sum<Decimal, Decimal>,
                InternalHelpers.Avg<Decimal, Decimal>);

            c[typeof(DateTime)] = new InternalTableAggregatorType(
                InternalHelpers.Min<DateTime>,
                InternalHelpers.Max<DateTime>,
                null,
                DateTimeAvg);

            c[typeof(TimeSpan)] = new InternalTableAggregatorType(
                InternalHelpers.Min<TimeSpan>,
                InternalHelpers.Max<TimeSpan>,
                TimeSpanSum,
                TimeSpanAvg);

            c[typeof(String)] = new InternalTableAggregatorType(
                      StringMin,
                      StringMax,
                      null,
                      null);


        }

        static readonly Func<String, String, int> StringCmp = StringComparer.Ordinal.Compare;

        static Object StringMin(IEnumerable<Object> values)
        {
            var cmp = StringCmp;
            String c = default;
            bool first = true;
            foreach (var v in values)
            {
                var t = (String)v;
                if (first)
                    c = t;
                else
                    c = cmp(c, t) < 0 ? c : t;
                first = false;
            }
            return c;
        }

        static Object StringMax(IEnumerable<Object> values)
        {
            var cmp = StringCmp;
            String c = default;
            bool first = true;
            foreach (var v in values)
            {
                var t = (String)v;
                if (first)
                    c = t;
                else
                    c = cmp(c, t) > 0 ? c : t;
                first = false;
            }
            return c;
        }


        static Object TimeSpanSum(IEnumerable<Object> values)
        {
            TimeSpan c = default;
            bool first = true;
            foreach (var v in values)
            {
                var t = (TimeSpan)v;
                if (first)
                    c = t;
                else
                    c += t;
                first = false;
            }
            return c;
        }

        static Object TimeSpanAvg(IEnumerable<Object> values)
        {
            TimeSpan c = default;
            long count = 0;
            bool first = true;
            foreach (var v in values)
            {
                var t = (TimeSpan)v;
                if (first)
                    c = t;
                else
                    c += t;
                ++count;
                first = false;
            }
            return TimeSpan.FromTicks(c.Ticks / count);
        }

        static Object DateTimeAvg(IEnumerable<Object> values)
        {
            long c = default;
            long count = 0;
            DateTimeKind ff = default;
            bool first = true;
            foreach (var v in values)
            {
                var dt = ((DateTime)v);
                var t = dt.Ticks;
                if (first)
                {
                    ff = dt.Kind;
                    c = t;
                }
                else
                    c += t;
                ++count;
                first = false;
            }
            var res = c / count;
            return new DateTime(res, ff);
        }


        static readonly ConcurrentDictionary<Type, InternalTableAggregatorType> Cache = new ConcurrentDictionary<Type, InternalTableAggregatorType>();


    }






}
