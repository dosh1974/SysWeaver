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
        /// value.ToString(CultureInfo.InvariantCulture), for the integer types (the current culture may use another negative sign, ex: U+2212).
        /// </summary>
        static readonly Func<Expression, Expression> InvariantToString = src => Expression.Call(src, src.Type.GetMethod(nameof(Object.ToString), InvTypes), Inv);

        /// <summary>
        /// Per type converters from a value expression to a url string expression (invariant culture, round-trippable formats).
        /// Strings, <see cref="float"/>, <see cref="double"/> and <see cref="DateTime"/> values are url escaped (a null string becomes an empty string); other values are emitted as formatted.
        /// </summary>
        /// <remarks>
        /// <see cref="DateTime"/> uses the "o" format which may include a '+' (time zone offset), floating point values use "r" which may include a '+' (exponent), so they are escaped.
        /// <see cref="bool"/> is formatted as "True" / "False".
        /// Enums, nullable types and <see cref="char"/> are not supported.
        /// </remarks>
        public static readonly IReadOnlyDictionary<Type, Func<Expression, Expression>> ToStrings = new Dictionary<Type, Func<Expression, Expression>>
            {
                { typeof(Boolean), DefaultToString },
                { typeof(Byte), InvariantToString },
                { typeof(UInt16), InvariantToString },
                { typeof(UInt32), InvariantToString },
                { typeof(UInt64), InvariantToString },
                { typeof(SByte), InvariantToString },
                { typeof(Int16), InvariantToString },
                { typeof(Int32), InvariantToString },
                { typeof(Int64), InvariantToString },
                { typeof(String), x => Expression.Condition(Expression.ReferenceEqual(x, NullString), NullStringValue, Secure(x)) },
                { typeof(Single), x => Secure(Expression.Call(x, typeof(Single).GetMethod(nameof(Single.ToString), RinvTypes), R, Inv)) },
                { typeof(Double), x => Secure(Expression.Call(x, typeof(Double).GetMethod(nameof(Double.ToString), RinvTypes), R, Inv)) },
                { typeof(Decimal), x => Expression.Call(x, typeof(Decimal).GetMethod(nameof(Decimal.ToString), InvTypes), Inv) },
                { typeof(TimeSpan), x => Expression.Call(x, typeof(TimeSpan).GetMethod(nameof(TimeSpan.ToString), RinvTypes), C, Inv) },
                { typeof(DateTime), x => Secure(Expression.Call(x, typeof(DateTime).GetMethod(nameof(DateTime.ToString), RinvTypes), O, Inv)) },
                { typeof(Guid), DefaultToString },
            }.Freeze();

        /// <summary>
        /// Format a value of any type (an element of an object[]) as an escaped url string, using the same formats as <see cref="ToStrings"/>.
        /// </summary>
        /// <param name="value">The value, may be null</param>
        /// <returns>The url escaped string, an empty string if <paramref name="value"/> is null</returns>
        public static String FormatObject(Object value)
        {
            String s;
            switch (value)
            {
                case null:
                    return "";
                case String x:
                    s = x;
                    break;
                case Single x:
                    s = x.ToString("r", CultureInfo.InvariantCulture);
                    break;
                case Double x:
                    s = x.ToString("r", CultureInfo.InvariantCulture);
                    break;
                case DateTime x:
                    s = x.ToString("o", CultureInfo.InvariantCulture);
                    break;
                case TimeSpan x:
                    s = x.ToString("c", CultureInfo.InvariantCulture);
                    break;
                case Boolean x:
                    s = x.ToString();
                    break;
                default:
                    s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                    break;
            }
            return Uri.EscapeDataString(s);
        }

        static readonly MethodInfo FormatObjectMethod = typeof(EncoderHelper).GetMethod(nameof(FormatObject), [typeof(Object)]);

        /// <summary>
        /// Converter from an <see cref="object"/> expression to a url string expression (formatted at runtime by <see cref="FormatObject(object)"/>).
        /// </summary>
        public static readonly Func<Expression, Expression> ObjectToString = x => Expression.Call(FormatObjectMethod, x);

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
