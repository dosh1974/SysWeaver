using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the display name to use for editing
    /// </summary>
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
        public readonly String Name;
    }

}
