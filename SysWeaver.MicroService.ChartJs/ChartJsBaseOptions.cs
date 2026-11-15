using System;
using SysWeaver.AI;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// A value for each of the four corners (ex: the border radius)
    /// </summary>
    public class ChartJsCorner
    {
        /// <summary>
        /// Top left corner value
        /// </summary>
        [AiOptional]
        public double? topLeft;

        /// <summary>
        /// Top right corner value
        /// </summary>
        [AiOptional]
        public double? topRight;

        /// <summary>
        /// Bottom right corner value
        /// </summary>
        [AiOptional]
        public double? bottomRight;

        /// <summary>
        /// Bottom left corner value
        /// </summary>
        [AiOptional]
        public double? bottomLeft;
    }


    /// <summary>
    /// Style options shared by data sets and elements of a chart
    /// </summary>
    public class ChartJsBaseOptions
    {
        /// <summary>
        /// The background CSS-color of the data, can be null or empty.
        /// One color per value on the x-axis (labels).
        /// To use the same color for all values, supply an array with one element, ex: ["#f00"].
        /// </summary>
        [AiOptional]
        public String[] backgroundColor;

        /// <summary>
        /// The border CSS-color of the data, can be null or empty.
        /// One color per value on the x-axis (labels).
        /// To use the same color for all values, supply an array with one element, ex: ["#f00"].
        /// </summary>
        [AiOptional]
        public String[] borderColor;

        /// <summary>
        /// The width in pixels of the border
        /// </summary>
        [AiOptional]
        public double? borderWidth;

        /// <summary>
        /// The radius to use for rounded corners (bar charts), by default the top corners have a radius of 2
        /// </summary>
        [AiOptional]
        public ChartJsCorner borderRadius = new ChartJsCorner
        {
            topLeft = 2,
            topRight = 2,
        };

        /// <summary>
        /// If true the border isn't drawn at the bottom (start of the bar), default false (bar charts)
        /// </summary>
        [AiOptional]
        public bool? borderSkipped = false;




        /// <summary>
        /// For bar charts only.
        /// Percent (0-1) of the available width each bar should be within the category width. 1.0 will take the whole category width and put the bars right next to each other.
        /// </summary>
        [AiOptional]
        public double? barPercentage;

        /// <summary>
        /// For bar charts only.
        /// Percent (0-1) of the available width each category should be within the sample width.
        /// </summary>
        [AiOptional]
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
        [AiOptional]
        public String stepped;

        /// <summary>
        /// For line charts only.
        /// If false, the line is not drawn for this dataset.
        /// </summary>
        [AiOptional]
        public bool? showLine;

        /// <summary>
        /// Bezier curve tension of the line. Set to 0 to draw straightlines. This option is ignored if monotone cubic interpolation is used.
        /// </summary>
        [AiOptional]
        public double? tension;

        /// <summary>
        /// How and if to fill area in a line chart
        /// </summary>
        [AiOptional]
        public ChartJsLineFillOptions fill;

        #endregion Line chart lines

        #region Line chart points

        /// <summary>
        /// Point radius, default 3.
        /// </summary>
        [AiOptional]
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
        [AiOptional]
        public String pointStyle;

        /// <summary>
        /// Point rotation (in degrees), default: 0
        /// </summary>
        [AiOptional]
        public double? rotation;

        /// <summary>
        /// Extra radius added to point radius for hit detection, default: 1 (not available to AI generated charts)
        /// </summary>
        [AiIgnore]
        public double? hitRadius;

        /// <summary>
        /// Point radius when hovered, default: 4
        /// </summary>
        [AiOptional]
        public double? hoverRadius;

        /// <summary>
        /// Stroke width when hovered, default: 1
        /// </summary>
        [AiOptional]
        public double? hoverBorderWidth;

        #endregion//Line chart points


    }


    /// <summary>
    /// Options for filling the area under a line
    /// </summary>
    public sealed class ChartJsLineFillOptions
    {
        /// <summary>
        /// How to fill the area under the line.
        /// Valid:
        /// "origin"
        /// "start"
        /// "end"
        /// </summary>
        [AiOptional]
        public String target;

        /// <summary>
        /// If no color is set, the default color will be the background color of the chart.
        /// </summary>
        [AiOptional]
        public string above;

        /// <summary>
        /// If no color is set, the default color will be the background color of the chart.
        /// </summary>
        [AiOptional]
        public string below;
    }



}
