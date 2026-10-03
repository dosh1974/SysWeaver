using LLama.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Chat;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// An AI service (chat provider etc) using local LLM's (GGUF files) through LLamaSharp (llama.cpp).
    /// All models are loaded when the service is created.
    /// See: https://github.com/SciSharp/LLamaSharp
    /// </summary>
    [WebApiUrl("../llamaSharp")]
    [RequiredDep<ApiHttpServerModule>]
    [OptionalDep<IUserStorageService>]
    [OptionalDep<IQrCodeService>]
    [OptionalDep<IHaveOpenAiTools>]
    [AiToolPrefix("")]
    public sealed partial class LlamaSharpAiService : AiServiceBase
    {
        /// <summary>
        /// The url root of the web resources of this service
        /// </summary>
        internal const String WebUrlRoot = "../llamaSharp/";

        /// <summary>
        /// The url root of the icons of this service
        /// </summary>
        internal const String IconRoot = WebUrlRoot + "icons/";

        /// <summary>
        /// Create a LLamaSharp AI service, all models are loaded
        /// </summary>
        /// <param name="sm">The service manager</param>
        /// <param name="p">The parameters</param>
        /// <param name="msg">Optional message host</param>
        public LlamaSharpAiService(ServiceManager sm = null, LlamaSharpAiParams p = null, IMessageHost msg = null)
            : this(sm, p ?? new LlamaSharpAiParams(), msg, 0)
        {
        }

        LlamaSharpAiService(ServiceManager sm, LlamaSharpAiParams p, IMessageHost msg, int _)
            : base(sm, p, msg, "LlamaSharp", p.Llms?.FirstOrDefault(x => !String.IsNullOrEmpty(x?.Name))?.Name ?? "", "", WebUrlRoot, "LlamaSharp")
        {
            msg = Msg;
            InitNative(p, msg);
            var models = new Dictionary<String, LlamaSharpModel>(StringComparer.OrdinalIgnoreCase);
            var llms = p.Llms;
            if ((llms?.Length ?? 0) > 0)
            {
                msg?.AddMessage("Loading local LLM's:", MessageLevels.Debug);
                using (msg?.Tab())
                {
                    foreach (var l in llms)
                    {
                        if (l == null)
                            continue;
                        var name = l.Name;
                        if (String.IsNullOrEmpty(name))
                        {
                            msg?.AddMessage("A model without a name can't be loaded, file: " + l.FilePath.ToQuoted(), MessageLevels.Error);
                            continue;
                        }
                        if (models.ContainsKey(name))
                        {
                            msg?.AddMessage("A model named " + name.ToQuoted() + " is already loaded, ignoring file: " + l.FilePath.ToQuoted(), MessageLevels.Error);
                            continue;
                        }
                        if (String.IsNullOrEmpty(l.FilePath))
                        {
                            msg?.AddMessage("The model " + name.ToQuoted() + " have no file path", MessageLevels.Error);
                            continue;
                        }
                        var fileName = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(l.FilePath));
                        if (!File.Exists(fileName))
                        {
                            msg?.AddMessage("The model file " + fileName.ToQuoted() + " of " + name.ToQuoted() + " doesn't exist", MessageLevels.Error);
                            continue;
                        }
                        try
                        {
                            var m = new LlamaSharpModel(l, fileName);
                            models.Add(name, m);
                            msg?.AddMessage(String.Concat("Loaded ", name.ToQuoted(), " from ", fileName.ToQuoted(), ", context size: ", m.ContextSize.ToString()), MessageLevels.Debug);
                        }
                        catch (Exception ex)
                        {
                            msg?.AddMessage("Failed to load the model " + name.ToQuoted() + " from " + fileName.ToQuoted(), ex, MessageLevels.Error);
                        }
                    }
                }
            }
            else
                msg?.AddMessage("No local LLM's are specified", MessageLevels.Warning);
            Models = models;
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (var m in Models.Values)
                m.Dispose();
        }

        #region Native

        static readonly Object NativeLock = new Object();
        static volatile IMessageHost NativeLog;

        /// <summary>
        /// Select the backend and setup logging (process wide, must be done before the native library is loaded)
        /// </summary>
        static void InitNative(LlamaSharpAiParams p, IMessageHost msg)
        {
            lock (NativeLock)
            {
                if (p.LogNative && (msg != null))
                    NativeLog = msg;
                if (NativeLibraryConfig.LLama.LibraryHasLoaded)
                    return;
                var c = NativeLibraryConfig.All.WithVulkan(p.UseVulkan).WithAutoFallback(true);
                c.WithLogCallback(OnNativeLog);
            }
        }

        static void OnNativeLog(LLamaLogLevel level, String message)
        {
            var m = NativeLog;
            if (m == null)
                return;
            MessageLevels l;
            switch (level)
            {
                //  Warnings are mostly informational (and some are repeated for every request)
                case LLamaLogLevel.Warning:
                    l = MessageLevels.Debug;
                    break;
                case LLamaLogLevel.Error:
                    l = MessageLevels.Error;
                    break;
                default:
                    return;
            }
            message = message?.TrimEnd();
            if (String.IsNullOrEmpty(message))
                return;
            m.AddMessage("llama.cpp: " + message, l);
        }

        #endregion//Native

        #region Models

        /// <summary>
        /// The loaded models (key is the model name / code)
        /// </summary>
        readonly IReadOnlyDictionary<String, LlamaSharpModel> Models;

        /// <summary>
        /// All loaded models
        /// </summary>
        public IEnumerable<LlamaSharpModel> LoadedModels => Models.Values;

        /// <summary>
        /// Get a loaded model
        /// </summary>
        /// <param name="name">The name (model code) of the model, null or empty for the default model</param>
        /// <returns>The model</returns>
        /// <exception cref="Exception">Thrown if no model with that name is loaded</exception>
        public LlamaSharpModel GetModel(String name = null)
        {
            if (String.IsNullOrEmpty(name))
                name = DefaultChatModel;
            if (Models.TryGetValue(name ?? "", out var m))
                return m;
            throw new Exception("No local LLM named " + name.ToQuoted() + " is loaded!");
        }

        protected override Task<AiModels> FetchModels()
        {
            var llms = Models.Values.Select(x => new AiLlmModel
            {
                Id = x.Name,
                Name = x.Llm.DisplayName,
                Description = x.Llm.Description,
                Owner = "local",
                Created = x.Created,
                InputTokenLimit = x.ContextSize,
                OutputTokenLimit = x.Llm.MaxTokens > 0 ? Math.Min(x.Llm.MaxTokens, x.ContextSize) : x.ContextSize,
                CanReason = x.Llm.CanReason,
            }).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
            return Task.FromResult(new AiModels(llms, []));
        }

        #endregion//Models

        #region Sessions

        /// <summary>
        /// Create a chat session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="isPrivate">If true, this chat is user only</param>
        /// <param name="p">Optional parameters for this session, if null, the default will be used.</param>
        /// <param name="joinAuth">Optional comma separated list of required auth tokens</param>
        /// <param name="clearAuth">Auth required to clear the chat session</param>
        /// <param name="defaultTools">true to add some default tools for the AI to use</param>
        /// <param name="memory">An optional memory implementation </param>
        /// <returns>A new chat session</returns>
        public LlamaSharpChatSession CreateChatSession(bool isPrivate, LlamaSharpSessionParams p = null, String joinAuth = "", String clearAuth = "Admin", bool defaultTools = true, IAiMemory memory = null)
        {
            var s = new LlamaSharpChatSession(isPrivate, GetModel(p?.Model), this, joinAuth, clearAuth, PerfMon, ChatLock, memory);
            if (defaultTools)
                AddDefaultTools(s);
            return s;
        }

        protected override IAiChatSession CreateAiChatSession(bool isPrivate, AiSessionParams p, String joinAuth, String clearAuth, bool defaultTools, IAiMemory memory)
            => CreateChatSession(isPrivate, LlamaSharpSessionParams.From(p), joinAuth, clearAuth, defaultTools, memory);

        /// <summary>
        /// Create a query session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="p">Optional parameters for this session, if null, the default will be used.</param>
        /// <returns>A new query session</returns>
        public LlamaSharpQuerySession CreateQuerySession(LlamaSharpSessionParams p = null)
            => new LlamaSharpQuerySession(GetModel(p?.Model), this, PerfMon);

        /// <summary>
        /// Create a query session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="model">The model to use.</param>
        /// <returns>A new query session</returns>
        public LlamaSharpQuerySession CreateQuerySession(String model)
            => CreateQuerySession(new LlamaSharpSessionParams { Model = model });

        protected override IAiQuerySession CreateAiQuerySession(AiSessionParams p)
            => CreateQuerySession(LlamaSharpSessionParams.From(p));

        #endregion//Sessions

        #region Debug

        /// <summary>
        /// Simple chat complete, only use to test that the model works etc.
        /// </summary>
        /// <param name="prompt">Some prompt</param>
        /// <returns>Some response</returns>
        [WebApi("debug/" + nameof(ChatComplete))]
        [WebApiAuth(Roles.Debug)]
        public Task<String> ChatComplete(String prompt)
            => CreateQuerySession(DefaultChatModel).Query(prompt);

        /// <summary>
        /// Chat session for debugging, only use to test that the model works etc.
        /// </summary>
        /// <param name="prompt">Some prompt</param>
        /// <param name="context"></param>
        /// <returns>Some response</returns>
        [WebApi("debug/" + nameof(ChatSession))]
        [WebApiAuth(Roles.Debug)]
        public Task<String> ChatSession(String prompt, HttpServerRequest context)
            => InternalChatSession(prompt, context);

        /// <summary>
        /// Count the number of tokens some text is using (with the default chat model)
        /// </summary>
        /// <param name="text">The text to count tokens for</param>
        /// <returns>The number of tokens</returns>
        [WebApi("debug/" + nameof(TokenCount))]
        [WebApiAuth(Roles.Debug)]
        public int TokenCount(String text)
            => GetModel().CountTokens(text);

        /// <summary>
        /// Do not use directly!
        /// Get used internally by an AI tool to display tables generated by the AI.
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        public Task<TableData> MessageTable(TableDataRequest r, HttpServerRequest context)
            => InternalMessageTable(r, context);

        #endregion//Debug

    }
}
