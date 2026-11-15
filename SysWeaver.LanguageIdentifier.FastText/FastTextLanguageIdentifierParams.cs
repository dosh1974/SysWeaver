namespace SysWeaver.LanguageIdentifier
{
    /// <summary>
    /// Parameters for the FastText language identifier
    /// </summary>
    public sealed class FastTextLanguageIdentifierParams
    {
        /// <summary>
        /// An optional model file to use (path templates are resolved), if empty (default) the embedded lid.176.ftz model is used
        /// </summary>
        public string ModelFile;

        /// <summary>
        /// The maximum number of predictions to check when looking for the user language, default 20 (values below 1 are treated as 1)
        /// </summary>
        public int MaxChecked = 20;

        /// <summary>
        /// Number of seconds to cache results, default 60 (values below 5 are treated as 5)
        /// </summary>
        public int CacheSeconds = 60;
    }
}
