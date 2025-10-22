using System;

namespace SysWeaver.Media
{
    public sealed class SvgTextParams : Svg3dParams
    {
        public SvgFont Font;
        public double FitWidth = 224;
        public double FitHeight = 212;
        public double OffsetX = 16;
        public double OffsetY = 18;
        public SvgTextPathParams Text;
        public String BackgroundColor;
    }


}