using OpenAI.Chat;
using OpenAI.Responses;
using System;
using System.Collections.Generic;

namespace SysWeaver.AI
{
#pragma warning disable OPENAI001

    /// <summary>
    /// OpenAI model capabilities and conversions between API agnostic and OpenAI types
    /// </summary>
    static class OpenAiModels
    {
        internal sealed class Opt
        {
            public String Model;
            /// <summary>
            /// True if model supports temperature
            /// </summary>
            public bool Temp = true;
            /// <summary>
            /// True if model supports the System role
            /// </summary>
            public bool System = true;
            /// <summary>
            /// True if model supports paralell tool calls
            /// </summary>
            public bool? PTools = true;

            /// <summary>
            /// The reasoning efforts supported by the model (in ascending order), null if reasoning effort is not supported
            /// </summary>
            public AiReasoning[] Reasoning = AllReasoning;

            /// <summary>
            /// If true, the model support setting the service tier
            /// </summary>
            public bool HaveTiers = true;

            /// <summary>
            /// If true, the model support the reasoning effort
            /// </summary>
            public bool CanReason => Reasoning != null;

            /// <summary>
            /// If true, reasoning can be disabled (reasoning effort none)
            /// </summary>
            public bool CanDisableReasoning => ((Reasoning?.Length ?? 0) > 0) && (Reasoning[0] == AiReasoning.None);

            /// <summary>
            /// Get the supported reasoning effort that is closest to the requested one (rounds up on ties)
            /// </summary>
            public AiReasoning GetReasoning(AiReasoning r)
            {
                var l = Reasoning;
                if ((l?.Length ?? 0) <= 0)
                    return r;
                var best = l[0];
                var bestDist = Math.Abs((int)best - (int)r);
                foreach (var x in l)
                {
                    var d = Math.Abs((int)x - (int)r);
                    if (d <= bestDist)
                    {
                        best = x;
                        bestDist = d;
                    }
                }
                return best;
            }
        }

        internal static Opt GetOptions(String model)
        {
            Opt o = null;
            foreach (var x in Opts)
            {
                if (model.StartsWith(x.Model, StringComparison.OrdinalIgnoreCase))
                {
                    o = x;
                    break;
                }
            }
            return o ?? new Opt();
        }

        //  Unknown models: everything except max (maps to xhigh)
        static readonly AiReasoning[] AllReasoning = [AiReasoning.None, AiReasoning.Minimal, AiReasoning.Low, AiReasoning.Medium, AiReasoning.High, AiReasoning.ExtraHigh];
        static readonly AiReasoning[] NoneToMax = [AiReasoning.None, AiReasoning.Low, AiReasoning.Medium, AiReasoning.High, AiReasoning.ExtraHigh, AiReasoning.Max];
        static readonly AiReasoning[] NoneToExtraHigh = [AiReasoning.None, AiReasoning.Low, AiReasoning.Medium, AiReasoning.High, AiReasoning.ExtraHigh];
        static readonly AiReasoning[] NoneToHigh = [AiReasoning.None, AiReasoning.Low, AiReasoning.Medium, AiReasoning.High];
        static readonly AiReasoning[] MinimalToHigh = [AiReasoning.Minimal, AiReasoning.Low, AiReasoning.Medium, AiReasoning.High];
        static readonly AiReasoning[] LowToMax = [AiReasoning.Low, AiReasoning.Medium, AiReasoning.High, AiReasoning.ExtraHigh, AiReasoning.Max];
        static readonly AiReasoning[] LowToExtraHigh = [AiReasoning.Low, AiReasoning.Medium, AiReasoning.High, AiReasoning.ExtraHigh];
        static readonly AiReasoning[] LowToHigh = [AiReasoning.Low, AiReasoning.Medium, AiReasoning.High];
        static readonly AiReasoning[] MediumToExtraHigh = [AiReasoning.Medium, AiReasoning.High, AiReasoning.ExtraHigh];
        static readonly AiReasoning[] MediumOnly = [AiReasoning.Medium];
        static readonly AiReasoning[] HighOnly = [AiReasoning.High];

        //  Order matters (prefix match), more specific names must come first
        static readonly Opt[] Opts =
        [
            new Opt { Model = "o1-preview", Temp = false, System = false, PTools = null, Reasoning = null, HaveTiers = false },
            new Opt { Model = "o1-mini", Temp = false, System = false, PTools = null, Reasoning = null, HaveTiers = false },
            new Opt { Model = "o1-pro", Temp = false, PTools = null, Reasoning = LowToHigh },
            new Opt { Model = "o1", Temp = false, PTools = null, Reasoning = null, HaveTiers = false },
            new Opt { Model = "o3-mini", Temp = false, PTools = null, Reasoning = null, HaveTiers = false },
            new Opt { Model = "o3-deep-research", Temp = false, Reasoning = MediumOnly },
            new Opt { Model = "o3", Temp = false, Reasoning = LowToHigh },
            new Opt { Model = "o4-mini-deep-research", Temp = false, Reasoning = MediumOnly },
            new Opt { Model = "o4-mini", Temp = false, Reasoning = LowToHigh },
            new Opt { Model = "codex-mini", Temp = false, Reasoning = LowToHigh },
            new Opt { Model = "gpt-oss", Reasoning = LowToHigh, HaveTiers = false },
            new Opt { Model = "chat-latest", Temp = false, Reasoning = null },
            //  GPT-6
            new Opt { Model = "gpt-6.1-sol", Temp = false, Reasoning = LowToMax },
            new Opt { Model = "gpt-6-astra", Temp = false, Reasoning = LowToMax },
            new Opt { Model = "gpt-6", Temp = false, Reasoning = NoneToMax },
            //  GPT-5.x
            new Opt { Model = "gpt-5.6", Temp = false, Reasoning = NoneToMax },
            new Opt { Model = "gpt-5.5-pro", Temp = false, Reasoning = MediumToExtraHigh },
            new Opt { Model = "gpt-5.5", Temp = false, Reasoning = NoneToMax },
            new Opt { Model = "gpt-5.4-pro", Temp = false, Reasoning = MediumToExtraHigh },
            new Opt { Model = "gpt-5.4", Temp = false, Reasoning = NoneToExtraHigh },
            new Opt { Model = "gpt-5.3-codex", Temp = false, Reasoning = LowToExtraHigh },
            new Opt { Model = "gpt-5.3-chat", Temp = false, Reasoning = null },
            new Opt { Model = "gpt-5.2-pro", Temp = false, Reasoning = MediumToExtraHigh },
            new Opt { Model = "gpt-5.2-codex", Temp = false, Reasoning = LowToExtraHigh },
            new Opt { Model = "gpt-5.2-chat", Temp = false, Reasoning = null },
            new Opt { Model = "gpt-5.2", Temp = false, Reasoning = NoneToExtraHigh },
            new Opt { Model = "gpt-5.1-codex-max", Temp = false, Reasoning = LowToExtraHigh },
            new Opt { Model = "gpt-5.1-codex", Temp = false, Reasoning = LowToHigh },
            new Opt { Model = "gpt-5.1-chat", Temp = false, Reasoning = null },
            new Opt { Model = "gpt-5.1", Temp = false, Reasoning = NoneToHigh },
            new Opt { Model = "gpt-5.", Temp = false, Reasoning = NoneToExtraHigh },
            //  GPT-5
            new Opt { Model = "gpt-5-pro", Temp = false, Reasoning = HighOnly },
            new Opt { Model = "gpt-5-codex", Temp = false, Reasoning = LowToHigh },
            new Opt { Model = "gpt-5-chat", Temp = false, Reasoning = null },
            new Opt { Model = "gpt-5", Temp = false, Reasoning = MinimalToHigh },
            //  Non reasoning models
            new Opt { Model = "gpt-4", Reasoning = null },
            new Opt { Model = "chatgpt-4o", Reasoning = null },
            new Opt { Model = "gpt-3.5", Reasoning = null },
        ];

        #region Chat completions

        internal static readonly ChatReasoningEffortLevel[] ChatReasoningLevels =
        [
            ChatReasoningEffortLevel.None,
            ChatReasoningEffortLevel.Minimal,
            ChatReasoningEffortLevel.Low,
            ChatReasoningEffortLevel.Medium,
            ChatReasoningEffortLevel.High,
            new ChatReasoningEffortLevel("xhigh"),
            new ChatReasoningEffortLevel("max"),
        ];

        internal static readonly ChatServiceTier[] ChatTiers =
            [
            ChatServiceTier.Auto,
            ChatServiceTier.Default,
            ChatServiceTier.Flex,
            ChatServiceTier.Scale,
            new ChatServiceTier("fast"),
            ];

        /// <summary>
        /// Get the chat completions tool of an API agnostic tool
        /// </summary>
        internal static ChatTool GetChatTool(AiTool tool)
            => tool.Tool.GetApiTool(f => ChatTool.CreateFunctionTool(f.FunctionName, f.FunctionDescription, f.FunctionParameters, f.FunctionSchemaIsStrict));

        /// <summary>
        /// Create the chat completions options
        /// </summary>
        internal static ChatCompletionOptions CreateChatOptions(IReadOnlyList<AiTool> tools, bool haveTemperature, float temperature, bool canDisableReasoning, ChatReasoningEffortLevel? reasoningLevel, ChatServiceTier? tier, bool? parallelToolCalls)
        {
            var options = new ChatCompletionOptions();
            if (haveTemperature)
                options.Temperature = temperature;
            options.ReasoningEffortLevel = reasoningLevel;
            options.ServiceTier = tier;
            var d = options.Tools;
            foreach (var x in tools)
                d.Add(GetChatTool(x));
            if (options.Tools.Count > 0)
            {
                options.AllowParallelToolCalls = parallelToolCalls;
                //  Function calling in the chat completions API requires reasoning to be disabled (on models that can)
                if (canDisableReasoning)
                    options.ReasoningEffortLevel = ChatReasoningEffortLevel.None;
            }
            return options;
        }

        static ChatImageDetailLevel ToChatDetail(bool highDetail) => highDetail ? ChatImageDetailLevel.High : ChatImageDetailLevel.Auto;

        /// <summary>
        /// Convert an API agnostic content part to a chat completions content part
        /// </summary>
        internal static ChatMessageContentPart ToChatPart(AiContentPart x)
        {
            switch (x.Kind)
            {
                case AiContentPartKinds.Text:
                    return ChatMessageContentPart.CreateTextPart(x.Text);
                case AiContentPartKinds.Image:
                    if (x.Uri != null)
                        return ChatMessageContentPart.CreateImagePart(x.Uri, ToChatDetail(x.HighDetail));
                    return ChatMessageContentPart.CreateImagePart(x.Data, x.MimeType, ToChatDetail(x.HighDetail));
                case AiContentPartKinds.File:
                    if (x.Data == null)
                        throw new NotSupportedException("File uri's are not supported by the chat completions API!");
                    return ChatMessageContentPart.CreateFilePart(x.Data, x.MimeType, x.Filename);
            }
            throw new NotSupportedException("Content of kind " + x.Kind + " is not supported!");
        }

        #endregion//Chat completions

        #region Responses

        internal static readonly ResponseReasoningEffortLevel[] ResponseReasoningLevels =
        [
            ResponseReasoningEffortLevel.None,
            ResponseReasoningEffortLevel.Minimal,
            ResponseReasoningEffortLevel.Low,
            ResponseReasoningEffortLevel.Medium,
            ResponseReasoningEffortLevel.High,
            ResponseReasoningEffortLevel.ExtraHigh,
            new ResponseReasoningEffortLevel("max"),
        ];

        internal static readonly ResponseServiceTier[] ResponseTiers =
            [
            ResponseServiceTier.Auto,
            ResponseServiceTier.Default,
            ResponseServiceTier.Flex,
            ResponseServiceTier.Scale,
            new ResponseServiceTier("fast"),
            ];

        /// <summary>
        /// Get the responses tool of an API agnostic tool
        /// </summary>
        internal static ResponseTool GetResponseTool(AiTool tool)
            => tool.Tool.GetApiTool<ResponseTool>(f => ResponseTool.CreateFunctionTool(f.FunctionName, f.FunctionParameters, f.FunctionSchemaIsStrict, f.FunctionDescription));

        static ResponseImageDetailLevel ToResponseDetail(bool highDetail) => highDetail ? ResponseImageDetailLevel.High : ResponseImageDetailLevel.Auto;

        /// <summary>
        /// Convert an API agnostic content part to a responses content part
        /// </summary>
        internal static ResponseContentPart ToResponsePart(AiContentPart x)
        {
            switch (x.Kind)
            {
                case AiContentPartKinds.Text:
                    return ResponseContentPart.CreateInputTextPart(x.Text);
                case AiContentPartKinds.Image:
                    if (x.Uri != null)
                        return ResponseContentPart.CreateInputImagePart(x.Uri, ToResponseDetail(x.HighDetail));
                    return ResponseContentPart.CreateInputImagePart(x.Data.WithMediaType(x.MimeType), ToResponseDetail(x.HighDetail));
                case AiContentPartKinds.File:
                    if (x.Uri != null)
                        return ResponseContentPart.CreateInputFilePart(x.Uri);
                    return ResponseContentPart.CreateInputFilePart(x.Data, x.MimeType, x.Filename);
            }
            throw new NotSupportedException("Content of kind " + x.Kind + " is not supported!");
        }

        #endregion//Responses



    }

#pragma warning restore OPENAI001
}
