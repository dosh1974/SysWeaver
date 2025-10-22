using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the type that contain attributes for the collection element
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditElementAttributesAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the type that contain attributes for the collection element
        /// </summary>
        /// <param name="t">The name of the type that contain the attributes for the collection element</param>
        public EditElementAttributesAttribute(Type t)
        {
            T = t;
        }
        public readonly Type T;
    }

}
