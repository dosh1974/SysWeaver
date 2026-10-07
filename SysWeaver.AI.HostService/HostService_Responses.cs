using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// The OpenAI Responses API (stateless, previous_response_id isn't supported), see: https://platform.openai.com/docs/api-reference/responses
    /// </summary>
    public sealed partial class HostService
    {
        sealed class ResponsesRequest
        {
            public AiCompletionRequest Request;
            public bool Stream;
            public String Instructions;
            public String ToolChoice;
        }

        static AiCompletionRoles? GetMessageRole(String role, String param)
            => role switch
            {
                "user" => AiCompletionRoles.User,
                "assistant" => AiCompletionRoles.Assistant,
                "system" => null,
                "developer" => null,
                _ => throw HostException.BadRequest(String.Concat("Invalid role ", (role ?? "null").ToQuoted()), param),
            };

        static ResponsesRequest ParseResponsesRequest(JsonElement root)
        {
            if (TryGet(root, "previous_response_id", out _))
                throw HostException.BadRequest("previous_response_id is not supported, send the whole conversation as input", "previous_response_id");
            if (TryGet(root, "conversation", out _))
                throw HostException.BadRequest("conversation is not supported, send the whole conversation as input", "conversation");
            var req = new AiCompletionRequest
            {
                Model = GetString(root, "model"),
            };
            var instructions = GetString(root, "instructions");
            String system = instructions;
            if (!TryGet(root, "input", out var input))
                throw HostException.BadRequest("Missing required parameter: 'input'", "input");
            if (input.ValueKind == JsonValueKind.String)
            {
                req.Messages.Add(new AiCompletionMessage { Role = AiCompletionRoles.User, Text = input.GetString() });
            }
            else if (input.ValueKind == JsonValueKind.Array)
            {
                var messages = req.Messages;
                int index = 0;
                foreach (var item in input.EnumerateArray())
                {
                    var param = String.Concat("input[", index.ToString(), "]");
                    ++index;
                    var type = GetString(item, "type", param + ".type") ?? "message";
                    switch (type)
                    {
                        case "message":
                            {
                                var role = GetMessageRole(GetString(item, "role", param + ".role"), param + ".role");
                                var text = GetContentText(item, "content", param + ".content");
                                if (role == null)
                                {
                                    AddSystem(ref system, text);
                                    break;
                                }
                                messages.Add(new AiCompletionMessage { Role = role.Value, Text = text ?? "" });
                            }
                            break;
                        case "function_call":
                            {
                                var call = new AiCompletionToolCall
                                {
                                    Id = GetString(item, "call_id", param + ".call_id"),
                                    Name = GetString(item, "name", param + ".name"),
                                    Arguments = GetArguments(item, "arguments"),
                                };
                                //  Calls following an assistant message (or other calls) belong to the same message
                                var last = messages.Count > 0 ? messages[messages.Count - 1] : null;
                                if ((last == null) || (last.Role != AiCompletionRoles.Assistant))
                                {
                                    last = new AiCompletionMessage { Role = AiCompletionRoles.Assistant };
                                    messages.Add(last);
                                }
                                (last.ToolCalls ??= new List<AiCompletionToolCall>()).Add(call);
                            }
                            break;
                        case "function_call_output":
                            {
                                String output;
                                if (TryGet(item, "output", out var o) && (o.ValueKind == JsonValueKind.Array))
                                    output = GetContentText(item, "output", param + ".output");
                                else
                                    output = GetString(item, "output", param + ".output");
                                messages.Add(new AiCompletionMessage
                                {
                                    Role = AiCompletionRoles.Tool,
                                    Text = output ?? "",
                                    ToolCallId = GetString(item, "call_id", param + ".call_id"),
                                });
                            }
                            break;
                        //  Reasoning from a previous response can't be used by other models
                        case "reasoning":
                            break;
                        default:
                            throw HostException.BadRequest(String.Concat("Input items of type ", type.ToQuoted(), " are not supported"), param + ".type");
                    }
                }
            }
            else
                throw HostException.BadRequest("Expected input to be a string or an array of input items", "input");
            req.SystemPrompt = system;
            if (TryGet(root, "tools", out var tools) && (tools.ValueKind == JsonValueKind.Array))
            {
                int index = 0;
                foreach (var t in tools.EnumerateArray())
                {
                    var param = String.Concat("tools[", index.ToString(), "]");
                    ++index;
                    var type = GetString(t, "type", param + ".type");
                    if (type != "function")
                        throw HostException.BadRequest(String.Concat("Tools of type ", (type ?? "null").ToQuoted(), " are not supported, only function tools are supported"), param + ".type");
                    (req.Tools ??= new List<AiToolFunction>()).Add(ParseFunction(t, param));
                }
            }
            req.ToolChoice = ParseToolChoice(root, "tool_choice");
            req.Temperature = GetFloat(root, "temperature");
            req.MaxTokens = GetInt(root, "max_output_tokens");
            if (TryGet(root, "reasoning", out var reasoning))
                req.Reasoning = ParseReasoning(GetString(reasoning, "effort", "reasoning.effort"), "reasoning.effort");
            return new ResponsesRequest
            {
                Request = req,
                Stream = GetBool(root, "stream") ?? false,
                Instructions = instructions,
                ToolChoice = req.ToolChoice switch
                {
                    AiCompletionToolChoices.None => "none",
                    AiCompletionToolChoices.Required => "required",
                    _ => "auto",
                },
            };
        }

        /// <summary>
        /// The state of a response (used to write the response object)
        /// </summary>
        sealed class ResponseState
        {
            public String Id;
            public long Created;
            public String Model;
            public ResponsesRequest Req;
            public String MessageId;
            public String Text;
            public List<ValueTuple<String, AiCompletionToolCall>> Calls = new();
            public AiCompletionResult Result;
            public String Status = "in_progress";
        }

        static void WriteMessageItem(Utf8JsonWriter w, String id, String text, String status)
        {
            w.WriteStartObject();
            w.WriteString("id", id);
            w.WriteString("type", "message");
            w.WriteString("status", status);
            w.WriteString("role", "assistant");
            w.WriteStartArray("content");
            if (text != null)
                WriteOutputText(w, text);
            w.WriteEndArray();
            w.WriteEndObject();
        }

        static void WriteOutputText(Utf8JsonWriter w, String text)
        {
            w.WriteStartObject();
            w.WriteString("type", "output_text");
            w.WriteString("text", text);
            w.WriteStartArray("annotations");
            w.WriteEndArray();
            w.WriteStartArray("logprobs");
            w.WriteEndArray();
            w.WriteEndObject();
        }

        static void WriteFunctionCallItem(Utf8JsonWriter w, String id, AiCompletionToolCall c, String arguments, String status)
        {
            w.WriteStartObject();
            w.WriteString("id", id);
            w.WriteString("type", "function_call");
            w.WriteString("status", status);
            w.WriteString("call_id", c.Id);
            w.WriteString("name", c.Name);
            w.WriteString("arguments", arguments);
            w.WriteEndObject();
        }

        static void WriteResponse(Utf8JsonWriter w, ResponseState s, String propertyName = null)
        {
            if (propertyName == null)
                w.WriteStartObject();
            else
                w.WriteStartObject(propertyName);
            var req = s.Req;
            var r = req.Request;
            var res = s.Result;
            w.WriteString("id", s.Id);
            w.WriteString("object", "response");
            w.WriteNumber("created_at", s.Created);
            w.WriteString("status", s.Status);
            w.WriteBoolean("background", false);
            w.WriteNull("error");
            if (s.Status == "incomplete")
            {
                w.WriteStartObject("incomplete_details");
                w.WriteString("reason", "max_output_tokens");
                w.WriteEndObject();
            }
            else
                w.WriteNull("incomplete_details");
            HostHttp.WriteStringOrNull(w, "instructions", req.Instructions);
            if (r.MaxTokens.HasValue)
                w.WriteNumber("max_output_tokens", r.MaxTokens.Value);
            else
                w.WriteNull("max_output_tokens");
            w.WriteString("model", s.Model);
            w.WriteStartArray("output");
            if (s.MessageId != null)
                WriteMessageItem(w, s.MessageId, s.Text ?? "", res == null ? "in_progress" : "completed");
            foreach (var (fid, c) in s.Calls)
                WriteFunctionCallItem(w, fid, c, c.Arguments ?? "{}", "completed");
            w.WriteEndArray();
            w.WriteBoolean("parallel_tool_calls", true);
            w.WriteNull("previous_response_id");
            w.WriteStartObject("reasoning");
            w.WriteNull("effort");
            w.WriteNull("summary");
            w.WriteEndObject();
            w.WriteBoolean("store", false);
            if (r.Temperature.HasValue)
                w.WriteNumber("temperature", r.Temperature.Value);
            else
                w.WriteNumber("temperature", 1.0);
            w.WriteStartObject("text");
            w.WriteStartObject("format");
            w.WriteString("type", "text");
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteString("tool_choice", req.ToolChoice);
            w.WriteStartArray("tools");
            if (r.Tools != null)
                foreach (var t in r.Tools)
                {
                    w.WriteStartObject();
                    w.WriteString("type", "function");
                    w.WriteString("name", t.FunctionName);
                    HostHttp.WriteStringOrNull(w, "description", t.FunctionDescription);
                    w.WritePropertyName("parameters");
                    if (t.FunctionParameters == null)
                        w.WriteNullValue();
                    else
                        w.WriteRawValue(t.FunctionParameters.ToString());
                    w.WriteBoolean("strict", false);
                    w.WriteEndObject();
                }
            w.WriteEndArray();
            w.WriteNumber("top_p", 1.0);
            w.WriteString("truncation", "disabled");
            if (res == null)
                w.WriteNull("usage");
            else
            {
                w.WriteStartObject("usage");
                w.WriteNumber("input_tokens", res.InputTokens);
                w.WriteStartObject("input_tokens_details");
                w.WriteNumber("cached_tokens", 0);
                w.WriteEndObject();
                w.WriteNumber("output_tokens", res.OutputTokens);
                w.WriteStartObject("output_tokens_details");
                w.WriteNumber("reasoning_tokens", 0);
                w.WriteEndObject();
                w.WriteNumber("total_tokens", res.InputTokens + res.OutputTokens);
                w.WriteEndObject();
            }
            w.WriteNull("user");
            w.WriteStartObject("metadata");
            w.WriteEndObject();
            w.WriteEndObject();
        }

        /// <summary>
        /// Set the final state from a result
        /// </summary>
        static void SetResult(ResponseState s, AiCompletionResult res)
        {
            s.Result = res;
            s.Status = res.FinishReason == AiCompletionFinishReasons.Length ? "incomplete" : "completed";
            if (res.ToolCalls != null)
                foreach (var c in res.ToolCalls)
                    s.Calls.Add(("fc_" + NewId(), c));
        }

        async Task<HttpRequestData> Responses(HttpServerRequest r)
        {
            using var _ = PerfMon.Track(nameof(Responses));
            ResponsesRequest rr;
            IAiService service;
            try
            {
                using (var doc = await HostHttp.ReadJson(r, MaxRequestSize).ConfigureAwait(false))
                    rr = ParseResponsesRequest(doc.RootElement);
                var (sv, m) = await ResolveModel(rr.Request.Model).ConfigureAwait(false);
                service = sv;
                rr.Request.Model = m;
            }
            catch (Exception ex)
            {
                return HostHttp.ErrorResponse(r, HostHttp.ToHostException(ex));
            }
            var req = rr.Request;
            req.Cancel = r.GetRequestCancellationToken();
            var s = new ResponseState
            {
                Id = "resp_" + NewId(),
                Created = UnixNow(),
                Model = req.Model,
                Req = rr,
            };
            if (!rr.Stream)
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
                s.Model = res.Model ?? s.Model;
                if (!String.IsNullOrEmpty(res.Text))
                {
                    s.MessageId = "msg_" + NewId();
                    s.Text = res.Text;
                }
                SetResult(s, res);
                return HostHttp.JsonResponse(r, 200, HostHttp.Json(w => WriteResponse(w, s)));
            }
            int seq = 0;
            Task Send(SseWriter sse, String type, Action<Utf8JsonWriter> write)
                => sse.Event(type, HostHttp.Json(w =>
                {
                    w.WriteStartObject();
                    w.WriteString("type", type);
                    w.WriteNumber("sequence_number", seq++);
                    write(w);
                    w.WriteEndObject();
                }));
            return HostHttp.EventStream(r, async sse =>
            {
                await Send(sse, "response.created", w => WriteResponse(w, s, "response")).ConfigureAwait(false);
                await Send(sse, "response.in_progress", w => WriteResponse(w, s, "response")).ConfigureAwait(false);
                var sent = "";
                async Task SendText(String text)
                {
                    var d = GetDelta(sent, text);
                    if (d == null)
                        return;
                    if (s.MessageId == null)
                    {
                        s.MessageId = "msg_" + NewId();
                        await Send(sse, "response.output_item.added", w =>
                        {
                            w.WriteNumber("output_index", 0);
                            w.WritePropertyName("item");
                            WriteMessageItem(w, s.MessageId, null, "in_progress");
                        }).ConfigureAwait(false);
                        await Send(sse, "response.content_part.added", w =>
                        {
                            w.WriteString("item_id", s.MessageId);
                            w.WriteNumber("output_index", 0);
                            w.WriteNumber("content_index", 0);
                            w.WritePropertyName("part");
                            WriteOutputText(w, "");
                        }).ConfigureAwait(false);
                    }
                    sent = text;
                    s.Text = text;
                    await Send(sse, "response.output_text.delta", w =>
                    {
                        w.WriteString("item_id", s.MessageId);
                        w.WriteNumber("output_index", 0);
                        w.WriteNumber("content_index", 0);
                        w.WriteString("delta", d);
                        w.WriteStartArray("logprobs");
                        w.WriteEndArray();
                    }).ConfigureAwait(false);
                }
                var res = await service.Complete(req, SendText).ConfigureAwait(false);
                await SendText(res.Text).ConfigureAwait(false);
                s.Model = res.Model ?? s.Model;
                int outputIndex = 0;
                if (s.MessageId != null)
                {
                    var text = s.Text;
                    await Send(sse, "response.output_text.done", w =>
                    {
                        w.WriteString("item_id", s.MessageId);
                        w.WriteNumber("output_index", 0);
                        w.WriteNumber("content_index", 0);
                        w.WriteString("text", text);
                        w.WriteStartArray("logprobs");
                        w.WriteEndArray();
                    }).ConfigureAwait(false);
                    await Send(sse, "response.content_part.done", w =>
                    {
                        w.WriteString("item_id", s.MessageId);
                        w.WriteNumber("output_index", 0);
                        w.WriteNumber("content_index", 0);
                        w.WritePropertyName("part");
                        WriteOutputText(w, text);
                    }).ConfigureAwait(false);
                    await Send(sse, "response.output_item.done", w =>
                    {
                        w.WriteNumber("output_index", 0);
                        w.WritePropertyName("item");
                        WriteMessageItem(w, s.MessageId, text, "completed");
                    }).ConfigureAwait(false);
                    ++outputIndex;
                }
                SetResult(s, res);
                foreach (var (fid, c) in s.Calls)
                {
                    var oi = outputIndex++;
                    var args = c.Arguments ?? "{}";
                    await Send(sse, "response.output_item.added", w =>
                    {
                        w.WriteNumber("output_index", oi);
                        w.WritePropertyName("item");
                        WriteFunctionCallItem(w, fid, c, "", "in_progress");
                    }).ConfigureAwait(false);
                    await Send(sse, "response.function_call_arguments.delta", w =>
                    {
                        w.WriteString("item_id", fid);
                        w.WriteNumber("output_index", oi);
                        w.WriteString("delta", args);
                    }).ConfigureAwait(false);
                    await Send(sse, "response.function_call_arguments.done", w =>
                    {
                        w.WriteString("item_id", fid);
                        w.WriteNumber("output_index", oi);
                        w.WriteString("name", c.Name);
                        w.WriteString("arguments", args);
                    }).ConfigureAwait(false);
                    await Send(sse, "response.output_item.done", w =>
                    {
                        w.WriteNumber("output_index", oi);
                        w.WritePropertyName("item");
                        WriteFunctionCallItem(w, fid, c, args, "completed");
                    }).ConfigureAwait(false);
                }
                await Send(sse, s.Status == "incomplete" ? "response.incomplete" : "response.completed", w => WriteResponse(w, s, "response")).ConfigureAwait(false);
            }, async (sse, ex) =>
            {
                var he = HostHttp.ToHostException(ex);
                Msg?.AddMessage(LogPrefix + "Response failed", ex, MessageLevels.Warning);
                await Send(sse, "error", w =>
                {
                    HostHttp.WriteStringOrNull(w, "code", he.Code ?? he.Type);
                    w.WriteString("message", he.SafeMessage());
                    HostHttp.WriteStringOrNull(w, "param", he.Param);
                }).ConfigureAwait(false);
            });
        }
    }
}
