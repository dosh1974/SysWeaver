using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    /// <summary>
    /// A loaded local LLM, prompt formatting (using the chat template of the model), tool call parsing and the request loop
    /// </summary>
    public sealed class LlamaSharpModel : IDisposable
    {
        public override string ToString() => String.Concat(Name, " : ", FilePath);

        /// <summary>
        /// Load a model
        /// </summary>
        /// <param name="llm">The model parameters</param>
        /// <param name="filePath">The resolved (absolute) file path</param>
        /// <param name="msg">Optional message host</param>
        internal LlamaSharpModel(LlamaSharpLlm llm, String filePath, IMessageHost msg = null)
        {
            Llm = llm;
            Name = llm.Name;
            FilePath = filePath;
            var mp = new ModelParams(filePath)
            {
                ContextSize = llm.ContextSize > 0 ? llm.ContextSize : null,
                GpuLayerCount = llm.GpuLayerCount,
                MainGpu = llm.MainGpu,
                BatchSize = llm.BatchSize,
                UBatchSize = llm.UBatchSize,
                FlashAttention = llm.FlashAttention switch
                {
                    LlamaSharpFlashAttention.Disabled => false,
                    LlamaSharpFlashAttention.Enabled => true,
                    _ => null,
                },
                TypeK = GetType(llm.TypeK),
                TypeV = GetType(llm.TypeV),
                UseMemorymap = llm.UseMemoryMap,
                UseMemoryLock = llm.UseMemoryLock,
            };
            if (llm.Threads > 0)
                mp.Threads = llm.Threads;
            if (llm.BatchThreads > 0)
                mp.BatchThreads = llm.BatchThreads;
            if (llm.RopeFrequencyBase > 0)
                mp.RopeFrequencyBase = llm.RopeFrequencyBase;
            if (llm.RopeFrequencyScale > 0)
                mp.RopeFrequencyScale = llm.RopeFrequencyScale;
            Params = mp;
            try
            {
                Weights = LLamaWeights.LoadFromFile(mp);
                LoadedFilePath = filePath;
            }
            catch
            {
                //  Some models have layers that the bundled llama.cpp doesn't support, try to use a copy without them
                var stripped = LlamaSharpGguf.GetStrippedModel(filePath, Name, msg);
                if (stripped == null)
                    throw;
                mp.ModelPath = stripped;
                Weights = LLamaWeights.LoadFromFile(mp);
                LoadedFilePath = stripped;
            }
            ContextSize = (int)(llm.ContextSize > 0 ? llm.ContextSize : (uint)Weights.ContextSize);
            Lock = new AsyncLock(Math.Max(1, llm.MaxConcurrent));
            Created = File.GetLastWriteTimeUtc(filePath);
            try
            {
                BuiltInTemplate = GetBuiltInTemplate();
            }
            catch
            {
                Weights.Dispose();
                throw;
            }
            IsGemma4 = BuiltInTemplate == LlamaSharpGemma4.Name;
            AntiPrompts = IsGemma4 ? LlamaSharpGemma4.AntiPrompts : null;
        }

        static GGMLType? GetType(LlamaSharpKvCacheType t)
            => t switch
            {
                LlamaSharpKvCacheType.F16 => GGMLType.GGML_TYPE_F16,
                LlamaSharpKvCacheType.Q8_0 => GGMLType.GGML_TYPE_Q8_0,
                LlamaSharpKvCacheType.Q4_0 => GGMLType.GGML_TYPE_Q4_0,
                _ => null,
            };

        public void Dispose() => Weights.Dispose();

        /// <summary>
        /// The parameters the model was loaded with
        /// </summary>
        public readonly LlamaSharpLlm Llm;

        /// <summary>
        /// The model name (model code)
        /// </summary>
        public readonly String Name;

        /// <summary>
        /// The resolved file path of the model
        /// </summary>
        public readonly String FilePath;

        /// <summary>
        /// The file that was actually loaded, can be a processed copy of FilePath (ex: with unsupported layers removed)
        /// </summary>
        public readonly String LoadedFilePath;

        /// <summary>
        /// The context size (in tokens) of each request
        /// </summary>
        public readonly int ContextSize;

        /// <summary>
        /// The last write time of the model file
        /// </summary>
        public readonly DateTime Created;

        /// <summary>
        /// The model weights
        /// </summary>
        public readonly LLamaWeights Weights;

        readonly ModelParams Params;

        /// <summary>
        /// Limits the number of concurrent requests to this model
        /// </summary>
        readonly AsyncLock Lock;

        /// <summary>
        /// Executors that aren't in use
        /// </summary>
        readonly ConcurrentBag<StatelessExecutor> Executors = new ConcurrentBag<StatelessExecutor>();

        /// <summary>
        /// Count the number of tokens in some text
        /// </summary>
        public int CountTokens(String text, bool addBos = false)
            => String.IsNullOrEmpty(text) ? 0 : Weights.Tokenize(text, addBos, true, Encoding.UTF8).Length;

        #region Prompt

        internal const String RoleSystem = "system";
        internal const String RoleUser = "user";
        internal const String RoleAssistant = "assistant";

        const String TemplateGemma4 = LlamaSharpGemma4.Name;

        /// <summary>
        /// The built in format to use (llama.cpp can't run jinja templates, it only knows a fixed set of templates), null to let llama.cpp apply the template
        /// </summary>
        readonly String BuiltInTemplate;

        /// <summary>
        /// True if the built in Gemma 4 template is used (including native tool calls)
        /// </summary>
        readonly bool IsGemma4;

        /// <summary>
        /// Extra stop sequences used by the built in template (if any)
        /// </summary>
        readonly String[] AntiPrompts;

        LLamaTemplate CreateTemplate()
        {
            var t = Llm.ChatTemplate;
            return String.IsNullOrEmpty(t) ? new LLamaTemplate(Weights) : new LLamaTemplate(t);
        }

        /// <summary>
        /// Get the built in template to use if llama.cpp doesn't support the chat template
        /// </summary>
        /// <returns>The name of the built in template to use, or null if llama.cpp supports the template</returns>
        String GetBuiltInTemplate()
        {
            var t = Llm.ChatTemplate;
            if (String.Equals(t, TemplateGemma4, StringComparison.OrdinalIgnoreCase))
                return TemplateGemma4;
            try
            {
                var test = CreateTemplate();
                test.AddAssistant = true;
                test.Add(RoleUser, "Hi");
                test.Apply();
                return null;
            }
            catch (Exception ex)
            {
                if (String.IsNullOrEmpty(t))
                    Weights.Metadata.TryGetValue("tokenizer.chat_template", out t);
                if ((t != null) && t.Contains("<|turn>", StringComparison.Ordinal))
                    return TemplateGemma4;
                throw new Exception(String.Concat("The chat template of model \"", Name, "\" isn't supported by llama.cpp, set ChatTemplate to a template known by llama.cpp (ex: \"chatml\") or to \"", TemplateGemma4, "\""), ex);
            }
        }

        /// <summary>
        /// Format a conversation using the chat template of the model (ending with the start of an assistant message)
        /// </summary>
        /// <param name="systemPrompt">The system prompt, can be null</param>
        /// <param name="tools">The tools available to the model, can be null</param>
        /// <param name="messages">The messages</param>
        /// <param name="first">Index of the first message to include</param>
        /// <returns>The prompt</returns>
        String Format(String systemPrompt, AiTool[] tools, IReadOnlyList<LlamaSharpMessage> messages, int first)
        {
            if (IsGemma4)
                return LlamaSharpGemma4.Format(systemPrompt, tools, messages, first);
            systemPrompt = AddTools(systemPrompt, tools);
            var t = CreateTemplate();
            t.AddAssistant = true;
            var haveSystem = !String.IsNullOrEmpty(systemPrompt);
            var systemAsUser = haveSystem && (!Llm.SupportSystemRole);
            if (haveSystem && (!systemAsUser))
                t.Add(RoleSystem, systemPrompt);
            var ml = messages.Count;
            for (int i = first; i < ml; ++i)
            {
                var m = messages[i];
                var c = m.Content ?? "";
                if (systemAsUser && (m.Role == RoleUser))
                {
                    c = String.Concat(systemPrompt, "\n\n", c);
                    systemAsUser = false;
                }
                t.Add(m.Role, c);
            }
            if (systemAsUser)
                t.Add(RoleUser, systemPrompt);
            return Encoding.UTF8.GetString(t.Apply());
        }

        /// <summary>
        /// Create a user message
        /// </summary>
        internal static LlamaSharpMessage CreateUserMessage(String text, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData, String from)
        {
            var sb = new StringBuilder();
            //  No participant names, so add it as text
            if (!String.IsNullOrEmpty(from))
                sb.Append("[Message from: ").Append(from).Append("]\n");
            sb.Append(text);
            if (extraData != null)
                foreach (var x in extraData)
                {
                    var p = x.Item1;
                    if (p.Kind == AiContentPartKinds.Text)
                    {
                        sb.Append("\n\n").Append(p.Text);
                        continue;
                    }
                    //  Local models are text only
                    sb.Append("\n\n[Attachment ").Append(p.Filename ?? p.Uri?.ToString() ?? "").Append(" (").Append(p.MimeType).Append(") can't be read by this model]");
                }
            return new LlamaSharpMessage(RoleUser, sb.ToString());
        }

        #endregion//Prompt

        #region Tools

        sealed class ToolJson
        {
            public ToolJson(String json) => Json = json;
            public readonly String Json;
        }

        /// <summary>
        /// Get the json description of a tool (Hermes / Qwen style)
        /// </summary>
        internal static String GetToolJson(AiTool tool)
            => tool.Tool.GetApiTool(f =>
            {
                using var ms = new MemoryStream();
                using (var w = new Utf8JsonWriter(ms))
                {
                    w.WriteStartObject();
                    w.WriteString("type", "function");
                    w.WritePropertyName("function");
                    w.WriteStartObject();
                    w.WriteString("name", f.FunctionName);
                    if (!String.IsNullOrEmpty(f.FunctionDescription))
                        w.WriteString("description", f.FunctionDescription);
                    w.WritePropertyName("parameters");
                    var p = f.FunctionParameters;
                    if (p == null)
                    {
                        w.WriteStartObject();
                        w.WriteString("type", "object");
                        w.WritePropertyName("properties");
                        w.WriteStartObject();
                        w.WriteEndObject();
                        w.WriteEndObject();
                    }
                    else
                        w.WriteRawValue(p.ToString());
                    w.WriteEndObject();
                    w.WriteEndObject();
                }
                return new ToolJson(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length));
            }).Json;

        /// <summary>
        /// Get the system prompt with tool descriptions added
        /// </summary>
        internal static String AddTools(String systemPrompt, AiTool[] tools)
        {
            if ((tools?.Length ?? 0) <= 0)
                return systemPrompt;
            var sb = new StringBuilder();
            if (!String.IsNullOrEmpty(systemPrompt))
                sb.Append(systemPrompt).Append("\n\n");
            sb.Append("# Tools\n\nYou may call one or more functions to assist with the user query.\n\nYou are provided with function signatures within <tools></tools> XML tags:\n<tools>");
            foreach (var x in tools)
                sb.Append('\n').Append(GetToolJson(x));
            sb.Append("\n</tools>\n\nFor each function call, return a json object with function name and arguments within <tool_call></tool_call> XML tags:\n<tool_call>\n{\"name\": <function-name>, \"arguments\": <args-json-object>}\n</tool_call>");
            return sb.ToString();
        }

        const String ToolCallStart = "<tool_call>";
        const String ToolCallEnd = "</tool_call>";

        /// <summary>
        /// Start and end tags of tool calls (Hermes / Qwen style and native Gemma 4)
        /// </summary>
        static readonly ValueTuple<String, String>[] ToolCallTags = [(ToolCallStart, ToolCallEnd), (LlamaSharpGemma4.ToolCallStart, LlamaSharpGemma4.ToolCallEnd)];

        /// <summary>
        /// Find the first tool call start tag
        /// </summary>
        /// <param name="s">The text to search</param>
        /// <param name="pos">The position to start searching at</param>
        /// <param name="tag">The index of the tag in ToolCallTags</param>
        /// <returns>The position of the start tag, or -1 if not found</returns>
        static int FindToolCall(String s, int pos, out int tag)
        {
            int best = -1;
            tag = -1;
            for (int i = 0; i < ToolCallTags.Length; ++i)
            {
                var b = s.IndexOf(ToolCallTags[i].Item1, pos, StringComparison.Ordinal);
                if ((b >= 0) && ((best < 0) || (b < best)))
                {
                    best = b;
                    tag = i;
                }
            }
            return best;
        }
        const String ThinkStart = "<think>";
        const String ThinkEnd = "</think>";

        /// <summary>
        /// Start and end tags of thinking (Gemma 4 uses a thought channel)
        /// </summary>
        static readonly ValueTuple<String, String>[] ThinkTags = [(ThinkStart, ThinkEnd), ("<|channel>", "<channel|>")];

        /// <summary>
        /// Tokens that some models outputs at the end of a message (if special tokens are decoded)
        /// </summary>
        static readonly String[] EndTokens = ["<|im_end|>", "<|eot_id|>", "<end_of_turn>", "<turn|>", LlamaSharpGemma4.ToolResponseStart, "<|end|>", "<|endoftext|>", "</s>", "<|return|>"];

        static String RemoveEndTokens(String s)
        {
            foreach (var x in EndTokens)
                if (s.Contains(x, StringComparison.Ordinal))
                    s = s.Replace(x, "", StringComparison.Ordinal);
            return s;
        }

        /// <summary>
        /// Remove thinking from model output
        /// </summary>
        internal static String RemoveThinking(String s)
        {
            foreach (var (start, end) in ThinkTags)
                s = RemoveThinking(s, start, end);
            return s;
        }

        static String RemoveThinking(String s, String start, String end)
        {
            //  Some templates starts the thinking, so the output only contains the end tag
            var e = s.IndexOf(end, StringComparison.Ordinal);
            var b = s.IndexOf(start, StringComparison.Ordinal);
            if ((e >= 0) && ((b < 0) || (b > e)))
                s = s.Substring(e + end.Length);
            for (; ; )
            {
                b = s.IndexOf(start, StringComparison.Ordinal);
                if (b < 0)
                    return s;
                e = s.IndexOf(end, b, StringComparison.Ordinal);
                if (e < 0)
                    return s.Substring(0, b);
                s = String.Concat(s.AsSpan(0, b), s.AsSpan(e + end.Length));
            }
        }

        /// <summary>
        /// Get the text to show to a user from (possibly partial) model output, thinking and tool calls are removed
        /// </summary>
        internal static String GetVisibleText(String raw)
        {
            var s = RemoveThinking(RemoveEndTokens(raw));
            for (; ; )
            {
                var b = FindToolCall(s, 0, out var tag);
                if (b < 0)
                    break;
                var end = ToolCallTags[tag].Item2;
                var e = s.IndexOf(end, b, StringComparison.Ordinal);
                if (e < 0)
                {
                    s = s.Substring(0, b);
                    break;
                }
                s = String.Concat(s.AsSpan(0, b), s.AsSpan(e + end.Length));
            }
            //  Hide a partial tag at the end (while streaming)
            var lt = s.LastIndexOf('<');
            if (lt >= 0)
            {
                var tail = s.AsSpan(lt);
                var partial = false;
                foreach (var (start, _) in ToolCallTags)
                    partial |= start.AsSpan().StartsWith(tail, StringComparison.Ordinal);
                foreach (var (start, _) in ThinkTags)
                    partial |= start.AsSpan().StartsWith(tail, StringComparison.Ordinal);
                if (partial)
                    s = s.Substring(0, lt);
            }
            return s.Trim();
        }

        /// <summary>
        /// Parse the tool calls in some model output
        /// </summary>
        /// <param name="output">The model output</param>
        /// <param name="errors">An error message for each call that couldn't be parsed (null for valid calls), the list is null if there are no calls</param>
        /// <returns>The calls, or null if there are no calls</returns>
        internal static List<AiFunctionCall> GetToolCalls(String output, out List<String> errors)
        {
            List<AiFunctionCall> calls = null;
            errors = null;
            int pos = 0;
            for (; ; )
            {
                var b = FindToolCall(output, pos, out var tag);
                if (b < 0)
                    break;
                var (start, end) = ToolCallTags[tag];
                b += start.Length;
                var e = output.IndexOf(end, b, StringComparison.Ordinal);
                var call = (e < 0 ? output.Substring(b) : output.Substring(b, e - b)).Trim();
                pos = e < 0 ? output.Length : e + end.Length;
                calls ??= new List<AiFunctionCall>();
                errors ??= new List<String>();
                String error;
                if (start == ToolCallStart)
                    calls.Add(ParseToolCall(call, out error));
                else
                    calls.Add(LlamaSharpGemma4.ParseCall(call, InvalidToolCall, out error));
                errors.Add(error);
            }
            return calls;
        }

        /// <summary>
        /// The name used for a tool call where the function name couldn't be parsed
        /// </summary>
        const String InvalidToolCall = "invalid_tool_call";

        const String HermesFormatHelp = "use the format: {\"name\": <function-name>, \"arguments\": <args-json-object>}";

        static AiFunctionCall ParseToolCall(String json, out String error)
        {
            error = null;
            String name = null;
            try
            {
                using var d = JsonDocument.Parse(json);
                var r = d.RootElement;
                if (r.ValueKind != JsonValueKind.Object)
                {
                    error = "Error: The tool call isn't a json object, " + HermesFormatHelp;
                    return new AiFunctionCall(InvalidToolCall, BinaryData.FromString("{}"));
                }
                if (r.TryGetProperty("name", out var n) && (n.ValueKind == JsonValueKind.String))
                    name = n.GetString();
                if (String.IsNullOrEmpty(name))
                {
                    error = "Error: The tool call doesn't have a name, " + HermesFormatHelp;
                    return new AiFunctionCall(InvalidToolCall, BinaryData.FromString("{}"));
                }
                if (!r.TryGetProperty("arguments", out var a))
                    r.TryGetProperty("parameters", out a);
                String args = a.ValueKind switch
                {
                    JsonValueKind.Object => a.GetRawText(),
                    //  Some models encode the arguments as a string
                    JsonValueKind.String => a.GetString(),
                    _ => "{}",
                };
                return new AiFunctionCall(name, BinaryData.FromString(String.IsNullOrWhiteSpace(args) ? "{}" : args));
            }
            catch (Exception ex)
            {
                error = String.Concat("Error: The tool call isn't valid json (", ex.Message, "), ", HermesFormatHelp);
                return new AiFunctionCall(name ?? InvalidToolCall, BinaryData.FromString("{}"));
            }
        }

        /// <summary>
        /// Find the tool that the model meant to call.
        /// Some models adds a namespace (ex: "default_api:BuildTable" or "functions.BuildTable") or use a different casing.
        /// </summary>
        /// <param name="name">The name used by the model</param>
        /// <param name="tools">The available tools</param>
        /// <returns>The name of the tool, or null if there is no such tool</returns>
        static String ResolveToolName(String name, AiTool[] tools)
        {
            if (tools == null)
                return null;
            foreach (var t in tools)
                if (String.Equals(t.Name, name, StringComparison.Ordinal))
                    return t.Name;
            var i = name.LastIndexOfAny([':', '.', '/']);
            var n = (i >= 0 ? name.Substring(i + 1) : name).Trim().Trim('"', '\'');
            foreach (var t in tools)
                if (String.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase))
                    return t.Name;
            return null;
        }

        /// <summary>
        /// Resolve the tool names of calls, calls to unknown tools gets an error
        /// </summary>
        static void ResolveToolNames(List<AiFunctionCall> calls, List<String> errors, AiTool[] tools)
        {
            for (int i = 0; i < calls.Count; ++i)
            {
                if (errors[i] != null)
                    continue;
                var c = calls[i];
                var name = ResolveToolName(c.Name, tools);
                if (name != null)
                {
                    if (name != c.Name)
                        calls[i] = new AiFunctionCall(name, c.Arguments);
                    continue;
                }
                errors[i] = (tools?.Length ?? 0) > 0
                    ? String.Concat("Error: There is no tool named \"", c.Name, "\", the available tools are: ", String.Join(", ", tools.Select(x => x.Name)), ".")
                    : String.Concat("Error: There is no tool named \"", c.Name, "\", no tools are available.");
            }
        }

        /// <summary>
        /// Get a string that identifies a set of calls (used to detect a model repeating the same calls)
        /// </summary>
        static String GetCallsSignature(List<AiFunctionCall> calls)
        {
            var sb = new StringBuilder();
            foreach (var c in calls)
                sb.Append(c.Name).Append('\0').Append(c.Arguments?.ToString()).Append('\0');
            return sb.ToString();
        }

        /// <summary>
        /// Don't escape non-ascii, quotes etc (the json is only read by the model)
        /// </summary>
        static readonly JsonSerializerOptions ToolResponseJson = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        LlamaSharpMessage CreateToolResponses(IReadOnlyList<AiFunctionCall> calls, String[] res)
        {
            if (IsGemma4)
                return new LlamaSharpMessage(RoleUser, LlamaSharpGemma4.CreateToolResponses(calls, res), false, true);
            var sb = new StringBuilder();
            for (int i = 0; i < calls.Count; ++i)
            {
                if (i > 0)
                    sb.Append('\n');
                sb.Append("<tool_response>\n");
                sb.Append(JsonSerializer.Serialize(new Dictionary<String, String> { { "name", calls[i].Name }, { "content", res[i] } }, ToolResponseJson));
                sb.Append("\n</tool_response>");
            }
            return new LlamaSharpMessage(RoleUser, sb.ToString(), false, true);
        }

        #endregion//Tools

        #region Request loop

        /// <summary>
        /// The maximum number of tool call rounds in a single request (small models can get stuck calling tools)
        /// </summary>
        const int MaxToolRounds = 32;

        /// <summary>
        /// The maximum number of times the model may repeat the exact same tool calls (in consecutive rounds)
        /// </summary>
        const int MaxRepeatedToolCalls = 2;

        /// <summary>
        /// Number of tokens in the context that are never used
        /// </summary>
        const int ContextMargin = 4;

        /// <summary>
        /// The result of a single inference
        /// </summary>
        sealed class InferResult
        {
            /// <summary>
            /// The model output (end tokens and thinking removed)
            /// </summary>
            public String Output;

            /// <summary>
            /// Number of tokens in the prompt
            /// </summary>
            public int PromptTokens;

            /// <summary>
            /// Number of generated tokens
            /// </summary>
            public int OutTokens;

            /// <summary>
            /// True if the output was cut due to the token limit
            /// </summary>
            public bool IsTruncated;

            /// <summary>
            /// An error message if the inference couldn't be done, else null
            /// </summary>
            public String Error;
        }

        /// <summary>
        /// Run a single inference (the oldest messages are removed if the conversation doesn't fit in the context)
        /// </summary>
        /// <param name="systemPrompt">The system prompt, can be null</param>
        /// <param name="tools">The tools available to the model, can be null</param>
        /// <param name="history">The conversation (not modified)</param>
        /// <param name="temperature">The temperature to use</param>
        /// <param name="maxTokens">The maximum number of tokens to generate, 0 or less for no limit (except the context size)</param>
        /// <param name="chatLock">Optional lock to limit concurrency</param>
        /// <param name="monitor">Optional performance monitor</param>
        /// <param name="onVisible">If non null, this is called with the visible text (thinking and tool calls removed) whenever it changes</param>
        /// <param name="cancel">Cancellation token</param>
        /// <returns>The result</returns>
        async Task<InferResult> Infer(String systemPrompt, AiTool[] tools, List<LlamaSharpMessage> history, float temperature, int maxTokens,
            AsyncLock chatLock, PerfMonitor monitor, Func<String, Task> onVisible, CancellationToken cancel)
        {
            var llm = Llm;
            //  Remove the oldest messages until the prompt (and some output) fits in the context
            var ctx = ContextSize;
            var reserve = Math.Min(Math.Max(maxTokens, 256), ctx / 4);
            int first = 0;
            String prompt;
            int promptTokens;
            for (; ; )
            {
                prompt = Format(systemPrompt, tools, history, first);
                promptTokens = CountTokens(prompt, true);
                if ((promptTokens + reserve) <= ctx)
                    break;
                //  Keep at least the last message, and start with a user message (that isn't a tool response)
                int next = first + 1;
                var hl = history.Count;
                while ((next < hl) && ((history[next].Role != RoleUser) || history[next].IsToolResponse))
                    ++next;
                if (next >= hl)
                    break;
                first = next;
            }
            //  Keep a small margin, so that the executor never runs out of context
            var maxOut = ctx - promptTokens - ContextMargin;
            if (maxTokens > 0)
                maxOut = Math.Min(maxOut, maxTokens);
            if (maxOut <= 0)
                return new InferResult
                {
                    PromptTokens = promptTokens,
                    Error = String.Concat("Error: The prompt (", promptTokens.ToString(), " tokens) doesn't fit in the context (", ctx.ToString(), " tokens)."),
                };

            using var sampler = new DefaultSamplingPipeline
            {
                Temperature = temperature,
                TopK = llm.TopK,
                TopP = llm.TopP,
                MinP = llm.MinP,
                RepeatPenalty = llm.RepeatPenalty,
            };
            var ip = new InferenceParams
            {
                MaxTokens = maxOut,
                DecodeSpecialTokens = llm.DecodeSpecialTokens,
                SamplingPipeline = sampler,
            };
            if (AntiPrompts != null)
                ip.AntiPrompts = AntiPrompts;
            var raw = new StringBuilder();
            String visible = "";
            int outTokens = 0;
            using (var _ = await (chatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
            using (var _l = await Lock.Lock().ConfigureAwait(false))
            {
                using var _m = monitor?.Track(nameof(StatelessExecutor.InferAsync));
                //  An executor creates a new context for each inference, but can't be used concurrently
                if (!Executors.TryTake(out var executor))
                    executor = new StatelessExecutor(Weights, Params)
                    {
                        ApplyTemplate = false,
                    };
                try
                {
                    await foreach (var t in executor.InferAsync(prompt, ip, cancel).ConfigureAwait(false))
                    {
                        ++outTokens;
                        raw.Append(t);
                        if (onVisible == null)
                            continue;
                        var v = GetVisibleText(raw.ToString());
                        if (v == visible)
                            continue;
                        visible = v;
                        await onVisible(v).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Executors.Add(executor);
                }
            }
            return new InferResult
            {
                Output = RemoveThinking(RemoveEndTokens(raw.ToString())).Trim(),
                PromptTokens = promptTokens,
                OutTokens = outTokens,
                IsTruncated = outTokens >= maxOut,
            };
        }

        /// <summary>
        /// Run requests until the model stops calling tools.
        /// The model responses (without thinking) and tool responses are added to the history.
        /// </summary>
        /// <param name="getSystemPrompt">Function used to get the system prompt for each request (can change)</param>
        /// <param name="history">The conversation, model responses and tool results are added to this list</param>
        /// <param name="getTools">Function used to get the tools available to the model</param>
        /// <param name="temperature">The temperature to use</param>
        /// <param name="exec">Function used to execute tool calls</param>
        /// <param name="chatLock">Optional lock to limit concurrency</param>
        /// <param name="monitor">Optional performance monitor</param>
        /// <param name="debug">Optional debug tracking</param>
        /// <param name="onUsage">Optional usage callback (called once)</param>
        /// <param name="onText">If non null, this is called with the accumulated text whenever it changes</param>
        /// <param name="afterTools">Optional callback after each batch of tool calls</param>
        /// <param name="text">The accumulated text output (from all requests) is appended to this</param>
        /// <returns>null if successful, else an error message</returns>
        internal async Task<String> Run(Func<Task<String>> getSystemPrompt, List<LlamaSharpMessage> history, Func<AiTool[]> getTools, float temperature,
            Func<IReadOnlyList<AiFunctionCall>, Task<String[]>> exec,
            AsyncLock chatLock, PerfMonitor monitor, AiDebugMessage debug, Func<String, long, long, Task> onUsage,
            Func<String, Task> onText, Func<Task> afterTools, StringBuilder text)
        {
            long totalIn = 0;
            long totalOut = 0;
            int toolRounds = 0;
            String prevSig = null;
            int repeats = 0;
            var llm = Llm;
            try
            {
                for (; ; )
                {
                    var tools = llm.SupportTools ? getTools() : null;
                    var systemPrompt = await getSystemPrompt().ConfigureAwait(false);
                    var textStart = text.Length;
                    Func<String, Task> onVisible = onText == null ? null : v =>
                    {
                        text.Length = textStart;
                        text.Append(v);
                        return onText(text.ToString());
                    };
                    var r = await Infer(systemPrompt, tools, history, temperature, llm.MaxTokens, chatLock, monitor, onVisible, default).ConfigureAwait(false);
                    if (r.Error != null)
                        return r.Error;
                    totalIn += r.PromptTokens;
                    totalOut += r.OutTokens;
                    if (debug != null)
                    {
                        debug.InputTokenCount += r.PromptTokens;
                        debug.OutputTokenCount += r.OutTokens;
                    }
                    var output = r.Output;
                    text.Length = textStart;
                    text.Append(GetVisibleText(output));
                    List<String> errors = null;
                    var calls = llm.SupportTools ? GetToolCalls(output, out errors) : null;
                    if (output.Length > 0)
                        history.Add(new LlamaSharpMessage(RoleAssistant, output, calls != null));
                    if (r.IsTruncated)
                        return "Error: Incomplete model output due to MaxTokens parameter or token limit exceeded.";
                    if (calls == null)
                        return null;
                    if (++toolRounds > MaxToolRounds)
                        return "Error: The model made too many rounds of tool calls.";
                    //  Small models can get stuck making the same (failing) calls over and over
                    var sig = GetCallsSignature(calls);
                    repeats = sig == prevSig ? repeats + 1 : 0;
                    prevSig = sig;
                    if (repeats >= MaxRepeatedToolCalls)
                        return String.Concat("Error: The model is stuck repeating the same tool call (", calls[0].Name, ").");
                    ResolveToolNames(calls, errors, tools);
                    //  Only execute valid calls, invalid calls gets an error message back
                    var res = new String[calls.Count];
                    List<AiFunctionCall> valid = null;
                    for (int i = 0; i < res.Length; ++i)
                    {
                        res[i] = errors[i];
                        if (errors[i] == null)
                            (valid ??= new List<AiFunctionCall>()).Add(calls[i]);
                    }
                    if (valid != null)
                    {
                        var vres = await exec(valid).ConfigureAwait(false);
                        int vi = 0;
                        for (int i = 0; i < res.Length; ++i)
                            if (errors[i] == null)
                                res[i] = vres[vi++];
                    }
                    history.Add(CreateToolResponses(calls, res));
                    if (afterTools != null)
                        await afterTools().ConfigureAwait(false);
                }
            }
            finally
            {
                if (onUsage != null)
                    await onUsage(Name, totalIn, totalOut).ConfigureAwait(false);
            }
        }

        #endregion//Request loop

        #region Completion

        /// <summary>
        /// The temperature used for completions if none is specified (same as the llama.cpp server)
        /// </summary>
        const float DefaultCompletionTemperature = 0.8f;

        /// <summary>
        /// Format a tool call made by the model (as the model would have written it)
        /// </summary>
        String FormatToolCall(AiCompletionToolCall call)
        {
            var args = String.IsNullOrWhiteSpace(call.Arguments) ? "{}" : call.Arguments;
            if (IsGemma4)
                return LlamaSharpGemma4.FormatCall(call.Name, args);
            String argsJson;
            try
            {
                using var d = JsonDocument.Parse(args);
                argsJson = d.RootElement.GetRawText();
            }
            catch
            {
                //  Arguments that isn't valid json is sent as a string
                argsJson = JsonSerializer.Serialize(args, ToolResponseJson);
            }
            return String.Concat(ToolCallStart, "\n{\"name\": ", JsonSerializer.Serialize(call.Name, ToolResponseJson), ", \"arguments\": ", argsJson, "}\n", ToolCallEnd);
        }

        /// <summary>
        /// Convert the messages of a completion request to the conversation format
        /// </summary>
        List<LlamaSharpMessage> GetHistory(AiCompletionRequest request)
        {
            var history = new List<LlamaSharpMessage>();
            var callNames = new Dictionary<String, String>(StringComparer.Ordinal);
            var messages = request.Messages;
            var ml = messages.Count;
            for (int i = 0; i < ml; ++i)
            {
                var m = messages[i];
                switch (m.Role)
                {
                    case AiCompletionRoles.User:
                        history.Add(CreateUserMessage(m.Text ?? "", null, m.Name));
                        break;
                    case AiCompletionRoles.Assistant:
                        {
                            var calls = m.ToolCalls;
                            var haveCalls = (calls?.Count ?? 0) > 0;
                            var sb = new StringBuilder(m.Text ?? "");
                            if (haveCalls)
                                foreach (var c in calls)
                                {
                                    if (c.Id != null)
                                        callNames[c.Id] = c.Name;
                                    if (sb.Length > 0)
                                        sb.Append('\n');
                                    sb.Append(FormatToolCall(c));
                                }
                            history.Add(new LlamaSharpMessage(RoleAssistant, sb.ToString(), haveCalls));
                        }
                        break;
                    case AiCompletionRoles.Tool:
                        {
                            //  Consecutive tool results are added as a single message
                            var calls = new List<AiFunctionCall>();
                            var res = new List<String>();
                            for (; i < ml; ++i)
                            {
                                var t = messages[i];
                                if (t.Role != AiCompletionRoles.Tool)
                                    break;
                                var name = t.Name;
                                if (String.IsNullOrEmpty(name) && (t.ToolCallId != null))
                                    callNames.TryGetValue(t.ToolCallId, out name);
                                calls.Add(new AiFunctionCall(name ?? "", null));
                                res.Add(t.Text ?? "");
                            }
                            --i;
                            history.Add(CreateToolResponses(calls, res.ToArray()));
                        }
                        break;
                }
            }
            return history;
        }

        /// <summary>
        /// Complete a conversation (stateless), tool calls are returned (not executed)
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="chatLock">Optional lock to limit concurrency</param>
        /// <param name="monitor">Optional performance monitor</param>
        /// <param name="onText">Optional callback with the accumulated text response whenever it changes</param>
        /// <returns>The result</returns>
        internal async Task<AiCompletionResult> Complete(AiCompletionRequest request, AsyncLock chatLock, PerfMonitor monitor, Func<String, Task> onText)
        {
            var llm = Llm;
            AiTool[] tools = null;
            var rt = request.Tools;
            if (llm.SupportTools && (request.ToolChoice != AiCompletionToolChoices.None) && ((rt?.Count ?? 0) > 0))
                tools = rt.Select(AiTool.CreateExternal).ToArray();
            else if ((request.ToolChoice == AiCompletionToolChoices.Required) && ((rt?.Count ?? 0) > 0))
                throw new AiCompletionException("The model " + Name.ToQuoted() + " doesn't support tools", "tools_not_supported");
            var systemPrompt = request.SystemPrompt;
            if ((tools != null) && (request.ToolChoice == AiCompletionToolChoices.Required))
                systemPrompt = String.Concat(systemPrompt, String.IsNullOrEmpty(systemPrompt) ? "" : "\n\n", "You must call at least one of the tools.");
            var maxTokens = request.MaxTokens ?? llm.MaxTokens;
            var r = await Infer(systemPrompt, tools, GetHistory(request), request.Temperature ?? DefaultCompletionTemperature, maxTokens, chatLock, monitor, onText, request.Cancel).ConfigureAwait(false);
            if (r.Error != null)
                throw new AiCompletionException(r.Error.Substring(7), "context_length_exceeded");
            var output = r.Output;
            List<AiCompletionToolCall> toolCalls = null;
            if (tools != null)
            {
                var calls = GetToolCalls(output, out var errors);
                if (calls != null)
                {
                    ResolveToolNames(calls, errors, tools);
                    //  Calls that couldn't be parsed or to unknown tools can't be executed by the caller, so they are dropped
                    for (int i = 0; i < calls.Count; ++i)
                    {
                        if (errors[i] != null)
                            continue;
                        (toolCalls ??= new List<AiCompletionToolCall>()).Add(new AiCompletionToolCall
                        {
                            Id = "call_" + Guid.NewGuid().ToString("N").Substring(0, 24),
                            Name = calls[i].Name,
                            Arguments = calls[i].Arguments?.ToString() ?? "{}",
                        });
                    }
                }
            }
            return new AiCompletionResult
            {
                Model = Name,
                Text = GetVisibleText(output),
                ToolCalls = toolCalls,
                FinishReason = toolCalls != null ? AiCompletionFinishReasons.ToolCalls : (r.IsTruncated ? AiCompletionFinishReasons.Length : AiCompletionFinishReasons.Stop),
                InputTokens = r.PromptTokens,
                OutputTokens = r.OutTokens,
            };
        }

        #endregion//Completion
    }
}
