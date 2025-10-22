using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
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
        [OpenAiIgnore]
        public String xAxisID = "x";

        /// <summary>
        /// If true the border isn't drawn at the bottom (start)
        /// </summary>
        [OpenAiIgnore]
        public bool? parsing;
    }


}
