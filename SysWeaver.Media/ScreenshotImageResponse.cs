using System;
using SysWeaver.Media;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The result of a screenshot request: the encoded image and optionally its media information
    /// </summary>
    public sealed class ScreenshotImageResponse
    {
        /// <summary>
        /// Image data in the requested format
        /// </summary>
        public Byte[] Data;
        
        /// <summary>
        /// Media info if available
        /// </summary>
        public MediaInfo Info;
    }




}
