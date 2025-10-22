using System;
using System.Reflection;

namespace SysWeaver.Data
{
    sealed class MinMaxHelper
    {

        static Func<T, bool> DoIt<T, M>(Tuple<M, M> minMax, Func<T, M> getM, Func<M, M, M, bool> func)
        {
            var min = minMax.Item1;
            var max = minMax.Item2;
            return x => func(min, max, getM(x));
        }

        static Func<T, bool> DoItNot<T, M>(Tuple<M, M> minMax, Func<T, M> getM, Func<M, M, M, bool> func)
        {
            var min = minMax.Item1;
            var max = minMax.Item2;
            return x => !func(min, max, getM(x));
        }

        public static readonly MethodInfo Check = typeof(MinMaxHelper).GetMethod(nameof(DoIt), BindingFlags.Static | BindingFlags.NonPublic);
        public static readonly MethodInfo CheckNot = typeof(MinMaxHelper).GetMethod(nameof(DoItNot), BindingFlags.Static | BindingFlags.NonPublic);
    }




}
