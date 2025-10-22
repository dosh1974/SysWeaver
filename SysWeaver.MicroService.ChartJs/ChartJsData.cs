using System;

namespace SysWeaver.MicroService
{
    public sealed class ChartJsData
    {
        /// <summary>
        /// The labels, on per value on the x-axis, must contain unique values, sorted in left to right
        /// </summary>
        public String[] labels;
        /// <summary>
        /// The data sets, can be one or more
        /// </summary>
        public ChartJsDataSet[] datasets;
    }
}
