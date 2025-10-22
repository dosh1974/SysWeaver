using System;

namespace SysWeaver.MicroService.Media
{
    /// <summary>
    /// Meta data for images
    /// </summary>
    public class MediaImage : IMediaData, IEquatable<MediaImage>
    {
        /// <summary>
        /// The duration in seconds to show this image before switching to the next visuals.
        /// If duration is zero, the duration specified in the quizz will be used (default).
        /// </summary>
        [EditRange(0, MediaLimits.MaxImageDuration)]
        [EditSlider(1)]
        [EditDefault(0)]
        public int Duration;

        /// <summary>
        /// Optional cropping
        /// </summary>
        [EditAllowNull]
        [EditDefault(null)]
        [EditOrder(-1)]
        public MediaCrop Crop;


        /// <summary>
        /// If the image is transparent, set this to true to hide any borders
        /// </summary>
        [EditDefault(false)]
        public bool Transparent;


        /// <summary>
        /// An optional realtime effect to use for this image
        /// </summary>
        [EditAllowNull]
        [EditDefault(null)]
        public string Effect;

        /// <summary>
        /// Effect parameters
        /// </summary>
        [EditHideIf(nameof(Effect), false)]
        public MediaEffect EffectParams;

        public virtual void Validate()
        {
            Crop?.Validate();
            var v = Duration;
            Duration = v < 0 ? 0 : v > MediaLimits.MaxImageDuration ? MediaLimits.MaxImageDuration : v;
        }

        public override int GetHashCode() => HashCode.Combine(Duration.GetHashCode(), Crop, Effect, EffectParams, Transparent);

        public override bool Equals(object obj) => Equals(obj as MediaImage);

        public bool Equals(MediaImage other)
        {
            if (other == null)
                return false;
            var c = Crop;
            if (c == null)
                if (other.Crop != null)
                    return false;
                else
                if (!c.Equals(other.Crop))
                    return false;
            if (Effect != other.Effect)
                return false;

            var e = EffectParams;
            if (e == null)
                if (other.EffectParams != null)
                    return false;
                else
                if (!e.Equals(other.EffectParams))
                    return false;
            if (Transparent != other.Transparent)
                return false;
            return Duration == other.Duration;
        }

    }
}
