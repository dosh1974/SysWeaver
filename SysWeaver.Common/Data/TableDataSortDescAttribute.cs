using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a member to tell sort this column in descending order by default
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataSortDescAttribute : Attribute
    {
    }


}
