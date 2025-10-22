using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    public sealed class ChartJsPlugins
    {
        /// <summary>
        /// Title options
        /// </summary>
        [OpenAiOptional]
        public ChartJsTitle title;

        /// <summary>
        /// Legend options
        /// </summary>
        [OpenAiOptional]
        public ChartJsLegend legend;

        /// <summary>
        /// Data labels 
        /// </summary>
        [OpenAiOptional]
        public ChartJsDataLabels datalabels;

    }


    public sealed class ChartJsDataLabels
    {
        /// <summary>
        /// Display the legend
        /// </summary>
        [OpenAiOptional]
        public bool? display;


    }

}
