using System;
using System.Linq.Expressions;

namespace SysWeaver
{
    /// <summary>
    /// Helpers that let the compiler infer the type of a lambda expression tree, so that a lambda can be captured as an <see cref="Expression{TDelegate}"/>
    /// using <c>var</c> (ex: <c>var e = LinkTools.Func(() =&gt; Something.Method());</c>).
    /// </summary>
    public static class LinkTools
    {
        /// <summary>
        /// Return the supplied expression tree as is (used for type inference only).
        /// </summary>
        /// <typeparam name="R">The return type of the lambda.</typeparam>
        /// <param name="value">A parameterless lambda, compiled to an expression tree.</param>
        /// <returns><paramref name="value"/>, unchanged.</returns>
        public static Expression<Func<R>> Func<R>(Expression<Func<R>> value) => value;
        
        /// <summary>
        /// Return the supplied expression tree as is (used for type inference only).
        /// </summary>
        /// <typeparam name="A0">The type of the first lambda parameter.</typeparam>
        /// <typeparam name="R">The return type of the lambda.</typeparam>
        /// <param name="value">A lambda with one parameter, compiled to an expression tree.</param>
        /// <returns><paramref name="value"/>, unchanged.</returns>
        public static Expression<Func<A0, R>> Func<A0, R>(Expression<Func<A0, R>> value) => value;

        /// <summary>
        /// Return the supplied expression tree as is (used for type inference only).
        /// </summary>
        /// <typeparam name="A0">The type of the first lambda parameter.</typeparam>
        /// <typeparam name="A1">The type of the second lambda parameter.</typeparam>
        /// <typeparam name="R">The return type of the lambda.</typeparam>
        /// <param name="value">A lambda with two parameters, compiled to an expression tree.</param>
        /// <returns><paramref name="value"/>, unchanged.</returns>
        public static Expression<Func<A0, A1, R>> Func<A0, A1, R>(Expression<Func<A0, A1, R>> value) => value;

    }

}
