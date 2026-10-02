using OpenAI;
using System;
using System.ClientModel;
using SysWeaver.Chat;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;
using TiktokenSharp;

namespace SysWeaver.AI
{

    [WebApiUrl("../openAI")]
    [RequiredDep<ApiHttpServerModule>]
    [OptionalDep<IUserStorageService>]
    [OptionalDep<IQrCodeService>]
    [OptionalDep<IHaveOpenAiTools>]
    [AiToolPrefix("")]
    public sealed partial class OpenAiService : AiServiceBase
    {
        /// <summary>
        /// The url root of the web resources of this service
        /// </summary>
        internal const String WebUrlRoot = "../openAI/";

        /// <summary>
        /// The url root of the icons of this service
        /// </summary>
        internal const String IconRoot = WebUrlRoot + "icons/";

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

        OpenAiService(ServiceManager sm, OpenAiParams p, IMessageHost msg, int _)
            : base(sm, p, msg, "OpenAI", "gpt-4.1", "dall-e-3", WebUrlRoot, "OpenAI")
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
            var tokenCache = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.TokenCacheFolder ?? @"$(CommonApplicationData)\SysWeaver_Tiktoken\"));
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
                        }else
                        {
                            msg?.AddMessage("Cached TikToken for " + t.ToQuoted(), MessageLevels.Debug);
                        }
                    }
                }
            }
        }

        readonly ApiKeyCredential ApiKey;
        readonly OpenAIClientOptions Options;

    }


}
