using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{

    public class ChartJsCorner
    {
        /// <summary>
        /// Top left corner value
        /// </summary>
        [OpenAiOptional]
        public double? topLeft;

        /// <summary>
        /// Top right corner value
        /// </summary>
        [OpenAiOptional]
        public double? topRight;

        /// <summary>
        /// Bottom right corner value
        /// </summary>
        [OpenAiOptional]
        public double? bottomRight;

        /// <summary>
        /// Bottom left corner value
        /// </summary>
        [OpenAiOptional]
        public double? bottomLeft;
    }


    public class ChartJsBaseOptions
    {
        /// <summary>
        /// The background CSS-color of the data, can be null or empty.
        /// One color per value on the x-axis (labels).
        /// To use the same color for all values, supply an array with one element, ex: ["#f00"].
        /// </summary>
        [OpenAiOptional]
        public String[] backgroundColor;

        /// <summary>
        /// The border CSS-color of the data, can be null or empty.
        /// One color per value on the x-axis (labels).
        /// To use the same color for all values, supply an array with one element, ex: ["#f00"].
        /// </summary>
        [OpenAiOptional]
        public String[] borderColor;

        /// <summary>
        /// The width in pixels of the border
        /// </summary>
        [OpenAiOptional]
        public double? borderWidth;

        /// <summary>
        /// The radius to use for rounded corners
        /// </summary>
        [OpenAiOptional]
        public ChartJsCorner borderRadius = new ChartJsCorner
        {
            topLeft = 2,
            topRight = 2,
        };

        /// <summary>
        /// If true the border isn't drawn at the bottom (start)
        /// </summary>
        [OpenAiOptional]
        public bool? borderSkipped = false;




        /// <summary>
        /// Percent (0-1) of the available width each bar should be within the category width. 1.0 will take the whole category width and put the bars right next to each other. 
        /// </summary>
        [OpenAiOptional]
        public double? barPercentage;

        /// <summary>
        /// Percent (0-1) of the available width each category should be within the sample width.
        /// </summary>
        [OpenAiOptional]
        public double? categoryPercentage;

        #region Line chart lines

        /// <summary>
        /// For line charts only.
        /// Instead of continous smooth lines binding the data points, the lines can be stepped. 
        /// The valid values are: 
        /// "before" - Step-before Interpolation. 
        /// "after" - Step-after Interpolation. 
        /// "middle" - Step-middle Interpolation. 
        /// </summary>
        [OpenAiOptional]
        public String stepped;

        /// <summary>
        /// For line charts only.
        /// If false, the line is not drawn for this dataset.
        /// </summary>
        [OpenAiOptional]
        public bool? showLine;

        /// <summary>
        /// Bezier curve tension of the line. Set to 0 to draw straightlines. This option is ignored if monotone cubic interpolation is used.
        /// </summary>
        [OpenAiOptional]
        public double? tension;

        /// <summary>
        /// How and if to fill area in a line chart
        /// </summary>
        [OpenAiOptional]
        public ChartJsLineFillOptions fill;

        #endregion Line chart lines

        #region Line chart points

        /// <summary>
        /// Point radius, default 3.
        /// </summary>
        [OpenAiOptional]
        public double? radius;

        /// <summary>
        /// Point style, default: 'circle'.
        /// Valid:
        ///    "circle"
        ///    "cross"
        ///    "crossRot"
        ///    "dash"
        ///    "line"
        ///    "rect"
        ///    "rectRounded"
        ///    "rectRot"
        ///    "star"
        ///    "triangle"
        /// </summary>
        [OpenAiOptional]
        public String pointStyle;

        /// <summary>
        /// Point rotation (in degrees), defaul: 0
        /// </summary>
        [OpenAiOptional]
        public double? rotation;

        /// <summary>
        /// Extra radius added to point radius for hit detection, default: 1
        /// </summary>
        [OpenAiIgnore]
        public double? hitRadius;

        /// <summary>
        /// Point radius when hovered, default: 4
        /// </summary>
        [OpenAiOptional]
        public double? hoverRadius;

        /// <summary>
        /// Stroke width when hovered, default: 1
        /// </summary>
        [OpenAiOptional]
        public double? hoverBorderWidth;

        #endregion//Line chart points


    }


    public sealed class ChartJsLineFillOptions
    {
        /// <summary>
        /// How to fill the area under the line.
        /// Valid:
        /// "origin"
        /// "start"
        /// "end"
        /// </summary>
        [OpenAiOptional]
        public String target;

        /// <summary>
        /// If no color is set, the default color will be the background color of the chart.
        /// </summary>
        [OpenAiOptional]
        public string above;

        /// <summary>
        /// If no color is set, the default color will be the background color of the chart.
        /// </summary>
        [OpenAiOptional]
        public string below;
    }



}
