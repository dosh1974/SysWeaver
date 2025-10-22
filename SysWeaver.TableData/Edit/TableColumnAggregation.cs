using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Represents the aggregation of a column
    /// </summary>
    public sealed class TableColumnAggregation
    {
#if DEBUG
        public override string ToString() => String.Concat(Aggregation, '(', ColumnName, ')');
#endif//DEBUG
        /// <summary>
        /// The existing column to aggregate
        /// </summary>
        public String ColumnName;

        /// <summary>
        /// The type of aggregation to perform
        /// </summary>
        public TableColumnAggregations Aggregation;

        /// <summary>
        /// An optional new column name (uses the exsiting name if omitted)
        /// </summary>
        public String NewName;
        /// <summary>
        /// An optional new column title (uses the exsiting title if omitted)
        /// </summary>
        public String NewTitle;
        /// <summary>
        /// An optional new description (uses the exsiting description if omitted)
        /// </summary>
        public String NewDesc;


    }






}
