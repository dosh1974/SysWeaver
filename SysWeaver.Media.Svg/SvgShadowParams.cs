using System;
using System.Text;

namespace SysWeaver.Media
{
    /// <summary>
    /// The drop shadow parameters of <see cref="SvgScene.AddPath"/>
    /// </summary>
    public class SvgShadowParams : ISvgCss
    {
        /// <summary>
        /// The shadow color, null for no shadow
        /// </summary>
        public String ShadowColor = "rgba(0,0,0,0.25)";
        /// <summary>
        /// The x offset of the shadow
        /// </summary>
        public double ShadowX = 8;
        /// <summary>
        /// The y offset of the shadow
        /// </summary>
        public double ShadowY = 12;

        /// <summary>
        /// Get the css declaration of the shadow fill (indented with three tabs), "none" if the color is null or empty
        /// </summary>
        /// <returns>The css declaration</returns>
        public String GetCss()
        {
            StringBuilder b = new StringBuilder();
            b.Append("\t\t\tfill: ");
            b.Append(String.IsNullOrEmpty(ShadowColor) ? "none" : ShadowColor);
            b.AppendLine(";");
            return b.ToString();
        }

        /// <summary>
        /// The css declarations in a block (without a selector)
        /// </summary>
        /// <returns>The css block</returns>
        public override string ToString() => String.Concat("\t\t{\n", GetCss(), "\t\t}");

    }


}