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


}



