using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// Serves MCP (Model Context Protocol) requests using the Streamable HTTP transport.
    /// A single end point (McpParams.Url) accepts JSON-RPC messages using POST, responses are returned as json (application/json).
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
            ServerName = String.IsNullOrWhiteSpace(p.ServerName) ? EnvInfo.AppName : p.ServerName.Trim();
            ServerVersion = Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "1.0.0";
            Instructions = String.IsNullOrWhiteSpace(p.Instructions) ? null : p.Instructions;
            var origins = (p.AllowedOrigins ?? []).Where(x => !String.IsNullOrWhiteSpace(x)).Select(x => x.Trim().TrimEnd('/')).ToArray();
            AnyOrigin = origins.Contains("*");
            AllowedOrigins = new HashSet<String>(origins, StringComparer.OrdinalIgnoreCase);
            var auth = Authorization.GetRequiredTokens(p.Auth);
            RequiresAuth = auth != null;
            UnauthorizedHandler = new McpRequestHandler("Unauthorized", null, Unauthorized);
            PostHandler = new McpRequestHandler(nameof(Post), auth, Post);
            MethodNotAllowedHandler = new McpRequestHandler("MethodNotAllowed", auth, MethodNotAllowed);
            AddTools(this);
            ServiceTools = p.ServiceTools;
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
            }
            Msg?.AddMessage(LogPrefix + "Serving MCP at " + url.ToQuoted() + " with " + Tools.Count + " tool(s)", MessageLevels.Debug);
        }

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
            if (ServiceTools && !ReferenceEquals(instance, this))
                AddTools(instance);
        }

        void Sm_OnServiceRemoved(Object instance, ServiceInfo info)
        {
            if (instance is IUserStorageService us)
                Interlocked.CompareExchange(ref UserStorage, Manager.TryGet<IUserStorageService>(), us);
            if (ServiceTools && !ReferenceEquals(instance, this))
                RemoveTools(instance);
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

        public PerfMonitor PerfMon { get; } = new PerfMonitor("Mcp");

        #region Tools

        /// <summary>
        /// The registered tools, key is the tool name
        /// </summary>
        readonly ConcurrentDictionary<String, AiTool> Tools = new(StringComparer.Ordinal);

        /// <summary>
        /// The names of all registered tools
        /// </summary>
        public IReadOnlyCollection<String> ToolNames => Tools.Keys.ToArray();

        /// <summary>
        /// Register a method as a tool, the tool is described in the same way as AI chat tools (XML documentation, AiToolNameAttribute, AiToolPrefixAttribute, WebApiAuthAttribute etc).
        /// </summary>
        /// <param name="instance">Object instance (null for static methods)</param>
        /// <param name="method">The method</param>
        /// <param name="fn">Optional tool name, default is computed from the AiToolPrefixAttribute and AiToolNameAttribute (type name + method name)</param>
        /// <returns>True if the tool was added, false if a tool with the same name already exists</returns>
        public bool AddTool(Object instance, MethodInfo method, String fn = null)
        {
            AiTool tool;
            try
            {
                tool = AiTool.FromMethod(instance, method, fn, PerfMon);
            }
            catch (Exception ex)
            {
                //  A bad method shouldn't prevent other tools from being registered
                Msg?.AddMessage(LogPrefix + "Failed to add the method " + method.Name.ToQuoted() + " of " + method.DeclaringType?.Name.ToQuoted() + " as a tool", ex, MessageLevels.Warning);
                return false;
            }
            if (!Tools.TryAdd(tool.Name, tool))
            {
                Msg?.AddMessage(LogPrefix + "A tool named " + tool.Name.ToQuoted() + " already exists, ignoring the method " + method.Name.ToQuoted() + " of " + method.DeclaringType?.Name.ToQuoted(), MessageLevels.Debug);
                return false;
            }
            Msg?.AddMessage(LogPrefix + "Added tool " + tool.Name.ToQuoted(), MessageLevels.Debug);
            return true;
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
            => Tools.TryRemove(name, out var _);

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
                    Msg?.AddMessage(LogPrefix + "Failed to store " + name.ToQuoted() + " in the user storage, the file is only temporary available", ex, MessageLevels.Warning);
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

        #region Http module

        public String[] OnlyForPrefixes { get; }

        readonly bool RequiresAuth;
        readonly IHttpRequestHandler PostHandler;
        readonly IHttpRequestHandler MethodNotAllowedHandler;
        readonly IHttpRequestHandler UnauthorizedHandler;

        const String IndexSuffix = "/index.html";

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var url = context.LocalUrl.AsSpan();
            //  The server adds "index.html" to urls ending with a '/'
            if (context.DidIndex && url.EndsWith(IndexSuffix, StringComparison.Ordinal))
                url = url.Slice(0, url.Length - IndexSuffix.Length);
            //  Files attached by tools are public (the url is unguessable), so that they can be displayed by any client
            if (url.StartsWith(FilesPrefix, StringComparison.Ordinal))
                return TryGetFile(context, out var _) ? FileHandler : null;
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
                return JsonResponse(r, 400, Json(w => WriteError(w, default, ex.Code, ex.Message)));
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
                var response = await HandleMessage(r, root).ConfigureAwait(false);
                return response == null ? Accepted(r) : JsonResponse(r, 200, response);
            }
        }

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
                return Json(w => WriteError(w, id, ex.Code, ex.Message));
            }
            catch (Exception ex)
            {
                Msg?.AddMessage(LogPrefix + "Failed to handle " + method.ToQuoted(), ex, MessageLevels.Warning);
                return Json(w => WriteError(w, id, ErrInternal, ex.Message));
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
            foreach (var t in Tools.Values.Where(x => CanUse(r, x)).OrderBy(x => x.Name, StringComparer.Ordinal))
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
            if ((!Tools.TryGetValue(name, out var tool)) || (!CanUse(r, tool)))
                throw new RpcException(ErrInvalidParams, "Unknown tool " + name.ToQuoted());
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
                    text = ToText(await tool.Invoke(b, r).ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    while (((ex is TargetInvocationException) || (ex is AggregateException)) && (ex.InnerException != null))
                        ex = ex.InnerException;
                    Msg?.AddMessage(LogPrefix + "Tool " + name.ToQuoted() + " failed", ex, MessageLevels.Debug);
                    text = ex.Message;
                    isError = true;
                }
                finally
                {
                    props.TryRemove(OpenAiToolExt.RequestAiToolContext, out var _);
                }
            }
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

        public Task<HttpRequestData> GetAsync(HttpServerRequest request) => GetFn(request);
    }
}
