using System;

namespace SysWeaver.MicroService
{
    public sealed class ThumbnailParams
    {
        /// <summary>
        /// The prefix to use before the width and height, ex ?"Thumb32x32"
        /// </summary>
        public String Prefix = "Thumb";


        /// <summary>
        /// Number of seconds to cache media information (speed up queries, but info might be stale for this duration)
        /// </summary>
        public int CacheSeconds = 120;

        /// <summary>
        /// The resolutions to support (and cache) for, as "WIDTHxHEIGHT" strings.
        /// </summary>
        public String[] Resolutions = new[]
        {
            "32x32",
        };

        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool PerMon = true;

    }
}
