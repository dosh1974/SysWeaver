using System;
using System.Text.Json.Serialization;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    public sealed class ChartJsScaleOptions
    {
        /// <summary>
        /// Controls the axis visibility
        /// </summary>
        [OpenAiOptional]
        public bool? display = true;

        /// <summary>
        /// Reverse the scale.
        /// </summary>
        [OpenAiOptional]
        public bool? reverse;

        /// <summary>
        /// Should the data be stacked.
        /// </summary>
        [OpenAiOptional]
        public bool? stacked;

        /// <summary>
        /// User defined minimum number for the scale, overrides minimum value from data
        /// </summary>
        [OpenAiOptional]
        public double? min;

        /// <summary>
        /// User defined maximum number for the scale, overrides maximum value from data
        /// </summary>
        [OpenAiOptional]
        public double? max;

        /// <summary>
        /// Grid line configuration
        /// </summary>
        [OpenAiOptional]
        public ChartJsGridOptions grid;

        /// <summary>
        /// Tick configuration
        /// </summary>
        [OpenAiOptional]
        public ChartJsTickOptions ticks;

        /// <summary>
        /// Axis title configuration
        /// </summary>
        [OpenAiOptional]
        public ChartJsTitle title;

        /// <summary>
        /// Point labels
        /// </summary>
        [OpenAiOptional]
        public ChartJsPointLabel pointLabels;
    }


    public sealed class ChartJsPointLabel
    {
        /// <summary>
        /// If true, point labels are shown
        /// </summary>
        [OpenAiOptional]
        public bool? display;

        /// <summary>
        /// If true, point labels are centered
        /// </summary>
        [OpenAiOptional]
        public bool? centerPointLabels;

        /// <summary>
        /// Color of label
        /// </summary>
        [OpenAiOptional]
        public String color;

        /// <summary>
        /// The font to use
        /// </summary>
        [OpenAiOptional]
        public ChartJsFontOptions font;

        /// <summary>
        /// Padding between chart and point labels.
        /// </summary>
        [OpenAiOptional]
        public int? padding;

    }
}
