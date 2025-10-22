using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this attribute on a member to allow it to be null (for class types)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditAllowNullAttribute : Attribute
    {
        /// <summary>
        /// Put this attribute on a member to allow it to be null (for class types)
        /// </summary>
        /// <param name="allowNull">True to allow it to be null</param>
        public EditAllowNullAttribute(bool allowNull = true)
        {
            AllowNull = allowNull;
        }
        public readonly bool AllowNull;
    }



}
