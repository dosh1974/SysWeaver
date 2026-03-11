using System;
using SysWeaver.AI;

namespace SysWeaver.Data
{
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
        [OpenAiOptional]
        public bool RequireColumns;

    }



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
        [OpenAiOptional]
        public bool RequireColumns;

    }
    




}
