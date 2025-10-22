using System;

namespace SysWeaver.MicroService
{


    public sealed class CustomUserImageParams
    {
        /// <summary>
        /// The repository key (must be matched when clients upload images).
        /// </summary>
        public String Key = "UserImage";

        /// <summary>
        /// If true, all applications on the machine will share the same user images
        /// </summary>
        public bool SystemWide;

        /// <summary>
        /// If true the images may be semi transparent (will be fitted into the square instead of filled into).
        /// </summary>
        public bool AllowTransparent = true;

        /// <summary>
        /// The format to use for images, supported are:
        /// "png"
        /// </summary>
        public String Format = "png";

        /// <summary>
        /// Background color to use when transparent images isn't allowed.
        /// http://www.imagemagick.org/script/color.php
        /// </summary>
        public String BackgroundColor = "#000";

        /// <summary>
        /// The supported sizes in pixels (always square)
        /// </summary>
        public int[] Sizes = [64, 512];


    }
}
