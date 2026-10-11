using System;

namespace SysWeaver.Media
{
    /// <summary>
    /// The parameters of a regular n-gon, see <see cref="SvgTools.GetSvgNCon"/> and <see cref="SvgScene.AddNGon"/>
    /// </summary>
    public sealed class SvgNgonParams : Svg3dParams
    {
        /// <summary>
        /// Rotation in degrees (clockwise)
        /// </summary>
        public double AngleOffset = 0;
        /// <summary>
        /// The size of the square that the n-gon is fitted into
        /// </summary>
        public int Size = 208;
        /// <summary>
        /// The left position of the square
        /// </summary>
        public int OffsetX = 24;
        /// <summary>
        /// The top position of the square
        /// </summary>
        public int OffsetY = 24;
        /// <summary>
        /// An optional background color (used by <see cref="SvgTools.GetSvgNCon"/>)
        /// </summary>
        public String BackgroundColor;
    }


}