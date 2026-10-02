using System;
using System.Threading.Tasks;
using SysWeaver.Chat;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// An AI service (API agnostic), acts as a chat provider and can create chat and query sessions
    /// </summary>
    public interface IAiService : IChatProvider, IAiToolCache, IPerfMonitored
    {
        /// <summary>
        /// The model used when not supplying a model in a session (can be configured)
        /// </summary>
        String DefaultChatModel { get; }

        /// <summary>
        /// The reasoning effort used when not supplying a reasoning effort in a session (can be configured)
        /// </summary>
        AiReasoning DefaultReasoning { get; }

        /// <summary>
        /// The model used for image generation when not supplying a model (can be configured)
        /// </summary>
        String DefaultImageModel { get; }

        /// <summary>
        /// Create a chat session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="isPrivate">If true, this chat is user only</param>
        /// <param name="p">Optional parameters for this session, if null, the default will be used (can be a derived API specific type)</param>
        /// <param name="joinAuth">Optional comma separated list of required auth tokens</param>
        /// <param name="clearAuth">Auth required to clear the chat session</param>
        /// <param name="defaultTools">true to add some default tools for the AI to use</param>
        /// <param name="memory">An optional memory implementation </param>
        /// <returns>A new chat session</returns>
        IAiChatSession CreateChatSession(bool isPrivate, AiSessionParams p = null, String joinAuth = "", String clearAuth = "Admin", bool defaultTools = true, IAiMemory memory = null);

        /// <summary>
        /// Create a query session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="p">Optional parameters for this session, if null, the default will be used (can be a derived API specific type)</param>
        /// <returns>A new query session</returns>
        IAiQuerySession CreateQuerySession(AiSessionParams p = null);

        /// <summary>
        /// Create a query session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="model">The model to use.</param>
        /// <returns>A new query session</returns>
        IAiQuerySession CreateQuerySession(String model);

        /// <summary>
        /// Add a global chat (accessible to all users that meet the join auth of the chat)
        /// </summary>
        /// <param name="chat">The chat session</param>
        /// <param name="name">The name (id) of the chat</param>
        /// <returns>True if added</returns>
        bool AddChat(IAiChatSession chat, String name);

        /// <summary>
        /// Remove a global chat
        /// </summary>
        /// <param name="name">The name (id) of the chat</param>
        /// <returns>True if removed</returns>
        bool RemoveChat(String name);

        /// <summary>
        /// Add a chat to the http session of a request (only accessible to that http session)
        /// </summary>
        /// <param name="chat">The chat session</param>
        /// <param name="name">The name (id) of the chat</param>
        /// <param name="request">The request</param>
        /// <returns>True if added</returns>
        bool AddChatSession(IAiChatSession chat, String name, HttpServerRequest request);

        /// <summary>
        /// Get a chat from the http session of a request
        /// </summary>
        bool TryGetSession(String name, HttpServerRequest request, out IAiChatSession chat);

        /// <summary>
        /// Remove a chat from the http session of a request
        /// </summary>
        /// <returns>The removed chat or null</returns>
        IAiChatSession RemoveChatSession(String name, HttpServerRequest request);

        /// <summary>
        /// Post a message to a chat (no AI response is generated)
        /// </summary>
        bool SendMessageAs(string providerChatId, HttpServerRequest request, ChatMessageBody message, String from = null, String fromImage = null, String toUserGuid = null);

        /// <summary>
        /// Add a command available to all chat sessions created by this service
        /// </summary>
        void AddCommand(String name, Func<String, IAiChatSession, HttpServerRequest, Task<Chat.ChatMessage>> func, String args, String desc, params String[] auths);

        /// <summary>
        /// Register tools from an instance (tools still have to be added to a session)
        /// </summary>
        void AddTools(IHaveOpenAiTools a);

        /// <summary>
        /// Unregister tools from an instance
        /// </summary>
        void RemoveTools(IHaveOpenAiTools a);

        /// <summary>
        /// Callback with usage stats, args are: the request, model, number of input tokens, number of output tokens
        /// </summary>
        event Func<HttpServerRequest, String, long, long, Task> OnUse;
    }

}
