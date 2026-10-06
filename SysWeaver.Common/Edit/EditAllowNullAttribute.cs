using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this attribute on a member to allow it to be null (for class types and nullable value types)
    /// </summary>
    /// <remarks>
    /// Read by the type service (SysWeaver.MicroService.Edit) when describing a type for the web based editor.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditAllowNullAttribute : Attribute
    {
        /// <summary>
        /// Put this attribute on a member to allow it to be null (for class types and nullable value types)
        /// </summary>
        /// <param name="allowNull">True to allow it to be null</param>
        public EditAllowNullAttribute(bool allowNull = true)
        {
            AllowNull = allowNull;
        }
        /// <summary>
        /// True if the member may be null
        /// </summary>
        public readonly bool AllowNull;
    }



}
