using System;

namespace SysWeaver.MicroService.Media
{
    public class MediaEffect : IMediaData, IEquatable<MediaEffect>
    {
        /// <summary>
        /// Width of the effect (an effect can adjust to any width, this is what's being used for reference)
        /// </summary>
        [EditRange(144, 7680)]
        [EditDefault(1920)]
        [EditSlider(8)]
        public int Width = 1920;
        /// <summary>
        /// Height of the effect  (an effect can adjust to any width, this is what's being used for reference)
        /// </summary>
        [EditRange(144, 7680)]
        [EditDefault(1080)]
        [EditSlider(8)]
        public int Height = 1080;

        /// <summary>
        /// Speed of the effect
        /// </summary>
        [EditRange(0.01, 100)]
        [EditDefault(1)]
        [EditSlider]
        [EditHideIf(nameof(Static), true)]
        public double Speed = 1;

        /// <summary>
        /// If the effect is adjusting it's size dynamically and this is true, make sure that the rendered dimension doesn't exceed Width and Height
        /// </summary>
        [EditDefault(false)]
        [EditHideIf(nameof(AdaptiveSize), false)]
        public bool LimitSize = false;

        /// <summary>
        /// If true the density (DPI) is adjusted dynamically according to performance
        /// </summary>
        [EditDefault(true)]
        public bool DpiAdjust = true;

        /// <summary>
        /// Adjust the DPI of this effect
        /// </summary>
        [EditRange(0.1, 1)]
        [EditDefault(1)]
        [EditSlider(0.05)]
        public double DpiScale = 1.0;

        /// <summary>
        /// If true the effect isn't re-rendered every frame, only rendered the first time used or when the size changes (aka a static image is produced)
        /// </summary>
        [EditDefault(false)]
        public bool Static = false;

        /// <summary>
        /// If the effect is transparent, set this to true to hide any borders
        /// </summary>
        [EditDefault(false)]
        public bool Transparent;

        /// <summary>
        /// If true the effect's width and height will change dynamically to match the container
        /// </summary>
        [EditDefault(false)]
        public bool AdaptiveSize = true;

        /// <summary>
        /// If a texture is used and this is true, Wrap U instead of clamp.
        /// </summary>
        public bool WrapU = false;

        /// <summary>
        /// If a texture is used and this is true, Wrap V instead of clamp.
        /// </summary>
        public bool WrapV = false;

        /// <summary>
        /// Optional image file to use as a texture
        /// </summary>
        public String Texture = null;

        /// <summary>
        /// If true, use WebGL2 
        /// </summary>
        public bool WebGL2 = false;

        /// <summary>
        /// If true and a image to be used as a texture is supplied, generate mip-maps for it
        /// </summary>
        public bool MipMap = false;

        /// <summary>
        /// If true enable antialiasing (MSAA)
        /// </summary>
        public bool AntiAlias = false;

        /// <summary>
        /// Optional id of a HTML element to read the scroll paramaters from.
        /// </summary>
        public String ScrollId = null;

        /// <summary>
        /// Optional id of a HTML element to read the mouse paramaters from.
        /// </summary>
        public String MouseId = null;

        /// <summary>
        /// If true and an image is used as a texture, set the effect size to the image size
        /// </summary>
        public bool ImageSize = false;

        /// <summary>
        /// If true, dump effect variables to the console log
        /// </summary>
        public bool ShowVars = false;

        /// <summary>
        /// Effect specific property object as a json string
        /// </summary>
        [EditMultiline]
        public string FxProps;

        public virtual void Validate()
        {
            var v = Speed;
            Speed = v < 0.01 ? 0.01 : v > 100 ? 100 : v;
            var w = Width;
            Width = w < 144 ? 144 : w > 7680 ? 7680 : w;
            var h = Height;
            Height = h < 144 ? 144 : h > 7680 ? 7680 : h;
            var d = DpiScale;
            DpiScale = d < 0.1 ? 0.1 : d > 1.0 ? 1.0 : d;
        }

        public override int GetHashCode() => HashCode.Combine(Speed, Width, Height, DpiScale, Texture, ScrollId, MouseId,
            (LimitSize ? 1 : 0) |
            (DpiAdjust ? 2 : 0) |
            (Static ? 4 : 0) |
            (AdaptiveSize ? 8 : 0) |
            (Transparent ? 16 : 0) |
            (WrapU ? 32 : 0) |
            (WrapV ? 64 : 0) |
            (WebGL2 ? 128 : 0) |
            (MipMap ? 256 : 0) |
            (AntiAlias ? 512 : 0) |
            (ImageSize ? 1024 : 0) |
            0);

        public override bool Equals(object obj) => Equals(obj as MediaEffect);

        public bool Equals(MediaEffect other)
        {
            if (other == null)
                return false;
            if (Width != other.Width)
                return false;
            if (Height != other.Height)
                return false;
            if (Speed != other.Speed)
                return false;
            if (LimitSize != other.LimitSize)
                return false;
            if (DpiAdjust != other.DpiAdjust)
                return false;
            if (DpiScale != other.DpiScale)
                return false;
            if (Static != other.Static)
                return false;
            if (Transparent != other.Transparent)
                return false;
            if (AdaptiveSize != other.AdaptiveSize)
                return false;
            if (WrapU != other.WrapU)
                return false;
            if (WrapV != other.WrapV)
                return false;
            if (Texture != other.Texture)
                return false;
            if (WebGL2 != other.WebGL2)
                return false;
            if (MipMap != other.MipMap)
                return false;
            if (AntiAlias != other.AntiAlias)
                return false;
            if (ScrollId != other.ScrollId)
                return false;
            if (MouseId != other.MouseId)
                return false;
            if (ImageSize != other.ImageSize)
                return false;
            if (ShowVars != other.ShowVars)
                return false;
            if (FxProps != other.FxProps)
                return false;
            return true;
        }

    }
}
