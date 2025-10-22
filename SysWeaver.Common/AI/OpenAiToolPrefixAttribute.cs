using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// An attribute that sets the AI tool prefix for all tools declared in this type.
    /// Default is the type name.
    /// Can be set to empty for no tool prefix.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class OpenAiToolPrefixAttribute : Attribute
    {
        public OpenAiToolPrefixAttribute(String toolPrefix)
        {
            ToolPrefix = toolPrefix;
        }

        public readonly String ToolPrefix;
    }

}
