using Google.GenAI;
using Google.GenAI.Types;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// A query session using the Google (Gemini) generate content API
    /// </summary>
    public class GoogleQuerySession : AiQuerySessionBase
    {
        public GoogleQuerySession(Client c, GoogleSessionParams p, IAiToolCache toolCache = null, PerfMonitor monitor = null, IAiMemory memory = null)
            : this(c, p, GoogleModels.GetOptions(p.Model), toolCache, monitor, memory)
        {
        }

        GoogleQuerySession(Client c, GoogleSessionParams p, GoogleModels.Opt o, IAiToolCache toolCache, PerfMonitor monitor, IAiMemory memory)
            : base(p.Model, o.Temp, o.System, o.PTools, toolCache, monitor, memory)
        {
            Thinking = GoogleModels.GetThinking(o.Thinking, p.Reasoning ?? AiReasoning.Low);
            Tier = GoogleModels.GetTier(p.Tier ?? GoogleServiceTier.Default);
            Client = c;
        }

        protected readonly Client Client;
        readonly ThinkingConfig Thinking;
        readonly ServiceTier? Tier;

        public override String SystemPrompt { get; set; }

        protected GenerateContentConfig CreateConfig()
            => GoogleModels.BuildConfig(GetTools(), HaveTemperature, Temperature, Thinking, Tier, SupportSystemRole ? SystemPrompt : null);

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
            var sp = SystemPrompt;
            var prefix = ((!SupportSystemRole) && (!String.IsNullOrEmpty(sp))) ? GoogleModels.CreateTextContent(GoogleModels.RoleUser, sp) : null;
            var history = new List<Content> { GoogleModels.CreateUserContent(text, extraData, null) };
            var sb = new StringBuilder();
            var config = CreateConfig();
            var err = await GoogleModels.Run(Client, Model, () => Task.FromResult(config), prefix, history,
                calls => AiCalls(calls, null, debug, null),
                null, Monitor, debug, onUsage, null, null, sb).ConfigureAwait(false);
            return err ?? sb.ToString();
        }
    }
}
