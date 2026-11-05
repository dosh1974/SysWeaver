using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a member to sort this column in descending order by default (sets <see cref="TableDataColumnProps.SortedDesc"/>).
    /// </summary>
    /// <remarks>
    /// Only a hint for the client (<see cref="TableDataColumnProps.SortedDesc"/>): its first sort on this column is descending, so it asks for "-Name".
    /// The server never changes the direction: "Name" always sorts ascending and "-Name" descending (the sort functions used to be swapped,
    /// so the web client's "-Name" returned ascending rows). A server side default order is set with <see cref="TableDataDefaultOrderAttribute"/>.
    /// Ignored if the column can't be sorted.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataSortDescAttribute : Attribute
    {
    }


}
