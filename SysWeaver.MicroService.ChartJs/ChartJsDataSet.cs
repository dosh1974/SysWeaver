using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A data set (a series) of a chart
    /// </summary>
    public sealed class ChartJsDataSet : ChartJsBaseOptions
    {
        /// <summary>
        /// The label for the dataset which appears in the legend and tooltips.
        /// </summary>
        [AutoTranslate]
        [AutoTranslateContext("This is a label for a data set in a chart image")]
        public String label;

        /// <summary>
        /// The data values (one for each label)
        /// </summary>
        //public ChartJsDataPoint[] data;
        public double[] data;


        /// <summary>
        /// Id of what x-axis to use.
        /// </summary>
        [AiIgnore]
        public String xAxisID = "x";

        /// <summary>
        /// Chart.js "parsing" option: false if the data is already in the internal format and doesn't need to be parsed
        /// </summary>
        [AiIgnore]
        public bool? parsing;




    }


}
