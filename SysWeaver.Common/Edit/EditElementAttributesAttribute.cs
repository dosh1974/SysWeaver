using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this on a collection member to specify the type that contain the (edit) attributes for the collection elements
    /// </summary>
    /// <remarks>
    /// The type must have a member with the same name as the member with this attribute, the attributes (and documentation) of that member are used for the elements.
    /// Read by the type service (SysWeaver.MicroService.Edit).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditElementAttributesAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the type that contain attributes for the collection element
        /// </summary>
        /// <param name="t">The type that contain a member (with the same name) with the attributes for the collection element</param>
        public EditElementAttributesAttribute(Type t)
        {
            T = t;
        }
        /// <summary>
        /// The type that contain a member (with the same name) with the attributes for the collection element
        /// </summary>
        public readonly Type T;
    }

}
