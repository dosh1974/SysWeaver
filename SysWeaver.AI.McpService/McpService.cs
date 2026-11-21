using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// Serves MCP (Model Context Protocol) requests using the Streamable HTTP transport.
    /// A single end point (McpParams.Url) accepts JSON-RPC messages using POST, responses are returned as json (application/json),
    /// except "tools/call" requests from clients that accepts server sent events (see McpParams.StreamToolCalls), they get an event stream with keep alives while the tool runs.
    /// The service is stateless (no Mcp-Session-Id) and doesn't offer a server initiated event stream (GET returns 405).
    /// Clients authenticates using an API key: "Authorization: Bearer &lt;key&gt;" or "x-api-key: &lt;key&gt;".
    /// </summary>
    public sealed class McpService : IHttpServerModule, IPerfMonitored, IDisposable
    {
        const String LogPrefix = "[Mcp] ";

        /// <summary>
        /// Create an MCP service
        /// </summary>
        /// <param name="sm">Optional service manager, if supplied (and McpParams.ServiceTools is true) the AI tools of all services are exposed</param>
        /// <param name="p">The parameters</param>
        /// <param name="msg">Optional message host</param>
        public McpService(ServiceManager sm = null, McpParams p = null, IMessageHost msg = null)
        {
            p = p ?? new McpParams();
            Msg = msg;
            var url = (p.Url ?? "").Trim().Trim('/');
            if (url.Length <= 0)
                throw new ArgumentException("The MCP url may not be empty", nameof(p));
            Url = url;
            OnlyForPrefixes = [url];
            MaxRequestSize = Math.Max(1024, p.MaxRequestSize);
            FileLifetime = TimeSpan.FromMinutes(Math.Max(1, p.FileLifetimeMinutes));
            FilesPrefix = url + "/files/";
            FileHandler = new McpRequestHandler("File", null, GetFile);
            DataPrefix = url + "/data/";
            Data = new FastMemCache<String, StoredFile>(TimeSpan.FromMinutes(Math.Max(1, p.DataLifetimeMinutes)), StringComparer.Ordinal);
            DataHandler = new McpRequestHandler("Data", null, GetData);
            StreamToolCalls = p.StreamToolCalls;
            KeepAliveInterval = TimeSpan.FromSeconds(Math.Max(1, p.KeepAliveSeconds));
            ServerName = String.IsNullOrWhiteSpace(p.ServerName) ? EnvInfo.AppName : p.ServerName.Trim();
            ServerVersion = Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "1.0.0";
            Instructions = String.IsNullOrWhiteSpace(p.Instructions) ? null : p.Instructions;
            var origins = (p.AllowedOrigins ?? []).Where(x => !String.IsNullOrWhiteSpace(x)).Select(x => x.Trim().TrimEnd('/')).ToArray();
            AnyOrigin = origins.Contains("*");
            AllowedOrigins = new HashSet<String>(origins, StringComparer.OrdinalIgnoreCase);
            var auth = Authorization.GetRequiredTokens(p.Auth);
            RequiresAuth = auth != null;
            DefaultToolAuth = p.Auth;
            UnauthorizedHandler = new McpRequestHandler("Unauthorized", null, Unauthorized);
            PostHandler = new McpRequestHandler(nameof(Post), auth, Post);
            MethodNotAllowedHandler = new McpRequestHandler("MethodNotAllowed", auth, MethodNotAllowed);
            AddTools(this);
            ServiceTools = p.ServiceTools;
            WebApis = GetWebApis(p.WebApis, msg);
            if (sm != null)
            {
                Manager = sm;
                //  Files attached by tools are stored in the user storage (if available), same as in the AI chat
                UserStorage = sm.TryGet<IUserStorageService>();
                if (ServiceTools)
                {
                    foreach (var x in sm.UniqueInstances)
                        //  Any service may have tools (IHaveOpenAiTools is only required by the AI chat)
                        if (!ReferenceEquals(x, this))
                            AddTools(x);
                }
                sm.OnServiceAdded += Sm_OnServiceAdded;
                sm.OnServiceRemoved += Sm_OnServiceRemoved;
                foreach (var api in WebApis)
                {
                    Object instance;
                    try
                    {
                        instance = sm.Get(api.Type, ServiceInstanceTypes.Any, api.Name);
                    }
                    catch
                    {
                        //  Not registered yet, added when the service is added (Sm_OnServiceAdded)
                        Msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}No service {api} is registered yet, its API's are added when it is");
                        continue;
                    }
                    AddWebApiTools(instance, api.ProxyData);
                }
            }
            else
            {
                if (WebApis.Length > 0)
                    Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}{nameof(McpParams.WebApis)} requires a service manager, ignoring {WebApis.Length} API(s)");
            }
            Msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Serving MCP at {url.ToQuoted()} with {Tools.Count} tool(s)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            var sm = Manager;
            if (sm == null)
                return;
            sm.OnServiceRemoved -= Sm_OnServiceRemoved;
            sm.OnServiceAdded -= Sm_OnServiceAdded;
        }

        void Sm_OnServiceAdded(Object instance, ServiceInfo info)
        {
            if (instance is IUserStorageService us)
                Interlocked.CompareExchange(ref UserStorage, us, null);
            if (ReferenceEquals(instance, this))
                return;
            if (ServiceTools)
                AddTools(instance);
            var specs = WebApis.Where(x => x.IsMatch(instance, info)).ToArray();
            if (specs.Length > 0)
                AddWebApiTools(instance, specs.Any(x => x.ProxyData));
        }

        void Sm_OnServiceRemoved(Object instance, ServiceInfo info)
        {
            if (instance is IUserStorageService us)
                Interlocked.CompareExchange(ref UserStorage, Manager.TryGet<IUserStorageService>(), us);
            if (ReferenceEquals(instance, this))
                return;
            if (ServiceTools)
                RemoveTools(instance);
            RemoveWebApiTools(instance);
        }

        readonly bool ServiceTools;

        /// <summary>
        /// Optional user storage, used to store files attached by tools (same as in the AI chat)
        /// </summary>
        IUserStorageService UserStorage;

        readonly IMessageHost Msg;
        readonly ServiceManager Manager;
        readonly String Url;
        readonly int MaxRequestSize;
        readonly String ServerName;
        readonly String ServerVersion;
        readonly String Instructions;
        readonly bool AnyOrigin;
        readonly HashSet<String> AllowedOrigins;

        /// <summary>
        /// The performance monitor used to track the requests of this service
        /// </summary>
        public PerfMonitor PerfMon { get; } = new PerfMonitor("Mcp");

        #region Tools

        /// <summary>
        /// A registered tool, with information about where it comes from and usage statistics
        /// </summary>
        sealed class McpTool
        {
            public McpTool(AiTool tool, Object instance, MethodInfo method, bool isWebApi, FileOutput file)
            {
                Tool = tool;
                Service = (instance?.GetType() ?? method.DeclaringType)?.FullName;
                Method = method;
                IsWebApi = isWebApi;
                File = file;
                Added = DateTime.UtcNow;
            }

            public readonly AiTool Tool;

            /// <summary>
            /// Non null if the tool returns a file (a WebApiRawAttribute method with binary output), the file is attached to the tool result
            /// </summary>
            public readonly FileOutput File;
            public readonly String Service;
            public readonly MethodInfo Method;
            public readonly bool IsWebApi;
            public readonly DateTime Added;

            public long Calls;
            public long Errors;
            public long LastCallTicks;

            public void OnCall(bool isError)
            {
                Interlocked.Increment(ref Calls);
                if (isError)
                    Interlocked.Increment(ref Errors);
                Interlocked.Exchange(ref LastCallTicks, DateTime.UtcNow.Ticks);
            }
        }

        /// <summary>
        /// How to invoke a tool that returns a file (raw binary data)
        /// </summary>
        sealed class FileOutput
        {
            public FileOutput(IApiHttpServerEndPoint endPoint, String mime, bool proxy)
            {
                EndPoint = endPoint;
                Mime = mime;
                Proxy = proxy;
            }

            /// <summary>
            /// If true, the data is kept in memory and the url is returned (McpWebApiInstance.ProxyData), else the data is attached to the tool result
            /// </summary>
            public readonly bool Proxy;

            /// <summary>
            /// The end point to invoke (the AiTool can only return text)
            /// </summary>
            public readonly IApiHttpServerEndPoint EndPoint;

            /// <summary>
            /// The mime type of the WebApiRawAttribute, null if the method returns a MemoryFile (the mime type is set per call)
            /// </summary>
            public readonly String Mime;
        }

        /// <summary>
        /// The registered tools, key is the tool name
        /// </summary>
        readonly ConcurrentDictionary<String, McpTool> Tools = new(StringComparer.Ordinal);

        /// <summary>
        /// The auth of tools that doesn't specify any auth (McpParams.Auth)
        /// </summary>
        readonly String DefaultToolAuth;

        /// <summary>
        /// The names of all registered tools (a shared snapshot, rebuilt when a tool is added or removed, don't modify it)
        /// </summary>
        public IReadOnlyCollection<String> ToolNames
        {
            get
            {
                var v = Volatile.Read(ref ToolsVersion);
                var c = ToolNamesCache;
                if ((c != null) && (c.Version == v))
                    return c.Names;
                c = new ToolNamesSnapshot(v, Tools.Keys.ToArray());
                ToolNamesCache = c;
                return c.Names;
            }
        }

        /// <summary>
        /// Incremented after every change of Tools (invalidates ToolNamesCache)
        /// </summary>
        int ToolsVersion;

        sealed class ToolNamesSnapshot
        {
            public ToolNamesSnapshot(int version, String[] names)
            {
                Version = version;
                Names = names;
            }
            public readonly int Version;
            public readonly String[] Names;
        }

        volatile ToolNamesSnapshot ToolNamesCache;

        /// <summary>
        /// Register a method as a tool, the tool is described in the same way as AI chat tools (XML documentation, AiToolNameAttribute, AiToolPrefixAttribute, WebApiAuthAttribute etc).
        /// Methods without any auth (WebApiAuthAttribute or IRunTimeWebApiAuth) requires McpParams.Auth.
        /// </summary>
        /// <param name="instance">Object instance (null for static methods)</param>
        /// <param name="method">The method</param>
        /// <param name="fn">Optional tool name, default is computed from the AiToolPrefixAttribute and AiToolNameAttribute (type name + method name)</param>
        /// <returns>True if the tool was added, false if a tool with the same name already exists</returns>
        public bool AddTool(Object instance, MethodInfo method, String fn = null)
            => TryAddTool(instance, method, fn, false, false, false) != null;

        /// <summary>
        /// Register a method as a tool
        /// </summary>
        /// <param name="instance">Object instance (null for static methods)</param>
        /// <param name="method">The method</param>
        /// <param name="fn">Optional tool name, default is computed from the AiToolPrefixAttribute and AiToolNameAttribute (type name + method name)</param>
        /// <param name="isWebApi">True if the method is added as a WebApi (McpParams.WebApis), false if it's an AI tool</param>
        /// <param name="isFile">True if the method returns raw data (WebApiRawAttribute), the data is attached to the tool result as a file (or proxied, see <paramref name="proxyData"/>)</param>
        /// <param name="proxyData">True if the data of a file method is kept in memory and the url is returned instead of attaching the data (McpWebApiInstance.ProxyData)</param>
        /// <returns>The tool if it was added, null if it couldn't be created or if a tool with the same name already exists</returns>
        AiTool TryAddTool(Object instance, MethodInfo method, String fn, bool isWebApi, bool isFile, bool proxyData)
        {
            AiTool tool;
            FileOutput file = null;
            try
            {
                if (isFile)
                {
                    fn = String.IsNullOrEmpty(fn) ? AiTool.GetToolName(method) : fn;
                    var ep = ApiHttpEntry.Create(AiServiceBase.IoParams, instance, method, fn, PerfMon, DefaultToolAuth);
                    tool = AiTool.Create(fn, ep, GetFileFunction(fn, ep, proxyData));
                    file = new FileOutput(ep, method.GetCustomAttribute<WebApiRawAttribute>(true)?.Mime, proxyData);
                }
                else
                {
                    tool = AiTool.FromMethod(instance, method, fn, PerfMon, DefaultToolAuth);
                }
            }
            catch (Exception ex)
            {
                //  A bad method shouldn't prevent other tools from being registered
                Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Failed to add the method {method.Name.ToQuoted()} of {method.DeclaringType?.Name.ToQuoted()} as a tool", ex);
                return null;
            }
            if (!Tools.TryAdd(tool.Name, new McpTool(tool, instance, method, isWebApi, file)))
            {
                Msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}A tool named {tool.Name.ToQuoted()} already exists, ignoring the method {method.Name.ToQuoted()} of {(method.DeclaringType?.Name.ToQuoted())}");
                return null;
            }
            Interlocked.Increment(ref ToolsVersion);
            Msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Added tool {tool.Name.ToQuoted()}");
            return tool;
        }

        /// <summary>
        /// Register all tools (methods with an AiToolAttribute or an OpenAiUseAttribute) of an instance, methods with an AiHideMcpAttribute are ignored
        /// </summary>
        /// <param name="instance">The instance</param>
        /// <returns>The number of tools added</returns>
        public int AddTools(Object instance)
        {
            if (instance == null)
                return 0;
            int count = 0;
            foreach (var m in AiTool.GetToolMethods(instance.GetType()))
            {
                //  Tools that only makes sense in an AI chat
                if (IsHidden(m))
                    continue;
                if (AddTool(instance, m))
                    ++count;
            }
            return count;
        }

        /// <summary>
        /// Check if a tool method should be hidden from MCP clients (have an AiHideMcpAttribute)
        /// </summary>
        /// <param name="method">The method</param>
        /// <returns>True if the method shouldn't be exposed as an MCP tool</returns>
        public static bool IsHidden(MethodInfo method)
            => method.GetCustomAttribute<AiHideMcpAttribute>(true)?.Hide ?? false;

        /// <summary>
        /// Unregister a tool
        /// </summary>
        /// <param name="name">The name of the tool</param>
        /// <returns>True if the tool was removed</returns>
        public bool RemoveTool(String name)
        {
            if (!Tools.TryRemove(name, out var _))
                return false;
            Interlocked.Increment(ref ToolsVersion);
            return true;
        }

        /// <summary>
        /// Unregister all tools of an instance
        /// </summary>
        /// <param name="instance">The instance</param>
        /// <returns>The number of tools removed</returns>
        public int RemoveTools(Object instance)
        {
            if (instance == null)
                return 0;
            int count = 0;
            foreach (var m in AiTool.GetToolMethods(instance.GetType()))
                if (RemoveTool(AiTool.GetToolName(m)))
                    ++count;
            return count;
        }

        /// <summary>
        /// Check if the user of a request is allowed to use a tool
        /// </summary>
        static bool CanUse(HttpServerRequest r, AiTool tool)
            => r.Session?.IsValid(tool.Auth) ?? (tool.Auth == null);

        #endregion//Tools

        #region Web API's

        /// <summary>
        /// A service whose [WebApi] methods are exposed as tools (McpParams.WebApis)
        /// </summary>
        sealed class WebApiSpec
        {
            public WebApiSpec(Type type, String name, bool proxyData)
            {
                Type = type;
                Name = String.IsNullOrWhiteSpace(name) ? null : name.Trim();
                ProxyData = proxyData;
            }

            public readonly Type Type;
            public readonly String Name;

            /// <summary>
            /// McpWebApiInstance.ProxyData
            /// </summary>
            public readonly bool ProxyData;

            /// <summary>
            /// Check if a service instance is the one described by this spec (same rules as ServiceManager.Get)
            /// </summary>
            public bool IsMatch(Object instance, ServiceInfo info)
                => Type.IsInstanceOfType(instance) && ((Name == null) || String.Equals(info?.Name, Name, StringComparison.OrdinalIgnoreCase));

            public override String ToString() => Name == null ? Type.FullName.ToQuoted() : String.Concat(Type.FullName.ToQuoted(), " named ", Name.ToQuoted());
        }

        readonly WebApiSpec[] WebApis;

        /// <summary>
        /// The names of the tools added for each WebApi instance (so that only those tools are removed when the instance is removed).
        /// Lock on this when used.
        /// </summary>
        readonly Dictionary<Object, String[]> WebApiTools = new(ReferenceEqualityComparer.Instance);

        static WebApiSpec[] GetWebApis(McpWebApiInstance[] apis, IMessageHost msg)
        {
            List<WebApiSpec> specs = new();
            foreach (var api in apis.Nullable())
            {
                var typeName = api?.InstanceType?.Trim();
                if (String.IsNullOrEmpty(typeName))
                    continue;
                var t = TypeFinder.Get(typeName);
                if (t == null)
                {
                    msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Failed to find the type {typeName.ToQuoted()}, ignoring it");
                    continue;
                }
                specs.Add(new WebApiSpec(t, api.InstanceName, api.ProxyData));
            }
            return specs.ToArray();
        }

        /// <summary>
        /// Register all [WebApi] methods of an instance as tools, each tool keeps the auth of the method (WebApiAuthAttribute or IRunTimeWebApiAuth, else McpParams.Auth).
        /// Methods with an AiHideMcpAttribute, disabled methods (WebApiOptionalAttribute) and methods returning pre-compressed data (WebApiRawAttribute) are ignored.
        /// </summary>
        /// <param name="instance">The instance</param>
        /// <param name="proxyData">True if the data returned by all WebApiRaw methods is kept in memory and the url is returned (McpWebApiInstance.ProxyData)</param>
        /// <returns>The number of tools added</returns>
        int AddWebApiTools(Object instance, bool proxyData)
        {
            lock (WebApiTools)
            {
                if (WebApiTools.ContainsKey(instance))
                    return 0;
                List<String> added = new();
                foreach (var m in instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy))
                {
                    if (m.GetCustomAttributeWithInterface<WebApiAttribute>(true) == null)
                        continue;
                    if (IsHidden(m))
                        continue;
                    try
                    {
                        if (!ApiHttpServerModule.IsEnabled(instance, m))
                            continue;
                    }
                    catch (Exception ex)
                    {
                        Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Failed to check if the method {m.Name.ToQuoted()} of {m.DeclaringType?.Name.ToQuoted()} is enabled, ignoring it", ex);
                        continue;
                    }
                    //  Raw binary data (ex: an image) is attached to the tool result as a file, raw text is returned as text.
                    //  With proxyData, all raw data is kept in memory and the url is returned
                    var raw = m.GetCustomAttribute<WebApiRawAttribute>(true);
                    if (raw?.PreCompressedWith != null)
                    {
                        Msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}The method {m.Name.ToQuoted()} of {m.DeclaringType?.Name.ToQuoted()} returns pre-compressed data, ignoring it");
                        continue;
                    }
                    var isFile = (raw != null) && (proxyData || (raw.Mime == null) || !IsText(BaseMime(raw.Mime)));
                    var tool = TryAddTool(instance, m, null, true, isFile, isFile && proxyData);
                    if (tool != null)
                        added.Add(tool.Name);
                }
                WebApiTools[instance] = added.ToArray();
                Msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Added {added.Count} tool(s) from the API's of {instance.GetType().Name.ToQuoted()}");
                return added.Count;
            }
        }

        /// <summary>
        /// The function definition of a tool that returns a file, same as AiTool.FromEndPoint but the return value is described as a file (not as the raw memory)
        /// </summary>
        static AiToolFunction GetFileFunction(String fn, IApiHttpServerEndPoint ep, bool proxyData)
        {
            ep.GetDesc(out var argType, out var _, out var methodDesc, out var argDesc, out var retDesc, out var argName);
            var p = argType == null ? null : JsonSchemaCache.GetBinaryDataParam(argType, argName, false, argDesc);
            var ret = proxyData
                ? "## Tool returns  \nThe url of the created file (ex: an image), the file is NOT attached to the result, download it from the url (no auth required, the url is temporary)"
                : "## Tool returns  \nA file (ex: an image) that is attached to the result, the text of the result is the url of the file";
            if (!String.IsNullOrEmpty(retDesc))
                ret = String.Concat(ret, ": ", retDesc);
            methodDesc = String.IsNullOrEmpty(methodDesc) ? ret : String.Join(".\n", methodDesc.Trim(), ret);
            return new AiToolFunction(fn, methodDesc.Trim().LimitLength(1024, ""), p, false);
        }

        /// <summary>
        /// Invoke a tool that returns a file, the file is attached to the result (stored like files added by tools).
        /// The end point is invoked using a detached copy of the request (same user), so that the mime type and headers of the file doesn't end up in the MCP response.
        /// </summary>
        /// <returns>The url of the file</returns>
        async Task<String> InvokeFile(HttpServerRequest r, McpTool entry, JsonElement args, McpToolContext context)
        {
            var tool = entry.Tool;
            ReadOnlyMemory<Byte> arg = default;
            if ((tool.Arg != null) && (args.ValueKind == JsonValueKind.Object))
            {
                //  Same as the AI chat: a json object with a single property (the method argument)
                if (!((tool.ArgName != null) && args.TryGetProperty(tool.ArgName, out var v)))
                {
                    v = default;
                    foreach (var x in args.EnumerateObject())
                    {
                        v = x.Value;
                        break;
                    }
                }
                if (v.ValueKind != JsonValueKind.Undefined)
                    arg = Encoding.UTF8.GetBytes(v.GetRawText());
            }
            var file = entry.File;
            using var fr = ManualHttpServerRequest.Detacher(r)();
            var data = await file.EndPoint.InvokeAsync(fr, arg).ConfigureAwait(false);
            var mime = file.Mime ?? fr.GetResMime();
            if (String.IsNullOrEmpty(mime))
                mime = "application/octet-stream";
            var name = GetDispositionFilename(fr.GetResHeader("Content-Disposition")) ?? tool.Name;
            if (!file.Proxy)
                return context.AddMessageFile(mime, data, name);
            //  Only the url is returned (as text and as a resource link), the client downloads the data itself
            var url = StoreData(r, mime, data, name);
            context.AddLink(url);
            return url;
        }

        /// <summary>
        /// Get the filename of a Content-Disposition header, ex: 'inline; filename="image.png"' => "image.png"
        /// </summary>
        static String GetDispositionFilename(String disposition)
        {
            if (String.IsNullOrEmpty(disposition))
                return null;
            const String key = "filename=\"";
            var i = disposition.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
                return null;
            i += key.Length;
            var e = disposition.IndexOf('"', i);
            return e <= i ? null : disposition.Substring(i, e - i);
        }

        /// <summary>
        /// Unregister all tools that was added for the [WebApi] methods of an instance
        /// </summary>
        /// <param name="instance">The instance</param>
        /// <returns>The number of tools removed</returns>
        int RemoveWebApiTools(Object instance)
        {
            String[] names;
            lock (WebApiTools)
            {
                if (!WebApiTools.Remove(instance, out names))
                    return 0;
            }
            int count = 0;
            foreach (var name in names)
                if (RemoveTool(name))
                    ++count;
            return count;
        }

        #endregion//Web API's

        #region Administration

        /// <summary>
        /// A tool served by the MCP end point, a row of <see cref="ToolTable(TableDataRequest)"/>
        /// </summary>
        [TableDataDefaultOrder(nameof(Name))]
        [TableDataPrimaryKey(nameof(Name))]
        public sealed class ToolRow
        {
            /// <summary>
            /// The name of the tool, as seen by MCP clients
            /// </summary>
            public String Name;

            /// <summary>
            /// "Tool" for an AI tool of a service (AiToolAttribute or AiUseAttribute), "WebApi" for a WebApi method of a service in McpParams.WebApis, "WebApi (file)" for a WebApi method that returns a file (attached to the result),
            /// "WebApi (url)" for a WebApi method whose data is kept in memory and returned as an url (McpWebApiInstance.ProxyData)
            /// </summary>
            public String Kind;

            /// <summary>
            /// The auth required to use the tool (users that don't fulfill it can't see or call the tool), every request also requires the auth of the MCP end point
            /// </summary>
            [TableDataAuth]
            public String Auth;

            /// <summary>
            /// The description of the tool, as sent to MCP clients
            /// </summary>
            [TableDataText]
            public String Description;

            /// <summary>
            /// The type of the parameter (null if the tool takes no parameter)
            /// </summary>
            public String Parameter;

            /// <summary>
            /// The JSON schema of the tool input, as sent to MCP clients
            /// </summary>
            [TableDataJson]
            public String InputSchema;

            /// <summary>
            /// The type of the return value (null if none)
            /// </summary>
            public String Returns;

            /// <summary>
            /// The type of the service that implements the tool
            /// </summary>
            public String Service;

            /// <summary>
            /// The method that is invoked (declaring type and method name)
            /// </summary>
            public String Method;

            /// <summary>
            /// The number of calls since the tool was added
            /// </summary>
            public long Calls;

            /// <summary>
            /// The number of calls that failed (the error is returned to the MCP client)
            /// </summary>
            public long Errors;

            /// <summary>
            /// When the tool was last called (UTC), null if never
            /// </summary>
            public DateTime? LastCall;

            /// <summary>
            /// When the tool was added (UTC)
            /// </summary>
            [TableDataHide]
            public DateTime Added;

            /// <summary>
            /// The MCP end point that serves the tool
            /// </summary>
            [TableDataHide]
            public String EndPoint;
        }

        /// <summary>
        /// All tools served by the MCP end point, with kind, auth, description, input schema, implementing service and usage
        /// </summary>
        /// <param name="r">Table request parameters (paging, sorting, filtering)</param>
        /// <returns>A table with one row per tool</returns>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiClientCache(4)]
        [WebMenuTable(null, "Admin/{0}", "MCP tools", "All tools served by the MCP end point, with auth, parameters and usage", "IconTable")]
        public TypedTableData<ToolRow> ToolTable(TableDataRequest r)
        {
            var endPoint = "/" + Url;
            var rows = Tools.Values.Select(x =>
            {
                var t = x.Tool;
                var last = Interlocked.Read(ref x.LastCallTicks);
                return new ToolRow
                {
                    Name = t.Name,
                    Kind = x.IsWebApi ? (x.File != null ? (x.File.Proxy ? "WebApi (url)" : "WebApi (file)") : "WebApi") : "Tool",
                    Auth = AuthToString(t.Auth),
                    Description = t.Tool.FunctionDescription,
                    Parameter = t.Arg?.Name,
                    InputSchema = t.Tool.FunctionParameters?.ToString(),
                    Returns = t.Ret?.Name,
                    Service = x.Service,
                    Method = String.Concat(x.Method.DeclaringType?.Name, ".", x.Method.Name),
                    Calls = Interlocked.Read(ref x.Calls),
                    Errors = Interlocked.Read(ref x.Errors),
                    LastCall = last == 0 ? null : new DateTime(last, DateTimeKind.Utc),
                    Added = x.Added,
                    EndPoint = endPoint,
                };
            }).ToList();
            return TableDataTools.GetTyped(r, 10000, rows, "MCP tools at " + endPoint.ToQuoted());
        }

        /// <summary>
        /// Convert required tokens (as returned by Authorization.GetRequiredTokens) back to a requirement string: null = no auth, "" = any logged in user, "-" = no one, else comma separated tokens
        /// </summary>
        static String AuthToString(IReadOnlyList<String> auth)
        {
            if (auth == null)
                return null;
            if (ReferenceEquals(auth, AuthTools.NoAuth))
                return "-";
            return String.Join(',', auth);
        }

        #endregion//Administration

        #region Tool files

        /// <summary>
        /// A file attached by a tool
        /// </summary>
        sealed class StoredFile
        {
            public StoredFile(String mime, ReadOnlyMemory<Byte> data, DateTime expires)
            {
                Mime = mime;
                Data = data;
                Expires = expires;
            }
            public readonly String Mime;
            public readonly ReadOnlyMemory<Byte> Data;
            public readonly DateTime Expires;
        }

        readonly ConcurrentDictionary<String, StoredFile> Files = new(StringComparer.Ordinal);
        readonly TimeSpan FileLifetime;
        readonly String FilesPrefix;
        readonly IHttpRequestHandler FileHandler;

        static readonly IReadOnlyDictionary<String, String> MimeExtensions = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            { "image/svg+xml", ".svg" },
            { "image/png", ".png" },
            { "image/jpeg", ".jpg" },
            { "image/gif", ".gif" },
            { "image/webp", ".webp" },
            { "application/json", ".json" },
            { "text/plain", ".txt" },
            { "text/html", ".html" },
            { "text/csv", ".csv" },
            { "video/mp4", ".mp4" },
            { "video/webm", ".webm" },
            { "audio/flac", ".flac" },
            { "audio/x-flac", ".flac" },
            { "audio/mpeg", ".mp3" },
            { "audio/wav", ".wav" },
            { "audio/x-wav", ".wav" },
            { "audio/ogg", ".ogg" },
            { "audio/opus", ".opus" },
        };

        /// <summary>
        /// Store a file attached by a tool, the file is available (without auth) for McpParams.FileLifetimeMinutes
        /// </summary>
        /// <returns>The absolute url of the file</returns>
        String StoreFile(HttpServerRequest r, String mime, ReadOnlyMemory<Byte> data, String filename)
        {
            mime = String.IsNullOrEmpty(mime) ? "application/octet-stream" : mime;
            var name = GetFilename(mime, filename);
            //  Store in the user storage if available (and a user is logged in), same as in the AI chat
            var us = Volatile.Read(ref UserStorage);
            if ((us != null) && (r.Session?.Auth != null))
            {
                try
                {
                    //  The tool context is synchronous (same as in the AI chat)
                    var url = us.StorePrivateFile(r, name, data).GetAwaiter().GetResult();
                    if (!String.IsNullOrEmpty(url))
                        return Uri.TryCreate(url, UriKind.Absolute, out var _) ? url : String.Concat(r.Prefix, url.TrimStart('/'));
                }
                catch (Exception ex)
                {
                    Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Failed to store {name.ToQuoted()} in the user storage, the file is only temporary available", ex);
                }
            }
            var now = DateTime.UtcNow;
            foreach (var x in Files)
                if (x.Value.Expires <= now)
                    Files.TryRemove(x.Key, out var _);
            var id = Guid.NewGuid().ToString("N");
            Files[id] = new StoredFile(mime, data, now + FileLifetime);
            return String.Concat(r.Prefix, FilesPrefix, id, "/", name);
        }

        /// <summary>
        /// Get a safe filename (with an extension matching the mime type)
        /// </summary>
        static String GetFilename(String mime, String filename)
        {
            var name = new String((filename ?? "").Select(c => Char.IsLetterOrDigit(c) || (c == '-') || (c == '_') || (c == '.') ? c : '_').ToArray()).Trim('_', '.');
            if (name.Length <= 0)
                name = "file";
            if (MimeExtensions.TryGetValue(BaseMime(mime), out var ext) && !name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                name += ext;
            return name;
        }

        /// <summary>
        /// Get a stored file from the request url: "{Url}/files/{id}/{name}"
        /// </summary>
        bool TryGetFile(HttpServerRequest r, out StoredFile file)
        {
            file = null;
            var url = r.LocalUrl;
            if (!url.StartsWith(FilesPrefix, StringComparison.Ordinal))
                return false;
            var start = FilesPrefix.Length;
            var end = url.IndexOf('/', start);
            var id = end < 0 ? url.Substring(start) : url.Substring(start, end - start);
            if (!Files.TryGetValue(id, out file))
                return false;
            return file.Expires > DateTime.UtcNow;
        }

        Task<HttpRequestData> GetFile(HttpServerRequest r)
        {
            if (!TryGetFile(r, out var f))
            {
                r.SetResStatusCode(404);
                return Task.FromResult(new HttpRequestData(ReadOnlyMemory<Byte>.Empty, true));
            }
            r.SetResMime(f.Mime);
            return Task.FromResult(new HttpRequestData(f.Data, true));
        }

        #endregion//Tool files

        #region Proxied data

        /// <summary>
        /// Data returned by WebApiRaw methods of instances with McpWebApiInstance.ProxyData, key is the id (removed after McpParams.DataLifetimeMinutes)
        /// </summary>
        readonly FastMemCache<String, StoredFile> Data;
        readonly String DataPrefix;
        readonly IHttpRequestHandler DataHandler;

        /// <summary>
        /// Keep data in memory, it's available (without auth) at "{Url}/data/{id}/{name}" for McpParams.DataLifetimeMinutes
        /// </summary>
        /// <returns>The absolute url of the data</returns>
        String StoreData(HttpServerRequest r, String mime, ReadOnlyMemory<Byte> data, String filename)
        {
            mime = String.IsNullOrEmpty(mime) ? "application/octet-stream" : mime;
            var name = GetFilename(mime, filename);
            var id = Guid.NewGuid().ToString("N");
            //  The cache handles the expiration, the expiration time of the item is unused
            Data.Set(id, new StoredFile(mime, data, DateTime.MaxValue));
            return String.Concat(r.Prefix, DataPrefix, id, "/", name);
        }

        /// <summary>
        /// Get proxied data from the request url: "{Url}/data/{id}/{name}" (or "{Url}/data/{id}")
        /// </summary>
        bool TryGetData(HttpServerRequest r, out StoredFile file)
        {
            file = null;
            var url = r.LocalUrl;
            if (!url.StartsWith(DataPrefix, StringComparison.Ordinal))
                return false;
            var start = DataPrefix.Length;
            var end = url.IndexOf('/', start);
            var id = end < 0 ? url.Substring(start) : url.Substring(start, end - start);
            return (id.Length > 0) && Data.TryGet(id, out file);
        }

        Task<HttpRequestData> GetData(HttpServerRequest r)
        {
            if (!TryGetData(r, out var f))
            {
                r.SetResStatusCode(404);
                return Task.FromResult(new HttpRequestData(ReadOnlyMemory<Byte>.Empty, true));
            }
            r.SetResMime(f.Mime);
            return Task.FromResult(new HttpRequestData(f.Data, true));
        }

        #endregion//Proxied data

        #region Http module

        /// <inheritdoc />
        public String[] OnlyForPrefixes { get; }

        readonly bool RequiresAuth;
        readonly IHttpRequestHandler PostHandler;
        readonly IHttpRequestHandler MethodNotAllowedHandler;
        readonly IHttpRequestHandler UnauthorizedHandler;

        const String IndexSuffix = "/index.html";

        /// <inheritdoc />
        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var url = context.LocalUrl.AsSpan();
            //  The server adds "index.html" to urls ending with a '/'
            if (context.DidIndex && url.EndsWith(IndexSuffix, StringComparison.Ordinal))
                url = url.Slice(0, url.Length - IndexSuffix.Length);
            //  Files attached by tools are public (the url is unguessable), so that they can be displayed by any client
            if (url.StartsWith(FilesPrefix, StringComparison.Ordinal))
                return TryGetFile(context, out var _) ? FileHandler : null;
            //  Same for proxied data
            if (url.StartsWith(DataPrefix, StringComparison.Ordinal))
                return TryGetData(context, out var _) ? DataHandler : null;
            if (!url.TrimEnd('/').Equals(Url, StringComparison.Ordinal))
                return null;
            //  Without credentials the server would redirect to the login page, MCP clients expects a 401
            if (RequiresAuth && (context.Session?.Auth == null) && !HaveCredentials(context))
                return UnauthorizedHandler;
            return context.HttpMethod == HttpServerMethods.POST ? PostHandler : MethodNotAllowedHandler;
        }

        /// <summary>
        /// Check if the request have any credentials that the server can use to authenticate the user
        /// </summary>
        static bool HaveCredentials(HttpServerRequest r)
            => (r.GetReqHeader("Authorization") ?? r.GetReqHeader("x-api-key") ?? r.GetReqHeader("x-goog-api-key")) != null;

        /// <summary>
        /// The server only authenticates the credentials of a request if the session have no user, so a session cookie (ex: from a browser or an earlier request using another key) would make the request use the user of the session.
        /// Check that the credentials (if any) belongs to the same user as the session.
        /// </summary>
        static async ValueTask<bool> CredentialsMatchSession(HttpServerRequest r)
        {
            var user = r.Session?.Auth;
            if (user == null)
                return true;
            var server = r.Server;
            if (!server.AllowAuthorizationAuth)
                return true;
            var h = r.GetReqHeader("Authorization");
            if (h == null)
            {
                //  Same as the server
                h = r.GetReqHeader("x-api-key") ?? r.GetReqHeader("x-goog-api-key");
                if (h == null)
                    return true;
                h = "*key " + h;
            }
            var am = server.Auth;
            if (am == null)
                return false;
            //  The result is cached by the auth manager
            var a = (await am.Http(h).ConfigureAwait(false)).Item1;
            return (a != null) && String.Equals(a.Guid, user.Guid, StringComparison.Ordinal);
        }

        /// <summary>
        /// The response to a request (that requires auth) without any credentials
        /// </summary>
        static Task<HttpRequestData> Unauthorized(HttpServerRequest r)
        {
            r.SetResHeader("WWW-Authenticate", "Bearer realm=\"mcp\"");
            return Task.FromResult(JsonResponse(r, 401, Json(w => WriteError(w, default, ErrInvalidRequest, "Unauthorized, supply an API key using \"Authorization: Bearer <key>\" or \"x-api-key: <key>\""))));
        }

        /// <summary>
        /// GET (server initiated event stream) and DELETE (session termination) are not supported by this stateless server
        /// </summary>
        static Task<HttpRequestData> MethodNotAllowed(HttpServerRequest r)
        {
            r.SetResHeader("Allow", "POST");
            return Task.FromResult(JsonResponse(r, 405, Json(w => WriteError(w, default, ErrInvalidRequest, "Method " + r.Method + " is not allowed, use POST"))));
        }

        /// <summary>
        /// Check that the Origin header (if any) is allowed (prevents DNS rebinding attacks)
        /// </summary>
        bool IsOriginAllowed(HttpServerRequest r)
        {
            var origin = r.GetReqHeader("Origin");
            if (String.IsNullOrEmpty(origin) || AnyOrigin)
                return true;
            origin = origin.TrimEnd('/');
            if (AllowedOrigins.Contains(origin))
                return true;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var o))
                return false;
            var host = r.GetReqHeader("Host");
            return (host != null) && String.Equals(o.Authority, host, StringComparison.OrdinalIgnoreCase);
        }

        #endregion//Http module

        #region JSON-RPC

        /// <summary>
        /// The protocol versions supported, latest first
        /// </summary>
        static readonly String[] ProtocolVersions = ["2025-11-25", "2025-06-18", "2025-03-26"];

        const int ErrParse = -32700;
        const int ErrInvalidRequest = -32600;
        const int ErrMethodNotFound = -32601;
        const int ErrInvalidParams = -32602;
        const int ErrInternal = -32603;

        /// <summary>
        /// A JSON-RPC error
        /// </summary>
        sealed class RpcException : Exception
        {
            public RpcException(int code, String message) : base(message)
            {
                Code = code;
            }
            public readonly int Code;
        }

        async Task<HttpRequestData> Post(HttpServerRequest r)
        {
            using var _ = PerfMon.Track(nameof(Post));
            if (!IsOriginAllowed(r))
                return JsonResponse(r, 403, Json(w => WriteError(w, default, ErrInvalidRequest, "The origin is not allowed")));
            if (!await CredentialsMatchSession(r).ConfigureAwait(false))
                return JsonResponse(r, 403, Json(w => WriteError(w, default, ErrInvalidRequest, "The supplied credentials doesn't match the user of the session (don't send cookies to the MCP end point)")));
            var version = r.GetReqHeader("MCP-Protocol-Version");
            if ((version != null) && (!ProtocolVersions.Contains(version)))
                return JsonResponse(r, 400, Json(w => WriteError(w, default, ErrInvalidRequest, "Unsupported protocol version " + version.ToQuoted())));
            JsonDocument doc;
            try
            {
                doc = await ReadJson(r, MaxRequestSize).ConfigureAwait(false);
            }
            catch (RpcException ex)
            {
                return JsonResponse(r, 400, Json(w => WriteError(w, default, ex.Code, ex.SafeMessage())));
            }
            using (doc)
            {
                var root = doc.RootElement;
                //  Batches were removed in protocol version 2025-06-18, but are still accepted
                if (root.ValueKind == JsonValueKind.Array)
                {
                    var responses = new List<Byte[]>();
                    foreach (var m in root.EnumerateArray())
                    {
                        var res = await HandleMessage(r, m).ConfigureAwait(false);
                        if (res != null)
                            responses.Add(res);
                    }
                    if (responses.Count <= 0)
                        return Accepted(r);
                    return JsonResponse(r, 200, Json(w =>
                    {
                        w.WriteStartArray();
                        foreach (var x in responses)
                            w.WriteRawValue(x, true);
                        w.WriteEndArray();
                    }));
                }
                //  A tool can run for a long time (ex: video generation), stream the response so that the client gets the headers at once and keep alives while the tool runs
                if (StreamToolCalls && IsToolCall(root) && AcceptsEventStream(r))
                    return StreamToolCall(r, root.Clone());
                var response = await HandleMessage(r, root).ConfigureAwait(false);
                return response == null ? Accepted(r) : JsonResponse(r, 200, response);
            }
        }

        #region Streaming

        readonly bool StreamToolCalls;
        readonly TimeSpan KeepAliveInterval;

        const String EventStreamMime = "text/event-stream";

        static readonly Byte[] KeepAliveComment = ": keep-alive\n\n"u8.ToArray();
        static readonly Byte[] EventPrefix = "event: message\ndata: "u8.ToArray();
        static readonly Byte[] EventSuffix = "\n\n"u8.ToArray();

        /// <summary>
        /// Check if a message is a "tools/call" request (with an id, so that a response is expected)
        /// </summary>
        static bool IsToolCall(JsonElement m)
        {
            if (m.ValueKind != JsonValueKind.Object)
                return false;
            if (!String.Equals(GetString(m, "method"), "tools/call", StringComparison.Ordinal))
                return false;
            return m.TryGetProperty("id", out var id) && ((id.ValueKind == JsonValueKind.String) || (id.ValueKind == JsonValueKind.Number));
        }

        /// <summary>
        /// Check if the client accepts server sent events (MCP clients using the Streamable HTTP transport sends "Accept: application/json, text/event-stream")
        /// </summary>
        static bool AcceptsEventStream(HttpServerRequest r)
            => r.GetReqHeader("Accept")?.Contains(EventStreamMime, StringComparison.OrdinalIgnoreCase) ?? false;

        /// <summary>
        /// Answer a tool call with an event stream: the headers are sent at once, keep alives are sent while the tool runs and the result is sent as the final event.
        /// The tool is invoked using a detached copy of the request (same user), since the response headers are already sent while it runs.
        /// </summary>
        /// <param name="r">The request</param>
        /// <param name="m">The message (a clone, the request document is disposed when this returns)</param>
        /// <returns>The response data (the event stream)</returns>
        HttpRequestData StreamToolCall(HttpServerRequest r, JsonElement m)
        {
            var stream = new McpEventStream();
            var detacher = ManualHttpServerRequest.Detacher(r);
            r.SetResStatusCode(200);
            r.SetResMime(EventStreamMime);
            //  Prevents buffering in reverse proxies (nginx)
            r.SetResHeader("X-Accel-Buffering", "no");
            _ = RunToolCallStream(stream, detacher, m);
            return new HttpRequestData(stream);
        }

        async Task RunToolCallStream(McpEventStream stream, Func<ManualHttpServerRequest> detacher, JsonElement m)
        {
            //  Make sure that the request handling continues (the stream is read by the server once this method yields)
            await Task.Yield();
            try
            {
                using var wr = detacher();
                var progressToken = GetProgressToken(m);
                var start = DateTime.UtcNow;
                var work = HandleMessage(wr, m);
                //  Something to send at once, so that the client gets the response headers
                stream.Write(KeepAliveComment);
                long count = 0;
                while (!work.IsCompleted)
                {
                    try
                    {
                        await work.WaitAsync(KeepAliveInterval).ConfigureAwait(false);
                        break;
                    }
                    catch (TimeoutException)
                    {
                    }
                    //  The client is gone, the tool keeps running (the detached request must outlive it)
                    if (stream.IsClosed)
                        continue;
                    ++count;
                    if (progressToken.ValueKind == JsonValueKind.Undefined)
                    {
                        stream.Write(KeepAliveComment);
                        continue;
                    }
                    var seconds = (long)(DateTime.UtcNow - start).TotalSeconds;
                    stream.Write(Event(Json(w => WriteProgress(w, progressToken, count, "Running for " + seconds + " s"))));
                }
                var res = await work.ConfigureAwait(false);
                if (res != null)
                    stream.Write(Event(res));
            }
            catch (Exception ex)
            {
                Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Failed to stream a tool call", ex);
            }
            finally
            {
                stream.Complete();
            }
        }

        /// <summary>
        /// Get the progress token of a request ("params._meta.progressToken"), undefined if none
        /// </summary>
        static JsonElement GetProgressToken(JsonElement m)
        {
            if (m.TryGetProperty("params", out var p) && (p.ValueKind == JsonValueKind.Object)
                && p.TryGetProperty("_meta", out var meta) && (meta.ValueKind == JsonValueKind.Object)
                && meta.TryGetProperty("progressToken", out var t) && ((t.ValueKind == JsonValueKind.String) || (t.ValueKind == JsonValueKind.Number)))
                return t;
            return default;
        }

        static void WriteProgress(Utf8JsonWriter w, JsonElement progressToken, long progress, String message)
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            w.WriteString("method", "notifications/progress");
            w.WriteStartObject("params");
            w.WritePropertyName("progressToken");
            progressToken.WriteTo(w);
            w.WriteNumber("progress", progress);
            w.WriteString("message", message);
            w.WriteEndObject();
            w.WriteEndObject();
        }

        /// <summary>
        /// A server sent event with a JSON-RPC message (the json is never indented, so it's a single data line)
        /// </summary>
        static Byte[] Event(Byte[] json)
        {
            var b = new Byte[EventPrefix.Length + json.Length + EventSuffix.Length];
            EventPrefix.CopyTo(b, 0);
            json.CopyTo(b, EventPrefix.Length);
            EventSuffix.CopyTo(b, EventPrefix.Length + json.Length);
            return b;
        }

        #endregion//Streaming

        /// <summary>
        /// Handle a single JSON-RPC message
        /// </summary>
        /// <returns>The json of the response, null if no response should be sent (notifications and responses)</returns>
        async Task<Byte[]> HandleMessage(HttpServerRequest r, JsonElement m)
        {
            if (m.ValueKind != JsonValueKind.Object)
                return Json(w => WriteError(w, default, ErrInvalidRequest, "Expected a JSON-RPC message object"));
            m.TryGetProperty("id", out var id);
            var hasId = (id.ValueKind == JsonValueKind.String) || (id.ValueKind == JsonValueKind.Number);
            var method = GetString(m, "method");
            if (method == null)
            {
                //  A response (to a server request, we never send any) or garbage
                return hasId || m.TryGetProperty("result", out var __) || m.TryGetProperty("error", out var ___) ? null : Json(w => WriteError(w, default, ErrInvalidRequest, "Missing method"));
            }
            //  Notifications (ex: "notifications/initialized") have no id and no response
            if (!hasId)
                return null;
            m.TryGetProperty("params", out var p);
            try
            {
                using var _ = PerfMon.Track(method);
                switch (method)
                {
                    case "initialize":
                        return Result(id, w => Initialize(w, p));
                    case "ping":
                        return Result(id, w => { w.WriteStartObject(); w.WriteEndObject(); });
                    case "tools/list":
                        return Result(id, w => ListTools(w, r));
                    case "tools/call":
                        var res = await CallTool(r, p).ConfigureAwait(false);
                        return Result(id, res);
                    default:
                        throw new RpcException(ErrMethodNotFound, "Method " + method.ToQuoted() + " not found");
                }
            }
            catch (RpcException ex)
            {
                return Json(w => WriteError(w, id, ex.Code, ex.SafeMessage()));
            }
            catch (Exception ex)
            {
                Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Failed to handle {method.ToQuoted()}", ex);
                return Json(w => WriteError(w, id, ErrInternal, ex.SafeMessage()));
            }
        }

        void Initialize(Utf8JsonWriter w, JsonElement p)
        {
            //  Use the client version if supported, else our latest
            var requested = GetString(p, "protocolVersion");
            var version = ProtocolVersions.Contains(requested) ? requested : ProtocolVersions[0];
            w.WriteStartObject();
            w.WriteString("protocolVersion", version);
            w.WriteStartObject("capabilities");
            w.WriteStartObject("tools");
            w.WriteBoolean("listChanged", false);
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteStartObject("serverInfo");
            w.WriteString("name", ServerName);
            w.WriteString("version", ServerVersion);
            w.WriteEndObject();
            if (Instructions != null)
                w.WriteString("instructions", Instructions);
            w.WriteEndObject();
        }

        static readonly Byte[] EmptyInputSchema = """{"type":"object","properties":{}}"""u8.ToArray();

        void ListTools(Utf8JsonWriter w, HttpServerRequest r)
        {
            w.WriteStartObject();
            w.WriteStartArray("tools");
            foreach (var t in Tools.Values.Select(x => x.Tool).Where(x => CanUse(r, x)).OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                var f = t.Tool;
                w.WriteStartObject();
                w.WriteString("name", t.Name);
                if (!String.IsNullOrEmpty(f.FunctionDescription))
                    w.WriteString("description", f.FunctionDescription);
                w.WritePropertyName("inputSchema");
                var schema = f.FunctionParameters;
                w.WriteRawValue(schema == null ? EmptyInputSchema : schema.ToMemory().Span, true);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }

        async Task<Action<Utf8JsonWriter>> CallTool(HttpServerRequest r, JsonElement p)
        {
            var name = GetString(p, "name") ?? throw new RpcException(ErrInvalidParams, "Missing tool name");
            //  Tools that the user isn't allowed to use are hidden
            if ((!Tools.TryGetValue(name, out var entry)) || (!CanUse(r, entry.Tool)))
                throw new RpcException(ErrInvalidParams, "Unknown tool " + name.ToQuoted());
            var tool = entry.Tool;
            p.TryGetProperty("arguments", out var args);
            //  Same as the AI chat: a json object with a single property (the method argument)
            var b = args.ValueKind == JsonValueKind.Object ? BinaryData.FromString(args.GetRawText()) : null;
            String text;
            bool isError = false;
            //  Tools may use a tool context (to attach files etc), same as in the AI chat
            var context = new McpToolContext(r, (mime, data, filename) => StoreFile(r, mime, data, filename));
            var props = r.Properties;
            props[OpenAiToolExt.RequestAiToolContext] = context;
            using (PerfMon.Track(name))
            {
                //  Errors in the tool are reported in the result so that the LLM can see them
                try
                {
                    text = entry.File != null
                        ? await InvokeFile(r, entry, args, context).ConfigureAwait(false)
                        : ToText(await tool.Invoke(b, r).ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    while (((ex is TargetInvocationException) || (ex is AggregateException)) && (ex.InnerException != null))
                        ex = ex.InnerException;
                    Msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Tool {name.ToQuoted()} failed", ex);
                    //  The error is sent to the MCP client, so sensitive information is removed
                    text = ex.SafeMessage();
                    isError = true;
                }
                finally
                {
                    props.TryRemove(OpenAiToolExt.RequestAiToolContext, out var _);
                }
            }
            entry.OnCall(isError);
            var files = context.Files;
            var links = context.Links;
            var linkBase = new Uri(r.Prefix + Url);
            //  Tools made for the AI chat returns urls relative to the chat page, the MCP client needs an absolute url
            if (!isError && IsRelativeUrl(text) && Uri.TryCreate(linkBase, text, out var abs))
                text = abs.AbsoluteUri;
            return w =>
            {
                w.WriteStartObject();
                w.WriteStartArray("content");
                w.WriteStartObject();
                w.WriteString("type", "text");
                w.WriteString("text", text ?? "");
                w.WriteEndObject();
                foreach (var f in files)
                    WriteFileContent(w, f);
                foreach (var l in links)
                {
                    if (!Uri.TryCreate(linkBase, l, out var u))
                        continue;
                    w.WriteStartObject();
                    w.WriteString("type", "resource_link");
                    w.WriteString("uri", u.AbsoluteUri);
                    w.WriteString("name", Path.GetFileName(u.AbsolutePath) is { Length: > 0 } n ? n : u.AbsoluteUri);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteBoolean("isError", isError);
                w.WriteEndObject();
            };
        }

        /// <summary>
        /// Image types that MCP clients can display (sent as image content), other files are sent as embedded resources
        /// </summary>
        static readonly HashSet<String> ImageMimes = new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/gif", "image/webp" };

        /// <summary>
        /// The mime type without any parameters, ex: "application/json; charset=UTF-8" => "application/json"
        /// </summary>
        static String BaseMime(String mime)
        {
            var i = mime.IndexOf(';');
            return (i < 0 ? mime : mime.Substring(0, i)).Trim();
        }

        static bool IsText(String mime)
            => mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || mime.EndsWith("+xml", StringComparison.OrdinalIgnoreCase)
            || mime.EndsWith("/xml", StringComparison.OrdinalIgnoreCase)
            || mime.EndsWith("/json", StringComparison.OrdinalIgnoreCase)
            || mime.EndsWith("+json", StringComparison.OrdinalIgnoreCase);

        static void WriteFileContent(Utf8JsonWriter w, McpToolFile f)
        {
            var mime = f.Mime ?? "application/octet-stream";
            var baseMime = BaseMime(mime);
            w.WriteStartObject();
            if (ImageMimes.Contains(baseMime))
            {
                w.WriteString("type", "image");
                w.WriteBase64String("data", f.Data.Span);
                w.WriteString("mimeType", mime);
            }
            else
            {
                w.WriteString("type", "resource");
                w.WriteStartObject("resource");
                w.WriteString("uri", f.Url);
                w.WriteString("mimeType", mime);
                if (IsText(baseMime))
                    w.WriteString("text", Encoding.UTF8.GetString(f.Data.Span));
                else
                    w.WriteBase64String("blob", f.Data.Span);
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }

        static bool IsRelativeUrl(String text)
            => (text != null) && (text.StartsWith("../", StringComparison.Ordinal) || text.StartsWith("./", StringComparison.Ordinal)) && !text.Any(Char.IsWhiteSpace);

        /// <summary>
        /// Tools returns json, a json string is returned as plain text
        /// </summary>
        static String ToText(String json)
        {
            if (String.IsNullOrEmpty(json) || (json[0] != '"'))
                return json;
            try
            {
                return JsonSerializer.Deserialize<String>(json);
            }
            catch (JsonException)
            {
                return json;
            }
        }

        static String GetString(JsonElement e, String name)
            => (e.ValueKind == JsonValueKind.Object) && e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.String) ? v.GetString() : null;

        #endregion//JSON-RPC

        #region Json helpers

        const String JsonMime = "application/json; charset=utf-8";

        static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        static Byte[] Json(Action<Utf8JsonWriter> write)
        {
            var b = new ArrayBufferWriter<Byte>(1024);
            using (var w = new Utf8JsonWriter(b, WriterOptions))
                write(w);
            return b.WrittenSpan.ToArray();
        }

        static Byte[] Result(JsonElement id, Action<Utf8JsonWriter> result)
            => Json(w =>
            {
                w.WriteStartObject();
                w.WriteString("jsonrpc", "2.0");
                w.WritePropertyName("id");
                id.WriteTo(w);
                w.WritePropertyName("result");
                result(w);
                w.WriteEndObject();
            });

        static void WriteError(Utf8JsonWriter w, JsonElement id, int code, String message)
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            if ((id.ValueKind == JsonValueKind.String) || (id.ValueKind == JsonValueKind.Number))
            {
                w.WritePropertyName("id");
                id.WriteTo(w);
            }
            else
            {
                w.WriteNull("id");
            }
            w.WriteStartObject("error");
            w.WriteNumber("code", code);
            w.WriteString("message", message);
            w.WriteEndObject();
            w.WriteEndObject();
        }

        static HttpRequestData JsonResponse(HttpServerRequest r, int statusCode, Byte[] json)
        {
            r.SetResStatusCode(statusCode);
            r.SetResMime(JsonMime);
            return new HttpRequestData(json, true);
        }

        /// <summary>
        /// The response to a POST with only notifications and/or responses
        /// </summary>
        static HttpRequestData Accepted(HttpServerRequest r)
        {
            r.SetResStatusCode(202);
            return new HttpRequestData(ReadOnlyMemory<Byte>.Empty, true);
        }

        static async Task<JsonDocument> ReadJson(HttpServerRequest r, int maxSize)
        {
            var len = r.ReqContentLength;
            if (len > maxSize)
                throw new RpcException(ErrInvalidRequest, "The request body is too large");
            using var ms = new MemoryStream(len > 0 ? (int)len : 4096);
            var input = r.InputStream;
            if (input != null)
            {
                var buf = new Byte[65536];
                for (; ; )
                {
                    var read = await input.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false);
                    if (read <= 0)
                        break;
                    if ((ms.Length + read) > maxSize)
                        throw new RpcException(ErrInvalidRequest, "The request body is too large");
                    ms.Write(buf, 0, read);
                }
            }
            if (ms.Length <= 0)
                throw new RpcException(ErrInvalidRequest, "The request body is empty, expected a JSON-RPC message");
            try
            {
                return JsonDocument.Parse(ms.GetBuffer().AsMemory(0, (int)ms.Length));
            }
            catch (JsonException ex)
            {
                throw new RpcException(ErrParse, "The request body isn't valid json: " + ex.Message);
            }
        }

        #endregion//Json helpers
    }

    /// <summary>
    /// A request handler that calls a function to get the response
    /// </summary>
    sealed class McpRequestHandler : IHttpRequestHandler
    {
        public McpRequestHandler(String name, IReadOnlyList<String> auth, Func<HttpServerRequest, Task<HttpRequestData>> get)
        {
            Name = name;
            Auth = auth;
            GetFn = get;
        }

        readonly Func<HttpServerRequest, Task<HttpRequestData>> GetFn;

        public String Name { get; }
        public HttpServerRequest Redirected { get; set; }
        public int ClientCacheDuration => 0;
        public int RequestCacheDuration => 0;
        public HttpCompressionPriority Compression => null;
        public ICompDecoder Decoder => null;
        public IReadOnlyList<String> Auth { get; }
        public ValueTask<String> GetCacheKey(HttpServerRequest request) => TaskExt.NullStringValueTask;

        public String GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = true;
            return null;
        }

        public HttpRequestData Get(HttpServerRequest request) => throw new NotSupportedException();

        public ValueTask<HttpRequestData> GetAsync(HttpServerRequest request) => new ValueTask<HttpRequestData>(GetFn(request));
    }
}
