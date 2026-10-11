using System;

namespace SysWeaver.Media
{
    /// <summary>
    /// A gradient stop
    /// </summary>
    public sealed class SvgGradStop
    {

        /// <summary>
        /// [0, 100] The position as a percentage
        /// </summary>
        public readonly double Pos;
        /// <summary>
        /// The css color
        /// </summary>
        public readonly String Color;

        /// <summary>
        /// Create a gradient stop
        /// </summary>
        /// <param name="pos">[0, 100] The position as a percentage</param>
        /// <param name="color">The css color</param>
        public SvgGradStop(double pos, string color)
        {
            Pos = pos;
            Color = color;
        }

        /// <summary>
        /// Get the svg stop element, ex: "&lt;stop offset='50%' stop-color='#f00' /&gt;"
        /// </summary>
        /// <param name="fmt">The number formatter for the position, null to use <see cref="SvgTools.FullFormat"/></param>
        /// <returns>The svg stop element</returns>
        public String ToSvg(Func<double, String> fmt = null)
            => String.Concat("<stop offset='", (fmt ?? SvgTools.FullFormat)(Pos) + "%' stop-color='", Color, "' />");

        /// <summary>
        /// Same as <see cref="ToSvg(Func{double, string})"/> with the default formatter
        /// </summary>
        /// <returns>The svg stop element</returns>
        public override string ToString()
            => ToSvg();
    }


}