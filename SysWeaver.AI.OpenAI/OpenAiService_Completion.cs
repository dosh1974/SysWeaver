using OpenAI.Chat;
using OpenAI.Responses;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
#pragma warning disable OPENAI001

    public sealed partial class OpenAiService
    {
        /// <summary>
        /// Complete a conversation (stateless, the whole conversation is in the request).
        /// Uses the chat completions or the responses API (as selected by DefaultChatApi).
        /// Tools in the request are defined by the caller, tool calls are returned (not executed).
        /// Attachments (images, files) of tool results are sent in a user message right after the tool results (the APIs only accept text tool results).
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="onText">Optional callback with the accumulated text response whenever it changes (streaming is used if supplied)</param>
        /// <returns>The result</returns>
        public override Task<AiCompletionResult> Complete(AiCompletionRequest request, Func<String, Task> onText = null)
        {
            var model = String.IsNullOrEmpty(request.Model) ? DefaultChatModel : request.Model;
            var o = OpenAiModels.GetOptions(model);
            var rt = request.Tools;
            var tools = (rt?.Count ?? 0) > 0 ? rt.Select(AiTool.CreateExternal).ToArray() : [];
            return DefaultChatApi == OpenAiChatApi.Responses
                ? CompleteResponses(request, model, o, tools, onText)
                : CompleteChat(request, model, o, tools, onText);
        }

        #region Completion helpers

        static AiCompletionException NoToolCallId()
            => new AiCompletionException("Tool messages must have a " + nameof(AiCompletionMessage.ToolCallId), "invalid_request");

        static String GetPartsIntro(AiCompletionMessage m)
            => String.IsNullOrEmpty(m.Name)
                ? String.Concat("[Attachments of the tool result ", m.ToolCallId, "]")
                : String.Concat("[Attachments of the tool result ", m.Name, " (", m.ToolCallId, ")]");

        static AiCompletionFinishReasons GetFinishReason(List<AiCompletionToolCall> toolCalls, bool isTruncated)
            => toolCalls != null ? AiCompletionFinishReasons.ToolCalls : (isTruncated ? AiCompletionFinishReasons.Length : AiCompletionFinishReasons.Stop);

        #endregion//Completion helpers

        #region Chat completions

        /// <summary>
        /// Convert a completion request to chat completions messages
        /// </summary>
        static List<ChatMessage> GetChatMessages(AiCompletionRequest request, OpenAiModels.Opt o)
        {
            var messages = new List<ChatMessage>(request.Messages.Count + 2);
            var sp = request.SystemPrompt;
            if (!String.IsNullOrEmpty(sp))
                messages.Add(o.System ? new SystemChatMessage(sp) : new UserChatMessage(sp));
            //  Attachments of tool results, added as a user message after the tool results
            List<ChatMessageContentPart> pending = null;
            String pendingId = null;
            foreach (var m in request.Messages)
            {
                if ((pending != null) && (m.Role != AiCompletionRoles.Tool))
                {
                    messages.Add(new UserChatMessage(pending));
                    pending = null;
                    pendingId = null;
                }
                var parts = m.Parts;
                switch (m.Role)
                {
                    case AiCompletionRoles.User:
                        var up = new List<ChatMessageContentPart>(1 + (parts?.Count ?? 0));
                        if (!String.IsNullOrEmpty(m.Text))
                            up.Add(ChatMessageContentPart.CreateTextPart(m.Text));
                        if (parts != null)
                            foreach (var x in parts)
                                up.Add(OpenAiModels.ToChatPart(x));
                        if (up.Count <= 0)
                            up.Add(ChatMessageContentPart.CreateTextPart(""));
                        var um = new UserChatMessage(up);
                        if (!String.IsNullOrEmpty(m.Name))
                            um.ParticipantName = m.Name;
                        messages.Add(um);
                        break;
                    case AiCompletionRoles.Assistant:
                        var tcs = m.ToolCalls;
                        if ((tcs?.Count ?? 0) > 0)
                        {
                            var am = new AssistantChatMessage(tcs.Select(x => ChatToolCall.CreateFunctionToolCall(x.Id, x.Name, BinaryData.FromString(String.IsNullOrEmpty(x.Arguments) ? "{}" : x.Arguments))));
                            if (!String.IsNullOrEmpty(m.Text))
                                am.Content.Add(ChatMessageContentPart.CreateTextPart(m.Text));
                            messages.Add(am);
                        }
                        else
                        {
                            messages.Add(new AssistantChatMessage(m.Text ?? ""));
                        }
                        break;
                    case AiCompletionRoles.Tool:
                        if (String.IsNullOrEmpty(m.ToolCallId))
                            throw NoToolCallId();
                        var text = m.Text ?? "";
                        if (parts != null)
                        {
                            foreach (var x in parts)
                            {
                                if (x.Kind == AiContentPartKinds.Text)
                                {
                                    text = String.IsNullOrEmpty(text) ? x.Text : String.Concat(text, "\n", x.Text);
                                    continue;
                                }
                                pending ??= new List<ChatMessageContentPart>();
                                if (pendingId != m.ToolCallId)
                                {
                                    pending.Add(ChatMessageContentPart.CreateTextPart(GetPartsIntro(m)));
                                    pendingId = m.ToolCallId;
                                }
                                pending.Add(OpenAiModels.ToChatPart(x));
                            }
                        }
                        messages.Add(new ToolChatMessage(m.ToolCallId, text));
                        break;
                }
            }
            if (pending != null)
                messages.Add(new UserChatMessage(pending));
            return messages;
        }

        async Task<AiCompletionResult> CompleteChat(AiCompletionRequest request, String model, OpenAiModels.Opt o, AiTool[] tools, Func<String, Task> onText)
        {
            using var _a = PerfMon.Track(nameof(CompleteChat));
            var messages = GetChatMessages(request, o);
            var reasoningLevel = o.CanReason ? OpenAiModels.ChatReasoningLevels[(int)o.GetReasoning(request.Reasoning ?? DefaultReasoning)] : (ChatReasoningEffortLevel?)null;
            var tier = o.HaveTiers ? OpenAiModels.ChatTiers[(int)DefaultTier] : (ChatServiceTier?)null;
            var temp = request.Temperature;
            var options = OpenAiModels.CreateChatOptions(tools, o.Temp && temp.HasValue, temp ?? 1.0f, o.CanDisableReasoning, reasoningLevel, tier, o.PTools);
            if (tools.Length > 0)
                options.ToolChoice = request.ToolChoice switch
                {
                    AiCompletionToolChoices.None => ChatToolChoice.CreateNoneChoice(),
                    AiCompletionToolChoices.Required => ChatToolChoice.CreateRequiredChoice(),
                    _ => ChatToolChoice.CreateAutoChoice(),
                };
            if (request.MaxTokens.HasValue)
                options.MaxOutputTokenCount = request.MaxTokens.Value;
            var client = new ChatClient(model, ApiKey, Options);
            var cancel = request.Cancel;
            var sb = new StringBuilder();
            List<AiCompletionToolCall> toolCalls = null;
            ChatFinishReason? fr = null;
            ChatTokenUsage usage = null;
            String refusal = null;
            String resModel = null;
            using (var _ = await (ChatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
            {
                if (onText == null)
                {
                    ClientResult<ChatCompletion> r;
                    using (var _b = PerfMon.Track(nameof(client.CompleteChatAsync)))
                        r = await client.CompleteChatAsync(messages, options, cancel).ConfigureAwait(false);
                    var v = r.Value;
                    resModel = v.Model;
                    usage = v.Usage;
                    refusal = v.Refusal;
                    fr = v.FinishReason;
                    foreach (var x in v.Content)
                        if (x.Kind == ChatMessageContentPartKind.Text)
                            sb.Append(x.Text);
                    foreach (var x in v.ToolCalls)
                        (toolCalls ??= new List<AiCompletionToolCall>()).Add(new AiCompletionToolCall
                        {
                            Id = x.Id,
                            Name = x.FunctionName,
                            Arguments = x.FunctionArguments?.ToString() ?? "{}",
                        });
                }
                else
                {
                    using var _b = PerfMon.Track(nameof(client.CompleteChatStreamingAsync));
                    StringBuilder e = null;
                    //  Tool calls are streamed in parts, the index identifies the call
                    var calls = new SortedDictionary<int, (String Id, String Name, StringBuilder Args)>();
                    await foreach (var sp in client.CompleteChatStreamingAsync(messages, options, cancel).ConfigureAwait(false))
                    {
                        resModel ??= sp.Model;
                        usage ??= sp.Usage;
                        fr ??= sp.FinishReason;
                        if (!String.IsNullOrEmpty(sp.RefusalUpdate))
                            (e ??= new StringBuilder()).Append(sp.RefusalUpdate);
                        var cu = sp.ContentUpdate;
                        if (cu != null)
                        {
                            bool changed = false;
                            foreach (var x in cu)
                            {
                                if (x.Kind != ChatMessageContentPartKind.Text)
                                    continue;
                                var t = x.Text;
                                if (String.IsNullOrEmpty(t))
                                    continue;
                                sb.Append(t);
                                changed = true;
                            }
                            if (changed)
                                await onText(sb.ToString()).ConfigureAwait(false);
                        }
                        var tcs = sp.ToolCallUpdates;
                        if (tcs == null)
                            continue;
                        foreach (var x in tcs)
                        {
                            var index = x.Index;
                            if (!calls.TryGetValue(index, out var call))
                            {
                                call = (null, null, new StringBuilder());
                                calls.Add(index, call);
                            }
                            if ((call.Id == null) && (x.ToolCallId != null))
                                call.Id = x.ToolCallId;
                            if ((call.Name == null) && (x.FunctionName != null))
                                call.Name = x.FunctionName;
                            calls[index] = call;
                            var au = x.FunctionArgumentsUpdate;
                            if (au != null)
                                call.Args.Append(au.ToString());
                        }
                    }
                    refusal = e?.ToString();
                    foreach (var x in calls.Values)
                        (toolCalls ??= new List<AiCompletionToolCall>()).Add(new AiCompletionToolCall
                        {
                            Id = x.Id ?? ("call_" + Guid.NewGuid().ToString("N").Substring(0, 24)),
                            Name = x.Name,
                            Arguments = x.Args.Length > 0 ? x.Args.ToString() : "{}",
                        });
                }
            }
            long tin = usage?.InputTokenCount ?? 0;
            long tout = usage?.OutputTokenCount ?? 0;
            long tcached = usage?.InputTokenDetails?.CachedTokenCount ?? 0;
            if (!String.IsNullOrEmpty(refusal))
                throw new AiCompletionException("Model refused to complete: " + refusal, "refusal");
            if (fr == ChatFinishReason.ContentFilter)
                throw new AiCompletionException("Omitted content due to a content filter flag", "content_filter");
            return new AiCompletionResult
            {
                Model = String.IsNullOrEmpty(resModel) ? model : resModel,
                Text = sb.ToString(),
                ToolCalls = toolCalls,
                FinishReason = GetFinishReason(toolCalls, fr == ChatFinishReason.Length),
                InputTokens = tin,
                CachedTokens = tcached,
                OutputTokens = tout,
            };
        }

        #endregion//Chat completions

        #region Responses

        /// <summary>
        /// Convert a completion request to responses input items (no previous response id is used, everything is sent)
        /// </summary>
        static List<ResponseItem> GetResponseItems(AiCompletionRequest request, OpenAiModels.Opt o)
        {
            var items = new List<ResponseItem>(request.Messages.Count + 2);
            //  The system prompt is sent as instructions (if supported by the model)
            var sp = request.SystemPrompt;
            if ((!o.System) && (!String.IsNullOrEmpty(sp)))
                items.Add(ResponseItem.CreateUserMessageItem(sp));
            //  Attachments of tool results, added as a user message after the tool results
            List<ResponseContentPart> pending = null;
            String pendingId = null;
            foreach (var m in request.Messages)
            {
                if ((pending != null) && (m.Role != AiCompletionRoles.Tool))
                {
                    items.Add(ResponseItem.CreateUserMessageItem(pending));
                    pending = null;
                    pendingId = null;
                }
                var parts = m.Parts;
                switch (m.Role)
                {
                    case AiCompletionRoles.User:
                        var up = new List<ResponseContentPart>(2 + (parts?.Count ?? 0));
                        //  The responses API have no participant name, so add it as a separate text part
                        if (!String.IsNullOrEmpty(m.Name))
                            up.Add(ResponseContentPart.CreateInputTextPart("[Message from: " + m.Name + "]"));
                        if (!String.IsNullOrEmpty(m.Text))
                            up.Add(ResponseContentPart.CreateInputTextPart(m.Text));
                        if (parts != null)
                            foreach (var x in parts)
                                up.Add(OpenAiModels.ToResponsePart(x));
                        if (up.Count <= 0)
                            up.Add(ResponseContentPart.CreateInputTextPart(""));
                        items.Add(ResponseItem.CreateUserMessageItem(up));
                        break;
                    case AiCompletionRoles.Assistant:
                        if (!String.IsNullOrEmpty(m.Text))
                            items.Add(ResponseItem.CreateAssistantMessageItem([ResponseContentPart.CreateOutputTextPart(m.Text, [])]));
                        var tcs = m.ToolCalls;
                        //  Function call items are added without an id, since they are not paired with their reasoning items (not stored)
                        if (tcs != null)
                            foreach (var x in tcs)
                                items.Add(ResponseItem.CreateFunctionCallItem(x.Id, x.Name, BinaryData.FromString(String.IsNullOrEmpty(x.Arguments) ? "{}" : x.Arguments)));
                        break;
                    case AiCompletionRoles.Tool:
                        if (String.IsNullOrEmpty(m.ToolCallId))
                            throw NoToolCallId();
                        var text = m.Text ?? "";
                        if (parts != null)
                        {
                            foreach (var x in parts)
                            {
                                if (x.Kind == AiContentPartKinds.Text)
                                {
                                    text = String.IsNullOrEmpty(text) ? x.Text : String.Concat(text, "\n", x.Text);
                                    continue;
                                }
                                pending ??= new List<ResponseContentPart>();
                                if (pendingId != m.ToolCallId)
                                {
                                    pending.Add(ResponseContentPart.CreateInputTextPart(GetPartsIntro(m)));
                                    pendingId = m.ToolCallId;
                                }
                                pending.Add(OpenAiModels.ToResponsePart(x));
                            }
                        }
                        items.Add(ResponseItem.CreateFunctionCallOutputItem(m.ToolCallId, text));
                        break;
                }
            }
            if (pending != null)
                items.Add(ResponseItem.CreateUserMessageItem(pending));
            return items;
        }

        async Task<AiCompletionResult> CompleteResponses(AiCompletionRequest request, String model, OpenAiModels.Opt o, AiTool[] tools, Func<String, Task> onText)
        {
            using var _a = PerfMon.Track(nameof(CompleteResponses));
            var options = new CreateResponseOptions(model, GetResponseItems(request, o))
            {
                //  Stateless, the whole conversation is in the request
                StoredOutputEnabled = false,
                ServiceTier = o.HaveTiers ? OpenAiModels.ResponseTiers[(int)DefaultTier] : null,
                MaxOutputTokenCount = request.MaxTokens,
            };
            var sp = request.SystemPrompt;
            if (o.System && (!String.IsNullOrEmpty(sp)))
                options.Instructions = sp;
            var temp = request.Temperature;
            if (o.Temp && temp.HasValue)
                options.Temperature = temp.Value;
            if (o.CanReason)
                options.ReasoningOptions = new ResponseReasoningOptions
                {
                    ReasoningEffortLevel = OpenAiModels.ResponseReasoningLevels[(int)o.GetReasoning(request.Reasoning ?? DefaultReasoning)],
                };
            if (tools.Length > 0)
            {
                var d = options.Tools;
                foreach (var x in tools)
                    d.Add(OpenAiModels.GetResponseTool(x));
                options.ParallelToolCallsEnabled = o.PTools;
                options.ToolChoice = request.ToolChoice switch
                {
                    AiCompletionToolChoices.None => ResponseToolChoice.CreateNoneChoice(),
                    AiCompletionToolChoices.Required => ResponseToolChoice.CreateRequiredChoice(),
                    _ => ResponseToolChoice.CreateAutoChoice(),
                };
            }
            var client = CreateResponsesClient();
            var cancel = request.Cancel;
            ResponseResult final = null;
            IList<ResponseItem> output = null;
            using (var _ = await (ChatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
            {
                if (onText == null)
                {
                    using var _b = PerfMon.Track(nameof(client.CreateResponseAsync));
                    final = (await client.CreateResponseAsync(options, cancel).ConfigureAwait(false)).Value;
                }
                else
                {
                    using var _b = PerfMon.Track(nameof(client.CreateResponseStreamingAsync));
                    options.StreamingEnabled = true;
                    var sb = new StringBuilder();
                    var doneItems = new SortedDictionary<int, ResponseItem>();
                    String failed = null;
                    await foreach (var u in client.CreateResponseStreamingAsync(options, cancel).ConfigureAwait(false))
                    {
                        switch (u)
                        {
                            case StreamingResponseOutputTextDeltaUpdate x:
                                if (!String.IsNullOrEmpty(x.Delta))
                                {
                                    sb.Append(x.Delta);
                                    await onText(sb.ToString()).ConfigureAwait(false);
                                }
                                break;
                            case StreamingResponseOutputItemDoneUpdate x:
                                if (x.Item != null)
                                    doneItems[x.OutputIndex] = x.Item;
                                break;
                            case StreamingResponseCompletedUpdate x:
                                final = x.Response;
                                break;
                            case StreamingResponseIncompleteUpdate x:
                                final = x.Response;
                                break;
                            case StreamingResponseFailedUpdate x:
                                final = x.Response;
                                break;
                            case StreamingResponseErrorUpdate x:
                                failed = String.Concat(x.Code, " - ", x.Message);
                                break;
                        }
                    }
                    if (failed != null)
                        throw new Exception("Response failed: " + failed);
                    output = final?.OutputItems;
                    if ((output == null) || (output.Count <= 0))
                        output = doneItems.Values.ToList();
                }
            }
            output ??= final?.OutputItems ?? [];
            //  Refusals
            var refusal = String.Concat(output.OfType<MessageResponseItem>().SelectMany(x => x.Content).Where(x => x.Kind == ResponseContentPartKind.Refusal).Select(x => x.Refusal));
            if (!String.IsNullOrEmpty(refusal))
                throw new AiCompletionException("Model refused to complete: " + refusal, "refusal");
            bool isTruncated = false;
            switch (final?.Status)
            {
                case ResponseStatus.Incomplete:
                    var reason = final.IncompleteStatusDetails?.Reason;
                    if (reason == ResponseIncompleteStatusReason.ContentFilter)
                        throw new AiCompletionException("Omitted content due to a content filter flag", "content_filter");
                    isTruncated = true;
                    break;
                case ResponseStatus.Failed:
                    var e = final.Error;
                    throw new Exception(e == null ? "Response failed" : String.Concat("Response failed: ", e.Code.ToString(), " - ", e.Message));
            }
            var text = new StringBuilder();
            List<AiCompletionToolCall> toolCalls = null;
            foreach (var x in output)
            {
                switch (x)
                {
                    case MessageResponseItem m:
                        foreach (var c in m.Content)
                            if (c.Kind == ResponseContentPartKind.OutputText)
                                text.Append(c.Text);
                        break;
                    case FunctionCallResponseItem f:
                        (toolCalls ??= new List<AiCompletionToolCall>()).Add(new AiCompletionToolCall
                        {
                            Id = f.CallId,
                            Name = f.FunctionName,
                            Arguments = f.FunctionArguments?.ToString() ?? "{}",
                        });
                        break;
                }
            }
            var usage = final?.Usage;
            return new AiCompletionResult
            {
                Model = String.IsNullOrEmpty(final?.Model) ? model : final.Model,
                Text = text.ToString(),
                ToolCalls = toolCalls,
                FinishReason = GetFinishReason(toolCalls, isTruncated),
                InputTokens = usage?.InputTokenCount ?? 0,
                CachedTokens = usage?.InputTokenDetails?.CachedTokenCount ?? 0,
                OutputTokens = usage?.OutputTokenCount ?? 0,
            };
        }

        #endregion//Responses
    }

#pragma warning restore OPENAI001
}
