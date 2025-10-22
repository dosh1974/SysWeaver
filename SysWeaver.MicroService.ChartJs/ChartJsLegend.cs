using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    public sealed class ChartJsLegend
    {
        /// <summary>
        /// Display the legend
        /// </summary>
        [OpenAiOptional]
        public bool? display;

        /// <summary>
        /// Where to put the legend.
        /// Can be: "start", "center" or "end"
        /// </summary>
        [OpenAiOptional]
        public String align;

        /// <summary>
        /// Position of the legend.
        /// Can be: "top", "left", "bottom", "right", "chartArea"
        /// </summary>
        [OpenAiOptional]
        public String position;

        /// <summary>
        /// Marks that this box should take the full width/height of the canvas
        /// </summary>
        [OpenAiOptional]
        public bool? fullSize = true;

        /// <summary>
        /// Legend will show datasets in reverse order.
        /// </summary>
        [OpenAiOptional]
        public bool? reverse = true;

        /// <summary>
        /// True for rendering the legends from right to left.
        /// </summary>
        [OpenAiOptional]
        public bool? rtl = true;

    }

}
