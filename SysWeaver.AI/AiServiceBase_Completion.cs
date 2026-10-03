using System;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    public abstract partial class AiServiceBase
    {
        /// <summary>
        /// Complete a conversation (stateless, the whole conversation is in the request).
        /// The default implementation uses a query session, the conversation is sent as a transcript and caller defined tools aren't supported (they are ignored).
        /// Override to use the API natively (multiple messages, tool calls, streaming).
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="onText">Optional callback with the accumulated text response (the default implementation calls it once, with the full response)</param>
        /// <returns>The result</returns>
        public virtual async Task<AiCompletionResult> Complete(AiCompletionRequest request, Func<String, Task> onText = null)
        {
            var tools = request.Tools;
            if ((request.ToolChoice == AiCompletionToolChoices.Required) && ((tools?.Count ?? 0) > 0))
                throw new AiCompletionException("Caller defined tools are not supported by this AI service", "tools_not_supported");
            var model = String.IsNullOrEmpty(request.Model) ? DefaultChatModel : request.Model;
            var q = CreateAiQuerySession(new AiSessionParams { Model = model, Reasoning = request.Reasoning });
            q.SystemPrompt = request.SystemPrompt;
            if (request.Temperature.HasValue && q.HaveTemperature)
                q.Temperature = request.Temperature.Value;
            long tin = 0;
            long tout = 0;
            var text = await q.Query(GetTranscript(request), null, (m, i, o) =>
            {
                tin += i;
                tout += o;
                return Task.CompletedTask;
            }).ConfigureAwait(false) ?? "";
            if (onText != null)
                await onText(text).ConfigureAwait(false);
            return new AiCompletionResult
            {
                Model = model,
                Text = text,
                FinishReason = AiCompletionFinishReasons.Stop,
                InputTokens = tin,
                OutputTokens = tout,
            };
        }

        /// <summary>
        /// Get the conversation of a request as a single text (the last user message is the query, previous messages are added as a transcript)
        /// </summary>
        protected static String GetTranscript(AiCompletionRequest request)
        {
            var messages = request.Messages;
            var ml = messages.Count;
            if (ml <= 0)
                return "";
            var last = messages[ml - 1];
            var lastIsUser = last.Role == AiCompletionRoles.User;
            if ((ml == 1) && lastIsUser)
                return last.Text ?? "";
            var sb = new StringBuilder();
            sb.Append("<conversation>\n");
            var tl = lastIsUser ? ml - 1 : ml;
            for (int i = 0; i < tl; ++i)
            {
                var m = messages[i];
                switch (m.Role)
                {
                    case AiCompletionRoles.User:
                        sb.Append(String.IsNullOrEmpty(m.Name) ? "User" : m.Name).Append(": ").Append(m.Text).Append('\n');
                        break;
                    case AiCompletionRoles.Assistant:
                        if (!String.IsNullOrEmpty(m.Text))
                            sb.Append("Assistant: ").Append(m.Text).Append('\n');
                        if (m.ToolCalls != null)
                            foreach (var c in m.ToolCalls)
                                sb.Append("Assistant called the tool ").Append(c.Name).Append(" with: ").Append(c.Arguments).Append('\n');
                        break;
                    case AiCompletionRoles.Tool:
                        sb.Append("Tool result").Append(String.IsNullOrEmpty(m.Name) ? "" : " from " + m.Name).Append(": ").Append(m.Text).Append('\n');
                        break;
                }
            }
            sb.Append("</conversation>\n\n");
            if (lastIsUser)
                sb.Append(last.Text);
            else
                sb.Append("Continue the conversation as the assistant.");
            return sb.ToString();
        }
    }
}
