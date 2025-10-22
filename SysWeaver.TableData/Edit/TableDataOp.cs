using System;
using SysWeaver.AI;

namespace SysWeaver.Data
{
    /// <summary>
    /// An operation to perform on some data, operations are performed in the step order.
    /// All steps are optional.
    /// </summary>
    public class TableDataOp
    {
        /// <summary>
        /// Step 1: Append rows from these tables (must have the same columns).
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public String[] AppendTableDataRef;

        /// <summary>
        /// Step 2: Merge in columns from another table (must have the same number of rows).
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public TableDataMergeOp[] MergeColumns;

        /// <summary>
        /// Step 3: Create new (computed) columns and insert them.
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public NewTableDataColumn[] ComputeColumns;

        /// <summary>
        /// Step 4a: The name of the columns to keep (in the desired order), can be used to re-order columns and/or get rid of unnecessary data.
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public String[] SelectColumns;

        /// <summary>
        /// Step 4b: The name of the columns to remove (get rid of unnecessary data).
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public String[] RemoveColumns;

        /// <summary>
        /// Step 5: Order, filter, offset and limit rows.
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public TableDataOrderRequest[] SortAndFilterRows;
    }


}
