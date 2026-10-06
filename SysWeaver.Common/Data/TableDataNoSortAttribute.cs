using System;

namespace SysWeaver.Data
{

    /// <summary>
    /// Put on a member to disable sorting it when using the type in a data table
    /// </summary>
    /// <remarks>
    /// Note: The current table data implementation still reports the column as sortable (<see cref="TableDataColumnProps.CanSort"/>),
    /// but sorts it using the string representation of the values instead of the values themselves.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataNoSortAttribute : Attribute
    {
    }


}
