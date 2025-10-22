using System;
using System.Collections.Generic;
using System.Reflection;

namespace SysWeaver.Data
{
    sealed class HashSetHelper
    {

        static Func<T, bool> DoIt<T, M>(HashSet<M> set, Func<T, M> getM)
            => x =>
            {
                var v = getM(x);
                return set.Contains(v);
            };
        static Func<T, bool> DoItNot<T, M>(HashSet<M> set, Func<T, M> getM)
            => x =>
            {
                var v = getM(x);
                return !set.Contains(v);
            };

        public static readonly MethodInfo Check = typeof(HashSetHelper).GetMethod(nameof(DoIt), BindingFlags.Static | BindingFlags.NonPublic);
        public static readonly MethodInfo CheckNot = typeof(HashSetHelper).GetMethod(nameof(DoItNot), BindingFlags.Static | BindingFlags.NonPublic);
    }




}
