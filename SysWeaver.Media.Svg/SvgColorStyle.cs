using System;
using System.Text;

namespace SysWeaver.Media
{
    /// <summary>
    /// A fill and stroke style (css)
    /// </summary>
    public class SvgColorStyle : ISvgCss
    {
        /// <summary>
        /// The fill color (any css color or "url(#id)"), null or empty for no fill ("none")
        /// </summary>
        public String FillColor = "#0ff";
        /// <summary>
        /// The stroke color, null to use the <see cref="FillColor"/>
        /// </summary>
        public String StrokeColor;
        /// <summary>
        /// The stroke width (in px), zero or less for no stroke
        /// </summary>
        public double StrokeWidth;
        /// <summary>
        /// The stroke line join, null or empty to not set it
        /// </summary>
        public String LineJoin = "round";

        /// <summary>
        /// Get the css declarations (one per line, indented with three tabs): fill, and if the stroke width is greater than zero and there is a stroke color:
        /// stroke, stroke-linejoin and stroke-width.
        /// If there is no stroke width and the stroke color is "none", "stroke-width: 0px" is added.
        /// </summary>
        /// <returns>The css declarations</returns>
        public String GetCss()
        {
            StringBuilder b = new StringBuilder();
            b.Append("\t\t\tfill: ");
            b.Append(String.IsNullOrEmpty(FillColor) ? "none" : FillColor);
            b.AppendLine(";");
            if (StrokeWidth > 0)
            {
                var col = StrokeColor ?? FillColor;
                if (!String.IsNullOrEmpty(col))
                {
                    b.Append("\t\t\tstroke: ");
                    b.Append(col);
                    b.AppendLine(";");
                    if (!String.IsNullOrEmpty(LineJoin))
                    {
                        b.Append("\t\t\tstroke-linejoin: ");
                        b.Append(LineJoin);
                        b.AppendLine(";");
                    }
                    b.Append("\t\t\tstroke-width: ");
                    b.Append(SvgTools.FullFormat(StrokeWidth));
                    b.AppendLine("px;");
                }
            }else
            {
                if (StrokeColor == "none")
                    b.AppendLine("\t\t\tstroke-width: 0px;");
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