using System;
using System.Reflection;
using System.Threading.Tasks;
using SysWeaver.Chat;
using SysWeaver.MicroService;
using SysWeaver.Net;
using SysWeaver.Serialization;

namespace SysWeaver.AI
{

    /// <summary>
    /// API agnostic base class for AI services.
    /// Implements the chat provider, the tool cache, commands, default tools etc.
    /// Derived classes implements the creation of sessions (and other API specific functionality).
    /// Note: WebApi methods must be declared in the derived classes (the url is based on the declaring type).
    /// </summary>
    [AiToolPrefix("")]
    public abstract partial class AiServiceBase : IAiService, IDisposable
    {

        public override string ToString() => String.Concat("Chat name: ", Name.ToQuoted(), ", Default chat model: ", DefaultChatModel.ToQuoted());

        protected readonly IMessageHost Msg;

        /// <summary>
        /// Create an AI service
        /// </summary>
        /// <param name="sm">The service manager</param>
        /// <param name="p">The parameters (must be non-null)</param>
        /// <param name="msg">Message host (if null the service manager is used)</param>
        /// <param name="defaultName">The name of the chat provider if not specified in the parameters</param>
        /// <param name="defaultChatModel">The default chat model if not specified in the parameters</param>
        /// <param name="defaultImageModel">The default image model if not specified in the parameters</param>
        /// <param name="webRoot">The url root of the web resources of the derived service (where the WebApi's of the derived type are), ex: "../openAI/".
        /// The "icons" sub folder must contain: "debug.svg", "error.svg", "working.svg", "Smiley_Angry.svg", "Smiley_HappyCrying.svg", "Smiley_Love.svg", "Smiley_Sad.svg", "Smiley_Tounge.svg".
        /// </param>
        /// <param name="perfMonitorName">The name of the performance monitor</param>
        protected AiServiceBase(ServiceManager sm, AiServiceParams p, IMessageHost msg, String defaultName, String defaultChatModel, String defaultImageModel, String webRoot, String perfMonitorName)
        {
            msg = msg ?? sm;
            Msg = msg;
            Manager = sm;
            PerfMon = new PerfMonitor(perfMonitorName);
            WebRoot = webRoot;
            var m = p.DefaultChatModel;
            if (String.IsNullOrEmpty(m))
                m = defaultChatModel;
            var n = p.ChatName;
            if (String.IsNullOrEmpty(n))
                n = defaultName;
            QrCode = sm?.TryGet<IQrCodeService>();
            Name = n;
            SessionChatPrefix = n + ".ChatSession.";
            DefaultChatModel = m;
            DefaultReasoning = p.DefaultReasoning;

            m = p.DefaultImageModel;
            if (String.IsNullOrEmpty(m))
                m = defaultImageModel;
            DefaultImageModel = m;

            ImageGenLock = p.MaxConcurrentImages > 0 ? new AsyncLock(p.MaxConcurrentImages) : null;
            ChatLock = p.MaxConcurrentChats > 0 ? new AsyncLock(p.MaxConcurrentChats) : null;

            Api = sm?.TryGet<ApiHttpServerModule>();

            AddCommand("help", CmdHelp, null, "Show all available commands");
            AddCommand("commands", CmdHelp, null, "Show all available commands");
            AddCommand("save", CmdSaveConversation, null, "Save the current conversation for training", ["debug"]);
            AddCommand("prompt", CmdShowPrompt, null, "Show the current system prompt", ["debug"]);
            AddCommand("clear", CmdClear, null, "Clear the current chat");
            UserStorage = sm?.TryGet<IUserStorageService>();
            if (sm != null)
            {
                foreach (var x in sm.UniqueInstances)
                {
                    AddTools(x as IHaveOpenAiTools);
                }
                sm.OnServiceAdded += Sm_OnServiceAdded;
                sm.OnServiceRemoved += Sm_OnServiceRemoved;
            }
        }

        /// <summary>
        /// The url root of the web resources of the derived service, ex: "../openAI/"
        /// </summary>
        protected readonly String WebRoot;

        /// <summary>
        /// The model used when not supplying a model in a session (can be configured)
        /// </summary>
        public String DefaultChatModel { get; }

        /// <summary>
        /// The reasoning effort used when not supplying a reasoning effort in a session (can be configured)
        /// </summary>
        public AiReasoning DefaultReasoning { get; }

        /// <summary>
        /// The model used when supplying an empty model (can be configured)
        /// </summary>
        public String DefaultImageModel { get; }

        /// <summary>
        /// Optional lock used to limit the number of concurrent chat requests
        /// </summary>
        protected readonly AsyncLock ChatLock;

        /// <summary>
        /// Optional lock used to limit the number of concurrent image generations
        /// </summary>
        protected readonly AsyncLock ImageGenLock;

        protected readonly IUserStorageService UserStorage;
        protected readonly ServiceManager Manager;
        protected readonly IQrCodeService QrCode;
        protected readonly ApiHttpServerModule Api;

        public PerfMonitor PerfMon { get; }

        public virtual void Dispose()
        {
            var sm = Manager;
            if (sm != null)
            {
                sm.OnServiceRemoved -= Sm_OnServiceRemoved;
                sm.OnServiceAdded -= Sm_OnServiceAdded;
            }
        }

        void Sm_OnServiceAdded(object arg1, ServiceInfo arg2)
        {
            AddTools(arg1 as IHaveOpenAiTools);
        }

        void Sm_OnServiceRemoved(object arg1, ServiceInfo arg2)
        {
            RemoveTools(arg1 as IHaveOpenAiTools);
        }


        /// <summary>
        /// Register tools from an instance (tools still have to be added to a session)
        /// </summary>
        /// <param name="a"></param>
        public void AddTools(IHaveOpenAiTools a)
        {
            if (a == null)
                return;
            var t = a.GetType();
            var perfMonitor = PerfMon;
            var api = Api;
            foreach (var mm in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (mm.GetCustomAttribute<AiToolAttribute>() == null)
                    if (!(mm.GetCustomAttribute<OpenAiUseAttribute>()?.Use ?? false))
                        continue;
                if (api.TryGetApi(mm, out var url))
                {
                    GetTool(url);
                    continue;
                }
                var fn = GetToolName(mm);
                var endPoint = ApiHttpEntry.Create(IoParams, a, mm, fn, perfMonitor, ApiHttpEntry.DefaultAuth, ApiHttpEntry.DefaultCachedCompression, ApiHttpEntry.DefaultLocationPrefix);
                GetTool(fn, endPoint, false);
            }
        }

        static readonly ISerializerType JsonSer = SerManager.Get("json");

        public static readonly ApiIoParams IoParams = new ApiIoParams([JsonSer], [JsonSer], JsonSer, JsonSer);

        /// <summary>
        /// Unregister tools from an instance
        /// </summary>
        /// <param name="a"></param>
        public void RemoveTools(IHaveOpenAiTools a)
        {
            if (a == null)
                return;
            var t = a.GetType();
            var cache = ApiTools;
            foreach (var mm in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (mm.GetCustomAttribute<AiToolAttribute>() == null)
                    if (!(mm.GetCustomAttribute<OpenAiUseAttribute>()?.Use ?? false))
                        continue;
                var fn = GetToolName(mm);
                cache.TryRemove(fn, out var _);
            }
        }

        #region Sessions

        /// <summary>
        /// Create a chat session (API specific).
        /// </summary>
        /// <param name="isPrivate">If true, this chat is user only</param>
        /// <param name="p">Optional parameters for this session, if null, the default will be used (can be a derived API specific type)</param>
        /// <param name="joinAuth">Optional comma separated list of required auth tokens</param>
        /// <param name="clearAuth">Auth required to clear the chat session</param>
        /// <param name="defaultTools">true to add some default tools for the AI to use (call AddDefaultTools)</param>
        /// <param name="memory">An optional memory implementation </param>
        /// <returns>A new chat session</returns>
        protected abstract IAiChatSession CreateAiChatSession(bool isPrivate, AiSessionParams p, String joinAuth, String clearAuth, bool defaultTools, IAiMemory memory);

        /// <summary>
        /// Create a query session (API specific).
        /// </summary>
        /// <param name="p">Optional parameters for this session, if null, the default will be used (can be a derived API specific type)</param>
        /// <returns>A new query session</returns>
        protected abstract IAiQuerySession CreateAiQuerySession(AiSessionParams p);

        IAiChatSession IAiService.CreateChatSession(bool isPrivate, AiSessionParams p, String joinAuth, String clearAuth, bool defaultTools, IAiMemory memory)
            => CreateAiChatSession(isPrivate, p, joinAuth, clearAuth, defaultTools, memory);

        IAiQuerySession IAiService.CreateQuerySession(AiSessionParams p)
            => CreateAiQuerySession(p);

        IAiQuerySession IAiService.CreateQuerySession(String model)
            => CreateAiQuerySession(new AiSessionParams { Model = model });

        /// <summary>
        /// Add the default tools to a chat session, override to add API specific tools (call the base).
        /// </summary>
        /// <param name="s">The chat session</param>
        protected virtual void AddDefaultTools(IAiChatSession s)
        {
            AddTool_GetPredefinedImage(s);
            AddTool_GetFileExtensionIcon(s);
            AddTool_GetCountryFlagIcon(s);
            //AddTool_BuildLogo(s);
            AddTool_BuildQrCode(s);
            AddTool_BuildTable(s);
            AddTool_BuildData(s);

            AddTool_DisplayUrl(s);
            AddTool_Calculate(s);
            AddTool_ElapsedTime(s);
            //AddTool_DisplayData(s);
            //AddTool_DisplayTable(s);

            AddTool_BuildGraph(s);

            AddTool_BuildMap(s);
            AddTool_Store(s);

            AddTool_ValidateRestApiCall(s);

            //AddTool_DisplayGraph(s);
        }

        /// <summary>
        /// Chat session for debugging, only use to test server connection etc (should be exposed as a WebApi by the derived class).
        /// </summary>
        /// <param name="prompt">Some prompt</param>
        /// <param name="context">The request</param>
        /// <returns>Some response</returns>
        protected Task<String> InternalChatSession(String prompt, HttpServerRequest context)
        {
            var key = Name + "DebugSession";
            if (!context.Session.TryGet<IAiChatSession>(key, out var s))
            {
                s = CreateAiChatSession(true, null, "Debug", "Debug", true, null);
                s.ChatId = key;
                context.Session.TryAdd(key, s);
            }
            return s.Complete(prompt, context);
        }

        #endregion//Sessions

        #region Images

        /// <summary>
        /// Store a generated image (in the user storage if available, else as message data) and return an url to it.
        /// Typically used by image generation tools.
        /// </summary>
        /// <param name="c">The tool context</param>
        /// <param name="request">The request</param>
        /// <param name="png">The png image data</param>
        /// <param name="title">The title of the image (used as the filename)</param>
        /// <returns>An url to the image</returns>
        protected async Task<String> StoreGeneratedImage(AiToolContext c, HttpServerRequest request, ReadOnlyMemory<Byte> png, String title)
        {
            var us = UserStorage;
            var filename = title ?? "Image";
            if (us == null)
            {
                var data = "data:image/png;base64," + Convert.ToBase64String(png.Span);
                return c.AddMessageFile("image/png", data, filename);
            }
            var s = c.Session;
            if (s.IsPrivate)
                return "../" + await us.StorePrivateFile(request, filename + ".png", png).ConfigureAwait(false);
            return "../" + await us.StorePublicFile(request, filename + ".png", png, String.Join(',', s.JoinAuth)).ConfigureAwait(false);
        }

        #endregion//Images

    }


}
