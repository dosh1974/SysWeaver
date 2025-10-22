namespace SysWeaver.Translation
{
    public class GoogleTranslatorParams
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
        /// True to enable the performance monitor
        /// </summary>
        public bool PerMon = true;

        /// <summary>
        /// Allow proxy through the Tor network (to avoid rate limiting), SysWeaver.Tor must be available.
        /// </summary>
        public bool UseTor = true;

        /// <summary>
        /// If true, start the tor client directly (else it's started on demand)
        /// </summary>
        public bool StartTor;

        /// <summary>
        /// Number of times to retry a translation request vefore giving up
        /// </summary>
        public int Retry = 10;

        /// <summary>
        /// Number of minutes to continue using tor before trying a "normal" request again
        /// </summary>
        public int UseTorFor = 60;


    }

}
