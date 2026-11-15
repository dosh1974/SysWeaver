namespace SysWeaver.LanguageIdentifier
{
    /// <summary>
    /// Parameters for the Llm language identifier
    /// </summary>
    public sealed class LlmLanguageIdentifierParams
    {
        /// <summary>
        /// The Llm model to use (default "gpt-4.1-nano"), leave blank to use the default chat model of the OpenAI service
        /// </summary>
        public string Model = "gpt-4.1-nano";

        /// <summary>
        /// Number of seconds to cache results, default 60 (values below 5 are treated as 5)
        /// </summary>
        public int CacheSeconds = 60;

        /// <summary>
        /// Maximum number of concurrent Llm queries, default 8 (values below 1 are treated as 1)
        /// </summary>
        public int MaxConcurrency = 8;

    }


}
