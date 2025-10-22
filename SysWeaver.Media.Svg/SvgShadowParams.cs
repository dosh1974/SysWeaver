using System;
using System.Text;

namespace SysWeaver.Media
{
    public class SvgShadowParams : ISvgCss
    {
        public String ShadowColor = "rgba(0,0,0,0.25)";
        public double ShadowX = 8;
        public double ShadowY = 12;

        public String GetCss()
        {
            StringBuilder b = new StringBuilder();
            b.Append("\t\t\tfill: ");
            b.Append(String.IsNullOrEmpty(ShadowColor) ? "none" : ShadowColor);
            b.AppendLine(";");
            return b.ToString();
        }

        public override string ToString() => String.Concat("\t\t{\n", GetCss(), "\t\t}");

    }


}