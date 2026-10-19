using Anthropic.Models.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    public sealed partial class AnthropicAiService
    {
        /// <summary>
        /// Complete a conversation (stateless, the whole conversation is in the request).
        /// Tools in the request are defined by the caller, tool calls are returned (not executed).
        /// Attachments (images, pdf and text files) of tool results are sent as content of the tool results.
        /// Return the Signature of tool calls unchanged in the next request, it holds the thinking blocks of the response (required to continue after a tool call when thinking is used).
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="onText">Optional callback with the accumulated text response whenever it changes</param>
        /// <returns>The result</returns>
        public override async Task<AiCompletionResult> Complete(AiCompletionRequest request, Func<String, Task> onText = null)
        {
            using var _a = PerfMon.Track(nameof(Complete));
            var model = String.IsNullOrEmpty(request.Model) ? DefaultChatModel : request.Model;
            var s = AnthropicModels.GetSettings(AnthropicModels.GetOptions(model), request.Reasoning ?? DefaultReasoning, request.MaxTokens ?? DefaultMaxTokens);
            var rt = request.Tools;
            var tools = (rt?.Count ?? 0) > 0 ? rt.Select(x => AnthropicModels.GetTool(AiTool.CreateExternal(x))).ToArray() : null;
            var p = AnthropicModels.BuildParams(model, s, request.SystemPrompt, tools, request.ToolChoice, request.Temperature, GetMessages(request), PromptCaching);
            var text = new StringBuilder();
            Func<String, Task> onDelta = onText == null ? null : t =>
            {
                text.Append(t);
                return onText(text.ToString());
            };
            Anthropic.Models.Messages.Message r;
            using (var _ = await (ChatLock?.Lock() ?? AsyncLock.NoLock).ConfigureAwait(false))
            {
                using var _m = PerfMon.Track(nameof(AnthropicModels.Send));
                r = await AnthropicModels.Send(Client, p, onDelta, request.Cancel).ConfigureAwait(false);
            }
            if (onText == null)
                text.Append(AnthropicModels.GetText(r));
            List<AiCompletionToolCall> toolCalls = null;
            String signature = null;
            foreach (var b in r.Content)
            {
                if (!b.TryPickToolUse(out var tu))
                    continue;
                (toolCalls ??= new List<AiCompletionToolCall>()).Add(new AiCompletionToolCall
                {
                    Id = tu.ID,
                    Name = tu.Name,
                    Arguments = JsonSerializer.Serialize(tu.Input),
                    //  The content of the response is stored with the first call
                    Signature = signature == null ? (signature = GetSignature(r)) : null,
                });
            }
            bool isTruncated = false;
            if (toolCalls == null)
            {
                var err = AnthropicModels.GetStopError(r);
                if (err != null)
                {
                    var sr = r.StopReason?.Raw();
                    if ((sr == "max_tokens") || (sr == "model_context_window_exceeded"))
                        isTruncated = true;
                    else if (err.StartsWith(AnthropicModels.ContentFilterError, StringComparison.Ordinal))
                        throw new AiCompletionException(err.Substring(7), "content_filter");
                    else
                        throw new Exception(err.Substring(7));
                }
            }
            var (tin, tcached, tout) = AnthropicModels.GetUsage(r);
            return new AiCompletionResult
            {
                Model = String.IsNullOrEmpty(r.Model) ? model : r.Model,
                Text = text.ToString(),
                ToolCalls = toolCalls,
                FinishReason = toolCalls != null ? AiCompletionFinishReasons.ToolCalls : (isTruncated ? AiCompletionFinishReasons.Length : AiCompletionFinishReasons.Stop),
                InputTokens = tin,
                CachedTokens = tcached,
                OutputTokens = tout,
            };
        }

        #region Completion helpers

        /// <summary>
        /// An entry of a tool call signature, the content blocks (in order) of the assistant message
        /// </summary>
        sealed class SigBlock
        {
            /// <summary>
            /// Thinking text (thinking blocks), or text (text blocks)
            /// </summary>
            public String T { get; set; }
            /// <summary>
            /// Signature (thinking blocks)
            /// </summary>
            public String S { get; set; }
            /// <summary>
            /// Data (redacted thinking blocks)
            /// </summary>
            public String R { get; set; }
            /// <summary>
            /// Tool use id (tool use blocks)
            /// </summary>
            public String U { get; set; }
        }

        static readonly JsonSerializerOptions SigOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Get the signature of a response (the content blocks in order, with thinking blocks), null if there are no thinking blocks
        /// </summary>
        static String GetSignature(Anthropic.Models.Messages.Message r)
        {
            var blocks = new List<SigBlock>(r.Content.Count);
            bool haveThinking = false;
            foreach (var b in r.Content)
            {
                if (b.TryPickThinking(out var th))
                {
                    blocks.Add(new SigBlock { T = th.Thinking, S = th.Signature });
                    haveThinking = true;
                }
                else if (b.TryPickRedactedThinking(out var rt))
                {
                    blocks.Add(new SigBlock { R = rt.Data });
                    haveThinking = true;
                }
                else if (b.TryPickText(out var t))
                {
                    if (!String.IsNullOrEmpty(t.Text))
                        blocks.Add(new SigBlock { T = t.Text });
                }
                else if (b.TryPickToolUse(out var tu))
                    blocks.Add(new SigBlock { U = tu.ID });
            }
            return haveThinking ? JsonSerializer.Serialize(blocks, SigOptions) : null;
        }

        static IReadOnlyDictionary<String, JsonElement> ParseArgs(String json)
        {
            if (String.IsNullOrWhiteSpace(json))
                return new Dictionary<String, JsonElement>();
            try
            {
                return JsonSerializer.Deserialize<Dictionary<String, JsonElement>>(json) ?? new Dictionary<String, JsonElement>();
            }
            catch (JsonException ex)
            {
                throw new AiCompletionException("The arguments of a tool call must be a json object: " + ex.Message, "invalid_request");
            }
        }

        /// <summary>
        /// Get the content of an assistant message.
        /// If a tool call have a signature (with the content blocks of the response), the content is rebuilt in the original order (with the thinking blocks), else text then tool calls.
        /// </summary>
        static List<ContentBlockParam> GetAssistantContent(AiCompletionMessage m)
        {
            var tcs = m.ToolCalls;
            var uses = new List<ToolUseBlockParam>(tcs?.Count ?? 0);
            String sig = null;
            if (tcs != null)
                foreach (var x in tcs)
                {
                    uses.Add(new ToolUseBlockParam { ID = x.Id, Name = x.Name, Input = ParseArgs(x.Arguments) });
                    sig ??= String.IsNullOrEmpty(x.Signature) ? null : x.Signature;
                }
            List<SigBlock> sigBlocks = null;
            if (sig != null)
            {
                try
                {
                    sigBlocks = JsonSerializer.Deserialize<List<SigBlock>>(sig);
                }
                catch (JsonException)
                {
                    throw new AiCompletionException("The signature of a tool call is invalid, return it unchanged", "invalid_request");
                }
            }
            var ret = new List<ContentBlockParam>(uses.Count + 1 + (sigBlocks?.Count ?? 0));
            var text = m.Text ?? "";
            if (sigBlocks != null)
            {
                //  The text of the message must be unchanged to use the original text blocks
                var sigText = String.Concat(sigBlocks.Where(x => (x.S == null) && (x.R == null) && (x.U == null)).Select(x => x.T));
                bool sameText = String.Equals(sigText, text, StringComparison.Ordinal);
                var byId = uses.ToDictionary(x => x.ID, StringComparer.Ordinal);
                foreach (var x in sigBlocks)
                {
                    if (x.R != null)
                        ret.Add(new RedactedThinkingBlockParam { Data = x.R });
                    else if (x.S != null)
                        ret.Add(new ThinkingBlockParam { Thinking = x.T ?? "", Signature = x.S });
                    else if (x.U != null)
                    {
                        if (byId.Remove(x.U, out var u))
                            ret.Add(u);
                    }
                    else if (sameText && (!String.IsNullOrEmpty(x.T)))
                        ret.Add(new TextBlockParam { Text = x.T });
                }
                if ((!sameText) && (text.Length > 0))
                {
                    //  Put the text before the first tool use
                    var i = ret.FindIndex(x => x.TryPickToolUse(out var _));
                    ret.Insert(i < 0 ? ret.Count : i, new TextBlockParam { Text = text });
                }
                //  Tool calls not in the signature
                foreach (var u in uses)
                    if (byId.ContainsKey(u.ID))
                        ret.Add(u);
                return ret;
            }
            if (text.Length > 0)
                ret.Add(new TextBlockParam { Text = text });
            foreach (var u in uses)
                ret.Add(u);
            return ret;
        }

        /// <summary>
        /// Convert a completion request to Anthropic messages.
        /// Tool results are tool result blocks in user messages (consecutive tool results are merged, as required by the API).
        /// </summary>
        static List<MessageParam> GetMessages(AiCompletionRequest request)
        {
            var messages = new List<MessageParam>(request.Messages.Count);
            //  Tool results of consecutive tool messages
            List<ContentBlockParam> results = null;
            void Flush()
            {
                if (results == null)
                    return;
                messages.Add(new MessageParam { Role = Role.User, Content = results });
                results = null;
            }
            foreach (var m in request.Messages)
            {
                if (m.Role != AiCompletionRoles.Tool)
                    Flush();
                var mp = m.Parts;
                switch (m.Role)
                {
                    case AiCompletionRoles.User:
                        var ub = new List<ContentBlockParam>(2 + (mp?.Count ?? 0));
                        //  The API have no participant name, so add it as a separate text block
                        if (!String.IsNullOrEmpty(m.Name))
                            ub.Add(new TextBlockParam { Text = "[Message from: " + m.Name + "]" });
                        if (!String.IsNullOrEmpty(m.Text))
                            ub.Add(new TextBlockParam { Text = m.Text });
                        if (mp != null)
                            foreach (var x in mp)
                                ub.Add(AnthropicModels.ToBlock(x));
                        if (ub.Count <= 0)
                            ub.Add(new TextBlockParam { Text = "..." });
                        messages.Add(new MessageParam { Role = Role.User, Content = ub });
                        break;
                    case AiCompletionRoles.Assistant:
                        var ab = GetAssistantContent(m);
                        //  Empty assistant messages are not allowed
                        if (ab.Count > 0)
                            messages.Add(new MessageParam { Role = Role.Assistant, Content = ab });
                        break;
                    case AiCompletionRoles.Tool:
                        var tid = m.ToolCallId;
                        if (String.IsNullOrEmpty(tid))
                            throw new AiCompletionException("The ToolCallId of a tool message must be set to the id of a previous tool call", "invalid_request");
                        var tb = new List<Block>(1 + (mp?.Count ?? 0));
                        if (!String.IsNullOrEmpty(m.Text))
                            tb.Add(new TextBlockParam { Text = m.Text });
                        if (mp != null)
                            foreach (var x in mp)
                                tb.Add(AnthropicModels.ToToolResultBlock(x));
                        (results ??= new List<ContentBlockParam>()).Add(new ToolResultBlockParam
                        {
                            ToolUseID = tid,
                            Content = tb,
                        });
                        break;
                }
            }
            Flush();
            return messages;
        }

        #endregion//Completion helpers
    }
}
