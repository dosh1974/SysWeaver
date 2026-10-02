using Google.GenAI;
using Google.GenAI.Types;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// A chat session using the Google (Gemini) generate content API.
    /// The conversation state is kept locally (in ApiMessages) and the full history is sent on every request.
    /// </summary>
    public sealed class GoogleChatSession : AiChatSessionBase
    {
        internal GoogleChatSession(bool isPrivate, Client c, GoogleSessionParams p, IAiToolCache toolCache, String joinAuth, String clearAuth, PerfMonitor monitor, AsyncLock chatLock, IAiMemory memory)
            : this(isPrivate, c, p, GoogleModels.GetOptions(p.Model), toolCache, joinAuth, clearAuth, monitor, chatLock, memory)
        {
        }

        GoogleChatSession(bool isPrivate, Client c, GoogleSessionParams p, GoogleModels.Opt o, IAiToolCache toolCache, String joinAuth, String clearAuth, PerfMonitor monitor, AsyncLock chatLock, IAiMemory memory)
            : base(isPrivate, p.Model, o.Temp, o.System, o.PTools, toolCache, joinAuth, clearAuth, monitor, chatLock, memory, GoogleAiService.IconRoot, "google.svg")
        {
            Thinking = GoogleModels.GetThinking(o.Thinking, p.Reasoning ?? AiReasoning.Low);
            Tier = GoogleModels.GetTier(p.Tier ?? GoogleServiceTier.Default);
            Client = c;
        }

        readonly Client Client;
        readonly ThinkingConfig Thinking;
        readonly ServiceTier? Tier;

        public override String SystemPrompt { get; set; }

        /// <summary>
        /// The conversation history (excluding the system prompt), sent on every request.
        /// </summary>
        public readonly List<Content> ApiMessages = new List<Content>();

        GenerateContentConfig CreateConfig()
            => GoogleModels.BuildConfig(GetTools(), HaveTemperature, Temperature, Thinking, Tier, SupportSystemRole ? SystemPrompt : null);

        /// <summary>
        /// The system prompt as the first user message (for models that don't support a system instruction), else null
        /// </summary>
        Content GetPrefix()
        {
            if (SupportSystemRole)
                return null;
            var sp = SystemPrompt;
            return String.IsNullOrEmpty(sp) ? null : GoogleModels.CreateTextContent(GoogleModels.RoleUser, sp);
        }

        /// <summary>
        /// Get a function that builds the config for each request (updating the system prompt from the session)
        /// </summary>
        Func<Task<GenerateContentConfig>> ConfigBuilder(HttpSession session)
            => async () =>
            {
                if (session != null)
                {
                    var p = await BuildSystemPrompt(session).ConfigureAwait(false);
                    if (p != null)
                        SystemPrompt = p;
                }
                return CreateConfig();
            };

        public override async Task<String> Query(String text, AiDebugMessage debug = null, Func<String, long, long, Task> onUsage = null, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData = null)
        {
            using var _a = Monitor?.Track(nameof(Query));
            var history = new List<Content> { GoogleModels.CreateUserContent(text, extraData, null) };
            var sb = new StringBuilder();
            var config = CreateConfig();
            var err = await GoogleModels.Run(Client, Model, () => Task.FromResult(config), GetPrefix(), history,
                calls => AiCalls(calls, null, debug, null),
                null, Monitor, debug, onUsage, null, null, sb).ConfigureAwait(false);
            return err ?? sb.ToString();
        }

        public override async Task<String> Complete(String text, HttpServerRequest request, AiDebugMessage debug = null, String from = null, Func<String, long, long, Task> onUsage = null)
        {
            using var _a = Monitor?.Track(nameof(Complete));
            var messages = ApiMessages;
            messages.Add(GoogleModels.CreateUserContent(text, null, from));
            var sb = new StringBuilder();
            var getConfig = ConfigBuilder(request?.Session);
            var err = await GoogleModels.Run(Client, Model, getConfig, GetPrefix(), messages,
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
            messages.Add(GoogleModels.CreateUserContent(text, extraData, from));
            StringBuilder link = new StringBuilder();
            if (GetTools().Length > 0)
                SetToolContext(request, link, saveFile, saveData);
            int linkLen = link.Length;
            var firstMessage = messages.Count;
            var sb = new StringBuilder();
            var getConfig = ConfigBuilder(request.Session);
            var err = await GoogleModels.Run(Client, Model, getConfig, GetPrefix(), messages,
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
                    if (m.Role != GoogleModels.RoleModel)
                        continue;
                    if (GoogleModels.HaveFunctionCalls(m))
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
                    JsonSerializer.Serialize(writer, GoogleModels.GetFunction(x));
                writer.WriteEndArray();
            }
            var sp = SystemPrompt;
            if (!String.IsNullOrEmpty(sp))
            {
                writer.WritePropertyName("systemInstruction");
                JsonSerializer.Serialize(writer, GoogleModels.CreateTextContent(null, sp));
            }
            writer.WritePropertyName("messages");
            writer.WriteStartArray();
            foreach (var m in ApiMessages)
                JsonSerializer.Serialize(writer, m);
            writer.WriteEndArray();
        }
    }
}
