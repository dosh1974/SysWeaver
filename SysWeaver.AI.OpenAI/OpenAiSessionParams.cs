using System;

namespace SysWeaver.AI
{
    public class OpenAiSessionParams
    {
        /// <summary>
        /// The model to use for this session.
        /// Examples:
        /// "gpt-4o-mini" -	Our affordable and intelligent small model for fast, lightweight tasks.
        /// "gpt-4o" - Our high-intelligence flagship model for complex, multi-step tasks.
        /// "o1-preview" - Language models trained with reinforcement learning to perform complex reasoning.
        /// </summary>
        public String Model;

        /// <summary>
        /// The reasoning effort for models that support reasoning.
        /// Null to use default.
        /// </summary>
        public OpenAiReasoning? Reasoning;


        /// <summary>
        /// The service tier to use for chat
        /// </summary>
        public OpenAiServiceTier? Tier;


    }

}
