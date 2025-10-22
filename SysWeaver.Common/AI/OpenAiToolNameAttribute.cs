using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// An attribute that overides the name (that the AI sees) of the tool.
    /// Default is the method name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class OpenAiToolNameAttribute : Attribute
    {
        /// <summary>
        /// Name (that the AI sees) of the tool.
        /// {0} is replaced with the AI tool prefix.
        /// {1} is replaced with the method name.
        /// Empty or null will use the "{0}{1}" name for the tool.
        /// </summary>
        /// <param name="toolName"></param>
        public OpenAiToolNameAttribute(String toolName)
        {
            ToolName = toolName;
        }

        public readonly String ToolName;
    }


}
