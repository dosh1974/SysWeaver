using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// A chat session using a local LLM (LLamaSharp).
    /// The conversation state is kept locally (in ApiMessages) and the full history is processed on every request.
    /// </summary>
    public sealed class LlamaSharpChatSession : AiChatSessionBase
    {
        internal LlamaSharpChatSession(bool isPrivate, LlamaSharpModel model, IAiToolCache toolCache, String joinAuth, String clearAuth, PerfMonitor monitor, AsyncLock chatLock, IAiMemory memory)
            : base(isPrivate, model.Name, true, model.Llm.SupportSystemRole, model.Llm.SupportTools ? true : null, toolCache, joinAuth, clearAuth, monitor, chatLock, memory, LlamaSharpAiService.ServiceName + ".svg")
        {
            LlmModel = model;
        }

        readonly LlamaSharpModel LlmModel;

        public override String SystemPrompt { get; set; }

        /// <summary>
        /// The conversation history (excluding the system prompt), processed on every request.
        /// </summary>
        public readonly List<LlamaSharpMessage> ApiMessages = new List<LlamaSharpMessage>();

        /// <summary>
        /// Get a function that gets the system prompt for each request (updating the system prompt from the session)
        /// </summary>
        Func<Task<String>> SystemPromptBuilder(HttpSession session)
            => async () =>
            {
                if (session != null)
                {
                    var p = await BuildSystemPrompt(session).ConfigureAwait(false);
                    if (p != null)
                        SystemPrompt = p;
                }
                return SystemPrompt;
            };

        public override async Task<String> Query(String text, AiDebugMessage debug = null, Func<String, long, long, Task> onUsage = null, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData = null)
        {
            using var _a = Monitor?.Track(nameof(Query));
            var history = new List<LlamaSharpMessage> { LlamaSharpModel.CreateUserMessage(text, extraData, null) };
            var sb = new StringBuilder();
            var sp = SystemPrompt;
            var err = await LlmModel.Run(() => Task.FromResult(sp), history, GetTools, Temperature,
                calls => AiCalls(calls, null, debug, null),
                null, Monitor, debug, onUsage, null, null, sb).ConfigureAwait(false);
            return err ?? sb.ToString();
        }

        public override async Task<String> Complete(String text, HttpServerRequest request, AiDebugMessage debug = null, String from = null, Func<String, long, long, Task> onUsage = null)
        {
            using var _a = Monitor?.Track(nameof(Complete));
            var messages = ApiMessages;
            messages.Add(LlamaSharpModel.CreateUserMessage(text, null, from));
            var sb = new StringBuilder();
            var err = await LlmModel.Run(SystemPromptBuilder(request?.Session), messages, GetTools, Temperature,
                calls => AiCalls(calls, request, debug, null),
                ChatLock, Monitor, debug, onUsage, null, null, sb).ConfigureAwait(false);
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
            messages.Add(LlamaSharpModel.CreateUserMessage(text, extraData, from));
            StringBuilder link = new StringBuilder();
            if (GetTools().Length > 0)
                SetToolContext(request, link, saveFile, saveData);
            int linkLen = link.Length;
            var firstMessage = messages.Count;
            var sb = new StringBuilder();
            var err = await LlmModel.Run(SystemPromptBuilder(request.Session), messages, GetTools, Temperature,
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
            //  Remove tool call messages
            if (AutoRemoveToolCalls)
            {
                int output = firstMessage;
                var ml = messages.Count;
                for (int i = firstMessage; i < ml; ++i)
                {
                    var m = messages[i];
                    if (m.IsToolCall || m.IsToolResponse)
                        continue;
                    messages[output] = m;
                    ++output;
                }
                if (output < ml)
                    messages.RemoveRange(output, ml - output);
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
                    writer.WriteRawValue(LlamaSharpModel.GetToolJson(x));
                writer.WriteEndArray();
            }
            var sp = SystemPrompt;
            if (!String.IsNullOrEmpty(sp))
                writer.WriteString("system", sp);
            writer.WritePropertyName("messages");
            writer.WriteStartArray();
            foreach (var m in ApiMessages)
            {
                writer.WriteStartObject();
                writer.WriteString("role", m.Role);
                writer.WriteString("content", m.Content);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
    }
}
