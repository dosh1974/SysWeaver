using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// An attribute that allows certain members to be ignored when creating an AI tool specification
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class OpenAiIgnoreAttribute : Attribute
    {
        public OpenAiIgnoreAttribute(bool ignore = true)
        {
            Ignore = ignore;
        }

        public readonly bool Ignore;
    }
}
