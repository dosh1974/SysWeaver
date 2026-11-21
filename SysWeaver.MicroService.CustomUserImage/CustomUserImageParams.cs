using System;

namespace SysWeaver.MicroService
{


    /// <summary>
    /// Parameters for the <see cref="CustomUserImageService"/>
    /// </summary>
    public sealed class CustomUserImageParams
    {
        /// <summary>
        /// The repository key (must be matched when clients upload images).
        /// If null or empty, "UserImage" is used.
        /// Defaults to "UserImage".
        /// </summary>
        public String Key = "UserImage";

        /// <summary>
        /// If true, all applications on the machine will share the same user images.
        /// Defaults to false.
        /// </summary>
        public bool SystemWide;

        /// <summary>
        /// If true the images may be semi transparent (will be fitted into the square instead of filled into).
        /// Always false if the format is "jpeg".
        /// Defaults to false.
        /// </summary>
        public bool AllowTransparent = false;

        /// <summary>
        /// The format to use for images, supported are:
        /// "png"
        /// "jpeg"
        /// Any other value is treated as "png".
        /// Defaults to "png".
        /// </summary>
        public String Format = "png";

        /// <summary>
        /// Background color to use when transparent images isn't allowed.
        /// http://www.imagemagick.org/script/color.php
        /// Defaults to "#000" (also used if the color can't be parsed).
        /// </summary>
        public String BackgroundColor = "#000";

        /// <summary>
        /// The supported sizes in pixels (always square), zero is ignored and duplicates are removed.
        /// If negative the absolute value is the size and <see cref="PublicAuth"/> is used, else <see cref="PrivateAuth"/> is used.
        /// If null or empty, the default is used.
        /// Defaults to [-64, 512].
        /// </summary>
        public int[] Sizes = [-64, 512];

        /// <summary>
        /// Auth required to access images (with a positive size), a comma separated list of tokens: null = no auth required, "" = any logged in user.
        /// Defaults to "" (any logged in user).
        /// </summary>
        public String PrivateAuth = "";

        /// <summary>
        /// Auth required to access images with a negative size, a comma separated list of tokens: null = no auth required, "" = any logged in user.
        /// Defaults to null (no auth required).
        /// </summary>
        public String PublicAuth;

    }
}
