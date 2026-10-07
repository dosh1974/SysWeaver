using OpenAI;
using System;
using System.ClientModel;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;
using TiktokenSharp;

namespace SysWeaver.AI
{

    [WebApiUrl("../" + ServiceName)]
    [RequiredDep<ApiHttpServerModule>]
    [OptionalDep<IUserStorageService>]
    [OptionalDep<IQrCodeService>]
    [OptionalDep<IHaveOpenAiTools>]
    [AiToolPrefix("")]
    public sealed partial class OpenAiService : AiServiceBase
    {

        /// <summary>
        ///
        /// </summary>
        /// <param name="sm"></param>
        /// <param name="p"></param>
        /// <param name="msg"></param>
        public OpenAiService(ServiceManager sm = null, OpenAiParams p = null, IMessageHost msg = null)
            : this(sm, p ?? new OpenAiParams(), msg, 0)
        {
        }

        internal const String ServiceName = "OpenAI";

        OpenAiService(ServiceManager sm, OpenAiParams p, IMessageHost msg, int _)
            : base(sm, p, msg, ServiceName, "gpt-4.1", "dall-e-3")
        {
            msg = Msg;
            DefaultTier = p.DefaultTier;
            DefaultChatApi = p.DefaultChatApi;

            var apiKey = p.GetApiKey(false);
            ApiKey = new ApiKeyCredential(apiKey ?? "Demo");
            Options = new OpenAIClientOptions
            {
                Endpoint = String.IsNullOrEmpty(p.EndPoint) ? null : new Uri(p.EndPoint),
                OrganizationId = String.IsNullOrEmpty(p.OrganizationId) ? null : p.OrganizationId,
                ProjectId = String.IsNullOrEmpty(p.ProjectId) ? null : p.ProjectId,
                UserAgentApplicationId = String.IsNullOrEmpty(p.UserAgentApplicationId) ? null : p.UserAgentApplicationId,
                NetworkTimeout = TimeSpan.FromSeconds(Math.Max(5, p.NetworkTimeoutSeconds)),
            };
            var tokenCache = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.TokenCacheFolder ?? @"$(CommonApplicationData)/SysWeaver_Tiktoken/"));
            PathExt.EnsureFolderExist(tokenCache);
            TikToken.PBEFileDirectory = tokenCache;
            var cache = p.CacheTokensFor;
            if ((cache?.Length ?? 0) > 0)
            {
                msg?.AddMessage("Caching tokens:", MessageLevels.Debug);
                using (msg?.Tab())
                {
                    foreach (var t in cache)
                    {
                        TikToken tikToken = null;
                        Exception exception = null;
                        try
                        {
                            tikToken = GetToken(t);
                            tikToken.Encode("hello world");
                        }
                        catch (Exception ex)
                        {
                            exception = ex;
                        }
                        if (tikToken == null)
                        {
                            msg?.AddMessage("Failed to get a TikToken for " + t.ToQuoted(), exception, MessageLevels.Warning);
                        }
                        else
                        {
                            msg?.AddMessage("Cached TikToken for " + t.ToQuoted(), MessageLevels.Debug);
                        }
                    }
                }
            }
        }

        readonly ApiKeyCredential ApiKey;
        readonly OpenAIClientOptions Options;

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

        /// <summary>
        /// Get a table with all available image models
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebMenuTable(null, "Debug/AI/Models/Image/" + ServiceName, ServiceName, "All available image models from this " + ServiceName + " service")]
        public async Task<TypedTableData<AiImageModel>> GetImageModelTable(TableDataRequest req)
            => TableDataTools.GetTyped(req, await GetImageModels().ConfigureAwait(false), ServiceName + " image models");

        #endregion//DEBUG

    }


}