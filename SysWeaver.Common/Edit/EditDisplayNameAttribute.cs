using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the display name to use for editing, default is the member name with camel casing removed
    /// </summary>
    /// <remarks>
    /// Read by the type service (SysWeaver.MicroService.Edit) when describing a type for the web based editor.
    /// If the member doesn't have the attribute, the attribute of the declaring type is used (if any).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditDisplayNameAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the display name to use for editing
        /// </summary>
        /// <param name="name">The name to show as a display name</param>
        public EditDisplayNameAttribute(String name)
        {
            Name = name;
        }
        /// <summary>
        /// The display name, null or empty means the default
        /// </summary>
        public readonly String Name;
    }

}
