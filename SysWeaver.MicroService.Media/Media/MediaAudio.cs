using System;

namespace SysWeaver.MicroService.Media
{
    /// <summary>
    /// Audio media data
    /// </summary>
    public class MediaAudio : IMediaData, IEquatable<MediaAudio>
    {
        /// <summary>
        /// Relative audio volume
        /// </summary>
        [EditRange(0, 100)]
        [EditDefault(100)]
        [EditSlider(1)]
        [EditHideIf(nameof(MediaVideo.Stream), EditHideOps.Equals, (int)MediaVideoStreams.OnlyVideo)]
        public float Volume = 100;

        public virtual void Validate()
        {
            var v = Volume;
            Volume = v < 0 ? 0 : v > 100 ? 100 : v;
            var x = StartAt;
            StartAt = x < 0 ? 0 : x;
            x = EndAt;
            EndAt = x < 0 ? 0 : x;
        }

        /// <summary>
        /// Start playing at this time stamp (in seconds)
        /// </summary>
        [EditDefault(0)]
        [EditMin(0)]
        public double StartAt = 0;

        /// <summary>
        /// Stop playing at this time stamp (in seconds).
        /// If this value is less or equal to the start time, it won't stop until the end
        /// </summary>
        [EditDefault(0)]
        [EditMin(0)]
        public double EndAt = 0;

        public override int GetHashCode() => HashCode.Combine(Volume, StartAt, EndAt);

        public override bool Equals(object obj) => Equals(obj as MediaAudio);


        public bool Equals(MediaAudio other)
        {
            if (other == null)
                return false;
            if (Volume != other.Volume)
                return false;
            if (StartAt != other.StartAt)
                return false;
            if (EndAt != other.EndAt)
                return false;
            return true;
        }
    }
}
