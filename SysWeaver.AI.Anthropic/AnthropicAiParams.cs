using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Parameters for the Anthropic (Claude) AI service.
    /// See: https://platform.claude.com/docs/en/api/overview
    /// </summary>
    public sealed class AnthropicAiParams : AiServiceParams
    {
        public AnthropicAiParams()
        {
            DefaultChatModel = "claude-opus-5-5";
        }

        #region API setup

        /// <summary>
        /// The maximum number of times a failed request (connection errors, 408, 409, 429 and 5xx) is retried by the client
        /// </summary>
        public int MaxRetries = 2;

        #endregion// API setup

        #region Chat

        /// <summary>
        /// The default maximum number of output tokens of a response (the API requires a value).
        /// Responses are always streamed, so large values are fine (up to 128000 on current models).
        /// </summary>
        public int DefaultMaxTokens = 32000;

        /// <summary>
        /// If true, prompt caching is enabled (the conversation prefix is cached between requests, reducing cost and latency)
        /// </summary>
        public bool PromptCaching = true;

        #endregion//Chat
    }


    /// <summary>
    /// Parameters for an Anthropic (Claude) session.
    /// Model examples:
    /// "claude-opus-5-5" - The default, highly capable model.
    /// "claude-sonnet-5-5" - Speed and capability for everyday work.
    /// "claude-haiku-5-5" - Fast and cost efficient.
    /// "claude-fable-5-1" - The most capable model.
    /// </summary>
    public class AnthropicSessionParams : AiSessionParams
    {
        /// <summary>
        /// The maximum number of output tokens of a response, null to use the service default
        /// </summary>
        public int? MaxTokens;

        /// <summary>
        /// Get Anthropic session parameters from some (possibly API agnostic) session parameters
        /// </summary>
        /// <param name="p">The parameters, can be null</param>
        /// <returns>The same instance if it's already an AnthropicSessionParams, else a new instance with the common values copied</returns>
        public static AnthropicSessionParams From(AiSessionParams p)
        {
            if (p == null)
                return null;
            if (p is AnthropicSessionParams ap)
                return ap;
            return new AnthropicSessionParams
            {
                Model = p.Model,
                Reasoning = p.Reasoning,
            };
        }
    }

}
