using System;

namespace SysWeaver.Media
{
    /// <summary>
    /// The extrusion parameters of <see cref="SvgScene.AddPath"/> (the inherited fill / stroke style is used for the sides)
    /// </summary>
    public class SvgExtrudeParams : SvgColorStyle
    {
        /// <summary>
        /// Create extrusion parameters (stroke width 0.25, fill "#066")
        /// </summary>
        public SvgExtrudeParams()
        {
            StrokeWidth = 0.25;
            FillColor = "#066";
        }

        /// <summary>
        /// The x extrusion delta
        /// </summary>
        public double ExtrudeX = 3;
        /// <summary>
        /// The y extrusion delta
        /// </summary>
        public double ExtrudeY = 6;
        /// <summary>
        /// The side colors are interpolated from this color (sides parallel to the extrusion direction) to the <see cref="SvgColorStyle.FillColor"/> (sides perpendicular to it).
        /// Null or empty to use the fill color for all sides.
        /// </summary>
        public String ExtrudeFillLight = "#099";


    }


}