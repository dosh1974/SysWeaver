using Google.GenAI.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    public sealed partial class GoogleAiService
    {
        /// <summary>
        /// Complete a conversation (stateless, the whole conversation is in the request).
        /// Tools in the request are defined by the caller, tool calls are returned (not executed).
        /// Attachments (images, files) of tool results are sent as multimodal function responses (Gemini 3+), or as parts after the function responses (older models).
        /// Return the Signature of tool calls unchanged in the next request (thought signatures are required by Gemini 3 when continuing after a tool call).
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="onText">Optional callback with the accumulated text response whenever it changes (streaming is used if supplied)</param>
        /// <returns>The result</returns>
        public override async Task<AiCompletionResult> Complete(AiCompletionRequest request, Func<String, Task> onText = null)
        {
            using var _a = PerfMon.Track(nameof(Complete));
            var model = String.IsNullOrEmpty(request.Model) ? DefaultChatModel : request.Model;
            var o = GoogleModels.GetOptions(model);
            var rt = request.Tools;
            var tools = (rt?.Count ?? 0) > 0 ? rt.Select(AiTool.CreateExternal).ToArray() : null;
            var sp = request.SystemPrompt;
            var temp = request.Temperature;
            var config = GoogleModels.BuildConfig(tools, o.Temp && temp.HasValue, temp ?? 1.0f,
                GoogleModels.GetThinking(o.Thinking, request.Reasoning ?? DefaultReasoning),
                GoogleModels.GetTier(DefaultTier),
                o.System ? sp : null);
            if (tools != null)
                config.ToolConfig = new ToolConfig
                {
                    FunctionCallingConfig = new FunctionCallingConfig
                    {
                        Mode = request.ToolChoice switch
                        {
                            AiCompletionToolChoices.None => FunctionCallingConfigMode.None,
                            AiCompletionToolChoices.Required => FunctionCallingConfigMode.Any,
                            _ => FunctionCallingConfigMode.Auto,
                        },
                    },
                };
            if (request.MaxTokens.HasValue)
                config.MaxOutputTokens = request.MaxTokens.Value;
            var contents = GetContents(request, model, o.System ? null : sp);
            var client = Client;
            var cancel = request.Cancel;
            var parts = new List<Part>();
            var text = new StringBuilder();
            Candidate last = null;
            GenerateContentResponseUsageMetadata usage = null;
            String blocked = null;
            String resModel = null;
            void Process(GenerateContentResponse r)
            {
                usage = r.UsageMetadata ?? usage;
                resModel ??= r.ModelVersion;
                var br = r.PromptFeedback?.BlockReason;
                if (br != null)
                    blocked = String.Concat(br.Value.ToString(), " ", r.PromptFeedback.BlockReasonMessage).Trim();
                var c = r.Candidates?.FirstOrDefault();
                if (c == null)
                    return;
                last = c;
                var cp = c.Content?.Parts;
                if (cp != null)
                    parts.AddRange(cp);
            }
            using (var _ = await (ChatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
            {
                if (onText == null)
                {
                    using var _m = PerfMon.Track(nameof(client.Models.GenerateContentAsync));
                    Process(await client.Models.GenerateContentAsync(model, contents, config, cancel).ConfigureAwait(false));
                    foreach (var p in parts)
                        if (IsText(p))
                            text.Append(p.Text);
                }
                else
                {
                    using var _m = PerfMon.Track(nameof(client.Models.GenerateContentStreamAsync));
                    await foreach (var r in client.Models.GenerateContentStreamAsync(model, contents, config, cancel).ConfigureAwait(false))
                    {
                        var before = parts.Count;
                        Process(r);
                        bool changed = false;
                        for (int i = before; i < parts.Count; ++i)
                        {
                            var p = parts[i];
                            if (!IsText(p))
                                continue;
                            text.Append(p.Text);
                            changed = true;
                        }
                        if (changed)
                            await onText(text.ToString()).ConfigureAwait(false);
                    }
                }
            }
            if (blocked != null)
                throw new AiCompletionException("Model refused to complete: " + blocked, "content_filter");
            List<AiCompletionToolCall> toolCalls = null;
            foreach (var p in parts)
            {
                var fc = p.FunctionCall;
                if (fc == null)
                    continue;
                var sig = p.ThoughtSignature;
                (toolCalls ??= new List<AiCompletionToolCall>()).Add(new AiCompletionToolCall
                {
                    //  The Gemini API doesn't always return an id
                    Id = String.IsNullOrEmpty(fc.Id) ? (GeneratedIdPrefix + Guid.NewGuid().ToString("N").Substring(0, 24)) : fc.Id,
                    Name = fc.Name,
                    Arguments = JsonSerializer.Serialize(fc.Args ?? new Dictionary<String, Object>()),
                    Signature = (sig?.Length ?? 0) > 0 ? Convert.ToBase64String(sig) : null,
                });
            }
            bool isTruncated = false;
            var fr = last?.FinishReason;
            if ((fr != null) && (toolCalls == null))
            {
                var v = fr.Value.Value;
                if (v == FinishReason.MaxTokens.Value)
                    isTruncated = true;
                else
                {
                    var err = GoogleModels.GetFinishError(last);
                    if (err != null)
                    {
                        if (err.StartsWith(GoogleModels.ContentFilterError, StringComparison.Ordinal))
                            throw new AiCompletionException(err.Substring(7), "content_filter");
                        throw new Exception(err.Substring(7));
                    }
                }
            }
            long tin = 0;
            long tout = 0;
            long tcached = 0;
            if (usage != null)
            {
                tin = (usage.PromptTokenCount ?? 0) + (usage.ToolUsePromptTokenCount ?? 0);
                tout = (usage.CandidatesTokenCount ?? 0) + (usage.ThoughtsTokenCount ?? 0);
                tcached = usage.CachedContentTokenCount ?? 0;
            }
            return new AiCompletionResult
            {
                Model = String.IsNullOrEmpty(resModel) ? model : resModel,
                Text = text.ToString(),
                ToolCalls = toolCalls,
                FinishReason = toolCalls != null ? AiCompletionFinishReasons.ToolCalls : (isTruncated ? AiCompletionFinishReasons.Length : AiCompletionFinishReasons.Stop),
                InputTokens = tin,
                CachedTokens = tcached,
                OutputTokens = tout,
            };
        }

        #region Completion helpers

        /// <summary>
        /// Prefix of tool call ids generated by this service (when the API doesn't supply an id), these are not sent back to the API
        /// </summary>
        const String GeneratedIdPrefix = "sw_call_";

        /// <summary>
        /// A dummy thought signature, used for function calls that have no signature (ex: created by the caller or by another model).
        /// See: https://ai.google.dev/gemini-api/docs/thought-signatures
        /// </summary>
        static readonly Byte[] DummySignature = Convert.FromBase64String("skip/thought/signature/validator");

        static bool IsText(Part p)
            => (!(p.Thought ?? false)) && (!String.IsNullOrEmpty(p.Text));

        /// <summary>
        /// True for older models (Gemini 1, Gemini 2 and Gemma) that doesn't support multimodal function responses or thought signatures
        /// </summary>
        static bool IsLegacyModel(String model)
            => model.StartsWith("gemini-1", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("gemini-2", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("gemma", StringComparison.OrdinalIgnoreCase);

        static Dictionary<String, Object> ParseArgs(String json)
        {
            if (String.IsNullOrWhiteSpace(json))
                return new Dictionary<String, Object>();
            try
            {
                return JsonSerializer.Deserialize<Dictionary<String, Object>>(json) ?? new Dictionary<String, Object>();
            }
            catch (JsonException ex)
            {
                throw new AiCompletionException("The arguments of a tool call must be a json object: " + ex.Message, "invalid_request");
            }
        }

        static FunctionResponsePart ToFunctionResponsePart(AiContentPart x)
        {
            if (x.Uri != null)
                return new FunctionResponsePart { FileData = new FunctionResponseFileData { FileUri = x.Uri.ToString(), MimeType = x.MimeType, DisplayName = x.Filename } };
            return new FunctionResponsePart { InlineData = new FunctionResponseBlob { Data = x.Data.ToArray(), MimeType = x.MimeType, DisplayName = x.Filename } };
        }

        /// <summary>
        /// Convert a completion request to Google contents.
        /// Assistant messages are "model" contents (text and function call parts), tool results are "user" contents with function response parts (consecutive tool results are merged).
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="model">The model</param>
        /// <param name="systemPrefix">The system prompt for models that doesn't support system instructions (added as the first user message), else null</param>
        static List<Content> GetContents(AiCompletionRequest request, String model, String systemPrefix)
        {
            var legacy = IsLegacyModel(model);
            var contents = new List<Content>(request.Messages.Count + 1);
            if (!String.IsNullOrEmpty(systemPrefix))
                contents.Add(GoogleModels.CreateTextContent(GoogleModels.RoleUser, systemPrefix));
            //  Tool call id => function name (to name function responses)
            var callNames = new Dictionary<String, String>(StringComparer.Ordinal);
            //  Function responses of consecutive tool messages, and attachments to add after them (legacy models)
            List<Part> responses = null;
            List<Part> extra = null;
            void Flush()
            {
                if (responses == null)
                    return;
                if (extra != null)
                    responses.AddRange(extra);
                contents.Add(new Content { Role = GoogleModels.RoleUser, Parts = responses });
                responses = null;
                extra = null;
            }
            foreach (var m in request.Messages)
            {
                if (m.Role != AiCompletionRoles.Tool)
                    Flush();
                var mp = m.Parts;
                switch (m.Role)
                {
                    case AiCompletionRoles.User:
                        var up = new List<Part>(2 + (mp?.Count ?? 0));
                        //  The API have no participant name, so add it as a separate text part
                        if (!String.IsNullOrEmpty(m.Name))
                            up.Add(new Part { Text = "[Message from: " + m.Name + "]" });
                        if (!String.IsNullOrEmpty(m.Text))
                            up.Add(new Part { Text = m.Text });
                        if (mp != null)
                            foreach (var x in mp)
                                up.Add(GoogleModels.ToPart(x));
                        if (up.Count <= 0)
                            up.Add(new Part { Text = "" });
                        contents.Add(new Content { Role = GoogleModels.RoleUser, Parts = up });
                        break;
                    case AiCompletionRoles.Assistant:
                        var ap = new List<Part>(1 + (m.ToolCalls?.Count ?? 0));
                        if (!String.IsNullOrEmpty(m.Text))
                            ap.Add(new Part { Text = m.Text });
                        var tcs = m.ToolCalls;
                        if (tcs != null)
                        {
                            bool haveSignature = false;
                            Part first = null;
                            foreach (var x in tcs)
                            {
                                var id = x.Id;
                                if (!String.IsNullOrEmpty(id))
                                    callNames[id] = x.Name;
                                var p = new Part
                                {
                                    FunctionCall = new FunctionCall
                                    {
                                        Id = (String.IsNullOrEmpty(id) || id.StartsWith(GeneratedIdPrefix, StringComparison.Ordinal)) ? null : id,
                                        Name = x.Name,
                                        Args = ParseArgs(x.Arguments),
                                    },
                                };
                                if (!String.IsNullOrEmpty(x.Signature))
                                {
                                    p.ThoughtSignature = Convert.FromBase64String(x.Signature);
                                    haveSignature = true;
                                }
                                first ??= p;
                                ap.Add(p);
                            }
                            //  Gemini 3 requires a thought signature on the first function call of a step
                            if ((!haveSignature) && (!legacy) && (first != null))
                                first.ThoughtSignature = DummySignature;
                        }
                        if (ap.Count <= 0)
                            ap.Add(new Part { Text = "" });
                        contents.Add(new Content { Role = GoogleModels.RoleModel, Parts = ap });
                        break;
                    case AiCompletionRoles.Tool:
                        var name = m.Name;
                        var tid = m.ToolCallId;
                        if (String.IsNullOrEmpty(name) && (!String.IsNullOrEmpty(tid)))
                            callNames.TryGetValue(tid, out name);
                        if (String.IsNullOrEmpty(name))
                            throw new AiCompletionException("The tool (function) name of a tool message is unknown, set the Name or a ToolCallId matching a previous tool call", "invalid_request");
                        var output = m.Text ?? "";
                        List<FunctionResponsePart> fparts = null;
                        if (mp != null)
                        {
                            foreach (var x in mp)
                            {
                                if (x.Kind == AiContentPartKinds.Text)
                                {
                                    output = String.IsNullOrEmpty(output) ? x.Text : String.Concat(output, "\n", x.Text);
                                    continue;
                                }
                                if (legacy)
                                {
                                    if (extra == null)
                                        extra = new List<Part>();
                                    extra.Add(new Part { Text = String.Concat("[Attachment of the tool result ", name, "]") });
                                    extra.Add(GoogleModels.ToPart(x));
                                    continue;
                                }
                                (fparts ??= new List<FunctionResponsePart>()).Add(ToFunctionResponsePart(x));
                            }
                        }
                        (responses ??= new List<Part>()).Add(new Part
                        {
                            FunctionResponse = new FunctionResponse
                            {
                                Id = (String.IsNullOrEmpty(tid) || tid.StartsWith(GeneratedIdPrefix, StringComparison.Ordinal)) ? null : tid,
                                Name = name,
                                Response = new Dictionary<String, Object> { { "output", output } },
                                Parts = fparts,
                            }
                        });
                        break;
                }
            }
            Flush();
            return contents;
        }

        #endregion//Completion helpers
    }
}
