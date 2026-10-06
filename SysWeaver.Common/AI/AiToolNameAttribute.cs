using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// An attribute that overrides the name (that the AI sees) of the tool.
    /// Default is "{0}{1}", i.e. the tool prefix (see <see cref="AiToolPrefixAttribute"/>) followed by the method name.
    /// </summary>
    /// <remarks>
    /// The name is used as a <see cref="String.Format(String, Object, Object)"/> format string, so literal braces must be escaped as "{{" and "}}".
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AiToolNameAttribute : Attribute
    {
        /// <summary>
        /// Name (that the AI sees) of the tool.
        /// {0} is replaced with the AI tool prefix.
        /// {1} is replaced with the method name.
        /// Empty or null will use the "{0}{1}" name for the tool.
        /// </summary>
        /// <param name="toolName">The tool name format string.</param>
        public AiToolNameAttribute(String toolName)
        {
            ToolName = toolName;
        }

        /// <summary>
        /// The tool name format string ({0} = tool prefix, {1} = method name), null or empty means "{0}{1}".
        /// </summary>
        public readonly String ToolName;
    }


}
