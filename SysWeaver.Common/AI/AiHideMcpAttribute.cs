using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Put this attribute on an AI tool method that only makes sense in an AI chat (ex: tools that displays something in the chat).
    /// The tool is not exposed to MCP clients (it's still available in the AI chat).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AiHideMcpAttribute : Attribute
    {
        public AiHideMcpAttribute(bool hide = true)
        {
            Hide = hide;
        }

        public readonly bool Hide;
    }
}
