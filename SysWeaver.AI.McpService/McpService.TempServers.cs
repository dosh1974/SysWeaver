using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// A temporary MCP server that was created (see <see cref="McpService.CreateTempServer"/>)
    /// </summary>
    public sealed class McpTempServerInfo
    {
        /// <summary>
        /// The guid of the temporary server (used to check or remove it, and as the Param of the MCP tool and call log tables)
        /// </summary>
        public String Guid;

        /// <summary>
        /// The API key to use when connecting to the server ("Authorization: Bearer &lt;key&gt;" or "x-api-key: &lt;key&gt;")
        /// </summary>
        public String ApiKey;

        /// <summary>
        /// The url of the MCP end point (relative to the server root, absolute when returned by a web API)
        /// </summary>
        public String Url;

        /// <summary>
        /// The number of tools served
        /// </summary>
        public int Tools;

        /// <summary>
        /// Instances or types that couldn't be found (they are not served)
        /// </summary>
        public String[] Warnings;
    }

    /// <summary>
    /// The state of a temporary MCP server
    /// </summary>
    public sealed class McpTempServerStatus
    {
        /// <summary>
        /// True if the server is running (else no other value is set)
        /// </summary>
        public bool Running;

        /// <summary>
        /// The guid of the temporary server
        /// </summary>
        public String Guid;

        /// <summary>
        /// The url of the MCP end point
        /// </summary>
        public String Url;

        /// <summary>
        /// The guid of the user that created the server
        /// </summary>
        public String CreatedBy;

        /// <summary>
        /// When the server was created (UTC)
        /// </summary>
        public DateTime Created;

        /// <summary>
        /// When the last request was made to the server (UTC), the creation time if no request have been made
        /// </summary>
        public DateTime LastRequest;

        /// <summary>
        /// When the server will be removed unless a request is made (UTC)
        /// </summary>
        public DateTime Expires;

        /// <summary>
        /// The number of seconds until the server is removed (unless a request is made)
        /// </summary>
        public double SecondsToExpiration;

        /// <summary>
        /// The number of http requests made to the server (all requests, including tool listings and failed requests)
        /// </summary>
        public long Requests;

        /// <summary>
        /// The number of tools served
        /// </summary>
        public int Tools;

        /// <summary>
        /// The number of tool calls
        /// </summary>
        public long Calls;

        /// <summary>
        /// The number of tool calls that failed (an exception in the tool or an unknown tool)
        /// </summary>
        public long Errors;

        /// <summary>
        /// When the last tool call was made (UTC), null if none
        /// </summary>
        public DateTime? LastCall;

        /// <summary>
        /// The last error (tool name and message), null if none
        /// </summary>
        public String LastError;

        /// <summary>
        /// When the last error happened (UTC), null if none
        /// </summary>
        public DateTime? LastErrorTime;

        /// <summary>
        /// The services whose API's and AI tools are served, with the number of each, ex: "SysWeaver.MicroService.ServiceManager (12 API's, 2 AI tools)"
        /// </summary>
        public String[] Services;

        /// <summary>
        /// Instances or types that couldn't be found when the server was created
        /// </summary>
        public String[] Warnings;
    }

    public sealed partial class McpService
    {
        /// <summary>
        /// The url of a temporary server is this followed by the guid
        /// </summary>
        const String TempUrlPrefix = "mcp_";

        /// <summary>
        /// The auth token of a temporary server is this followed by the guid
        /// </summary>
        const String TempAuthPrefix = "mcp_";

        /// <summary>
        /// The user name of the API key of a temporary server is this followed by the guid
        /// </summary>
        const String TempUserPrefix = "mcp_";

        /// <summary>
        /// The monitoring services that the debug server (StartDebugMcp) exposes by default (besides the service manager and the http server),
        /// as type names since this project doesn't reference them
        /// </summary>
        static readonly String[] DebugTypeNames =
        [
            "SysWeaver.MicroService.ExploreHttpServerService, SysWeaver.MicroService.HttpServer",
            "SysWeaver.MicroService.FileLogService, SysWeaver.MicroServices.Log",
            "SysWeaver.MicroService.ChartJsService, SysWeaver.MicroService.ChartJs",
        ];

        const String DebugInstructions =
            "Exposes API's and AI tools of a running SysWeaver service, selected by an administrator. " +
            "The monitoring API's (if selected) give log entries, the log file, performance measurements, statistics, problems (failures), " +
            "loaded services, scheduled tasks, active sessions and users, caches etc: use them to look for errors, bugs and performance problems in the running service. " +
            "Table API's take a TableDataRequest (paging, sorting and filtering), start with a small MaxRowCount.";

        sealed class TempServer
        {
            public TempServer(McpService service, String guid, String userGuid, String apiKey, String authToken, String[] services, String[] warnings)
            {
                Service = service;
                Guid = guid;
                UserGuid = userGuid;
                ApiKey = apiKey;
                AuthToken = authToken;
                Services = services;
                Warnings = warnings;
                Created = DateTime.UtcNow;
                LastUseTicks = Created.Ticks;
            }

            public readonly McpService Service;
            public readonly String Guid;
            public readonly String UserGuid;
            public readonly String ApiKey;
            public readonly String AuthToken;
            public readonly String[] Services;
            public readonly String[] Warnings;
            public readonly DateTime Created;

            public long LastUseTicks;
            public long Requests;

            /// <summary>
            /// The authorization of the API key, recreated when the change counter of the authorizer changes
            /// </summary>
            public volatile Task<Authorization> Auth;

            public String UserName => TempUserPrefix + Guid;

            public DateTime LastUse => new DateTime(Interlocked.Read(ref LastUseTicks), DateTimeKind.Utc);

            public void Touch()
            {
                Interlocked.Exchange(ref LastUseTicks, DateTime.UtcNow.Ticks);
                Interlocked.Increment(ref Requests);
            }
        }

        /// <summary>
        /// The temporary servers, key is the guid
        /// </summary>
        readonly ConcurrentDictionary<String, TempServer> TempServers = new(StringComparer.Ordinal);

        ConcurrentDictionary<String, TempServer>.AlternateLookup<ReadOnlySpan<Char>> TempServerLookup => TempServers.GetAlternateLookup<ReadOnlySpan<Char>>();

        /// <summary>
        /// The temporary servers, key is the API key
        /// </summary>
        readonly ConcurrentDictionary<String, TempServer> TempApiKeys = new(StringComparer.Ordinal);

        /// <summary>
        /// McpParams.TempServerLifetimeMinutes
        /// </summary>
        readonly TimeSpan TempLifetime;

        /// <summary>
        /// Removes expired temporary servers, created with the first temporary server
        /// </summary>
        Timer TempTimer;

        int IsDisposed;

        /// <summary>
        /// A random guid that can be used in an url (base 64 with '-' and '_' instead of '+' and '/')
        /// </summary>
        static String NewUrlGuid()
            => SecureRng.GetHashGuid24().Replace('+', '-').Replace('/', '_');

        TempServer TryGetTempServer(String guid)
        {
            if (String.IsNullOrEmpty(guid))
                return null;
            return TempServers.TryGetValue(guid.Trim(), out var t) ? t : null;
        }

        /// <summary>
        /// Create a temporary MCP server that exposes the [WebApi] methods of some instances, it's served at "mcp_{guid}" (by this service) until it's removed.
        /// The server is removed McpParams.TempServerLifetimeMinutes after the last request to it, when it's removed (<see cref="RemoveTempServer(string, HttpServerRequest)"/>) or when this service is disposed.
        /// A new auth token ("mcp_{guid}") is required by the server and by all its tools (it replaces the auth of the methods),
        /// the returned API key (this service is the authorizer of the key) is the only way to get that token.
        /// The server isn't registered in the service manager.
        /// </summary>
        /// <param name="instancesOrTypeNames">The instances whose [WebApi] methods should be exposed, a string is a type name and the instance registered as that type in the service manager is used.
        /// Types that can't be found and types without a registered instance are ignored (with a warning).</param>
        /// <param name="userGuid">The guid of the user that creates the server (only that user can remove it)</param>
        /// <param name="onlyForAuths">Optional set of auth tokens, if non null only methods that requires one of the tokens are exposed (see <see cref="AddWebApiTools(object, bool, IReadOnlySet{string}, string)"/>)</param>
        /// <param name="instructions">Optional instructions reported to MCP clients, null to use McpParams.Instructions</param>
        /// <returns>The guid, API key and url of the server</returns>
        public McpTempServerInfo CreateTempServer(Object[] instancesOrTypeNames, String userGuid, IReadOnlySet<String> onlyForAuths = null, String instructions = null)
            => CreateTempServerFrom(instancesOrTypeNames.Nullable().Select(x => new TempServerSource(x, null, null)), userGuid, onlyForAuths, instructions, null);

        /// <summary>
        /// What to expose from an instance on a temporary server
        /// </summary>
        sealed class TempServerSource
        {
            /// <param name="instanceOrTypeName">The instance, or the name of the type of a registered instance</param>
            /// <param name="webApis">The names of the [WebApi] methods to expose, null for all</param>
            /// <param name="aiTools">The names of the AI tool methods to expose, null for none</param>
            public TempServerSource(Object instanceOrTypeName, IReadOnlySet<String> webApis, IReadOnlySet<String> aiTools)
            {
                InstanceOrTypeName = instanceOrTypeName;
                WebApis = webApis;
                AiTools = aiTools;
            }

            public readonly Object InstanceOrTypeName;
            public readonly IReadOnlySet<String> WebApis;
            public readonly IReadOnlySet<String> AiTools;

            public bool IncludeWebApi(MethodInfo m) => WebApis?.Contains(m.Name) ?? true;
            public bool IncludeAiTool(MethodInfo m) => AiTools?.Contains(m.Name) ?? false;
        }

        /// <summary>
        /// Create a temporary MCP server (see <see cref="CreateTempServer(object[], string, IReadOnlySet{string}, string)"/>) with the selected [WebApi] methods and AI tools of each instance
        /// </summary>
        /// <param name="sources">The instances (or type names) and what to expose of each</param>
        /// <param name="userGuid">The guid of the user that creates the server</param>
        /// <param name="onlyForAuths">Optional auth filter</param>
        /// <param name="instructions">Optional instructions reported to MCP clients, null to use McpParams.Instructions</param>
        /// <param name="warnings">Optional warnings found before (they are kept with the server), null for none</param>
        /// <returns>The guid, API key and url of the server</returns>
        McpTempServerInfo CreateTempServerFrom(IEnumerable<TempServerSource> sources, String userGuid, IReadOnlySet<String> onlyForAuths, String instructions, List<String> warnings)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref IsDisposed) != 0, this);
            if (IsTemp)
                throw new InvalidOperationException("A temporary MCP server can't create temporary servers");
            var p = Params;
            var guid = NewUrlGuid();
            var url = TempUrlPrefix + guid;
            var token = TempAuthPrefix + guid;
            var service = new McpService(null, new McpParams
            {
                Url = url,
                Auth = token,
                ServerName = ServerName,
                Instructions = instructions ?? p.Instructions,
                AllowedOrigins = p.AllowedOrigins,
                ServiceTools = false,
                FileLifetimeMinutes = p.FileLifetimeMinutes,
                DataLifetimeMinutes = p.DataLifetimeMinutes,
                StreamToolCalls = p.StreamToolCalls,
                KeepAliveSeconds = p.KeepAliveSeconds,
                MaxRequestSize = p.MaxRequestSize,
                CallLogSize = p.CallLogSize,
                CallLogMaxLength = p.CallLogMaxLength,
            }, Msg, true, DefaultToolAuth);
            List<String> services = new();
            warnings ??= new();
            foreach (var source in sources)
            {
                var x = source?.InstanceOrTypeName;
                var instance = x;
                if (x is String typeName)
                {
                    typeName = typeName.Trim();
                    var t = TypeFinder.Get(typeName);
                    if (t == null)
                    {
                        var w = "The type " + typeName.ToQuoted() + " wasn't found";
                        warnings.Add(w);
                        Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}{w}, it's not added to the temporary server {url.ToQuoted()}");
                        continue;
                    }
                    instance = Manager?.TryGet(t);
                    if (instance == null)
                    {
                        var w = "No instance of " + t.FullName.ToQuoted() + " is registered";
                        warnings.Add(w);
                        Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}{w}, it's not added to the temporary server {url.ToQuoted()}");
                        continue;
                    }
                }
                if (instance == null)
                    continue;
                var n = ((source.WebApis == null) || (source.WebApis.Count > 0)) ? service.AddWebApiTools(instance, true, onlyForAuths, token, source.IncludeWebApi) : 0;
                var a = (source.AiTools?.Count ?? 0) > 0 ? service.AddAiTools(instance, onlyForAuths, token, source.IncludeAiTool) : 0;
                var desc = String.Concat(instance.GetType().FullName, " (", n, n == 1 ? " API" : " API's");
                if (a > 0)
                    desc = String.Concat(desc, ", ", a, a == 1 ? " AI tool" : " AI tools");
                services.Add(desc + ")");
            }
            var key = SecureRng.GetHashGuid24();
            var temp = new TempServer(service, guid, userGuid, key, token, services.ToArray(), warnings.ToArray());
            TempServers[guid] = temp;
            TempApiKeys[key] = temp;
            lock (TempServers)
            {
                if (Volatile.Read(ref IsDisposed) != 0)
                {
                    RemoveTemp(guid, "the MCP service was disposed");
                    throw new ObjectDisposedException(GetType().FullName);
                }
                if (TempTimer == null)
                {
                    var interval = TimeSpan.FromSeconds(Math.Clamp(TempLifetime.TotalSeconds / 10, 5, 60));
                    TempTimer = new Timer(_ => RemoveExpiredTemps(), null, interval, interval);
                }
            }
            Msg?.AddMessage(MessageLevels.Info, $"{LogPrefix}Created the temporary MCP server {url.ToQuoted()} with {service.Tools.Count} tool(s) for the user {userGuid.ToQuoted()}");
            return new McpTempServerInfo
            {
                Guid = guid,
                ApiKey = key,
                Url = url,
                Tools = service.Tools.Count,
                Warnings = temp.Warnings,
            };
        }

        /// <summary>
        /// Remove a temporary MCP server (see StartDebugMcp), only the user that created the server can remove it
        /// </summary>
        /// <param name="guid">The guid of the temporary server</param>
        /// <param name="context">The request</param>
        /// <returns>True if the server was removed, false if it doesn't exist or was created by another user</returns>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        public bool RemoveTempServer(String guid, HttpServerRequest context)
        {
            var t = TryGetTempServer(guid);
            if (t == null)
                return false;
            var user = context?.Session?.Auth?.Guid;
            if ((user == null) || !String.Equals(t.UserGuid, user, StringComparison.Ordinal))
                return false;
            return RemoveTemp(t.Guid, "removed by the user");
        }


        #region Debug server (StartDebugMcp)

        /// <summary>
        /// The methods of the MCP service that are never offered by the debug server (they manage the debug servers themselves)
        /// </summary>
        static readonly HashSet<String> DebugExcludedMethods = new(StringComparer.Ordinal)
        {
            nameof(StartDebugMcp),
            nameof(GetDebugMcpServices),
            nameof(CheckDebugMcp),
            nameof(RemoveTempServer),
        };

        /// <summary>
        /// A service that can be exposed by the debug server, and the methods that the user may expose (the key is the method name)
        /// </summary>
        sealed class DebugCandidate
        {
            public Object Instance;
            public String TypeName;
            public String InstanceName;
            public bool Default;
            public SortedDictionary<String, DebugMcpMethod> WebApis;
            public SortedDictionary<String, DebugMcpMethod> AiTools;

            public bool IsMatch(DebugMcpService s)
                => String.Equals(s?.TypeName?.Trim(), TypeName, StringComparison.OrdinalIgnoreCase)
                && String.Equals(String.IsNullOrWhiteSpace(s.InstanceName) ? null : s.InstanceName.Trim(), InstanceName, StringComparison.OrdinalIgnoreCase);

            public String DisplayName => InstanceName == null ? TypeName : String.Concat(TypeName, " [", InstanceName, "]");
        }

        /// <summary>
        /// True if a type is one of the monitoring services that the debug server exposes by default (<see cref="DebugTypeNames"/>)
        /// </summary>
        static bool IsDebugDefaultType(Type t)
        {
            foreach (var x in DebugTypeNames)
            {
                var c = x.IndexOf(',');
                if (String.Equals(c < 0 ? x : x.Substring(0, c).Trim(), t.FullName, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The description of a method for the selection list: the first paragraph of the XML documentation (the tool description without the return value)
        /// </summary>
        static String ShortDesc(String desc)
        {
            if (String.IsNullOrWhiteSpace(desc))
                return null;
            desc = desc.Trim();
            var i = desc.IndexOf("## Tool returns", StringComparison.Ordinal);
            if (i >= 0)
                desc = desc.Substring(0, i).Trim().TrimEnd('.');
            i = desc.IndexOf('\n');
            if (i > 0)
                desc = desc.Substring(0, i).Trim();
            return desc.Length == 0 ? null : desc.LimitLength(300);
        }

        /// <summary>
        /// The required auth of a method as text: "" if anyone may use it (no auth), "*" if any signed in user may use it, else the tokens
        /// </summary>
        static String AuthText(IReadOnlyList<String> auth)
        {
            if (auth == null)
                return "";
            if (ReferenceEquals(auth, AuthTools.NoAuth))
                return "-";
            return auth.Count <= 0 ? "*" : String.Join(", ", auth);
        }

        /// <summary>
        /// Describe a method of a service, null if it can't be described or if the user may not use it (the debug server replaces the auth of the methods,
        /// so it must never expose a method that the user creating it can't use)
        /// </summary>
        DebugMcpMethod DescribeDebugMethod(Object instance, MethodInfo m, HttpServerRequest context)
        {
            try
            {
                var tool = AiTool.GetToolName(m);
                var ep = ApiHttpEntry.Create(AiTool.IoParams, instance, m, tool, null, DefaultToolAuth);
                var auth = ep.Auth;
                if (!(context.Session?.IsValid(auth) ?? (auth == null)))
                    return null;
                ep.GetDesc(out var _, out var _, out var desc, out var _, out var _, out var _);
                return new DebugMcpMethod
                {
                    Method = m.Name,
                    Tool = tool,
                    Description = ShortDesc(desc),
                    Auth = AuthText(auth),
                };
            }
            catch (Exception ex)
            {
                Msg?.AddMessage(MessageLevels.Debug, $"{LogPrefix}Failed to describe the method {m.Name.ToQuoted()} of {m.DeclaringType?.Name.ToQuoted()} for the debug server, ignoring it", ex);
                return null;
            }
        }

        /// <summary>
        /// The [WebApi] methods of an instance that can be exposed as tools (same rules as <see cref="AddWebApiTools(object, bool, IReadOnlySet{string}, string)"/>)
        /// </summary>
        IEnumerable<MethodInfo> GetWebApiToolMethods(Object instance)
        {
            foreach (var m in instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy))
            {
                if (m.GetCustomAttributeWithInterface<WebApiAttribute>(true) == null)
                    continue;
                if (IsHidden(m))
                    continue;
                if (m.GetCustomAttribute<WebApiRawAttribute>(true)?.PreCompressedWith != null)
                    continue;
                bool enabled;
                try
                {
                    enabled = ApiHttpServerModule.IsEnabled(instance, m);
                }
                catch
                {
                    enabled = false;
                }
                if (enabled)
                    yield return m;
            }
        }

        /// <summary>
        /// Get the services that the user of a request can expose using the debug server: the service manager, the http server and every registered (local) service
        /// that have at least one [WebApi] method or AI tool that the user may use.
        /// </summary>
        List<DebugCandidate> GetDebugCandidates(HttpServerRequest context)
        {
            List<DebugCandidate> res = new();
            HashSet<Object> seen = new(ReferenceEqualityComparer.Instance);
            void Add(Object instance, String name, bool isDefault)
            {
                if ((instance == null) || !seen.Add(instance))
                    return;
                //  Temporary servers are never registered, but a second MCP service may be
                var isMcp = instance is McpService;
                SortedDictionary<String, DebugMcpMethod> webApis = new(StringComparer.Ordinal);
                foreach (var m in GetWebApiToolMethods(instance))
                {
                    if ((isMcp && DebugExcludedMethods.Contains(m.Name)) || webApis.ContainsKey(m.Name))
                        continue;
                    var d = DescribeDebugMethod(instance, m, context);
                    if (d != null)
                        webApis[m.Name] = d;
                }
                SortedDictionary<String, DebugMcpMethod> aiTools = new(StringComparer.Ordinal);
                foreach (var m in AiTool.GetToolMethods(instance.GetType()))
                {
                    if (IsHidden(m) || (isMcp && DebugExcludedMethods.Contains(m.Name)) || aiTools.ContainsKey(m.Name))
                        continue;
                    var d = DescribeDebugMethod(instance, m, context);
                    if (d != null)
                        aiTools[m.Name] = d;
                }
                if ((webApis.Count + aiTools.Count) <= 0)
                    return;
                res.Add(new DebugCandidate
                {
                    Instance = instance,
                    TypeName = instance.GetType().CleanAssemblyQualifiedTypename(),
                    InstanceName = String.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                    Default = isDefault,
                    WebApis = webApis,
                    AiTools = aiTools,
                });
            }
            var man = Manager;
            Add(man, null, true);
            Add(context.Server, null, true);
            if (man != null)
                foreach (var x in man.OrderedUniqueInfoInstances)
                {
                    //  A remote service is a proxy, its API's are served by another process
                    if (x.Value?.Remote ?? false)
                        continue;
                    Add(x.Key, x.Value?.Name, IsDebugDefaultType(x.Key.GetType()));
                }
            return res;
        }

        /// <summary>
        /// Get the services that can be exposed by a debug MCP server (see <see cref="StartDebugMcp"/>): the service manager, the http server and every registered service
        /// that have [WebApi] methods or AI tools (methods with an AiToolAttribute or AiUseAttribute), with the methods that the current user may use.
        /// Methods hidden from MCP (AiHideMcpAttribute), disabled methods and methods returning pre-compressed data are not listed, nor are the methods that manage the debug servers.
        /// </summary>
        /// <param name="context">The request</param>
        /// <returns>The services, in registration order (the service manager and the http server first)</returns>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        public DebugMcpServiceInfo[] GetDebugMcpServices(HttpServerRequest context)
            => GetDebugCandidates(context).Select(c => new DebugMcpServiceInfo
            {
                TypeName = c.TypeName,
                InstanceName = c.InstanceName,
                Name = c.Instance.GetType().Name,
                Default = c.Default,
                WebApis = c.WebApis.Values.ToArray(),
                AiTools = c.AiTools.Values.ToArray(),
            }).ToArray();

        /// <summary>
        /// Pick the methods of a candidate from a requested list of method names (null = all, unknown names are reported as warnings)
        /// </summary>
        static HashSet<String> PickDebugMethods(DebugCandidate c, SortedDictionary<String, DebugMcpMethod> available, String[] requested, String kind, List<String> warnings)
        {
            if (requested == null)
                return new HashSet<String>(available.Keys, StringComparer.Ordinal);
            HashSet<String> res = new(StringComparer.Ordinal);
            foreach (var x in requested)
            {
                var n = x?.Trim();
                if (String.IsNullOrEmpty(n))
                    continue;
                if (available.ContainsKey(n))
                    res.Add(n);
                else
                    warnings.Add(String.Concat("The ", kind, " ", n.ToQuoted(), " of ", c.DisplayName.ToQuoted(), " isn't available"));
            }
            return res;
        }

        /// <summary>
        /// Start a temporary MCP server that exposes selected [WebApi] methods and AI tools of the services of this process, so that an AI agent can analyze the running service
        /// (look for bugs and performance problems) or use it.
        /// Use <see cref="GetDebugMcpServices"/> to get the services and methods that can be selected.
        /// Without a selection (no request, or no services in it), the monitoring API's are exposed: the service manager, the http server, the explorer, the log file and the charts (logs, performance, statistics, problems, sessions etc).
        /// Only methods that the current user may use can be exposed, the tools of the server only require the API key of the server.
        /// The server is removed McpParams.TempServerLifetimeMinutes (default 60) after the last request to it, or when removed using <see cref="RemoveTempServer"/>.
        /// </summary>
        /// <param name="r">The services and methods to expose, null for the monitoring API's</param>
        /// <param name="context">The request</param>
        /// <returns>The guid, API key and absolute url of the server</returns>
        /// <exception cref="HttpResponseException">400 if nothing that can be exposed was selected</exception>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        public McpTempServerInfo StartDebugMcp(StartDebugMcpRequest r, HttpServerRequest context)
        {
            var user = context.Session?.Auth?.Guid ?? throw new UnauthorizedAccessException("A user is required");
            var candidates = GetDebugCandidates(context);
            List<TempServerSource> sources = new();
            List<String> warnings = new();
            var selected = r?.Services;
            if ((selected == null) || (selected.Length <= 0))
            {
                foreach (var c in candidates)
                    if (c.Default)
                        sources.Add(new TempServerSource(c.Instance, new HashSet<String>(c.WebApis.Keys, StringComparer.Ordinal), null));
            }
            else
            {
                HashSet<DebugCandidate> used = new(ReferenceEqualityComparer.Instance);
                foreach (var s in selected)
                {
                    if (String.IsNullOrWhiteSpace(s?.TypeName))
                        continue;
                    var c = candidates.FirstOrDefault(x => x.IsMatch(s));
                    if (c == null)
                    {
                        var n = String.IsNullOrWhiteSpace(s.InstanceName) ? s.TypeName.Trim() : String.Concat(s.TypeName.Trim(), " [", s.InstanceName.Trim(), "]");
                        warnings.Add(String.Concat("The service ", n.ToQuoted(), " isn't available"));
                        continue;
                    }
                    if (!used.Add(c))
                        continue;
                    var web = PickDebugMethods(c, c.WebApis, s.WebApis, "API", warnings);
                    var ai = PickDebugMethods(c, c.AiTools, s.AiTools, "AI tool", warnings);
                    if ((web.Count + ai.Count) > 0)
                        sources.Add(new TempServerSource(c.Instance, web, ai));
                }
            }
            if (sources.Count <= 0)
                throw new HttpResponseException(400, warnings.Count > 0
                    ? "Nothing to expose: " + String.Join(", ", warnings)
                    : "Nothing to expose, select at least one API or AI tool");
            var info = CreateTempServerFrom(sources, user, null, DebugInstructions, warnings);
            info.Url = context.Prefix + info.Url;
            return info;
        }

        #endregion//Debug server (StartDebugMcp)

        /// <summary>
        /// Get the state of a temporary MCP server (see StartDebugMcp)
        /// </summary>
        /// <param name="guid">The guid of the temporary server</param>
        /// <param name="context">The request</param>
        /// <returns>The state, Running is false if the server doesn't exist (any more)</returns>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        public McpTempServerStatus CheckDebugMcp(String guid, HttpServerRequest context)
        {
            var t = TryGetTempServer(guid);
            if (t == null)
                return new McpTempServerStatus { Guid = guid };
            var s = t.Service;
            var last = t.LastUse;
            var expires = last + TempLifetime;
            var lastCall = Interlocked.Read(ref s.LastCallTicks);
            var lastError = Interlocked.Read(ref s.LastErrorTicks);
            return new McpTempServerStatus
            {
                Running = true,
                Guid = t.Guid,
                Url = (context?.Prefix ?? "") + s.Url,
                CreatedBy = t.UserGuid,
                Created = t.Created,
                LastRequest = last,
                Expires = expires,
                SecondsToExpiration = Math.Max(0, (expires - DateTime.UtcNow).TotalSeconds),
                Requests = Interlocked.Read(ref t.Requests),
                Tools = s.Tools.Count,
                Calls = Interlocked.Read(ref s.TotalCalls),
                Errors = Interlocked.Read(ref s.TotalErrors),
                LastCall = lastCall == 0 ? null : new DateTime(lastCall, DateTimeKind.Utc),
                LastError = Volatile.Read(ref s.LastError),
                LastErrorTime = lastError == 0 ? null : new DateTime(lastError, DateTimeKind.Utc),
                Services = t.Services,
                Warnings = t.Warnings,
            };
        }

        /// <summary>
        /// Remove a temporary server, the API key stops working at once (sessions using it are logged out)
        /// </summary>
        bool RemoveTemp(String guid, String reason)
        {
            if (!TempServers.TryRemove(guid, out var t))
                return false;
            TempApiKeys.TryRemove(t.ApiKey, out var _);
            //  Invalidates the cached authorizations (AuthManager)
            Interlocked.Increment(ref InternalChangeCounter);
            var a = t.Auth;
            if ((a != null) && a.IsCompletedSuccessfully)
                a.Result?.RequestLogout(reason);
            t.Service.Dispose();
            Msg?.AddMessage(MessageLevels.Info, $"{LogPrefix}Removed the temporary MCP server {t.Service.Url.ToQuoted()}, {reason}");
            return true;
        }

        void RemoveExpiredTemps()
        {
            try
            {
                var now = DateTime.UtcNow;
                foreach (var x in TempServers)
                    if ((x.Value.LastUse + TempLifetime) <= now)
                        RemoveTemp(x.Key, "it wasn't used for " + TempLifetime.TotalMinutes + " minutes");
            }
            catch (Exception ex)
            {
                Msg?.AddMessage(MessageLevels.Warning, $"{LogPrefix}Failed to remove expired temporary servers", ex);
            }
        }

        void DisposeTempServers()
        {
            Timer timer;
            lock (TempServers)
            {
                Interlocked.Exchange(ref IsDisposed, 1);
                timer = TempTimer;
                TempTimer = null;
            }
            timer?.Dispose();
            foreach (var x in TempServers.Keys.ToArray())
                RemoveTemp(x, "the MCP service was disposed");
        }

        #region Authorizer (API keys of temporary servers)

        readonly String GuidPrefixValue;
        long InternalChangeCounter;

        /// <inheritdoc />
        public override String Name => "MCP " + Url;

        /// <inheritdoc />
        public override String GuidPrefix => GuidPrefixValue;

        /// <inheritdoc />
        public override long ChangeCounter => Interlocked.Read(ref InternalChangeCounter);

        /// <summary>
        /// Authorize the API key of a temporary server, the user only have the auth token of that server
        /// </summary>
        /// <param name="token">The API key</param>
        /// <returns>The authorization, null if the key is unknown</returns>
        public override Task<Authorization> BearerAuth(String token)
        {
            if ((token == null) || !TempApiKeys.TryGetValue(token, out var t))
                return NoAuth;
            var a = t.Auth;
            //  Authorizations created before the change counter changed are rejected by the auth manager cache, create a new one
            if ((a != null) && (a.Result.Cc == ChangeCounter))
                return a;
            a = Task.FromResult(new Authorization(this, t.UserName, Authorization.GetRequiredTokenSet(t.AuthToken), true, GetUserGuid(t), null, "MCP " + t.Service.Url)
            {
                IsApiKey = true,
            });
            t.Auth = a;
            return a;
        }

        String GetUserGuid(TempServer t) => String.Concat(GuidPrefixValue, ":", t.Guid);

        static readonly Task<AuthorizationInfo> NoUser = Task.FromResult<AuthorizationInfo>(null);

        Task<AuthorizationInfo> GetUserInfo(TempServer t)
            => t == null ? NoUser : Task.FromResult(new AuthorizationInfo(t.UserName, Authorization.GetRequiredTokenSet(t.AuthToken), null, null, null, GetUserGuid(t), "MCP " + t.Service.Url));

        /// <inheritdoc />
        public override Task<AuthorizationInfo> FindUserFromGuid(String userGuid)
        {
            var p = GuidPrefixValue;
            if ((userGuid == null) || (userGuid.Length <= (p.Length + 1)) || !userGuid.StartsWith(p, StringComparison.Ordinal) || (userGuid[p.Length] != ':'))
                return NoUser;
            return GetUserInfo(TryGetTempServer(userGuid.Substring(p.Length + 1)));
        }

        /// <inheritdoc />
        public override Task<AuthorizationInfo> FindUser(String userName)
        {
            if ((userName == null) || !userName.StartsWith(TempUserPrefix, StringComparison.Ordinal))
                return NoUser;
            return GetUserInfo(TryGetTempServer(userName.Substring(TempUserPrefix.Length)));
        }

        #endregion//Authorizer (API keys of temporary servers)
    }


    /// <summary>
    /// What a debug MCP server should expose (see McpService.StartDebugMcp)
    /// </summary>
    public sealed class StartDebugMcpRequest
    {
        /// <summary>
        /// The services and their methods to expose (see McpService.GetDebugMcpServices).
        /// Null or empty exposes the monitoring API's (the services that DebugMcpServiceInfo.Default is true for, all their API's and no AI tools).
        /// </summary>
        public DebugMcpSelection[] Services;
    }

    /// <summary>
    /// Identifies a service instance (a row of McpService.GetDebugMcpServices)
    /// </summary>
    public class DebugMcpService
    {
        /// <summary>
        /// The type of the service (assembly qualified, without version etc), ex: "SysWeaver.MicroService.ServiceManager, SysWeaver.MicroServices.ServiceManager"
        /// </summary>
        public String TypeName;

        /// <summary>
        /// The name of the instance, null for an instance without a name (there can be more than one instance of a type, with different names)
        /// </summary>
        public String InstanceName;
    }

    /// <summary>
    /// The methods of a service to expose on a debug MCP server
    /// </summary>
    public sealed class DebugMcpSelection : DebugMcpService
    {
        /// <summary>
        /// The names of the [WebApi] methods to expose (DebugMcpMethod.Method), null for all, empty for none
        /// </summary>
        public String[] WebApis;

        /// <summary>
        /// The names of the AI tool methods to expose (DebugMcpMethod.Method), null for all, empty for none
        /// </summary>
        public String[] AiTools;
    }

    /// <summary>
    /// A service that a debug MCP server can expose, and the methods that the current user may expose
    /// </summary>
    public sealed class DebugMcpServiceInfo : DebugMcpService
    {
        /// <summary>
        /// The short name of the type, ex: "ServiceManager"
        /// </summary>
        public String Name;

        /// <summary>
        /// True if the service is exposed by default (a monitoring service: the service manager, the http server, the explorer, the log file and the charts)
        /// </summary>
        public bool Default;

        /// <summary>
        /// The [WebApi] methods that can be exposed, sorted by name
        /// </summary>
        public DebugMcpMethod[] WebApis;

        /// <summary>
        /// The AI tools (methods with an AiToolAttribute or AiUseAttribute) that can be exposed, sorted by name
        /// </summary>
        public DebugMcpMethod[] AiTools;
    }

    /// <summary>
    /// A method that a debug MCP server can expose as a tool
    /// </summary>
    public sealed class DebugMcpMethod
    {
        /// <summary>
        /// The name of the method (used to select it, see DebugMcpSelection)
        /// </summary>
        public String Method;

        /// <summary>
        /// The name of the tool, as seen by MCP clients (a method that is both a [WebApi] and an AI tool is only exposed once)
        /// </summary>
        public String Tool;

        /// <summary>
        /// The first paragraph of the description of the tool, null if it has none
        /// </summary>
        public String Description;

        /// <summary>
        /// The auth the method normally requires: "" for none, "*" for any signed in user, else the tokens (comma separated).
        /// On the debug server every tool only requires the API key of the server.
        /// </summary>
        public String Auth;
    }


}
