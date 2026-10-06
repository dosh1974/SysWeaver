using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// A column merge operation, merges columns from another table (with the same number of rows) into the current table.
    /// Used by <see cref="TableDataOp.MergeColumns"/>, see <see cref="TableDataEdit.MergeColumns(BaseTableData, BaseTableData, String[])"/>.
    /// </summary>
    public class TableDataMergeOp
    {
        /// <summary>
        /// The table data reference to insert columns from
        /// </summary>
        [EditMin(1)]
        public String TableDataRef;

        /// <summary>
        /// The name of the columns to keep (in desired order).
        /// If a column exist in both tables, use a prefix of '-' to take it from the first or '+' to take it from the other table.
        /// Ex: "-Value" to use the Value column of the first table.
        /// "+Value" to use the Value column of the second table (this table data reference)
        /// </summary>
        /// <remarks>
        /// A name without a prefix that exists in both tables is taken from the first table.
        /// NOTE: The '-' / '+' prefix is currently not stripped before the column lookup, so prefixed names fail (see bug report).
        /// </remarks>
        public String[] SelectColumns;
    }



}
