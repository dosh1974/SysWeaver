using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
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
        internal LlamaSharpModel(LlamaSharpLlm llm, String filePath)
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
            Weights = LLamaWeights.LoadFromFile(mp);
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
            AntiPrompts = BuiltInTemplate == TemplateGemma4 ? ["<turn|>"] : null;
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

        /// <summary>
        /// Name of the built in Gemma 4 template (can be used as the ChatTemplate)
        /// </summary>
        internal const String TemplateGemma4 = "gemma4";

        /// <summary>
        /// The built in format to use (llama.cpp can't run jinja templates, it only knows a fixed set of templates), null to let llama.cpp apply the template
        /// </summary>
        readonly String BuiltInTemplate;

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
        /// <param name="systemPrompt">The system prompt (including tool descriptions), can be null</param>
        /// <param name="messages">The messages</param>
        /// <param name="first">Index of the first message to include</param>
        /// <returns>The prompt</returns>
        String Format(String systemPrompt, IReadOnlyList<LlamaSharpMessage> messages, int first)
        {
            var bt = BuiltInTemplate;
            var t = bt == null ? CreateTemplate() : null;
            var sb = bt == null ? null : new StringBuilder();
            void Add(String role, String content)
            {
                if (t != null)
                    t.Add(role, content);
                else
                    AddGemma4(sb, role, content);
            }
            var haveSystem = !String.IsNullOrEmpty(systemPrompt);
            var systemAsUser = haveSystem && (!Llm.SupportSystemRole);
            if (haveSystem && (!systemAsUser))
                Add(RoleSystem, systemPrompt);
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
                Add(m.Role, c);
            }
            if (systemAsUser)
                Add(RoleUser, systemPrompt);
            if (t == null)
            {
                //  Start a model turn with an empty thought channel (thinking disabled)
                sb.Append("<|turn>model\n<|channel>thought\n<channel|>");
                return sb.ToString();
            }
            t.AddAssistant = true;
            return Encoding.UTF8.GetString(t.Apply());
        }

        /// <summary>
        /// Add a message using the Gemma 4 format (the BOS token is added by the tokenizer)
        /// </summary>
        static void AddGemma4(StringBuilder sb, String role, String content)
            => sb
                .Append("<|turn>").Append(role == RoleAssistant ? "model" : role).Append('\n')
                .Append(content.Trim())
                .Append("<turn|>\n");

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
        const String ThinkStart = "<think>";
        const String ThinkEnd = "</think>";

        /// <summary>
        /// Start and end tags of thinking (Gemma 4 uses a thought channel)
        /// </summary>
        static readonly ValueTuple<String, String>[] ThinkTags = [(ThinkStart, ThinkEnd), ("<|channel>", "<channel|>")];

        /// <summary>
        /// Tokens that some models outputs at the end of a message (if special tokens are decoded)
        /// </summary>
        static readonly String[] EndTokens = ["<|im_end|>", "<|eot_id|>", "<end_of_turn>", "<turn|>", "<|end|>", "<|endoftext|>", "</s>", "<|return|>"];

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
                var b = s.IndexOf(ToolCallStart, StringComparison.Ordinal);
                if (b < 0)
                    break;
                var e = s.IndexOf(ToolCallEnd, b, StringComparison.Ordinal);
                if (e < 0)
                {
                    s = s.Substring(0, b);
                    break;
                }
                s = String.Concat(s.AsSpan(0, b), s.AsSpan(e + ToolCallEnd.Length));
            }
            //  Hide a partial tag at the end (while streaming)
            var lt = s.LastIndexOf('<');
            if (lt >= 0)
            {
                var tail = s.AsSpan(lt);
                if (ToolCallStart.AsSpan().StartsWith(tail, StringComparison.Ordinal) || ThinkStart.AsSpan().StartsWith(tail, StringComparison.Ordinal))
                    s = s.Substring(0, lt);
            }
            return s.Trim();
        }

        /// <summary>
        /// Parse the tool calls in some model output
        /// </summary>
        internal static List<AiFunctionCall> GetToolCalls(String output)
        {
            List<AiFunctionCall> calls = null;
            int pos = 0;
            for (; ; )
            {
                var b = output.IndexOf(ToolCallStart, pos, StringComparison.Ordinal);
                if (b < 0)
                    break;
                b += ToolCallStart.Length;
                var e = output.IndexOf(ToolCallEnd, b, StringComparison.Ordinal);
                var json = (e < 0 ? output.Substring(b) : output.Substring(b, e - b)).Trim();
                pos = e < 0 ? output.Length : e + ToolCallEnd.Length;
                calls ??= new List<AiFunctionCall>();
                calls.Add(ParseToolCall(json));
            }
            return calls;
        }

        /// <summary>
        /// The name used for a tool call that couldn't be parsed (the model gets an error back)
        /// </summary>
        const String InvalidToolCall = "invalid_tool_call";

        static AiFunctionCall ParseToolCall(String json)
        {
            try
            {
                using var d = JsonDocument.Parse(json);
                var r = d.RootElement;
                if (r.ValueKind != JsonValueKind.Object)
                    return new AiFunctionCall(InvalidToolCall, BinaryData.FromString("{}"));
                String name = null;
                if (r.TryGetProperty("name", out var n) && (n.ValueKind == JsonValueKind.String))
                    name = n.GetString();
                if (String.IsNullOrEmpty(name))
                    return new AiFunctionCall(InvalidToolCall, BinaryData.FromString("{}"));
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
            catch
            {
                return new AiFunctionCall(InvalidToolCall, BinaryData.FromString("{}"));
            }
        }

        /// <summary>
        /// Don't escape non-ascii, quotes etc (the json is only read by the model)
        /// </summary>
        static readonly JsonSerializerOptions ToolResponseJson = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        static LlamaSharpMessage CreateToolResponses(IReadOnlyList<AiFunctionCall> calls, String[] res)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < calls.Count; ++i)
            {
                if (i > 0)
                    sb.Append('\n');
                var r = calls[i].Name == InvalidToolCall ? "Error: The tool call wasn't valid json, use the format: {\"name\": <function-name>, \"arguments\": <args-json-object>}" : res[i];
                sb.Append("<tool_response>\n");
                sb.Append(JsonSerializer.Serialize(new Dictionary<String, String> { { "name", calls[i].Name }, { "content", r } }, ToolResponseJson));
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
            var llm = Llm;
            try
            {
                for (; ; )
                {
                    var tools = llm.SupportTools ? getTools() : null;
                    var systemPrompt = AddTools(await getSystemPrompt().ConfigureAwait(false), tools);

                    //  Remove the oldest messages until the prompt (and some output) fits in the context
                    var ctx = ContextSize;
                    var reserve = Math.Min(Math.Max(llm.MaxTokens, 256), ctx / 4);
                    int first = 0;
                    String prompt;
                    int promptTokens;
                    for (; ; )
                    {
                        prompt = Format(systemPrompt, history, first);
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
                    var maxOut = ctx - promptTokens;
                    if (llm.MaxTokens > 0)
                        maxOut = Math.Min(maxOut, llm.MaxTokens);
                    if (maxOut <= 0)
                        return String.Concat("Error: The prompt (", promptTokens.ToString(), " tokens) doesn't fit in the context (", ctx.ToString(), " tokens).");

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
                    var textStart = text.Length;
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
                            await foreach (var t in executor.InferAsync(prompt, ip).ConfigureAwait(false))
                            {
                                ++outTokens;
                                raw.Append(t);
                                if (onText == null)
                                    continue;
                                var v = GetVisibleText(raw.ToString());
                                if (v == visible)
                                    continue;
                                visible = v;
                                text.Length = textStart;
                                text.Append(v);
                                await onText(text.ToString()).ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            Executors.Add(executor);
                        }
                    }
                    totalIn += promptTokens;
                    totalOut += outTokens;
                    if (debug != null)
                    {
                        debug.InputTokenCount += promptTokens;
                        debug.OutputTokenCount += outTokens;
                    }
                    var output = RemoveThinking(RemoveEndTokens(raw.ToString())).Trim();
                    text.Length = textStart;
                    text.Append(GetVisibleText(output));
                    var calls = llm.SupportTools ? GetToolCalls(output) : null;
                    if (output.Length > 0)
                        history.Add(new LlamaSharpMessage(RoleAssistant, output, calls != null));
                    if (calls == null)
                        return outTokens >= maxOut ? "Error: Incomplete model output due to MaxTokens parameter or token limit exceeded." : null;
                    if (++toolRounds > MaxToolRounds)
                        return "Error: The model made too many rounds of tool calls.";
                    var res = await exec(calls).ConfigureAwait(false);
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
    }
}
