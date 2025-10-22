using OpenAI;
using OpenAI.Chat;
using System;
using System.Buffers;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
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

    [WebApiUrl("../openAI")]
    [RequiredDep<ApiHttpServerModule>]
    [OptionalDep<IUserStorageService>]
    [OptionalDep<IQrCodeService>]
    [OptionalDep<IHaveOpenAiTools>]
    [OpenAiToolPrefix("")]
    public sealed partial class OpenAiService : IChatProvider, IPerfMonitored, IOpenAiToolCache, IDisposable
    {

        public override string ToString() => String.Concat("Chat name: ", Name.ToQuoted(), ", Default chat model: ", DefaultChatModel.ToQuoted());


        public OpenAiService(ServiceManager sm, OpenAiParams p)
        {
            Manager = sm;
            p = p ?? new OpenAiParams();
            var m = p.DefaultChatModel;
            if (String.IsNullOrEmpty(m))
                m = "gpt-4.1";
            var n = p.ChatName;
            if (String.IsNullOrEmpty(n))
                n = "OpenAI";
            QrCode = sm.TryGet<IQrCodeService>();
            Name = n;
            SessionChatPrefix = n + ".ChatSession.";
            DefaultChatModel = m;
            
            m = p.DefaultImageModel;
            if (String.IsNullOrEmpty(m))
                m = "dall-e-3";
            DefaultImageModel = m;


            ImageGenLock = p.MaxConcurrentImages > 0 ? new AsyncLock(p.MaxConcurrentImages) : null;
            ChatLock = p.MaxConcurrentChats > 0 ? new AsyncLock(p.MaxConcurrentChats) : null;

            Api = sm.TryGet<ApiHttpServerModule>();
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
                sm.AddMessage("Caching tokens:", MessageLevels.Debug);
                using (sm.Tab())
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
                            sm.AddMessage("Failed to get a TikToken for " + t.ToQuoted(), exception, MessageLevels.Warning);
                        }else
                        {
                            sm.AddMessage("Cached TikToken for " + t.ToQuoted(), MessageLevels.Debug);
                        }
                    }
                }
            }

            AddCommand("help", CmdHelp, null, "Show all available commands");
            AddCommand("commands", CmdHelp, null, "Show all available commands");
            AddCommand("save", CmdSaveConversation, null, "Save the current conversation for training", ["debug"]);
            AddCommand("prompt", CmdShowPrompt, null, "Show the current system prompt", ["debug"]);
            AddCommand("clear", CmdClear, null, "Clear the current chat");
            UserStorage = sm.TryGet<IUserStorageService>();
            foreach (var x in sm.UniqueInstances)
            {
                AddTools(x as IHaveOpenAiTools);
            }
            sm.OnServiceAdded += Sm_OnServiceAdded;
            sm.OnServiceRemoved += Sm_OnServiceRemoved;
        }

        readonly IUserStorageService UserStorage;
        readonly ServiceManager Manager;

        public void Dispose()
        {
            var sm = Manager;
            sm.OnServiceRemoved -= Sm_OnServiceRemoved;
            sm.OnServiceAdded -= Sm_OnServiceAdded;
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
            var pref = t.Name + ".";
            var perfMonitor = PerfMon;
            foreach (var mm in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (mm.GetCustomAttribute<OpenAiToolAttribute>() == null)
                    if (!(mm.GetCustomAttribute<OpenAiUseAttribute>()?.Use ?? false))
                        continue;
                var fn = GetToolName(mm);
                var endPoint = ApiHttpEntry.Create(IoParams, a, mm, fn, perfMonitor, ApiHttpEntry.DefaultAuth, ApiHttpEntry.DefaultCachedCompression, ApiHttpEntry.DefaultLocationPrefix);
                GetTool(fn, endPoint);
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
            var pref = t.Name + ".";
            var cache = ApiTools;
            foreach (var mm in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (mm.GetCustomAttribute<OpenAiToolAttribute>() == null)
                    if (!(mm.GetCustomAttribute<OpenAiUseAttribute>()?.Use ?? false))
                        continue;
                var fn = GetToolName(mm);
                cache.TryRemove(fn, out var _);
            }
        }


        readonly IQrCodeService QrCode;


        public PerfMonitor PerfMon { get; } = new PerfMonitor("OpenAI");


        readonly ApiKeyCredential ApiKey;
        readonly OpenAIClientOptions Options;
        readonly ApiHttpServerModule Api;



    }


}
