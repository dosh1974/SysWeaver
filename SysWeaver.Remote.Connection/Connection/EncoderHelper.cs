using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;

namespace SysWeaver.Remote.Connection
{

    static class EncoderHelper
    {
        public static readonly ParameterExpression Sb = Expression.Variable(typeof(StringBuilder), "sb");
        public static readonly Expression SbNew = Expression.Assign(Sb, Expression.New(typeof(StringBuilder)));
        public static readonly ParameterExpression[] SbBlock = [ Sb ];

        public static readonly MethodInfo SbAppendString = typeof(StringBuilder).GetMethod(nameof(StringBuilder.Append), [typeof(String) ]);
        public static readonly MethodInfo SbToString = typeof(StringBuilder).GetMethod(nameof(StringBuilder.ToString), []);

        public static readonly MethodInfo ToStringMethod = typeof(Object).GetMethod(nameof(Object.ToString), []);
        public static readonly MethodInfo EscapeMethod = typeof(Uri).GetMethod(nameof(Uri.EscapeDataString));

        public static Expression Secure(Expression ex) => Expression.Call(EscapeMethod, ex);

        static readonly Func<Expression, Expression> DefaultToString = src => Expression.Call(src, ToStringMethod);

        static readonly Expression NullString = Expression.Constant(null, typeof(String));
        static readonly Expression NullStringValue = Expression.Constant("");
        static readonly Type[] RinvTypes = [typeof(String), typeof(IFormatProvider)];
        static readonly Type[] InvTypes = [typeof(IFormatProvider)];
        static readonly Expression R = Expression.Constant("r");
        static readonly Expression C = Expression.Constant("c");
        static readonly Expression O = Expression.Constant("o");
        static readonly Expression Inv = Expression.Constant(CultureInfo.InvariantCulture);

        public static readonly IReadOnlyDictionary<Type, Func<Expression, Expression>> ToStrings = new Dictionary<Type, Func<Expression, Expression>>
            {
                { typeof(Boolean), DefaultToString },
                { typeof(Byte), DefaultToString },
                { typeof(UInt16), DefaultToString },
                { typeof(UInt32), DefaultToString },
                { typeof(UInt64), DefaultToString },
                { typeof(SByte), DefaultToString },
                { typeof(Int16), DefaultToString },
                { typeof(Int32), DefaultToString },
                { typeof(Int64), DefaultToString },
                { typeof(String), x => Expression.Condition(Expression.ReferenceEqual(x, NullString), NullStringValue, Secure(x)) },
                { typeof(Single), x => Expression.Call(x, typeof(Single).GetMethod(nameof(Single.ToString), RinvTypes), R, Inv) },
                { typeof(Double), x => Expression.Call(x, typeof(Double).GetMethod(nameof(Double.ToString), RinvTypes), R, Inv) },
                { typeof(Decimal), x => Expression.Call(x, typeof(Decimal).GetMethod(nameof(Decimal.ToString), InvTypes), Inv) },
                { typeof(TimeSpan), x => Expression.Call(x, typeof(TimeSpan).GetMethod(nameof(TimeSpan.ToString), RinvTypes), C, Inv) },
                { typeof(DateTime), x => Expression.Call(x, typeof(DateTime).GetMethod(nameof(DateTime.ToString), RinvTypes), O, Inv) },
                { typeof(Guid), DefaultToString },
            }.Freeze();

        public static readonly MethodInfo StringJoin = typeof(String).GetMethod(nameof(String.Join), [typeof(String), typeof(String[])]);
        public static readonly Expression ConstEqual = Expression.Constant("=");
    }

}
