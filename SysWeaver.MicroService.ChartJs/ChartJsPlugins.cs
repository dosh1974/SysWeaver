using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The plugin options of a chart
    /// </summary>
    public sealed class ChartJsPlugins
    {
        /// <summary>
        /// Title options
        /// </summary>
        [AiOptional]
        public ChartJsTitle title;

        /// <summary>
        /// Legend options
        /// </summary>
        [AiOptional]
        public ChartJsLegend legend;

        /// <summary>
        /// Data labels 
        /// </summary>
        [AiOptional]
        public ChartJsDataLabels datalabels;

        /// <summary>
        /// Shadows
        /// </summary>
        [AiOptional]
        public ChartJsShadow shadow;
        /// <summary>
        /// Shadows
        /// </summary>
        [AiOptional]
        public ChartJsTooltip tooltip;

    }

    /// <summary>
    /// Options for the tooltip plugin
    /// </summary>
    public sealed class ChartJsTooltip
    {
        /// <summary>
        /// Are on-canvas tooltips enabled?
        /// </summary>
        public bool? enabled;
    }

    /// <summary>
    /// Options for the shadow plugin
    /// </summary>
    public sealed class ChartJsShadow
    {
        /// <summary>
        /// Shadow offset X in pixels
        /// </summary>
        [AiOptional]
        public double? dx = 2;

        /// <summary>
        /// Shadow offset Y in pixels
        /// </summary>
        [AiOptional]
        public double? dy = 4;

        /// <summary>
        /// Blur radius in pixels
        /// </summary>
        [AiOptional]
        public double? rad = 8;

        /// <summary>
        /// Shadow color
        /// </summary>
        [AiOptional]
        public String color;


    }


    /// <summary>
    /// Options for the data labels plugin
    /// </summary>
    public sealed class ChartJsDataLabels
    {
        /// <summary>
        /// Display the data labels
        /// </summary>
        [AiOptional]
        public bool? display;


    }

}
