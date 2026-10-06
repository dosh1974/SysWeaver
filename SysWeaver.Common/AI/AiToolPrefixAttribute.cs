using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// An attribute that sets the AI tool prefix for all tools declared in this type.
    /// Default is the type name.
    /// Can be set to empty for no tool prefix.
    /// </summary>
    /// <remarks>
    /// The prefix is taken from the type that declares the tool method (inherited attributes are considered),
    /// and is inserted as {0} in the <see cref="AiToolNameAttribute"/> format.
    /// A null prefix behaves like not having the attribute (the type name is used).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class AiToolPrefixAttribute : Attribute
    {
        /// <summary>
        /// Set the AI tool prefix for all tools declared in this type.
        /// </summary>
        /// <param name="toolPrefix">The prefix, use an empty string for no prefix.</param>
        public AiToolPrefixAttribute(String toolPrefix)
        {
            ToolPrefix = toolPrefix;
        }

        /// <summary>
        /// The AI tool prefix, empty for no prefix.
        /// </summary>
        public readonly String ToolPrefix;
    }

}
