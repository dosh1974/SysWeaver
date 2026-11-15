using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Describes how to make a chart from a table
    /// </summary>
    public sealed class TableChartParams
    {
        /// <summary>
        /// The Api that gets the table
        /// </summary>
        public String TableApi;

        /// <summary>
        /// The column(s) to use as key
        /// </summary>
        public String[] Keys;

        /// <summary>
        /// The column(s) to use as series values
        /// </summary>
        public String[] Values;

        /// <summary>
        /// The table request options
        /// </summary>
        public TableDataRequest Options;

        /// <summary>
        /// The title of the chart
        /// </summary>
        public String Title;

        /// <summary>
        /// If multiple keys are specified, separate them using this string.
        /// Defaults to " / ".
        /// </summary>
        public String KeySeparator = " / ";
    }




}
