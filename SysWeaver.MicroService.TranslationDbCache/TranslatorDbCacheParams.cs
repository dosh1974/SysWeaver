using System;
using SysWeaver.Db;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="TranslatorDbCacheService"/>
    /// </summary>
    public sealed class TranslatorDbCacheParams : MySqlDbParams
    {
        /// <summary>
        /// Sets the default schema name ("Translations")
        /// </summary>
        public TranslatorDbCacheParams()
        {
            Schema = "Translations";
        }

        /// <summary>
        /// Number of seconds to cache translations in memory for short retention, default is 3600 (minimum 30)
        /// </summary>
        public int ShortMemCacheDuration = 60 * 60;

        /// <summary>
        /// Number of hours to cache translations in a db for short retention, default is 720 (30 days, minimum 1)
        /// </summary>
        public int ShortDbCacheDuration = 30 * 24;

        /// <summary>
        /// Number of seconds to cache translations in memory for medium retention, default is 28800 (minimum 30)
        /// </summary>
        public int MediumMemCacheDuration = 8 * 60 * 60;

        /// <summary>
        /// Number of hours to cache translations in a db for medium retention, default is 2160 (90 days, minimum 1)
        /// </summary>
        public int MediumDbCacheDuration = 90 * 24;

        /// <summary>
        /// Number of seconds to cache translations in memory for long retention, default is 86400 (minimum 30)
        /// </summary>
        public int LongMemCacheDuration = 24 * 60 * 60;

        /// <summary>
        /// Number of hours to cache translations in a db for long retention, default is 8640 (360 days, minimum 1)
        /// </summary>
        public int LongDbCacheDuration = 360 * 24;

        /// <summary>
        /// The maximum number of hours that is randomly added to the db expiration, default is 168 (7 days), 0 disables it
        /// </summary>
        public int RandomizeExpiration = 7 * 24;

        /// <summary>
        /// Optional name of the IInternalTranslator instance to use (null uses the default instance)
        /// </summary>
        public string TranslatorInstance;

        /// <summary>
        /// Rebuild the inputs table when requests come in (is somewhat slower), default is false
        /// </summary>
        public bool RebuildInputs;
    }


}
