using System;
using SysWeaver.Data;

namespace SysWeaver.Data
{

    /// <summary>
    /// A server side stored table (<see cref="BaseTableData"/>) that clients can refer to by <see cref="DataReference.Id"/>,
    /// for example to view, export or edit it as a table without resending the data.
    /// Created by <see cref="DataReferenceStorage.Add(BaseTableData, int)"/>.
    /// </summary>
    public sealed class TableDataReference : DataReference
    {
        /// <summary>
        /// Returns the table dimensions and the reference info, for debugging.
        /// </summary>
        /// <returns>A text such as <c>Table 4x100 "gXyz" expires at ...</c> (the column count is 0 on a response copy created without columns).</returns>
        public override string ToString() => String.Concat("Table ", Cols?.Length ?? 0, 'x', Rows, ' ', base.ToString());

        /// <summary>
        /// Validate the table before the base constructor schedules the expiration (so that nothing is scheduled for a rejected table).
        /// </summary>
        static BaseTableData Validate(BaseTableData data)
        {
            if (data.Cols == null)
                throw new Exception("Only complete tables with columns may be used!");
            return data;
        }

        /// <summary>
        /// Create a server side reference to a table, cloning the column definitions.
        /// </summary>
        /// <param name="scope">The scope (visibility) of the data.</param>
        /// <param name="id">The unique id of the data.</param>
        /// <param name="data">The table, must have columns.</param>
        /// <param name="timeToLiveInSeconds">Number of seconds to keep the data alive after creation or last use (minimum 10).</param>
        /// <param name="removeAction">Invoked when the reference is removed.</param>
        /// <exception cref="Exception">The table has no columns.</exception>
        internal TableDataReference(DataScopes scope, String id, BaseTableData data, int timeToLiveInSeconds, Action removeAction) : base(scope, id, Validate(data), timeToLiveInSeconds, removeAction)
        {
            var c = data.Cols;
            var cl = c.Length;
            var cc = new TableDataBaseColumn[cl];
            for (int i = 0; i < cl; ++i)
                cc[i] = (c[i] as TableDataBaseColumn).Clone();
            Cols = cc;
            Rows = data.Rows?.Length ?? 0;
        }

        /// <summary>
        /// Create a response copy with the id and row count only (no columns, no data).
        /// </summary>
        TableDataReference(TableDataReference cloneForResponse)
            : base(cloneForResponse)
        {
            Rows = cloneForResponse.Rows;
        }


        /// <summary>
        /// The total number of rows in the table (when the reference was created).
        /// </summary>
        public long Rows;

        /// <summary>
        /// Columns in this table (a clone of the source columns), null in a response copy created with <c>requireCols</c> set to false.
        /// </summary>
        public TableDataBaseColumn[] Cols;

        /// <summary>
        /// Get an instance suitable for returning to a client.
        /// </summary>
        /// <param name="requireCols">If true this instance (including the columns) is returned, else a lightweight copy with only the id and row count.</param>
        /// <returns>The reference to serialize to the client.</returns>
        public TableDataReference AsResponse(bool requireCols)
        {
            if (requireCols)
                return this;
            return new TableDataReference(this);
        }


        #region Server side

        /// <summary>
        /// Get the original data back (server side only, the same instance that was added, not a copy).
        /// </summary>
        /// <returns>The stored table, or null on a response copy.</returns>
        public BaseTableData Get() => DataGet<BaseTableData>();

        /// <summary>
        /// Get the original data back to return it as a whole to a client or an AI (ex: the "GetTableData" web API / AI tool), with a row cap.
        /// A larger table is never truncated silently: an exception tells the caller to select fewer rows first
        /// (ex: an "EditTableData" call with a SortAndFilterRows step using Row / MaxRowCount, then get the new reference).
        /// </summary>
        /// <param name="maxAllowedRows">The maximum number of rows that may be returned, defaults to <see cref="TableDataTools.DefaultMaxAllowedRows"/>.
        /// Zero or negative disables the cap (only for trusted server side callers).</param>
        /// <returns>The stored table (the same instance as <see cref="Get"/>), or null on a response copy.</returns>
        /// <exception cref="InvalidOperationException">The table has more than <paramref name="maxAllowedRows"/> rows.</exception>
        public BaseTableData GetForClient(long maxAllowedRows = TableDataTools.DefaultMaxAllowedRows)
        {
            var data = Get();
            if ((data == null) || (maxAllowedRows <= 0))
                return data;
            var rows = data.Rows?.LongLength ?? 0;
            if (rows > maxAllowedRows)
                throw new InvalidOperationException(String.Concat("The table data reference \"", Id, "\" has ", rows.ToString(), " rows, at most ", maxAllowedRows.ToString(),
                    " rows can be returned. Select fewer rows first, ex: use EditTableData with SortAndFilterRows (Row and MaxRowCount) and get the new reference."));
            return data;
        }

        #endregion//Server side


    }


}
