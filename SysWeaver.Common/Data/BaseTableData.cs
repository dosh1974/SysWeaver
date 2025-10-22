using System;
using System.Linq;
using SysWeaver.AI;

namespace SysWeaver.Data
{
    public class BaseTableData
    {
#if DEBUG
        public override string ToString() => String.Concat( Cols?.Length ?? Rows?.FirstOrDefault()?.Values?.Length ?? 0, 'x', Rows?.Length ?? 0);
#endif//DEBUG

        /// <summary>
        /// Rows in the data, this is the first row + number of returned rows + look ahead rows (that are avasilable).
        /// Example (page with 20 items, stepping max 3 pages forward at a time):
        ///     Request:
        ///         Row = 20
        ///         MaxRowCount = 20
        ///         LookAhead = 20 * 3 + 1
        ///     Response:
        ///         RowCount = 35 => There are 35 rows total, 15 rows will be returned for page 2 and no more pages exist.
        ///         RowCount = 50 => There are 50 rows total, 20 rows will be returned for page 2 and a page 3 exists.
        ///         RowCount = 90 => There are 90 rows total, 20 rows will be returned for page 2 and a page 3, 4, 5 exists.
        ///         RowCount = 100 => There are 100 rows total, 20 rows will be returned for page 2 and a page 3, 4, 5 exists.
        ///         RowCount = 101 => There are at least 101 rows total, 20 rows will be returned for page 2 and a page 3, 4, 5, 6 exists and maybe more pages.
        /// </summary>
        [EditMin(0)]
        [OpenAiIgnore]
        public long RowCount;

        /// <summary>
        /// Columns, can be null if the request change counter matches the internal change counter (no changes)
        /// </summary>
        public TableDataColumn[] Cols;

        /// <summary>
        /// Data rows
        /// </summary>
        public TableDataRow[] Rows;

        /// <summary>
        /// Title of the table.
        /// </summary>
        [OpenAiOptional]
        public String Title;

        public void CopyFrom(BaseTableData s)
        {
            RowCount = s.RowCount;
            Cols = s.Cols;
            Rows = s.Rows;
            Title = s.Title;
        }

        public BaseTableData Clone()
        {
            var t = new BaseTableData();
            t.CopyFrom(this);
            return t;
        }

    }



}
