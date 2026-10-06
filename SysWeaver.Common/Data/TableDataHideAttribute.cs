using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a member to tell any renderer to hide the column by default (sets <see cref="TableDataColumnProps.Hide"/>).
    /// </summary>
    /// <remarks>
    /// The column is still part of the table data (and can be shown by the user), use <see cref="TableDataIgnoreAttribute"/> to exclude it.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataHideAttribute : Attribute
    {
    }


}
