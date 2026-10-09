using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Put this attribute on an AI tool method that only makes sense in an AI chat (ex: tools that displays something in the chat).
    /// The tool is not exposed to MCP clients (it's still available in the AI chat).
    /// </summary>
    /// <remarks>
    /// Only has an effect on methods that are AI tools (see <see cref="AiToolAttribute"/> and <see cref="AiUseAttribute"/>).
    /// Checked by the MCP service when registering the tools of an instance (looked up with inheritance).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AiHideMcpAttribute : Attribute
    {
        /// <summary>
        /// Hide (or explicitly expose) an AI tool method from MCP clients.
        /// </summary>
        /// <param name="hide">True to hide the tool from MCP clients, false to expose it (same as not having the attribute).</param>
        public AiHideMcpAttribute(bool hide = true)
        {
            Hide = hide;
        }

        /// <summary>
        /// True if the tool should be hidden from MCP clients.
        /// </summary>
        public readonly bool Hide;
    }
}
