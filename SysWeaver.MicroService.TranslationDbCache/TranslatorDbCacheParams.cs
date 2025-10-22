using System;
using SysWeaver.Db;

namespace SysWeaver.MicroService
{
    public sealed class TranslatorDbCacheParams : MySqlDbParams
    {
        public TranslatorDbCacheParams()
        {
            Schema = "Translations";
        }

        /// <summary>
        /// Number of seconds to cache translations in memory for short retention
        /// </summary>
        public int ShortMemCacheDuration = 60 * 60;

        /// <summary>
        /// Number of hours to cache translations in a db for short retention
        /// </summary>
        public int ShortDbCacheDuration = 30 * 24;

        /// <summary>
        /// Number of seconds to cache translations in memory for medium retention
        /// </summary>
        public int MediumMemCacheDuration = 8 * 60 * 60;

        /// <summary>
        /// Number of hours to cache translations in a db for medium retention
        /// </summary>
        public int MediumDbCacheDuration = 90 * 24;

        /// <summary>
        /// Number of seconds to cache translations in memory for long retention
        /// </summary>
        public int LongMemCacheDuration = 24 * 60 * 60;

        /// <summary>
        /// Number of hours to cache translations in a db for long retention
        /// </summary>
        public int LongDbCacheDuration = 360 * 24;

        /// <summary>
        /// How many hours to randomly add to the expiration
        /// </summary>
        public int RandomizeExpiration = 7 * 24;

        /// <summary>
        /// Optional name of the IInternalTranslation instance to use
        /// </summary>
        public string TranslatorInstance;

        /// <summary>
        /// Rebuild the inputs table when requests come in (is some what slower)
        /// </summary>
        public bool RebuildInputs = true;
    }


}
