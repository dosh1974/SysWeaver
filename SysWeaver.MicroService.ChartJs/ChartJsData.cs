using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The data of a chart
    /// </summary>
    public sealed class ChartJsData
    {
        /// <summary>
        /// The labels, one per value on the x-axis, must contain unique values, sorted from left to right
        /// </summary>
        public String[] labels;
        /// <summary>
        /// The data sets, can be one or more
        /// </summary>
        public ChartJsDataSet[] datasets;
    }
}
