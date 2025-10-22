namespace SysWeaver.Data
{
    public enum TableDataFilterOps
    {
        /// <summary>
        /// The row value must be equal to the filter value
        /// </summary>
        Equals,
        /// <summary>
        /// The row value may not be equal to the filter value
        /// </summary>
        NotEqual,
        /// <summary>
        /// The row value must be less than the filter value
        /// </summary>
        LessThan,
        /// <summary>
        /// The row value must be greater than the filter value
        /// </summary>
        GreaterThan,
        /// <summary>
        /// The row value must be less or equal to the filter value
        /// </summary>
        LessEqual,
        /// <summary>
        /// The row value must be greater or equal to the filter value
        /// </summary>
        GreaterEqual,
        /// <summary>
        /// The row value must contain the filter value
        /// </summary>
        Contains,
        /// <summary>
        /// The row value must start with the filter value
        /// </summary>
        StartsWith,
        /// <summary>
        /// The row value must end with the filter value
        /// </summary>
        EndsWith,
        /// <summary>
        /// The row value must be equal to any of the comma separated filter values
        /// </summary>
        AnyOf,
        /// <summary>
        /// The row value may not be equal to any of the comma separated filter values
        /// </summary>
        NoneOf,
        /// <summary>
        /// [min, max) The row value must be greater or equal to min AND less than max (2 comma separated values in the filter value)
        /// </summary>
        InRange,
        /// <summary>
        /// The row value must be less than min OR greater than max (2 comma separated values in the filter value)
        /// </summary>
        OutsideRange,
    }

    public static class TableDataFilterOpsProps
    {
        public const int Count = 13;
    }

}
