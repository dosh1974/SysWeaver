using System;
using System.Collections.Generic;
using System.Reflection;

namespace SysWeaver.Data
{
    /// <summary>
    /// Creates row predicates that test if a row value is (or isn't) a member of a set.
    /// Used by the generated <see cref="TableDataFilterOps.AnyOf"/> and <see cref="TableDataFilterOps.NoneOf"/> filters,
    /// the methods are invoked through reflection (<see cref="Check"/> and <see cref="CheckNot"/>) from expression trees.
    /// </summary>
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

        /// <summary>
        /// Generic method definition <c>Func&lt;T, bool&gt; DoIt&lt;T, M&gt;(HashSet&lt;M&gt; set, Func&lt;T, M&gt; getM)</c>,
        /// returning a predicate that is true if the row value is in the set.
        /// </summary>
        public static readonly MethodInfo Check = typeof(HashSetHelper).GetMethod(nameof(DoIt), BindingFlags.Static | BindingFlags.NonPublic);
        /// <summary>
        /// Generic method definition with the same signature as <see cref="Check"/>, returning a predicate that is true if the row value is NOT in the set.
        /// </summary>
        public static readonly MethodInfo CheckNot = typeof(HashSetHelper).GetMethod(nameof(DoItNot), BindingFlags.Static | BindingFlags.NonPublic);
    }




}
