using System;

namespace SysWeaver.Media
{
    /// <summary>
    /// A style that can be converted to css declarations (used as a css class by <see cref="SvgScene.AddStyle"/>)
    /// </summary>
    public interface ISvgCss
    {
        /// <summary>
        /// Get the css declarations (without a selector or braces)
        /// </summary>
        /// <returns>The css declarations, may be empty</returns>
        String GetCss();

    }


}