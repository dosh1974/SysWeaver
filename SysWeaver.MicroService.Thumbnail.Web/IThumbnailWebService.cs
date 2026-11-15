using System;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{
                     
    /// <summary>
    /// The client interface of the thumbnail web service (<see cref="ThumbnailWebService"/>) as used by a remote client.
    /// </summary>
    public interface IThumbnailRemoteService : IDisposable
    {
        /// <summary>
        /// Get data from an image url (a screenshot of the image and information about it)
        /// </summary>
        /// <param name="r">The image url, image options, the size of the screenshot, when to take it and an optional extra delay</param>
        /// <returns>The screenshot (png) and information about it</returns>
        Task<ScreenshotImageResponse> GetMediaImage(GetMediaImageRequest r);

        /// <summary>
        /// Get a png from an image url
        /// </summary>
        /// <param name="r">The image url, image options, the size of the screenshot, when to take it and an optional extra delay</param>
        /// <returns>The screenshot as png image data</returns>
        Task<Byte[]> MediaImage(GetMediaImageRequest r);

        /// <summary>
        /// Get data from a video url (a screenshot of a frame of the video and information about it)
        /// </summary>
        /// <param name="r">The video url, video options, the size of the screenshot, when to take it (position in seconds) and an optional extra delay</param>
        /// <returns>The screenshot (png) and information about it</returns>
        Task<ScreenshotImageResponse> GetMediaVideo(GetMediaVideoRequest r);

        /// <summary>
        /// Get a png from a video url
        /// </summary>
        /// <param name="r">The video url, video options, the size of the screenshot, when to take it (position in seconds) and an optional extra delay</param>
        /// <returns>The screenshot as png image data</returns>
        Task<Byte[]> MediaVideo(GetMediaVideoRequest r);


        /// <summary>
        /// Get data from an effect url (a screenshot of the effect and information about it)
        /// </summary>
        /// <param name="r">The effect url, effect options, the size of the screenshot, when to take it (position in seconds) and an optional extra delay</param>
        /// <returns>The screenshot (png) and information about it</returns>
        Task<ScreenshotImageResponse> GetMediaEffect(GetMediaEffectRequest r);

        /// <summary>
        /// Get a png from an effect url
        /// </summary>
        /// <param name="r">The effect url, effect options, the size of the screenshot, when to take it (position in seconds) and an optional extra delay</param>
        /// <returns>The screenshot as png image data</returns>
        Task<Byte[]> MediaEffect(GetMediaEffectRequest r);

        /// <summary>
        /// Get data from a sound url (a screenshot of an audio effect drawing the sound in its middle, and information about it)
        /// </summary>
        /// <param name="r">The sound url, audio options (the effect, default the service's AudioEffect: Led_Spectrum), the size of the screenshot and an optional extra delay</param>
        /// <returns>The screenshot (png) and information about it</returns>
        Task<ScreenshotImageResponse> GetMediaAudio(GetMediaAudioRequest r);

        /// <summary>
        /// Get a png from a sound url
        /// </summary>
        /// <param name="r">The sound url, audio options (the effect, default the service's AudioEffect: Led_Spectrum), the size of the screenshot and an optional extra delay</param>
        /// <returns>The screenshot as png image data</returns>
        Task<Byte[]> MediaAudio(GetMediaAudioRequest r);

        /// <summary>
        /// Get data from a YouTube code (a screenshot of a frame of the video and information about it)
        /// </summary>
        /// <param name="r">The YouTube video code, the size of the screenshot, when to take it (position in seconds) and an optional extra delay</param>
        /// <returns>The screenshot (png) and information about it</returns>
        Task<ScreenshotImageResponse> GetMediaYouTube(GetMediaYouTubeRequest r);

        /// <summary>
        /// Get a png from a YouTube code
        /// </summary>
        /// <param name="r">The YouTube video code, the size of the screenshot, when to take it (position in seconds) and an optional extra delay</param>
        /// <returns>The screenshot as png image data</returns>
        Task<Byte[]> MediaYouTube(GetMediaYouTubeRequest r);

        /// <summary>
        /// Get a png from a google map position
        /// </summary>
        /// <param name="r">The map: center (latitude, longitude), zoom, map type, language, size, dpi and an optional pin</param>
        /// <returns>The map as png image data</returns>
        Task<Byte[]> GetGoogleMapPng(GetGoogleMapRequest r);

        /// <summary>
        /// Get a jpg from a google map position
        /// </summary>
        /// <param name="r">The map: center (latitude, longitude), zoom, map type, language, size, dpi, an optional pin and the jpeg quality</param>
        /// <returns>The map as jpeg image data</returns>
        Task<Byte[]> GetGoogleMapJpg(GetGoogleMapJpegRequest r);

        /// <summary>
        /// Get an image and meta data for a given url (a screenshot of the page)
        /// </summary>
        /// <param name="r">The url, the size of the window, the scale (in percent), the image format and quality, and optional delay and speed settings</param>
        /// <returns>The image data, and information about the image if control was requested</returns>
        Task<ScreenshotImageResponse> GetImage(ScreenshotImageRequest r);

        /// <summary>
        /// Get a png image (screenshot) from an url
        /// </summary>
        /// <param name="r">The url, the size of the window, the scale and optional delay and speed settings</param>
        /// <returns>The screenshot as png image data</returns>
        Task<Byte[]> WebScreenshotPng(ScreenshotPngRequest r);

        /// <summary>
        /// Get a jpeg image (screenshot) from an url
        /// </summary>
        /// <param name="r">The url, the size of the window, the scale, the jpeg quality and optional delay and speed settings</param>
        /// <returns>The screenshot as jpeg image data</returns>
        Task<Byte[]> WebScreenshotJpg(ScreenshotJpegRequest r);
    }


}
