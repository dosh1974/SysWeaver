using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// A single row in a <see cref="BaseTableData"/>.
    /// </summary>
    public sealed class TableDataRow
    {
#if DEBUG
        /// <summary>
        /// Debug builds only: all values of the row separated by '|'.
        /// </summary>
        /// <returns>A short text for debugging.</returns>
        public override string ToString() => Values == null ? "null" : String.Join('|', Values);
#endif//DEBUG
        /// <summary>
        /// The column values, one per column in <see cref="CommonTableData.Cols"/> (in the same order)
        /// </summary>
        public Object[] Values;
    }

}
