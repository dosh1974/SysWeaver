using System;
using System.Linq.Expressions;

namespace SysWeaver.Data
{
    public static class StringConverterExp
    {
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
