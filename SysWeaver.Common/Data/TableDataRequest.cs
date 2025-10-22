using System;
using SysWeaver.AI;

namespace SysWeaver.Data
{


    /// <summary>
    /// Used when requesting data as a data table
    /// </summary>
    public class TableDataSortAndFilterRequest
    {
        /// <summary>
        /// Column filtering
        /// </summary>
        [EditRange(0, 10)]
        [EditAllowNull]
        [OpenAiOptional]
        public TableDataFilter[] Filters;


        /// <summary>
        /// Column names, first entry is the primary order, prefix with a '-' for descending order, ex:
        /// ["Name"]
        /// ["-Size"]
        /// ["Ext", "Name"]
        /// </summary>
        [EditAllowNull]
        public String[] Order;
    }


    /// <summary>
    /// Used when requesting data as a data table
    /// </summary>
    public class TableDataOrderRequest : TableDataSortAndFilterRequest
    {
#if DEBUG
        public override string ToString() => String.Concat( MaxRowCount, " @ ", Row);
#endif//DEBUG

        /// <summary>
        /// The first row to return (zero based index).
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
        [OpenAiOptional]
        public long Row;

        /// <summary>
        /// The maximum number of rows to return.
        /// 0 = All rows.
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
        [OpenAiOptional]
        public long MaxRowCount;
            
        /// <summary>
        /// Check availability of more rows (for paging).
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
        public long LookAheadCount;

    }



    /// <summary>
    /// Used when requesting data as a data table
    /// </summary>
    public class TableDataRequest : TableDataOrderRequest
    {
        public TableDataRequest()
        {
            MaxRowCount = 100;
        }

        /// <summary>
        /// Change counter, if this matches the internal counter, no column information will be returned (optimization)
        /// </summary>
        [OpenAiIgnore]
        public long Cc;

        /// <summary>
        /// Extra per table type params
        /// </summary>
        [EditAllowNull]
        [OpenAiIgnore]
        public String Param;

        /// <summary>
        /// The db index to use for full text search (defaults to "FullText" if none is supplied)
        /// </summary>
        [OpenAiIgnore]
        public String SearchIndex;

        /// <summary>
        /// The full text search (null or empty to do a regular table data request).
        /// Only available on Database mirrored tables that have a full text search index.
        /// </summary>
        [OpenAiIgnore]
        public String SearchText;

        /// <summary>
        /// If true, use natural language search instead of boolean search
        /// </summary>
        [OpenAiIgnore]
        public bool SearchNatural = true;
    }


    public class TableDataState
    {
        
        public TableDataRequest RequestParams;

        public int FilterRows;

        /// <summary>
        /// 0 = None, 1 = Simple, 2 = Advanced
        /// </summary>
        public int Expanded = 1;

        public bool? AutoRowCount = true;

        public TableDataFilter[][] Filters;

    }



}
