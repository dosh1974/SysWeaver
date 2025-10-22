namespace SysWeaver.MicroService
{
    /// <summary>
    /// Represents a chart type.
    /// Must be matched with Chart.TypeNames
    /// </summary>
    public enum ChartTypes
    {
        /// <summary>
        /// A bar chart
        /// </summary>
        Bar = 0,
        
        /// <summary>
        /// A line chart
        /// </summary>
        Line,

        /// <summary>
        /// A pie chart
        /// </summary>
        Pie,

        /// <summary>
        /// A doughnut chart
        /// </summary>
        Doughnut,

        /// <summary>
        /// Polart area chart
        /// </summary>
        PolarArea,
    }
}
