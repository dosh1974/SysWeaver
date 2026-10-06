using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// An attribute that marks a public field or property as optional in the JSON schema generated for an AI tool
    /// (the member's description states that it's optional and, for fields, it's left out of the "required" list).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class AiOptionalAttribute : Attribute
    {
        /// <summary>
        /// Mark (or explicitly unmark) a member as optional in an AI tool schema.
        /// </summary>
        /// <param name="optional">True if the member is optional, false if it's required (same as not having the attribute).</param>
        public AiOptionalAttribute(bool optional = true)
        {
            Optional = optional;
        }

        /// <summary>
        /// True if the member is optional.
        /// </summary>
        public readonly bool Optional;
    }
}
