using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Explicitly map a property or field to a workflow value (matching is case insensitive).
    /// Input types (T):
    ///   "tag.input" maps to an input of the node(s) annotated with "$tag".
    ///   "tag" maps a bool to a node tag, false will bypass the node(s).
    /// Output types (R):
    ///   "tag" maps to the output of the node(s) annotated with "#tag".
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ComfyUiNameAttribute : Attribute
    {
        public ComfyUiNameAttribute(String name)
        {
            Name = name;
        }

        /// <summary>
        /// The name of the workflow value, "tag.input" or "tag"
        /// </summary>
        public readonly String Name;
    }

    /// <summary>
    /// Don't try to map this property or field to a workflow value (no warnings are emitted)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ComfyUiIgnoreAttribute : Attribute
    {
    }

}
