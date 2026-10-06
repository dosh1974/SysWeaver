using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a member to set its column title, default is the column name cleaned up (removing camel casing)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataTitleAttribute : Attribute
    {
        /// <summary>
        /// Set the column title.
        /// </summary>
        /// <param name="value">The title to display</param>
        public TableDataTitleAttribute(String value)
        {
            Value = value;
        }
        /// <summary>
        /// The title to display
        /// </summary>
        public readonly String Value;
    }

}
