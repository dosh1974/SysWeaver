namespace SysWeaver.LanguageIdentifier
{
    public sealed class FastTextLanguageIdentifierParams
    {
        /// <summary>
        /// An optional model file to use
        /// </summary>
        public string ModelFile;

        /// <summary>
        /// The maximum number of prediction to check
        /// </summary>
        public int MaxChecked = 20;

        /// <summary>
        /// Number of seconds to cache
        /// </summary>
        public int CacheSeconds = 60;
    }
}
