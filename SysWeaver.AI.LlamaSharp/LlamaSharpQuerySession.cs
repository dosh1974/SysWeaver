using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    /// <summary>
    /// A query session using a local LLM (LLamaSharp)
    /// </summary>
    public class LlamaSharpQuerySession : AiQuerySessionBase
    {
        public LlamaSharpQuerySession(LlamaSharpModel model, IAiToolCache toolCache = null, PerfMonitor monitor = null, IAiMemory memory = null)
            : base(model.Name, true, model.Llm.SupportSystemRole, model.Llm.SupportTools ? true : null, toolCache, monitor, memory)
        {
            LlmModel = model;
        }

        protected readonly LlamaSharpModel LlmModel;

        public override String SystemPrompt { get; set; }

        /// <summary>
        /// Perform a query
        /// </summary>
        /// <param name="text">The query text</param>
        /// <param name="debug">An optional obejct used to track tool calls etc</param>
        /// <param name="onUsage">An optional function to call when token in/out usage is changed</param>
        /// <param name="extraData">optional attachements (only text is supported)</param>
        /// <returns>The Ai response, typically MD encoded text</returns>
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
    }
}
