using System;
using SysWeaver.MicroService.Media;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A request for a screenshot of a sound: drawn by an audio effect (Options.Effect, else the service's AudioEffect, "Led_Spectrum"), taken
    /// in the middle of the sound (its duration × 0.5; Pos is used when the duration isn't known)
    /// </summary>
    public sealed class GetMediaAudioRequest : GetMediaRequest
    {
        internal override int Type => (int)MediaTypes.Audio;

        internal override Object Params => Options;

        /// <summary>The position as a part of the duration: the middle</summary>
        internal override double? RelPos => 0.5;

        /// <summary>The sound is fetched and decoded by the page (the offline analysis)</summary>
        internal override (Func<String> Get, Action<String> Set)[] TextureUrls => [(() => Url, v => Url = v)];

        /// <summary>
        /// Audio options (Effect: the effect that draws the sound, null = the service's AudioEffect; EffectParams: its settings)
        /// </summary>
        public MediaAudio Options;
    }
}
