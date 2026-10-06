using System;
using System.Reflection;

namespace SysWeaver.Data
{
    /// <summary>
    /// Creates row predicates that compare a row value against a (min, max) pair.
    /// Used by the generated <see cref="TableDataFilterOps.InRange"/> and <see cref="TableDataFilterOps.OutsideRange"/> filters,
    /// the methods are invoked through reflection (<see cref="Check"/> and <see cref="CheckNot"/>) from expression trees.
    /// </summary>
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

        /// <summary>
        /// Generic method definition <c>Func&lt;T, bool&gt; DoIt&lt;T, M&gt;(Tuple&lt;M, M&gt; minMax, Func&lt;T, M&gt; getM, Func&lt;M, M, M, bool&gt; func)</c>,
        /// returning a predicate that evaluates <c>func(min, max, value)</c>.
        /// </summary>
        public static readonly MethodInfo Check = typeof(MinMaxHelper).GetMethod(nameof(DoIt), BindingFlags.Static | BindingFlags.NonPublic);
        /// <summary>
        /// Generic method definition with the same signature as <see cref="Check"/>, returning a predicate that evaluates <c>!func(min, max, value)</c>.
        /// </summary>
        public static readonly MethodInfo CheckNot = typeof(MinMaxHelper).GetMethod(nameof(DoItNot), BindingFlags.Static | BindingFlags.NonPublic);
    }




}
