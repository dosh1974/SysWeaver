using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// All column values of a row
    /// </summary>
    public sealed class OpenAiTableRow
    {
        public override string ToString()
            => String.Concat(ColumnData?.Length ?? 0, ": ", String.Join("; ", ColumnData?.Nullable()));

        /// <summary>
        /// Column data for this row, must have one value per header.
        /// </summary>
        [EditMin(1)]
        public String[] ColumnData { get; set; }
    }


}
