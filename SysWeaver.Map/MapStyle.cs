using System;
using System.Globalization;
using System.Text;
using SysWeaver.AI;

namespace SysWeaver.Map
{
    public class MapStyle
    {
        /// <summary>
        /// The CSS color to use for filling, ex: "#f00", "#00f802", "red", "rgba(0, 255, 0, 0.5)"
        /// </summary>
        [OpenAiOptional]
        public String FillColor;

        /// <summary>
        /// The CSS color to use for strokes, ex: "#f00", "#00f802", "red", "rgba(0, 255, 0, 0.5)"
        /// </summary>
        [OpenAiOptional]
        public String StrokeColor;

        /// <summary>
        /// The width in pixels of the stroke
        /// </summary>
        [OpenAiOptional]
        public double? StrokeWidth;

        /// <summary>
        /// [0, 1] The amount to extrude, as a fraction of the max extrusion.
        /// </summary>
        [OpenAiOptional]
        public double Extrude;

        /// <summary>
        /// The CSS color to use for the filling the extruded polygons, ex: "#f00", "#00f802", "red", "rgba(0, 255, 0, 0.5)"
        /// </summary>
        [OpenAiOptional]
        public String ExtrudeFillColor;

        /// <summary>
        /// The CSS color to use for stroking of the extruded polygons, ex: "#f00", "#00f802", "red", "rgba(0, 255, 0, 0.5)"
        /// </summary>
        [OpenAiIgnore]
        public String ExtrudeStrokeColor;

        /// <summary>
        /// The width in pixels to use for the strokes of the extruded polygons
        /// </summary>
        [OpenAiIgnore]
        public double? ExtrudeStrokeWidth;

        /// <summary>
        /// The text (title) to write on top of a region.
        /// {0} = Is replaced with the region name (typically country code).
        /// {1} = Is replaced with the country name if the region is a country code (else the region name is used).
        /// Use "\n" to insert a new line.
        /// </summary>
        [OpenAiOptional]
        public String Text = "{1}";

        /// <summary>
        /// The css color to use for text
        /// </summary>
        [OpenAiOptional]
        public String TextColor = "#fff";

        /// <summary>
        /// The css color to use for text the outline
        /// </summary>
        [OpenAiOptional]
        public String TextOutlineColor;

        /// <summary>
        /// The width of the text outline, less than zero means inherit or none
        /// </summary>
        [OpenAiOptional]
        public double? TextOutlineWidth;

        /// <summary>
        /// If set, specify the CSS font size for the text, ex: "100%", "14px"
        /// </summary>
        [OpenAiOptional]
        public String TextSize = "75%";

        /// <summary>
        /// The tooltip to show when hovering over a text.
        /// {0} = Is replaced with the region name (typically country code).
        /// {1} = Is replaced with the country name if the region is a country code (else the region name is used).
        /// Use "\n" to insert a new line.
        /// </summary>
        [OpenAiOptional]
        public String TextToolTip;


        /// <summary>
        /// The font family to use, ex "Verdana"
        /// </summary>
        [OpenAiOptional]
        public String FontFamily;

        /// <summary>
        /// The CSS font weight to use, ex: "bold", "light", "900"
        /// </summary>
        [OpenAiOptional]
        public String FontWeight;

        /// <summary>
        /// The CSS font style to use, ex: "italic", "underline"
        /// </summary>
        [OpenAiOptional]
        public String FontStyle;

        /// <summary>
        /// The tooltip to show when hovering over a region.
        /// {0} = Is replaced with the region name (typically country code).
        /// {1} = Is replaced with the country name if the region is a country code (else the region name is used).
        /// Use "\n" to insert a new line.
        /// </summary>
        [OpenAiOptional]
        public String ToolTip;

        public void WriteCss(StringBuilder sb, String className, MapStyleBase baseStyle)
        {
            sb.Append('.');
            sb.Append(className);
            sb.Append("{fill:");
            sb.Append(FillColor ?? baseStyle?.FillColor ?? "#444");
            sb.Append(";stroke:");
            sb.Append(StrokeColor ?? baseStyle?.StrokeColor ?? "#ddd");
            sb.Append(";stroke-width:");
            sb.Append((StrokeWidth ?? baseStyle?.StrokeWidth ?? 0.25).ToString("0.###", CultureInfo.InvariantCulture));
            sb.AppendLine("px;}");
        }


        public void WriteShadowCss(StringBuilder sb, String className, MapStyleBase baseStyle)
        {
            sb.Append('.');
            sb.Append(className);
            sb.Append("{fill:");
            var col = ExtrudeFillColor ?? baseStyle?.ExtrudeFillColor ?? HtmlColors.MakeTransparentLerp(FillColor ?? baseStyle?.FillColor ?? "#444", baseStyle.ShadowColor ?? "#000", baseStyle.ShadowFillStrength);
            sb.Append(col);
            sb.Append(";stroke:");
            col = ExtrudeStrokeColor ?? baseStyle?.ExtrudeStrokeColor ?? HtmlColors.MakeTransparentLerp(FillColor ?? baseStyle?.FillColor ?? "#444", baseStyle.ShadowColor ?? "#000", baseStyle.ShadowStrokeStrength);
            sb.Append(col);
            sb.Append(";stroke-width:");
            sb.Append((ExtrudeStrokeWidth ?? StrokeWidth ?? baseStyle?.ExtrudeStrokeWidth ?? baseStyle?.StrokeWidth ?? 0.25).ToString("0.###", CultureInfo.InvariantCulture));
            sb.AppendLine("px;}");
        }


        public void WriteTextCss(StringBuilder sb, String className, MapStyleBase baseStyle)
        {
            sb.Append('.');
            sb.Append(className);
            sb.Append("{dominant-baseline:middle;text-anchor:middle;fill:");
            sb.Append(TextColor ?? baseStyle?.TextColor ?? "#fff");
            var v = TextSize ?? baseStyle?.TextSize;
            if (!String.IsNullOrEmpty(v))
            {
                sb.Append(";font-size:");
                sb.Append(v);
            }
            v = FontFamily ?? baseStyle?.FontFamily;
            if (!String.IsNullOrEmpty(v))
            {
                sb.Append(";font-family:");
                sb.Append(v);
            }
            v = FontWeight ?? baseStyle?.FontWeight;
            if (!String.IsNullOrEmpty(v))
            {
                sb.Append(";font-weight:");
                sb.Append(v);
            }
            v = FontStyle ?? baseStyle?.FontStyle;
            if (!String.IsNullOrEmpty(v))
            {
                sb.Append(";font-style:");
                sb.Append(v);
            }
            v = TextOutlineColor ?? baseStyle?.TextOutlineColor;
            var w = TextOutlineWidth ?? baseStyle?.TextOutlineWidth ?? 0;
            if (w > 0)
            {
                if (!String.IsNullOrEmpty(v))
                {
                    sb.Append(";paint-order:stroke");
                    sb.Append(";stroke:");
                    sb.Append(v);
                    sb.Append(";stroke-width:");
                    sb.Append(w.ToString("0.###", CultureInfo.InvariantCulture)).Append("px");
                }
            }
            sb.Append(";}");
        }


    }

}
