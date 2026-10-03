using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// The OpenAI Chat Completions API, see: https://platform.openai.com/docs/api-reference/chat
    /// </summary>
    public sealed partial class HostService
    {
        sealed class ChatRequest
        {
            public AiCompletionRequest Request;
            public bool Stream;
            public bool IncludeUsage;
        }

        static ChatRequest ParseChatRequest(JsonElement root)
        {
            var req = new AiCompletionRequest
            {
                Model = GetString(root, "model"),
            };
            var n = GetInt(root, "n");
            if ((n ?? 1) != 1)
                throw HostException.BadRequest("Only n = 1 is supported", "n");
            if ((!TryGet(root, "messages", out var messages)) || (messages.ValueKind != JsonValueKind.Array))
                throw HostException.BadRequest("Missing required parameter: 'messages'", "messages");
            String system = null;
            int index = 0;
            foreach (var m in messages.EnumerateArray())
            {
                var param = String.Concat("messages[", index.ToString(), "]");
                ++index;
                var role = GetString(m, "role", param + ".role");
                switch (role)
                {
                    case "system":
                    case "developer":
                        AddSystem(ref system, GetContentText(m, "content", param + ".content"));
                        break;
                    case "user":
                        req.Messages.Add(new AiCompletionMessage
                        {
                            Role = AiCompletionRoles.User,
                            Text = GetContentText(m, "content", param + ".content") ?? "",
                            Name = GetString(m, "name", param + ".name"),
                        });
                        break;
                    case "assistant":
                        {
                            var am = new AiCompletionMessage
                            {
                                Role = AiCompletionRoles.Assistant,
                                Text = GetContentText(m, "content", param + ".content"),
                            };
                            if (TryGet(m, "tool_calls", out var calls) && (calls.ValueKind == JsonValueKind.Array))
                            {
                                foreach (var c in calls.EnumerateArray())
                                {
                                    if (!TryGet(c, "function", out var f))
                                        continue;
                                    (am.ToolCalls ??= new List<AiCompletionToolCall>()).Add(new AiCompletionToolCall
                                    {
                                        Id = GetString(c, "id", param + ".tool_calls.id"),
                                        Name = GetString(f, "name", param + ".tool_calls.function.name"),
                                        Arguments = GetArguments(f, "arguments"),
                                    });
                                }
                            }
                            //  Legacy function call
                            else if (TryGet(m, "function_call", out var fc))
                            {
                                am.ToolCalls = [new AiCompletionToolCall
                                {
                                    Id = GetString(fc, "name", param + ".function_call.name"),
                                    Name = GetString(fc, "name", param + ".function_call.name"),
                                    Arguments = GetArguments(fc, "arguments"),
                                }];
                            }
                            req.Messages.Add(am);
                        }
                        break;
                    case "tool":
                        req.Messages.Add(new AiCompletionMessage
                        {
                            Role = AiCompletionRoles.Tool,
                            Text = GetContentText(m, "content", param + ".content") ?? "",
                            ToolCallId = GetString(m, "tool_call_id", param + ".tool_call_id"),
                        });
                        break;
                    case "function":
                        {
                            //  Legacy function result
                            var name = GetString(m, "name", param + ".name");
                            req.Messages.Add(new AiCompletionMessage
                            {
                                Role = AiCompletionRoles.Tool,
                                Text = GetContentText(m, "content", param + ".content") ?? "",
                                Name = name,
                                ToolCallId = name,
                            });
                        }
                        break;
                    default:
                        throw HostException.BadRequest(String.Concat("Invalid role ", (role ?? "null").ToQuoted()), param + ".role");
                }
            }
            req.SystemPrompt = system;
            if (TryGet(root, "tools", out var tools) && (tools.ValueKind == JsonValueKind.Array))
            {
                index = 0;
                foreach (var t in tools.EnumerateArray())
                {
                    var param = String.Concat("tools[", index.ToString(), "]");
                    ++index;
                    var type = GetString(t, "type", param + ".type") ?? "function";
                    if (type != "function")
                        throw HostException.BadRequest(String.Concat("Tools of type ", type.ToQuoted(), " are not supported"), param + ".type");
                    if (!TryGet(t, "function", out var f))
                        throw HostException.BadRequest("Missing function definition", param + ".function");
                    (req.Tools ??= new List<AiToolFunction>()).Add(ParseFunction(f, param + ".function"));
                }
            }
            //  Legacy functions
            else if (TryGet(root, "functions", out var functions) && (functions.ValueKind == JsonValueKind.Array))
            {
                index = 0;
                foreach (var f in functions.EnumerateArray())
                {
                    var param = String.Concat("functions[", index.ToString(), "]");
                    ++index;
                    (req.Tools ??= new List<AiToolFunction>()).Add(ParseFunction(f, param));
                }
            }
            req.ToolChoice = ParseToolChoice(root, "tool_choice");
            req.Temperature = GetFloat(root, "temperature");
            req.MaxTokens = GetInt(root, "max_completion_tokens") ?? GetInt(root, "max_tokens");
            req.Reasoning = ParseReasoning(GetString(root, "reasoning_effort"), "reasoning_effort");
            var includeUsage = false;
            if (TryGet(root, "stream_options", out var so))
                includeUsage = GetBool(so, "include_usage") ?? false;
            return new ChatRequest
            {
                Request = req,
                Stream = GetBool(root, "stream") ?? false,
                IncludeUsage = includeUsage,
            };
        }

        static void WriteToolCalls(Utf8JsonWriter w, List<AiCompletionToolCall> calls, bool withIndex)
        {
            w.WriteStartArray("tool_calls");
            for (int i = 0; i < calls.Count; ++i)
            {
                var c = calls[i];
                w.WriteStartObject();
                if (withIndex)
                    w.WriteNumber("index", i);
                w.WriteString("id", c.Id);
                w.WriteString("type", "function");
                w.WriteStartObject("function");
                w.WriteString("name", c.Name);
                w.WriteString("arguments", c.Arguments ?? "{}");
                w.WriteEndObject();
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }

        static void WriteChatUsage(Utf8JsonWriter w, AiCompletionResult res)
        {
            w.WriteStartObject("usage");
            w.WriteNumber("prompt_tokens", res.InputTokens);
            w.WriteNumber("completion_tokens", res.OutputTokens);
            w.WriteNumber("total_tokens", res.InputTokens + res.OutputTokens);
            w.WriteEndObject();
        }

        static void WriteChunkStart(Utf8JsonWriter w, String id, long created, String model)
        {
            w.WriteStartObject();
            w.WriteString("id", id);
            w.WriteString("object", "chat.completion.chunk");
            w.WriteNumber("created", created);
            w.WriteString("model", model);
        }

        /// <summary>
        /// Create a chunk with a single choice
        /// </summary>
        static Byte[] Chunk(String id, long created, String model, Action<Utf8JsonWriter> writeDelta, String finishReason)
            => HostHttp.Json(w =>
            {
                WriteChunkStart(w, id, created, model);
                w.WriteStartArray("choices");
                w.WriteStartObject();
                w.WriteNumber("index", 0);
                w.WriteStartObject("delta");
                writeDelta?.Invoke(w);
                w.WriteEndObject();
                w.WriteNull("logprobs");
                HostHttp.WriteStringOrNull(w, "finish_reason", finishReason);
                w.WriteEndObject();
                w.WriteEndArray();
                w.WriteEndObject();
            });

        async Task<HttpRequestData> ChatCompletions(HttpServerRequest r)
        {
            using var _ = PerfMon.Track(nameof(ChatCompletions));
            ChatRequest cr;
            IAiService service;
            try
            {
                using (var doc = await HostHttp.ReadJson(r, MaxRequestSize).ConfigureAwait(false))
                    cr = ParseChatRequest(doc.RootElement);
                var (s, m) = await ResolveModel(cr.Request.Model).ConfigureAwait(false);
                service = s;
                cr.Request.Model = m;
            }
            catch (Exception ex)
            {
                return HostHttp.ErrorResponse(r, HostHttp.ToHostException(ex));
            }
            var req = cr.Request;
            req.Cancel = r.GetRequestCancellationToken();
            var id = "chatcmpl-" + NewId();
            var created = UnixNow();
            if (!cr.Stream)
            {
                AiCompletionResult res;
                try
                {
                    res = await service.Complete(req).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    return HostHttp.ErrorResponse(r, HostHttp.ToHostException(ex));
                }
                var model = res.Model ?? req.Model;
                return HostHttp.JsonResponse(r, 200, HostHttp.Json(w =>
                {
                    w.WriteStartObject();
                    w.WriteString("id", id);
                    w.WriteString("object", "chat.completion");
                    w.WriteNumber("created", created);
                    w.WriteString("model", model);
                    w.WriteStartArray("choices");
                    w.WriteStartObject();
                    w.WriteNumber("index", 0);
                    w.WriteStartObject("message");
                    w.WriteString("role", "assistant");
                    var text = res.Text;
                    var haveCalls = (res.ToolCalls?.Count ?? 0) > 0;
                    if (String.IsNullOrEmpty(text) && haveCalls)
                        w.WriteNull("content");
                    else
                        w.WriteString("content", text ?? "");
                    w.WriteNull("refusal");
                    if (haveCalls)
                        WriteToolCalls(w, res.ToolCalls, false);
                    w.WriteEndObject();
                    w.WriteNull("logprobs");
                    w.WriteString("finish_reason", GetFinishReason(res.FinishReason));
                    w.WriteEndObject();
                    w.WriteEndArray();
                    WriteChatUsage(w, res);
                    w.WriteEndObject();
                }));
            }
            var includeUsage = cr.IncludeUsage;
            return HostHttp.EventStream(r, async sse =>
            {
                var model = req.Model;
                await sse.Data(Chunk(id, created, model, w =>
                {
                    w.WriteString("role", "assistant");
                    w.WriteString("content", "");
                }, null)).ConfigureAwait(false);
                var sent = "";
                async Task SendText(String text)
                {
                    var d = GetDelta(sent, text);
                    if (d == null)
                        return;
                    sent = text;
                    await sse.Data(Chunk(id, created, model, w => w.WriteString("content", d), null)).ConfigureAwait(false);
                }
                var res = await service.Complete(req, SendText).ConfigureAwait(false);
                await SendText(res.Text).ConfigureAwait(false);
                if ((res.ToolCalls?.Count ?? 0) > 0)
                    await sse.Data(Chunk(id, created, model, w => WriteToolCalls(w, res.ToolCalls, true), null)).ConfigureAwait(false);
                await sse.Data(Chunk(id, created, model, null, GetFinishReason(res.FinishReason))).ConfigureAwait(false);
                if (includeUsage)
                    await sse.Data(HostHttp.Json(w =>
                    {
                        WriteChunkStart(w, id, created, model);
                        w.WriteStartArray("choices");
                        w.WriteEndArray();
                        WriteChatUsage(w, res);
                        w.WriteEndObject();
                    })).ConfigureAwait(false);
                await sse.Done().ConfigureAwait(false);
            }, async (sse, ex) =>
            {
                var he = HostHttp.ToHostException(ex);
                Msg?.AddMessage(LogPrefix + "Chat completion failed", ex, MessageLevels.Warning);
                await sse.Data(HostHttp.Json(w =>
                {
                    w.WriteStartObject();
                    HostHttp.WriteError(w, he.Message, he.Type, he.Code, he.Param);
                    w.WriteEndObject();
                })).ConfigureAwait(false);
                await sse.Done().ConfigureAwait(false);
            });
        }
    }
}
