using System;
using System.Linq.Expressions;
using System.Reflection;

namespace SysWeaver.Inspection.Implementation
{
    internal static class EnumTypeHandlerCreator<T>
    {

        private static readonly Type UnderlyingType = Enum.GetUnderlyingType(typeof(T));
        private static readonly MethodInfo mi = typeof(IInspector).GetRuntimeMethod(nameof(IInspector.Field), new Type[] { UnderlyingType.MakeByRefType() });
        private static readonly ParameterExpression enumVar = Expression.Variable(UnderlyingType, "Enum");

        public static TypeHandler<T>.CreateDelegate MakeCreator(Type type, ParameterExpression inspector, ParameterExpression version, ParameterExpression isLatestVersion)
        {
            var l = Expression.Lambda<TypeHandler<T>.CreateDelegate>(
                Expression.Block(new ParameterExpression[] { enumVar },
                    Expression.Assign(enumVar, Expression.Default(UnderlyingType)),
                    Expression.Call(inspector, mi, enumVar),
                    Expression.Convert(enumVar, typeof(T))
                    ), inspector, version, isLatestVersion);
            return l.Compile();
        }

        public static TypeHandler<T>.DescribeDelegate MakeDescriptor(Type type, ParameterExpression inspector, ParameterExpression Enum, ParameterExpression version)
        {
            var l = Expression.Lambda<TypeHandler<T>.DescribeDelegate>(
                Expression.Block(new ParameterExpression[] { enumVar },
                    Expression.Assign(enumVar, Expression.Convert(Enum, UnderlyingType)),
                    Expression.Call(inspector, mi, enumVar),
                    Expression.Assign(Enum, Expression.Convert(enumVar, typeof(T)))
                    ), inspector, Enum, version);
            return l.Compile();
        }

        public static TypeHandler<T>.DescribeDelegate MakeWrappedDescriptor(Type callType, Type type, ParameterExpression inspector, ParameterExpression Enum, ParameterExpression version)
        {
            var l = Expression.Lambda<TypeHandler<T>.DescribeDelegate>(
                Expression.Block(new ParameterExpression[] { enumVar },
                    Expression.Assign(enumVar, Expression.Convert(Expression.Convert(Enum, typeof(T)), UnderlyingType)),
                    Expression.Call(inspector, mi, enumVar),
                    Expression.Assign(Enum, Expression.Convert(Expression.Convert(enumVar, typeof(T)), typeof(Object)))
                    ), inspector, Enum, version);
            return l.Compile();
        }

    }

}