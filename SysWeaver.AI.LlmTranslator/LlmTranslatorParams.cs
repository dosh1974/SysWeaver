using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Parameters for the <see cref="LlmTranslator"/> service
    /// </summary>
    public sealed class LlmTranslatorParams
    {
        /// <summary>
        /// What AI service (IAiService) instance to use (null to use any instance)
        /// </summary>
        public String AiInstance;

        /// <summary>
        /// The AI model to use for low efforts (default "gpt-4.1-nano")
        /// </summary>
        public String LowModel = "gpt-4.1-nano";    // $0.10 + $0.40

        /// <summary>
        /// The AI model to use for medium efforts (default "gpt-4.1-mini")
        /// </summary>
        public String MediumModel = "gpt-4.1-mini"; // $0.40 + $1.60

        /// <summary>
        /// The AI model to use for high efforts (default "gpt-4.1")
        /// </summary>
        public String HighModel = "gpt-4.1";        // $2.00 + $8.00

        /// <summary>
        /// If true, the supported languages are loaded (from the store, refreshed if older than 30 days) or queried from the LLM on service start-up
        /// </summary>
        public bool GetLanguagesOnLoad = true;

        /// <summary>
        /// If true, the supported languages are always queried from the LLM on service start-up (a stored list is ignored and replaced)
        /// </summary>
        public bool ForceLanguagesOnLoad;

        /// <summary>
        /// Number of times to retry a LLM translation request before giving up (default 10)
        /// </summary>
        public int RetryCount = 10;


        /// <summary>
        /// If true, all languages are considered valid
        /// </summary>
        public bool AllowAllLanguages;


        /// <summary>
        /// A list of languages to forcefully add to the supported languages
        /// </summary>
        public String[] ForcedLanguages;

    }
}
