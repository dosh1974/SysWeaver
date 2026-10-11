using System;
using System.Text;

namespace SysWeaver.Media
{
    /// <summary>
    /// A stroke only style (css)
    /// </summary>
    public class SvgStrokeStyle : ISvgCss
    {
        /// <summary>
        /// The stroke color, null or empty to not set it
        /// </summary>
        public String StrokeColor;
        /// <summary>
        /// The stroke width (in px), zero or less to not set it
        /// </summary>
        public double StrokeWidth;
        /// <summary>
        /// The stroke line join, null or empty to not set it
        /// </summary>
        public String LineJoin = "round";

        /// <summary>
        /// Get the css declarations (one per line, indented with three tabs) for the set values: stroke-width, stroke-linejoin and stroke
        /// </summary>
        /// <returns>The css declarations, empty if nothing is set</returns>
        public String GetCss()
        {
            StringBuilder b = new StringBuilder();
            if (StrokeWidth > 0)
            {
                b.Append("\t\t\tstroke-width: ");
                b.Append(SvgTools.FullFormat(StrokeWidth));
                b.AppendLine("px;");
            }
            if (!String.IsNullOrEmpty(LineJoin))
            {
                b.Append("\t\t\tstroke-linejoin: ");
                b.Append(LineJoin);
                b.AppendLine(";");
            }
            var col = StrokeColor;
            if (!String.IsNullOrEmpty(col))
            {
                b.Append("\t\t\tstroke: ");
                b.Append(col);
                b.AppendLine(";");
            }
            return b.ToString();
        }

        /// <summary>
        /// The css declarations in a block (without a selector)
        /// </summary>
        /// <returns>The css block</returns>
        public override string ToString() => String.Concat("\t\t{\n", GetCss(), "\t\t}");

    }


}