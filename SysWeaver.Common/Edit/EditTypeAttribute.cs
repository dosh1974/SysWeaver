using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify an editor type 
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public class EditTypeAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify an editor type 
        /// </summary>
        /// <param name="type">The item type</param>
        public EditTypeAttribute(String type)
        {
            Type = type;
        }
        public readonly String Type;
    }


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
        /// Hide this member if the condition specificed is true
        /// </summary>
        /// <param name="memberName">The member to check against</param>
        /// <param name="op">The operation to perform</param>
        /// <param name="value">The value to compare with</param>
        public EditHideIfAttribute(String memberName, EditHideOps op, Object value)
            : base("Hide:this." + memberName + Ops[(int)op] + Val(value))
        {
        }

        /// <summary>
        /// Hide this member if another member is trueful of false 
        /// </summary>
        /// <param name="memberName">The member to check against</param>
        /// <param name="isTrue">If true, and the member is trueful then hide</param>
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


    public enum EditHideOps
    {
        Equals = 0,
        NotEquals,
        GreaterThan,
        LessThan,
        GreaterOrEqualThan,
        LessOrEqualThan,
    }


}
