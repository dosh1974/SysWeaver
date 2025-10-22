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
        public String LowModel = "gpt-4.1-nano";

        /// <summary>
        /// The AI model to use for medium efforts 
        /// </summary>
        public String MediumModel = "gpt-4.1-mini";

        /// <summary>
        /// The AI model to use for medium efforts 
        /// </summary>
        public String HighModel = "gpt-4.1";

    }
}
