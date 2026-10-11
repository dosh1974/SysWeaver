namespace SysWeaver.Media
{
    /// <summary>
    /// The parameters of <see cref="SvgPath.GetSvgTextPaths(string, SvgTextPathParams)"/>
    /// </summary>
    public class SvgTextPathParams
    {
        /// <summary>
        /// The font to use (required)
        /// </summary>
        public SvgFont Font;
        /// <summary>
        /// The font size, used if the text is not fitted
        /// </summary>
        public double Size = 10;
        /// <summary>
        /// The width of the area to fit the text into, the text is only fitted if both the width and height are greater than zero
        /// </summary>
        public double FitWidth;
        /// <summary>
        /// The height of the area to fit the text into, the text is only fitted if both the width and height are greater than zero
        /// </summary>
        public double FitHeight;
        /// <summary>
        /// The left position of the fit area
        /// </summary>
        public double MarginX;
        /// <summary>
        /// The top position of the fit area
        /// </summary>
        public double MarginY;
        /// <summary>
        /// The maximum number of decimals of the coordinates
        /// </summary>
        public int MaxDecimals = 3;
    }


}