using System;

namespace SysWeaver.Data
{

    /// <summary>
    /// Put on a member to disable sorting it when using the type in a data table
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataNoSortAttribute : Attribute
    {
    }


}
