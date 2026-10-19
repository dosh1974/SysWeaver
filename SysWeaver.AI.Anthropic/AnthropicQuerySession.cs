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

        protected readonly IAnthropicClient Client;
        readonly AnthropicModels.Settings Settings;
        readonly bool PromptCaching;

        public override String SystemPrompt { get; set; }

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
