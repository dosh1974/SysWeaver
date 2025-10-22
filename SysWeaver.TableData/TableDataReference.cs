using System;
using SysWeaver.Data;

namespace SysWeaver.Data
{

    /// <summary>
    /// Data that represents a table
    /// </summary>
    public sealed class TableDataReference : DataReference
    {
        public override string ToString() => String.Concat("Table ", Cols.Length, 'x', Rows, ' ', base.ToString());

        internal TableDataReference(DataScopes scope, String id, BaseTableData data, int timeToLiveInSeconds, Action removeAction) : base(scope, id, data, timeToLiveInSeconds, removeAction)
        {
            var c = data.Cols;
            if (c == null)
                throw new Exception("Only complete tables with columns may be used!");
            var cl = c.Length;
            var cc = new TableDataBaseColumn[cl];
            for (int i = 0; i < cl; ++i)
                cc[i] = (c[i] as TableDataBaseColumn).Clone();
            Cols = cc;
            Rows = data.Rows?.Length ?? 0;
        }

        TableDataReference(TableDataReference cloneForResponse)
            : base(cloneForResponse)
        {
            Rows = cloneForResponse.Rows;
        }


        /// <summary>
        /// The total number of rows
        /// </summary>
        public long Rows;

        /// <summary>
        /// Columns in this table
        /// </summary>
        public TableDataBaseColumn[] Cols;

        public TableDataReference AsResponse(bool requireCols)
        {
            if (requireCols)
                return this;
            return new TableDataReference(this);
        }


        #region Server side

        /// <summary>
        /// Get the original data back
        /// </summary>
        /// <returns></returns>
        public BaseTableData Get() => DataGet<BaseTableData>();

        #endregion//Server side


    }


}
