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
        /// Number of seconds before a failed media information build (ex: the remote thumbnail service was down) is tried again.
        /// The failure time is stored with the cached info, so a failure is never retried more often than this (sets MediaInfo.RetryFailedSeconds).
        /// </summary>
        public int RetryFailedSeconds = 300;

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
