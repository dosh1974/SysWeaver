using System;
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

        /// <summary>
        /// Shadows
        /// </summary>
        [OpenAiOptional]
        public ChartJsShadow shadow;
        /// <summary>
        /// Shadows
        /// </summary>
        [OpenAiOptional]
        public ChartJsTooltip tooltip;

    }

    public sealed class ChartJsTooltip
    {
        /// <summary>
        /// Are on-canvas tooltips enabled?
        /// </summary>
        public bool? enabled;
    }

    public sealed class ChartJsShadow
    {
        /// <summary>
        /// Shadow offset X in pixels
        /// </summary>
        [OpenAiOptional]
        public double? dx = 2;

        /// <summary>
        /// Shadow offset Y in pixels
        /// </summary>
        [OpenAiOptional]
        public double? dy = 4;

        /// <summary>
        /// Blur radius in pixels
        /// </summary>
        [OpenAiOptional]
        public double? rad = 8;

        /// <summary>
        /// Shadow color
        /// </summary>
        [OpenAiOptional]
        public String color;


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
