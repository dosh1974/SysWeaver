using OpenAI.Chat;
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

    /// <summary>
    /// A chat session using the OpenAI chat completions API
    /// </summary>
    public sealed class OpenAiCompletionsChatSession : AiChatSessionBase, IOpenAiChatSession
    {
        public OpenAiChatApi ChatApi => OpenAiChatApi.Completions;

        internal OpenAiCompletionsChatSession(bool isPrivate, ChatClient c, OpenAiSessionParams p, IAiToolCache toolCache, String joinAuth, String clearAuth, PerfMonitor monitor, AsyncLock chatLock, IAiMemory memory)
            : this(isPrivate, c, p, OpenAiModels.GetOptions(p.Model), toolCache, joinAuth, clearAuth, monitor, chatLock, memory)
        {
        }

        OpenAiCompletionsChatSession(bool isPrivate, ChatClient c, OpenAiSessionParams p, OpenAiModels.Opt o, IAiToolCache toolCache, String joinAuth, String clearAuth, PerfMonitor monitor, AsyncLock chatLock, IAiMemory memory)
            : base(isPrivate, p.Model, o.Temp, o.System, o.PTools, toolCache, joinAuth, clearAuth, monitor, chatLock, memory, OpenAiService.IconRoot, "openai.svg")
        {
            CanReason = o.CanReason;
            if (CanReason)
                ReasonigLevel = OpenAiModels.ChatReasoningLevels[(int)(p.Reasoning ?? AiReasoning.Low)];
            if (o.HaveTiers)
                Tier = OpenAiModels.ChatTiers[(int)(p.Tier ?? OpenAiServiceTier.Auto)];
            Client = c;
        }

        #region Options

        readonly ChatClient Client;

        volatile ChatCompletionOptions Options;

#pragma warning disable OPENAI001

        readonly bool CanReason;
        readonly ChatReasoningEffortLevel? ReasonigLevel;
        readonly ChatServiceTier? Tier;

        protected override void OnToolsChanged()
        {
            Options = null;
        }

        ChatCompletionOptions CreateOptions()
        {
            var options = Options;
            if (options != null)
                return options;
            options = OpenAiModels.CreateChatOptions(GetTools(), HaveTemperature, Temperature, CanReason, ReasonigLevel, Tier, SupportParallelToolCalls);
            Options = options;
            return options;
        }

#pragma warning restore OPENAI001

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
                                new SystemChatMessage(value)
                            :
                                new UserChatMessage(value));
        }

        public ChatMessage MsgSystemPrompt { get; private set; }

        public readonly List<ChatMessage> ApiMessages = new List<ChatMessage>();

        IEnumerable<ChatMessage> EnumMessages(String p)
        {
            if (p != null)
                SystemPrompt = p;
            var m = MsgSystemPrompt;
            if (m != null)
                yield return m;
            foreach (var x in ApiMessages)
                yield return x;
        }

        async Task AiCalls(IReadOnlyList<ChatToolCall> tcs, HttpServerRequest request, AiDebugMessage debugMsg, Action<String> onToolCalls, List<ChatMessage> messages)
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
                messages.Add(new ToolChatMessage(tcs[i].Id, res[i]));
        }

        public override async Task<String> Query(String text, AiDebugMessage debug = null, Func<String, long, long, Task> onUsage = null, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData = null)
        {
            using var _a = Monitor?.Track(nameof(Query));
            List<ChatMessage> messages = new List<ChatMessage>(2);
            var s = MsgSystemPrompt;
            if (s != null)
                messages.Add(s);
            var um = new UserChatMessage(text);
            if (extraData != null)
                foreach (var x in extraData)
                    um.Content.Add(OpenAiModels.ToChatPart(x.Item1));
            messages.Add(um);
            return await InternalComplete(messages, messages, null, debug, onUsage, false).ConfigureAwait(false);
        }

        public override async Task<String> Complete(String text, HttpServerRequest request, AiDebugMessage debug = null, String from = null, Func<String, long, long, Task> onUsage = null)
        {
            using var _a = Monitor?.Track(nameof(Complete));
            var messages = ApiMessages;
            var um = new UserChatMessage(text);
            if (!String.IsNullOrEmpty(from))
                um.ParticipantName = from;
            messages.Add(um);
            return await InternalComplete(null, messages, request, debug, onUsage, true).ConfigureAwait(false);
        }

        /// <summary>
        /// Run (non-streaming) requests until the model stops calling tools
        /// </summary>
        /// <param name="input">The input messages, if null the system prompt (built from the session) and ApiMessages are used</param>
        /// <param name="messages">The list to add response messages and tool results to</param>
        /// <param name="request">The request (null for queries)</param>
        /// <param name="debug">Optional debug tracking</param>
        /// <param name="onUsage">Optional usage callback</param>
        /// <param name="useLock">True to use the chat lock</param>
        async Task<String> InternalComplete(List<ChatMessage> input, List<ChatMessage> messages, HttpServerRequest request, AiDebugMessage debug, Func<String, long, long, Task> onUsage, bool useLock)
        {
            var options = CreateOptions();
            var session = request?.Session;
            long totalIn = 0;
            long totalOut = 0;
            var model = Model;
            for (; ; )
            {
                IEnumerable<ChatMessage> mm = input;
                if (mm == null)
                    mm = EnumMessages(session == null ? null : await BuildSystemPrompt(session).ConfigureAwait(false));
                ClientResult<ChatCompletion> r;
                using (var _b = await ((useLock ? ChatLock : null)?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
                {
                    using var _c = Monitor?.Track(nameof(Client.CompleteChatAsync));
                    r = await Client.CompleteChatAsync(mm, options).ConfigureAwait(false);
                }
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
                            await AiCalls(tcs, request, debug, null, messages).ConfigureAwait(false);
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
            var um = new UserChatMessage(text);
            if (extraData != null)
                foreach (var x in extraData)
                    um.Content.Add(OpenAiModels.ToChatPart(x.Item1));
            if (!String.IsNullOrEmpty(from))
                um.ParticipantName = from;
            messages.Add(um);
            var options = CreateOptions();
            StringBuilder link = new StringBuilder();
            if (options.Tools.Count > 0)
                SetToolContext(request, link, saveFile, saveData);
            int linkLen = link.Length;
            long totalIn = 0;
            long totalOut = 0;
            StringBuilder sb = new StringBuilder();
            var firstMessage = messages.Count;
            var session = request.Session;
            var client = Client;
            var model = Model;
            for (; ; )
            {
                StringBuilder e = new StringBuilder();
                Dictionary<String, Tuple<int, String>> toolCallIndices = new Dictionary<string, Tuple<int, string>>(StringComparer.Ordinal);
                List<StringBuilder> toolCalls = new List<StringBuilder>();
                var p = await BuildSystemPrompt(session).ConfigureAwait(false);
                var mm = EnumMessages(p);
                using (var _ = await (ChatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
                {
                    using var _b = Monitor?.Track(nameof(Client.CompleteChatStreaming));
                    //                    await foreach (var sp in Client.CompleteChatStreamingAsync(mm, options))
                    ChatTokenUsage usage = null;
                    foreach (var sp in client.CompleteChatStreaming(mm, options))
                    {
                        usage = usage ?? sp.Usage;
                        if (!String.IsNullOrEmpty(sp.RefusalUpdate))
                            e.Append(sp.RefusalUpdate);
                        if (sp.ContentUpdate != null)
                        {
                            bool changed = false;
                            foreach (var x in sp.ContentUpdate)
                            {
                                switch (x.Kind)
                                {
                                    case ChatMessageContentPartKind.Text:
                                        var t = x.Text;
                                        if (!String.IsNullOrEmpty(t))
                                        {
                                            sb.Append(x.Text);
                                            changed = true;
                                        }
                                        break;
                                }
                            }
                            if (changed)
                                await onUpdate(sb.ToString(), link?.ToString()).ConfigureAwait(false);
                        }
                        var tcs = sp.ToolCallUpdates;
                        if (tcs != null)
                        {
                            foreach (var x in tcs)
                            {
                                var tid = x.ToolCallId;
                                var index = x.Index;
                                if (tid != null)
                                {
                                    if (!toolCallIndices.TryGetValue(tid, out var call))
                                    {
                                        call = new Tuple<int, string>(index, x.FunctionName);
                                        toolCallIndices.Add(tid, call);
                                    }
                                }
                                while (index >= toolCalls.Count)
                                    toolCalls.Add(new StringBuilder());



                                if (x.FunctionArgumentsUpdate != null)
                                {
                                    using (var ts = x.FunctionArgumentsUpdate.ToStream())
                                        if (ts.Length <= 0)
                                            continue;
                                    toolCalls[index].Append(x.FunctionArgumentsUpdate.ToString());
                                }
                            }
                        }
                    }

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
                if (sb.Length > 0)
                {
                    var ext = sb.ToString();
                    messages.Add(new AssistantChatMessage(ext));
                }
                var tcl = toolCalls.Count;
                if (tcl <= 0)
                    break;
                ChatToolCall[] newCalls = new ChatToolCall[tcl];
                foreach (var x in toolCallIndices)
                {
                    var val = x.Value;
                    var index = val.Item1;
                    var vb = toolCalls[index];
                    newCalls[index] = ChatToolCall.CreateFunctionToolCall(x.Key, val.Item2, vb.Length > 0 ? BinaryData.FromString(vb.ToString()) : null);
                }
                messages.Add(new AssistantChatMessage(newCalls));
                await AiCalls(newCalls, request, debug, onTools, ApiMessages).ConfigureAwait(false);
                var newL = link.Length;
                if (newL != linkLen)
                {
                    linkLen = newL;
                    await onUpdate(sb.ToString(), newL > 0 ? link.ToString() : null).ConfigureAwait(false);
                }
                onTools?.Invoke(null);
            }
            //  Remove tool call messages
            if (AutoRemoveToolCalls)
            {
                int output = firstMessage;
                var ml = messages.Count;
                for (int i = firstMessage; i < ml; ++i)
                {
                    var m = messages[i] as AssistantChatMessage;
                    if (m == null)
                        continue;
                    if (m.ToolCalls.Count > 0)
                        continue;
                    messages[output] = m;
                    ++output;
                }
                if (output < ml)
                    messages.RemoveRange(output, ml - output);
            }
            if (onUsage != null)
                await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
            if ((sb.Length > 0) || (link.Length > 0))
                return null;
            return "No text response!";
        }

        public override void ClearApiMessages() => ApiMessages.Clear();

        protected override void WriteConversation(Utf8JsonWriter writer)
        {
            var opt = ModelReaderWriterOptions.Json;
            var tools = GetTools();
            if (tools.Length > 0)
            {
                writer.WritePropertyName("tools");
                writer.WriteStartArray();
                foreach (var x in tools)
                    (OpenAiModels.GetChatTool(x) as IJsonModel<ChatTool>).Write(writer, opt);
                writer.WriteEndArray();
            }
            writer.WritePropertyName("messages");
            writer.WriteStartArray();
            var t = MsgSystemPrompt;
            if (t != null)
                (t as IJsonModel<ChatMessage>).Write(writer, opt);
            foreach (IJsonModel<ChatMessage> m in ApiMessages)
                m.Write(writer, opt);
            writer.WriteEndArray();
        }

    }

}
