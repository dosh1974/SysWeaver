using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Net;
using SysWeaver.Security;

namespace SysWeaver.MicroService
{
    public abstract class HttpServerServiceBase<S, P> : IDisposable where S : HttpServerBase where P : HttpServerBaseParams, new()
    {

        public override string ToString() => "[Service] " + Server;

        public HttpServerServiceBase(ServiceManager manager, P p)
        {
            p = p ?? new P();
            Manager = manager;
            Params = p;
            var auth = manager.TryGet<AuthManager>();
            Dictionary<String, ICertificateProvider> certs = new Dictionary<String, ICertificateProvider>(StringComparer.OrdinalIgnoreCase);
            foreach (var ci in manager.GetAllInfo<ICertificateProvider>(ServiceInstanceTypes.Any, ServiceInstanceOrders.Oldest))
                certs[ci.Value.Name ?? "Default"] = ci.Key;
            var firewallHandler = manager.TryGet<IFirewallHandler>();
            var server = Create(out var paramType, out var instanceName, out var start, certs, auth, firewallHandler);
            Server = server;
            using (manager.Tab())
            {
                foreach (var instance in manager.GetAll(ServiceInstanceTypes.LocalOnly, ServiceInstanceOrders.Oldest))
                    OnServiceAdded(instance, null);
            }
            //  Handle future add/removes
            manager.OnServiceAdded += OnServiceAdded;
            manager.OnServiceRemoved += OnServiceRemoved;
            manager.Register(server, instanceName, false, paramType);
            start?.Invoke();
        }

        /// <summary>
        /// Create the http server instance
        /// </summary>
        /// <param name="paramType">The paramater type used to create the http server</param>
        /// <param name="instanceName">The name to register this instance as (from parameters)</param>
        /// <param name="start">The function to use for starting the http server (if parameters require auto start)</param>
        /// <param name="certs">The registered certificates</param>
        /// <param name="auth">The auth manager registered (can be null)</param>
        /// <param name="firewallHandler">The firewall handler (can be null)</param>
        /// <returns>An instance of a http server</returns>
        protected abstract S Create(out Type paramType, out String instanceName, out Func<bool> start, Dictionary<String, ICertificateProvider> certs, AuthManager auth, IFirewallHandler firewallHandler);


        protected readonly ServiceManager Manager;
        protected readonly S Server;
        protected readonly P Params;

        public const String ServiceAudit = "Service";

        /// <summary>
        /// Restart the process
        /// </summary>
        /// <returns>True if successful</returns>
        [WebApi("admin/{0}")]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit(ServiceAudit)]
        public bool RestartProcess()
        {
            if (!Manager.RestartProcess())
                return false;
            Server.PushMessageAllSessions(HttpServerBase.MessageServerRestart);
            return true;
        }

        /// <summary>
        /// Read and parse the server config file
        /// </summary>
        /// <param name="config">What config file to get</param>
        /// <returns>Services in the </returns>
        [WebApi("admin/{0}")]
        [WebApiAuth(Roles.Admin)]
        public ConfigEntry[] GetServerConfig(Configs config)
        {
            var fn = GetConfigFilename(config);
            if (fn == null)
                return null;
            return Manager.ReadManifest(fn);
        }

        /// <summary>
        /// The raw server config as json
        /// </summary>
        /// <param name="config">What config file to load</param>
        /// <returns>Services in the </returns>
        [WebApi("admin/ServerConfig.json")]
        [WebApiAuth(Roles.Admin)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public async Task<ReadOnlyMemory<Byte>> ServerConfig(Configs config)
        {
            var fn = GetConfigFilename(config);
            if (fn == null)
                return null;
            return await File.ReadAllBytesAsync(fn).ConfigureAwait(false);
        }

        /// <summary>
        /// Save server config 
        /// </summary>
        /// <param name="jsonText">A new config file as json encoded text</param>
        /// <returns>True if successful</returns>
        [WebApi("admin/{0}")]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit(ServiceAudit)]
        public async Task<bool> SaveServerConfigRaw(String jsonText)
        {
            var fn = Manager.ManifestFileName;
            await File.WriteAllTextAsync(fn, jsonText).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Save server config 
        /// </summary>
        /// <param name="configEntries">Config entries</param>
        /// <returns>True if successful</returns>
        [WebApi("admin/{0}")]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit(ServiceAudit)]
        public Task<bool> SaveServerConfig(ConfigEntry[] configEntries)
        {
            var text = Manager.BuildManifest(configEntries);
            return SaveServerConfigRaw(text);
        }

        String GetConfigFilename(Configs config)
        {
            var fn = Manager.ManifestFileName;
            if (config == Configs.Current)
                return fn;
            var baseDir = Path.GetDirectoryName(fn);
            var baseFilename = Path.GetFileNameWithoutExtension(fn);
            var baseName = Path.Combine(baseDir, baseFilename);
            var baseExt = Path.GetExtension(fn);
            if (config == Configs.LastGood)
            {
                fn = String.Join(".LastGood", baseName, baseExt);
                return File.Exists(fn) ? fn : null;
            }
            var index = (int)config - (int)Configs.Previous1;
            var files = Directory.GetFiles(baseDir, String.Join(".*_*", baseFilename, baseExt), SearchOption.TopDirectoryOnly);
            var fl = files.Length;
            if (fl <= 0)
                return null;
            if (index >= fl)
                return null;
            if (fl > 1)
                Array.Sort(files, (aa, bb) => String.CompareOrdinal(bb.Substring(bb.Length - 24), aa.Substring(aa.Length - 24)));
            return files[index];
        }



        void OnServiceRemoved(object instance, ServiceInfo info)
        {
            var m = Manager;
            var s = Server;
            var module = instance as IHttpServerModule;
            if (module != null)
            {
                if (s.RemoveModule(module))
                {
                    m.AddMessage(MsgPrefix + "Removed module of type \"" + module.GetType() + "\" (" + module + ")", MessageLevels.Debug);
                }
                else
                {
                    m.AddMessage(MsgPrefix + "Failed to remove module! Already removed? Type \"" + module.GetType() + "\" (" + module + ")", MessageLevels.Warning);
                }
            }
            var tv = instance as IHaveTemplateVariables;
            if (tv != null)
            {
                var sv = s.TempVarGroups;
                lock (sv)
                {
                    foreach (var x in tv.TemplateVariableGroups)
                    {
                        if (!sv.TryGetValue(x.Key, out var k))
                            continue;
                        if (k != x.Value)
                            continue;
                        sv.TryRemove(x.Key, out k);
                    }
                }
            }
        }

        void OnServiceAdded(object instance, ServiceInfo info)
        {
            var m = Manager;
            var s = Server;
            var tv = instance as IHaveTemplateVariables;
            if (tv != null)
            {
                var sv = s.TempVarGroups;
                lock ((sv))
                {
                    foreach (var x in tv.TemplateVariableGroups)
                    {
                        if (!sv.TryAdd(x.Key, x.Value))
                            m.AddMessage(MsgPrefix + "Failed to add template variables group \"" + x.Key + "\" since it already exist, ignored", MessageLevels.Warning);
                    }
                }
            }
            var module = instance as IHttpServerModule;
            if (module != null)
            {
                if (s.AddModule(module))
                {
                    m.AddMessage(MsgPrefix + "Added module of type \"" + module.GetType() + "\" (" + module + ")", MessageLevels.Debug);
                }
                else
                {
                    m.AddMessage(MsgPrefix + "Failed to add module! Already added? Type \"" + module.GetType() + "\" (" + module + ")", MessageLevels.Warning);
                }
            }
        }

        protected const String MsgPrefix = "[HttpServerService] ";



        public void Dispose()
        {
            var server = Server;
            Manager.Unregister(server);
            server.Dispose();
        }


    }


    public enum Configs
    {
        /// <summary>
        /// The current config (the one that will be used on next process start).
        /// </summary>
        Current = 0,
        /// <summary>
        /// The last good config, typically what the process is using now.
        /// </summary>
        LastGood,
        /// <summary>
        /// The previous config 1
        /// </summary>
        Previous1,
        /// <summary>
        /// The previous config 2
        /// </summary>
        Previous2,
        /// <summary>
        /// The previous config 3
        /// </summary>
        Previous3,
        /// <summary>
        /// The previous config 4
        /// </summary>
        Previous4,
        /// <summary>
        /// The previous config 5
        /// </summary>
        Previous5,
    }

}
