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
    /// <summary>
    /// The common base of the http server micro services (NetHttpServerService, AspHttpServerService).
    /// Creates the server with the registered certificate providers, auth manager and firewall handler, adds every module, raw module, transformer service
    /// and template variable group of the local services (now and when they are added later, removed again when a service is removed),
    /// registers the extension viewers of the pages in the view folder (md, html, json, xml ...) and has the admin APIs to read and save the manifest and to restart the process.
    /// </summary>
    /// <typeparam name="S">The type of the http server</typeparam>
    /// <typeparam name="P">The type of the parameters of the service</typeparam>
    public abstract class HttpServerServiceBase<S, P> : IDisposable where S : HttpServerBase where P : HttpServerBaseParams, new()
    {

        public override string ToString() => "[Service] " + Server;

        /// <summary>
        /// Create the server (see <see cref="Create"/>), attach the local services, register the server and start it if the parameters say so.
        /// The certificate providers, the auth manager and the firewall handler are only looked up here, so they must be registered before this service.
        /// </summary>
        /// <param name="manager">The service manager</param>
        /// <param name="p">The parameters, null for the defaults</param>
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
            //  The viewers of the pages every http server serves (SysWeaver.HttpServer srcWeb); the first server registers them
            foreach (var (ext, viewer) in Viewers)
                if (manager.TryAddExtensionViewer(ext, viewer))
                    AddedViewers.Add(ext);
            start?.Invoke();
        }

        /// <summary>
        /// The markdown viewer (view/markdown.html, renders the file with the shared markdown converter), {0} is the read url
        /// </summary>
        const String MarkdownViewer = "view/markdown.html?r={0}";

        /// <summary>
        /// The html viewer (view/html.html, the page in a sandboxed frame below the page header), {0} is the read url
        /// </summary>
        const String HtmlViewer = "view/html.html?r={0}";

        /// <summary>
        /// The tree viewer (view/tree.html, json and xml as a tree), {0} is the read url
        /// </summary>
        const String TreeViewer = "view/tree.html?r={0}";

        /// <summary>
        /// The extension viewers of the pages in the view folder (extension, viewer url format)
        /// </summary>
        static readonly (String, String)[] Viewers = [
            ("md", MarkdownViewer), ("markdown", MarkdownViewer),
            ("html", HtmlViewer), ("htm", HtmlViewer), ("xhtml", HtmlViewer),
            ("json", TreeViewer),
            ("xml", TreeViewer), ("xsd", TreeViewer), ("xsl", TreeViewer), ("xslt", TreeViewer),
        ];

        /// <summary>
        /// The extension viewers this instance registered (removed again on dispose)
        /// </summary>
        readonly List<String> AddedViewers = new List<String>();

        /// <summary>
        /// Create the http server instance
        /// </summary>
        /// <param name="paramType">The parameter type used to create the http server</param>
        /// <param name="instanceName">The name to register this instance as (from parameters)</param>
        /// <param name="start">The function to use for starting the http server (if parameters require auto start)</param>
        /// <param name="certs">The registered certificates</param>
        /// <param name="auth">The auth manager registered (can be null)</param>
        /// <param name="firewallHandler">The firewall handler (can be null)</param>
        /// <returns>An instance of a http server</returns>
        protected abstract S Create(out Type paramType, out String instanceName, out Func<bool> start, Dictionary<String, ICertificateProvider> certs, AuthManager auth, IFirewallHandler firewallHandler);


        /// <summary>
        /// The service manager
        /// </summary>
        protected readonly ServiceManager Manager;
        /// <summary>
        /// The http server created by <see cref="Create"/>
        /// </summary>
        protected readonly S Server;
        /// <summary>
        /// The parameters of the service
        /// </summary>
        protected readonly P Params;

        /// <summary>
        /// The audit name of the admin APIs that change the server (restart, save the manifest)
        /// </summary>
        public const String ServiceAudit = "Service";

        /// <summary>
        /// Restart the process
        /// </summary>
        /// <returns>True when the restart was started (never false: a restart that can't be done is a 409 with the reason)</returns>
        /// <exception cref="HttpResponseException">409 with the reason: a restart is already under way, or this host can't restart the process</exception>
        [WebApi("admin/{0}")]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit(ServiceAudit)]
        public bool RestartProcess()
        {
            if (!Manager.RestartProcess())
                throw HttpResponseException.Refused("Can't restart the process: a restart is already under way, or the host application doesn't support restarting");
            Server.PushMessageAllSessions(HttpServerBase.MessageServerRestart);
            return true;
        }

        /// <summary>
        /// Read and parse the server config file
        /// </summary>
        /// <param name="config">What config file to get</param>
        /// <returns>The services in the config file, null if that config file doesn't exist</returns>
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
        /// <returns>The content of the config file, null if that config file doesn't exist</returns>
        [WebApi("admin/ServerConfig.json")]
        [WebApiAuth(Roles.Admin)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public async Task<ReadOnlyMemory<Byte>> ServerConfig(Configs config)
        {
            var fn = GetConfigFilename(config);
            if (fn == null)
                return null;
            return await FileExt.ReadBytesAsync(fn).ConfigureAwait(false);
        }

        /// <summary>
        /// Save the server config (the current manifest file); the text is written as is (not validated) and is used on the next process start
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
        /// Save the server config (the current manifest file) built from the entries; it is used on the next process start
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
            var transformService = instance as IHttpTransformerService;
            if (transformService != null)
            {
                if (s.UnregisterTransformerService(transformService))
                    m.AddMessage(MessageLevels.Debug, $"{MsgPrefix}Unregistered transformer service \"{transformService.GetType()}\" ({transformService})");
                else
                    m.AddMessage(MessageLevels.Warning, $"{MsgPrefix}Failed to unregister transformer service! Already unregistered? Type \"{transformService.GetType()}\" ({transformService})");
            }

            var rawModule = instance as IHttpServerRawModule;
            if (rawModule != null)
            {
                if (s.RemoveRawModule(rawModule))
                {
                    m.AddMessage(MessageLevels.Debug, $"{MsgPrefix}Removed raw module of type \"{rawModule.GetType()}\" ({rawModule})");
                }
                else
                {
                    m.AddMessage(MessageLevels.Warning, $"{MsgPrefix}Failed to remove raw module! Already removed? Type \"{rawModule.GetType()}\" ({rawModule})");
                }
            }
            var module = instance as IHttpServerModule;
            if (module != null)
            {
                if (s.RemoveModule(module))
                {
                    m.AddMessage(MessageLevels.Debug, $"{MsgPrefix}Removed module of type \"{module.GetType()}\" ({module})");
                }
                else
                {
                    m.AddMessage(MessageLevels.Warning, $"{MsgPrefix}Failed to remove module! Already removed? Type \"{module.GetType()}\" ({module})");
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
                            m.AddMessage(MessageLevels.Warning, $"{MsgPrefix}Failed to add template variables group \"{x.Key}\" since it already exist, ignored");
                    }
                }
            }
            var module = instance as IHttpServerModule;
            if (module != null)
            {
                if (s.AddModule(module))
                {
                    m.AddMessage(MessageLevels.Debug, $"{MsgPrefix}Added module of type \"{module.GetType()}\" ({module})");
                }
                else
                {
                    m.AddMessage(MessageLevels.Warning, $"{MsgPrefix}Failed to add module! Already added? Type \"{module.GetType()}\" ({module})");
                }
            }

            var rawModule = instance as IHttpServerRawModule;
            if (rawModule != null)
            {
                if (s.AddRawModule(rawModule))
                {
                    m.AddMessage(MessageLevels.Debug, $"{MsgPrefix}Added raw module of type \"{rawModule.GetType()}\" ({rawModule})");
                }
                else
                {
                    m.AddMessage(MessageLevels.Warning, $"{MsgPrefix}Failed to add raw module! Already added? Type \"{rawModule.GetType()}\" ({rawModule})");
                }
            }
            var transformService = instance as IHttpTransformerService;
            if (transformService != null) {
                s.RegisterTransformerService(transformService);
                m.AddMessage(MessageLevels.Debug, $"{MsgPrefix}Registered transformer service \"{transformService.GetType()}\" ({transformService})");
            }
        }

        protected const String MsgPrefix = "[HttpServerService] ";



        /// <summary>
        /// Remove the extension viewers this instance registered, then unregister and dispose the server
        /// </summary>
        public void Dispose()
        {
            var server = Server;
            foreach (var x in AddedViewers)
                Manager.TryRemoveExtensionViewer(x, out var _);
            AddedViewers.Clear();
            Manager.Unregister(server);
            server.Dispose();
        }


    }


    /// <summary>
    /// The server config (manifest) files that the admin APIs of <see cref="HttpServerServiceBase{S, P}"/> can read
    /// </summary>
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
