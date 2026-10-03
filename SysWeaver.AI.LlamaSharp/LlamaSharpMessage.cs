using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// A message in a LLamaSharp conversation (formatted using the chat template of the model)
    /// </summary>
    public sealed class LlamaSharpMessage
    {
        public override string ToString() => String.Concat(Role, ": ", Content);

        public LlamaSharpMessage()
        {
        }

        public LlamaSharpMessage(String role, String content, bool isToolCall = false, bool isToolResponse = false)
        {
            Role = role;
            Content = content;
            IsToolCall = isToolCall;
            IsToolResponse = isToolResponse;
        }

        /// <summary>
        /// The role, "user" or "assistant"
        /// </summary>
        public String Role;

        /// <summary>
        /// The content of the message
        /// </summary>
        public String Content;

        /// <summary>
        /// True if this is an assistant message containing tool calls
        /// </summary>
        public bool IsToolCall;

        /// <summary>
        /// True if this is a message containing tool responses
        /// </summary>
        public bool IsToolResponse;
    }
}
