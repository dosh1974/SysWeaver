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
        [AiOptional]
        public bool? display = true;

        /// <summary>
        /// Reverse the scale.
        /// </summary>
        [AiOptional]
        public bool? reverse;

        /// <summary>
        /// Should the data be stacked.
        /// </summary>
        [AiOptional]
        public bool? stacked;

        /// <summary>
        /// User defined minimum number for the scale, overrides minimum value from data
        /// </summary>
        [AiOptional]
        public double? min;

        /// <summary>
        /// User defined maximum number for the scale, overrides maximum value from data
        /// </summary>
        [AiOptional]
        public double? max;

        /// <summary>
        /// Grid line configuration
        /// </summary>
        [AiOptional]
        public ChartJsGridOptions grid;

        /// <summary>
        /// Tick configuration
        /// </summary>
        [AiOptional]
        public ChartJsTickOptions ticks;

        /// <summary>
        /// Axis title configuration
        /// </summary>
        [AiOptional]
        public ChartJsTitle title;

        /// <summary>
        /// Point labels
        /// </summary>
        [AiOptional]
        public ChartJsPointLabel pointLabels;
    }


    public sealed class ChartJsPointLabel
    {
        /// <summary>
        /// If true, point labels are shown
        /// </summary>
        [AiOptional]
        public bool? display;

        /// <summary>
        /// If true, point labels are centered
        /// </summary>
        [AiOptional]
        public bool? centerPointLabels;

        /// <summary>
        /// Color of label
        /// </summary>
        [AiOptional]
        public String color;

        /// <summary>
        /// The font to use
        /// </summary>
        [AiOptional]
        public ChartJsFontOptions font;

        /// <summary>
        /// Padding between chart and point labels.
        /// </summary>
        [AiOptional]
        public int? padding;

    }
}
