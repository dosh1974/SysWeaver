using System;

namespace SysWeaver.Media
{
    /// <summary>
    /// The parameters of a text, see <see cref="SvgTools.GetSvgImageFromText"/> and <see cref="SvgScene.AddText"/>
    /// </summary>
    public sealed class SvgTextParams : Svg3dParams
    {
        /// <summary>
        /// The font to use (required)
        /// </summary>
        public SvgFont Font;
        /// <summary>
        /// The width of the area that the text is fitted into
        /// </summary>
        public double FitWidth = 224;
        /// <summary>
        /// The height of the area that the text is fitted into
        /// </summary>
        public double FitHeight = 212;
        /// <summary>
        /// The left position of the area
        /// </summary>
        public double OffsetX = 16;
        /// <summary>
        /// The top position of the area
        /// </summary>
        public double OffsetY = 18;
        /// <summary>
        /// Not used
        /// </summary>
        public SvgTextPathParams Text;
        /// <summary>
        /// An optional background color (used by <see cref="SvgTools.GetSvgImageFromText"/>)
        /// </summary>
        public String BackgroundColor;
    }


}