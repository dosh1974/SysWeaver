using System;
using SysWeaver.Media;

namespace SysWeaver.MicroService
{
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
