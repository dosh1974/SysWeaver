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
        /// Set to true unless you already know the exact format of the table being returned.
        /// </summary>
        [OpenAiOptional]
        public bool RequireColumns;

    }



    public sealed class GetTableDataRequest
    {
        /// <summary>
        /// The reference to the table data to get
        /// </summary>
        [EditMin(1)]
        public String TableDataRef;

        /// <summary>
        /// Set to true unless you already know the exact format of the table being returned.
        /// </summary>
        [OpenAiOptional]
        public bool RequireColumns;

    }





}
