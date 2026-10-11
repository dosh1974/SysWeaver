using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Media
{

    /// <summary>
    /// Svg helpers: number formatting (invariant culture) and simple complete svg documents from text, n-gons and paths
    /// </summary>
    public static class SvgTools
    {

        /// <summary>
        /// Reads a file, used by the composition templates for file names starting with '$'
        /// </summary>
        /// <param name="filename">The file name (without the '$')</param>
        /// <returns>The file data (empty if not found) and the mime type of the data</returns>
        public delegate Task<ValueTuple<ReadOnlyMemory<Byte>, String>> FileReader(String filename);

        /// <summary>
        /// The format provider used for all numbers (the invariant culture)
        /// </summary>
        public static readonly IFormatProvider Format = CultureInfo.InvariantCulture;

        /// <summary>
        /// Get a number formatter that rounds to at most <paramref name="decimalCount"/> decimals without trailing zeros (format "0.##..", invariant culture).
        /// Note that small negative values that are rounded to zero are formatted as "-0".
        /// </summary>
        /// <param name="decimalCount">The maximum number of decimals, 0 rounds to integers</param>
        /// <returns>A formatter function</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="decimalCount"/> is negative</exception>
        public static Func<double, String> GetFormat(int decimalCount)
        {
            var t = "0." + new String('#', decimalCount);
            var f = Format;
            return v => v.ToString(t, f);
        }

        /// <summary>
        /// A number formatter with at most 12 decimals, see <see cref="GetFormat(int)"/>
        /// </summary>
        public static readonly Func<double, String> FullFormat = GetFormat(12);

        /// <summary>
        /// Get the number formatter with at most 12 decimals (<see cref="FullFormat"/>)
        /// </summary>
        /// <returns><see cref="FullFormat"/></returns>
        public static Func<double, String> GetFormat() => FullFormat;


        /// <summary>
        /// Create a complete svg document with some text (converted to paths and fitted into FitWidth x FitHeight at OffsetX, OffsetY),
        /// rendered with the face, extrusion and shadow styles of <paramref name="p"/>.
        /// The document size is (FitWidth + 2 * OffsetX) x (FitHeight + 2 * OffsetY).
        /// </summary>
        /// <param name="text">The text</param>
        /// <param name="p">The parameters (<see cref="SvgTextParams.Font"/> must be set)</param>
        /// <returns>The svg document</returns>
        /// <exception cref="ArgumentNullException">The font is null</exception>
        public static String GetSvgImageFromText(String text, SvgTextParams p)
        {
            //  Text
            var paths = SvgPath.GetSvgTextPaths(text, new SvgTextPathParams
            {
                Font = p.Font,
                FitWidth = p.FitWidth,
                FitHeight = p.FitHeight,
                MarginX = p.OffsetX,
                MarginY = p.OffsetY,
                MaxDecimals = p.MaxDecimals,
            });
            var path = SvgPath.JoinPaths(paths);
            var tw = p.FitWidth + p.OffsetX * 2;
            var th = p.FitHeight + p.OffsetY * 2;
            var svg = new SvgScene(tw, th);
            var fmt = FullFormat;
            if (!String.IsNullOrEmpty(p.BackgroundColor))
                svg.AddGeometry($"\t<rect width='{fmt(tw)}' height='{fmt(th)}' fill='{p.BackgroundColor}' />");
            svg.AddPath(path, p);
            return svg.ToSvg();
        }

        /// <summary>
        /// Create a complete svg document with a regular n-gon (see <see cref="SvgPath.GetNGonPath"/>),
        /// rendered with the face, extrusion and shadow styles of <paramref name="p"/>.
        /// The document size is (Size + 2 * OffsetX) x (Size + 2 * OffsetY).
        /// </summary>
        /// <param name="n">The number of vertices, must be at least 3</param>
        /// <param name="p">The parameters</param>
        /// <returns>The svg document</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is less than 3</exception>
        public static String GetSvgNCon(int n, SvgNgonParams p)
        {
            var path = SvgPath.GetNGonPath(n, p.Size, p.MaxDecimals, p.AngleOffset, p.OffsetX, p.OffsetY);
            var tw = p.Size + p.OffsetX * 2;
            var th = p.Size + p.OffsetY * 2;
            var svg = new SvgScene(tw, th);
            var fmt = FullFormat;
            if (!String.IsNullOrEmpty(p.BackgroundColor))
                svg.AddGeometry($"\t<rect width='{fmt(tw)}' height='{fmt(th)}' fill='{p.BackgroundColor}' />");
            svg.AddPath(path, p);
            return svg.ToSvg();
        }

        /// <summary>
        /// Create a complete svg document with a single filled path.
        /// If the width or height is zero or less, it's computed from the path bounds (the bounds size plus twice the positive min coordinate, i.e. the same margin on both sides).
        /// Nothing is drawn if the fill color is null or empty.
        /// </summary>
        /// <param name="path">The path data</param>
        /// <param name="p">The fill and stroke style, null to use the defaults of <see cref="SvgColorStyle"/></param>
        /// <param name="width">The view box width, zero or less to compute it from the path</param>
        /// <param name="height">The view box height, zero or less to compute it from the path</param>
        /// <returns>The svg document (lines separated by <see cref="Environment.NewLine"/>)</returns>
        /// <exception cref="FormatException">The size is computed and the path contains an unsupported command or an invalid number</exception>
        public static String GetSvgFromPath(String path, SvgColorStyle p = null, double width = 0, double height = 0)
        {
            p = p ?? new SvgColorStyle();
            if ((width <= 0) || (height <= 0))
            {
                var size = new SvgMinMaxState();
                SvgPath.UpdateMinMax(size, path);
                if (width <= 0)
                {
                    var mx = Math.Max(0, size.MinX);
                    width = size.Width + mx * 2;
                }
                if (height <= 0)
                {
                    var my = Math.Max(0, size.MinY);
                    height = size.Height + my * 2;
                }
            }
            StringBuilder svg = new StringBuilder();
            var fmt = FullFormat;
            svg.AppendLine($"<svg viewBox='0 0 {fmt(width)} {fmt(height)}' xmlns='http://www.w3.org/2000/svg' version='1.1'>");
            //  Main face
            if (!String.IsNullOrEmpty(p.FillColor))
            {
                if (p.StrokeWidth > 0)
                {
                    var sfmt = GetFormat(10);
                    svg.AppendLine($"\t<path fill='{p.FillColor}' stroke='{p.StrokeColor ?? p.FillColor}' stroke-width='{sfmt(p.StrokeWidth)}' stroke-linejoin='round' d='{path}' />");
                }
                else
                    svg.AppendLine($"\t<path fill='{p.FillColor}' d='{path}' />");

            }
            svg.AppendLine("</svg>");
            return svg.ToString();
        }

    }


}