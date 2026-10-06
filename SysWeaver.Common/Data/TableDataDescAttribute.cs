using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a member to set its column description (tool tip), default is the XML documentation of the member
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataDescAttribute : Attribute
    {
        /// <summary>
        /// Set the column description.
        /// </summary>
        /// <param name="value">The description</param>
        public TableDataDescAttribute(String value)
        {
            Value = value;
        }
        /// <summary>
        /// The description
        /// </summary>
        public readonly String Value;
    }


    /// <summary>
    /// Put on a member to indicate that values may be word wrapped when rendering (this is just a hint to the renderer)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataWordWrapAttribute : Attribute
    {
        /// <summary>
        /// Allow (or disallow) word wrapping, sets <see cref="TableDataColumnProps.WordWrap"/>.
        /// </summary>
        /// <param name="wordWrap">True to allow word wrapping</param>
        public TableDataWordWrapAttribute(bool wordWrap = true)
        {
            WordWrap = wordWrap;
        }
        /// <summary>
        /// True if word wrapping is allowed
        /// </summary>
        public readonly bool WordWrap;
    }


}



