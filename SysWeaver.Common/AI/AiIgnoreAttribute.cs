using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// An attribute that excludes a public field or property from the JSON schema generated for an AI tool's argument or return type.
    /// </summary>
    /// <remarks>
    /// The member is still serialized normally, it's only omitted from the schema that the AI sees.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class AiIgnoreAttribute : Attribute
    {
        /// <summary>
        /// Exclude (or explicitly include) a member from an AI tool schema.
        /// </summary>
        /// <param name="ignore">True to exclude the member, false to include it (same as not having the attribute).</param>
        public AiIgnoreAttribute(bool ignore = true)
        {
            Ignore = ignore;
        }

        /// <summary>
        /// True if the member should be excluded from the AI tool schema.
        /// </summary>
        public readonly bool Ignore;
    }
}
