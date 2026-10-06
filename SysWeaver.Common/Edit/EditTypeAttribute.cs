using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify an editor type (see <see cref="EditTypes"/> for some predefined types)
    /// </summary>
    /// <remarks>
    /// Only one attribute derived from this type can be used on a member, so it can't be combined with <see cref="EditHideIfAttribute"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public class EditTypeAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify an editor type 
        /// </summary>
        /// <param name="type">The editor type</param>
        public EditTypeAttribute(String type)
        {
            Type = type;
        }
        /// <summary>
        /// The editor type
        /// </summary>
        public readonly String Type;
    }


    /// <summary>
    /// Put this on a member to hide it in the editor when a condition is true.
    /// The condition is a javascript expression that is evaluated by the web client (encoded as an editor type starting with "Hide:").
    /// </summary>
    /// <remarks>
    /// Values are inserted as javascript literals without escaping, so string values must not contain double quotes or backslashes (and char values must not be a single quote or a backslash).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class EditHideIfAttribute : EditTypeAttribute
    {
        static readonly String[] Ops = [
            "===",
            "!==",
            ">",
            "<",
            ">=",
            "<=",
            ];

        static String Val(Object value)
        {
            if (value == null)
                return "null";
            var t = value.GetType();
            if (t == typeof(String))
                return String.Join(value.ToString(), '"', '"');
            if (t == typeof(Char))
                return String.Join(value.ToString(), (Char)39, (Char)39);
            return value.ToString();
        }

        /// <summary>
        /// Hide this member if the condition specified is true
        /// </summary>
        /// <param name="memberName">The member to check against</param>
        /// <param name="op">The operation to perform</param>
        /// <param name="value">The value to compare with (strings and chars are quoted, null becomes null, other values use ToString)</param>
        public EditHideIfAttribute(String memberName, EditHideOps op, Object value)
            : base("Hide:this." + memberName + Ops[(int)op] + Val(value))
        {
        }

        /// <summary>
        /// Hide this member if another member is truthy (or falsy)
        /// </summary>
        /// <param name="memberName">The member to check against</param>
        /// <param name="isTrue">True to hide if the member is truthy, false to hide if the member is falsy</param>
        public EditHideIfAttribute(String memberName, bool isTrue)
            : base((isTrue ? "Hide:this." : "Hide:!this.") + memberName)
        {
        }

        /// <summary>
        /// Hide this member if an expression evaluates to true
        /// </summary>
        /// <param name="expression">The javascript expression to evaluate.\nYou can use "this" to access other members, ex:\n"this.SomeValue.toLowerCase()==='xyz'" </param>
        public EditHideIfAttribute(String expression)
            : base("Hide:" + expression)
        {
        }

    }


    /// <summary>
    /// The comparison operations used by <see cref="EditHideIfAttribute"/> (translated to javascript operators)
    /// </summary>
    public enum EditHideOps
    {
        /// <summary>
        /// Strict equality (===)
        /// </summary>
        Equals = 0,
        /// <summary>
        /// Strict inequality (!==)
        /// </summary>
        NotEquals,
        /// <summary>
        /// Greater than (&gt;)
        /// </summary>
        GreaterThan,
        /// <summary>
        /// Less than (&lt;)
        /// </summary>
        LessThan,
        /// <summary>
        /// Greater than or equal (&gt;=)
        /// </summary>
        GreaterOrEqualThan,
        /// <summary>
        /// Less than or equal (&lt;=)
        /// </summary>
        LessOrEqualThan,
    }


}
