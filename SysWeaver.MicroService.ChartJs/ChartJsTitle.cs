using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    public sealed class ChartJsTitle
    {
        /// <summary>
        /// Display the title
        /// </summary>
        [OpenAiOptional]
        public bool? display;

        /// <summary>
        /// Title text to display. 
        /// Each array element is rendered on a separated row.
        /// </summary>
        public String[] text;

        /// <summary>
        /// Where to put the title.
        /// Can be: "start", "center" or "end"
        /// </summary>
        [OpenAiOptional]
        public String align;

        /// <summary>
        /// Text color as a CSS color string.
        /// </summary>
        [OpenAiOptional]
        public String color;

        /// <summary>
        /// Marks that this box should take the full width/height of the canvas. If false, the box is sized and placed above/beside the chart area.
        /// </summary>
        [OpenAiOptional]
        public bool? fullSize = true;

        /// <summary>
        /// Position of the title.
        /// Can be: "top", "left", "bottom" or "right"
        /// </summary>
        [OpenAiOptional]
        public String position;

        /// <summary>
        /// The font to use
        /// </summary>
        [OpenAiOptional]
        public ChartJsFontOptions font;

    }


}
