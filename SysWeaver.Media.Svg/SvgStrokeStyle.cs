using System;
using System.Text;

namespace SysWeaver.Media
{
    public class SvgStrokeStyle : ISvgCss
    {
        public String StrokeColor;
        public double StrokeWidth;
        public String LineJoin = "round";

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

        public override string ToString() => String.Concat("\t\t{\n", GetCss(), "\t\t}");

    }


}