using Google.GenAI;
using Google.GenAI.Types;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Chat;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// An AI service (chat provider etc) using the Google Gemini API (or Vertex AI).
    /// See: https://ai.google.dev/api
    /// </summary>
    [WebApiUrl("../google")]
    [RequiredDep<ApiHttpServerModule>]
    [OptionalDep<IUserStorageService>]
    [OptionalDep<IQrCodeService>]
    [OptionalDep<IHaveOpenAiTools>]
    [AiToolPrefix("")]
    public sealed partial class GoogleAiService : AiServiceBase
    {
        /// <summary>
        /// The url root of the web resources of this service
        /// </summary>
        internal const String WebUrlRoot = "../google/";

        /// <summary>
        /// The url root of the icons of this service
        /// </summary>
        internal const String IconRoot = WebUrlRoot + "icons/";

        /// <summary>
        /// Create a Google AI service
        /// </summary>
        /// <param name="sm">The service manager</param>
        /// <param name="p">The parameters</param>
        /// <param name="msg">Optional message host</param>
        public GoogleAiService(ServiceManager sm = null, GoogleAiParams p = null, IMessageHost msg = null)
            : this(sm, p ?? new GoogleAiParams(), msg, 0)
        {
        }

        GoogleAiService(ServiceManager sm, GoogleAiParams p, IMessageHost msg, int _)
            : base(sm, p, msg, "Google", "gemini-2.5-flash", "gemini-2.5-flash-image", WebUrlRoot, "Google")
        {
            DefaultTier = p.DefaultTier;
            var apiKey = p.GetApiKey(false);
            var httpOptions = new HttpOptions
            {
                BaseUrl = String.IsNullOrEmpty(p.EndPoint) ? null : p.EndPoint,
                ApiVersion = String.IsNullOrEmpty(p.ApiVersion) ? null : p.ApiVersion,
                Timeout = Math.Max(5, p.NetworkTimeoutSeconds) * 1000,
            };
            var vertex = p.VertexAI;
            var project = String.IsNullOrEmpty(p.Project) ? null : p.Project;
            var location = String.IsNullOrEmpty(p.Location) ? null : p.Location;
            //  Created on first use, so that a missing api key doesn't prevent the service from starting
            InternalClient = new Lazy<Client>(() => new Client(
                vertexAI: vertex ? true : null,
                apiKey: String.IsNullOrEmpty(apiKey) ? null : apiKey,
                project: project,
                location: location,
                httpOptions: httpOptions));
        }

        readonly Lazy<Client> InternalClient;

        /// <summary>
        /// The Google GenAI client used by this service
        /// </summary>
        public Client Client => InternalClient.Value;

        public override void Dispose()
        {
            base.Dispose();
            if (InternalClient.IsValueCreated)
                InternalClient.Value.Dispose();
        }

        /// <summary>
        /// The tier used when not supplying a tier in a session (can be configured)
        /// </summary>
        public readonly GoogleServiceTier DefaultTier;

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
        public GoogleChatSession CreateChatSession(bool isPrivate, GoogleSessionParams p = null, String joinAuth = "", String clearAuth = "Admin", bool defaultTools = true, IAiMemory memory = null)
        {
            p = p ?? new GoogleSessionParams();
            if (String.IsNullOrEmpty(p.Model))
                p.Model = DefaultChatModel;
            p.Tier = p.Tier ?? DefaultTier;
            p.Reasoning = p.Reasoning ?? DefaultReasoning;
            var s = new GoogleChatSession(isPrivate, Client, p, this, joinAuth, clearAuth, PerfMon, ChatLock, memory);
            if (defaultTools)
                AddDefaultTools(s);
            return s;
        }

        protected override IAiChatSession CreateAiChatSession(bool isPrivate, AiSessionParams p, String joinAuth, String clearAuth, bool defaultTools, IAiMemory memory)
            => CreateChatSession(isPrivate, GoogleSessionParams.From(p), joinAuth, clearAuth, defaultTools, memory);

        /// <summary>
        /// Create a query session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="p">Optional parameters for this session, if null, the default will be used.</param>
        /// <returns>A new query session</returns>
        public GoogleQuerySession CreateQuerySession(GoogleSessionParams p = null)
        {
            p = p ?? new GoogleSessionParams();
            if (String.IsNullOrEmpty(p.Model))
                p.Model = DefaultChatModel;
            p.Tier = p.Tier ?? DefaultTier;
            p.Reasoning = p.Reasoning ?? DefaultReasoning;
            return new GoogleQuerySession(Client, p, this, PerfMon);
        }

        /// <summary>
        /// Create a query session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="model">The model to use.</param>
        /// <returns>A new query session</returns>
        public GoogleQuerySession CreateQuerySession(String model)
            => CreateQuerySession(new GoogleSessionParams { Model = model });

        protected override IAiQuerySession CreateAiQuerySession(AiSessionParams p)
            => CreateQuerySession(GoogleSessionParams.From(p));

        #endregion//Sessions

        #region Debug

        /// <summary>
        /// Simple chat complete, only use to test server connection etc.
        /// </summary>
        /// <param name="prompt">Some prompt</param>
        /// <returns>Some response</returns>
        [WebApi("debug/" + nameof(ChatComplete))]
        [WebApiAuth(Roles.Debug)]
        public async Task<String> ChatComplete(String prompt)
        {
            GenerateContentResponse r;
            using (var _ = await (ChatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
                r = await Client.Models.GenerateContentAsync(DefaultChatModel, prompt).ConfigureAwait(false);
            var br = r.PromptFeedback?.BlockReason;
            if (br != null)
                throw new Exception("Model refused to complete: " + br.Value);
            return r.Text;
        }

        /// <summary>
        /// Chat session for debugging, only use to test server connection etc.
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
        public async Task<int> TokenCount(String text)
        {
            var r = await Client.Models.CountTokensAsync(DefaultChatModel, text).ConfigureAwait(false);
            return r.TotalTokens ?? 0;
        }

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
