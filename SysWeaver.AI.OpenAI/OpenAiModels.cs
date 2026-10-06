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
            /// If true, the model support the reasoning effort
            /// </summary>
            public bool CanReason = true;

            /// <summary>
            /// If true, the model support setting the service tier
            /// </summary>
            public bool HaveTiers = true;

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

        static readonly Opt[] Opts =
        [
            new Opt { Model = "o1-preview", Temp = false, System = false, PTools = null, CanReason = false, HaveTiers = false },
            new Opt { Model = "o1-mini", Temp = false, System = false, PTools = null, CanReason = false, HaveTiers = false },
            new Opt { Model = "o1", Temp = false, PTools = null, CanReason = false, HaveTiers = false },
            new Opt { Model = "o3-mini", Temp = false, PTools = null, CanReason = false, HaveTiers = false },
            new Opt { Model = "gpt-5", Temp = false, CanReason = true, HaveTiers = true },
            new Opt { Model = "gpt-6", Temp = false, CanReason = true, HaveTiers = true },
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
        internal static ChatCompletionOptions CreateChatOptions(IReadOnlyList<AiTool> tools, bool haveTemperature, float temperature, bool canReason, ChatReasoningEffortLevel? reasoningLevel, ChatServiceTier? tier, bool? parallelToolCalls)
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
                if (canReason)
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
