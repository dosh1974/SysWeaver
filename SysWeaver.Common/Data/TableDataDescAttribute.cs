using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a member to set it's description, default is the code comments
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataDescAttribute : Attribute
    {
        public TableDataDescAttribute(String value)
        {
            Value = value;
        }
        public readonly String Value;
    }


    /// <summary>
    /// Put on a member to indicate that values may be word wrapped when rendering (this is just a hint to the renderer)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataWordWrapAttribute : Attribute
    {
        public TableDataWordWrapAttribute(bool wordWrap = true)
        {
            WordWrap = wordWrap;
        }
        public readonly bool WordWrap;
    }


}



