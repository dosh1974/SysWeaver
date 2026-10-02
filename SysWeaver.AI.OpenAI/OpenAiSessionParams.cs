using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Parameters for an OpenAI session.
    /// Model examples:
    /// "gpt-4o-mini" -	Our affordable and intelligent small model for fast, lightweight tasks.
    /// "gpt-4o" - Our high-intelligence flagship model for complex, multi-step tasks.
    /// "o1-preview" - Language models trained with reinforcement learning to perform complex reasoning.
    /// </summary>
    public class OpenAiSessionParams : AiSessionParams
    {
        /// <summary>
        /// The service tier to use for chat
        /// </summary>
        public OpenAiServiceTier? Tier;

        /// <summary>
        /// The API used to implement a chat session (ignored for query sessions).
        /// Null to use the default (OpenAiParams.DefaultChatApi).
        /// </summary>
        public OpenAiChatApi? ChatApi;

        /// <summary>
        /// Get OpenAI session parameters from some (possibly API agnostic) session parameters
        /// </summary>
        /// <param name="p">The parameters, can be null</param>
        /// <returns>The same instance if it's already an OpenAiSessionParams, else a new instance with the common values copied</returns>
        public static OpenAiSessionParams From(AiSessionParams p)
        {
            if (p == null)
                return null;
            if (p is OpenAiSessionParams op)
                return op;
            return new OpenAiSessionParams
            {
                Model = p.Model,
                Reasoning = p.Reasoning,
            };
        }

    }

}
