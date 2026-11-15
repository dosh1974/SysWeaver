using Anthropic;
using Anthropic.Models.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// A query session using the Anthropic (Claude) messages API
    /// </summary>
    public class AnthropicQuerySession : AiQuerySessionBase
    {
        /// <summary>
        /// Create a query session
        /// </summary>
        /// <param name="c">The Anthropic client to use</param>
        /// <param name="p">Session parameters (model, reasoning, max tokens)</param>
        /// <param name="promptCaching">True to enable prompt caching</param>
        /// <param name="toolCache">Optional cache for tool results</param>
        /// <param name="monitor">Optional performance monitor</param>
        /// <param name="memory">Optional AI memory</param>
        public AnthropicQuerySession(IAnthropicClient c, AnthropicSessionParams p, bool promptCaching = true, IAiToolCache toolCache = null, PerfMonitor monitor = null, IAiMemory memory = null)
            : this(c, p, promptCaching, AnthropicModels.GetSettings(AnthropicModels.GetOptions(p.Model), p.Reasoning ?? AiReasoning.Low, p.MaxTokens ?? 32000), toolCache, monitor, memory)
        {
        }

        AnthropicQuerySession(IAnthropicClient c, AnthropicSessionParams p, bool promptCaching, AnthropicModels.Settings s, IAiToolCache toolCache, PerfMonitor monitor, IAiMemory memory)
            : base(p.Model, s.Temp, true, true, toolCache, monitor, memory)
        {
            Settings = s;
            PromptCaching = promptCaching;
            Client = c;
        }

        /// <summary>
        /// The Anthropic client used by this session
        /// </summary>
        protected readonly IAnthropicClient Client;
        readonly AnthropicModels.Settings Settings;
        readonly bool PromptCaching;

        /// <inheritdoc />
        public override String SystemPrompt { get; set; }

        /// <summary>
        /// Perform a query
        /// </summary>
        /// <param name="text">The query text</param>
        /// <param name="debug">An optional object used to track tool calls etc</param>
        /// <param name="onUsage">An optional function to call when token in/out usage is changed</param>
        /// <param name="extraData">optional attachments</param>
        /// <returns>The Ai response, typically MD encoded text</returns>
        /// <exception cref="Exception"></exception>
        public override async Task<String> Query(String text, AiDebugMessage debug = null, Func<String, long, long, Task> onUsage = null, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData = null)
        {
            using var _a = Monitor?.Track(nameof(Query));
            var history = new List<MessageParam> { AnthropicModels.CreateUserMessage(text, extraData, null) };
            var sb = new StringBuilder();
            var sp = SystemPrompt;
            var tools = GetTools().Select(AnthropicModels.GetTool).ToArray();
            var s = Settings;
            var temp = HaveTemperature ? Temperature : (float?)null;
            var cache = PromptCaching;
            var err = await AnthropicModels.Run(Client, Model,
                h => AnthropicModels.BuildParams(Model, s, sp, tools, AiCompletionToolChoices.Auto, temp, h, cache),
                history,
                calls => AiCalls(calls, null, debug, null),
                null, Monitor, debug, onUsage, null, null, sb).ConfigureAwait(false);
            return err ?? sb.ToString();
        }
    }
}
