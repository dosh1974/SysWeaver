using System;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A service that renders web pages (urls) to images, ex: to create thumbnails of web pages.
    /// Implemented by the web thumbnail service (SysWeaver.MicroService.Thumbnail.Web), using a headless web browser.
    /// </summary>
    public interface IThumbnailWebService : IDisposable
    {
        /// <summary>
        /// Get an image (screenshot) from an url
        /// </summary>
        /// <param name="r">Parameters</param>
        /// <returns>Response</returns>
        Task<ScreenshotImageResponse> GetImage(ScreenshotImageRequest r);
    }




}
