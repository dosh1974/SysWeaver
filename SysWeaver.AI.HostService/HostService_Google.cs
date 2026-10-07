using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// The Google Gemini API and Vertex AI (generate content), see: https://ai.google.dev/api and https://cloud.google.com/vertex-ai/generative-ai/docs/reference/rest
    /// End points (relative to the Google prefix):
    /// GET {version}/models, GET {version}/models/{model},
    /// POST {version}/models/{model}:generateContent, POST {version}/models/{model}:streamGenerateContent (Gemini API)
    /// POST {version}/projects/{project}/locations/{location}/publishers/{publisher}/models/{model}:generateContent (Vertex AI, also streamGenerateContent)
    /// POST {version}/publishers/{publisher}/models/{model}:generateContent (Vertex AI express mode, also streamGenerateContent)
    /// </summary>
    public sealed partial class HostService
    {
        /// <summary>
        /// All Google API versions starts with this, ex: "v1", "v1beta", "v1beta1"
        /// </summary>
        const String GoogleVersionStart = "v1";

        const String GenerateContentMethod = "generateContent";
        const String StreamGenerateContentMethod = "streamGenerateContent";
        const String CountTokensMethod = "countTokens";

        #region Routing

        IHttpRequestHandler GoogleError(String name, HostException ex)
            => new HostRequestHandler(name, Auth, r => Task.FromResult(GoogleErrorResponse(r, ex)));

        IHttpRequestHandler GoogleHandler(HttpServerRequest context, ReadOnlySpan<Char> path, bool isGet, bool isPost)
        {
            var s = path.ToString();
            var slash = s.IndexOf('/');
            if (slash <= 0)
                return null;
            var version = s.Substring(0, slash);
            if (!version.StartsWith(GoogleVersionStart, StringComparison.Ordinal))
                return null;
            var rest = s.Substring(slash + 1);
            if (rest == ModelsPath)
                return isGet ? GoogleModelsHandler : GoogleError("GoogleMethodNotAllowed", new HostException(405, "Method " + context.Method + " is not allowed"));
            String modelPart;
            bool vertex = false;
            if (rest.StartsWith(ModelsPath + "/", StringComparison.Ordinal))
            {
                modelPart = rest.Substring(ModelsPath.Length + 1);
            }
            else
            {
                //  Vertex AI: "projects/{project}/locations/{location}/publishers/{publisher}/models/{model}" or (express mode) "publishers/{publisher}/models/{model}"
                if (rest.StartsWith("projects/", StringComparison.Ordinal))
                {
                    var parts = rest.Split('/', 5);
                    if ((parts.Length < 5) || (parts[2] != "locations"))
                        return null;
                    rest = parts[4];
                }
                if (!rest.StartsWith("publishers/", StringComparison.Ordinal))
                    return null;
                var parts2 = rest.Split('/', 4);
                if ((parts2.Length < 4) || (parts2[2] != ModelsPath))
                    return null;
                modelPart = parts2[3];
                vertex = true;
            }
            var colon = modelPart.LastIndexOf(':');
            if (colon < 0)
            {
                if (vertex || (modelPart.Length <= 0) || modelPart.Contains('/'))
                    return null;
                var id = Uri.UnescapeDataString(modelPart);
                return isGet
                    ? new HostRequestHandler("GoogleGetModel", Auth, r => GoogleGetModel(r, id))
                    : GoogleError("GoogleMethodNotAllowed", new HostException(405, "Method " + context.Method + " is not allowed"));
            }
            var model = Uri.UnescapeDataString(modelPart.Substring(0, colon));
            var method = modelPart.Substring(colon + 1);
            if ((model.Length <= 0) || model.Contains('/'))
                return null;
            switch (method)
            {
                case GenerateContentMethod:
                case StreamGenerateContentMethod:
                    if (!isPost)
                        return GoogleError("GoogleMethodNotAllowed", new HostException(405, "Method " + context.Method + " is not allowed"));
                    var stream = method == StreamGenerateContentMethod;
                    return new HostRequestHandler(vertex ? "VertexGenerateContent" : "GoogleGenerateContent", Auth, r => GoogleGenerateContent(r, model, stream));
                case CountTokensMethod:
                    return GoogleError("GoogleCountTokens", new HostException(501, "countTokens is not supported", "server_error", "not_implemented"));
            }
            return GoogleError("GoogleUnknownMethod", new HostException(404, String.Concat("Method ", method.ToQuoted(), " is not supported"), "invalid_request_error", "not_found"));
        }

        #endregion//Routing

        #region Errors

        static String GetGoogleStatus(int code)
            => code switch
            {
                400 => "INVALID_ARGUMENT",
                401 => "UNAUTHENTICATED",
                403 => "PERMISSION_DENIED",
                404 => "NOT_FOUND",
                409 => "ALREADY_EXISTS",
                429 => "RESOURCE_EXHAUSTED",
                499 => "CANCELLED",
                501 => "UNIMPLEMENTED",
                503 => "UNAVAILABLE",
                504 => "DEADLINE_EXCEEDED",
                _ => code >= 500 ? "INTERNAL" : "INVALID_ARGUMENT",
            };

        static void WriteGoogleError(Utf8JsonWriter w, HostException ex)
        {
            w.WriteStartObject();
            w.WriteStartObject("error");
            w.WriteNumber("code", ex.StatusCode);
            w.WriteString("message", ex.SafeMessage());
            w.WriteString("status", GetGoogleStatus(ex.StatusCode));
            w.WriteEndObject();
            w.WriteEndObject();
        }

        static HttpRequestData GoogleErrorResponse(HttpServerRequest r, HostException ex)
            => HostHttp.JsonResponse(r, ex.StatusCode, HostHttp.Json(w => WriteGoogleError(w, ex)));

        #endregion//Errors

        #region Models

        IHttpRequestHandler GoogleModelsHandlerInstance;

        IHttpRequestHandler GoogleModelsHandler => GoogleModelsHandlerInstance ??= new HostRequestHandler("GoogleGetModels", Auth, GoogleGetModels);

        static void WriteGoogleModel(Utf8JsonWriter w, AiLlmModel m)
        {
            w.WriteStartObject();
            w.WriteString("name", "models/" + m.Id);
            w.WriteString("baseModelId", m.Id);
            w.WriteString("version", "1");
            w.WriteString("displayName", String.IsNullOrEmpty(m.Name) ? m.Id : m.Name);
            w.WriteString("description", m.Description ?? "");
            if (m.InputTokenLimit.HasValue)
                w.WriteNumber("inputTokenLimit", m.InputTokenLimit.Value);
            if (m.OutputTokenLimit.HasValue)
                w.WriteNumber("outputTokenLimit", m.OutputTokenLimit.Value);
            w.WriteStartArray("supportedGenerationMethods");
            w.WriteStringValue(GenerateContentMethod);
            w.WriteStringValue(StreamGenerateContentMethod);
            w.WriteEndArray();
            if (m.CanReason.HasValue)
                w.WriteBoolean("thinking", m.CanReason.Value);
            w.WriteEndObject();
        }

        async Task<HttpRequestData> GoogleGetModels(HttpServerRequest r)
        {
            using var _ = PerfMon.Track(nameof(GoogleGetModels));
            try
            {
                var map = await GetMap().ConfigureAwait(false);
                return HostHttp.JsonResponse(r, 200, HostHttp.Json(w =>
                {
                    w.WriteStartObject();
                    w.WriteStartArray("models");
                    foreach (var m in map.Models)
                        WriteGoogleModel(w, m.Model);
                    w.WriteEndArray();
                    w.WriteEndObject();
                }));
            }
            catch (Exception ex)
            {
                return GoogleErrorResponse(r, HostHttp.ToHostException(ex));
            }
        }

        async Task<HttpRequestData> GoogleGetModel(HttpServerRequest r, String id)
        {
            using var _ = PerfMon.Track(nameof(GoogleGetModel));
            try
            {
                var map = await GetMap().ConfigureAwait(false);
                if (!map.Lookup.TryGetValue(id, out var m))
                    throw new HostException(404, String.Concat("models/", id, " is not found."), "invalid_request_error", "model_not_found");
                return HostHttp.JsonResponse(r, 200, HostHttp.Json(w => WriteGoogleModel(w, m.Model)));
            }
            catch (Exception ex)
            {
                return GoogleErrorResponse(r, HostHttp.ToHostException(ex));
            }
        }

        #endregion//Models

        #region Request parsing

        /// <summary>
        /// Convert a camel case name to snake case, ex: "systemInstruction" to "system_instruction"
        /// </summary>
        static String ToSnakeCase(String name)
        {
            var sb = new StringBuilder(name.Length + 4);
            foreach (var c in name)
            {
                if (Char.IsUpper(c))
                    sb.Append('_').Append(Char.ToLowerInvariant(c));
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Get a property, the REST API accepts both camel case and snake case names
        /// </summary>
        static bool TryGetG(JsonElement e, String camelName, out JsonElement value)
            => TryGet(e, camelName, out value) || TryGet(e, ToSnakeCase(camelName), out value);

        static String GetStringG(JsonElement e, String camelName, String param)
        {
            if (!TryGetG(e, camelName, out var v))
                return null;
            if (v.ValueKind != JsonValueKind.String)
                throw HostException.BadRequest(String.Concat("Expected ", param.ToQuoted(), " to be a string"), param);
            return v.GetString();
        }

        static JsonElement GetParts(JsonElement content, String param)
        {
            if ((!TryGetG(content, "parts", out var parts)) || (parts.ValueKind != JsonValueKind.Array))
                throw HostException.BadRequest(String.Concat("Expected ", (param + ".parts").ToQuoted(), " to be an array"), param + ".parts");
            return parts;
        }

        static void ThrowIfMedia(JsonElement part, String param)
        {
            if (TryGetG(part, "inlineData", out _) || TryGetG(part, "fileData", out _))
                throw HostException.BadRequest("Only text content is supported (inline data and files are not supported)", param);
        }

        static bool IsThought(JsonElement part)
            => TryGet(part, "thought", out var t) && (t.ValueKind == JsonValueKind.True);

        /// <summary>
        /// Get the text of a content (system instruction)
        /// </summary>
        static String GetContentText(JsonElement content, String param)
        {
            if (content.ValueKind == JsonValueKind.String)
                return content.GetString();
            var texts = new List<String>();
            int index = 0;
            foreach (var part in GetParts(content, param).EnumerateArray())
            {
                var pp = String.Concat(param, ".parts[", (index++).ToString(), "]");
                ThrowIfMedia(part, pp);
                var t = GetStringG(part, "text", pp + ".text");
                if (t != null)
                    texts.Add(t);
            }
            return String.Join("\n", texts);
        }

        /// <summary>
        /// Get the text of a function response, a single string value is used as is, else the json
        /// </summary>
        static String GetFunctionResponseText(JsonElement fr)
        {
            if (!TryGet(fr, "response", out var r))
                return "";
            if (r.ValueKind == JsonValueKind.String)
                return r.GetString();
            if (r.ValueKind == JsonValueKind.Object)
            {
                JsonProperty single = default;
                int count = 0;
                foreach (var p in r.EnumerateObject())
                {
                    single = p;
                    ++count;
                }
                if ((count == 1) && (single.Value.ValueKind == JsonValueKind.String))
                    return single.Value.GetString();
            }
            return r.GetRawText();
        }

        /// <summary>
        /// Copy a schema, OpenAPI style type names ("OBJECT", "STRING" etc) are converted to json schema type names ("object", "string" etc)
        /// </summary>
        static void CopySchema(Utf8JsonWriter w, JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    w.WriteStartObject();
                    foreach (var p in e.EnumerateObject())
                    {
                        w.WritePropertyName(p.Name);
                        if ((p.Name == "type") && (p.Value.ValueKind == JsonValueKind.String))
                            w.WriteStringValue(p.Value.GetString().ToLowerInvariant());
                        else
                            CopySchema(w, p.Value);
                    }
                    w.WriteEndObject();
                    return;
                case JsonValueKind.Array:
                    w.WriteStartArray();
                    foreach (var x in e.EnumerateArray())
                        CopySchema(w, x);
                    w.WriteEndArray();
                    return;
                default:
                    e.WriteTo(w);
                    return;
            }
        }

        static AiToolFunction ParseFunctionDeclaration(JsonElement fd, String param)
        {
            var name = GetStringG(fd, "name", param + ".name");
            if (String.IsNullOrEmpty(name))
                throw HostException.BadRequest("A function declaration must have a name", param + ".name");
            var desc = GetStringG(fd, "description", param + ".description");
            BinaryData parameters = null;
            if (TryGetG(fd, "parametersJsonSchema", out var js))
                parameters = BinaryData.FromString(js.GetRawText());
            else if (TryGetG(fd, "parameters", out var ps))
            {
                if (ps.ValueKind != JsonValueKind.Object)
                    throw HostException.BadRequest("The parameters of a function declaration must be a schema object", param + ".parameters");
                parameters = BinaryData.FromBytes(HostHttp.Json(w => CopySchema(w, ps)));
            }
            return new AiToolFunction(name, desc, parameters);
        }

        static AiReasoning? GetReasoning(JsonElement thinkingConfig)
        {
            if (TryGetG(thinkingConfig, "thinkingLevel", out var level) && (level.ValueKind == JsonValueKind.String))
                return level.GetString().ToUpperInvariant() switch
                {
                    "MINIMAL" => AiReasoning.Minimal,
                    "LOW" => AiReasoning.Low,
                    "MEDIUM" => AiReasoning.Medium,
                    "HIGH" => AiReasoning.High,
                    _ => null,
                };
            if (TryGetG(thinkingConfig, "thinkingBudget", out var budget) && budget.TryGetInt32(out var b))
            {
                //  Negative is dynamic (model default)
                if (b < 0)
                    return null;
                if (b == 0)
                    return AiReasoning.None;
                if (b <= 1024)
                    return AiReasoning.Low;
                if (b <= 8192)
                    return AiReasoning.Medium;
                return AiReasoning.High;
            }
            return null;
        }

        static AiCompletionRequest ParseGoogleRequest(JsonElement root, String model)
        {
            var req = new AiCompletionRequest
            {
                Model = model,
            };
            if (TryGetG(root, "systemInstruction", out var si))
                req.SystemPrompt = GetContentText(si, "systemInstruction");
            if ((!TryGetG(root, "contents", out var contents)) || (contents.ValueKind != JsonValueKind.Array))
                throw HostException.BadRequest("contents is required and must be an array", "contents");
            int index = 0;
            int callIndex = 0;
            foreach (var content in contents.EnumerateArray())
            {
                var param = String.Concat("contents[", (index++).ToString(), "]");
                var role = GetStringG(content, "role", param + ".role") ?? "user";
                var parts = GetParts(content, param);
                var texts = new List<String>();
                int pi = 0;
                switch (role)
                {
                    case "model":
                        {
                            var am = new AiCompletionMessage { Role = AiCompletionRoles.Assistant };
                            foreach (var part in parts.EnumerateArray())
                            {
                                var pp = String.Concat(param, ".parts[", (pi++).ToString(), "]");
                                ThrowIfMedia(part, pp);
                                if (TryGetG(part, "functionCall", out var fc))
                                {
                                    var name = GetStringG(fc, "name", pp + ".functionCall.name");
                                    (am.ToolCalls ??= new List<AiCompletionToolCall>()).Add(new AiCompletionToolCall
                                    {
                                        Id = GetStringG(fc, "id", pp + ".functionCall.id") ?? String.Concat("call_", (callIndex++).ToString()),
                                        Name = name,
                                        Arguments = TryGet(fc, "args", out var args) ? args.GetRawText() : "{}",
                                    });
                                    continue;
                                }
                                if (IsThought(part))
                                    continue;
                                var t = GetStringG(part, "text", pp + ".text");
                                if (t != null)
                                    texts.Add(t);
                            }
                            am.Text = String.Join("", texts);
                            req.Messages.Add(am);
                        }
                        break;
                    case "user":
                    case "function":
                    case "tool":
                        foreach (var part in parts.EnumerateArray())
                        {
                            var pp = String.Concat(param, ".parts[", (pi++).ToString(), "]");
                            ThrowIfMedia(part, pp);
                            if (TryGetG(part, "functionResponse", out var fr))
                            {
                                req.Messages.Add(new AiCompletionMessage
                                {
                                    Role = AiCompletionRoles.Tool,
                                    Name = GetStringG(fr, "name", pp + ".functionResponse.name"),
                                    ToolCallId = GetStringG(fr, "id", pp + ".functionResponse.id"),
                                    Text = GetFunctionResponseText(fr),
                                });
                                continue;
                            }
                            var t = GetStringG(part, "text", pp + ".text");
                            if (t != null)
                                texts.Add(t);
                        }
                        if (texts.Count > 0)
                            req.Messages.Add(new AiCompletionMessage { Role = AiCompletionRoles.User, Text = String.Join("\n", texts) });
                        break;
                    default:
                        throw HostException.BadRequest(String.Concat("Invalid role ", role.ToQuoted(), ", expected \"user\" or \"model\""), param + ".role");
                }
            }
            if (TryGetG(root, "tools", out var tools) && (tools.ValueKind == JsonValueKind.Array))
            {
                index = 0;
                foreach (var tool in tools.EnumerateArray())
                {
                    var param = String.Concat("tools[", (index++).ToString(), "]");
                    foreach (var p in tool.EnumerateObject())
                    {
                        if ((p.Name != "functionDeclarations") && (p.Name != "function_declarations"))
                        {
                            if ((p.Value.ValueKind == JsonValueKind.Null) || (p.Value.ValueKind == JsonValueKind.Undefined))
                                continue;
                            throw HostException.BadRequest(String.Concat("Tools of type ", p.Name.ToQuoted(), " are not supported, only function declarations are supported"), param + "." + p.Name);
                        }
                        if (p.Value.ValueKind != JsonValueKind.Array)
                            continue;
                        int fi = 0;
                        foreach (var fd in p.Value.EnumerateArray())
                            (req.Tools ??= new List<AiToolFunction>()).Add(ParseFunctionDeclaration(fd, String.Concat(param, ".functionDeclarations[", (fi++).ToString(), "]")));
                    }
                }
            }
            if (TryGetG(root, "toolConfig", out var tc) && TryGetG(tc, "functionCallingConfig", out var fcc))
            {
                var mode = GetStringG(fcc, "mode", "toolConfig.functionCallingConfig.mode");
                req.ToolChoice = mode?.ToUpperInvariant() switch
                {
                    "ANY" => AiCompletionToolChoices.Required,
                    "NONE" => AiCompletionToolChoices.None,
                    _ => AiCompletionToolChoices.Auto,
                };
            }
            if (TryGetG(root, "generationConfig", out var gc))
            {
                if (TryGetG(gc, "candidateCount", out var cc) && cc.TryGetInt32(out var ccv) && (ccv != 1))
                    throw HostException.BadRequest("Only candidateCount = 1 is supported", "generationConfig.candidateCount");
                if (TryGetG(gc, "temperature", out var temp))
                {
                    if ((temp.ValueKind != JsonValueKind.Number) || (!temp.TryGetSingle(out var tv)))
                        throw HostException.BadRequest("Expected temperature to be a number", "generationConfig.temperature");
                    req.Temperature = tv;
                }
                if (TryGetG(gc, "maxOutputTokens", out var mot))
                {
                    if ((mot.ValueKind != JsonValueKind.Number) || (!mot.TryGetInt32(out var mv)))
                        throw HostException.BadRequest("Expected maxOutputTokens to be an integer", "generationConfig.maxOutputTokens");
                    req.MaxTokens = mv;
                }
                if (TryGetG(gc, "thinkingConfig", out var think))
                    req.Reasoning = GetReasoning(think);
            }
            return req;
        }

        #endregion//Request parsing

        #region Response

        /// <summary>
        /// Write function call arguments (must be an object)
        /// </summary>
        static void WriteArgs(Utf8JsonWriter w, String args)
        {
            w.WritePropertyName("args");
            try
            {
                using var d = JsonDocument.Parse(String.IsNullOrWhiteSpace(args) ? "{}" : args);
                if (d.RootElement.ValueKind == JsonValueKind.Object)
                {
                    d.RootElement.WriteTo(w);
                    return;
                }
            }
            catch
            {
            }
            w.WriteStartObject();
            w.WriteEndObject();
        }

        /// <summary>
        /// Write a GenerateContentResponse
        /// </summary>
        /// <param name="w">The writer</param>
        /// <param name="responseId">The response id</param>
        /// <param name="model">The model</param>
        /// <param name="text">Text to include, can be null</param>
        /// <param name="res">The final result (function calls, finish reason and usage are included), null for partial responses</param>
        static void WriteGoogleResponse(Utf8JsonWriter w, String responseId, String model, String text, AiCompletionResult res)
        {
            w.WriteStartObject();
            w.WriteStartArray("candidates");
            w.WriteStartObject();
            w.WriteStartObject("content");
            w.WriteString("role", "model");
            w.WriteStartArray("parts");
            var calls = res?.ToolCalls;
            if ((text != null) && ((text.Length > 0) || (calls == null)))
            {
                w.WriteStartObject();
                w.WriteString("text", text);
                w.WriteEndObject();
            }
            if (calls != null)
                foreach (var c in calls)
                {
                    w.WriteStartObject();
                    w.WriteStartObject("functionCall");
                    w.WriteString("id", c.Id);
                    w.WriteString("name", c.Name);
                    WriteArgs(w, c.Arguments);
                    w.WriteEndObject();
                    w.WriteEndObject();
                }
            w.WriteEndArray();
            w.WriteEndObject();
            if (res != null)
                w.WriteString("finishReason", res.FinishReason == AiCompletionFinishReasons.Length ? "MAX_TOKENS" : "STOP");
            w.WriteNumber("index", 0);
            w.WriteEndObject();
            w.WriteEndArray();
            if (res != null)
            {
                w.WriteStartObject("usageMetadata");
                w.WriteNumber("promptTokenCount", res.InputTokens);
                w.WriteNumber("candidatesTokenCount", res.OutputTokens);
                w.WriteNumber("totalTokenCount", res.InputTokens + res.OutputTokens);
                w.WriteEndObject();
            }
            w.WriteString("modelVersion", model);
            w.WriteString("responseId", responseId);
            w.WriteEndObject();
        }

        static readonly Byte[] ArrayStart = Encoding.UTF8.GetBytes("[");
        static readonly Byte[] ArraySeparator = Encoding.UTF8.GetBytes(",\r\n");
        static readonly Byte[] ArrayEnd = Encoding.UTF8.GetBytes("]");

        async Task<HttpRequestData> GoogleGenerateContent(HttpServerRequest r, String model, bool stream)
        {
            using var _ = PerfMon.Track(nameof(GoogleGenerateContent));
            AiCompletionRequest req;
            IAiService service;
            try
            {
                using (var doc = await HostHttp.ReadJson(r, MaxRequestSize).ConfigureAwait(false))
                    req = ParseGoogleRequest(doc.RootElement, model);
                var (s, m) = await ResolveModel(model).ConfigureAwait(false);
                service = s;
                req.Model = m;
            }
            catch (Exception ex)
            {
                return GoogleErrorResponse(r, HostHttp.ToHostException(ex));
            }
            req.Cancel = r.GetRequestCancellationToken();
            var id = NewId();
            if (!stream)
            {
                AiCompletionResult res;
                try
                {
                    res = await service.Complete(req).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    return GoogleErrorResponse(r, HostHttp.ToHostException(ex));
                }
                return HostHttp.JsonResponse(r, 200, HostHttp.Json(w => WriteGoogleResponse(w, id, res.Model ?? req.Model, res.Text ?? "", res)));
            }
            //  "alt=sse" gives server sent events, else a json array (streamed)
            var sse = r.QueryParamsLowercase.TryGetValue("alt", out var alt) && String.Equals(alt, "sse", StringComparison.OrdinalIgnoreCase);
            bool first = true;
            async Task Write(SseWriter w, Byte[] json)
            {
                if (sse)
                {
                    await w.Data(json).ConfigureAwait(false);
                    return;
                }
                await w.Raw(first ? ArrayStart : ArraySeparator).ConfigureAwait(false);
                first = false;
                await w.Raw(json).ConfigureAwait(false);
            }
            return HostHttp.EventStream(r, async w =>
            {
                var sent = "";
                async Task SendText(String text)
                {
                    var d = GetDelta(sent, text);
                    if (d == null)
                        return;
                    sent = text;
                    await Write(w, HostHttp.Json(x => WriteGoogleResponse(x, id, req.Model, d, null))).ConfigureAwait(false);
                }
                var res = await service.Complete(req, SendText).ConfigureAwait(false);
                //  The last chunk contains any remaining text, the function calls, the finish reason and the usage
                var final = GetDelta(sent, res.Text) ?? "";
                await Write(w, HostHttp.Json(x => WriteGoogleResponse(x, id, res.Model ?? req.Model, final, res))).ConfigureAwait(false);
                if (!sse)
                    await w.Raw(ArrayEnd).ConfigureAwait(false);
            }, async (w, ex) =>
            {
                var he = HostHttp.ToHostException(ex);
                Msg?.AddMessage(LogPrefix + "Generate content failed", ex, MessageLevels.Warning);
                await Write(w, HostHttp.Json(x => WriteGoogleError(x, he))).ConfigureAwait(false);
                if (!sse)
                    await w.Raw(ArrayEnd).ConfigureAwait(false);
            }, sse ? HostHttp.EventStreamMime : HostHttp.JsonMime);
        }

        #endregion//Response
    }
}
