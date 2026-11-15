using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// A message in a LLamaSharp conversation (formatted using the chat template of the model)
    /// </summary>
    public sealed class LlamaSharpMessage
    {
        /// <inheritdoc />
        public override string ToString() => String.Concat(Role, ": ", Content);

        /// <summary>
        /// Create an empty message (for deserialization)
        /// </summary>
        public LlamaSharpMessage()
        {
        }

        /// <summary>
        /// Create a message
        /// </summary>
        /// <param name="role">The role, "user" or "assistant"</param>
        /// <param name="content">The content of the message</param>
        /// <param name="isToolCall">True if the message is a tool call made by the model</param>
        /// <param name="isToolResponse">True if the message is the response of a tool call</param>
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
