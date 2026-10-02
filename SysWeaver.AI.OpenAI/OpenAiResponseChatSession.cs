using OpenAI.Responses;
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{

#pragma warning disable OPENAI001

    /// <summary>
    /// A chat session using the OpenAI Responses API.
    /// The conversation state is kept locally (in ApiMessages) and the full history is sent on every request (nothing is stored server side).
    /// </summary>
    public sealed class OpenAiResponseChatSession : AiChatSessionBase, IOpenAiChatSession
    {
        public OpenAiChatApi ChatApi => OpenAiChatApi.Responses;

        internal OpenAiResponseChatSession(bool isPrivate, ResponsesClient c, OpenAiSessionParams p, IAiToolCache toolCache, String joinAuth, String clearAuth, PerfMonitor monitor, AsyncLock chatLock, IAiMemory memory)
            : this(isPrivate, c, p, OpenAiModels.GetOptions(p.Model), toolCache, joinAuth, clearAuth, monitor, chatLock, memory)
        {
        }

        OpenAiResponseChatSession(bool isPrivate, ResponsesClient c, OpenAiSessionParams p, OpenAiModels.Opt o, IAiToolCache toolCache, String joinAuth, String clearAuth, PerfMonitor monitor, AsyncLock chatLock, IAiMemory memory)
            : base(isPrivate, p.Model, o.Temp, o.System, o.PTools, toolCache, joinAuth, clearAuth, monitor, chatLock, memory, OpenAiService.IconRoot, "openai.svg")
        {
            CanReason = o.CanReason;
            if (CanReason)
                ReasonigLevel = OpenAiModels.ResponseReasoningLevels[(int)(p.Reasoning ?? AiReasoning.Low)];
            if (o.HaveTiers)
                Tier = OpenAiModels.ResponseTiers[(int)(p.Tier ?? OpenAiServiceTier.Auto)];
            Client = c;
        }

        #region Options

        readonly ResponsesClient Client;
        readonly bool CanReason;
        readonly ResponseReasoningEffortLevel? ReasonigLevel;
        readonly ResponseServiceTier? Tier;

        volatile ResponseTool[] ApiTools;

        protected override void OnToolsChanged()
        {
            ApiTools = null;
        }

        ResponseTool[] GetApiTools()
        {
            var t = ApiTools;
            if (t != null)
                return t;
            t = GetTools().Select(OpenAiModels.GetResponseTool).ToArray();
            ApiTools = t;
            return t;
        }

        /// <summary>
        /// Create the options for a request, the options are NOT cached since they contain the input items.
        /// </summary>
        /// <param name="inputItems">The input items to use</param>
        /// <returns></returns>
        CreateResponseOptions CreateOptions(IEnumerable<ResponseItem> inputItems)
        {
            var options = new CreateResponseOptions(Model, inputItems)
            {
                // History is kept locally, so no need to store anything on the server
                StoredOutputEnabled = false,
                ServiceTier = Tier,
            };
            if (HaveTemperature)
                options.Temperature = Temperature;
            if (CanReason)
            {
                options.ReasoningOptions = new ResponseReasoningOptions
                {
                    ReasoningEffortLevel = ReasonigLevel,
                };
                // Required to pass reasoning items back when nothing is stored server side
                options.IncludedProperties.Add(IncludedResponseProperty.ReasoningEncryptedContent);
            }
            var tools = GetApiTools();
            if (tools.Length > 0)
            {
                var d = options.Tools;
                foreach (var x in tools)
                    d.Add(x);
                options.ParallelToolCallsEnabled = SupportParallelToolCalls;
            }
            return options;
        }

        #endregion//Options

        public override String SystemPrompt
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
                                ResponseItem.CreateSystemMessageItem(value)
                            :
                                ResponseItem.CreateUserMessageItem(value));
        }

        public MessageResponseItem MsgSystemPrompt { get; private set; }

        /// <summary>
        /// The conversation history (excluding the system prompt), sent as input items on every request.
        /// </summary>
        public readonly List<ResponseItem> ApiMessages = new List<ResponseItem>();

        async Task AiCalls(IReadOnlyList<FunctionCallResponseItem> tcs, HttpServerRequest request, AiDebugMessage debugMsg, Action<String> onToolCalls, List<ResponseItem> messages)
        {
            var tcl = tcs.Count;
            if (tcl <= 0)
                return;
            var calls = new AiFunctionCall[tcl];
            for (int i = 0; i < tcl; ++i)
                calls[i] = new AiFunctionCall(tcs[i].FunctionName, tcs[i].FunctionArguments);
            var res = await AiCalls(calls, request, debugMsg, onToolCalls).ConfigureAwait(false);
            if (messages == null)
                return;
            for (int i = 0; i < tcl; ++i)
                messages.Add(ResponseItem.CreateFunctionCallOutputItem(tcs[i].CallId, res[i]));
        }

        #region Response helpers

        static MessageResponseItem CreateUserMessage(String text, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData, String from)
        {
            List<ResponseContentPart> parts = new List<ResponseContentPart>(2 + (extraData?.Count ?? 0));
            //  The responses API have no participant name, so add it as a separate text part
            if (!String.IsNullOrEmpty(from))
                parts.Add(ResponseContentPart.CreateInputTextPart("[Message from: " + from + "]"));
            parts.Add(ResponseContentPart.CreateInputTextPart(text));
            if (extraData != null)
                foreach (var x in extraData)
                    parts.Add(OpenAiModels.ToResponsePart(x.Item1));
            return ResponseItem.CreateUserMessageItem(parts);
        }

        static String GetRefusal(IEnumerable<ResponseItem> items)
        {
            StringBuilder sb = null;
            foreach (var m in items.OfType<MessageResponseItem>())
            {
                foreach (var x in m.Content)
                {
                    if (x.Kind != ResponseContentPartKind.Refusal)
                        continue;
                    var r = x.Refusal;
                    if (String.IsNullOrEmpty(r))
                        continue;
                    sb = sb ?? new StringBuilder();
                    sb.Append(r);
                }
            }
            return sb?.ToString();
        }

        static String GetText(IEnumerable<ResponseItem> items)
        {
            var sb = new StringBuilder();
            foreach (var m in items.OfType<MessageResponseItem>())
            {
                foreach (var x in m.Content)
                {
                    if (x.Kind != ResponseContentPartKind.OutputText)
                        continue;
                    sb.AppendLine(x.Text);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns an error message if the response isn't completed, else null.
        /// </summary>
        static String GetStatusError(ResponseResult v)
        {
            switch (v.Status)
            {
                case null:
                case ResponseStatus.Completed:
                    return null;
                case ResponseStatus.Incomplete:
                    var reason = v.IncompleteStatusDetails?.Reason;
                    if (reason == ResponseIncompleteStatusReason.MaxOutputTokens)
                        return "Error: Incomplete model output due to MaxTokens parameter or token limit exceeded.";
                    if (reason == ResponseIncompleteStatusReason.ContentFilter)
                        return "Error: Omitted content due to a content filter flag.";
                    return "Error: Incomplete model output: " + reason;
                case ResponseStatus.Failed:
                    var e = v.Error;
                    return e == null ? "Error: Response failed." : String.Concat("Error: ", e.Code.ToString(), " - ", e.Message);
                default:
                    return "Error: " + v.Status;
            }
        }

        #endregion//Response helpers

        /// <summary>
        /// Perform a query (the query is not added to the conversation history)
        /// </summary>
        /// <param name="text">The query text</param>
        /// <param name="debug">An optional obejct used to track tool calls etc</param>
        /// <param name="onUsage">An optional function to call when token in/out usage is changed</param>
        /// <param name="extraData">optional attachements</param>
        /// <returns>The Ai response, typically MD encoded text</returns>
        /// <exception cref="Exception"></exception>
        public override async Task<String> Query(String text, AiDebugMessage debug = null, Func<String, long, long, Task> onUsage = null, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData = null)
        {
            using var _a = Monitor?.Track(nameof(Query));
            List<ResponseItem> messages = new List<ResponseItem>(2);
            var s = MsgSystemPrompt;
            if (s != null)
                messages.Add(s);
            messages.Add(CreateUserMessage(text, extraData, null));
            return await InternalComplete(messages, messages, null, debug, onUsage, false).ConfigureAwait(false);
        }

        /// <summary>
        /// Run requests until the model stops calling tools (non-streaming).
        /// </summary>
        async Task<String> InternalComplete(IEnumerable<ResponseItem> input, List<ResponseItem> messages, HttpServerRequest request, AiDebugMessage debug, Func<String, long, long, Task> onUsage, bool useLock)
        {
            long totalIn = 0;
            long totalOut = 0;
            var model = Model;
            var session = request?.Session;
            for (; ; )
            {
                IEnumerable<ResponseItem> mm = session != null
                    ? EnumMessages(await BuildSystemPrompt(session).ConfigureAwait(false))
                    : (input ?? EnumMessages(null));
                var options = CreateOptions(mm);
                ClientResult<ResponseResult> r;
                using (var _b = await ((useLock ? ChatLock : null)?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
                {
                    using var _c = Monitor?.Track(nameof(Client.CreateResponseAsync));
                    r = await Client.CreateResponseAsync(options).ConfigureAwait(false);
                }
                var v = r.Value;
                var usage = v.Usage;
                if (usage != null)
                {
                    totalIn += usage.InputTokenCount;
                    totalOut += usage.OutputTokenCount;
                    if (debug != null)
                    {
                        debug.InputTokenCount += usage.InputTokenCount;
                        debug.OutputTokenCount += usage.OutputTokenCount;
                    }
                }
                var output = v.OutputItems;
                var e = GetRefusal(output);
                if (!String.IsNullOrEmpty(e))
                {
                    if (onUsage != null)
                        await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                    throw new Exception("Model refused to complete: " + e);
                }
                var err = GetStatusError(v);
                if (err != null)
                {
                    if (onUsage != null)
                        await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                    return err;
                }
                messages.AddRange(output);
                var tcs = output.OfType<FunctionCallResponseItem>().ToList();
                if (tcs.Count > 0)
                {
                    await AiCalls(tcs, request, debug, null, messages).ConfigureAwait(false);
                    continue;
                }
                if (onUsage != null)
                    await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                return GetText(output);
            }
        }


        IEnumerable<ResponseItem> EnumMessages(String p)
        {
            if (p != null)
                SystemPrompt = p;
            var m = MsgSystemPrompt;
            if (m != null)
                yield return m;
            foreach (var x in ApiMessages)
                yield return x;
        }


        public override async Task<String> Complete(String text, HttpServerRequest request, AiDebugMessage debug = null, String from = null, Func<String, long, long, Task> onUsage = null)
        {
            using var _a = Monitor?.Track(nameof(Complete));
            var messages = ApiMessages;
            messages.Add(CreateUserMessage(text, null, from));
            return await InternalComplete(null, messages, request, debug, onUsage, true).ConfigureAwait(false);
        }


        public override async Task<String> CompleteUpdate(String text,
            IReadOnlyList<ValueTuple<AiContentPart, String>> extraData,
            HttpServerRequest request,
            Func<String, String, Task> onUpdate,
            Func<String, String, String, String> saveFile,
            Func<Object, String> saveData,
            Action<String> onTools,
            AiDebugMessage debug,
            String from,
            Func<String, long, long, Task> onUsage
            )
        {
            using var _a = Monitor?.Track(nameof(CompleteUpdate));
            var messages = ApiMessages;
            text = AppendAttachmentUrls(text, extraData);
            messages.Add(CreateUserMessage(text, extraData, from));
            StringBuilder link = new StringBuilder();
            if (GetApiTools().Length > 0)
                SetToolContext(request, link, saveFile, saveData);
            int linkLen = link.Length;
            long totalIn = 0;
            long totalOut = 0;
            StringBuilder sb = new StringBuilder();
            var firstMessage = messages.Count;
            var session = request.Session;
            var client = Client;
            var model = Model;
            String statusError = null;
            for (; ; )
            {
                StringBuilder e = new StringBuilder();
                SortedDictionary<int, ResponseItem> doneItems = new SortedDictionary<int, ResponseItem>();
                ResponseResult final = null;
                String failed = null;
                var p = await BuildSystemPrompt(session).ConfigureAwait(false);
                var options = CreateOptions(EnumMessages(p));
                options.StreamingEnabled = true;
                using (var _ = await (ChatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
                {
                    using var _b = Monitor?.Track(nameof(Client.CreateResponseStreaming));
                    //                    await foreach (var sp in Client.CreateResponseStreamingAsync(options))
                    foreach (var sp in client.CreateResponseStreaming(options))
                    {
                        switch (sp)
                        {
                            case StreamingResponseOutputTextDeltaUpdate x:
                                if (!String.IsNullOrEmpty(x.Delta))
                                {
                                    sb.Append(x.Delta);
                                    await onUpdate(sb.ToString(), link?.ToString()).ConfigureAwait(false);
                                }
                                break;
                            case StreamingResponseRefusalDeltaUpdate x:
                                if (!String.IsNullOrEmpty(x.Delta))
                                    e.Append(x.Delta);
                                break;
                            case StreamingResponseOutputItemDoneUpdate x:
                                if (x.Item != null)
                                    doneItems[x.OutputIndex] = x.Item;
                                break;
                            case StreamingResponseCompletedUpdate x:
                                final = x.Response;
                                break;
                            case StreamingResponseIncompleteUpdate x:
                                final = x.Response;
                                break;
                            case StreamingResponseFailedUpdate x:
                                final = x.Response;
                                failed = GetStatusError(x.Response) ?? "Error: Response failed.";
                                break;
                            case StreamingResponseErrorUpdate x:
                                failed = String.Concat("Error: ", x.Code, " - ", x.Message);
                                break;
                        }
                    }

                    var usage = final?.Usage;
                    if (usage != null)
                    {
                        totalIn += usage.InputTokenCount;
                        totalOut += usage.OutputTokenCount;
                        if (debug != null)
                        {
                            debug.InputTokenCount += usage.InputTokenCount;
                            debug.OutputTokenCount += usage.OutputTokenCount;
                        }
                    }
                }
                if (e.Length > 0)
                {
                    if (onUsage != null)
                        await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                    throw new Exception("Model refused to complete: " + e.ToString());
                }
                if (failed != null)
                {
                    if (onUsage != null)
                        await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
                    throw new Exception(failed);
                }
                IList<ResponseItem> output = final?.OutputItems;
                if ((output == null) || (output.Count <= 0))
                    output = doneItems.Values.ToList();
                messages.AddRange(output);
                var newCalls = output.OfType<FunctionCallResponseItem>().ToList();
                if (newCalls.Count <= 0)
                {
                    if (final != null)
                        statusError = GetStatusError(final);
                    break;
                }
                await AiCalls(newCalls, request, debug, onTools, messages).ConfigureAwait(false);
                var newL = link.Length;
                if (newL != linkLen)
                {
                    linkLen = newL;
                    await onUpdate(sb.ToString(), newL > 0 ? link.ToString() : null).ConfigureAwait(false);
                }
                onTools?.Invoke(null);
            }
            //  Remove tool call messages (and reasoning items, since they can't be sent without their following item)
            if (AutoRemoveToolCalls)
            {
                int output = firstMessage;
                var ml = messages.Count;
                for (int i = firstMessage; i < ml; ++i)
                {
                    var m = messages[i] as MessageResponseItem;
                    if (m == null)
                        continue;
                    messages[output] = m;
                    ++output;
                }
                if (output < ml)
                    messages.RemoveRange(output, ml - output);
            }
            if (onUsage != null)
                await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
            if (statusError != null)
                return statusError;
            if ((sb.Length > 0) || (link.Length > 0))
                return null;
            return "No text response!";
        }

        public override void ClearApiMessages() => ApiMessages.Clear();

        protected override void WriteConversation(Utf8JsonWriter writer)
        {
            var opt = ModelReaderWriterOptions.Json;
            var tools = GetApiTools();
            if (tools.Length > 0)
            {
                writer.WritePropertyName("tools");
                writer.WriteStartArray();
                foreach (IJsonModel<ResponseTool> x in tools)
                    x.Write(writer, opt);
                writer.WriteEndArray();
            }
            writer.WritePropertyName("messages");
            writer.WriteStartArray();
            var t = MsgSystemPrompt;
            if (t != null)
                (t as IJsonModel<ResponseItem>).Write(writer, opt);
            foreach (IJsonModel<ResponseItem> m in ApiMessages)
                m.Write(writer, opt);
            writer.WriteEndArray();
        }
    }

#pragma warning restore OPENAI001

}
