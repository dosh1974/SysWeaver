using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a member to set it's title, default is the member name cleaned up (removing camel casing)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataTitleAttribute : Attribute
    {
        public TableDataTitleAttribute(String value)
        {
            Value = value;
        }
        public readonly String Value;
    }

}
