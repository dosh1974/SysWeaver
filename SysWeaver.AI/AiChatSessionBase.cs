using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Net;

namespace SysWeaver.AI
{

    /// <summary>
    /// API agnostic base class for chat sessions.
    /// Handles menus, commands, appearance, messages, system prompt building etc.
    /// Derived classes implements the actual API calls.
    /// </summary>
    public abstract class AiChatSessionBase : AiQuerySessionBase, IAiChatSession
    {

        public event Action<IAiChatSession, String, HttpServerRequest> OnUserMessage;
        public event Action<IAiChatSession, String, HttpServerRequest> OnAiResponseCompleted;

        internal void InvokeOnUserMessageBegin(String body, HttpServerRequest request)
            => OnUserMessage?.Invoke(this, body, request);

        internal void InvokeAiResponseCompleted(String body, HttpServerRequest request)
            => OnAiResponseCompleted?.Invoke(this, body, request);

        public String ThinkMessage { get; set; } = "🤔 thinking..";

        /// <summary>
        /// Optional lock used to limit the number of concurrent requests to the API
        /// </summary>
        protected readonly AsyncLock ChatLock;

        /// <summary>
        /// Create a chat session
        /// </summary>
        /// <param name="isPrivate">If true, this chat is user only</param>
        /// <param name="model">The model used</param>
        /// <param name="haveTemperature">True if the model supports setting the temperature</param>
        /// <param name="supportSystemRole">True if the model supports a system prompt (system role)</param>
        /// <param name="supportParallelToolCalls">True if the model supports parallel tool calls, null if unknown</param>
        /// <param name="toolCache">The tool cache</param>
        /// <param name="joinAuth">Comma separated list of required auth tokens</param>
        /// <param name="clearAuth">Auth required to clear the chat session</param>
        /// <param name="monitor">Optional performance monitor</param>
        /// <param name="chatLock">Optional lock used to limit the number of concurrent requests to the API</param>
        /// <param name="memory">Optional memory implementation</param>
        /// <param name="agentIcon">The filename of the agent icon (in the icon root), ex: "openai.svg"</param>
        protected AiChatSessionBase(bool isPrivate, String model, bool haveTemperature, bool supportSystemRole, bool? supportParallelToolCalls, IAiToolCache toolCache, String joinAuth, String clearAuth, PerfMonitor monitor, AsyncLock chatLock, IAiMemory memory, String agentIcon)
            : base(model, haveTemperature, supportSystemRole, supportParallelToolCalls, toolCache, monitor, memory)
        {
            IsPrivate = isPrivate;
            ChatLock = chatLock;
            AgentName = model;
            var iconRoot = AiTools.IconRoot;
            AgentImageUrl = iconRoot + agentIcon;
            ErrorImageUrl = iconRoot + "error.svg";
            DebugImageUrl = iconRoot + "debug.svg";
            WorkingImageUrl = iconRoot + "working.svg";

            JoinAuth = Authorization.GetRequiredTokens(joinAuth);
            ClearAuth = Authorization.GetRequiredTokens(clearAuth);

            foreach (var x in AiTools.DebugItems)
            {
                var y = x.Clone();
                AddToMenu(true, y, SendFunctionDebug);
            }
            //  Register tools:
            AddCommand("tools", ListTools, null, "List all tools available to the AI");
            AddCommand("tool", HelpTool, "<tool name>", "Get information about a tool");
        }


        public String ChatId { get; internal set;  }
        public override string ToString() => ChatId;

        #region Menu

        internal readonly ConcurrentDictionary<String, AiCommand> Commands = new(StringComparer.Ordinal);

        public void AddCommand(String name, Func<String, IAiChatSession, HttpServerRequest, Task<Chat.ChatMessage>> func, String args, String desc, params String[] auths)
        {
            Commands.TryAdd(name, new AiCommand(name, auths, func, args, desc));
        }

        Task<Chat.ChatMessage> ListTools(String args, IAiChatSession s, HttpServerRequest r)
        {
            StringBuilder p = new StringBuilder();
            p.AppendLine("Available tools in this session:");
            var session = r.Session;
            var haveDebug = session.IsValid(["debug"]);
            foreach (var x in Tools.OrderBy(x => x.Key))
            {
                var t = x.Value;
                var a = t.Auth;
                if (!haveDebug)
                    if (!session.IsValid(a))
                        continue;
                p.Append("- **").Append(AiTools.MdEscape(t.Name));
                var icon = t.Icon;
                if (!String.IsNullOrEmpty(icon))
                    p.Append(" [").Append(AiTools.MdEscape(icon)).Append(']');
                p.Append("**");
                if (haveDebug)
                {
                    if (a != null)
                    {
                        if (a.Count == 0)
                        {
                            p.AppendLine(" *user*");
                        }
                        else
                        {
                            var st = String.Join(String.Join("] [", a), '[', ']');
                            p.Append(" *").Append(AiTools.MdEscape(st)).Append("*");
                        }
                    }
                }
                p.AppendLine();
            }
            return Task.FromResult(new Chat.ChatMessage
            {
                Text = p.ToString(),
                Format = Chat.ChatMessageFormats.MarkDown,
            });
        }

        Task<Chat.ChatMessage> HelpTool(String args, IAiChatSession s, HttpServerRequest r)
        {
            if (String.IsNullOrEmpty(args))
                throw new Exception("Expected a tool name as argument!");
            if (!Tools.TryGetValue(args, out var t))
                throw new Exception(args.ToQuoted() + " is an unknown tool!");
            StringBuilder p = new StringBuilder();
            p.Append("## ").Append(AiTools.MdEscape(t.Name));
            var icon = t.Icon;
            if (!String.IsNullOrEmpty(icon))
                p.Append(" [").Append(AiTools.MdEscape(icon)).Append(']');
            p.AppendLine();
            var tt = t.Tool;
            var desc = tt.FunctionDescription;
            if (!String.IsNullOrEmpty(desc))
                p.Append(AiTools.MdEscape(desc)).AppendLine("  ");
            var fp = tt.FunctionParameters?.ToString();
            if (!String.IsNullOrEmpty(fp))
                p.Append(String.Join(AiTools.BeautifyJson(fp), "\n```json\n", "\n```\n"));
            return Task.FromResult(new Chat.ChatMessage
            {
                Text = p.ToString(),
                Format = Chat.ChatMessageFormats.MarkDown,
            });
        }

        public bool TryGetToolCommand(string name, out AiCommand cmd)
        {
            cmd = null;
            if (!Tools.TryGetValue(name, out var tool))
                return false;
            cmd = tool.AsCommand;
            return true;
        }

        long AddOrder;

        public bool AddToMenu(bool toMessageMenu, Chat.ChatMenuItem item, IAiChatSession.SetVarDel setFunc = null, Chat.ChatMenuItem parent = null)
        {
            if (!Values.TryAdd(item.Id, Tuple.Create(Interlocked.Increment(ref AddOrder), item, setFunc, parent, toMessageMenu)))
                return false;
#if DEBUG
            if (item.Children != null)
                throw new Exception("Can't have children! (must call AddToMenu with a parent that was already added instead)");
#endif//DEBUG
            if (parent == null)
                return true;
#if DEBUG
            if (parent == item)
                throw new Exception("Parent can't be same as item!");
            if (!Values.TryGetValue(parent.Id, out var x))
                throw new Exception("Parent not found");
            if (x.Item2 != parent)
                throw new Exception("Parent not found");
#endif//DEBUG
            parent.Children = parent.Children.Push(item);
            return true;
        }

        readonly ConcurrentDictionary<String, Tuple<long, Chat.ChatMenuItem, IAiChatSession.SetVarDel, Chat.ChatMenuItem, bool>> Values = new ConcurrentDictionary<string, Tuple<long, Chat.ChatMenuItem, IAiChatSession.SetVarDel, Chat.ChatMenuItem, bool>>(StringComparer.Ordinal);

        public Task<bool> SetValue(Chat.IChatController c, String providerChatId, Chat.ChatScopes scope, HttpSession session, long messageId, String key, String value)
        {
            if (!Values.TryGetValue(key, out var v))
                throw new Exception("Variable " + key.ToQuoted() + " is unknown!");
            var fn = v.Item3;
            if (fn == null)
                throw new Exception("Variable " + key.ToQuoted() + " is invalid!");
            var ia = v.Item2.Auth;
            var a = session.Auth;
            if (a == null)
            {
                if (ia != null)
                    throw new Exception("No authorized to change value " + key.ToQuoted());
            }
            else
            {
                if (!a.IsValid(ia))
                    throw new Exception("No authorized to change value " + key.ToQuoted());
            }
            return fn(c, providerChatId, scope, session, messageId, value);
        }

        Task<bool> SendFunctionDebug(Chat.IChatController c, String providerChatId, Chat.ChatScopes scope, HttpSession session, long messageId, String value)
        {
            if (!Enum.TryParse<AiDebugInfo>(value, out var v))
                throw new Exception("Invalid value " + value.ToQuoted());
            Chat.ChatMessage m;
            lock (Messages)
            {
                MessageLookup.TryGetValue(messageId, out m);
                if (m == null)
                    throw new Exception("Message #" + messageId + " not found!");
            }
            var val = m.GetData(AiTools.DebugKey) as AiDebugMessage;
            if (val == null)
                throw new Exception("No debug data for message!");
            var message = val.Get(v);
            if (String.IsNullOrEmpty(message))
                throw new Exception("No debug data for message!");

            m = new Chat.ChatMessage
            {
                Id = NextMsgId(),
                From = AgentName + " debug",
                FromImage = DebugImageUrl,
                Text = message,
                Time = DateTime.UtcNow,
                Lang = "en",
                Format = Chat.ChatMessageFormats.MarkDown,
            };
            m.SetTo(session?.Auth?.Guid);
            c.PostMessage(providerChatId, m, session, scope);
            lock (Messages)
            {
                Messages.Add(m);
                MessageLookup[m.Id] = m;
            }
            return TaskExt.TrueTask;
        }


        public Chat.ChatMenuItem[] GetMenu(HttpSession session, bool isMessage, IReadOnlySet<String> exclude = null)
        {
            exclude = exclude ?? AiTools.EmptyStringSet;
            var v = Values;
            var auth = session.Auth;
            Chat.ChatMenuItem[] chatMenuItems = null;
            void AddRec(ref Chat.ChatMenuItem[] items, Chat.ChatMenuItem item)
            {
                if (exclude.Contains(item.Id))
                    return;
                var ia = item.Auth;
                if (auth == null)
                {
                    if (ia != null)
                        return;
                } else
                {
                    if (!auth.IsValid(ia))
                        return;
                }
                var cc = item.Children;
                item = item.Clone(false);
                items = items.Push(item);
                if (cc == null)
                    return;
                foreach (var c in cc)
                    AddRec(ref item.Children, c);
            }
            foreach (var x in v.Where(y => y.Value.Item4 == null).OrderBy(y => y.Value.Item1))
            {
                if (x.Value.Item5 == isMessage)
                    AddRec(ref chatMenuItems, x.Value.Item2);
            }
            return chatMenuItems;
        }

        #endregion//Menu

        #region Appearance

        public bool IsPrivate { get; }


        /// <summary>
        /// Name used for AI response messages
        /// </summary>
        public String AgentName
        {
            get => InternalAgentName;
            set
            {
                if (value.FastEquals(InternalAgentName))
                    return;
                InternalAgentName = value;
                AgentSpeechName = AiTools.FilterSpeechName(value);
            }
        }

        public String AgentSpeechName { get; set; } = "AI";

        String InternalAgentName;

        /// <summary>
        /// Name used for error messages
        /// </summary>
        public String ErrorName { get; set; } = "Error";

        /// <summary>
        /// Url to image used for AI response messages
        /// </summary>
        public String AgentImageUrl { get; set; }

        /// <summary>
        /// Url to image used for error messages
        /// </summary>
        public String ErrorImageUrl { get; set; }

        /// <summary>
        /// Url to image used for debug messages
        /// </summary>
        public String DebugImageUrl { get; set; }


        /// <summary>
        /// If true, speech should be enabled by default
        /// </summary>
        public bool EnableSpeechByDefault { get; set; }

        /// <summary>
        /// If true, the user may input markdown text (client is allowed to send the message with the MarkDown format).
        /// </summary>
        public bool AllowUserMarkDown { get; set; } = true;

        /// <summary>
        /// Allow storing files and links on the server (requires a UserStore).
        /// </summary>
        public bool AllowStore { get; set; } = true;

        /// <summary>
        /// If true, the server supports message translation (to the users language)
        /// </summary>
        public bool CanTranslate { get; set; } = true;

        /// <summary>
        /// If true, enable the menu option to show a user profile
        /// </summary>
        public bool CanShowProfile { get; set; }

        /// <summary>
        /// If true a user may upload files
        /// </summary>
        public bool CanUpload { get; set; } = true;

        /// <summary>
        /// The image to use when the AI is working
        /// </summary>
        public String WorkingImageUrl { get; set; }



        public String[] SpeechNames { get; set; } =
            [
                "{0}",
                "All|-",
            ];

        public Chat.ChatVoice[] Voices { get; set; } = [
                new Chat.ChatVoice
                {
                    Name = "{0}",
                    Language = "en-GB",
                    Pitch = 1.1f,
                    Rate = 1.25f,
                },
                new Chat.ChatVoice
                {
                    Name = "{1}",
                    Language = "en-GB",
                    Rate = 1.25f,
                    Male = true,
                },
            ];

        #endregion//Appearance

        #region System prompt

        /// <summary>
        /// The function used to get the system prompt
        /// </summary>
        public Func<HttpSession, String> GetSystemPrompt { get; set; }

        /// <summary>
        /// The function used to format a user message.
        /// Parameters are: session, user name, original message.
        /// </summary>
        public Func<HttpSession, String, String, String> FormatUserMessage { get; set; }

        const String MemSection = """
                                  ## MEMORY  
                                  """;



        const String MemHeader =    """

                                    This is a list of all key / value pairs you have previously stored about this user
                                    
                                    | Key | Last used | Short description |
                                    |-----|-----------|-------------------|
                                    """;

        const String MemInstruction =   """
                                        Please store / retrieve whatever you see fit to improve the user experience.  
                                        Make sure to store key information about a user to make the experience more personal.  
                                        Things to store: personal details, recent discussed items and so on.  
                                        Make sure to update the memory frequently, call AddMemory / SetMemory whenever a discussion changes or other valuable information is revealed / added.
                                        Consult stored memory when in doubt.  
                                        Use a consistent key naming to avoid unnecessary memory retrieval.  

                                        Memory can be modified using the following tools:  
                                        - AddMemory to add a NEW memory.  
                                        - GetMemory  
                                        - SetMemory to update an EXISTING memory.   
                                        - RemoveMemory  
                                          
                                        The memory stored with key "Global" will be available in the system prompt, keep frequent used, personal memories there.
                                        """;

        const String MemGlobal =    """
                                    ### GLOBAL MEMORY 
                                    This is the content of the global memory for this user:
                                    """;

        const String RestAPI = """
                                    ### REST API  
                                    Tools that have a REST API url in their description can be invoked using a JSON post to that url (response is JSON).  
                                    The url in the tool is relative to the site root, so the site root must be prepended.
                                    Never assume property names, always read the tool descriptions.  
                                    The site root is *[ROOT]*.  
                                    ALWAYS use the full absolute url when invoking any API.  
                                    Make sure to respect the casing of properties.  

                                    Table reference rule:  
                                    - Check if API's return a table data reference or a table data.  
                                    - If it's a table reference, call the GetTableData API to get the  ACTUAL table data.  

                                    REST request body rule:
                                    - For REST API calls, remove the outer tool-function input wrapper from the request schema.
                                    - Example: if a tool’s input is `{ "queryParams": { ... } }`, POST only `{ ... }`, not `{ "queryParams": { ... } }`.
                                    
                                    Verification before delivery:
                                    - For generated HTML or other code that calls APIs, validate every endpoint path, request-body shape, response property name, and table-reference flow against the function descriptions and/or an actual successful tool response.  
                                    - ALWAYS call the ValidateRestApiCall tool, to validate your code.  
                                    - Whenever a fix is requested, regenerate the artifact and immediately display the corrected version.  

                                    """;

        /// <summary>
        /// Build the complete system prompt (user supplied prompt, memory and REST API instructions)
        /// </summary>
        /// <param name="s">The http session</param>
        /// <returns>The system prompt</returns>
        protected internal async Task<String> BuildSystemPrompt(HttpSession s)
        {
            var gs = GetSystemPrompt;
            var p = gs == null ? null : gs(s);
            var mem = Memory;
            if (mem != null)
            {
                var r = await mem.GetMemoryEntries(s).ConfigureAwait(false);
                if ((r != null) && (r.Length > 0))
                {
                    var data = String.Join("\n", r.Select(x => x.MdTableRow));
                    var gmem = await mem.GetMemory(s, "Global").ConfigureAwait(false);

                    if (String.IsNullOrEmpty(p))
                    {
                        if (String.IsNullOrEmpty(gmem))
                            p = String.Concat(MemSection + "\n" + MemHeader, "\n", data, "\n\n" + MemInstruction);
                        else
                            p = String.Concat(MemSection + "\n" + MemHeader, "\n", data, "\n\n" + MemInstruction, "\n" + MemGlobal, "\n", gmem);
                    }
                    else
                    {
                        if (String.IsNullOrEmpty(gmem))
                            p = String.Concat(p, "\n\n", MemSection + "\n" + MemHeader, "\n", data, "\n\n" + MemInstruction);
                        else
                            p = String.Concat(p, "\n\n", MemSection + "\n" + MemHeader, "\n", data, "\n\n" + MemInstruction, "\n" + MemGlobal, "\n", gmem);
                    }
                }else
                {
                    if (String.IsNullOrEmpty(p))
                    {
                        p = MemSection + "\n" + MemInstruction;
                    }
                    else
                    {
                        p = String.Concat(p, "\n\n", MemSection + "\n" + MemInstruction);
                    }
                }
            }
            p = String.Concat(p, "\n\n", RestAPI.Replace("[ROOT]", StringTools.EscapeMD(s.SiteRoot)));
            return p;
        }

        #endregion//System prompt

        #region Chat

        public AsyncLock ChatQueryLock { get; } = new AsyncLock();

        /// <summary>
        /// Add a user message to the conversation and get the AI response (tools are executed as needed).
        /// </summary>
        /// <param name="text">The user message</param>
        /// <param name="request">The request</param>
        /// <param name="debug">An optional obejct used to track tool calls etc</param>
        /// <param name="from">Optional name of the user</param>
        /// <param name="onUsage">An optional function to call when token in/out usage is changed</param>
        /// <returns>The AI response</returns>
        public abstract Task<String> Complete(String text, HttpServerRequest request, AiDebugMessage debug = null, String from = null, Func<String, long, long, Task> onUsage = null);

        /// <summary>
        /// Add a user message to the conversation and stream the AI response (tools are executed as needed).
        /// </summary>
        /// <returns>null if successful, else an error message</returns>
        public abstract Task<String> CompleteUpdate(String text,
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

        /// <summary>
        /// Clear the conversation history sent to the API
        /// </summary>
        public abstract void ClearApiMessages();

        /// <summary>
        /// Write the tools and messages (including the system prompt) as json properties (the containing object is written by the caller).
        /// </summary>
        /// <param name="writer">The writer to use</param>
        protected abstract void WriteConversation(Utf8JsonWriter writer);

        /// <summary>
        /// Set the tool context of a request (used by tools to add links, files, data etc to the response message).
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="link">Links added by tools are appended here (separated by ';')</param>
        /// <param name="saveFile">Function used to save a file (mime, data, filename) returns an url</param>
        /// <param name="saveData">Function used to save some data, returns an id</param>
        protected void SetToolContext(HttpServerRequest request, StringBuilder link, Func<String, String, String, String> saveFile, Func<Object, String> saveData)
        {
            if (request != null)
                request.Properties[OpenAiToolExt.RequestAiToolContext] = new AiToolContext(this, link, saveFile, saveData);
        }

        /// <summary>
        /// Append the urls of any attachments to the user message text (so that the AI can refer to them)
        /// </summary>
        /// <param name="text">The user message</param>
        /// <param name="extraData">The attachments</param>
        /// <returns>The text to send to the AI</returns>
        protected static String AppendAttachmentUrls(String text, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData)
        {
            if ((extraData?.Count ?? 0) <= 0)
                return text;
            return String.Concat(text, "\n\n\nThe supplied content can be accessed using the following URL's:\n * ", String.Join("\n * ", extraData.Select(x => StringTools.EscapeMD(x.Item2).ToQuoted())));
        }

        public bool AutoRemoveToolCalls { get; set; }

        public IReadOnlyList<String> JoinAuth { get; }
        public IReadOnlyList<String> ClearAuth { get; }
        public List<Chat.ChatMessage> Messages { get; } = new List<Chat.ChatMessage>();
        public Dictionary<long, Chat.ChatMessage> MessageLookup { get; } = new Dictionary<long, Chat.ChatMessage>();

        long InternalMsgId;

        public long MsgId => Interlocked.Read(ref InternalMsgId);

        public long NextMsgId() => Interlocked.Increment(ref InternalMsgId);

        #endregion//Chat

        #region Properties

        readonly ConcurrentDictionary<String, Object> Props = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);

        internal void SetProperty<T>(String key, T value) => Props[key] = value;

        internal bool TryGetProperty<T>(String key, out T value)
        {
            if (!Props.TryGetValue(key, out var x))
            {
                value = default;
                return false;
            }
            value = (T)x;
            return true;
        }

        internal bool TryRemoveProperty<T>(String key, out T value)
        {
            if (!Props.TryRemove(key, out var x))
            {
                value = default;
                return false;
            }
            value = (T)x;
            return true;
        }

        #endregion//Properties

        #region IAiChatSession (internal members)

        String IAiChatSession.ChatId { get => ChatId; set => ChatId = value; }
        void IAiChatSession.InvokeOnUserMessageBegin(String body, HttpServerRequest request) => InvokeOnUserMessageBegin(body, request);
        void IAiChatSession.InvokeAiResponseCompleted(String body, HttpServerRequest request) => InvokeAiResponseCompleted(body, request);
        ConcurrentDictionary<String, AiCommand> IAiChatSession.Commands => Commands;
        Task<String> IAiChatSession.BuildSystemPrompt(HttpSession s) => BuildSystemPrompt(s);
        void IAiChatSession.WriteConversation(Utf8JsonWriter writer) => WriteConversation(writer);
        void IAiChatSession.SetProperty<T>(String key, T value) => SetProperty(key, value);
        bool IAiChatSession.TryGetProperty<T>(String key, out T value) => TryGetProperty(key, out value);
        bool IAiChatSession.TryRemoveProperty<T>(String key, out T value) => TryRemoveProperty(key, out value);

        #endregion//IAiChatSession (internal members)
    }

}
