using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="ThumbnailWebService"/>
    /// </summary>
    public sealed class ThumbnailWebParams
    {
        /// <summary>
        /// Number of seconds that clients may cache the images.
        /// Defaults to 30.
        /// </summary>
        public int ClientCacheDuration = 30;
        /// <summary>
        /// Number of seconds that the server caches the images.
        /// Defaults to 25.
        /// </summary>
        public int RequestCacheDuration = 25;
        /// <summary>
        /// The auth required to use the service.
        /// Defaults to <see cref="Roles.Service"/>.
        /// </summary>
        public String Auth = Roles.Service;
        /// <summary>
        /// The maximum number of screenshots that are taken at the same time (at least 1).
        /// Defaults to 32.
        /// </summary>
        public int MaxConcurrency = 32;

        /// <summary>
        /// The effect that draws a sound for its screenshot (GetMediaAudio / MediaAudio, when the request names no Effect): the name of an
        /// effect in mediaView/audioEffects (ex: "Led_Spectrum"), or an url / a path relative to the mediaView folder (ex: "audioEffects/Spectrum.glsl").
        /// Null or empty = "Led_Spectrum".
        /// </summary>
        public String AudioEffect = "Led_Spectrum";

        /// <summary>
        /// The Google Maps (embed) api key, used when creating map images.
        /// Defaults to reading the key from the file "$(KeyFolder)/GoogleMaps_Embed.txt".
        /// </summary>
        public ApiKeyParams GoogleMapsKey = new ApiKeyParams
        {
            CredFile = "$(KeyFolder)/GoogleMaps_Embed.txt"
        };
    }


    /// <summary>
    /// A request for a Google Maps image as a jpeg
    /// </summary>
    public class GetGoogleMapJpegRequest : GetGoogleMapRequest
    {
        /// <summary>
        /// Jpeg quality, [10, 100].
        /// Defaults to 70.
        /// </summary>
        [EditSlider]
        [EditDefault(70.0)]
        [EditRange(10.0, 100.0)]
        public int Quality = 70;
    }


}
