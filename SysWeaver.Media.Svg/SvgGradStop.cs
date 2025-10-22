using System;

namespace SysWeaver.Media
{
    public sealed class SvgGradStop
    {

        public readonly double Pos;
        public readonly String Color;

        public SvgGradStop(double pos, string color)
        {
            Pos = pos;
            Color = color;
        }

        public String ToSvg(Func<double, String> fmt = null)
            => String.Concat("<stop offset='", (fmt ?? SvgTools.FullFormat)(Pos) + "%' stop-color='", Color, "' />");
        public override string ToString()
            => ToSvg();
    }


}