using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Chat;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    public abstract partial class AiServiceBase
    {
        #region Tools

        readonly ConcurrentDictionary<String, AiTool> ApiTools = new ConcurrentDictionary<string, AiTool>(StringComparer.OrdinalIgnoreCase);

        public AiTool GetTool(String apiName, String fn = null)
        {
            if (Api == null)
                return null;
            IApiHttpServerEndPoint a = Api.TryGet(apiName);
            if (a == null)
                throw new ArgumentException("Unknown api name " + apiName.ToQuoted(), nameof(apiName));
            fn = fn ?? GetToolName(a.MethodInfo);
            return GetTool(fn, a);
        }

        static String GetToolName(MethodInfo method)
        {
            var type = method.DeclaringType;
            var prefix = type.GetCustomAttribute<AiToolPrefixAttribute>(true)?.ToolPrefix ?? type.Name;
            var name = method.GetCustomAttribute<AiToolNameAttribute>(true)?.ToolName;
            if (String.IsNullOrEmpty(name))
                name = "{0}{1}";
            name = String.Format(name, prefix, method.Name);
            return name;
        }

        public AiTool GetTool(Object instance, MethodInfo method, String fn = null, PerfMonitor perfMonitor = null, String defaultAuth = ApiHttpEntry.DefaultAuth, String defaultCachedCompression = ApiHttpEntry.DefaultCachedCompression, String defaultCompression = ApiHttpEntry.DefaultCompression, String locationPrefix = ApiHttpEntry.DefaultLocationPrefix)
        {
            if (Api != null)
            {
                if (Api.TryGetApi(method, out var apiUrl))
                    return GetTool(apiUrl, fn);
            }

            fn = String.IsNullOrEmpty(fn) ? GetToolName(method) : fn;
            if (ApiTools.TryGetValue(fn, out var tool))
                return tool;
            var endPoint = ApiHttpEntry.Create(IoParams, instance, method, fn, perfMonitor, defaultAuth, defaultCachedCompression, defaultCompression, locationPrefix);
            return GetTool(fn, endPoint, false);
        }


        AiTool GetTool(String fn, IApiHttpServerEndPoint a, bool exposeApi = true)
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
                String prefix = "Return type is a";
                if (isArray)
                {
                    retType = retType.GetElementType();
                    prefix = "Return type is an array of";
                }
                if (JsonSchema.TryGetPrim(retType, out var jtype))
                {
                    if (String.IsNullOrEmpty(retDesc))
                        retDesc = String.Concat(prefix, ' ', jtype.ToQuoted());
                    else
                        retDesc = String.Concat(retDesc, ".\n", prefix, ' ', jtype.ToQuoted());
                }
                else
                {
                    retDesc = String.Concat(prefix, isArray ? " objects" : "n object", " with JSON schema:  \n\n```json\n", JsonSchema.ToString(JsonSchema.Get(retType, true, retDesc), true), "\n```\n");
                }
                retDesc = String.Concat("## Tool returns  \n", retDesc);
                methodDesc = String.IsNullOrEmpty(methodDesc) ? retDesc : String.Join(".\n", methodDesc, retDesc);
            }
            if (exposeApi)
            {
                var api = a.Uri;
                if (api != null)
                {
                    methodDesc = methodDesc.Trim().LimitLength(900, "");
                    methodDesc = String.Concat(methodDesc, "\n\n### REST API  \n" + api + "  \n");
                }
            }
            var ct = new AiToolFunction(fn, methodDesc.Trim().LimitLength(1024, ""), p, false);
            tool = AiTool.Create(fn, a, ct, exposeApi);
            if (!cache.TryAdd(fn, tool))
                tool = cache[fn];
            return tool;
        }


        public AiTool GetRegisteredTool(String fn)
            => 
            ApiTools.TryGetValue(fn, out var tool) ? tool : null;



        /// <summary>
        /// Validate an API.
        /// Use this to validate a REST API call in some code that you make.
        /// This will actually perform the supplied request and inform of any errors as well as letting you inspect the response structure.
        /// </summary>
        /// <param name="data">The REST API parameters</param>
        /// <param name="context"></param>
        /// <returns>The REST API return value as a Json string</returns>
        [AiTool("✅⛔")]
        async Task<String> ValidateRestApiCall(AiRestCall data, HttpServerRequest context)
        {
            var a = Api;
            if (a == null)
                throw new Exception("API calls is not supported!");

            var url = data.ApiUrl;
            var p = context.Prefix;
            if (!url.FastStartsWith(p))
                throw new Exception("API urls must be absolute, thus start with " + p.ToQuoted());
            url = url.Substring(p.Length);
            var api = a.TryGet(url);
            if (api == null)
                throw new Exception("Couldn't find the API " + url.ToQuoted());
            var inp = data.PostJsonData;
            api.GetDesc(out var arg, out var ret, out var _, out var _, out var _, out var _);
            ReadOnlyMemory<Byte> indata = ReadOnlyMemory<Byte>.Empty;
            if (arg == null)
            {
                if (!String.IsNullOrEmpty(inp))
                    throw new Exception("API " + url.ToQuoted() + " do not take an input. Use GET or post no data");
            }else
            {
                if (String.IsNullOrEmpty(inp))
                    throw new Exception("API " + url.ToQuoted() + " expect Json POST data using the schema: " + JsonSchema.Get(arg, true));
                indata = Encoding.UTF8.GetBytes(inp);
            }
            if (!context.Session.IsValid(api.Auth))
                throw new UserNotAllowedException();
            try
            {
                var outData = await api.InvokeAsync(context, indata).ConfigureAwait(false);
                if (outData.IsEmpty)
                    return null;
                return Encoding.UTF8.GetString(outData.Span);
            }
            catch (Exception ex)
            {
                throw new Exception("API " + url.ToQuoted() + " threw error: " + ex.Message.ToQuoted() + ", expected Json POST data using the schema: " + JsonSchema.Get(arg, true), ex);
            }
        }

        static readonly MethodInfo Method_ValidateRestApiCall = typeof(AiServiceBase).GetMethod(nameof(ValidateRestApiCall), BindingFlags.NonPublic | BindingFlags.Instance);

        #endregion//Tools

        #region IChatProvider

        readonly String SessionChatPrefix;

        public bool AddChat(IAiChatSession chat, String name)
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

        public bool AddChatSession(IAiChatSession chat, String name, HttpServerRequest request)
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

        public bool TryGetSession(String name, HttpServerRequest request, out IAiChatSession chat)
        {
            return request.Session.TryGet(SessionChatPrefix + name, out chat);
        }

        public IAiChatSession RemoveChatSession(String name, HttpServerRequest request)
        {
            if (!request.Session.TryRemove(SessionChatPrefix + name, out IAiChatSession chat))
                return null;
            lock (chat)
            {
                chat.ChatId = null;
            }
            return chat;
        }

        readonly ConcurrentDictionary<String, IAiChatSession> ChatSessions = new ConcurrentDictionary<string, IAiChatSession>(StringComparer.OrdinalIgnoreCase);

        IAiChatSession GetValidatedSession(out IChatController controller, out ChatScopes scope, String name, HttpServerRequest request)
        {
            controller = ChatController;
            if (controller == null)
                throw new Exception(GetType().Name.ToQuoted() + " is not registered to any " + nameof(ChatService).ToQuoted());
            if (request.Session.TryGet<IAiChatSession>(SessionChatPrefix + name, out var s))
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
                        s = CreateAiChatSession(true, null, "Debug", "Debug", name != "Pure", null);
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
                        s.ClearApiMessages();
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
                        s.ClearApiMessages();
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
            return Task.FromResult(s.MsgId);

        }

        static Chat.ChatMessage[] InternalGetMessages(IAiChatSession s, String guid, long pivotId, int maxCount)
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
                CanPost = true,
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
                Id = s.NextMsgId(),
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
                var j = s.FastIndexOf("sandbox:", i);
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



        readonly ConcurrentDictionary<String, AiCommand> Commands = new(StringComparer.Ordinal);

        public void AddCommand(String name, Func<String, IAiChatSession, HttpServerRequest, Task<Chat.ChatMessage>> func, String args, String desc, params String[] auths)
        {
            Commands.TryAdd(name, new AiCommand(name, auths, func, args, desc));
        }

        Task<Chat.ChatMessage> CmdHelp(String args, IAiChatSession s, HttpServerRequest r)
        {
            var session = r.Session;
            var haveDebug = session.IsValid(["debug"]);
            StringBuilder p = new StringBuilder();
            p.AppendLine("Available commands in this session:");
            void addCommand(AiCommand t)
            {
                var a = t.Auth;
                if (!haveDebug)
                    if (!session.IsValid(a))
                        return;

                p.Append("- **").Append(AiTools.MdEscape(t.Name)).Append("**");
                var g = t.Args;
                if (!String.IsNullOrEmpty(g))
                    p.Append(' ').Append(AiTools.MdEscape(g));
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
                            p.Append(" *").Append(AiTools.MdEscape(st)).Append('*');
                        }
                    }
                }
                var d = t.Desc;
                if (String.IsNullOrEmpty(d))
                    p.AppendLine();
                else
                    p.Append(" **\\-** ").AppendLine(AiTools.MdEscape(d));
            }

            foreach (var x in s.Commands)
                addCommand(x.Value);
            foreach (var x in Commands)
            {
                if (s.Commands.ContainsKey(x.Key))
                    continue;
                addCommand(x.Value);
            }
            foreach (var x in s.ALlTools)
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


        async Task<Chat.ChatMessage> CmdClear(String args, IAiChatSession s, HttpServerRequest r)
        {
            if (await Clear(s.ChatId, r).ConfigureAwait(true))
                return null;
            return new Chat.ChatMessage
            {
                Text = "Failed to clear, not allowed?",
                Format = Chat.ChatMessageFormats.Text,
            };
        }

        async Task<Chat.ChatMessage> CmdSaveConversation(String args, IAiChatSession s, HttpServerRequest r)
        {
            Byte[] data;
            using (MemoryStream stream = new())
            {
                using (Utf8JsonWriter writer = new(stream))
                {
                    writer.WriteStartObject();
                    s.WriteConversation(writer);
                    writer.WriteEndObject();
                }
                data = stream.ToArray();
            }
            var filename = "Training_" + SHA256.HashData(data).ToHex() + ".json";
            await File.WriteAllBytesAsync(filename, data).ConfigureAwait(false);
            return new Chat.ChatMessage
            {
                Text = "Saved chat to file *" + AiTools.MdEscape(filename) + "*",
                Format = Chat.ChatMessageFormats.MarkDown,
            };
        }

        async Task<Chat.ChatMessage> CmdShowPrompt(String args, IAiChatSession s, HttpServerRequest r)
        {
            var p = await s.BuildSystemPrompt(r.Session).ConfigureAwait(false);
            if (p == null)
                return new Chat.ChatMessage
                {
                    Text = "There is no system prompt!",
                    Format = ChatMessageFormats.Text,
                };
            return new Chat.ChatMessage
            {
                Text = String.Join(StringTools.EscapeMD(p), "```\n", "\n```"),
                Format = ChatMessageFormats.MarkDown,
            };
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


        async Task<IReadOnlyList<ValueTuple<AiContentPart, String>>> GetData(ChatMessageBody message, HttpServerRequest request)
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
            List<ValueTuple<AiContentPart, String>> images = new (dl);
            for (int i = 0; i < dl; ++ i)
            {
                var fn = dd[i];
                var x = fn;
                while (x.FastStartsWith("../"))
                    x = x.Substring(3);
                try
                {
                    var ext = x.Substring(x.LastIndexOf('.') + 1);
                    if (!exts.TryGetValue(ext.FastToLower(), out var mime))
                        continue;
                    BinaryData bd;
                    using var t = await us.ReadFile(request, x, false).ConfigureAwait(false);
                    if (t == null)
                        continue;
                    bd = BinaryData.FromBytes(t.Memory);
                    var p = AiContentPart.CreateImagePart(bd, mime, true);
                    images.Add(ValueTuple.Create(p, fn));
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
                    Id = s.NextMsgId(),
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
                    Id = s.NextMsgId(),
                    From = s.ErrorName,
                    FromImage = s.ErrorImageUrl,
                    Lang = "en",
                    Time = DateTime.UtcNow
                };
                m.SetTo(currentUserGuid);
                if (cmd == null)
                {
                    m.Text = "Command **" + AiTools.MdEscape(cmdText) + "** is unknown!";
                    m.Format = ChatMessageFormats.MarkDown;
                }else
                {
                    if (!IsAuth(request, cmd.Auth))
                    {
                        m.Text = "You are no authorized to execute command **" + AiTools.MdEscape(cmdText) + "**! ";
                        if (cmd.Auth.Count > 0)
                            m.Text += "\nOne of the following auth tokens are required:\n- " + String.Join("\n- ", cmd.Auth.Select(x => AiTools.MdEscape(x)));
                        else
                            m.Text += "\nA user must be logged in.";
                        m.Format = ChatMessageFormats.MarkDown;
                    }
                    else
                    {
                        StringBuilder link = new StringBuilder();
                        if (request != null)
                        {
                            request.Properties[OpenAiToolExt.RequestAiToolContext] = new AiToolContext(s, link,
                                (mime, data, filename) => m.AddFileData(mime, data, Name, providerChatId, s.JoinAuth, request, filename),
                                data => m.AddData(data, Name, providerChatId, request));
                        }
                        try
                        {
                            request.Session.Set("AiCommand", true);
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
                            request.Session.Set("AiCommand", false);
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
                AiDebugMessage debug = new AiDebugMessage();
                m = new Chat.ChatMessage
                {
                    Id = s.NextMsgId(),
                    From = from,
                    FromImage = session?.Auth?.GetUserImage(),
                    Text = body,
                    Data = message.Data,
                    Format = message.Format,
                    Lang = session?.Language,
                    Time = DateTime.UtcNow,
                };
                lock (s.Messages)
                {
                    s.Messages.Add(m);
                    s.MessageLookup[m.Id] = m;
                }
                c.PostMessage(providerChatId, m, session, scope);
                var loadingData = s.WorkingImageUrl;
                var start = DateTime.UtcNow;
                try
                {
                    m = new Chat.ChatMessage
                    {
                        Id = s.NextMsgId(),
                        From = s.AgentName,
                        FromImage = s.AgentImageUrl,
                        Text = s.ThinkMessage,
                        Data = loadingData,
                        Lang = session?.Language,
                        Time = start,
                        Flags = ChatMessageFlags.IsWorking,
                    };
                    m.AddNamedData(AiTools.DebugKey, debug);
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
                            async (model, inputCount, outputCount) => await OnUse.RaiseEvents(request, model, inputCount, outputCount).ConfigureAwait(false)
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
                m.MenuItems = s.GetMenu(session, true, debug.HaveInfo ? AiTools.EmptyStringSet : AiTools.NoDebugSet);
                c.ReplaceMessage(providerChatId, m, session, scope);
                s.InvokeAiResponseCompleted(m.Text, request);
                debug.Took = DateTime.UtcNow - start;
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
