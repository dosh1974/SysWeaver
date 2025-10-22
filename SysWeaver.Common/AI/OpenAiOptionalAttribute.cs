using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// An attribute that adds the explanation that a member is optional in an AI tool specification
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class OpenAiOptionalAttribute : Attribute
    {
        public OpenAiOptionalAttribute(bool optional = true)
        {
            Optional = optional;
        }

        public readonly bool Optional;
    }
}
