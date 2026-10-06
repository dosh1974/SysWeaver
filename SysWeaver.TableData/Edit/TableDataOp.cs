using System;
using SysWeaver.AI;

namespace SysWeaver.Data
{
    /// <summary>
    /// An operation to perform on some data, operations are performed in the step order.
    /// All steps are optional.
    /// </summary>
    /// <remarks>
    /// Applied by <see cref="TableDataEdit.ApplyOps(BaseTableData, Func{String, BaseTableData}, TableDataOp[])"/>, each step produces a new table
    /// (the input table is never modified). Table data references are resolved using the reference solver supplied to ApplyOps.
    /// </remarks>
    public class TableDataOp
    {
        /// <summary>
        /// Step 1: Append rows from these tables (must have the same columns).
        /// </summary>
        /// <remarks>Only the column count and column types are validated (not the names), the columns of the current table are kept.</remarks>
        [AiOptional]
        [EditAllowNull]
        public String[] AppendTableDataRef;

        /// <summary>
        /// Step 2: Merge in columns from another table (must have the same number of rows).
        /// </summary>
        [AiOptional]
        [EditAllowNull]
        public TableDataMergeOp[] MergeColumns;

        /// <summary>
        /// Step 3: Create new (computed) columns and insert them.
        /// </summary>
        [AiOptional]
        [EditAllowNull]
        public NewTableDataColumn[] ComputeColumns;

        /// <summary>
        /// Step 4a: The name of the columns to keep (in the desired order), can be used to re-order columns and/or get rid of unnecessary data.
        /// </summary>
        [AiOptional]
        [EditAllowNull]
        public String[] SelectColumns;

        /// <summary>
        /// Step 4b: The name of the columns to remove (get rid of unnecessary data).
        /// </summary>
        /// <remarks>Applied after <see cref="SelectColumns"/> if both are specified.</remarks>
        [AiOptional]
        [EditAllowNull]
        public String[] RemoveColumns;

        /// <summary>
        /// Step 5: Order, filter, offset and limit rows.
        /// </summary>
        /// <remarks>Each request is applied in order on the result of the previous one.</remarks>
        [AiOptional]
        [EditAllowNull]
        public TableDataOrderRequest[] SortAndFilterRows;
    }


}
