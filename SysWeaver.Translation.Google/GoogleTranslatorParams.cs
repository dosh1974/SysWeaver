namespace SysWeaver.Translation
{
    /// <summary>
    /// Parameters for the Google translator
    /// </summary>
    public class GoogleTranslatorParams
    {
        /// <summary>
        /// Number of seconds to cache translations in memory for short retention, default 3600 (minimum 10)
        /// </summary>
        public int ShortMemCacheDuration = 60 * 60;

        /// <summary>
        /// Number of seconds to cache translations in memory for medium retention, default 28800 (minimum 10)
        /// </summary>
        public int MediumMemCacheDuration = 8 * 60 * 60;

        /// <summary>
        /// Number of seconds to cache translations in memory for long retention, default 86400 (minimum 10)
        /// </summary>
        public int LongMemCacheDuration = 24 * 60 * 60;

        /// <summary>
        /// True to enable the performance monitor, default true
        /// </summary>
        public bool PerMon = true;

        /// <summary>
        /// Allow proxy through the Tor network (to avoid rate limiting), SysWeaver.Tor must be available. Default true.
        /// </summary>
        public bool UseTor = true;

        /// <summary>
        /// If true, start the tor client directly (else it's started on demand), default false
        /// </summary>
        public bool StartTor;

        /// <summary>
        /// Number of times to retry a translation request before giving up, default 10 (limited to 0 - 100)
        /// </summary>
        public int Retry = 10;

        /// <summary>
        /// Number of minutes to continue using tor before trying a "normal" request again, default 60 (minimum 1)
        /// </summary>
        public int UseTorFor = 60;


    }

}
