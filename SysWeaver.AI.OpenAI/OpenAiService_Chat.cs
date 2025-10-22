using CommunityToolkit.HighPerformance;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Buffers;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Chat;
using SysWeaver.Data;
using SysWeaver.Media;
using SysWeaver.MicroService;
using SysWeaver.Net;
using SysWeaver.Serialization;
using TiktokenSharp;

namespace SysWeaver.AI
{
    public sealed partial class OpenAiService
    {

        /// <summary>
        /// The model used when supplying an empty model (can be configured)
        /// </summary>
        public readonly String DefaultChatModel;


        readonly AsyncLock ChatLock;

        /// <summary>
        /// Create a simple chat client (simple query / anwser)
        /// </summary>
        /// <param name="model">The gpt model to use, ex:
        /// "gpt-4o-mini" -	Our affordable and intelligent small model for fast, lightweight tasks.
        /// "gpt-4o" - Our high-intelligence flagship model for complex, multi-step tasks.
        /// "o1-preview" - Language models trained with reinforcement learning to perform complex reasoning.
        /// </param>
        /// <returns></returns>
        public ChatClient CreateChatClient(String model = null)
        {
            if (String.IsNullOrEmpty(model))
                model = DefaultChatModel;
            return new ChatClient(model, ApiKey, Options);
        }


        /// <summary>
        /// Create a chat session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="isPrivate">If true, this chat is user only</param>
        /// <param name="model">The gpt model to use, ex:
        /// "gpt-4o-mini" -	Our affordable and intelligent small model for fast, lightweight tasks.
        /// "gpt-4o" - Our high-intelligence flagship model for complex, multi-step tasks.
        /// "o1-preview" - Language models trained with reinforcement learning to perform complex reasoning.
        /// </param>
        /// <param name="joinAuth">Optional comma separated list of required auth tokens</param>
        /// <param name="clearAuth">Auth required to clear the chat session</param>
        /// <param name="defaultTools">true to add some default tools for the AI to use</param>
        /// <returns></returns>
        public OpenAiChatSession CreateChatSession(bool isPrivate, String model = null, String joinAuth = "", String clearAuth = "Admin", bool defaultTools = true)
        {
            if (String.IsNullOrEmpty(model))
                model = DefaultChatModel;
            var mon = PerfMon;
            var s = new OpenAiChatSession(isPrivate, new ChatClient(model, ApiKey, Options), model, this, joinAuth, clearAuth, mon, ChatLock);
            if (defaultTools)
            {
                AddTool_GetPredefinedImage(s);
                AddTool_GetFileExtensionIcon(s);
                AddTool_GetCountryFlagIcon(s);
                AddTool_BuildLogo(s);
                AddTool_BuildQrCode(s);
                AddTool_BuildTable(s);
                AddTool_BuildData(s);

                AddTool_GenerateImage(s);

                AddTool_DisplayUrl(s);
                AddTool_Calculate(s);
                AddTool_ElapsedTime(s);
                //AddTool_DisplayData(s);
                //AddTool_DisplayTable(s);

                AddTool_BuildGraph(s);

                AddTool_BuildMap(s);
                AddTool_Store(s);

                //AddTool_DisplayGraph(s);

                /*            
                            s.AddTool("Api/debug/explore/GetAppSeed");
                            s.AddTool("Api/auth/GetUserSalt");
                            s.AddTool("Api/auth/GetUser");
                */
            }
            return s;
        }


        /// <summary>
        /// Create a query session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="model">The gpt model to use, ex:
        /// "gpt-4o-mini" -	Our affordable and intelligent small model for fast, lightweight tasks.
        /// "gpt-4o" - Our high-intelligence flagship model for complex, multi-step tasks.
        /// "o1-preview" - Language models trained with reinforcement learning to perform complex reasoning.
        /// </param>
        /// <returns></returns>
        public OpenAiQuerySession CreateQuerySession(String model = null)
        {
            if (String.IsNullOrEmpty(model))
                model = DefaultChatModel;
            var mon = PerfMon;
            var s = new OpenAiQuerySession(new ChatClient(model, ApiKey, Options), model, this, mon);
            return s;
        }


        /// <summary>
        /// Simple chat complete, only use to test server connection etc.
        /// </summary>
        /// <param name="prompt">Some prompt</param>
        /// <returns>Some response</returns>
        /// <exception cref="Exception"></exception>
        [WebApi("debug/" + nameof(ChatComplete))]
        [WebApiAuth(Roles.Debug)]
        public async Task<String> ChatComplete(String prompt)
        {
            var c = CreateChatClient();
            //SystemChatMessage
            //UserChatMessage
            //AssistantChatMessage
            ClientResult<ChatCompletion> r;
            using (var _ = await (ChatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
                r = await c.CompleteChatAsync(new UserChatMessage(prompt)).ConfigureAwait(false);
            var v = r.Value;
            var e = v.Refusal;
            if (!String.IsNullOrEmpty(e))
                throw new Exception("Model refused to complete: " + e);
            StringBuilder b = new StringBuilder();
            foreach (var x in v.Content)
            {
                var t = x.Text;
                if (!String.IsNullOrEmpty(t))
                    b.AppendLine(t);

            }
            return b.ToString();
        }

        /// <summary>
        /// Chat session for debugging, only use to test server connection etc.
        /// </summary>
        /// <param name="prompt">Some prompt</param>
        /// <param name="context">Some prompt</param>
        /// <returns>Some response</returns>
        /// <exception cref="Exception"></exception>
        [WebApi("debug/" + nameof(ChatSession))]
        [WebApiAuth(Roles.Debug)]
        public Task<String> ChatSession(String prompt, HttpServerRequest context)
        {
            if (!context.Session.TryGet<OpenAiChatSession>("OpenAiDebugSession", out var s))
            {
                s = CreateChatSession(true, null, "Debug", "Debug");
                s.ChatId = "OpenAiDebugSession";
                context.Session.TryAdd("OpenAiDebugSession", s);
            }
            return s.Complete(prompt, context);
        }

        #region Tokens

        TikToken GetToken(String modelOrAlgorithm)
        {
            TikToken tikToken = null;
            Exception exception = null;
            try
            {
                tikToken = TikToken.EncodingForModel(modelOrAlgorithm);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            if (tikToken == null)
            {
                try
                {
                    tikToken = TikToken.GetEncoding(modelOrAlgorithm);
                }
                catch (Exception ex)
                {
                    exception = exception ?? ex;
                    throw;
                }

            }
            return tikToken;
        }


        /// <summary>
        /// Split some text in to AI tokens
        /// </summary>
        /// <param name="encode">Paramaters for the text to encode</param>
        /// <returns>An array of tokens</returns>
        [WebApi("debug/" + nameof(TokenEncode))]
        [WebApiAuth(Roles.Debug)]
        public int[] TokenEncode(OpenAiTokenEncodeRequest encode)
        {
            var p = encode.ModelOrAlgorithm;
            p = String.IsNullOrEmpty(p) ? "gpt-4.1" : p;
            var t = GetToken(p);
            if (t == null)
                throw new ArgumentException("Unknown model or algorithm", nameof(encode.ModelOrAlgorithm));
            var r = t.Encode(encode.Text);
            return r.ToArray();
        }

        /// <summary>
        /// Recreate some text from AI tokens
        /// </summary>
        /// <param name="decode">Parameters for the reconstruction</param>
        /// <returns>The recreated text</returns>
        [WebApi("debug/" + nameof(TokenDecode))]
        [WebApiAuth(Roles.Debug)]
        public String TokenDecode(OpenAiTokenDecodeRequest decode)
        {
            var p = decode.ModelOrAlgorithm;
            p = String.IsNullOrEmpty(p) ? "gpt-4.1" : p;
            var t = GetToken(p);
            if (t == null)
                throw new ArgumentException("Unknown model or algorithm", nameof(decode.ModelOrAlgorithm));
            var r = t.Decode(new List<int>(decode.Tokens));
            return r;
        }

        #endregion//Tokens

        #region Tools

        readonly ConcurrentDictionary<String, OpenAiTool> ApiTools = new ConcurrentDictionary<string, OpenAiTool>(StringComparer.OrdinalIgnoreCase);

        public OpenAiTool GetTool(String apiName, String fn = null)
        {
            if (Api == null)
                return null;
            IApiHttpServerEndPoint a = null;
            if (fn == null)
            {
                a = Api.TryGet(apiName);
                if (a == null)
                    throw new ArgumentException("Unknown api name " + apiName.ToQuoted(), nameof(apiName));
                fn = GetToolName(a.MethodInfo);
            }
            else
            {
                if (fn.Length == 0)
                    fn = apiName.Replace('/', '_');
            }

            fn = String.IsNullOrEmpty(fn) ? apiName.Replace('/', '_') : fn;
            if (a == null)
            {
                a = Api.TryGet(apiName);
                if (a == null)
                    throw new ArgumentException("Unknown api name " + apiName.ToQuoted(), nameof(apiName));
            }
            return GetTool(fn, a);
        }

        static String GetToolName(MethodInfo method)
        {
            var type = method.DeclaringType;
            var prefix = type.GetCustomAttribute<OpenAiToolPrefixAttribute>(true)?.ToolPrefix ?? type.Name;
            var name = method.GetCustomAttribute<OpenAiToolNameAttribute>(true)?.ToolName;
            if (String.IsNullOrEmpty(name))
                name = "{0}{1}";
            name = String.Format(name, prefix, method.Name);
            return name;
        }

        public OpenAiTool GetTool(Object instance, MethodInfo method, String fn = null, PerfMonitor perfMonitor = null, String defaultAuth = ApiHttpEntry.DefaultAuth, String defaultCachedCompression = ApiHttpEntry.DefaultCachedCompression, String defaultCompression = ApiHttpEntry.DefaultCompression, String locationPrefix = ApiHttpEntry.DefaultLocationPrefix)
        {
            fn = String.IsNullOrEmpty(fn) ? GetToolName(method) : fn;
            if (ApiTools.TryGetValue(fn, out var tool))
                return tool;
            var endPoint = ApiHttpEntry.Create(OpenAiService.IoParams, instance, method, fn, perfMonitor, defaultAuth, defaultCachedCompression, defaultCompression, locationPrefix);
            return GetTool(fn, endPoint);
        }


        OpenAiTool GetTool(String fn, IApiHttpServerEndPoint a)
        {
            var cache = ApiTools;
            if (cache.TryGetValue(fn, out var tool))
                return tool;
//            if (a.Serializer.Extension != "json")
//                throw new ArgumentException("Only json api's are currently supported" + fn.ToQuoted(), nameof(fn));
            a.GetDesc(out var argType, out var retType, out var methodDesc, out var argDesc, out var retDesc, out var argName);
            BinaryData p = null;
            if (argType != null)
                p = JsonSchemaCache.GetBinaryDataParam(argType, argName, false, argDesc);
            /*
            if ((retType == typeof(TableDataReference)) || (retType == typeof(Task<TableDataReference>)))
            {
                var rta = a.MethodInfo.GetCustomAttributes<OpenAiTableRowTypeAttribute>(true)?.FirstOrDefault();
                if (rta != null)
                {
                    var rowType = rta.RowType;
                    if (rowType != null)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("Returns a table data reference with columns:");
                        foreach (var col in TableDataTools.GetCols(rowType))
                            sb.Append(col.Type.Replace("System.", "")).Append(',').Append(col.Name).Append(',').AppendLine((col.Desc ?? "").Split('\n')[0]);
                        retDesc = sb.ToString();
                        methodDesc = retDesc.LimitLength(1024, "**Incomplete**");
                    }
                }
            }
            */

            if (retType != null)
            {
                bool isArray = retType.IsArray;
                String prefix = "Return type is a ";
                if (isArray)
                {
                    retType = retType.GetElementType();
                    prefix = "Return type is an array of ";
                }
                if (JsonSchema.TryGetPrim(retType, out var jtype))
                {
                    if (String.IsNullOrEmpty(retDesc))
                        retDesc = prefix + jtype.ToQuoted();
                    else
                        retDesc = String.Concat(retDesc, ".\n", prefix, jtype.ToQuoted());
                }
                else
                {
                    retDesc = String.Concat(prefix, retType.Name.ToQuoted(), " that is an object with JSON schema: ", JsonSchema.ToString(JsonSchema.Get(retType, true, retDesc)));
                }
                methodDesc = String.IsNullOrEmpty(methodDesc) ? retDesc : String.Join(".\n", methodDesc, retDesc);
            }
            var ct = ChatTool.CreateFunctionTool(fn, methodDesc.LimitLength(1024, ""), p, false);
            tool = OpenAiTool.Create(fn, a, ct);
            if (!cache.TryAdd(fn, tool))
                tool = cache[fn];
            return tool;
        }


        public OpenAiTool GetRegisteredTool(String fn)
            => 
            ApiTools.TryGetValue(fn, out var tool) ? tool : null;


        #endregion//Tools

        #region IChatProvider

        readonly String SessionChatPrefix;

        public bool AddChat(OpenAiChatSession chat, String name)
        {
            lock (chat)
            {
                if (chat.ChatId != null)
                    throw new Exception("A chat session may only be added once to an OpenAi instance!");
                chat.ChatId = name;
                if (ChatSessions.TryAdd(name, chat))
                    return true;
                chat.ChatId = null;
            }
            return false;
        }

        public bool RemoveChat(String name)
        {
            if (!ChatSessions.TryRemove(name, out var chat))
                return false;
            lock (chat)
            {
                chat.ChatId = null;
            }
            return true;
        }

        public bool AddChatSession(OpenAiChatSession chat, String name, HttpServerRequest request)
        {
            lock (chat)
            {
                if (chat.ChatId != null)
                    throw new Exception("A chat session may only be added once to an OpenAi instance!");
                chat.ChatId = name;
                if (request.Session.TryAdd(SessionChatPrefix + name, chat))
                    return true;
                chat.ChatId = null;
            }
            return false;
        }

        public bool TryGetSession(String name, HttpServerRequest request, out OpenAiChatSession chat)
        {
            return request.Session.TryGet(SessionChatPrefix + name, out chat);
        }

        public OpenAiChatSession RemoveChatSession(String name, HttpServerRequest request)
        {
            if (!request.Session.TryRemove(SessionChatPrefix + name, out OpenAiChatSession chat))
                return null;
            lock (chat)
            {
                chat.ChatId = null;
            }
            return chat;
        }

        readonly ConcurrentDictionary<String, OpenAiChatSession> ChatSessions = new ConcurrentDictionary<string, OpenAiChatSession>(StringComparer.OrdinalIgnoreCase);

        OpenAiChatSession GetValidatedSession(out IChatController controller, out ChatScopes scope, String name, HttpServerRequest request)
        {
            controller = ChatController;
            if (controller == null)
                throw new Exception(nameof(OpenAiService).ToQuoted() + " is not registered to any " + nameof(ChatService).ToQuoted());
            if (request.Session.TryGet<OpenAiChatSession>(SessionChatPrefix + name, out var s))
            {
                scope = ChatScopes.Session;
                return s;
            }
            scope = ChatScopes.Global;
            var auth = request.Session?.Auth;
            var c = ChatSessions;
            if (!c.TryGetValue(name, out s))
            {
                lock (c)
                {
                    if (!c.TryGetValue(name, out s))
                    {
                        if ((name != "Debug") && (name != "Pure"))
                            throw new ArgumentException("No chat session named " + name.ToQuoted(), nameof(name));
                        if ((auth == null) || (!auth.IsValid("debug")))
                            throw new Exception("Session is not authorized to acccess chat session " + name.ToQuoted());
                        s = CreateChatSession(true, null, "Debug", "Debug", name != "Pure");
                        s.ChatId = name;
                        c.TryAdd(name, s);
                    }
                }
            }
            if (s == null)
                throw new ArgumentException("No chat session named " + name.ToQuoted(), nameof(name));
            if (auth == null)
                if (s.JoinAuth != null)
                    throw new Exception("Session is not authorized to acccess chat session " + name.ToQuoted());
            if (!auth.IsValid(s.JoinAuth))
                throw new Exception("Session is not authorized to chat session " + name.ToQuoted());
            return s;
        }

        static bool IsAuth(HttpServerRequest request, IReadOnlyList<String> tokens)
        {
            var auth = request.Session?.Auth;
            if (auth == null)
                if (tokens != null)
                    return false;
            return auth.IsValid(tokens);
        }

        public string Name { get; init; }


        IChatController ChatController;



        public Task<string> CreateNewChat(string type, HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> Clear(string providerChatId, HttpServerRequest request)
        {
            var s = GetValidatedSession(out var c, out var scope, providerChatId, request);
            var session = request.Session;
            var auth = session.Auth;
            if ((auth == null) || (!auth.IsValid(s.ClearAuth)))
                throw new Exception("Not authorized to clear this chat session");
            using (await s.ChatQueryLock.Lock().ConfigureAwait(false))
            {
                lock (s.Messages)
                {
                    if (s.Messages.Count > 0)
                    {
                        s.Messages.Clear();
                        s.MessageLookup.Clear();
                        s.ApiMessages.Clear();
                        c.ClearAllMessages(providerChatId, session, scope);
                    }
                }
            }
            return true;
        }

        public async Task<bool> ForceClear(string providerChatId, HttpServerRequest request)
        {
            var s = GetValidatedSession(out var c, out var scope, providerChatId, request);
            var session = request.Session;
            using (await s.ChatQueryLock.Lock().ConfigureAwait(false))
            {
                lock (s.Messages)
                {
                    if (s.Messages.Count > 0)
                    {
                        s.Messages.Clear();
                        s.MessageLookup.Clear();
                        s.ApiMessages.Clear();
                        c.ClearAllMessages(providerChatId, session, scope);
                    }
                }
            }
            return true;
        }




        public async Task<bool> RemoveMessage(string providerChatId, long messageId, HttpServerRequest request)
        {
            var s = GetValidatedSession(out var c, out var scope, providerChatId, request);
            var session = request.Session;
            var auth = session.Auth;
            using (await s.ChatQueryLock.Lock().ConfigureAwait(false))
            {
                lock (s.Messages)
                {
                    if (!s.MessageLookup.TryGetValue(messageId, out var m))
                        throw new Exception("Unknown message id #" + messageId);
                    if (!m.IsFor(auth?.Guid))
                    {
                        if ((auth == null) || (!auth.IsValid(s.ClearAuth)))
                            throw new Exception("Not authorized to clear this message");
                    }
                    if (!s.MessageLookup.TryRemove(messageId, out m))
                        throw new Exception("Internal error!");
                    s.Messages.Remove(m);
                    c.RemoveMessage(providerChatId, messageId, session, ChatScopes.Global);
                }
            }
            return true;
        }


        public Task<long> GetCurrentId(string providerChatId, HttpServerRequest request)
        {
            var s = GetValidatedSession(out var c, out var scope, providerChatId, request);
            return Task.FromResult(Interlocked.Read(ref s.MsgId));

        }

        static Chat.ChatMessage[] InternalGetMessages(OpenAiChatSession s, String guid, long pivotId, int maxCount)
        {
            bool reverse = maxCount < 0;
            if (reverse)
                maxCount = -maxCount;
            List<Chat.ChatMessage> ret = new (maxCount);
            var m = s.Messages;
            var ml = m.Count;
            lock (m)
            {
                if (ml > 0)
                {
                    if (pivotId <= 0)
                    {
                        while (ml > 0)
                        {
                            --ml;
                            var msg = m[ml];
                            if (msg.IsFor(guid))
                            {
                                ret.Add(msg);
                                if (ret.Count >= maxCount)
                                    break;
                            }
                        }
                        ret.Reverse();
                    }
                    else
                    {
                        var max = m[ml - 1].Id;
                        if (reverse)
                        {
                            var i = BinarySearch.Upper(0, ml, pivotId, i => m[i].Id);
                            if (i >= 0)
                            {
                                while (i > 0)
                                {
                                    --i;
                                    var msg = m[ml];
                                    if (msg.IsFor(guid))
                                    {
                                        ret.Add(msg);
                                        if (ret.Count >= maxCount)
                                            break;
                                    }
                                }
                                ret.Reverse();
                            }
                        }else { 
                            var i = BinarySearch.Lower(0, ml, pivotId, i => m[i].Id);
                            if (i >= 0)
                            {
                                while (i < ml)
                                {
                                    var msg = m[ml];
                                    if (msg.IsFor(guid))
                                    {
                                        ret.Add(msg);
                                        if (ret.Count >= maxCount)
                                            break;
                                    }
                                    ++i;
                                }
                            }
                        }
                    }
                }
            }
            return ret.ToArray();
        }


        public Task<Chat.ChatMessage[]> GetMessages(string providerChatId, HttpServerRequest request, long pivotId, int maxCount)
        {
            var s = GetValidatedSession(out var c, out var scope, providerChatId, request);
            return Task.FromResult(InternalGetMessages(s, request.Session.Auth?.Guid, pivotId, maxCount));
        }

        public Task<ChatJoinResponse> Join(string providerChatId, HttpServerRequest request, long pivotId, int maxCount)
        {
            var s = GetValidatedSession(out var c, out var scope, providerChatId, request);
            var session = request.Session;
            var a = session.Auth;
            var msgs = InternalGetMessages(s, a?.Guid, pivotId, maxCount);
            var ca = s.ClearAuth;
            return Task.FromResult(new ChatJoinResponse
            {
                UserName = ChatTools.GetUsername(session),
                Lang = session.Language,
                MaxTextLength = 8192,
                MaxDataCount = 5,
                Messages = msgs,
                CanClear = a == null ? (ca == null) : a.IsValid(ca),
                CanRemove = ChatRemoveMessages.None,
                SpeechName = s.SpeechNames?.Select(x => String.Format(x, s.AgentSpeechName))?.ToArray(),
                Voices = s.Voices?.Select(x => new Chat.ChatVoice
                {
                    Name = String.Format(x.Name, s.AgentName, s.ErrorName),
                    Language = x.Language,
                    Male = x.Male,
                    Pitch = x.Pitch,
                    Rate = x.Rate,
                    Voice = x.Voice,
                }).ToArray(),
                EnableSpeechByDefault = s.EnableSpeechByDefault,
                AllowMarkDown = s.AllowUserMarkDown,
                CanStore = s.AllowStore,
                CanTranslate = s.CanTranslate,
                CanShowProfile = s.CanShowProfile,
                UploadRepo = s.CanUpload ? "UserPrivate" : null,
                DoNotConfirmClear = true,
                Menus = s.GetMenu(session, false)
            });
        }

        public Task<bool> SetValue(String providerChatId, HttpServerRequest request, long messageId, String key, String value)
        {
            var s = GetValidatedSession(out var c, out var scope, providerChatId, request);
            return s.SetValue(c, providerChatId, scope, request.Session, messageId, key, value);
        }

        public bool SendMessageAs(string providerChatId, HttpServerRequest request, ChatMessageBody message, String from = null, String fromImage = null, String toUserGuid = null)
        {
            var s = GetValidatedSession(out var c, out var scope, providerChatId, request);
            var m = new Chat.ChatMessage
            {
                Id = Interlocked.Increment(ref s.MsgId),
                From = from ?? "SYSTEM",
                FromImage = fromImage,
                Text = message.Text,
                Data = message.Data,
                Format = message.Format,
                Time = DateTime.UtcNow
            };
            m.SetTo(toUserGuid);
            lock (s.Messages)
            {
                s.Messages.Add(m);
                s.MessageLookup[m.Id] = m;
            }
            c.PostMessage(providerChatId, m, request.Session, scope);
            return true;
        }


        static String ReplaceSandbox(String s)
        {
            if (s == null)
                return s;
            var l = s.Length;
            if (l < 10)
                return s;
            var sb = new StringBuilder(l);
            int i = 0;
            for (; ; )
            {
                var j = s.IndexOf("sandbox:", i);
                if (j < 0)
                    break;
                var d = j - i;
                if (d > 0)
                    sb.Append(s, i, d);
                i = j + 8;
                while (i < l)
                {
                    if (s[i] != '/')
                        break;
                    ++i;
                }
            }
            if (i < l)
                sb.Append(s, i, l - i);
            if (sb.Length == l)
                return s;
            return sb.ToString();
        }



        readonly ConcurrentDictionary<String, OpenAiCommand> Commands = new(StringComparer.Ordinal);

        public void AddCommand(String name, Func<String, OpenAiChatSession, HttpServerRequest, Task<Chat.ChatMessage>> func, String args, String desc, params String[] auths)
        {
            Commands.TryAdd(name, new OpenAiCommand(name, auths, func, args, desc));
        }

        Task<Chat.ChatMessage> CmdHelp(String args, OpenAiChatSession s, HttpServerRequest r)
        {
            var session = r.Session;
            var haveDebug = session.IsValid(["debug"]);
            StringBuilder p = new StringBuilder();
            p.AppendLine("Available commands in this session:");
            void addCommand(OpenAiCommand t)
            {
                var a = t.Auth;
                if (!haveDebug)
                    if (!session.IsValid(a))
                        return;

                p.Append("- **").Append(OpenAiTools.MdEscape(t.Name)).Append("**");
                var g = t.Args;
                if (!String.IsNullOrEmpty(g))
                    p.Append(' ').Append(OpenAiTools.MdEscape(g));
                if (haveDebug)
                {
                    if (a != null)
                    {
                        if (a.Count == 0)
                        {
                            p.Append(" *user*");
                        }
                        else
                        {
                            var st = String.Join(String.Join("] [", a), '[', ']');
                            p.Append(" *").Append(OpenAiTools.MdEscape(st)).Append('*');
                        }
                    }
                }
                var d = t.Desc;
                if (String.IsNullOrEmpty(d))
                    p.AppendLine();
                else
                    p.Append(" **\\-** ").AppendLine(OpenAiTools.MdEscape(d));
            }

            foreach (var x in s.Commands)
                addCommand(x.Value);
            foreach (var x in Commands)
            {
                if (s.Commands.ContainsKey(x.Key))
                    continue;
                addCommand(x.Value);
            }
            foreach (var x in s.Tools)
            {
                var v = x.Value.AsCommand;
                var vn = v.Name;
                if (s.Commands.ContainsKey(x.Key))
                    continue;
                if (Commands.ContainsKey(x.Key))
                    continue;
                addCommand(v);
            }
            return Task.FromResult(new Chat.ChatMessage
            {
                Text = p.ToString(),
                Format = Chat.ChatMessageFormats.MarkDown,
            });
        }



#pragma warning disable CS0649

        sealed class TrainMsg
        {
            public String role;
            public String content;
        }

        sealed class TrainConversation
        {
            public TrainMsg[] messages;
        }
#pragma warning restore CS0649

        static readonly Dictionary<Type, String> TrainRoles = new Dictionary<Type, string>()
        {
            { typeof(UserChatMessage), "user" },
            { typeof(AssistantChatMessage), "assistant" },
            { typeof(ToolChatMessage), "user" },
            { typeof(SystemChatMessage), "system" },
        };

        async Task<Chat.ChatMessage> CmdClear(String args, OpenAiChatSession s, HttpServerRequest r)
        {
            if (await Clear(s.ChatId, r).ConfigureAwait(true))
                return null;
            return new Chat.ChatMessage
            {
                Text = "Failed to clear, not allowed?",
                Format = Chat.ChatMessageFormats.Text,
            };
        }

        async Task<Chat.ChatMessage> CmdSaveConversation(String args, OpenAiChatSession s, HttpServerRequest r)
        {
            Byte[] data;
            using (MemoryStream stream = new())
            {
                using (Utf8JsonWriter writer = new(stream))
                {
                    var opt = ModelReaderWriterOptions.Json;
                    writer.WriteStartObject();
                    var tools = s.ALlTools.Select(x => x.Value).ToList();
                    if (tools.Count > 0)
                    {
                        writer.WritePropertyName("tools");
                        writer.WriteStartArray();
                        foreach (var x in tools)
                            (x.Tool as IJsonModel<OpenAI.Chat.ChatTool>).Write(writer, opt);
                        writer.WriteEndArray();
                    }
                    writer.WritePropertyName("messages");
                    writer.WriteStartArray();
                    var t = s.MsgSystemPrompt;
                    if (t != null)
                        (t as IJsonModel<OpenAI.Chat.ChatMessage>).Write(writer, opt);
                    var session = r.Session;
                    foreach (IJsonModel<OpenAI.Chat.ChatMessage> m in s.ApiMessages)
                        m.Write(writer, opt);
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                data = stream.ToArray();
            }
            var filename = "Training_" + SHA256.HashData(data).ToHex() + ".json";
            await File.WriteAllBytesAsync(filename, data).ConfigureAwait(false);
            return new Chat.ChatMessage
            {
                Text = "Saved chat to file *" + OpenAiTools.MdEscape(filename) + "*",
                Format = Chat.ChatMessageFormats.MarkDown,
            };
        }

        Task<Chat.ChatMessage> CmdShowPrompt(String args, OpenAiChatSession s, HttpServerRequest r)
        {
            var gs = s.GetSystemPrompt;
            if (gs == null)
                return Task.FromResult(new Chat.ChatMessage
                {
                    Text = "There is no system prompt!",
                    Format = Chat.ChatMessageFormats.Text,
                });
            var p = gs(r.Session);
            return Task.FromResult(new Chat.ChatMessage
            {
                Text = String.Join(p, "```\n", "\n```"),
                Format = Chat.ChatMessageFormats.MarkDown,
            });
        }


        public delegate Task UsageDelegate(HttpServerRequest request, long inputTokens, long outputTokens);


        /// <summary>
        /// Callback with usage stats, args are: the request, model, number of input tokens, number of output tokens
        /// </summary>
        public event Func<HttpServerRequest, String, long, long, Task> OnUse;


        static IReadOnlyDictionary<String, String> ImageExtensions = ReadOnlyData.Dictionary<String, String>(StringComparer.Ordinal,
                new KeyValuePair<String, String>("png", MimeTypeMap.GetMimeType("png")?.Item1),
                new KeyValuePair<String, String>("jpg", MimeTypeMap.GetMimeType("jpg")?.Item1),
                new KeyValuePair<String, String>("jpeg", MimeTypeMap.GetMimeType("jpeg")?.Item1),
                new KeyValuePair<String, String>("webp", MimeTypeMap.GetMimeType("webp")?.Item1),
                new KeyValuePair<String, String>("gif", MimeTypeMap.GetMimeType("gif")?.Item1)
            );


        async ValueTask<IReadOnlyList<ChatMessageContentPart>> GetData(ChatMessageBody message, HttpServerRequest request)
        {
            var d = message.Data;
            if (d == null)
                return null;
            var us = UserStorage;
            if (us == null)
                return null;
            var dd = d.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var dl = dd.Length;
            var exts = ImageExtensions;
            List<ChatMessageContentPart> images = new List<ChatMessageContentPart>(dl);
            for (int i = 0; i < dl; ++ i)
            {
                var x = dd[i];
                while (x.FastStartsWith("../"))
                    x = x.Substring(3);
                try
                {
                    var ext = x.Substring(x.LastIndexOf('.') + 1);
                    if (!exts.TryGetValue(ext.FastToLower(), out var mime))
                        continue;
                    BinaryData bd;
                    var t = await us.ReadFile(request, x, false).ConfigureAwait(false);
                    if (!t.HasValue)
                        continue;
                    using (var ms = t.Value.AsStream())
                        bd = BinaryData.FromStream(ms, mime);
                    images.Add(ChatMessageContentPart.CreateImagePart(bd, mime, ChatImageDetailLevel.High));
                }
                catch
                {
                }
            }
            return images.Count <= 0 ? null : images;
        }

        public async Task<bool> UserMessage(string providerChatId, HttpServerRequest request, ChatMessageBody message)
        {
            var s = GetValidatedSession(out var c, out var scope, providerChatId, request);
            if ((message.Format == ChatMessageFormats.MarkDown) && (!s.AllowUserMarkDown))
                throw new ArgumentException("Users may not send MarkDown messages!", nameof(message.Format));
            var session = request.Session;
            var from = ChatTools.GetUsername(session);
            Chat.ChatMessage m;
            var body = message.Text?.Trim() ?? "";
            bool noResponse = String.IsNullOrEmpty(body) || body.FastStartsWith("-");
            bool isCommand = (!noResponse) && (body[0] == '/');
            var auth = session?.Auth;
            var currentUserGuid = auth?.Guid;
            if (noResponse || isCommand)
            {
                if (!isCommand)
                    body = body.Substring(1).TrimStart();
                m = new Chat.ChatMessage
                {
                    Id = Interlocked.Increment(ref s.MsgId),
                    From = from,
                    FromImage = auth?.GetUserImage(),
                    Text = body,
                    Data = message.Data,
                    Format = message.Format,
                    Time = DateTime.UtcNow,
                    Lang = message.Lang ?? request.Language,
                };
                m.SetTo(isCommand ? currentUserGuid : null);
                lock (s.Messages)
                {
                    s.Messages.Add(m);
                    s.MessageLookup[m.Id] = m;
                }
                c.PostMessage(providerChatId, m, session, scope);
                if (!isCommand)
                    return true;
                var x = body.IndexOf(' ');
                if (x < 0)
                    x = body.Length;
                var cmdText = body.Substring(1, x - 1);
                var cmdArg = body.Substring(x).TrimStart();
                if (!s.Commands.TryGetValue(cmdText, out var cmd))
                    if (!Commands.TryGetValue(cmdText, out cmd))
                        s.TryGetToolCommand(cmdText, out cmd);
                m = new Chat.ChatMessage
                {
                    Id = Interlocked.Increment(ref s.MsgId),
                    From = s.ErrorName,
                    FromImage = s.ErrorImageUrl,
                    Lang = "en",
                    Time = DateTime.UtcNow
                };
                m.SetTo(currentUserGuid);
                if (cmd == null)
                {
                    m.Text = "Command **" + OpenAiTools.MdEscape(cmdText) + "** is unknown!";
                    m.Format = ChatMessageFormats.MarkDown;
                }else
                {
                    if (!IsAuth(request, cmd.Auth))
                    {
                        m.Text = "You are no authorized to execute command **" + OpenAiTools.MdEscape(cmdText) + "**! ";
                        if (cmd.Auth.Count > 0)
                            m.Text += "\nOne of the following auth tokens are required:\n- " + String.Join("\n- ", cmd.Auth.Select(x => OpenAiTools.MdEscape(x)));
                        else
                            m.Text += "\nA user must be logged in.";
                        m.Format = ChatMessageFormats.MarkDown;
                    }
                    else
                    {
                        StringBuilder link = new StringBuilder();
                        if (request != null)
                        {
                            request.Properties[OpenAiToolExt.RequestAiToolContext] = new OpenAiToolContext(s, link,
                                (mime, data, filename) => m.AddFileData(mime, data, Name, providerChatId, s.JoinAuth, request, filename),
                                data => m.AddData(data, Name, providerChatId, request));
                        }
                        try
                        {
                            request.Session.Set("OpenAiCommand", true);
                            var n = await cmd.Fn(cmdArg, s, request).ConfigureAwait(false);
                            if (n == null)
                                return true;
                            m.From = n.From ?? "System";
                            m.FromImage = n.FromImage ?? "IconChatSystem";
                            m.Text = n.Text;
                            m.Format = n.Format;
                            if (link.Length > 0)
                                m.Data = link.ToString();
                        }
                        catch (Exception ex)
                        {
                            m.Text = String.Join(ex.Message, "```\n", "\n```");
                            m.Format = ChatMessageFormats.MarkDown;
                        }
                        finally
                        {
                            request.Session.Set("OpenAiCommand", false);
                        }
                    }
                }
                m.Flags &= ~ChatMessageFlags.IsWorking;
                lock (s.Messages)
                {
                    s.Messages.Add(m);
                    s.MessageLookup[m.Id] = m;
                }
                c.PostMessage(providerChatId, m, session, scope);
                return true;
            }
            s.InvokeOnUserMessageBegin(body, request);
            using (await s.ChatQueryLock.Lock().ConfigureAwait(false))
            {
                OpenAiDebugMessage debug = new OpenAiDebugMessage();
                m = new Chat.ChatMessage
                {
                    Id = Interlocked.Increment(ref s.MsgId),
                    From = from,
                    FromImage = session?.Auth?.GetUserImage(),
                    Text = body,
                    Data = message.Data,
                    Format = message.Format,
                    Lang = session?.Language,
                    Time = DateTime.UtcNow
                };
                lock (s.Messages)
                {
                    s.Messages.Add(m);
                    s.MessageLookup[m.Id] = m;
                }
                c.PostMessage(providerChatId, m, session, scope);
                var loadingData = s.WorkingImageUrl;
                try
                {
                    m = new Chat.ChatMessage
                    {
                        Id = Interlocked.Increment(ref s.MsgId),
                        From = s.AgentName,
                        FromImage = s.AgentImageUrl,
                        Text = s.ThinkMessage,
                        Data = loadingData,
                        Lang = session?.Language,
                        Time = DateTime.UtcNow,
                        Flags = ChatMessageFlags.IsWorking,
                    };
                    m.AddNamedData(OpenAiTools.DebugKey, debug);
                    lock (s.Messages)
                    {
                        s.Messages.Add(m);
                        s.MessageLookup[m.Id] = m;
                    }
                    
                    c.PostMessage(providerChatId, m, session, scope);

                    var f = s.FormatUserMessage;
                    if (f != null)
                        body = f(session, from, body);
                    var extraData = await GetData(message, request).ConfigureAwait(false);

                    DateTime sendNext = DateTime.MinValue;
                    bool didChange = false;
                    String error;

                    using (var t = new PeriodicTask(() =>
                    {
                        lock (m)
                        {
                            if (!didChange)
                                return true;
                            var now = DateTime.UtcNow;
                            if (sendNext > DateTime.MinValue)
                            {
                                if (now < sendNext)
                                    return true;
                            }
                            sendNext = now.AddMilliseconds(200);
                            c.ReplaceMessage(providerChatId, m, session, scope);
                            didChange = false;
                        }
                        return true;
                    }, 50))
                    {
                        error = await s.CompleteUpdate(body, extraData, request, (text, link) =>
                        {
                            text = ReplaceSandbox(text);
                            link = ReplaceSandbox(link);
                            lock (m)
                            {
                                m.Text = text;
                                m.Data = link;
                                didChange = true;
                            }
                            return Task.CompletedTask;
                        },
                            (mime, data, filename) =>
                            {
                                lock (m)
                                {
                                    if (m.Data == loadingData)
                                    {
                                        m.Text += "💾 ";
                                        didChange = true;
                                    }
                                    return m.AddFileData(mime, data, Name, providerChatId, s.JoinAuth, request, filename);
                                }
                            },
                            data => m.AddData(data, Name, providerChatId, request),
                            (toolCode) =>
                            {
                                lock (m)
                                {
                                    if (m.Data == loadingData)
                                    {
                                        if (toolCode == null)
                                            m.Text += "] ";
                                        else
                                            m.Text += "[" + toolCode;
                                        didChange = true;
                                    }
                                }

                            },
                            debug, from,
                            (model, inputCount, outputCount) => OnUse.RaiseEvents(request, model, inputCount, outputCount)
                        ).ConfigureAwait(false);
                        lock (m)
                        {
                            didChange = false;
                        }
                    }
                    if (error != null)
                    {
                        m.From = s.ErrorName;
                        m.FromImage = s.ErrorImageUrl;
                        m.Text += String.Join(error, "\n\n```\n", "\n```");
                        if (m.Data == s.WorkingImageUrl)
                            m.Data = null;
                    }
                }
                catch (Exception ex)
                {
                    m.From = s.ErrorName;
                    m.FromImage = s.ErrorImageUrl;
                    m.Text += String.Join(ex.Message, "\n\n```\n", "\n```");
                    if (m.Data == s.WorkingImageUrl)
                        m.Data = null;
                }
                m.Format = ChatMessageFormats.MarkDown;
                m.Flags &= ~ChatMessageFlags.IsWorking;
                m.MenuItems = s.GetMenu(session, true, debug.HaveInfo ? OpenAiTools.EmptyStringSet : OpenAiTools.NoDebugSet);
                c.ReplaceMessage(providerChatId, m, session, scope);
                s.InvokeAiResponseCompleted(m.Text, request);
            }
            return true;

        }

        

        public Task<Chat.ChatMessage> GetChatMessage(String providerChatId, long messageId, HttpServerRequest request)
        {
            var s = GetValidatedSession(out var c, out var scope, providerChatId, request);
            lock (s.Messages)
            {
                s.MessageLookup.TryGetValue(messageId, out var m);
                return Task.FromResult(m);
            }
        }

        public void OnInit(IChatController controller)
        {
            ChatController = controller;
        }

        #endregion//IChatProvider


    }

}
