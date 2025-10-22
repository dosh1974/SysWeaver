namespace SysWeaver.Data
{
    public enum TableColumnAggregations
    {
        /// <summary>
        /// Selects the first value in the column
        /// </summary>
        SelectFirst = 0,
        /// <summary>
        /// Selects the last value in the column
        /// </summary>
        SelectLast,
        /// <summary>
        /// Output's the count (type changed to Int32 or Int64)
        /// </summary>
        Count,
        /// <summary>
        /// The minimum value (not available for all types)
        /// </summary>
        Min,
        /// <summary>
        /// The maximum value (not available for all types)
        /// </summary>
        Max,
        /// <summary>
        /// The sum of all values (not available for all types)
        /// </summary>
        Sum,
        /// <summary>
        /// The average of all values (not available for all types)
        /// </summary>
        Average,
    }






}
