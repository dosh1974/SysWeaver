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

        /// <summary>
        /// All tools of this session by name
        /// </summary>
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
        /// <summary>
        /// Raised when a user message is received (session, message text, request), before it is sent to the AI
        /// </summary>
        event Action<IAiChatSession, String, HttpServerRequest> OnUserMessage;

        /// <summary>
        /// Raised when the AI has completed a response (session, response text, request)
        /// </summary>
        event Action<IAiChatSession, String, HttpServerRequest> OnAiResponseCompleted;

        /// <summary>
        /// The text of the temporary message shown while the AI is working on a response
        /// </summary>
        String ThinkMessage { get; set; }

        /// <summary>
        /// The id of this chat (set when the session is added to a service)
        /// </summary>
        String ChatId { get; internal set; }

        #region Menu

        /// <summary>
        /// Add a command (ex: "/tools") that users can run in this chat
        /// </summary>
        /// <param name="name">The name of the command</param>
        /// <param name="func">The function that runs the command (arguments, session, request), returns the message to show</param>
        /// <param name="args">A description of the arguments (null if none)</param>
        /// <param name="desc">A description of what the command does</param>
        /// <param name="auths">The auth required to run the command</param>
        void AddCommand(String name, Func<String, IAiChatSession, HttpServerRequest, Task<Chat.ChatMessage>> func, String args, String desc, params String[] auths);

        /// <summary>
        /// Try to get the command that runs a tool of this session
        /// </summary>
        /// <param name="name">The name of the tool</param>
        /// <param name="cmd">The command if found</param>
        /// <returns>True if a tool with the name exist in this session</returns>
        bool TryGetToolCommand(string name, out AiCommand cmd);

        /// <summary>
        /// Called when a menu value is changed
        /// </summary>
        /// <param name="c">The chat controller</param>
        /// <param name="providerChatId">The id of the chat</param>
        /// <param name="scope">The scope of the change</param>
        /// <param name="session">The session of the user</param>
        /// <param name="messageId">The id of the message the change applies to (for message menus)</param>
        /// <param name="value">The new value</param>
        /// <returns>True if the value was accepted</returns>
        delegate Task<bool> SetVarDel(Chat.IChatController c, String providerChatId, Chat.ChatScopes scope, HttpSession session, long messageId, String value);

        /// <summary>
        /// Add an item to the chat menu
        /// </summary>
        /// <param name="toMessageMenu">True to add to the message menu, false to add to the chat menu</param>
        /// <param name="item">The item to add</param>
        /// <param name="setFunc">Optional function called when the value of the item is changed</param>
        /// <param name="parent">Optional parent item (that must already have been added)</param>
        /// <returns>False if an item with the same id was already added</returns>
        bool AddToMenu(bool toMessageMenu, Chat.ChatMenuItem item, SetVarDel setFunc = null, Chat.ChatMenuItem parent = null);

        /// <summary>
        /// Set the value of a menu item (calls the function supplied when the item was added)
        /// </summary>
        /// <param name="c">The chat controller</param>
        /// <param name="providerChatId">The id of the chat</param>
        /// <param name="scope">The scope of the change</param>
        /// <param name="session">The session of the user</param>
        /// <param name="messageId">The id of the message the change applies to (for message menus)</param>
        /// <param name="key">The id of the menu item</param>
        /// <param name="value">The new value</param>
        /// <returns>True if the value was accepted</returns>
        Task<bool> SetValue(Chat.IChatController c, String providerChatId, Chat.ChatScopes scope, HttpSession session, long messageId, String key, String value);

        /// <summary>
        /// Get the menu items that the user of a session may use
        /// </summary>
        /// <param name="session">The session of the user</param>
        /// <param name="isMessage">True to get the message menu, false to get the chat menu</param>
        /// <param name="exclude">Optional ids of items to exclude</param>
        /// <returns>The menu items (with children), filtered by the auth of the user</returns>
        Chat.ChatMenuItem[] GetMenu(HttpSession session, bool isMessage, IReadOnlySet<String> exclude = null);

        #endregion//Menu

        #region Appearance

        /// <summary>
        /// True if this is a private chat (user only)
        /// </summary>
        bool IsPrivate { get; }

        /// <summary>
        /// Name used for AI response messages
        /// </summary>
        String AgentName { get; set; }

        /// <summary>
        /// Name used for the AI when speaking (updated from AgentName when that is changed)
        /// </summary>
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

        /// <summary>
        /// The names that are spoken by the speech engine, "{0}" is replaced with the AgentSpeechName
        /// </summary>
        String[] SpeechNames { get; set; }

        /// <summary>
        /// The voices available for speech
        /// </summary>
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

        /// <summary>
        /// Lock held while a query is run in the chat (only one query at a time)
        /// </summary>
        AsyncLock ChatQueryLock { get; }

        /// <summary>
        /// If true, tool call messages should be removed from the history when done (not currently read by the library)
        /// </summary>
        bool AutoRemoveToolCalls { get; set; }

        /// <summary>
        /// The auth required to join the chat
        /// </summary>
        IReadOnlyList<String> JoinAuth { get; }

        /// <summary>
        /// The auth required to clear the chat
        /// </summary>
        IReadOnlyList<String> ClearAuth { get; }

        /// <summary>
        /// The messages of the chat, in order
        /// </summary>
        List<Chat.ChatMessage> Messages { get; }

        /// <summary>
        /// The messages of the chat by message id
        /// </summary>
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
