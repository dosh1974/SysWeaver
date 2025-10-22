using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{
    public sealed class ChartJsFontOptions
    {
        /// <summary>
        /// Font family for all text, follows CSS font-family options.
        /// </summary>
        [OpenAiOptional]
        public String family;

        /// <summary>
        /// Font size (in px) for text. Does not apply to radialLinear scale point labels.
        /// </summary>
        [OpenAiOptional]
        public double? size;

        /// <summary>
        /// Font style, follows CSS font-style options (i.e. normal, italic, oblique, initial, inherit).
        /// Does not apply to tooltip title or footer. 
        /// Does not apply to chart title. 
        /// </summary>
        [OpenAiOptional]
        public string style;

        /// <summary>
        /// Font weight (boldness), can be "normal", "bold", "lighter", "bolder"
        /// </summary>
        [OpenAiOptional]
        public string weight;

        /// <summary>
        /// Height of an individual line of text (scale);
        /// </summary>
        [OpenAiOptional]
        public double? lineHeight;
    }


}
