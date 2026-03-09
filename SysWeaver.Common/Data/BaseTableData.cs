using System;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.AI;

namespace SysWeaver.Data
{

    public abstract class CommonTableData
    {
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
        /// Title of the table.
        /// </summary>
        [OpenAiOptional]
        public String Title;

        public void CopyFrom(CommonTableData s)
        {
            RowCount = s.RowCount;
            Cols = s.Cols;
            Title = s.Title;
        }

    }

    public class BaseTableData : CommonTableData
    {
#if DEBUG
        public override string ToString() => String.Concat( Cols?.Length ?? Rows?.FirstOrDefault()?.Values?.Length ?? 0, 'x', Rows?.Length ?? 0);
#endif//DEBUG

        /// <summary>
        /// Data rows
        /// </summary>
        public TableDataRow[] Rows;

        public void CopyFrom(BaseTableData s)
        {
            base.CopyFrom(s);
            Rows = s.Rows;
        }

        public BaseTableData Clone()
        {
            var t = new BaseTableData();
            t.CopyFrom(this);
            return t;
        }

    }

    public sealed class TypedTableData<T> : CommonTableData
    {

        /// <summary>
        /// A change counter for the column information, if the request Cc is equal to this, no column information is sent
        /// </summary>
        public long Cc;

        /// <summary>
        /// Number of ms to wait before a new refresh
        /// </summary>
        [EditMin(0)]
        public long RefreshRate;

        /// <summary>
        /// Data rows
        /// </summary>
        public T[] Rows;

        public TypedTableData()
        {
        }

        public TypedTableData<N> Retype<N>(Func<T, N> convert)
            => new TypedTableData<N>
            {
                Cc = Cc,
                RefreshRate = RefreshRate,
                Rows = Rows.Convert(convert),
                Cols = Cols,
                RowCount = RowCount,
                Title = Title,
            };

        public async ValueTask<TypedTableData<N>> RetypeAsync<N>(Func<T, ValueTask<N>> convert)
            => new TypedTableData<N>
            {
                Cc = Cc,
                RefreshRate = RefreshRate,
                Rows = await Rows.ConvertAsyncValue(convert).ConfigureAwait(false),
                Cols = Cols,
                RowCount = RowCount,
                Title = Title,
            };

    }




}
