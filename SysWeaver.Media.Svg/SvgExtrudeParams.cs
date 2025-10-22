using System;

namespace SysWeaver.Media
{
    public class SvgExtrudeParams : SvgColorStyle
    {
        public SvgExtrudeParams()
        {
            StrokeWidth = 0.25;
            FillColor = "#066";
        }

        public double ExtrudeX = 3;
        public double ExtrudeY = 6;
        public String ExtrudeFillLight = "#099";


    }


}