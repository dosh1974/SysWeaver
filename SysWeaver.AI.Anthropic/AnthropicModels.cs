using Anthropic;
using Anthropic.Helpers;
using Anthropic.Models.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    using Message = Anthropic.Models.Messages.Message;

    /// <summary>
    /// Anthropic (Claude) model capabilities, conversions between API agnostic and Anthropic types and the request loop
    /// </summary>
    static class AnthropicModels
    {
        internal enum ThinkingTypes
        {
            /// <summary>
            /// The model can't think (Claude 3)
            /// </summary>
            None,
            /// <summary>
            /// Thinking with a token budget, can be disabled (Claude 3.7 to Claude 4.5)
            /// </summary>
            Budget,
            /// <summary>
            /// Adaptive thinking, can be disabled
            /// </summary>
            Adaptive,
            /// <summary>
            /// Adaptive thinking, can only be disabled between tool calls (Claude Sonnet 5.5)
            /// </summary>
            AdaptiveBetweenTools,
            /// <summary>
            /// Adaptive thinking, can't be disabled (Claude Opus 5.5, Fable)
            /// </summary>
            AdaptiveAlways,
        }

        internal enum EffortTypes
        {
            /// <summary>
            /// The model have no effort setting
            /// </summary>
            None,
            /// <summary>
            /// Effort levels: low, medium, high, max (Claude 4.6)
            /// </summary>
            NoXHigh,
            /// <summary>
            /// Effort levels: low, medium, high, xhigh, max
            /// </summary>
            All,
        }

        internal sealed class Opt
        {
            public String Model;
            /// <summary>
            /// True if model supports temperature (only used when thinking is disabled)
            /// </summary>
            public bool Temp;
            /// <summary>
            /// True if model supports forcing tool use (only used when thinking is disabled)
            /// </summary>
            public bool ForcedTools;
            /// <summary>
            /// How thinking (reasoning) is configured
            /// </summary>
            public ThinkingTypes Thinking = ThinkingTypes.AdaptiveAlways;
            /// <summary>
            /// The supported effort levels
            /// </summary>
            public EffortTypes Effort = EffortTypes.All;
        }

        /// <summary>
        /// Get the capabilities of a model (unknown models are assumed to be like the latest models)
        /// </summary>
        /// <param name="model">The model id, platform prefixes such as "anthropic." or "us.anthropic." are ignored</param>
        internal static Opt GetOptions(String model)
        {
            var i = model.IndexOf("claude-", StringComparison.OrdinalIgnoreCase);
            if (i > 0)
                model = model.Substring(i);
            foreach (var x in Opts)
                if (model.StartsWith(x.Model, StringComparison.OrdinalIgnoreCase))
                    return x;
            return new Opt();
        }

        static readonly Opt[] Opts =
        [
            //  Order matters (prefix match), more specific names must come first
            new Opt { Model = "claude-fable-5-1" },
            new Opt { Model = "claude-mythos-5-1" },
            new Opt { Model = "claude-fable-5", ForcedTools = true },
            new Opt { Model = "claude-mythos-5", ForcedTools = true },
            new Opt { Model = "claude-opus-5-5" },
            new Opt { Model = "claude-opus-5", ForcedTools = true, Thinking = ThinkingTypes.Adaptive },
            new Opt { Model = "claude-sonnet-5-5", Thinking = ThinkingTypes.AdaptiveBetweenTools },
            new Opt { Model = "claude-sonnet-5", ForcedTools = true, Thinking = ThinkingTypes.Adaptive },
            new Opt { Model = "claude-haiku-5-5", ForcedTools = true, Thinking = ThinkingTypes.Adaptive },
            new Opt { Model = "claude-opus-4-8", ForcedTools = true, Thinking = ThinkingTypes.Adaptive },
            new Opt { Model = "claude-opus-4-7", ForcedTools = true, Thinking = ThinkingTypes.Adaptive },
            new Opt { Model = "claude-opus-4-6", Temp = true, ForcedTools = true, Thinking = ThinkingTypes.Adaptive, Effort = EffortTypes.NoXHigh },
            new Opt { Model = "claude-sonnet-4-6", Temp = true, ForcedTools = true, Thinking = ThinkingTypes.Adaptive, Effort = EffortTypes.NoXHigh },
            new Opt { Model = "claude-opus-4", Temp = true, ForcedTools = true, Thinking = ThinkingTypes.Budget, Effort = EffortTypes.None },
            new Opt { Model = "claude-sonnet-4", Temp = true, ForcedTools = true, Thinking = ThinkingTypes.Budget, Effort = EffortTypes.None },
            new Opt { Model = "claude-haiku-4", Temp = true, ForcedTools = true, Thinking = ThinkingTypes.Budget, Effort = EffortTypes.None },
            new Opt { Model = "claude-3-7", Temp = true, ForcedTools = true, Thinking = ThinkingTypes.Budget, Effort = EffortTypes.None },
            new Opt { Model = "claude-3", Temp = true, ForcedTools = true, Thinking = ThinkingTypes.None, Effort = EffortTypes.None },
        ];

        /// <summary>
        /// The request settings that depends on the model and the reasoning effort
        /// </summary>
        internal sealed class Settings
        {
            public ThinkingConfigParam Thinking;
            public OutputConfig Output;
            public int MaxTokens;
            /// <summary>
            /// True if a temperature can be sent (thinking is disabled and the model supports it)
            /// </summary>
            public bool Temp;
            /// <summary>
            /// True if tool use can be forced (thinking is disabled and the model supports it)
            /// </summary>
            public bool ForcedTools;
        }

        static Effort GetEffort(EffortTypes t, AiReasoning r)
            => r switch
            {
                AiReasoning.Medium => Effort.Medium,
                AiReasoning.High => Effort.High,
                AiReasoning.ExtraHigh => t == EffortTypes.All ? Effort.Xhigh : Effort.High,
                AiReasoning.Max => Effort.Max,
                _ => Effort.Low,
            };

        static int GetBudget(AiReasoning r)
            => r switch
            {
                AiReasoning.Minimal => 1024,
                AiReasoning.Low => 2048,
                AiReasoning.Medium => 4096,
                AiReasoning.High => 8192,
                AiReasoning.ExtraHigh => 16384,
                _ => 32768,
            };

        /// <summary>
        /// Get the request settings for a model and reasoning effort
        /// </summary>
        internal static Settings GetSettings(Opt o, AiReasoning r, int maxTokens)
        {
            var s = new Settings
            {
                MaxTokens = Math.Max(1, maxTokens),
            };
            if (o.Effort != EffortTypes.None)
                s.Output = new OutputConfig { Effort = GetEffort(o.Effort, r) };
            bool thinking = true;
            switch (o.Thinking)
            {
                case ThinkingTypes.None:
                    thinking = false;
                    break;
                case ThinkingTypes.Budget:
                    if (r == AiReasoning.None)
                    {
                        thinking = false;
                        break;
                    }
                    var budget = GetBudget(r);
                    //  The budget must be less than the max tokens
                    s.MaxTokens = Math.Max(s.MaxTokens, budget + 4096);
                    s.Thinking = new ThinkingConfigEnabled { BudgetTokens = budget };
                    break;
                case ThinkingTypes.Adaptive:
                    if (r == AiReasoning.None)
                    {
                        thinking = false;
                        s.Thinking = new ThinkingConfigDisabled();
                        break;
                    }
                    s.Thinking = new ThinkingConfigAdaptive();
                    break;
                case ThinkingTypes.AdaptiveBetweenTools:
                    s.Thinking = r == AiReasoning.None ? new ThinkingConfigBetweenTools() : new ThinkingConfigAdaptive();
                    break;
                default:
                    s.Thinking = new ThinkingConfigAdaptive();
                    break;
            }
            if (!thinking)
            {
                s.Temp = o.Temp;
                s.ForcedTools = o.ForcedTools;
            }
            return s;
        }

        /// <summary>
        /// Instruction added to the system prompt when tool use is required but can't be forced
        /// </summary>
        internal const String RequireToolsInstruction = "You must call at least one of the available tools before answering.";

        /// <summary>
        /// Build a request
        /// </summary>
        /// <param name="model">The model to use</param>
        /// <param name="s">The settings</param>
        /// <param name="system">The system prompt, can be null</param>
        /// <param name="tools">The tools, can be null</param>
        /// <param name="toolChoice">How tools should be used</param>
        /// <param name="temperature">The temperature, null for the API default (only sent if supported)</param>
        /// <param name="messages">The conversation</param>
        /// <param name="cache">True to enable (automatic) prompt caching</param>
        internal static MessageCreateParams BuildParams(String model, Settings s, String system, Tool[] tools, AiCompletionToolChoices toolChoice, float? temperature, List<MessageParam> messages, bool cache)
        {
            var haveTools = (tools?.Length ?? 0) > 0;
            if (haveTools && (toolChoice == AiCompletionToolChoices.Required) && (!s.ForcedTools))
                system = String.IsNullOrEmpty(system) ? RequireToolsInstruction : String.Concat(system, "\n\n", RequireToolsInstruction);
            var p = new MessageCreateParams
            {
                Model = model,
                MaxTokens = s.MaxTokens,
                Messages = messages,
            };
            if (s.Thinking != null)
                p = p with { Thinking = s.Thinking };
            if (s.Output != null)
                p = p with { OutputConfig = s.Output };
            if (!String.IsNullOrEmpty(system))
                p = p with { System = system };
#pragma warning disable CS0618 // Temperature is deprecated, but supported by older models (only sent if supported)
            if (s.Temp && temperature.HasValue)
                p = p with { Temperature = Math.Clamp(temperature.Value, 0.0f, 1.0f) };
#pragma warning restore CS0618
            if (haveTools)
            {
                p = p with { Tools = tools.Select(x => (ToolUnion)x).ToList() };
                if (toolChoice == AiCompletionToolChoices.None)
                    p = p with { ToolChoice = new ToolChoiceNone() };
                else if ((toolChoice == AiCompletionToolChoices.Required) && s.ForcedTools)
                    p = p with { ToolChoice = new ToolChoiceAny() };
            }
            if (cache)
                p = p with { CacheControl = new CacheControlEphemeral() };
            return p;
        }

        #region Conversions

        /// <summary>
        /// Get the Anthropic tool of an API agnostic tool
        /// </summary>
        internal static Tool GetTool(AiTool tool)
            => tool.Tool.GetApiTool(f => new Tool
            {
                Name = f.FunctionName,
                Description = f.FunctionDescription,
                InputSchema = GetSchema(f.FunctionParameters),
            });

        static InputSchema GetSchema(BinaryData p)
        {
            Dictionary<String, JsonElement> d = null;
            if (p != null)
                d = JsonSerializer.Deserialize<Dictionary<String, JsonElement>>(p.ToMemory().Span);
            d ??= new Dictionary<String, JsonElement>();
            if (!d.ContainsKey("type"))
                d["type"] = JsonSerializer.SerializeToElement("object");
            return InputSchema.FromRawUnchecked(d);
        }

        static bool IsMime(String mime, String start)
            => mime?.StartsWith(start, StringComparison.OrdinalIgnoreCase) ?? false;

        static bool IsPdf(String mime)
            => String.Equals(mime, "application/pdf", StringComparison.OrdinalIgnoreCase);

        static bool IsText(String mime)
            => IsMime(mime, "text/")
            || String.Equals(mime, "application/json", StringComparison.OrdinalIgnoreCase)
            || String.Equals(mime, "application/xml", StringComparison.OrdinalIgnoreCase);

        static ImageBlockParam ToImage(AiContentPart x)
        {
            if (x.Uri != null)
                return new ImageBlockParam { Source = new UrlImageSource { Url = x.Uri.ToString() } };
            return new ImageBlockParam
            {
                Source = new Base64ImageSource
                {
                    Data = Convert.ToBase64String(x.Data.ToMemory().Span),
                    MediaType = x.MimeType.ToLowerInvariant() switch
                    {
                        "image/jpeg" => MediaType.ImageJpeg,
                        "image/jpg" => MediaType.ImageJpeg,
                        "image/png" => MediaType.ImagePng,
                        "image/gif" => MediaType.ImageGif,
                        "image/webp" => MediaType.ImageWebP,
                        _ => throw new NotSupportedException("Images of type " + x.MimeType + " are not supported!"),
                    },
                },
            };
        }

        static DocumentBlockParam ToDocument(AiContentPart x)
        {
            var mime = x.MimeType;
            var title = String.IsNullOrEmpty(x.Filename) ? null : x.Filename;
            DocumentBlockParam d;
            if (x.Uri != null)
            {
                //  Only pdf documents can be referenced by url
                if ((mime != null) && (!IsPdf(mime)))
                    throw new NotSupportedException("Files of type " + mime + " can't be referenced by an url!");
                d = new DocumentBlockParam { Source = new UrlPdfSource { Url = x.Uri.ToString() } };
            }
            else if (IsPdf(mime))
                d = new DocumentBlockParam { Source = new Base64PdfSource { Data = Convert.ToBase64String(x.Data.ToMemory().Span) } };
            else if (IsText(mime))
                d = new DocumentBlockParam { Source = new PlainTextSource { Data = Encoding.UTF8.GetString(x.Data.ToMemory().Span) } };
            else
                throw new NotSupportedException("Files of type " + mime + " are not supported!");
            if (title != null)
                d = d with { Title = title };
            return d;
        }

        /// <summary>
        /// Convert an API agnostic content part to an Anthropic content block
        /// </summary>
        internal static ContentBlockParam ToBlock(AiContentPart x)
        {
            switch (x.Kind)
            {
                case AiContentPartKinds.Text:
                    return new TextBlockParam { Text = x.Text };
                case AiContentPartKinds.Image:
                    return ToImage(x);
                case AiContentPartKinds.File:
                    if (IsMime(x.MimeType, "image/"))
                        return ToImage(x);
                    return ToDocument(x);
            }
            throw new NotSupportedException("Content of kind " + x.Kind + " is not supported!");
        }

        /// <summary>
        /// Convert an API agnostic content part to an Anthropic tool result content block
        /// </summary>
        internal static Block ToToolResultBlock(AiContentPart x)
        {
            switch (x.Kind)
            {
                case AiContentPartKinds.Text:
                    return new TextBlockParam { Text = x.Text };
                case AiContentPartKinds.Image:
                    return ToImage(x);
                case AiContentPartKinds.File:
                    if (IsMime(x.MimeType, "image/"))
                        return ToImage(x);
                    return ToDocument(x);
            }
            throw new NotSupportedException("Content of kind " + x.Kind + " is not supported!");
        }

        /// <summary>
        /// Create a user message
        /// </summary>
        internal static MessageParam CreateUserMessage(String text, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData, String from)
        {
            var blocks = new List<ContentBlockParam>(2 + (extraData?.Count ?? 0));
            //  The API have no participant name, so add it as a separate text block
            if (!String.IsNullOrEmpty(from))
                blocks.Add(new TextBlockParam { Text = "[Message from: " + from + "]" });
            if (!String.IsNullOrEmpty(text))
                blocks.Add(new TextBlockParam { Text = text });
            if (extraData != null)
                foreach (var x in extraData)
                    blocks.Add(ToBlock(x.Item1));
            if (blocks.Count <= 0)
                blocks.Add(new TextBlockParam { Text = "..." });
            return new MessageParam { Role = Role.User, Content = blocks };
        }

        /// <summary>
        /// Get the content blocks of a message
        /// </summary>
        internal static IReadOnlyList<ContentBlockParam> GetBlocks(MessageParam m)
        {
            var c = m.Content;
            if (c.TryPickContentBlockParams(out var blocks))
                return blocks;
            if (c.TryPickString(out var s) && (!String.IsNullOrEmpty(s)))
                return [new TextBlockParam { Text = s }];
            return [];
        }

        internal static bool IsAssistant(MessageParam m)
            => m.Role == Role.Assistant;

        internal static bool HaveToolUse(MessageParam m)
            => GetBlocks(m).Any(x => x.TryPickToolUse(out var _));

        static bool IsThinking(ContentBlockParam b)
            => b.TryPickThinking(out var _) || b.TryPickRedactedThinking(out var _);

        /// <summary>
        /// Remove all thinking blocks from the history (assistant messages without any other content are removed).
        /// Thinking blocks are bound to the conversation prefix that produced them, newer models reject them if an earlier turn (or the system prompt or tools) changed.
        /// Removing all of them at the end of a turn allows the system prompt to change and turns to be removed between turns.
        /// </summary>
        /// <param name="history">The conversation</param>
        internal static void StripThinking(List<MessageParam> history)
        {
            int output = 0;
            var hl = history.Count;
            for (int i = 0; i < hl; ++i)
            {
                var m = history[i];
                if (IsAssistant(m))
                {
                    var blocks = GetBlocks(m);
                    if (blocks.Any(IsThinking))
                    {
                        var keep = blocks.Where(x => !IsThinking(x)).ToList();
                        if (keep.Count <= 0)
                            continue;
                        m = new MessageParam { Role = Role.Assistant, Content = keep };
                    }
                }
                history[output] = m;
                ++output;
            }
            if (output < hl)
                history.RemoveRange(output, hl - output);
        }

        /// <summary>
        /// Convert the content of a response to the content of an assistant message (thinking blocks are kept unchanged, they are required when continuing after a tool call)
        /// </summary>
        internal static List<ContentBlockParam> ToParams(Message m)
        {
            var ret = new List<ContentBlockParam>(m.Content.Count);
            foreach (var b in m.Content)
            {
                if (b.TryPickText(out var t))
                {
                    if (!String.IsNullOrEmpty(t.Text))
                        ret.Add(new TextBlockParam { Text = t.Text });
                }
                else if (b.TryPickThinking(out var th))
                    ret.Add(new ThinkingBlockParam { Thinking = th.Thinking, Signature = th.Signature });
                else if (b.TryPickRedactedThinking(out var rt))
                    ret.Add(new RedactedThinkingBlockParam { Data = rt.Data });
                else if (b.TryPickToolUse(out var tu))
                    ret.Add(new ToolUseBlockParam { ID = tu.ID, Name = tu.Name, Input = tu.Input });
            }
            return ret;
        }

        /// <summary>
        /// Get the text of a response
        /// </summary>
        internal static String GetText(Message m)
        {
            var sb = new StringBuilder();
            foreach (var b in m.Content)
                if (b.TryPickText(out var t))
                    sb.Append(t.Text);
            return sb.ToString();
        }

        #endregion//Conversions

        #region Request loop

        /// <summary>
        /// The start of the error returned by GetStopError when the model refused to respond
        /// </summary>
        internal const String ContentFilterError = "Error: The model refused to respond";

        /// <summary>
        /// Get an error message (starting with "Error: ") if the model didn't stop normally, else null
        /// </summary>
        internal static String GetStopError(Message m)
        {
            var sr = m.StopReason?.Raw();
            switch (sr)
            {
                case null:
                case "end_turn":
                case "stop_sequence":
                case "tool_use":
                    return null;
                case "max_tokens":
                    return "Error: Incomplete model output due to MaxTokens parameter or token limit exceeded.";
                case "model_context_window_exceeded":
                    return "Error: Incomplete model output, the context window of the model is exceeded.";
                case "refusal":
                    var cat = m.StopDetails?.Category?.Raw();
                    return String.IsNullOrEmpty(cat) ? ContentFilterError + "." : String.Concat(ContentFilterError, " (", cat, ").");
            }
            return "Error: " + sr;
        }

        /// <summary>
        /// Get the input and output token counts of a response (input includes cached tokens)
        /// </summary>
        internal static (long In, long Cached, long Out) GetUsage(Message m)
        {
            var u = m.Usage;
            if (u == null)
                return (0, 0, 0);
            var cached = u.CacheReadInputTokens ?? 0;
            return (u.InputTokens + (u.CacheCreationInputTokens ?? 0) + cached, cached, u.OutputTokens);
        }

        /// <summary>
        /// Send a request (streaming is always used, so that long responses doesn't time out)
        /// </summary>
        /// <param name="client">The client</param>
        /// <param name="p">The request</param>
        /// <param name="onText">Optional callback with every text delta</param>
        /// <param name="cancel">Cancellation token</param>
        /// <returns>The complete response</returns>
        internal static async Task<Message> Send(IAnthropicClient client, MessageCreateParams p, Func<String, Task> onText, CancellationToken cancel = default)
        {
            var agg = new MessageContentAggregator();
            await foreach (var e in agg.CollectAsync(client.Messages.CreateStreaming(p, cancel)).ConfigureAwait(false))
            {
                if (onText == null)
                    continue;
                if (e.TryPickContentBlockDelta(out var d) && d.Delta.TryPickText(out var t) && (!String.IsNullOrEmpty(t.Text)))
                    await onText(t.Text).ConfigureAwait(false);
            }
            return agg.Message();
        }

        /// <summary>
        /// Prefix of tool results that are errors (as returned by AiCalls)
        /// </summary>
        const String ToolErrorPrefix = "Execution failed: ";

        /// <summary>
        /// Run requests until the model stops calling tools.
        /// The model responses (including thinking blocks) and tool results are added to the history.
        /// </summary>
        /// <param name="client">The client</param>
        /// <param name="model">The model to use</param>
        /// <param name="build">Function used to build each request (from the history)</param>
        /// <param name="history">The conversation, model responses and tool results are added to this list</param>
        /// <param name="exec">Function used to execute tool calls</param>
        /// <param name="chatLock">Optional lock to limit concurrency</param>
        /// <param name="monitor">Optional performance monitor</param>
        /// <param name="debug">Optional debug tracking</param>
        /// <param name="onUsage">Optional usage callback (called once)</param>
        /// <param name="onText">If non null, this is called with the accumulated text whenever it changes</param>
        /// <param name="afterTools">Optional callback after each batch of tool calls</param>
        /// <param name="text">The accumulated text output (from all requests) is appended to this</param>
        /// <returns>null if successful, else an error message</returns>
        internal static async Task<String> Run(IAnthropicClient client, String model, Func<List<MessageParam>, MessageCreateParams> build, List<MessageParam> history,
            Func<IReadOnlyList<AiFunctionCall>, Task<String[]>> exec,
            AsyncLock chatLock, PerfMonitor monitor, AiDebugMessage debug, Func<String, long, long, Task> onUsage,
            Func<String, Task> onText, Func<Task> afterTools, StringBuilder text)
        {
            long totalIn = 0;
            long totalOut = 0;
            Func<String, Task> onDelta = onText == null ? null : t =>
            {
                text.Append(t);
                return onText(text.ToString());
            };
            try
            {
                for (; ; )
                {
                    var p = build(history);
                    Message m;
                    using (var _ = await (chatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
                    {
                        using var _m = monitor?.Track(nameof(Send));
                        m = await Send(client, p, onDelta).ConfigureAwait(false);
                    }
                    var (i, _c, o) = GetUsage(m);
                    totalIn += i;
                    totalOut += o;
                    if (debug != null)
                    {
                        debug.InputTokenCount += i;
                        debug.OutputTokenCount += o;
                    }
                    if (onText == null)
                        text.Append(GetText(m));
                    var content = ToParams(m);
                    if (content.Count > 0)
                        history.Add(new MessageParam { Role = Role.Assistant, Content = content });
                    var calls = m.Content.Select(x => x.TryPickToolUse(out var tu) ? tu : null).Where(x => x != null).ToList();
                    if ((calls.Count <= 0) || (m.StopReason?.Raw() != "tool_use"))
                        return GetStopError(m);
                    var res = await exec(calls.Select(x => new AiFunctionCall(x.Name, BinaryData.FromString(JsonSerializer.Serialize(x.Input)))).ToList()).ConfigureAwait(false);
                    var results = new List<ContentBlockParam>(calls.Count);
                    for (int c = 0; c < calls.Count; ++c)
                    {
                        var r = res[c] ?? "";
                        var tr = new ToolResultBlockParam
                        {
                            ToolUseID = calls[c].ID,
                            Content = r,
                        };
                        if (r.StartsWith(ToolErrorPrefix, StringComparison.Ordinal))
                            tr = tr with { IsError = true };
                        results.Add(tr);
                    }
                    history.Add(new MessageParam { Role = Role.User, Content = results });
                    if (afterTools != null)
                        await afterTools().ConfigureAwait(false);
                }
            }
            finally
            {
                if (onUsage != null)
                    await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
            }
        }

        #endregion//Request loop
    }
}
