using System;

namespace SysWeaver.MicroService.Media
{
    /// <summary>
    /// Meta data for youtube hosted videos
    /// </summary>
    public sealed class MediaYouTube : MediaVideo, IEquatable<MediaYouTube>
    {

        public MediaYouTube()
        {
            Crop = new MediaCrop
            {
                MarginAsPercentage = true,
                MarginTopP = 21.875f,
                MarginBottomP = 21.875f,
            };
        }

        /// <summary>
        /// Width of the video (actual video will fit into this, try to match original aspect ratio, typically 16:9)
        /// </summary>
        [EditRange(144, 7680)]
        [EditDefault(1280)]
        [EditSlider(8)]
        [EditHideIf(nameof(Stream), EditHideOps.Equals, (int)MediaVideoStreams.OnlyAudio)]
        public int Width = 1280;

        /// <summary>
        /// Height of the video (actual video will fit into this, try to match original aspect ratio, typically 16:9)
        /// </summary>
        [EditRange(144, 7680)]
        [EditDefault(1280)]
        [EditSlider(8)]
        [EditHideIf(nameof(Stream), EditHideOps.Equals, (int)MediaVideoStreams.OnlyAudio)]
        public int Height = 1280;

        public override void Validate()
        {
            base.Validate();
            var w = Width;
            Width = w < 144 ? 144 : w > 7680 ? 7680 : w;
            var h = Height;
            Height = h < 144 ? 144 : h > 7680 ? 7680 : h;
        }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Width, Height);

        public override bool Equals(object obj) => Equals(obj as MediaYouTube);


        public bool Equals(MediaYouTube other)
        {
            if (!base.Equals(other))
                return false;
            if (Width != other.Width)
                return false;
            return Height == other.Height;
        }

    }
}
