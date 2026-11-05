using System;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{
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
