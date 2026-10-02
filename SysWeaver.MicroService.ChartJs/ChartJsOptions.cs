using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{


   
    public sealed class ChartJsOptions : ChartJsBaseOptions
    {
        /// <summary>
        /// Scales to use (defining axis)
        /// </summary>
        [AiOptional]
        public ChartJsScalesOptions scales;

        /// <summary>
        /// Resizes the chart canvas when its container does 
        /// </summary>
        [AiOptional]
        public bool responsive = true;

        /// <summary>
        /// Maintain the original canvas aspect ratio (width / height) when resizing.
        /// </summary>
        [AiOptional]
        public bool maintainAspectRatio = true;

        /// <summary>
        /// Animation options
        /// </summary>
        [AiIgnore]
        public ChartJsAnimationOption animation = new ChartJsAnimationOption();

        /// <summary>
        /// Plugin options
        /// </summary>
        [AiOptional]
        public ChartJsPlugins plugins;

        /// <summary>
        /// Can be used to create a horizontal bar chart instead.
        /// Values can be "x" (default) or "y" to create a horizontal bar chart.
        /// </summary>
        [AiOptional]
        public String indexAxis;


        /// <summary>
        /// Options specific to elements
        /// </summary>
        [AiIgnore]
        public ChartJsElementsOption elements;

    }

    public sealed class ChartJsElementsOption
    {
        /// <summary>
        /// Point Configuration.
        /// Point elements are used to represent the points in a line, radar or bubble chart.
        /// </summary>
        [AiIgnore]
        public ChartJsBaseOptions point;

        /// <summary>
        /// Line Configuration.
        /// Line elements are used to represent the line in a line chart.
        /// </summary>
        [AiIgnore]
        public ChartJsBaseOptions line;

        /// <summary>
        /// Bar Configuration.
        /// Bar elements are used to represent the bars in a bar chart.
        /// </summary>
        [AiIgnore]
        public ChartJsBaseOptions bar;

        /// <summary>
        /// Arc Configuration.
        /// Arcs are used in the polar area, doughnut and pie charts.
        /// </summary>
        [AiIgnore]
        public ChartJsBaseOptions arc;

    }

}
