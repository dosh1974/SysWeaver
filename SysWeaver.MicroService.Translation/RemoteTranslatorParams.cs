using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="RemoteTranslatorService"/>
    /// </summary>
    public sealed class RemoteTranslatorParams : CredentialParams
    {
        /// <summary>
        /// Number of seconds to cache translations in memory for short retention, default is 3600 (minimum 30)
        /// </summary>
        public int ShortMemCacheDuration = 60 * 60;

        /// <summary>
        /// Number of seconds to cache translations in memory for medium retention, default is 28800 (minimum 30)
        /// </summary>
        public int MediumMemCacheDuration = 8 * 60 * 60;

        /// <summary>
        /// Number of seconds to cache translations in memory for long retention, default is 86400 (minimum 30)
        /// </summary>
        public int LongMemCacheDuration = 24 * 60 * 60;

        /// <summary>
        /// The base url, all endpoints defined in an API is prefixed with this value, ex: "http://localhost:1234/api/"
        /// </summary>
        public String BaseUrl;

        /// <summary>
        /// Number of seconds to cache Source and Target languages, default is 3600 (minimum 30).
        /// </summary>
        public int FromToCacheDuration = 60 * 60;

        /// <summary>
        /// Maximum number of concurrent requests, default is 32 (minimum 1)
        /// </summary>
        public int MaxConcurrency = 32;
    }


}
