using System;
using System.Globalization;
using System.Text;

namespace SysWeaver.Media
{


    public static class SvgTools
    {
        public static readonly IFormatProvider Format = CultureInfo.InvariantCulture;
        public static Func<double, String> GetFormat(int decimalCount)
        {
            var t = "0." + new String('#', decimalCount);
            var f = Format;
            return v => v.ToString(t, f);
        }

        public static readonly Func<double, String> FullFormat = GetFormat(12);
        public static Func<double, String> GetFormat() => FullFormat;


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
            if (!String.IsNullOrEmpty(p.BackgroundColor))
                svg.AddGeometry($"\t<rect width='{tw}' height='{th}' fill='{p.BackgroundColor}' />");
            svg.AddPath(path, p);
            return svg.ToSvg();
        }

        public static String GetSvgNCon(int n, SvgNgonParams p)
        {
            var path = SvgPath.GetNGonPath(n, p.Size, p.MaxDecimals, p.AngleOffset, p.OffsetX, p.OffsetY);
            var tw = p.Size + p.OffsetX * 2;
            var th = p.Size + p.OffsetY * 2;
            var svg = new SvgScene(tw, th);
            if (!String.IsNullOrEmpty(p.BackgroundColor))
                svg.AddGeometry($"\t<rect width='{tw}' height='{th}' fill='{p.BackgroundColor}' />");
            svg.AddPath(path, p);
            return svg.ToSvg();
        }
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

            svg.AppendLine($"<svg viewBox='0 0 {width} {height}' xmlns='http://www.w3.org/2000/svg' version='1.1'>");
            //  Main face
            if (!String.IsNullOrEmpty(p.FillColor))
            {
                if (p.StrokeWidth > 0)
                {
                    var fmt = GetFormat(10);
                    svg.AppendLine($"\t<path fill='{p.FillColor}' stroke='{p.StrokeColor ?? p.FillColor}' stroke-width='{fmt(p.StrokeWidth)}' stroke-linejoin='round' d='{path}' />");
                }
                else
                    svg.AppendLine($"\t<path fill='{p.FillColor}' d='{path}' />");

            }
            svg.AppendLine("</svg>");
            return svg.ToString();
        }

    }


}