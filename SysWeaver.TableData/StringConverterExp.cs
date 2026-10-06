using System;
using System.Linq.Expressions;

namespace SysWeaver.Data
{
    /// <summary>
    /// Builds expression trees that convert strings to other types using the lenient <see cref="StringConverter"/> methods.
    /// </summary>
    public static class StringConverterExp
    {
        /// <summary>
        /// Get an expression that converts a string expression to the given type.
        /// </summary>
        /// <param name="type">The destination type.</param>
        /// <param name="stringExpression">An expression of type <see cref="String"/>.</param>
        /// <returns><paramref name="stringExpression"/> itself if the type is <see cref="String"/>, a call to a <see cref="StringConverter"/> method, or null if the type isn't supported.</returns>
        public static Expression FromString(Type type, Expression stringExpression)
        {
            if (type == typeof(String))
                return stringExpression;
            var m = StringConverter.GetMethod(type);
            if (m == null)
                return null;
            return Expression.Call(m, stringExpression);
        }


        static readonly ParameterExpression StrExp = Expression.Parameter(typeof(string), "str");

        /// <summary>
        /// Get a lambda expression (<c>Func&lt;String, T&gt;</c>) that converts a string to the given type.
        /// </summary>
        /// <param name="t">The destination type.</param>
        /// <returns>The lambda expression, or null if the type isn't supported.</returns>
        public static Expression GetFromStringLambdaExp(Type t)
        {
            var p = StrExp;
            var e = FromString(t, p);
            if (e == null)
                return null;
            return Expression.Lambda(typeof(Func<,>).MakeGenericType(typeof(String), t), e, p);
        }


    }

}
