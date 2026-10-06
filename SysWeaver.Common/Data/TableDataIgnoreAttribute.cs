using System;

namespace SysWeaver.Data
{

    /// <summary>
    /// Put on a member to ignore it when using the type in a data table (no column is created for it).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIgnoreAttribute : Attribute
    {
    }

}
