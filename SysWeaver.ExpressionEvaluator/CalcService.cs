using SysWeaver.Parser;
using System;

namespace SysWeaver.MicroService
{
    public sealed class CalcService
    {
        public override string ToString() => nameof(CalcService);

        /// <summary>
        /// Evaluate the value of an expression using Double precision arithmetics
        /// </summary>
        /// <param name="expression">The expression to evaluate, ex: "5 * 12"</param>
        /// <returns>The value of the expression</returns>
        [WebApi]
        [WebApiRequestCache(10)]
        [WebApiClientCache(30)]
        public Double Calc(String expression) => ExpressionEvaluator.Double.Value(expression);

        /// <summary>
        /// Evaluate the value of an expression using Decimal arithmetics
        /// </summary>
        /// <param name="expression">The expression to evaluate, ex: "5 * 12"</param>
        /// <returns>The value of the expression</returns>
        [WebApi]
        [WebApiRequestCache(10)]
        [WebApiClientCache(30)]
        public Decimal CalcDecimal(String expression) => ExpressionEvaluator.Decimal.Value(expression);

        /// <summary>
        /// Evaluate the value of an expression using Int64 arithmetics
        /// </summary>
        /// <param name="expression">The expression to evaluate, ex: "5 * 12"</param>
        /// <returns>The value of the expression</returns>
        [WebApi]
        [WebApiRequestCache(10)]
        [WebApiClientCache(30)]
        public Int64 CalcInteger(String expression) => ExpressionEvaluator.Int64.Value(expression);

        /// <summary>
        /// Evaluate the value of an expression using UInt64 arithmetics
        /// </summary>
        /// <param name="expression">The expression to evaluate, ex: "5 * 12"</param>
        /// <returns>The value of the expression</returns>
        [WebApi]
        [WebApiRequestCache(10)]
        [WebApiClientCache(30)]
        public UInt64 CalcUnsigned(String expression) => ExpressionEvaluator.UInt64.Value(expression);

    }


}
