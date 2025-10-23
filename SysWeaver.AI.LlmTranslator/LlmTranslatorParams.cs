using System;

namespace SysWeaver.AI
{
    public sealed class LlmTranslatorParams
    {
        /// <summary>
        /// What OpenAiService instance to use (null to use any instance)
        /// </summary>
        public String AiInstance;

        /// <summary>
        /// The AI model to use for low efforts 
        /// </summary>
        public String LowModel = "gpt-4.1-nano";    // $0.10 + $0.40

        /// <summary>
        /// The AI model to use for medium efforts 
        /// </summary>
        public String MediumModel = "gpt-4.1-mini"; // $0.40 + $1.60

        /// <summary>
        /// The AI model to use for medium efforts 
        /// </summary>
        public String HighModel = "gpt-4.1";        // $2.00 + $8.00

        /// <summary>
        /// If true, the supported languages are loaded / queried on service start-up
        /// </summary>
        public bool GetLanguagesOnLoad = true;

        /// <summary>
        /// If true, the supported languages are queried on service start-up
        /// </summary>
        public bool ForceLanguagesOnLoad;

        /// <summary>
        /// Number of times to retry a LLM translation request before giving up
        /// </summary>
        public int RetryCount = 10;
    }
}
