using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a member to sort this column in descending order by default (sets <see cref="TableDataColumnProps.SortedDesc"/>).
    /// </summary>
    /// <remarks>
    /// The ascending and descending sort functions of the column are swapped, so a request ordering by "Name" sorts descending and "-Name" sorts ascending.
    /// Ignored if the column can't be sorted.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataSortDescAttribute : Attribute
    {
    }


}
