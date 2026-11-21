using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A size (and format) that an uploaded image is saved as
    /// </summary>
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
        /// Name format, {0} is replaced with the original file name (without extension, the target extension is added).
        /// Must contain "{0}" and differ from "{0}", names must be unique within a repository.
        /// Defaults to "{0}", which <see cref="ImageRepoService"/> rejects, so every size must set a name (ex: "{0}_small").
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
        /// Defaults to "#000".
        /// </summary>
        public String Background = "#000";

        /// <summary>
        /// Force an exact size (the image is padded with the background color if it is fitted).
        /// Defaults to false.
        /// </summary>
        public bool ExactSize;


        /// <summary>
        /// The target extension (required, case sensitive), supported:
        /// ".png"
        /// ".jpg"
        /// Other values (or null) are not validated when the service starts, they make every upload to the repository fail.
        /// </summary>
        public String Ext;

        /// <summary>
        /// Target quality, clamped to [10, 100].
        /// Defaults to 80.
        /// </summary>
        public int Quality = 80;

        /// <summary>
        /// If true the images may be semi transparent, else transparency is removed using <see cref="Background"/>.
        /// Defaults to true.
        /// </summary>
        public bool AllowTransparent = true;
    }
}
