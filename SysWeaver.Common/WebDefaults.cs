using System;

// https://github.com/SimpleStack/simplestack.orm



namespace SysWeaver
{
    /// <summary>
    /// Default values for web related parameters, the compression method lists used by the HTTP server and API parameters (format: "method:level, ...").
    /// </summary>
    public static class WebDefaults
    {
        /// <summary>
        /// Default compression methods to use for one-off data (ex: returning API data that isn't cached at all).
        /// </summary>
        public const String DynamicCompression = "zstd:Fast, deflate:Fast, gzip:Fast, br:Fast";

        /// <summary>
        /// Default compression methods to use for cached data (ex: returning API data that is cached in memory for some limited time).
        /// </summary>
        public const String CachedCompression = "zstd:Balanced, deflate:Balanced, gzip:Balanced, br:Balanced";

        /// <summary>
        /// Default compression methods to use for static data (ex: static data that never changes for the process duration or is stored on disc etc).
        /// </summary>
        public const String StaticCompression = "br:Best, zstd:Best, deflate:Best, gzip:Best";

    }




}
