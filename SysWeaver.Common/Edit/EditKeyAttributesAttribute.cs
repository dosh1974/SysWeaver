using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this on a dictionary member to specify the type that contain the (edit) attributes for the collection keys
    /// </summary>
    /// <remarks>
    /// The type must have a member with the same name as the member with this attribute, the attributes (and documentation) of that member are used for the keys.
    /// Read by the type service (SysWeaver.MicroService.Edit).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditKeyAttributesAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the type that contain attributes for the collection key
        /// </summary>
        /// <param name="t">The type that contain a member (with the same name) with the attributes for the collection key</param>
        public EditKeyAttributesAttribute(Type t)
        {
            T = t;
        }
        /// <summary>
        /// The type that contain a member (with the same name) with the attributes for the collection key
        /// </summary>
        public readonly Type T;
    }

}
