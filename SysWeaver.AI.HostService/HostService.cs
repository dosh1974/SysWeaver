using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// Hosts one or more AI services (IAiService) using an OpenAI compatible API (Chat Completions and Responses).
    /// Requests are forwarded to the AI service that provides the requested model.
    /// Endpoints (relative to the prefix):
    /// GET models, GET models/{model}, POST chat/completions, POST responses
    /// </summary>
    [OptionalDep<IAiService>]
    public sealed partial class HostService : IHttpServerModule, IPerfMonitored, IDisposable
    {
        const String LogPrefix = "[AiHost] ";

        /// <summary>
        /// Create an AI host service
        /// </summary>
        /// <param name="sm">The service manager (used to find the AI services)</param>
        /// <param name="p">The parameters</param>
        /// <param name="msg">Optional message host</param>
        public HostService(ServiceManager sm, HostParams p = null, IMessageHost msg = null)
        {
            p = p ?? new HostParams();
            Manager = sm;
            Msg = msg ?? sm;
            Instances = (p.Instances ?? []).Where(x => !String.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
            Auth = Authorization.GetRequiredTokens(p.Auth);
            MaxRequestSize = Math.Max(1024, p.MaxRequestSize);
            var prefix = (p.Prefix ?? "").Trim().TrimStart('/');
            if ((prefix.Length > 0) && (!prefix.EndsWith('/')))
                prefix += '/';
            Prefix = prefix;
            OnlyForPrefixes = prefix.Length > 0 ? [prefix] : null;
            ModelsHandler = new HostRequestHandler(nameof(GetModels), Auth, GetModels);
            ModelHandler = new HostRequestHandler(nameof(GetModel), Auth, GetModel);
            ChatCompletionsHandler = new HostRequestHandler(nameof(ChatCompletions), Auth, ChatCompletions);
            ResponsesHandler = new HostRequestHandler(nameof(Responses), Auth, Responses);
            MethodNotAllowedHandler = new HostRequestHandler("MethodNotAllowed", Auth, r => Task.FromResult(HostHttp.ErrorResponse(r, new HostException(405, "Method " + r.Method + " is not allowed"))));
            if (sm != null)
            {
                sm.OnServiceAdded += OnServicesChanged;
                sm.OnServiceRemoved += OnServicesChanged;
            }
        }

        public void Dispose()
        {
            var sm = Manager;
            if (sm == null)
                return;
            sm.OnServiceAdded -= OnServicesChanged;
            sm.OnServiceRemoved -= OnServicesChanged;
        }

        readonly ServiceManager Manager;
        readonly IMessageHost Msg;
        readonly String[] Instances;
        readonly IReadOnlyList<String> Auth;
        readonly int MaxRequestSize;
        readonly String Prefix;

        public PerfMonitor PerfMon { get; } = new PerfMonitor("AiHost");

        #region AI services

        /// <summary>
        /// The AI services to use (in creation order), null if they need to be resolved
        /// </summary>
        volatile IAiService[] Services;

        void OnServicesChanged(Object instance, ServiceInfo info)
        {
            if (instance is IAiService)
                Services = null;
        }

        /// <summary>
        /// Get the hosted AI services (in creation order)
        /// </summary>
        public IAiService[] GetServices()
        {
            var s = Services;
            if (s != null)
                return s;
            var all = Manager?.GetAllInfo<IAiService>(ServiceInstanceTypes.Any, ServiceInstanceOrders.Oldest).ToList() ?? [];
            var names = Instances;
            if (names.Length > 0)
            {
                var set = new HashSet<String>(names, StringComparer.OrdinalIgnoreCase);
                s = all.Where(x => set.Contains(x.Value.Name ?? "")).Select(x => x.Key).ToArray();
                var found = new HashSet<String>(all.Select(x => x.Value.Name ?? ""), StringComparer.OrdinalIgnoreCase);
                foreach (var x in names)
                    if (!found.Contains(x))
                        Msg?.AddMessage(LogPrefix + "No AI service instance named " + x.ToQuoted() + " was found", MessageLevels.Warning);
            }
            else
            {
                s = all.Take(1).Select(x => x.Key).ToArray();
            }
            if (s.Length <= 0)
                Msg?.AddMessage(LogPrefix + "No AI service found", MessageLevels.Warning);
            Services = s;
            return s;
        }

        #endregion//AI services

        #region Models

        /// <summary>
        /// A model provided by a hosted AI service
        /// </summary>
        sealed class HostModel
        {
            public HostModel(AiLlmModel model, IAiService service)
            {
                Model = model;
                Service = service;
            }
            public readonly AiLlmModel Model;
            public readonly IAiService Service;
        }

        /// <summary>
        /// Maps model codes to AI services
        /// </summary>
        sealed class ModelMap
        {
            public IAiService[] Services;

            /// <summary>
            /// The model lists the map was built from (the AI services caches them, so the map is rebuilt when any of them changes)
            /// </summary>
            public AiLlmModel[][] Lists;

            /// <summary>
            /// All models (in order)
            /// </summary>
            public HostModel[] Models;

            public Dictionary<String, HostModel> Lookup;

            /// <summary>
            /// The model used when no model is specified, null if no models are known
            /// </summary>
            public HostModel Default;
        }

        volatile ModelMap Map;

        /// <summary>
        /// Get the current model map (rebuilt if the AI services or their models have changed)
        /// </summary>
        async Task<ModelMap> GetMap()
        {
            var services = GetServices();
            var sl = services.Length;
            var lists = new AiLlmModel[sl][];
            for (int i = 0; i < sl; ++i)
            {
                try
                {
                    lists[i] = await services[i].GetLlmModels().ConfigureAwait(false) ?? [];
                }
                catch (Exception ex)
                {
                    Msg?.AddMessage(LogPrefix + "Failed to get the models of an AI service", ex, MessageLevels.Warning);
                    lists[i] = [];
                }
            }
            var map = Map;
            if ((map != null) && (map.Services == services) && map.Lists.Length == sl)
            {
                bool same = true;
                for (int i = 0; same && (i < sl); ++i)
                    same = map.Lists[i] == lists[i];
                if (same)
                    return map;
            }
            var models = new List<HostModel>();
            var lookup = new Dictionary<String, HostModel>(StringComparer.OrdinalIgnoreCase);
            HostModel def = null;
            for (int i = 0; i < sl; ++i)
            {
                foreach (var m in lists[i])
                {
                    if (String.IsNullOrEmpty(m?.Id))
                        continue;
                    var hm = new HostModel(m, services[i]);
                    //  The default is the last model of the last AI service (that have any models)
                    def = hm;
                    //  If several services provide the same model, the first one is used
                    if (lookup.TryAdd(m.Id, hm))
                        models.Add(hm);
                }
            }
            map = new ModelMap
            {
                Services = services,
                Lists = lists,
                Models = models.ToArray(),
                Lookup = lookup,
                Default = def,
            };
            Map = map;
            return map;
        }

        /// <summary>
        /// Get the AI service to use for a model
        /// </summary>
        /// <param name="model">The requested model, null or empty for the default model</param>
        /// <returns>The AI service and the model to use</returns>
        async Task<ValueTuple<IAiService, String>> ResolveModel(String model)
        {
            var map = await GetMap().ConfigureAwait(false);
            if (String.IsNullOrEmpty(model))
            {
                var d = map.Default;
                if (d != null)
                    return (d.Service, d.Model.Id);
                //  No models are listed, use the default model of the last service
                var s = map.Services;
                if (s.Length <= 0)
                    throw new HostException(503, "No AI service is available", "server_error", "service_unavailable");
                var ls = s[s.Length - 1];
                return (ls, ls.DefaultChatModel);
            }
            if (map.Lookup.TryGetValue(model, out var m))
                return (m.Service, m.Model.Id);
            throw new HostException(404, String.Concat("The model `", model, "` does not exist or you do not have access to it."), "invalid_request_error", "model_not_found", "model");
        }

        static void WriteModel(Utf8JsonWriter w, AiLlmModel m)
        {
            w.WriteStartObject();
            w.WriteString("id", m.Id);
            w.WriteString("object", "model");
            w.WriteNumber("created", m.Created.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(m.Created.Value, DateTimeKind.Utc)).ToUnixTimeSeconds() : 0);
            w.WriteString("owned_by", String.IsNullOrEmpty(m.Owner) ? "system" : m.Owner);
            w.WriteEndObject();
        }

        async Task<HttpRequestData> GetModels(HttpServerRequest r)
        {
            using var _ = PerfMon.Track(nameof(GetModels));
            try
            {
                var map = await GetMap().ConfigureAwait(false);
                return HostHttp.JsonResponse(r, 200, HostHttp.Json(w =>
                {
                    w.WriteStartObject();
                    w.WriteString("object", "list");
                    w.WriteStartArray("data");
                    foreach (var m in map.Models)
                        WriteModel(w, m.Model);
                    w.WriteEndArray();
                    w.WriteEndObject();
                }));
            }
            catch (Exception ex)
            {
                return HostHttp.ErrorResponse(r, HostHttp.ToHostException(ex));
            }
        }

        async Task<HttpRequestData> GetModel(HttpServerRequest r)
        {
            using var _ = PerfMon.Track(nameof(GetModel));
            try
            {
                var id = Uri.UnescapeDataString(r.LocalUrl.Substring(Prefix.Length + ModelsPath.Length + 1));
                var map = await GetMap().ConfigureAwait(false);
                if (!map.Lookup.TryGetValue(id, out var m))
                    throw new HostException(404, String.Concat("The model `", id, "` does not exist or you do not have access to it."), "invalid_request_error", "model_not_found", "model");
                return HostHttp.JsonResponse(r, 200, HostHttp.Json(w => WriteModel(w, m.Model)));
            }
            catch (Exception ex)
            {
                return HostHttp.ErrorResponse(r, HostHttp.ToHostException(ex));
            }
        }

        #endregion//Models

        #region Http module

        const String ModelsPath = "models";
        const String ChatCompletionsPath = "chat/completions";
        const String ResponsesPath = "responses";

        public String[] OnlyForPrefixes { get; }

        readonly IHttpRequestHandler ModelsHandler;
        readonly IHttpRequestHandler ModelHandler;
        readonly IHttpRequestHandler ChatCompletionsHandler;
        readonly IHttpRequestHandler ResponsesHandler;
        readonly IHttpRequestHandler MethodNotAllowedHandler;

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var url = context.LocalUrl;
            var prefix = Prefix;
            if (!url.StartsWith(prefix, StringComparison.Ordinal))
                return null;
            var path = url.AsSpan(prefix.Length).TrimEnd('/');
            var method = context.HttpMethod;
            var isGet = (method == HttpServerMethods.GET) || (method == HttpServerMethods.HEAD);
            var isPost = method == HttpServerMethods.POST;
            if (path.Equals(ChatCompletionsPath, StringComparison.Ordinal))
                return isPost ? ChatCompletionsHandler : MethodNotAllowedHandler;
            if (path.Equals(ResponsesPath, StringComparison.Ordinal))
                return isPost ? ResponsesHandler : MethodNotAllowedHandler;
            if (path.Equals(ModelsPath, StringComparison.Ordinal))
                return isGet ? ModelsHandler : MethodNotAllowedHandler;
            if (path.StartsWith(ModelsPath + "/", StringComparison.Ordinal) && (path.Length > ModelsPath.Length + 1))
                return isGet ? ModelHandler : MethodNotAllowedHandler;
            return null;
        }

        #endregion//Http module

        #region Request parsing

        static String NewId() => Guid.NewGuid().ToString("N");

        static long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        static bool TryGet(JsonElement e, String name, out JsonElement value)
        {
            if ((e.ValueKind == JsonValueKind.Object) && e.TryGetProperty(name, out value) && (value.ValueKind != JsonValueKind.Null) && (value.ValueKind != JsonValueKind.Undefined))
                return true;
            value = default;
            return false;
        }

        static String GetString(JsonElement e, String name, String param = null)
        {
            if (!TryGet(e, name, out var v))
                return null;
            if (v.ValueKind != JsonValueKind.String)
                throw HostException.BadRequest(String.Concat("Expected ", (param ?? name).ToQuoted(), " to be a string"), param ?? name);
            return v.GetString();
        }

        static bool? GetBool(JsonElement e, String name)
        {
            if (!TryGet(e, name, out var v))
                return null;
            if ((v.ValueKind != JsonValueKind.True) && (v.ValueKind != JsonValueKind.False))
                throw HostException.BadRequest(String.Concat("Expected ", name.ToQuoted(), " to be a boolean"), name);
            return v.GetBoolean();
        }

        static float? GetFloat(JsonElement e, String name)
        {
            if (!TryGet(e, name, out var v))
                return null;
            if ((v.ValueKind != JsonValueKind.Number) || (!v.TryGetSingle(out var f)))
                throw HostException.BadRequest(String.Concat("Expected ", name.ToQuoted(), " to be a number"), name);
            return f;
        }

        static int? GetInt(JsonElement e, String name)
        {
            if (!TryGet(e, name, out var v))
                return null;
            if ((v.ValueKind != JsonValueKind.Number) || (!v.TryGetInt32(out var i)))
                throw HostException.BadRequest(String.Concat("Expected ", name.ToQuoted(), " to be an integer"), name);
            return i;
        }

        static AiReasoning? ParseReasoning(String effort, String param)
            => effort switch
            {
                null => null,
                "none" => AiReasoning.None,
                "minimal" => AiReasoning.Minimal,
                "low" => AiReasoning.Low,
                "medium" => AiReasoning.Medium,
                "high" => AiReasoning.High,
                "xhigh" => AiReasoning.ExtraHigh,
                _ => throw HostException.BadRequest("Unknown reasoning effort " + effort.ToQuoted(), param),
            };

        static AiCompletionToolChoices ParseToolChoice(JsonElement root, String name)
        {
            if (!TryGet(root, name, out var v))
                return AiCompletionToolChoices.Auto;
            if (v.ValueKind == JsonValueKind.String)
                return v.GetString() switch
                {
                    "auto" => AiCompletionToolChoices.Auto,
                    "none" => AiCompletionToolChoices.None,
                    "required" => AiCompletionToolChoices.Required,
                    var s => throw HostException.BadRequest("Unknown tool choice " + s.ToQuoted(), name),
                };
            //  A specific function, treated as required
            if (v.ValueKind == JsonValueKind.Object)
                return AiCompletionToolChoices.Required;
            throw HostException.BadRequest("Invalid tool choice", name);
        }

        /// <summary>
        /// Get a tool function definition
        /// </summary>
        static AiToolFunction ParseFunction(JsonElement f, String param)
        {
            var name = GetString(f, "name", param + ".name");
            if (String.IsNullOrEmpty(name))
                throw HostException.BadRequest("A function must have a name", param + ".name");
            var desc = GetString(f, "description", param + ".description");
            BinaryData parameters = null;
            if (TryGet(f, "parameters", out var p))
            {
                if (p.ValueKind != JsonValueKind.Object)
                    throw HostException.BadRequest("The parameters of a function must be a json schema object", param + ".parameters");
                parameters = BinaryData.FromString(p.GetRawText());
            }
            return new AiToolFunction(name, desc, parameters);
        }

        /// <summary>
        /// Get the arguments of a tool call (a string with json, or a json object)
        /// </summary>
        static String GetArguments(JsonElement e, String name)
        {
            if (!TryGet(e, name, out var v))
                return "{}";
            return v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText();
        }

        /// <summary>
        /// Get the text of a content (string or an array of text parts)
        /// </summary>
        static String GetContentText(JsonElement e, String name, String param)
        {
            if (!TryGet(e, name, out var c))
                return null;
            if (c.ValueKind == JsonValueKind.String)
                return c.GetString();
            if (c.ValueKind != JsonValueKind.Array)
                throw HostException.BadRequest("Expected the content to be a string or an array of content parts", param);
            var parts = new List<String>();
            foreach (var p in c.EnumerateArray())
            {
                if (p.ValueKind == JsonValueKind.String)
                {
                    parts.Add(p.GetString());
                    continue;
                }
                var type = GetString(p, "type", param + ".type");
                switch (type)
                {
                    case "text":
                    case "input_text":
                    case "output_text":
                        parts.Add(GetString(p, "text", param + ".text") ?? "");
                        break;
                    case "refusal":
                        parts.Add(GetString(p, "refusal", param + ".refusal") ?? "");
                        break;
                    default:
                        throw HostException.BadRequest(String.Concat("Content of type ", type.ToQuoted(), " is not supported, only text is supported"), param);
                }
            }
            return String.Join("\n", parts);
        }

        static void AddSystem(ref String system, String text)
        {
            if (String.IsNullOrEmpty(text))
                return;
            system = String.IsNullOrEmpty(system) ? text : String.Concat(system, "\n\n", text);
        }

        static String GetFinishReason(AiCompletionFinishReasons r)
            => r switch
            {
                AiCompletionFinishReasons.Length => "length",
                AiCompletionFinishReasons.ToolCalls => "tool_calls",
                _ => "stop",
            };

        /// <summary>
        /// Get the text that should be added to already sent text (streaming can't take anything back)
        /// </summary>
        static String GetDelta(String sent, String text)
        {
            if (String.IsNullOrEmpty(text) || (text.Length <= sent.Length))
                return null;
            if (!text.StartsWith(sent, StringComparison.Ordinal))
                return null;
            return text.Substring(sent.Length);
        }

        #endregion//Request parsing
    }
}
