using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    public class OpenAiQuerySession
    {
        public OpenAiQuerySession(ChatClient c, String model, IOpenAiToolCache toolCache = null, PerfMonitor monitor = null)
        {
            Model = model;
            var o = OpenAiTools.GetOptions(model);
            HaveTemperature = o.Temp;
            SupportSystemRole = o.System;
            SupportParallelToolCalls = o.PTools;
            Client = c;
            Monitor = monitor;
            ToolCache = toolCache;
        }

        protected readonly ChatClient Client;
        protected readonly PerfMonitor Monitor;
        protected readonly IOpenAiToolCache ToolCache;
        public readonly String Model;
        public readonly bool HaveTemperature;
        public readonly bool SupportSystemRole;
        public readonly bool? SupportParallelToolCalls;

        /// <summary>
        /// Temperature of the AI model, lower is more cosistent and less random [0, 2] (1 is default).
        /// </summary>
        public float Temperature = 0.2f;

        public String SystemPrompt
        {
            get =>
                MsgSystemPrompt == null ? null : String.Join('\n', MsgSystemPrompt.Content.Select(x => x.Text));
            set =>
                MsgSystemPrompt =
                    String.IsNullOrEmpty(value)
                    ?
                        null
                    :
                        (SupportSystemRole
                            ?
                                new SystemChatMessage(value)
                            :
                                new UserChatMessage(value));
        }

        public ChatMessage MsgSystemPrompt { get; private set; }

        internal readonly Dictionary<String, OpenAiTool> Tools = new Dictionary<string, OpenAiTool>(StringComparer.Ordinal);

        public IEnumerable<KeyValuePair<String, OpenAiTool>> ALlTools => Tools;

        /// <summary>
        /// Add an API endpoint that the AI can use in this session
        /// </summary>
        /// <param name="apiName">Name of tha api local url: ex: "Api/auth/GetUser"</param>
        /// <param name="fn">An optional function name (as shown to the AI).
        /// null - will use the underlaying method name (or as specified by the OpenAiToolName attribute) in combination with the declaring type name.
        /// "" - will user the full apiName, but with '/' replaced by '_'.
        /// </param>
        /// <returns>True if the tool was added</returns>
        public bool AddTool(String apiName, String fn = null)
        {
            var tool = ToolCache.GetTool(apiName, fn);
            if (tool == null)
                return false;
            Tools.TryAdd(tool.Name, tool);
            return true;
        }

        /// <summary>
        /// Add a tool from some registered tool
        /// </summary>
        /// <param name="name">The name of the tool</param>
        /// <returns>True if the tool was added</returns>
        public bool AddRegistredTool(String name)
        {
            var tool = ToolCache.GetRegisteredTool(name);
            if (tool == null)
                return false;
            Tools.TryAdd(tool.Name, tool);
            return true;
        }

        /// <summary>
        /// Add a method that the AI can use in this session.
        /// </summary>
        /// <param name="instance">Object instance</param>
        /// <param name="method">The method</param>
        /// <param name="fn">Optional function name, default will be instance type name _ method name</param>
        /// <param name="perfMonitor">An optional performance monitor</param>
        /// <param name="defaultAuth">The default auth to use if there is no WebApiAuthAttribute on the method</param>
        /// <param name="defaultCachedCompression">The default compression when there is no WebApiCompressionAttribute on the method</param>
        /// <param name="defaultCompression">The default compression for uncached cached methods when there is no WebApiCompressionAttribute on the method</param>
        /// <returns></returns>
        public bool AddTool(Object instance, MethodInfo method, String fn = null, PerfMonitor perfMonitor = null, String defaultAuth = ApiHttpEntry.DefaultAuth, String defaultCachedCompression = ApiHttpEntry.DefaultCachedCompression, String defaultCompression = ApiHttpEntry.DefaultCompression)
        {
            var tool = ToolCache.GetTool(instance, method, fn, perfMonitor, defaultAuth, defaultCachedCompression, defaultCompression);
            if (tool == null)
                return false;
            Tools.TryAdd(tool.Name, tool);
            return true;
        }

        async Task<OpenAiCallInstance> AiCall(String name, BinaryData b, HttpServerRequest request, Dictionary<String, int> callIcons)
        {
            var start = DateTime.UtcNow;
            if (!Tools.TryGetValue(name, out var tool))
                return new OpenAiCallInstance(name, b, start, DateTime.UtcNow, new Exception("The tool " + name.ToQuoted() + " is unknown!"));
            if (!request.Session.IsValid(tool.Auth))
                return new OpenAiCallInstance(name, b, start, DateTime.UtcNow, new Exception("The requesting user is not allowed to use the tool " + name.ToQuoted()));

            var icon = tool.Icon;
            callIcons.TryGetValue(icon, out var c);
            callIcons[icon] = c + 1;
            try
            {
                using var xx = Monitor?.Track("AiCall." + name);
                var res = await tool.Invoke(b, request).ConfigureAwait(false);
                return new OpenAiCallInstance(name, b, start, DateTime.UtcNow, res);
            }
            catch (Exception e)
            {
                return new OpenAiCallInstance(name, b, start, DateTime.UtcNow, e);
            }
        }

        protected async Task AiCalls(IReadOnlyList<ChatToolCall> tcs, HttpServerRequest request, OpenAiDebugMessage debugMsg, Action<String> onToolCalls, List<ChatMessage> messages)
        {
            var tcl = tcs.Count;
            if (tcl <= 0)
                return;
            debugMsg?.StartBatch();
            Task<OpenAiCallInstance>[] tasks = new Task<OpenAiCallInstance>[tcl];
            Dictionary<String, int> icons = new Dictionary<string, int>();
            for (int i = 0; i < tcl; ++i)
            {
                var tc = tcs[i];
                tasks[i] = AiCall(tc.FunctionName, tc.FunctionArguments, request, icons);
            }
            StringBuilder sb = new StringBuilder();
            bool prevIsDigit = false;
            foreach (var x in icons.OrderByDescending(x => x.Value))
            {
                if (prevIsDigit)
                    sb.Append(' ');
                var c = x.Key;
                var v = x.Value;
                if (c == "")
                {
                    prevIsDigit = true;
                    sb.Append('x').Append(v);
                }
                else
                {
                    sb.Append(x.Key);
                    prevIsDigit = v > 1;
                    if (prevIsDigit)
                        sb.Append('x').Append(v);
                }
            }
            onToolCalls?.Invoke(sb.ToString());
            await Task.WhenAll(tasks).ConfigureAwait(false);

            for (int i = 0; i < tcl; ++i)
            {
                var tc = tcs[i];
                var res = tasks[i].Result;
                debugMsg?.AddCall(res);
                var ex = res.Ex;
                if (ex != null)
                    messages?.Add(new ToolChatMessage(tc.Id, "Execution failed: " + ex.Message));
                else
                    messages?.Add(new ToolChatMessage(tc.Id, res.Ret));
            }
            debugMsg?.EndBatch();
        }


        /// <summary>
        /// Perform a query
        /// </summary>
        /// <param name="text">The query text</param>
        /// <param name="debug">An optional obejct used to track tool calls etc</param>
        /// <param name="onUsage">An optional function to call when token in/out usage is changed</param>
        /// <returns>The Ai response, typically MD encoded text</returns>
        /// <exception cref="Exception"></exception>
        public async Task<String> Query(String text, OpenAiDebugMessage debug = null, Func<String, long, long, Task> onUsage = null)
        {
            using var _a = Monitor?.Track(nameof(Query));
            List<ChatMessage> messages = new List<ChatMessage>(2);
            var s = MsgSystemPrompt;
            if (s != null)
                messages.Add(s);
            var um = new UserChatMessage(text);
            messages.Add(um);
            ChatCompletionOptions options = new ChatCompletionOptions();
            if (HaveTemperature)
                options.Temperature = Temperature;
            var tools = Tools;
            if (tools.Count > 0)
            {
                var d = options.Tools;
                foreach (var x in tools)
                    d.Add(x.Value.Tool);
                options.AllowParallelToolCalls = SupportParallelToolCalls;
            }
            long totalIn = 0;
            long totalOut = 0;
            var model = Model;
            for (; ; )
            {
                ClientResult<ChatCompletion> r;
                using (var _c = Monitor?.Track(nameof(Client.CompleteChatAsync)))
                    r = await Client.CompleteChatAsync(messages, options).ConfigureAwait(false);
                var v = r.Value;
                var e = v.Refusal;
                var usage = v.Usage;
                if (usage != null)
                {
                    totalIn += usage.InputTokenCount;
                    totalOut += usage.OutputTokenCount;
                }
                if (!String.IsNullOrEmpty(e))
                {
                    if (onUsage != null)
                        await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                    throw new Exception("Model refused to complete: " + e);
                }
                var fr = v.FinishReason;
                switch (fr)
                {
                    case ChatFinishReason.Stop:
                        messages.Add(new AssistantChatMessage(r));
                        var sb = new StringBuilder();
                        List<String> p = new List<string>(4);
                        foreach (var x in v.Content)
                        {
                            switch (x.Kind)
                            {
                                case ChatMessageContentPartKind.Text:
                                    var t = x.Text;
                                    sb.AppendLine(t);
                                    break;
                                case ChatMessageContentPartKind.Image:
                                    break;

                            }
                        }
                        if (onUsage != null)
                            await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                        return sb.ToString();
                    case ChatFinishReason.ToolCalls:
                        messages.Add(new AssistantChatMessage(r));
                        var tcs = v.ToolCalls;
                        if (tcs != null)
                            await AiCalls(tcs, null, debug, null, null).ConfigureAwait(false);
                        break;
                    case ChatFinishReason.Length:
                        if (onUsage != null)
                            await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                        return "Error: Incomplete model output due to MaxTokens parameter or token limit exceeded.";

                    case ChatFinishReason.ContentFilter:
                        if (onUsage != null)
                            await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                        return "Error: Omitted content due to a content filter flag.";

                    case ChatFinishReason.FunctionCall:
                        if (onUsage != null)
                            await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                        return "Error: Deprecated in favor of tool calls.";
                    default:
                        if (onUsage != null)
                            await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                        return "Error: " + fr;

                }

            }
        }


    }

}
