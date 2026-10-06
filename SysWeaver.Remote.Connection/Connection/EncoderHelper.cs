using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;

namespace SysWeaver.Remote.Connection
{

    /// <summary>
    /// Expression tree building blocks used by <see cref="UriParamsEncoder{T}"/> to build url query strings.
    /// </summary>
    static class EncoderHelper
    {
        /// <summary>
        /// The <see cref="StringBuilder"/> variable used in the generated code.
        /// </summary>
        public static readonly ParameterExpression Sb = Expression.Variable(typeof(StringBuilder), "sb");
        /// <summary>
        /// Assigns a new <see cref="StringBuilder"/> to <see cref="Sb"/>.
        /// </summary>
        public static readonly Expression SbNew = Expression.Assign(Sb, Expression.New(typeof(StringBuilder)));
        /// <summary>
        /// Block variables (only <see cref="Sb"/>).
        /// </summary>
        public static readonly ParameterExpression[] SbBlock = [ Sb ];

        /// <summary>
        /// <see cref="StringBuilder.Append(string)"/>.
        /// </summary>
        public static readonly MethodInfo SbAppendString = typeof(StringBuilder).GetMethod(nameof(StringBuilder.Append), [typeof(String) ]);
        /// <summary>
        /// <see cref="StringBuilder.ToString()"/>.
        /// </summary>
        public static readonly MethodInfo SbToString = typeof(StringBuilder).GetMethod(nameof(StringBuilder.ToString), []);

        /// <summary>
        /// <see cref="object.ToString"/>.
        /// </summary>
        public static readonly MethodInfo ToStringMethod = typeof(Object).GetMethod(nameof(Object.ToString), []);
        /// <summary>
        /// <see cref="Uri.EscapeDataString(string)"/>.
        /// </summary>
        public static readonly MethodInfo EscapeMethod = typeof(Uri).GetMethod(nameof(Uri.EscapeDataString), [typeof(string)]);

        /// <summary>
        /// Wraps a string expression in a <see cref="Uri.EscapeDataString(string)"/> call.
        /// </summary>
        /// <param name="ex">A string expression.</param>
        /// <returns>The escaped string expression.</returns>
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

        /// <summary>
        /// Per type converters from a value expression to a url string expression (invariant culture, round-trippable formats).
        /// Only strings and <see cref="DateTime"/> values are url escaped (a null string becomes an empty string); other values are emitted as formatted.
        /// </summary>
        /// <remarks>
        /// <see cref="DateTime"/> uses the "o" format which may include a '+' (time zone offset), so it's escaped.
        /// Enums, nullable types and <see cref="char"/> are not supported.
        /// </remarks>
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
                { typeof(DateTime), x => Secure(Expression.Call(x, typeof(DateTime).GetMethod(nameof(DateTime.ToString), RinvTypes), O, Inv)) },
                { typeof(Guid), DefaultToString },
            }.Freeze();

        /// <summary>
        /// <see cref="String.Join(string, string[])"/>.
        /// </summary>
        public static readonly MethodInfo StringJoin = typeof(String).GetMethod(nameof(String.Join), [typeof(String), typeof(String[])]);
        /// <summary>
        /// The constant string "=".
        /// </summary>
        public static readonly Expression ConstEqual = Expression.Constant("=");
    }

}
