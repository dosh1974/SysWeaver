using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// A column merge operation
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
        public String[] SelectColumns;
    }



}
