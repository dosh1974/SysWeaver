using System;

namespace SysWeaver.MicroService
{
    public sealed class RemoteTranslatorParams : CredentialParams
    {
        /// <summary>
        /// Number of seconds to cache translations in memory for short retention
        /// </summary>
        public int ShortMemCacheDuration = 60 * 60;

        /// <summary>
        /// Number of seconds to cache translations in memory for medium retention
        /// </summary>
        public int MediumMemCacheDuration = 8 * 60 * 60;

        /// <summary>
        /// Number of seconds to cache translations in memory for long retention
        /// </summary>
        public int LongMemCacheDuration = 24 * 60 * 60;

        /// <summary>
        /// The base url, all endpoints defined in an API is prefixed with this value, ex: "http://locahost:1234/api/"
        /// </summary>
        public String BaseUrl;

        /// <summary>
        /// Number of seconds to cache Source and Target languages.
        /// </summary>
        public int FromToCacheDuration = 60 * 60;

        /// <summary>
        /// Maximum number of concurrent requests
        /// </summary>
        public int MaxConcurrency = 100;
    }


}
