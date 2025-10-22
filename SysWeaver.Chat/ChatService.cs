using CommunityToolkit.HighPerformance;
using HtmlAgilityPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using SysWeaver;
using SysWeaver.AI;
using SysWeaver.Compression;
using SysWeaver.MicroService;
using SysWeaver.Net;
using SysWeaver.Translation;

namespace SysWeaver.Chat
{

    /// <summary>
    /// A service that handles the sync/coms between the chat client and server.
    /// Multiple chat providers may be registered.
    /// Services in the ServiceManager that implements the IChatProvider will be registered automatically.
    /// </summary>
    [IsMicroService]
    [OptionalDep<IChatProvider>]
    [OptionalDep<HttpServerBase>]
    [OptionalDep<IUserStorageService>]
    [WebApiUrl("../chat")]
    [OpenAiToolPrefix("")]
    public sealed class ChatService : IDisposable, IHttpServerModule, IHaveOpenAiTools
    {
        public override string ToString() => "Providers: " + String.Join(", ", Providers.Values.Select(x => x.Provider.Name));

        #region IHttpServerModule

        public String[] OnlyForPrefixes { get; } = ["chat/file/"];

        public Func<HttpServerRequest, ValueTask<IHttpRequestHandler>> AsyncHandler { get; init; }

        async ValueTask<IHttpRequestHandler> GetChatFile(String l, HttpServerRequest context)
        {
            var t = l.Split('/');
            var tl = t.Length;
            if (tl < 6)
                return null;
            if (!long.TryParse(t[4], out var msgId))
                return null;
            var providerName = t[2];
            var providerChatId = t[3];
            var name = t[5];
            if (!TryGetController(String.Join('.', providerName, providerChatId), out var c, out var pid))
                return null;
            var msg = await c.Provider.GetChatMessage(pid, msgId, context).ConfigureAwait(false);
            if (msg == null)
                return null;
            var to = msg.To;
            if (to != null)
                if (to != (context.Session?.Auth?.Guid))
                    return null;
            return msg.GetData(name) as IHttpRequestHandler;
        }

        /// <summary>
        /// Enumerate all enpoints
        /// </summary>
        /// <param name="root">If null all endpoints are returned (recursively)</param>
        /// <returns>End point information</returns>
        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(String root = null) => HttpServerTools.NoEndPoints;

        #endregion//IHttpServerModule


        public ChatService(ServiceManager manager, ChatServiceParams p = null)
        {
            p = p ?? new ChatServiceParams();
            InputLanguages = HttpServerBase.ValidateLanguageList(p.InputLanguages);
            AllowPublicStore = p.AllowPublicStore;
            AsyncHandler = c => GetChatFile(c.LocalUrl, c);
            Manager = manager;
            var server = manager.TryGet<HttpServerBase>();
            foreach (var x in manager.UniqueInstances)
            {
                AddChatProvider(x as IChatProvider);
                AddLinkHandler(x as IChatStoreLinkHandler);
            }
            UserStore = manager.TryGet<IUserStorageService>();
            manager.OnServiceAdded += ServiceAdded;
            manager.OnServiceRemoved += ServiceRemoved;
        }

        /// <summary>
        /// If true, the user can store files in chat in a way that anyone can look at them
        /// </summary>
        readonly bool AllowPublicStore;

        public bool AddLinkHandler(IChatStoreLinkHandler h)
        {
            if (h == null)
                return false;
            return LinkHandlers.TryAdd(h, h);
        }

        public bool RemoveLinkHandler(IChatStoreLinkHandler h)
        {
            if (h == null)
                return false;
            return LinkHandlers.TryRemove(h, out var _);
        }


        readonly ConcurrentDictionary<IChatStoreLinkHandler, IChatStoreLinkHandler> LinkHandlers = new ConcurrentDictionary<IChatStoreLinkHandler, IChatStoreLinkHandler>();



        readonly IUserStorageService UserStore;
        readonly ServiceManager Manager;

        public void Dispose()
        {
            var m = Manager;
            m.OnServiceRemoved -= ServiceRemoved;
            m.OnServiceAdded -= ServiceAdded;
        }

        void ServiceRemoved(object inst, ServiceInfo info)
        {
            RemoveLinkHandler(inst as IChatStoreLinkHandler);
            RemoveChatProvider(inst as IChatProvider);
            var s = inst as HttpServerBase;
            if (s == null)
                return;
            Server = null;

        }

        void ServiceAdded(object inst, ServiceInfo info)
        {
            AddChatProvider(inst as IChatProvider);
            AddLinkHandler(inst as IChatStoreLinkHandler);
            var s = inst as HttpServerBase;
            if (s == null)
                return;
            Server = s;
        }

        HttpServerBase Server;


        void PushMessage(PushMessage msg, HttpSession session, ChatScopes scope, String toUser = null)
        {
            var s = Server;
            if (s == null)
                return;
            if (!String.IsNullOrEmpty(toUser))
            {
                switch (scope)
                {
                    case ChatScopes.Global:
                        s.PushMessageUser(toUser, msg, false);
                        break;
                    case ChatScopes.Session:
                        if (session.Auth.Guid == toUser)
                            session.PushMessage(msg, false);
                        break;
                }
            }
            else
            {
                switch (scope)
                {
                    case ChatScopes.Global:
                        s.PushMessageAllSessions(msg, false);
                        break;
                    case ChatScopes.Session:
                        session.PushMessage(msg, false);
                        break;
                }

            }
        }

        #region Manually adding/removing providers

        public IChatController AddChatProvider(IChatProvider chatProvider)
        {
            if (chatProvider == null)
                return null;
            var name = chatProvider.Name.FastToLower();
            if (String.IsNullOrEmpty(name))
                throw new Exception("Invalid provider name!");
            var p = Providers;
            var c = new Controller(chatProvider, this);
            if (!p.TryAdd(name, c))
                throw new Exception("Can only add the chat provider once!");
            chatProvider.OnInit(c);
            return c;
        }

        public void RemoveChatProvider(IChatProvider chatProvider)
        {
            if (chatProvider == null)
                return;
            var name = chatProvider.Name.FastToLower();
            var p = Providers;
            if (!p.TryRemove(name, out var c))
                throw new Exception("Can't remove chat provider!");
            chatProvider.OnInit(null);
        }

        #endregion//Manually adding/removing providers

        sealed class Controller : IChatController
        {
            public readonly IChatProvider Provider;
            public readonly ChatService Service;

            public Controller(IChatProvider provider, ChatService service)
            {
                Provider = provider;
                Service = service;
            }

            String MessageName(String operation, String providerChatId) =>
                String.Join('.', "chat", operation, Provider.Name, providerChatId).FastToLower();

            public void ClearAllMessages(string providerChatId, HttpSession session, ChatScopes scope)
            {
                Service.PushMessage(new PushMessage(MessageName("Clear", providerChatId)), session, scope);
            }

            public void PostMessage(string providerChatId, ChatMessage message, HttpSession session, ChatScopes scope)
            {
                message.Flags &= ~ChatMessageFlags.CanRemove;
                var to = message.GetTo();
                if (!String.IsNullOrEmpty(to))
                {
                    if (session.Auth?.Guid == to)
                        message.Flags |= ChatMessageFlags.CanRemove;
                }
                Service.PushMessage(new ChatPushMessage(MessageName("Post", providerChatId), message), session, scope, message.To);
            }

            public void RemoveMessage(string providerChatId, long messageId, HttpSession session, ChatScopes scope)
            {
                Service.PushMessage(new PushMessagIntValue(MessageName("Remove", providerChatId), messageId), session, scope);
            }

            public void ReplaceMessage(string providerChatId, ChatMessage message, HttpSession session, ChatScopes scope)
            {
                message.Flags &= ~ChatMessageFlags.CanRemove;
                var to = message.GetTo();
                if (!String.IsNullOrEmpty(to))
                {
                    if (session.Auth?.Guid == to)
                        message.Flags |= ChatMessageFlags.CanRemove;
                }
                Service.PushMessage(new ChatPushMessage(MessageName("Replace", providerChatId), message), session, scope, message.To);
            }
        }


        readonly ConcurrentDictionary<String, Controller> Providers = new ConcurrentDictionary<string, Controller>(StringComparer.Ordinal);


        bool TryGetController(String chatId, out Controller controller, out String providerChatId)
        {
            var i = chatId.IndexOf('.');
            controller = null;
            providerChatId = null;
            if (i <= 0)
                return false;
            var pid = chatId.Substring(0, i).FastToLower();
            if (!Providers.TryGetValue(pid, out controller))
                return false;
            providerChatId = chatId.Substring(i + 1);
            return true;
        }

        /// <summary>
        /// Call to create a new chat session for the given chat provider and chat type.
        /// </summary>
        /// <param name="r">The parameters for the chat create request</param>
        /// <param name="context">The context for the http request (ignore)</param>
        /// <returns>A new chat id for the create chat session if succeeded, else null</returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        public async Task<String> CreateNewChat(ChatCreateRequest r, HttpServerRequest context)
        {
            var pname = r.Provider.FastToLower();
            if (!Providers.TryGetValue(pname, out var c))
                throw new Exception("Unknown chat provider " + r.Provider.ToQuoted());
            var p = c.Provider;
            var cid = await p.CreateNewChat(r.Type, context).ConfigureAwait(false);
            return String.Join('.', p.Name, cid);
        }

        /// <summary>
        /// Request that the specified chat session should be cleared (restarted)
        /// </summary>
        /// <param name="chatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="context">The context for the http request (ignore)</param>
        /// <returns>True if successful</returns>
        /// <exception cref="ArgumentException"></exception>
        [WebApi]
        public Task<bool> ClearAllMessages(String chatId, HttpServerRequest context)
        {
            if (!TryGetController(chatId, out var c, out var pid))
                throw new ArgumentException("Unknown chat id!", nameof(chatId));
            return c.Provider.Clear(pid, context);
        }

        /// <summary>
        /// Remove a single message from a chat.
        /// </summary>
        /// <param name="msgRequest">The parameters for the post message request</param>
        /// <param name="context">The context for the http request (ignore)</param>
        /// <returns>True if successful</returns>
        [WebApi]
        public Task<bool> RemoveMessage(ChatMessageIdRequest msgRequest, HttpServerRequest context)
        {
            if (!TryGetController(msgRequest.ChatId, out var c, out var pid))
                throw new ArgumentException("Unknown chat id!", nameof(msgRequest.ChatId));
            return c.Provider.RemoveMessage(pid, msgRequest.MessageId, context);
        }



        /// <summary>
        /// Get the current message id for the specified chat session.
        /// </summary>
        /// <param name="chatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="context">The context for the http request (ignore)</param>
        /// <returns>The last message id in the chat session, or 0 if no messages are available</returns>
        /// <exception cref="ArgumentException"></exception>
        [WebApi]
        public Task<long> GetCurrentId(String chatId, HttpServerRequest context)
        {
            if (!TryGetController(chatId, out var c, out var pid))
                throw new ArgumentException("Unknown chat id!", nameof(chatId));
            return c.Provider.GetCurrentId(pid, context);
        }

        /// <summary>
        /// Get messages from the specified chat session.
        /// </summary>
        /// <param name="joinRequest">The parameters for the poll messages request</param>
        /// <param name="context">The context for the http request (ignore)</param>
        /// <returns>Array of messages, can be null or empty</returns>
        /// <exception cref="ArgumentException"></exception>
        [WebApi]
        public async Task<ChatJoinResponse> Join(ChatJoinRequest joinRequest, HttpServerRequest context)
        {
            if (!TryGetController(joinRequest.ChatId, out var c, out var pid))
                throw new ArgumentException("Unknown chat id!", nameof(joinRequest.ChatId));
            var from = joinRequest.FromId;
            var max = joinRequest.MaxCount;
            if (max < -200)
                max = -200;
            if (max == 0)
                max = 10;
            if (max > 200)
                max = 200;
            var res = await c.Provider.Join(pid, context, from, max).ConfigureAwait(false);
            var userGuid = context.Session.Auth?.Guid;
            var userStore = userGuid == null ? null : UserStore?.GetUserPath(userGuid);
            res.UserGuid = userGuid.ToHex();
            res.CanStore &= (UserStore != null);
            res.CanStore &= (userStore != null);
            res.CanTranslate &= (context.Translator != null);
            res.AllowPublicStore = AllowPublicStore;
            res.UserStore = userStore;
            JoinResponses[joinRequest.ChatId] = res;
            return res;
        }

        readonly ConcurrentDictionary<String, ChatJoinResponse> JoinResponses = new ConcurrentDictionary<string, ChatJoinResponse>(StringComparer.Ordinal);



        /// <summary>
        /// Get messages from the specified chat session.
        /// </summary>
        /// <param name="msgRequest">The parameters for the poll messages request</param>
        /// <param name="context">The context for the http request (ignore)</param>
        /// <returns>Array of messages, can be null or empty</returns>
        /// <exception cref="ArgumentException"></exception>
        [WebApi]
        public async Task<ChatMessage[]> GetMessages(ChatJoinRequest msgRequest, HttpServerRequest context)
        {
            if (!TryGetController(msgRequest.ChatId, out var c, out var pid))
                throw new ArgumentException("Unknown chat id!", nameof(msgRequest.ChatId));
            var from = msgRequest.FromId;
            var max = msgRequest.MaxCount;
            if (max < -200)
                max = -200;
            if (max == 0)
                max = 10;
            if (max > 200)
                max = 200;
            var u = context.Session?.Auth?.Guid;
            var m = await c.Provider.GetMessages(pid, context, from, max).ConfigureAwait(false);
            if (m != null)
            {
                var l = m.Length;
                int o = 0;
                for (int i = 0; i < l; ++i)
                {
                    var mm = m[i];
                    var to = mm.To;
                    if (to != null)
                        if (u != to)
                            continue;
                    m[o] = mm;
                    ++o;
                }
                if (o != l)
                    Array.Resize(ref m, o);
            }
            return m;
        }

        /// <summary>
        /// Translate a message to the users selected language
        /// </summary>
        /// <param name="translateRequest"></param>
        /// <param name="context"></param>
        /// <returns>The translated message</returns>
        /// <exception cref="ArgumentException"></exception>
        [WebApi]
        public async Task<String> Translate(ChatTranslateRequest translateRequest, HttpServerRequest context)
        {
            if (!JoinResponses.TryGetValue(translateRequest.ChatId, out var r))
                throw new ArgumentException("Unknown message id!", nameof(translateRequest.MessageId));
            if (!r.CanTranslate)
                throw new Exception("Not allowed to translate message in this chat");
            if (!TryGetController(translateRequest.ChatId, out var c, out var pid))
                throw new ArgumentException("Unknown chat id!", nameof(translateRequest.ChatId));
            var mid = translateRequest.MessageId;
            var m = await c.Provider.GetChatMessage(pid, mid, context).ConfigureAwait(false);
            if (m == null)
                throw new ArgumentException("Unknown message id!", nameof(translateRequest.MessageId));
            var to = context.Language;
            return await TranslationCache.GetOrUpdateValueAsync(String.Join('\n', mid, to), async key =>
            {
                return await context.Translator.TranslateOne(new TranslateRequest
                {
                    From = m.Lang,
                    To = to,
                    Text = m.Text,
                    Context = TranslationContexts[(int)m.Format],
                    Effort = TranslationEffort.Low,
                    Retention = TranslationCacheRetention.Short,
                }).ConfigureAwait(false);
            });
        }

        const String TranslationSourceDisclaimer = "The source language is a guestimate, only use as a tie breaker.";

        static readonly String[] TranslationContexts =
        [
            TranslationSourceDisclaimer,
            TranslationSourceDisclaimer + "\n" + TypeTranslator.MdContext,
            TranslationSourceDisclaimer + "\n" + TypeTranslator.HtmlContext,
        ];

        readonly FastMemCache<String, String> TranslationCache = new FastMemCache<string, string>(TimeSpan.FromMinutes(5), StringComparer.Ordinal);




        /// <summary>
        /// Post a message to the specified chat session
        /// </summary>
        /// <param name="msgRequest">The parameters for the post message request</param>
        /// <param name="context">The context for the http request (ignore)</param>
        /// <returns>True if the message was poseted successfully</returns>
        [WebApi]
        public Task<bool> UserMessage(ChatMessageRequest msgRequest, HttpServerRequest context)
        {
            if (!TryGetController(msgRequest.ChatId, out var c, out var pid))
                throw new ArgumentException("Unknown chat id!", nameof(msgRequest.ChatId));
            var body = msgRequest.Body;
            if (body == null)
                throw new ArgumentNullException("No message body supplied!", nameof(msgRequest.Body));
            if (body.Format == ChatMessageFormats.HTML)
                throw new ArgumentException("Users may not send HTML messages!", nameof(body.Format));
            return c.Provider.UserMessage(pid, context, body);
        }


        /// <summary>
        /// Set a chat value
        /// </summary>
        /// <param name="request">The parameters for the request</param>
        /// <param name="context">The context for the http request (ignore)</param>
        /// <returns>True if the value was set successfully</returns>
        [WebApi]
        public Task<bool> SetValue(ChatValueRequest request, HttpServerRequest context)
        {
            if (!TryGetController(request.ChatId, out var c, out var pid))
                throw new ArgumentException("Unknown chat id!", nameof(request.ChatId));
            var key = request.Key?.Trim();
            if (String.IsNullOrEmpty(key))
                throw new ArgumentNullException("No key supplied!", nameof(request.Key));
            return c.Provider.SetValue(pid, context, request.Id, key, request.Value);
        }


        /// <summary>
        /// Persist a file to the user's storage.
        /// </summary>
        /// <param name="request">What and how to store</param>
        /// <param name="context">The context for the http request (ignore)</param>
        /// <returns>URL to the stored files</returns>
        [WebApi]
        [WebApiAuth]
        [OpenAiTool("💽")]
        public async Task<String> StoreFile(ChatStore request, HttpServerRequest context)
        {

            var l = Uri.UnescapeDataString(request.Url);
            l = context.MakeRequestAbsolute(l);
            var data = await context.Server.InternalRead(context, l).ConfigureAwait(false);
            if (!context.Session.IsValid(data.Item3.Auth))
                throw new UserNotAllowedException();
            if (!AllowPublicStore)
                if (request.Scope == UserStorageScopes.Public)
                    throw new UserNotAllowedException();
            var filename = l.Substring(l.LastIndexOf('/') + 1);
            var mem = data.Item1;
            String url = null;
            switch (request.Scope)
            {
                case UserStorageScopes.Private:
                    url = await UserStore.StorePrivateFile(context, filename, mem).ConfigureAwait(false);
                    break;
                case UserStorageScopes.Protected:
                    url = await UserStore.StorePublicFile(context, filename, mem, "").ConfigureAwait(false);
                    break;
                case UserStorageScopes.Public:
                    url = await UserStore.StorePublicFile(context, filename, mem).ConfigureAwait(false);
                    break;
            }
            return "../" + url;
        }

        /// <summary>
        /// Create a persistent link to some URL, save it and any files required to the user's storage.
        /// WHen a user want to store something, this is what you want unless the user explicitly instructs you to save it as a file.
        /// </summary>
        /// <param name="request">What and how to store</param>
        /// <param name="context">The context for the http request (ignore)</param>
        /// <returns>URL showing the stored link</returns>
        [WebApi]
        [WebApiAuth]
        [OpenAiTool("🔗")]
        public async Task<String> StoreLink(ChatStore request, HttpServerRequest context)
        {
            const String localPrefix = "../";
            var us = UserStore;
            if (us == null)
                return null;
            var l = context.MakeRequestAbsolute(Uri.UnescapeDataString(request.Url)).Substring(context.Prefix.Length);
            foreach (var x in LinkHandlers)
            {
                var r = await x.Key.HandleLink(us, l, request.Scope, context).ConfigureAwait(false);
                if (r != null)
                    return localPrefix + r;
            }
            l = context.Prefix + l;
            var data = await context.Server.InternalRead(context, l).ConfigureAwait(false);
            if (!context.Session.IsValid(data.Item3.Auth))
                throw new UserNotAllowedException();
            if (!AllowPublicStore)
                if (request.Scope == UserStorageScopes.Public)
                    throw new UserNotAllowedException();
            List<String> files = new List<string>();
            Func<String, ReadOnlyMemory<Byte>, Task<String>> saveFile = null;
            Func<String, Task<String>> saveLink = null;
            var requestScope = request.Scope;
            switch (requestScope)
            {
                case UserStorageScopes.Private:
                    saveFile = (nn, mm) => us.StorePrivateFile(context, nn, mm, false);
                    saveLink = (nn) => us.StorePrivateLink(context, nn, files.ToArray());
                    break;
                case UserStorageScopes.Protected:
                    saveFile = (nn, mm) => us.StorePublicFile(context, nn, mm, "", false);
                    saveLink = (nn) => us.StorePublicLink(context, nn, "", files.ToArray());
                    break;
                case UserStorageScopes.Public:
                    saveFile = (nn, mm) => us.StorePublicFile(context, nn, mm, null, false);
                    saveLink = (nn) => us.StorePublicLink(context, nn, null, files.ToArray());
                    break;
            }

            var url = data.Item4.LocalUrl;
            var filename = url.Substring(url.LastIndexOf('/') + 1);
            var mem = data.Item1;
            const String chatPrefix = "chat/file/";
            bool needFile = url.StartsWith(chatPrefix);
            var storagePrefix = await us.GetBaseUrlPrefix().ConfigureAwait(false);
            if (!needFile)
            {
                if (url.StartsWith(storagePrefix))
                {
                    var linkScope = await us.GetScope(context, url).ConfigureAwait(false);
                    if (linkScope != null)
                        needFile = (linkScope ?? UserStorageScopes.Public) < requestScope;
                }
            }
            //  TODO: Add referenced chat content
            if (data.Item4.GetResMime().FastStartsWith("text/html"))
            {
                var chatLinkPrefix = localPrefix + chatPrefix;
                var localStoragePrefix = localPrefix + storagePrefix;
                async Task<bool> Change(HtmlNode node, String name)
                {
                    var val = node.GetAttributeValue(name, (String)null);
                    if (val == null)
                        return false;
                    //  Stored files (create copy in scope needs changing)
                    if (val.StartsWith(localStoragePrefix))
                    {
                        var localFile = val.Substring(localPrefix.Length);
                        var linkScope = await us.GetScope(context, localFile).ConfigureAwait(false);
                        if (linkScope != null)
                        {
                            if ((linkScope ?? UserStorageScopes.Public) < requestScope)
                            {
                                var data = await us.ReadFile(context, localFile, false).ConfigureAwait(false);
                                if (data != null)
                                {
                                    var filename = localFile.Substring(localFile.LastIndexOf("/") + 1);
                                    var newName = await saveFile(filename, data ?? throw new Exception()).ConfigureAwait(false);
                                    files.Add(newName);
                                    node.SetAttributeValue(name, localPrefix + newName);
                                    return true;
                                }
                            }
                            return false;
                        }
                    }
                    //  Chat files (in memory)
                    if (val.StartsWith(chatLinkPrefix))
                    {
                        var localFile = val.Substring(localPrefix.Length);
                        var d = await GetChatFile(localFile, context).ConfigureAwait(false);
                        if (d != null)
                        {
                            if (context.Session.IsValid(d.Auth))
                            {
                                var data = (d as StaticMemoryHttpRequestHandler)?.Data;
                                if (data != null)
                                {
                                    var filename = localFile.Substring(localFile.LastIndexOf("/") + 1);
                                    var newName = await saveFile(filename, data ?? throw new Exception()).ConfigureAwait(false);
                                    files.Add(newName);
                                    node.SetAttributeValue(name, localPrefix + newName);
                                    return true;
                                }
                            }
                            return false;
                        }
                    }
                    return false;
                }
                var html = new HtmlDocument();
                using (var ms = mem.AsStream())
                    html.Load(ms);
                Dictionary<String, String> replacements = new Dictionary<string, string>(StringComparer.Ordinal);
                Stack<HtmlNode> nodes = new();
                nodes.Push(html.DocumentNode);
                bool changed = false;
                while (nodes.TryPop(out var node))
                {
                    foreach (var x in node.ChildNodes)
                        nodes.Push(x);
                    changed |= await Change(node, "href").ConfigureAwait(false);
                    changed |= await Change(node, "src").ConfigureAwait(false);
                }
                using (var ms = new MemoryStream())
                {
                    html.Save(ms);
                    mem = ms.ToArray();
                }
            }


            if (needFile)
            {
                url = await saveFile(filename, mem).ConfigureAwait(false);
                files.Add(url);
            }
            url = await saveLink(url).ConfigureAwait(false);
            return localPrefix + url;
        }


        /// <summary>
        /// Get a list of the supported input languages, with localized meta information.
        /// </summary>
        /// <returns>The list of supported languages.
        /// null = No language support</returns>
        [WebApi]
        [WebApiClientCache(1)]
        [WebApiRequestCacheStatic]
        public Task<LanguageInfo[]> GetInputLanguages(HttpServerRequest context)
        {
            var langs = InputLanguages;
            var server = context.Server;
            if (langs == null)
                return server.GetLocalizedLanguages(context);
            return LocalizedLanguagesCache.GetOrUpdateAsync(context.Session.Language, async cl =>
            {
                var l = langs.Length;
                var d = new LanguageInfo[l];
                if (l <= 0)
                    return d;
                cl = context.Session.Language;
                Task<LanguageInfo>[] tasks = new Task<LanguageInfo>[l];
                for (int i = 0; i < l; ++i)
                    tasks[i] = server.GetLocalizedLanguage(langs[i], cl);
                await Task.WhenAll(tasks).ConfigureAwait(false);
                for (int i = 0; i < l; ++i)
                    d[i] = tasks[i].Result;
                return d;
            });
        }

        readonly FastMemCache<String, LanguageInfo[]> LocalizedLanguagesCache = new FastMemCache<string, LanguageInfo[]>(TimeSpan.FromDays(1));

        readonly String[] InputLanguages;

    }


    /// <summary>
    /// Details on what to store
    /// </summary>
    public sealed class ChatStore
    {
        /// <summary>
        /// The URL to store.
        /// </summary>
        public String Url;
        /// <summary>
        /// The access scope of the stored data.
        /// Always assume that the user wants a public scope, unless they state that it's private or if they wan't to share it with other users (use protected).
        /// </summary>
        [OpenAiOptional]
        public UserStorageScopes Scope = UserStorageScopes.Public;
    }

}
