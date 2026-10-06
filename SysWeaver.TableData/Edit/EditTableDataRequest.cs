using System;
using SysWeaver.AI;

namespace SysWeaver.Data
{
    /// <summary>
    /// Request for editing the table data behind a table data reference (used by the "EditTableData" web API / AI tool).
    /// The operations are applied using <see cref="TableDataEdit.ApplyOps(BaseTableData, Func{String, BaseTableData}, TableDataOp[])"/>,
    /// the result is stored as a new table data reference and the source data is never modified.
    /// </summary>
    public sealed class EditTableDataRequest
    {
        /// <summary>
        /// The reference to the table data to edit
        /// </summary>
        [EditMin(1)]
        public String TableDataRef;

        /// <summary>
        /// The operations to perform (in the order they appear)
        /// </summary>
        public TableDataOp[] Ops;

        /// <summary>
        /// True to get column meta data, only do this on your first "request".
        /// Columns will not mutate unless you do it (and then you still know the meta data).
        /// </summary>
        [AiOptional]
        public bool RequireColumns;

    }



    /// <summary>
    /// Request for getting the content of a table data reference (used by the "GetTableData" web API / AI tool).
    /// </summary>
    public sealed class GetTableDataRequest
    {
        /// <summary>
        /// The reference to the table data to get.
        /// Make sure that you use the correct reference, typically the last from an edit or query operation.
        /// </summary>
        [EditMin(1)]
        public String TableDataRef;

        /// <summary>
        /// True to get column meta data, only do this on your first "request".
        /// Columns will not mutate unless you do it (and then you still know the meta data).
        /// It's very rare that this is required.
        /// </summary>
        [AiOptional]
        public bool RequireColumns;

    }
    




}
