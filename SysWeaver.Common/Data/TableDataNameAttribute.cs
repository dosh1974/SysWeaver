using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a member to tell any renderer to use this as the name instead of the member name
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataNameAttribute : Attribute
    {
        /// <summary>
        /// Put on a member to tell any renderer to use this as the name instead of the member name
        /// </summary>
        /// <param name="name">The name to use for this column</param>
        public TableDataNameAttribute(String name)
        {
            Name = name;
        }
        /// <summary>
        /// The name (unique id) to use for this column
        /// </summary>
        public readonly String Name;
    }


}
