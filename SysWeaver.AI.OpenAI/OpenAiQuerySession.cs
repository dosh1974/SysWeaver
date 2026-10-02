using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{

    /// <summary>
    /// A query session using the OpenAI chat completions API
    /// </summary>
    public class OpenAiQuerySession : AiQuerySessionBase
    {
        public OpenAiQuerySession(ChatClient c, OpenAiSessionParams p, IAiToolCache toolCache = null, PerfMonitor monitor = null, IAiMemory memory = null)
            : this(c, p, OpenAiModels.GetOptions(p.Model), toolCache, monitor, memory)
        {
        }

        OpenAiQuerySession(ChatClient c, OpenAiSessionParams p, OpenAiModels.Opt o, IAiToolCache toolCache, PerfMonitor monitor, IAiMemory memory)
            : base(p.Model, o.Temp, o.System, o.PTools, toolCache, monitor, memory)
        {
            CanReason = o.CanReason;
            if (CanReason)
                ReasonigLevel = OpenAiModels.ChatReasoningLevels[(int)(p.Reasoning ?? AiReasoning.Low)];
            if (o.HaveTiers)
                Tier = OpenAiModels.ChatTiers[(int)(p.Tier ?? OpenAiServiceTier.Auto)];
            Client = c;
        }

        volatile ChatCompletionOptions Options;

#pragma warning disable OPENAI001

        readonly bool CanReason;
        readonly ChatReasoningEffortLevel? ReasonigLevel;
        readonly ChatServiceTier? Tier;

        protected override void OnToolsChanged()
        {
            Options = null;
        }

        protected ChatCompletionOptions CreateOptions()
        {
            var options = Options;
            if (options != null)
                return options;
            options = OpenAiModels.CreateChatOptions(GetTools(), HaveTemperature, Temperature, CanReason, ReasonigLevel, Tier, SupportParallelToolCalls);
            Options = options;
            return options;
        }

#pragma warning restore OPENAI001


        protected readonly ChatClient Client;

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

        /// <summary>
        /// Perform a query
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
            List<ChatMessage> messages = new List<ChatMessage>(2);
            var s = MsgSystemPrompt;
            if (s != null)
                messages.Add(s);
            var um = new UserChatMessage(text);
            var haveData = (extraData?.Count ?? 0) > 0;
            if (haveData)
                foreach (var x in extraData)
                    um.Content.Add(OpenAiModels.ToChatPart(x.Item1));
            messages.Add(um);
            var options = CreateOptions();
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
                            await AiCalls(tcs, null, debug, null, messages).ConfigureAwait(false);
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

        /// <summary>
        /// Execute tool calls and add the results to the messages (if supplied)
        /// </summary>
        protected async Task AiCalls(IReadOnlyList<ChatToolCall> tcs, HttpServerRequest request, AiDebugMessage debugMsg, Action<String> onToolCalls, List<ChatMessage> messages)
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

    }

}
