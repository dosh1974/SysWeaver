using System;

namespace SysWeaver.MicroService.Media
{
    /// <summary>
    /// Video media data
    /// </summary>
    public class MediaVideo : MediaAudio, IEquatable<MediaVideo>
    {
        /// <summary>
        /// What streams to use
        /// </summary>
        public MediaVideoStreams Stream;


        /// <summary>
        /// Optional cropping
        /// </summary>
        [EditAllowNull]
        [EditDefault(null)]
        [EditHideIf(nameof(Stream), EditHideOps.Equals, (int)MediaVideoStreams.OnlyAudio)]
        [EditOrder(-1)]
        public MediaCrop Crop;

        /// <summary>
        /// If the video is transparent, set this to true to hide any borders
        /// </summary>
        [EditHideIf(nameof(Stream), EditHideOps.Equals, (int)MediaVideoStreams.OnlyAudio)]
        [EditDefault(false)]
        public bool Transparent;

        public override void Validate()
        {
            base.Validate();
            Crop?.Validate();
            var s = Stream;
            if (s < MediaVideoStreams.VideoAndAudio || s > MediaVideoStreams.OnlyAudio)
                throw new Exception("Invalid stream value!");
        }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), (int)Stream, Crop?.GetHashCode(), Transparent);

        public override bool Equals(object obj) => Equals(obj as MediaVideo);


        public bool Equals(MediaVideo other)
        {
            if (!base.Equals(other))
                return false;
            var c = Crop;
            if (c == null)
                if (other.Crop != null)
                    return false;
                else
                if (!c.Equals(other.Crop))
                    return false;
            if (Transparent != other.Transparent)
                return false;
            return Stream == other.Stream;
        }


    }
}
