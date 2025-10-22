using System;
using System.Text;

namespace SysWeaver.Media
{
    public class SvgColorStyle : ISvgCss
    {
        public String FillColor = "#0ff";
        public String StrokeColor;
        public double StrokeWidth;
        public String LineJoin = "round";

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

        public override string ToString() => String.Concat("\t\t{\n", GetCss(), "\t\t}");

    }


}