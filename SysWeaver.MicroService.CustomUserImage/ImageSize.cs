using System;

namespace SysWeaver.MicroService
{
    public sealed class ImageSize
    {
        /// <summary>
        /// Width in pixels.
        /// If one of Width or Height is zero (or less) that will be replaced with the uniform scaled.
        /// If both are zero, the original width and height will be used.
        /// </summary>
        public int Width;
        /// <summary>
        /// Height in pixels.
        /// If one of Width or Height is zero (or less) that will be replaced with the uniform scaled.
        /// If both are zero, the original width and height will be used.
        /// </summary>
        public int Height;

        /// <summary>
        /// Name format, {0} is replaced with the original file name.
        /// </summary>
        public String Name = "{0}";

        /// <summary>
        /// If true the source image will be fitted into the target rectangle.
        /// Default is to fill the target rectangle (clip)
        /// </summary>
        public bool Fit;

        /// <summary>
        /// Background color to use when transparent images isn't allowed.
        /// http://www.imagemagick.org/script/color.php
        /// </summary>
        public String Background = "#000";

        /// <summary>
        /// Force an exact size
        /// </summary>
        public bool ExactSize;


        /// <summary>
        /// The target extension, supported:
        /// ".png"
        /// ".jpg"
        /// </summary>
        public String Ext;

        /// <summary>
        /// Target quality
        /// </summary>
        public int Quality = 80;

        /// <summary>
        /// If true the images may be semi transparent (will be fitted into the square instead of filled into).
        /// </summary>
        public bool AllowTransparent = true;
    }
}
