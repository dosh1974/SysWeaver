using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    public sealed class ChartJsScalesOptions
    {
        /// <summary>
        /// X-axis scale definition
        /// </summary>
        [OpenAiOptional]
        public ChartJsScaleOptions x;
        /// <summary>
        /// Y-axis scale definition
        /// </summary>
        [OpenAiOptional]
        public ChartJsScaleOptions y;
        /// <summary>
        /// Secondary x-axis scale definition
        /// </summary>
        [OpenAiOptional]
        public ChartJsScaleOptions x2;
        /// <summary>
        /// Radial scale definition
        /// </summary>
        [OpenAiOptional]
        public ChartJsScaleOptions r;
    }


}
