using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Represents a data table
    /// </summary>
    public sealed class OpenAiTable
    {
        public override string ToString()
            => String.Concat(Columns?.Length ?? 0, "x", Rows?.Length ?? 0, " (Columns x Rows)");

        /// <summary>
        /// Title of the table.
        /// </summary>
        public String Title;

        /// <summary>
        /// Defines the columns that this table have
        /// </summary>
        [EditMin(1)]
        public OpenAiTableColumn[] Columns { get; set; }

        /// <summary>
        /// All rows in the data
        /// </summary>
        public OpenAiTableRow[] Rows { get; set; }

    }


}
