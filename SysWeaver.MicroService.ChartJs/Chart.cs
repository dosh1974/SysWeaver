using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Data required to draw a graph
    /// </summary>
    public sealed class Chart
    {
        
        /// <summary>
        /// Must be matched with the enum 
        /// </summary>
        public static readonly String[] TypeNames = 
            [
                "bar",
                "line",
                "pie",
                "doughnut",
                "polarArea",
            ];


        /// <summary>
        /// Title of the chart.
        /// </summary>
        public String Title;

        /// <summary>
        /// Type of chart
        /// </summary>
        [OpenAiOptional]
        public ChartTypes Type;

        /// <summary>
        /// The labels (point on the x-axis).
        /// Data will be shown left to right in this order.
        /// Always try to present sorted data if applicable.
        /// </summary>
        public String[] Labels;

        /// <summary>
        /// The data series (y-value for each label)
        /// </summary>
        public ChartSeries[] Series;

        /// <summary>
        /// If true the data series are stacked on top of each other.
        /// </summary>
        [OpenAiOptional]
        public bool Stack;

        /// <summary>
        /// If true the chart should have a horizontal layout instead of a vertical (if applicable)
        /// </summary>
        [OpenAiOptional]
        public bool Horizontal;

        /// <summary>
        /// Sort data by this series (name of the series to use).
        /// If this string starts with a '-', the data is sorted in descending order.
        /// </summary>
        [OpenAiOptional]
        public String SortBySeries;

        /// <summary>
        /// The title to put on the values.
        /// Typically the unit of the values (if applicable), like "km", "meters" or "hours" etc.
        /// If there is no unit or it's a count something like: "# of People", "Response Choices", "Votes" is suitable.
        /// </summary>
        [OpenAiOptional]
        public String ValueTitle;

        /// <summary>
        /// If true the chart should have a smoothed lines instead of straight lines (applies to line charts only)
        /// </summary>
        [OpenAiOptional]
        public bool SmoothLines;


    }
}
