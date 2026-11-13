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
        /// Speed of the effect:
        /// time = RealTime * Speed + Offset
        /// </summary>
        [EditRange(-100, 100)]
        [EditDefault(1)]
        [EditSlider]
        [EditHideIf(nameof(Static), true)]
        public double Speed = 1;


        /// <summary>
        /// Time offset of the effect:
        /// time = RealTime * Speed + Offset
        /// </summary>
        [EditDefault(0)]
        [EditSlider]
        [EditHideIf(nameof(Static), true)]
        public double Offset = 0;

        /// <summary>
        /// If the effect is adjusting it's size dynamically and this is true, make sure that the rendered dimension doesn't exceed Width and Height
        /// </summary>
        [EditDefault(false)]
        [EditHideIf(nameof(AdaptiveSize), false)]
        public bool LimitSize = false;

        /// <summary>
        /// If true the density (DPI) is adjusted dynamically according to performance (off by default, as the player and the media editor have it)
        /// </summary>
        [EditDefault(false)]
        public bool DpiAdjust = false;

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
        /// Obsolete, not used: the player always uses WebGL2 when the browser has it (kept so stored settings still read)
        /// </summary>
        [EditHide]
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
        /// Optional id of a HTML element to read the scroll parameters from.
        /// </summary>
        public String ScrollId = null;

        /// <summary>
        /// Optional id of a HTML element to read the mouse parameters from.
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

        /// <summary>
        /// The live controls of an Effect Weaver effect (the effect reads them from iMouse x, y, z and w, see its "// iMouse.x = Name | ..." comments): up to 4 values in [0, 1].
        /// Null (or a missing value) uses the effect's defaults.
        /// </summary>
        public double[] Controls;

        public virtual void Validate()
        {
            var c = Controls;
            if (c != null)
            {
                if (c.Length > 4)
                    Array.Resize(ref c, 4);
                for (int i = 0; i < c.Length; ++i)
                {
                    var x = c[i];
                    c[i] = double.IsNaN(x) ? 0.5 : x < 0 ? 0 : x > 1 ? 1 : x;
                }
                Controls = c;
            }
            var v = Speed;
            Speed = v < -100 ? -100 : v > 100 ? 100 : v;
            var w = Width;
            Width = w < 144 ? 144 : w > 7680 ? 7680 : w;
            var h = Height;
            Height = h < 144 ? 144 : h > 7680 ? 7680 : h;
            var d = DpiScale;
            DpiScale = d < 0.1 ? 0.1 : d > 1.0 ? 1.0 : d;
        }

        public override int GetHashCode() => ObjectHash.Mix(Speed, Offset, Width, Height, DpiScale, Texture, ScrollId, MouseId,
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
            if (Offset != other.Offset)
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
            if (!((Controls ?? []).AsSpan().SequenceEqual(other.Controls ?? [])))
                return false;
            return true;
        }

    }
}
