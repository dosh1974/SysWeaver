using System;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.AI;

namespace SysWeaver.Data
{

    /// <summary>
    /// Members common to all table data responses.
    /// </summary>
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
        [AiIgnore]
        public long RowCount;

        /// <summary>
        /// Columns, can be null if the request change counter matches the internal change counter (no changes)
        /// </summary>
        public TableDataColumn[] Cols;

        /// <summary>
        /// Title of the table.
        /// </summary>
        [AiOptional]
        public String Title;

        /// <summary>
        /// Copy the common values from another instance (shallow copy).
        /// </summary>
        /// <param name="s">The instance to copy from</param>
        public void CopyFrom(CommonTableData s)
        {
            RowCount = s.RowCount;
            Cols = s.Cols;
            Title = s.Title;
        }

    }

    /// <summary>
    /// Table data where each row is a <see cref="TableDataRow"/> containing the column values (type agnostic).
    /// </summary>
    public class BaseTableData : CommonTableData
    {
#if DEBUG
        public override string ToString() => String.Concat( Cols?.Length ?? Rows?.FirstOrDefault()?.Values?.Length ?? 0, 'x', Rows?.Length ?? 0);
#endif//DEBUG

        /// <summary>
        /// Data rows
        /// </summary>
        public TableDataRow[] Rows;

        /// <summary>
        /// Copy all values from another instance (shallow copy, the arrays are shared).
        /// </summary>
        /// <param name="s">The instance to copy from</param>
        public void CopyFrom(BaseTableData s)
        {
            base.CopyFrom(s);
            Rows = s.Rows;
        }

        /// <summary>
        /// Create a shallow copy (the arrays are shared).
        /// </summary>
        /// <returns>A new instance</returns>
        public BaseTableData Clone()
        {
            var t = new BaseTableData();
            t.CopyFrom(this);
            return t;
        }

    }

    /// <summary>
    /// Table data where each row is an instance of the row type (the columns are the members of the row type).
    /// </summary>
    /// <typeparam name="T">The row type</typeparam>
    public class TypedTableData<T> : CommonTableData
    {

        /// <summary>
        /// A change counter for the column information, if the request Cc is equal to this, no column information is sent
        /// </summary>
        [AiIgnore]
        public long Cc;

        /// <summary>
        /// Number of ms to wait before a new refresh
        /// </summary>
        [EditMin(0)]
        [AiIgnore]
        public long RefreshRate;

        /// <summary>
        /// Data rows
        /// </summary>
        public T[] Rows;

        /// <summary>
        /// Create an empty instance.
        /// </summary>
        public TypedTableData()
        {
        }

        /// <summary>
        /// Copy all values from another instance (shallow copy, the arrays are shared).
        /// </summary>
        /// <param name="s">The instance to copy from</param>
        public void CopyFrom(TypedTableData<T> s)
        {
            base.CopyFrom(s);
            Cc = s.Cc;
            RefreshRate = s.RefreshRate;
            Rows = s.Rows;
        }

        /// <summary>
        /// Create a shallow copy (the arrays are shared).
        /// </summary>
        /// <returns>A new instance</returns>
        public TypedTableData<T> Clone()
        {
            var t = new TypedTableData<T>();
            t.CopyFrom(this);
            return t;
        }


        /// <summary>
        /// Create a new table data with the rows converted to another type, all other values (including the columns) are copied as is.
        /// </summary>
        /// <typeparam name="N">The new row type</typeparam>
        /// <param name="convert">The function used to convert each row</param>
        /// <returns>A new instance</returns>
        /// <remarks>The columns are not updated, so they still describe <typeparamref name="T"/>.</remarks>
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

        /// <summary>
        /// Create a new table data with the rows converted to another type (asynchronously), all other values (including the columns) are copied as is.
        /// </summary>
        /// <typeparam name="N">The new row type</typeparam>
        /// <param name="convert">The function used to convert each row</param>
        /// <returns>A new instance</returns>
        /// <remarks>The columns are not updated, so they still describe <typeparamref name="T"/>.</remarks>
        public async Task<TypedTableData<N>> RetypeAsync<N>(Func<T, Task<N>> convert)
            => new TypedTableData<N>
            {
                Cc = Cc,
                RefreshRate = RefreshRate,
                Rows = await Rows.ConvertAsync(convert).ConfigureAwait(false),
                Cols = Cols,
                RowCount = RowCount,
                Title = Title,
            };

    }




}
