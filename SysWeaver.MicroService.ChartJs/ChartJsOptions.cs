using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{

    public sealed class ChartJsOptions : ChartJsBaseOptions
    {
        /// <summary>
        /// Scales to use (defining axis)
        /// </summary>
        [OpenAiOptional]
        public ChartJsScalesOptions scales;

        /// <summary>
        /// Resizes the chart canvas when its container does 
        /// </summary>
        [OpenAiOptional]
        public bool responsive = true;

        /// <summary>
        /// Maintain the original canvas aspect ratio (width / height) when resizing.
        /// </summary>
        [OpenAiOptional]
        public bool maintainAspectRatio = true;

        /// <summary>
        /// Animation options
        /// </summary>
        [OpenAiIgnore]
        public ChartJsAnimationOption animation = new ChartJsAnimationOption();

        /// <summary>
        /// Plugin options
        /// </summary>
        [OpenAiOptional]
        public ChartJsPlugins plugins;

        /// <summary>
        /// Can be used to create a horizontal bar chart instead.
        /// Values can be "x" (default) or "y" to create a horizontal bar chart.
        /// </summary>
        [OpenAiOptional]
        public String indexAxis;





    }


}
