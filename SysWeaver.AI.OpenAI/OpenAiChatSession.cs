using Newtonsoft.Json;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Docs;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{


    public sealed class OpenAiChatSession : OpenAiQuerySession
    {

        public event Action<OpenAiChatSession, String, HttpServerRequest> OnUserMessage;
        public event Action<OpenAiChatSession, String, HttpServerRequest> OnAiResponseCompleted;

        internal void InvokeOnUserMessageBegin(String body, HttpServerRequest request)
            => OnUserMessage?.Invoke(this, body, request);

        internal void InvokeAiResponseCompleted(String body, HttpServerRequest request)
            => OnAiResponseCompleted?.Invoke(this, body, request);

        public String ThinkMessage = "🤔 thinking..";


        readonly AsyncLock ChatLock;




        internal OpenAiChatSession(bool isPrivate, ChatClient c, String model, IOpenAiToolCache toolCache, String joinAuth, String clearAuth, PerfMonitor monitor, AsyncLock chatLock)
            : base(c, model, toolCache, monitor)
        {
            IsPrivate = isPrivate;
            ChatLock = chatLock;
            AgentName = model;
            AgentImageUrl = "../openAI/icons/openai.svg";
            ErrorImageUrl = "../openAI/icons/error.svg";
            DebugImageUrl = "../openAI/icons/debug.svg";

            JoinAuth = Authorization.GetRequiredTokens(joinAuth);
            ClearAuth = Authorization.GetRequiredTokens(clearAuth);

            foreach (var x in OpenAiTools.DebugItems)
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

        internal readonly ConcurrentDictionary<String, OpenAiCommand> Commands = new(StringComparer.Ordinal);

        public void AddCommand(String name, Func<String, OpenAiChatSession, HttpServerRequest, Task<Chat.ChatMessage>> func, String args, String desc, params String[] auths)
        {
            Commands.TryAdd(name, new OpenAiCommand(name, auths, func, args, desc));
        }

        Task<Chat.ChatMessage> ListTools(String args, OpenAiChatSession s, HttpServerRequest r)
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
                p.Append("- **").Append(OpenAiTools.MdEscape(t.Name)).Append("**");
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
                            p.Append(" *").Append(OpenAiTools.MdEscape(st)).Append("*");
                        }
                    }
                }
                /*                var d = t.Desc;
                                if (String.IsNullOrEmpty(d))
                                    p.AppendLine();
                                else
                                    p.Append(" **\\-** ").AppendLine(OpenAiTools.MdEscape(d));
                */
                p.AppendLine();
            }
            return Task.FromResult(new Chat.ChatMessage
            {
                Text = p.ToString(),
                Format = Chat.ChatMessageFormats.MarkDown,
            });
        }

        Task<Chat.ChatMessage> HelpTool(String args, OpenAiChatSession s, HttpServerRequest r)
        {
            if (String.IsNullOrEmpty(args))
                throw new Exception("Expected a tool name as argument!");
            if (!Tools.TryGetValue(args, out var t))
                throw new Exception(args.ToQuoted() + " is an unknown tool!");
            StringBuilder p = new StringBuilder();
            p.Append("## ").AppendLine(OpenAiTools.MdEscape(t.Name));
            var tt = t.Tool;
            var desc = tt.FunctionDescription;
            if (!String.IsNullOrEmpty(desc))
                p.Append(OpenAiTools.MdEscape(desc)).AppendLine(" ");
            var fp = tt.FunctionParameters?.ToString();
            if (!String.IsNullOrEmpty(fp))
                p.Append(String.Join(OpenAiTools.BeautifyJson(fp), "```json\n", "\n```"));
            return Task.FromResult(new Chat.ChatMessage
            {
                Text = p.ToString(),
                Format = Chat.ChatMessageFormats.MarkDown,
            });
        }

        public bool TryGetToolCommand(string name, out OpenAiCommand cmd)
        {
            cmd = null;
            if (!Tools.TryGetValue(name, out var tool))
                return false;
            cmd = tool.AsCommand;
            return true;
        }

        long AddOrder;

        public bool AddToMenu(bool toMessageMenu, Chat.ChatMenuItem item, SetVarDel setFunc = null, Chat.ChatMenuItem parent = null)
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

        public delegate Task<bool> SetVarDel(Chat.IChatController c, String providerChatId, Chat.ChatScopes scope, HttpSession session, long messageId, String value);

        readonly ConcurrentDictionary<String, Tuple<long, Chat.ChatMenuItem, SetVarDel, Chat.ChatMenuItem, bool>> Values = new ConcurrentDictionary<string, Tuple<long, Chat.ChatMenuItem, SetVarDel, Chat.ChatMenuItem, bool>>(StringComparer.Ordinal);

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
            if (!Enum.TryParse<OpenAiDebugInfo>(value, out var v))
                throw new Exception("Invalid value " + value.ToQuoted());
            Chat.ChatMessage m;
            lock (Messages)
            {
                MessageLookup.TryGetValue(messageId, out m);
                if (m == null)
                    throw new Exception("Message #" + messageId + " not found!");
            }
            var val = m.GetData(OpenAiTools.DebugKey) as OpenAiDebugMessage;
            if (val == null)
                throw new Exception("No debug data for message!");
            var message = val.Get(v);
            if (String.IsNullOrEmpty(message))
                throw new Exception("No debug data for message!");

            m = new Chat.ChatMessage
            {
                Id = Interlocked.Increment(ref MsgId),
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
            exclude = exclude ?? OpenAiTools.EmptyStringSet;
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
            var a = session.Auth;
            foreach (var x in v.Where(y => y.Value.Item4 == null).OrderBy(y => y.Value.Item1))
            {
                if (x.Value.Item5 == isMessage)
                    AddRec(ref chatMenuItems, x.Value.Item2);
            }
            return chatMenuItems;
        }

        #endregion//Menu

        public readonly bool IsPrivate;


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
                AgentSpeechName = OpenAiTools.FilterSpeechName(value);
            }
        }

        public String AgentSpeechName = "AI";

        String InternalAgentName;

        /// <summary>
        /// Name used for error messages
        /// </summary>
        public String ErrorName = "Error";

        /// <summary>
        /// Url to image used for AI response messages
        /// </summary>
        public String AgentImageUrl;

        /// <summary>
        /// Url to image used for error messages
        /// </summary>
        public String ErrorImageUrl;

        /// <summary>
        /// Url to image used for debug messages
        /// </summary>
        public String DebugImageUrl;


        /// <summary>
        /// If true, speech should be enabled by default
        /// </summary>
        public bool EnableSpeechByDefault;

        /// <summary>
        /// If true, the user may input markdown text (client is allowed to send the message with the MarkDown format).
        /// </summary>
        public bool AllowUserMarkDown = true;

        /// <summary>
        /// Allow storing files and links on the server (requires a UserStore).
        /// </summary>
        public bool AllowStore = true;

        /// <summary>
        /// If true, the server supports message translation (to the users language)
        /// </summary>
        public bool CanTranslate = true;

        /// <summary>
        /// If true, enable the menu option to show a user profile
        /// </summary>
        public bool CanShowProfile;

        /// <summary>
        /// If true a user may upload files
        /// </summary>
        public bool CanUpload = true;

        /// <summary>
        /// The image to use when the AI is working
        /// </summary>
        public String WorkingImageUrl = "../openAI/icons/working.svg";



        public String[] SpeechNames =
            [
                "{0}",
                "All|-",
            ];

        public Chat.ChatVoice[] Voices = [
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


        /// <summary>
        /// The function used to get the system prompt
        /// </summary>
        public Func<HttpSession, String> GetSystemPrompt;

        /// <summary>
        /// The function used to format a user message.
        /// Parameters are: session, user name, original message.
        /// </summary>
        public Func<HttpSession, String, String, String> FormatUserMessage;


        public readonly List<ChatMessage> ApiMessages = new List<ChatMessage>();

        IEnumerable<ChatMessage> EnumMessages(HttpSession s)
        {
            var gs = GetSystemPrompt;
            if (gs != null)
                SystemPrompt = gs(s);
            var m = MsgSystemPrompt;
            if (m != null)
                yield return m;
            foreach (var x in ApiMessages)
                yield return x;
        }


        public readonly AsyncLock ChatQueryLock = new AsyncLock();

        //internal readonly HashSet<String> SeenUsers = new HashSet<string>(StringComparer.Ordinal);


        public async Task<String> Complete(String text, HttpServerRequest request, OpenAiDebugMessage debug = null, String from = null, Func<String, long, long, Task> onUsage = null)
        {
            using var _a = Monitor?.Track(nameof(Complete));
            var messages = ApiMessages;
            var um = new UserChatMessage(text);
            if (!String.IsNullOrEmpty(from))
                um.ParticipantName = from;
            messages.Add(um);
            ChatCompletionOptions options = new ChatCompletionOptions();
            if (HaveTemperature)
                options.Temperature = Temperature;
            var tools = Tools;
            if (tools.Count > 0)
            {
                var d = options.Tools;
                foreach (var x in tools)
                    d.Add(x.Value.Tool);
                options.AllowParallelToolCalls = SupportParallelToolCalls;
            }
            var session = request.Session;
            long totalIn = 0;
            long totalOut = 0;
            var model = Model;
            for (; ; )
            {
                var mm = EnumMessages(session);
                ClientResult<ChatCompletion> r;
                using (var _b = await (ChatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
                {
                    using var _c = Monitor?.Track(nameof(Client.CompleteChatAsync));
                    r = await Client.CompleteChatAsync(mm, options).ConfigureAwait(false);
                }
                var v = r.Value;
                var e = v.Refusal;
                var usage = v.Usage;
                if (usage != null)
                {
                    totalIn += usage.InputTokenCount;
                    totalOut += usage.OutputTokenCount;
                }
                if (!String.IsNullOrEmpty(e))
                {
                    if (onUsage != null)
                        await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                    throw new Exception("Model refused to complete: " + e);
                }
                var fr = v.FinishReason;
                switch (fr)
                {
                    case ChatFinishReason.Stop:
                        messages.Add(new AssistantChatMessage(r));
                        var sb = new StringBuilder();
                        List<String> p = new List<string>(4);
                        foreach (var x in v.Content)
                        {
                            switch (x.Kind)
                            {
                                case ChatMessageContentPartKind.Text:
                                    var t = x.Text;
                                    sb.AppendLine(t);
                                    break;
                                case ChatMessageContentPartKind.Image:
                                    break;

                            }
                        }
                        if (onUsage != null)
                            await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                        return sb.ToString();
                    case ChatFinishReason.ToolCalls:
                        messages.Add(new AssistantChatMessage(r));
                        var tcs = v.ToolCalls;
                        if (tcs != null)
                            await AiCalls(tcs, request, debug, null, ApiMessages).ConfigureAwait(false);
                        break;
                    case ChatFinishReason.Length:
                        if (onUsage != null)
                            await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                        return "Error: Incomplete model output due to MaxTokens parameter or token limit exceeded.";

                    case ChatFinishReason.ContentFilter:
                        if (onUsage != null)
                            await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                        return "Error: Omitted content due to a content filter flag.";

                    case ChatFinishReason.FunctionCall:
                        if (onUsage != null)
                            await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                        return "Error: Deprecated in favor of tool calls.";
                    default:
                        if (onUsage != null)
                            await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                        return "Error: " + fr;

                }

            }
        }


        public async Task<String> CompleteUpdate(String text,
            IReadOnlyList<ChatMessageContentPart> extraData,
            HttpServerRequest request, 
            Func<String, String, Task> onUpdate, 
            Func<String, String, String, String> saveFile,
            Func<Object, String> saveData,
            Action<String> onTools, 
            OpenAiDebugMessage debug, 
            String from,
            Func<String, long, long, Task> onUsage
            )
        {
            using var _a = Monitor?.Track(nameof(CompleteUpdate));
            var messages = ApiMessages;
            var um = new UserChatMessage(text);
            foreach (var x in extraData.Nullable())
                um.Content.Add(x);
            if (!String.IsNullOrEmpty(from))
                um.ParticipantName = from;
            messages.Add(um);
            ChatCompletionOptions options = new ChatCompletionOptions();
            if (HaveTemperature)
                options.Temperature = Temperature;
            StringBuilder link = new StringBuilder();
            var tools = Tools;
            if (tools.Count > 0)
            {
                var d = options.Tools;
                foreach (var x in tools)
                    d.Add(x.Value.Tool);
                options.AllowParallelToolCalls = SupportParallelToolCalls;
                if (request != null)
                    request.Properties[OpenAiToolExt.RequestAiToolContext] = new OpenAiToolContext(this, link, saveFile, saveData);
                //options.ToolChoice = ChatToolChoice.CreateAutoChoice();
            }
            long totalIn = 0;
            long totalOut = 0;
            StringBuilder sb = new StringBuilder();
            var firstMessage = messages.Count;
            var session = request.Session;
            var client = Client;
            var model = Model;
            for (; ; )
            {
                StringBuilder e = new StringBuilder();
                Dictionary<String, Tuple<int, String>> toolCallIndices = new Dictionary<string, Tuple<int, string>>(StringComparer.Ordinal);
                List<StringBuilder> toolCalls = new List<StringBuilder>();
                var mm = EnumMessages(session);
                using (var _ = await (ChatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
                {
                    using var _b = Monitor?.Track(nameof(Client.CompleteChatStreaming));
                    //                    await foreach (var sp in Client.CompleteChatStreamingAsync(mm, options))
                    ChatTokenUsage usage = null;
                    foreach (var sp in client.CompleteChatStreaming(mm, options))
                    {
                        usage = usage ?? sp.Usage;
                        if (!String.IsNullOrEmpty(sp.RefusalUpdate))
                            e.Append(sp.RefusalUpdate);
                        if (sp.ContentUpdate != null)
                        {
                            bool changed = false;
                            foreach (var x in sp.ContentUpdate)
                            {
                                switch (x.Kind)
                                {
                                    case ChatMessageContentPartKind.Text:
                                        var t = x.Text;
                                        if (!String.IsNullOrEmpty(t))
                                        {
                                            sb.Append(x.Text);
                                            changed = true;
                                        }
                                        break;
                                }
                            }
                            if (changed)
                                await onUpdate(sb.ToString(), link.Length > 0 ? link.ToString() : null).ConfigureAwait(false);
                        }
                        var tcs = sp.ToolCallUpdates;
                        if (tcs != null)
                        {
                            foreach (var x in tcs)
                            {
                                var tid = x.ToolCallId;
                                var index = x.Index;
                                if (tid != null)
                                {
                                    if (!toolCallIndices.TryGetValue(tid, out var call))
                                    {
                                        call = new Tuple<int, string>(index, x.FunctionName);
                                        toolCallIndices.Add(tid, call);
                                    }
                                }
                                while (index >= toolCalls.Count)
                                    toolCalls.Add(new StringBuilder());



                                if (x.FunctionArgumentsUpdate != null)
                                {
                                    using (var ts = x.FunctionArgumentsUpdate.ToStream())
                                        if (ts.Length <= 0)
                                            continue;
                                    toolCalls[index].Append(x.FunctionArgumentsUpdate.ToString());
                                }
                            }
                        }
                    }
                
                    if (usage != null)
                    {
                        totalIn += usage.InputTokenCount;
                        totalOut += usage.OutputTokenCount;
                    }
                }
                if (e.Length > 0)
                {
                    if (onUsage != null)
                        await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                    throw new Exception("Model refused to complete: " + e.ToString());
                }
                if (sb.Length > 0)
                {
                    var ext = sb.ToString();
                    messages.Add(new AssistantChatMessage(ext));
                }
                var tcl = toolCalls.Count;
                if (tcl <= 0)
                    break;
                ChatToolCall[] newCalls = new ChatToolCall[tcl];
                foreach (var x in toolCallIndices)
                {
                    var val = x.Value;
                    var index = val.Item1;
                    var vb = toolCalls[index];
                    newCalls[index] = ChatToolCall.CreateFunctionToolCall(x.Key, val.Item2, vb.Length > 0 ? BinaryData.FromString(vb.ToString()) : null);
                }
                messages.Add(new AssistantChatMessage(newCalls));
                var l = link.Length;
                await AiCalls(newCalls, request, debug, onTools, ApiMessages).ConfigureAwait(false);
                if (link.Length != l)
                    await onUpdate(sb.ToString(), link.Length > 0 ? link.ToString() : null).ConfigureAwait(false);
                onTools?.Invoke(null);
            }
            //  Remove tool call messages
            if (AutoRemoveToolCalls)
            {
                int output = firstMessage;
                var ml = messages.Count;
                for (int i = firstMessage; i < ml; ++i)
                {
                    var m = messages[i] as AssistantChatMessage;
                    if (m == null)
                        continue;
                    if (m.ToolCalls.Count > 0)
                        continue;
                    messages[output] = m;
                    ++output;
                }
                if (output < ml)
                    messages.RemoveRange(output, ml - output);
            }
            if (onUsage != null)
                await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
            if ((sb.Length > 0) || (link.Length > 0))
                return null;
            return "No text response!";
        }

        public bool AutoRemoveToolCalls;

        public readonly IReadOnlyList<String> JoinAuth;
        public readonly IReadOnlyList<String> ClearAuth;
        public readonly List<Chat.ChatMessage> Messages = new List<Chat.ChatMessage>();
        public readonly Dictionary<long, Chat.ChatMessage> MessageLookup = new Dictionary<long, Chat.ChatMessage>();

        public long MsgId;




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
    }

}
