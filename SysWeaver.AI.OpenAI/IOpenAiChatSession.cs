using System;

namespace SysWeaver.AI
{

    /// <summary>
    /// The API used to implement a chat session
    /// </summary>
    public enum OpenAiChatApi
    {
        /// <summary>
        /// Use the chat completions API (OpenAiChatSession)
        /// </summary>
        Completions,
        /// <summary>
        /// Use the responses API (OpenAiResponseChatSession)
        /// </summary>
        Responses,
    }

    /// <summary>
    /// An OpenAI chat session, implemented by OpenAiChatSession (chat completions API) and OpenAiResponseChatSession (responses API)
    /// </summary>
    public interface IOpenAiChatSession : IAiChatSession
    {
        /// <summary>
        /// The API used by this session
        /// </summary>
        OpenAiChatApi ChatApi { get; }
    }

}
