using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the type that contain attributes for the collection key
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditKeyAttributesAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the type that contain attributes for the collection key
        /// </summary>
        /// <param name="t">The name of the type that contain the attributes for the collection key</param>
        public EditKeyAttributesAttribute(Type t)
        {
            T = t;
        }
        public readonly Type T;
    }

}
