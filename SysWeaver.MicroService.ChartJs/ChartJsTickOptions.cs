using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    public sealed class ChartJsTickOptions
    {
        /// <summary>
        /// Color of ticks.
        /// </summary>
        [OpenAiOptional]
        public String color;

        /// <summary>
        /// If true, show tick labels.
        /// </summary>
        [OpenAiOptional]
        public bool display = true;

        /// <summary>
        /// The font to use for ticks
        /// </summary>
        public ChartJsFontOptions font;

        /// <summary>
        /// Where to put the title.
        /// Can be: "start", "center" or "end"
        /// </summary>
        [OpenAiOptional]
        public String align;

        /// <summary>
        /// Color of label backdrops.
        /// </summary>
        [OpenAiOptional]
        public String backdropColor;

        /// <summary>
        /// Padding of label backdrop.
        /// </summary>
        [OpenAiOptional]
        public int? backdropPadding;

        /// <summary>
        /// z-index of tick layer. 
        /// Useful when ticks are drawn on chart area. 
        /// Values less than zero are drawn under datasets, greater than zero on top.
        /// </summary>
        public int z = 1;

        /// <summary>
        /// If true, draw a background behind the tick labels.
        /// </summary>
        public bool? showLabelBackdrop;

        /// <summary>
        /// If defined and stepSize is not specified, the step size will be rounded to this many decimal places.
        /// </summary>
        public double? precision;

        /// <summary>
        /// User-defined fixed step size for the scale.
        /// </summary>
        public double? stepSize;


    }


}
