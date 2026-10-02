using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{

    /// <summary>
    /// A simple (stateless) query session, supporting a system prompt and tools
    /// </summary>
    public interface IAiQuerySession
    {
        #region Model

        /// <summary>
        /// The model used by this session
        /// </summary>
        String Model { get; }

        /// <summary>
        /// True if the model supports setting the temperature
        /// </summary>
        bool HaveTemperature { get; }

        /// <summary>
        /// True if the model supports a system prompt (system role)
        /// </summary>
        bool SupportSystemRole { get; }

        /// <summary>
        /// True if the model supports parallel tool calls, null if unknown
        /// </summary>
        bool? SupportParallelToolCalls { get; }

        /// <summary>
        /// Temperature of the AI model, lower is more cosistent and less random [0, 2] (1 is default).
        /// </summary>
        float Temperature { get; set; }

        /// <summary>
        /// The system prompt
        /// </summary>
        String SystemPrompt { get; set; }

        #endregion//Model

        #region Tools

        IEnumerable<KeyValuePair<String, AiTool>> ALlTools { get; }

        /// <summary>
        /// Add an API endpoint that the AI can use in this session
        /// </summary>
        /// <param name="apiName">Name of tha api local url: ex: "Api/auth/GetUser"</param>
        /// <param name="fn">An optional function name (as shown to the AI).
        /// null - will use the underlaying method name (or as specified by the OpenAiToolName attribute) in combination with the declaring type name.
        /// "" - will user the full apiName, but with '/' replaced by '_'.
        /// </param>
        /// <returns>True if the tool was added</returns>
        bool AddTool(String apiName, String fn = null);

        /// <summary>
        /// Add a tool from some registered tool
        /// </summary>
        /// <param name="name">The name of the tool</param>
        /// <returns>True if the tool was added</returns>
        bool AddRegistredTool(String name);

        /// <summary>
        /// Add a method that the AI can use in this session.
        /// </summary>
        /// <param name="instance">Object instance</param>
        /// <param name="method">The method</param>
        /// <param name="fn">Optional function name, default will be instance type name _ method name</param>
        /// <param name="perfMonitor">An optional performance monitor</param>
        /// <param name="defaultAuth">The default auth to use if there is no WebApiAuthAttribute on the method</param>
        /// <param name="defaultCachedCompression">The default compression when there is no WebApiCompressionAttribute on the method</param>
        /// <param name="defaultCompression">The default compression for uncached cached methods when there is no WebApiCompressionAttribute on the method</param>
        /// <returns></returns>
        bool AddTool(Object instance, MethodInfo method, String fn = null, PerfMonitor perfMonitor = null, String defaultAuth = ApiHttpEntry.DefaultAuth, String defaultCachedCompression = ApiHttpEntry.DefaultCachedCompression, String defaultCompression = ApiHttpEntry.DefaultCompression);

        #endregion//Tools

        /// <summary>
        /// Perform a query (the query is not added to any conversation history)
        /// </summary>
        /// <param name="text">The query text</param>
        /// <param name="debug">An optional obejct used to track tool calls etc</param>
        /// <param name="onUsage">An optional function to call when token in/out usage is changed</param>
        /// <param name="extraData">optional attachements</param>
        /// <returns>The Ai response, typically MD encoded text</returns>
        Task<String> Query(String text, AiDebugMessage debug = null, Func<String, long, long, Task> onUsage = null, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData = null);
    }


    /// <summary>
    /// A chat session (a conversation), supporting a system prompt, tools, menus, commands etc
    /// </summary>
    public interface IAiChatSession : IAiQuerySession
    {
        event Action<IAiChatSession, String, HttpServerRequest> OnUserMessage;
        event Action<IAiChatSession, String, HttpServerRequest> OnAiResponseCompleted;

        String ThinkMessage { get; set; }

        String ChatId { get; internal set; }

        #region Menu

        void AddCommand(String name, Func<String, IAiChatSession, HttpServerRequest, Task<Chat.ChatMessage>> func, String args, String desc, params String[] auths);

        bool TryGetToolCommand(string name, out AiCommand cmd);

        delegate Task<bool> SetVarDel(Chat.IChatController c, String providerChatId, Chat.ChatScopes scope, HttpSession session, long messageId, String value);

        bool AddToMenu(bool toMessageMenu, Chat.ChatMenuItem item, SetVarDel setFunc = null, Chat.ChatMenuItem parent = null);

        Task<bool> SetValue(Chat.IChatController c, String providerChatId, Chat.ChatScopes scope, HttpSession session, long messageId, String key, String value);

        Chat.ChatMenuItem[] GetMenu(HttpSession session, bool isMessage, IReadOnlySet<String> exclude = null);

        #endregion//Menu

        #region Appearance

        bool IsPrivate { get; }

        /// <summary>
        /// Name used for AI response messages
        /// </summary>
        String AgentName { get; set; }

        String AgentSpeechName { get; set; }

        /// <summary>
        /// Name used for error messages
        /// </summary>
        String ErrorName { get; set; }

        /// <summary>
        /// Url to image used for AI response messages
        /// </summary>
        String AgentImageUrl { get; set; }

        /// <summary>
        /// Url to image used for error messages
        /// </summary>
        String ErrorImageUrl { get; set; }

        /// <summary>
        /// Url to image used for debug messages
        /// </summary>
        String DebugImageUrl { get; set; }

        /// <summary>
        /// If true, speech should be enabled by default
        /// </summary>
        bool EnableSpeechByDefault { get; set; }

        /// <summary>
        /// If true, the user may input markdown text (client is allowed to send the message with the MarkDown format).
        /// </summary>
        bool AllowUserMarkDown { get; set; }

        /// <summary>
        /// Allow storing files and links on the server (requires a UserStore).
        /// </summary>
        bool AllowStore { get; set; }

        /// <summary>
        /// If true, the server supports message translation (to the users language)
        /// </summary>
        bool CanTranslate { get; set; }

        /// <summary>
        /// If true, enable the menu option to show a user profile
        /// </summary>
        bool CanShowProfile { get; set; }

        /// <summary>
        /// If true a user may upload files
        /// </summary>
        bool CanUpload { get; set; }

        /// <summary>
        /// The image to use when the AI is working
        /// </summary>
        String WorkingImageUrl { get; set; }

        String[] SpeechNames { get; set; }

        Chat.ChatVoice[] Voices { get; set; }

        #endregion//Appearance

        #region Chat

        /// <summary>
        /// The function used to get the system prompt
        /// </summary>
        Func<HttpSession, String> GetSystemPrompt { get; set; }

        /// <summary>
        /// The function used to format a user message.
        /// Parameters are: session, user name, original message.
        /// </summary>
        Func<HttpSession, String, String, String> FormatUserMessage { get; set; }

        AsyncLock ChatQueryLock { get; }

        bool AutoRemoveToolCalls { get; set; }

        IReadOnlyList<String> JoinAuth { get; }
        IReadOnlyList<String> ClearAuth { get; }
        List<Chat.ChatMessage> Messages { get; }
        Dictionary<long, Chat.ChatMessage> MessageLookup { get; }

        /// <summary>
        /// The current (last used) message id
        /// </summary>
        long MsgId { get; }

        /// <summary>
        /// Allocate a new message id
        /// </summary>
        /// <returns>The new message id</returns>
        long NextMsgId();

        /// <summary>
        /// Clear the conversation history sent to the API
        /// </summary>
        void ClearApiMessages();

        /// <summary>
        /// Add a user message to the conversation and get the AI response (tools are executed as needed).
        /// </summary>
        /// <param name="text">The user message</param>
        /// <param name="request">The request</param>
        /// <param name="debug">An optional obejct used to track tool calls etc</param>
        /// <param name="from">Optional name of the user</param>
        /// <param name="onUsage">An optional function to call when token in/out usage is changed</param>
        /// <returns>The AI response</returns>
        Task<String> Complete(String text, HttpServerRequest request, AiDebugMessage debug = null, String from = null, Func<String, long, long, Task> onUsage = null);

        /// <summary>
        /// Add a user message to the conversation and stream the AI response (tools are executed as needed).
        /// </summary>
        /// <returns>null if successful, else an error message</returns>
        Task<String> CompleteUpdate(String text,
            IReadOnlyList<ValueTuple<AiContentPart, String>> extraData,
            HttpServerRequest request,
            Func<String, String, Task> onUpdate,
            Func<String, String, String, String> saveFile,
            Func<Object, String> saveData,
            Action<String> onTools,
            AiDebugMessage debug,
            String from,
            Func<String, long, long, Task> onUsage
            );

        #endregion//Chat

        #region Internal

        internal void InvokeOnUserMessageBegin(String body, HttpServerRequest request);

        internal void InvokeAiResponseCompleted(String body, HttpServerRequest request);

        internal ConcurrentDictionary<String, AiCommand> Commands { get; }

        internal Task<String> BuildSystemPrompt(HttpSession s);

        /// <summary>
        /// Write the tools and messages (including the system prompt) as json properties (the containing object is written by the caller).
        /// </summary>
        internal void WriteConversation(Utf8JsonWriter writer);

        internal void SetProperty<T>(String key, T value);

        internal bool TryGetProperty<T>(String key, out T value);

        internal bool TryRemoveProperty<T>(String key, out T value);

        #endregion//Internal
    }

}
