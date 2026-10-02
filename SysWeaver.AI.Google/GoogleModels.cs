using Google.GenAI;
using Google.GenAI.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    /// <summary>
    /// Google (Gemini) model capabilities, conversions between API agnostic and Google types and the request loop
    /// </summary>
    static class GoogleModels
    {
        internal enum ThinkingTypes
        {
            /// <summary>
            /// No thinking configuration is sent
            /// </summary>
            None,
            /// <summary>
            /// Thinking budget (Gemini 2.5), can be disabled
            /// </summary>
            Budget,
            /// <summary>
            /// Thinking budget (Gemini 2.5 pro), can't be disabled
            /// </summary>
            BudgetNoDisable,
            /// <summary>
            /// Thinking levels: minimal, low, medium, high (Gemini 3 flash)
            /// </summary>
            Levels,
            /// <summary>
            /// Thinking levels: low, high (Gemini 3 pro)
            /// </summary>
            LowHighLevels,
        }

        internal sealed class Opt
        {
            public String Model;
            /// <summary>
            /// True if model supports temperature (or if it's recommended to set it)
            /// </summary>
            public bool Temp = true;
            /// <summary>
            /// True if model supports a system instruction
            /// </summary>
            public bool System = true;
            /// <summary>
            /// True if model supports paralell tool calls
            /// </summary>
            public bool? PTools = true;
            /// <summary>
            /// How thinking (reasoning) is configured
            /// </summary>
            public ThinkingTypes Thinking = ThinkingTypes.None;
        }

        internal static Opt GetOptions(String model)
        {
            foreach (var x in Opts)
                if (model.StartsWith(x.Model, StringComparison.OrdinalIgnoreCase))
                    return x;
            return new Opt();
        }

        static readonly Opt[] Opts =
        [
            new Opt { Model = "gemma", System = false, PTools = null },
            //  Gemini 3 strongly recommends keeping the default temperature (1.0)
            new Opt { Model = "gemini-3-pro", Temp = false, Thinking = ThinkingTypes.LowHighLevels },
            new Opt { Model = "gemini-3", Temp = false, Thinking = ThinkingTypes.Levels },
            new Opt { Model = "gemini-2.5-pro", Thinking = ThinkingTypes.BudgetNoDisable },
            new Opt { Model = "gemini-2.5", Thinking = ThinkingTypes.Budget },
        ];

        /// <summary>
        /// Get the thinking configuration for a model and reasoning effort
        /// </summary>
        internal static ThinkingConfig GetThinking(ThinkingTypes t, AiReasoning r)
        {
            switch (t)
            {
                case ThinkingTypes.Budget:
                case ThinkingTypes.BudgetNoDisable:
                    int budget = r switch
                    {
                        AiReasoning.None => t == ThinkingTypes.Budget ? 0 : 128,
                        AiReasoning.Minimal => 512,
                        AiReasoning.Low => 1024,
                        AiReasoning.Medium => 4096,
                        AiReasoning.High => 16384,
                        _ => -1,
                    };
                    return new ThinkingConfig { ThinkingBudget = budget };
                case ThinkingTypes.Levels:
                    return new ThinkingConfig
                    {
                        ThinkingLevel = r switch
                        {
                            AiReasoning.None => ThinkingLevel.Minimal,
                            AiReasoning.Minimal => ThinkingLevel.Minimal,
                            AiReasoning.Low => ThinkingLevel.Low,
                            AiReasoning.Medium => ThinkingLevel.Medium,
                            _ => ThinkingLevel.High,
                        }
                    };
                case ThinkingTypes.LowHighLevels:
                    return new ThinkingConfig
                    {
                        ThinkingLevel = r < AiReasoning.Medium ? ThinkingLevel.Low : ThinkingLevel.High,
                    };
            }
            return null;
        }

        internal static ServiceTier? GetTier(GoogleServiceTier t)
            => t switch
            {
                GoogleServiceTier.Flex => ServiceTier.Flex,
                GoogleServiceTier.Standard => ServiceTier.Standard,
                GoogleServiceTier.Priority => ServiceTier.Priority,
                _ => null,
            };

        /// <summary>
        /// Build the request configuration
        /// </summary>
        internal static GenerateContentConfig BuildConfig(AiTool[] tools, bool haveTemperature, float temperature, ThinkingConfig thinking, ServiceTier? tier, String systemInstruction)
        {
            var c = new GenerateContentConfig
            {
                ThinkingConfig = thinking,
                ServiceTier = tier?.Value?.FastEquals("UNSPECIFIED") ?? true ? null : tier,
            };
            if (haveTemperature)
                c.Temperature = temperature;
            if (!String.IsNullOrEmpty(systemInstruction))
                c.SystemInstruction = new Content { Parts = [new Part { Text = systemInstruction }] };
            if ((tools?.Length ?? 0) > 0)
                c.Tools = [new Tool { FunctionDeclarations = tools.Select(GetFunction).ToList() }];
            return c;
        }

        #region Conversions

        /// <summary>
        /// Get the function declaration of an API agnostic tool
        /// </summary>
        internal static FunctionDeclaration GetFunction(AiTool tool)
            => tool.Tool.GetApiTool(f =>
            {
                var p = f.FunctionParameters;
                return new FunctionDeclaration
                {
                    Name = f.FunctionName,
                    Description = f.FunctionDescription,
                    ParametersJsonSchema = p == null ? null : JsonDocument.Parse(p.ToMemory()).RootElement.Clone(),
                };
            });

        /// <summary>
        /// Convert an API agnostic content part to a Google part
        /// </summary>
        internal static Part ToPart(AiContentPart x)
        {
            switch (x.Kind)
            {
                case AiContentPartKinds.Text:
                    return new Part { Text = x.Text };
                case AiContentPartKinds.Image:
                case AiContentPartKinds.File:
                    if (x.Uri != null)
                        return new Part { FileData = new FileData { FileUri = x.Uri.ToString(), MimeType = x.MimeType } };
                    return new Part { InlineData = new Blob { Data = x.Data.ToArray(), MimeType = x.MimeType, DisplayName = x.Filename } };
            }
            throw new NotSupportedException("Content of kind " + x.Kind + " is not supported!");
        }

        internal const String RoleUser = "user";
        internal const String RoleModel = "model";

        /// <summary>
        /// Create a user message
        /// </summary>
        internal static Content CreateUserContent(String text, IReadOnlyList<ValueTuple<AiContentPart, String>> extraData, String from)
        {
            var parts = new List<Part>(2 + (extraData?.Count ?? 0));
            //  The API have no participant name, so add it as a separate text part
            if (!String.IsNullOrEmpty(from))
                parts.Add(new Part { Text = "[Message from: " + from + "]" });
            parts.Add(new Part { Text = text });
            if (extraData != null)
                foreach (var x in extraData)
                    parts.Add(ToPart(x.Item1));
            return new Content { Role = RoleUser, Parts = parts };
        }

        internal static Content CreateTextContent(String role, String text)
            => new Content { Role = role, Parts = [new Part { Text = text }] };

        internal static bool HaveFunctionCalls(Content c)
            => c.Parts?.Any(x => x.FunctionCall != null) ?? false;

        #endregion//Conversions

        #region Request loop

        static String GetFinishError(Candidate c)
        {
            var fr = c?.FinishReason;
            if (fr == null)
                return null;
            var v = fr.Value.Value;
            if ((v == FinishReason.Stop.Value) || (v == FinishReason.FinishReasonUnspecified.Value))
                return null;
            if (v == FinishReason.MaxTokens.Value)
                return "Error: Incomplete model output due to MaxTokens parameter or token limit exceeded.";
            if ((v == FinishReason.Safety.Value)
                || (v == FinishReason.Blocklist.Value)
                || (v == FinishReason.ProhibitedContent.Value)
                || (v == FinishReason.Spii.Value)
                || (v == FinishReason.Recitation.Value)
                || (v == FinishReason.ImageSafety.Value)
                || (v == FinishReason.ImageProhibitedContent.Value))
                return "Error: Omitted content due to a content filter flag (" + v + ").";
            var m = c.FinishMessage;
            return String.IsNullOrEmpty(m) ? "Error: " + v : String.Concat("Error: ", v, " - ", m);
        }

        static bool KeepPart(Part p)
            =>
                (p.FunctionCall != null)
                ||
                (!String.IsNullOrEmpty(p.Text))
                ||
                ((p.ThoughtSignature?.Length ?? 0) > 0)
                ||
                (p.InlineData != null)
                ||
                (p.FileData != null);

        /// <summary>
        /// Run requests until the model stops calling tools.
        /// The model responses and function responses are added to the history (as received, required for thought signatures).
        /// </summary>
        /// <param name="client">The client</param>
        /// <param name="model">The model to use</param>
        /// <param name="getConfig">Function used to get the config for each request (system prompt etc can change)</param>
        /// <param name="prefix">Optional content to prefix each request with (system prompt for models that don't support system instructions)</param>
        /// <param name="history">The conversation, model responses and tool results are added to this list</param>
        /// <param name="exec">Function used to execute tool calls</param>
        /// <param name="chatLock">Optional lock to limit concurrency</param>
        /// <param name="monitor">Optional performance monitor</param>
        /// <param name="debug">Optional debug tracking</param>
        /// <param name="onUsage">Optional usage callback (called once)</param>
        /// <param name="onText">If non null, streaming is used and this is called with the accumulated text whenever it changes</param>
        /// <param name="afterTools">Optional callback after each batch of tool calls</param>
        /// <param name="text">The accumulated text output (from all requests) is appended to this</param>
        /// <returns>null if successful, else an error message</returns>
        internal static async Task<String> Run(Client client, String model, Func<Task<GenerateContentConfig>> getConfig, Content prefix, List<Content> history,
            Func<IReadOnlyList<AiFunctionCall>, Task<String[]>> exec,
            AsyncLock chatLock, PerfMonitor monitor, AiDebugMessage debug, Func<String, long, long, Task> onUsage,
            Func<String, Task> onText, Func<Task> afterTools, StringBuilder text)
        {
            long totalIn = 0;
            long totalOut = 0;
            try
            {
                for (; ; )
                {
                    var config = await getConfig().ConfigureAwait(false);
                    List<Content> input;
                    if (prefix == null)
                        input = new List<Content>(history);
                    else
                    {
                        input = new List<Content>(history.Count + 1) { prefix };
                        input.AddRange(history);
                    }
                    List<Part> parts = new List<Part>();
                    Candidate last = null;
                    GenerateContentResponseUsageMetadata usage = null;
                    String blocked = null;
                    void Process(GenerateContentResponse r)
                    {
                        usage = r.UsageMetadata ?? usage;
                        var br = r.PromptFeedback?.BlockReason;
                        if (br != null)
                            blocked = String.Concat(br.Value.ToString(), " ", r.PromptFeedback.BlockReasonMessage).Trim();
                        var c = r.Candidates?.FirstOrDefault();
                        if (c == null)
                            return;
                        last = c;
                        var cp = c.Content?.Parts;
                        if (cp == null)
                            return;
                        foreach (var p in cp)
                            if (KeepPart(p))
                                parts.Add(p);
                    }
                    using (var _ = await (chatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
                    {
                        if (onText == null)
                        {
                            using var _m = monitor?.Track(nameof(Models.GenerateContentAsync));
                            Process(await client.Models.GenerateContentAsync(model, input, config).ConfigureAwait(false));
                        }
                        else
                        {
                            using var _m = monitor?.Track(nameof(Models.GenerateContentStreamAsync));
                            await foreach (var r in client.Models.GenerateContentStreamAsync(model, input, config).ConfigureAwait(false))
                            {
                                var before = parts.Count;
                                Process(r);
                                bool changed = false;
                                for (int i = before; i < parts.Count; ++i)
                                {
                                    var p = parts[i];
                                    if ((p.Thought ?? false) || String.IsNullOrEmpty(p.Text))
                                        continue;
                                    text.Append(p.Text);
                                    changed = true;
                                }
                                if (changed)
                                    await onText(text.ToString()).ConfigureAwait(false);
                            }
                        }
                    }
                    if (usage != null)
                    {
                        long i = (usage.PromptTokenCount ?? 0) + (usage.ToolUsePromptTokenCount ?? 0);
                        long o = (usage.CandidatesTokenCount ?? 0) + (usage.ThoughtsTokenCount ?? 0);
                        totalIn += i;
                        totalOut += o;
                        if (debug != null)
                        {
                            debug.InputTokenCount += i;
                            debug.OutputTokenCount += o;
                        }
                    }
                    if (blocked != null)
                        throw new Exception("Model refused to complete: " + blocked);
                    if (onText == null)
                        foreach (var p in parts)
                            if ((!(p.Thought ?? false)) && (!String.IsNullOrEmpty(p.Text)))
                                text.Append(p.Text);
                    if (parts.Count > 0)
                        history.Add(new Content { Role = RoleModel, Parts = parts });
                    var calls = parts.Where(x => x.FunctionCall != null).Select(x => x.FunctionCall).ToList();
                    if (calls.Count <= 0)
                        return GetFinishError(last);
                    var res = await exec(calls.Select(x => new AiFunctionCall(x.Name, BinaryData.FromString(JsonSerializer.Serialize(x.Args ?? new Dictionary<String, Object>())))).ToList()).ConfigureAwait(false);
                    var responses = new List<Part>(calls.Count);
                    for (int i = 0; i < calls.Count; ++i)
                    {
                        var c = calls[i];
                        responses.Add(new Part
                        {
                            FunctionResponse = new FunctionResponse
                            {
                                Id = c.Id,
                                Name = c.Name,
                                Response = new Dictionary<String, Object> { { "output", res[i] } },
                            }
                        });
                    }
                    history.Add(new Content { Role = RoleUser, Parts = responses });
                    if (afterTools != null)
                        await afterTools().ConfigureAwait(false);
                }
            }
            finally
            {
                if (onUsage != null)
                    await onUsage(model, totalIn, totalOut).ConfigureAwait(false);
            }
        }

        #endregion//Request loop
    }
}
