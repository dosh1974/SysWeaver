using Anthropic;
using Anthropic.Models.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// A chat session using the Anthropic (Claude) messages API.
    /// The conversation state is kept locally (in ApiMessages) and the full history is sent on every request.
    /// Thinking blocks are kept during a turn (required when continuing after tool calls) and removed when the turn is complete,
    /// so that the system prompt can change and tool calls can be removed between turns.
    /// </summary>
    public sealed class AnthropicChatSession : AiChatSessionBase
    {
        internal AnthropicChatSession(bool isPrivate, IAnthropicClient c, AnthropicSessionParams p, bool promptCaching, IAiToolCache toolCache, String joinAuth, String clearAuth, PerfMonitor monitor, AsyncLock chatLock, IAiMemory memory)
            : this(isPrivate, c, p, promptCaching, AnthropicModels.GetSettings(AnthropicModels.GetOptions(p.Model), p.Reasoning ?? AiReasoning.Low, p.MaxTokens ?? 32000), toolCache, joinAuth, clearAuth, monitor, chatLock, memory)
        {
        }

        AnthropicChatSession(bool isPrivate, IAnthropicClient c, AnthropicSessionParams p, bool promptCaching, AnthropicModels.Settings s, IAiToolCache toolCache, String joinAuth, String clearAuth, PerfMonitor monitor, AsyncLock chatLock, IAiMemory memory)
            : base(isPrivate, p.Model, s.Temp, true, true, toolCache, joinAuth, clearAuth, monitor, chatLock, memory, AnthropicAiService.ServiceName + ".svg")
        {
            Settings = s;
            PromptCaching = promptCaching;
            Client = c;
        }

        readonly IAnthropicClient Client;
        readonly AnthropicModels.Settings Settings;
        readonly bool PromptCaching;

        public override String SystemPrompt { get; set; }

        /// <summary>
        /// The conversation history (excluding the system prompt), sent on every request.
        /// </summary>
        public readonly List<MessageParam> ApiMessages = new List<MessageParam>();

        /// <summary>
        /// Get a function that builds each request of a turn (the system prompt and tools must not change during a turn)
        /// </summary>
        Func<List<MessageParam>, MessageCreateParams> RequestBuilder(String systemPrompt)
        {
            var tools = GetTools().Select(AnthropicModels.GetTool).ToArray();
            var s = Settings;
            var temp = HaveTemperature ? Temperature : (float?)null;
            var cache = PromptCaching;
            var model = Model;
            return h => AnthropicModels.BuildParams(model, s, systemPrompt, tools, AiCompletionToolChoices.Auto, temp, h, cache);
        }

        /// <summary>
        /// Update the system prompt from the session (once per turn)
        /// </summary>
        async Task<String> UpdateSystemPrompt(HttpSession session)
        {
            if (session != null)
            {
                var p = await BuildSystemPrompt(session).ConfigureAwait(false);
                if (p != null)
                    SystemPrompt = p;
            }
            return SystemPrompt;
        }

        public override async Task<String> Query(String text, AiDebugMessage debug = null, Func<String, long, long, Task> onUsage = null, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData = null)
        {
            using var _a = Monitor?.Track(nameof(Query));
            var history = new List<MessageParam> { AnthropicModels.CreateUserMessage(text, extraData, null) };
            var sb = new StringBuilder();
            var err = await AnthropicModels.Run(Client, Model, RequestBuilder(SystemPrompt), history,
                calls => AiCalls(calls, null, debug, null),
                null, Monitor, debug, onUsage, null, null, sb).ConfigureAwait(false);
            return err ?? sb.ToString();
        }

        public override async Task<String> Complete(String text, HttpServerRequest request, AiDebugMessage debug = null, String from = null, Func<String, long, long, Task> onUsage = null)
        {
            using var _a = Monitor?.Track(nameof(Complete));
            var messages = ApiMessages;
            messages.Add(AnthropicModels.CreateUserMessage(text, null, from));
            var sb = new StringBuilder();
            var build = RequestBuilder(await UpdateSystemPrompt(request?.Session).ConfigureAwait(false));
            String err;
            try
            {
                err = await AnthropicModels.Run(Client, Model, build, messages,
                    calls => AiCalls(calls, request, debug, null),
                    ChatLock, Monitor, debug, onUsage, null, null, sb).ConfigureAwait(false);
            }
            finally
            {
                AnthropicModels.StripThinking(messages);
            }
            return err ?? sb.ToString();
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
            messages.Add(AnthropicModels.CreateUserMessage(text, extraData, from));
            StringBuilder link = new StringBuilder();
            if (GetTools().Length > 0)
                SetToolContext(request, link, saveFile, saveData);
            int linkLen = link.Length;
            var firstMessage = messages.Count;
            var sb = new StringBuilder();
            var build = RequestBuilder(await UpdateSystemPrompt(request.Session).ConfigureAwait(false));
            String err;
            try
            {
                err = await AnthropicModels.Run(Client, Model, build, messages,
                    calls => AiCalls(calls, request, debug, onTools),
                    ChatLock, Monitor, debug, onUsage,
                    t => onUpdate(t, link.ToString()),
                    async () =>
                    {
                        var newL = link.Length;
                        if (newL != linkLen)
                        {
                            linkLen = newL;
                            await onUpdate(sb.ToString(), newL > 0 ? link.ToString() : null).ConfigureAwait(false);
                        }
                        onTools?.Invoke(null);
                    },
                    sb).ConfigureAwait(false);
            }
            finally
            {
                //  Remove tool call messages (assistant messages with tool use and the user messages with the tool results)
                if (AutoRemoveToolCalls)
                {
                    int output = firstMessage;
                    var ml = messages.Count;
                    for (int i = firstMessage; i < ml; ++i)
                    {
                        var m = messages[i];
                        if (!AnthropicModels.IsAssistant(m))
                            continue;
                        if (AnthropicModels.HaveToolUse(m))
                            continue;
                        messages[output] = m;
                        ++output;
                    }
                    if (output < ml)
                        messages.RemoveRange(output, ml - output);
                }
                AnthropicModels.StripThinking(messages);
            }
            if (err != null)
                return err;
            if ((sb.Length > 0) || (link.Length > 0))
                return null;
            return "No text response!";
        }

        public override void ClearApiMessages() => ApiMessages.Clear();

        protected override void WriteConversation(Utf8JsonWriter writer)
        {
            var tools = GetTools();
            if (tools.Length > 0)
            {
                writer.WritePropertyName("tools");
                writer.WriteStartArray();
                foreach (var x in tools)
                    JsonSerializer.Serialize(writer, AnthropicModels.GetTool(x));
                writer.WriteEndArray();
            }
            var sp = SystemPrompt;
            if (!String.IsNullOrEmpty(sp))
            {
                writer.WritePropertyName("system");
                writer.WriteStringValue(sp);
            }
            writer.WritePropertyName("messages");
            writer.WriteStartArray();
            foreach (var m in ApiMessages)
                JsonSerializer.Serialize(writer, m);
            writer.WriteEndArray();
        }
    }
}
