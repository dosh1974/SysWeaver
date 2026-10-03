using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Chat;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;
using TiktokenSharp;


namespace SysWeaver.AI
{
    public sealed partial class OpenAiService
    {

        /// <summary>
        /// The tier used when not supplying a model in a session (can be configured)
        /// </summary>
        public readonly OpenAiServiceTier DefaultTier;

        /// <summary>
        /// The API used to implement chat sessions when not specified in the session parameters (can be configured)
        /// </summary>
        public readonly OpenAiChatApi DefaultChatApi;

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

#pragma warning disable OPENAI001
        /// <summary>
        /// Create a client for the responses API (the model is specified per request)
        /// </summary>
        /// <returns></returns>
        public ResponsesClient CreateResponsesClient()
            => new OpenAIClient(ApiKey, Options).GetResponsesClient();
#pragma warning restore OPENAI001


        /// <summary>
        /// Create a chat session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="isPrivate">If true, this chat is user only</param>
        /// <param name="p">Optional parameters for this session, if null, the default will be used.
        /// Set p.ChatApi to select the implementation (chat completions or responses API), if null DefaultChatApi is used.</param>
        /// <param name="joinAuth">Optional comma separated list of required auth tokens</param>
        /// <param name="clearAuth">Auth required to clear the chat session</param>
        /// <param name="defaultTools">true to add some default tools for the AI to use</param>
        /// <param name="memory">An optional memory implementation </param>
        /// <returns></returns>
        public IOpenAiChatSession CreateChatSession(bool isPrivate, OpenAiSessionParams p = null, String joinAuth = "", String clearAuth = "Admin", bool defaultTools = true, IAiMemory memory = null)
        {
            p = p ?? new OpenAiSessionParams();
            if (String.IsNullOrEmpty(p.Model))
                p.Model = DefaultChatModel;
            p.Tier = p.Tier ?? DefaultTier;
            p.Reasoning = p.Reasoning ?? DefaultReasoning;
            p.ChatApi = p.ChatApi ?? DefaultChatApi;

            var mon = PerfMon;
            IOpenAiChatSession s = p.ChatApi == OpenAiChatApi.Responses
                ? new OpenAiResponseChatSession(isPrivate, CreateResponsesClient(), p, this, joinAuth, clearAuth, mon, ChatLock, memory)
                : new OpenAiCompletionsChatSession(isPrivate, new ChatClient(p.Model, ApiKey, Options), p, this, joinAuth, clearAuth, mon, ChatLock, memory);
            if (defaultTools)
                AddDefaultTools(s);
            return s;
        }

        protected override IAiChatSession CreateAiChatSession(bool isPrivate, AiSessionParams p, String joinAuth, String clearAuth, bool defaultTools, IAiMemory memory)
            => CreateChatSession(isPrivate, OpenAiSessionParams.From(p), joinAuth, clearAuth, defaultTools, memory);


        /// <summary>
        /// Create a query session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="p">Optional parameters for this session, if null, the default will be used.</param>
        /// <returns></returns>
        public OpenAiQuerySession CreateQuerySession(OpenAiSessionParams p = null)
        {
            p = p ?? new OpenAiSessionParams();
            if (String.IsNullOrEmpty(p.Model))
                p.Model = DefaultChatModel;
            p.Tier = p.Tier ?? DefaultTier;
            p.Reasoning = p.Reasoning ?? DefaultReasoning;
            var mon = PerfMon;
            var s = new OpenAiQuerySession(new ChatClient(p.Model, ApiKey, Options), p, this, mon);
            return s;
        }

        /// <summary>
        /// Create a query session (supporting SystemPrompt, tools etc)
        /// </summary>
        /// <param name="model">The model to use.</param>
        /// <returns></returns>
        public OpenAiQuerySession CreateQuerySession(String model)
            => CreateQuerySession(new OpenAiSessionParams { Model = model });

        protected override IAiQuerySession CreateAiQuerySession(AiSessionParams p)
            => CreateQuerySession(OpenAiSessionParams.From(p));


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
            => InternalChatSession(prompt, context);

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

    }

}
