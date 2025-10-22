using System;

namespace SysWeaver.AI
{
    public sealed class OpenAiTableColumn
    {
        public override string ToString()
            => String.Concat(Name, " [", Type, ']');

        /// <summary>
        /// Name of the column (show as header)
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// Optional column description of the data in the columns, displayed as a tool tip on the column header.
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public String ColDesc { get; set; }

        /// <summary>
        /// The type of data in the column
        /// </summary>
        public OpenAiTableColumnTypes Type { get; set; }

        /// <summary>
        /// An optional value prefix.
        /// Do not format the actual values in the row data if the same output can be achieved using prefix and suffix.
        /// Final text is: Prefix + FormattedValue + Suffix.
        /// The FormattedValue depends on the type.
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public String ValuePrefix { get; set; }

        /// <summary>
        /// An optional value suffix.
        /// Do not format the actual values in the row data if the same output can be achieved using prefix and suffix.
        /// Final text is: Prefix + FormattedValue + Suffix.
        /// The FormattedValue depends on the type.
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public String ValueSuffix { get; set; }

        /// <summary>
        /// Optional value description, displayed as a tool tip on the value.
        /// "{Value}" is replaced by the formatted value (as displayed).
        /// "{Raw}" is replaced by the raw input value.
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public String ValueDesc { get; set; }

        /// <summary>
        /// For decimal types, this is the number of decimals to show.
        /// Do not round the actual values in the row data, use this property for rounding.
        /// The default is 2.
        /// Example: 123.6895 will be displayed as "123.69" if the number of decimals is two.
        /// If the number is negative, the number will be displayed with that many decimals OR without decimals if the number is an integer:
        /// Example: 123.6895 will be displayed as "123.69" if the number of decimals is "-2", but 42.00 will be displayed as "42".
        /// </summary>
        [OpenAiOptional]
        public int NumDecimals { get; set; } = 2;

    }


}
