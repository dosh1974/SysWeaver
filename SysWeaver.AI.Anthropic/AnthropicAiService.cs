using Anthropic;
using Anthropic.Models.Messages;
using System;
using System.Threading.Tasks;
using SysWeaver.Chat;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// An AI service (chat provider etc) using the Anthropic Claude API.
    /// See: https://platform.claude.com/docs/en/api/overview
    /// </summary>
    [WebApiUrl("../" + ServiceName)]
    [RequiredDep<ApiHttpServerModule>]
    [OptionalDep<IUserStorageService>]
    [OptionalDep<IQrCodeService>]
    [OptionalDep<IHaveAiTools>]
    [AiToolPrefix("")]
    public sealed partial class AnthropicAiService : AiServiceBase
    {

        /// <summary>
        /// Create an Anthropic AI service
        /// </summary>
        /// <param name="sm">The service manager</param>
        /// <param name="p">The parameters</param>
        /// <param name="msg">Optional message host</param>
        public AnthropicAiService(ServiceManager sm = null, AnthropicAiParams p = null, IMessageHost msg = null)
            : this(sm, p ?? new AnthropicAiParams(), msg, 0)
        {
        }
        internal const String ServiceName = "Anthropic";

        AnthropicAiService(ServiceManager sm, AnthropicAiParams p, IMessageHost msg, int _)
            : base(sm, p, msg, ServiceName, "claude-opus-5-5", null)
        {
            DefaultMaxTokens = Math.Max(1, p.DefaultMaxTokens);
            PromptCaching = p.PromptCaching;
            var apiKey = p.GetApiKey(false);
            var baseUrl = String.IsNullOrEmpty(p.EndPoint) ? null : p.EndPoint.TrimEnd('/');
            var timeout = TimeSpan.FromSeconds(Math.Max(5, p.NetworkTimeoutSeconds));
            var retries = Math.Max(0, p.MaxRetries);
            //  Created on first use, so that a missing api key doesn't prevent the service from starting.
            //  If no api key is supplied, the client uses the ANTHROPIC_API_KEY environment variable (or other default credentials).
            InternalClient = new Lazy<IAnthropicClient>(IAnthropicClient () =>
            {
                var c = String.IsNullOrEmpty(apiKey)
                    ? new AnthropicClient { Timeout = timeout, MaxRetries = retries }
                    : new AnthropicClient { ApiKey = apiKey, Timeout = timeout, MaxRetries = retries };
                return baseUrl == null ? c : c.WithOptions(o => o with { BaseUrl = baseUrl });
            });
        }

        readonly Lazy<IAnthropicClient> InternalClient;

        /// <summary>
        /// The Anthropic client used by this service
        /// </summary>
        public IAnthropicClient Client => InternalClient.Value;

        public override void Dispose()
        {
            base.Dispose();
            if (InternalClient.IsValueCreated)
                (InternalClient.Value as IDisposable)?.Dispose();
        }

        /// <summary>
        /// The maximum number of output tokens used when not supplying a value in a session (can be configured)
        /// </summary>
        public readonly int DefaultMaxTokens;

        /// <summary>
        /// True if prompt caching is enabled (can be configured)
        /// </summary>
        public readonly bool PromptCaching;

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
        public AnthropicChatSession CreateChatSession(bool isPrivate, AnthropicSessionParams p = null, String joinAuth = "", String clearAuth = "Admin", bool defaultTools = true, IAiMemory memory = null)
        {
            p = p ?? new AnthropicSessionParams();
            if (String.IsNullOrEmpty(p.Model))
                p.Model = DefaultChatModel;
            p.MaxTokens = p.MaxTokens ?? DefaultMaxTokens;
            p.Reasoning = p.Reasoning ?? DefaultReasoning;
            var s = new AnthropicChatSession(isPrivate, Client, p, PromptCaching, this, joinAuth, clearAuth, PerfMon, ChatLock, memory);
            if (defaultTools)
                AddDefaultTools(s);
            return s;
        }

        protected override IAiChatSession CreateAiChatSession(bool isPrivate, AiSessionParams p, String joinAuth, String clearAuth, bool defaultTools, IAiMemory memory)
            => CreateChatSession(isPrivate, AnthropicSessionParams.From(p), joinAuth, clearAuth, defaultTools, memory);

        /// <summary>
        /// Create a query session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="p">Optional parameters for this session, if null, the default will be used.</param>
        /// <returns>A new query session</returns>
        public AnthropicQuerySession CreateQuerySession(AnthropicSessionParams p = null)
        {
            p = p ?? new AnthropicSessionParams();
            if (String.IsNullOrEmpty(p.Model))
                p.Model = DefaultChatModel;
            p.MaxTokens = p.MaxTokens ?? DefaultMaxTokens;
            p.Reasoning = p.Reasoning ?? DefaultReasoning;
            return new AnthropicQuerySession(Client, p, PromptCaching, this, PerfMon);
        }

        /// <summary>
        /// Create a query session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="model">The model to use.</param>
        /// <returns>A new query session</returns>
        public AnthropicQuerySession CreateQuerySession(String model)
            => CreateQuerySession(new AnthropicSessionParams { Model = model });

        protected override IAiQuerySession CreateAiQuerySession(AiSessionParams p)
            => CreateQuerySession(AnthropicSessionParams.From(p));

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
            var model = DefaultChatModel;
            var s = AnthropicModels.GetSettings(AnthropicModels.GetOptions(model), DefaultReasoning, DefaultMaxTokens);
            var p = AnthropicModels.BuildParams(model, s, null, null, AiCompletionToolChoices.Auto, null, [AnthropicModels.CreateUserMessage(prompt, null, null)], false);
            Anthropic.Models.Messages.Message r;
            using (var _ = await (ChatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
                r = await AnthropicModels.Send(Client, p, null).ConfigureAwait(false);
            var err = AnthropicModels.GetStopError(r);
            if (err != null)
                throw new Exception(err.Substring(7));
            return AnthropicModels.GetText(r);
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
        public async Task<long> TokenCount(String text)
        {
            var r = await Client.Messages.CountTokens(new MessageCountTokensParams
            {
                Model = DefaultChatModel,
                Messages = [AnthropicModels.CreateUserMessage(text, null, null)],
            }).ConfigureAwait(false);
            return r.InputTokens;
        }


        #endregion//Debug

        #region DEBUG

        /// <summary>
        /// Get a table with all available LLM models
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebMenuTable(null, "Debug/AI/Models/LLM/" + ServiceName, ServiceName, "All available LLM models from this " + ServiceName + " service")]
        public async Task<TypedTableData<AiLlmModel>> GetLlmModelTable(TableDataRequest req)
            => TableDataTools.GetTyped(req, await GetLlmModels().ConfigureAwait(false), ServiceName + " LLM models");

        #endregion//DEBUG
    }
}
