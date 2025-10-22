namespace SysWeaver.LanguageIdentifier
{
    public sealed class LlmLanguageIdentifierParams
    {
        /// <summary>
        /// The Llm model to use, leave blank to use the default
        /// </summary>
        public string Model = "gpt-4.1-nano";

        /// <summary>
        /// Number of seconds to cache
        /// </summary>
        public int CacheSeconds = 60;

        /// <summary>
        /// Maximum number of concurrent Llm queries
        /// </summary>
        public int MaxConcurrency = 8;

    }


}
