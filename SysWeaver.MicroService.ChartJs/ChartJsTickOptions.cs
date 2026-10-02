using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    public sealed class ChartJsTickOptions
    {
        /// <summary>
        /// Color of ticks.
        /// </summary>
        [AiOptional]
        public String color;

        /// <summary>
        /// If true, show tick labels.
        /// </summary>
        [AiOptional]
        public bool display = true;

        /// <summary>
        /// The font to use for ticks
        /// </summary>
        public ChartJsFontOptions font;

        /// <summary>
        /// Where to put the title.
        /// Can be: "start", "center" or "end"
        /// </summary>
        [AiOptional]
        public String align;

        /// <summary>
        /// Color of label backdrops.
        /// </summary>
        [AiOptional]
        public String backdropColor;

        /// <summary>
        /// Padding of label backdrop.
        /// </summary>
        [AiOptional]
        public int? backdropPadding;

        /// <summary>
        /// z-index of tick layer. 
        /// Useful when ticks are drawn on chart area. 
        /// Values less than zero are drawn under datasets, greater than zero on top.
        /// </summary>
        [AiOptional]
        public int z = 1;

        /// <summary>
        /// If true, draw a background behind the tick labels.
        /// </summary>
        [AiOptional]
        public bool? showLabelBackdrop;

        /// <summary>
        /// If defined and stepSize is not specified, the step size will be rounded to this many decimal places.
        /// </summary>
        [AiOptional]
        public double? precision;

        /// <summary>
        /// User-defined fixed step size for the scale.
        /// </summary>
        [AiOptional]
        public double? stepSize;

        /// <summary>
        /// Should the data be stacked.
        /// </summary>
        [AiOptional]
        public bool? beginAtZero;

    }


}
