using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the default value to use when editing
    /// </summary>
    /// <remarks>
    /// Read by the type service (SysWeaver.MicroService.Edit) when describing a type for the web based editor.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditDefaultAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the default value to use when editing
        /// </summary>
        /// <param name="def">The default value, must be convertible to the member type (null is allowed)</param>
        public EditDefaultAttribute(Object def)
        {
            Def = def;
        }
        /// <summary>
        /// The default value
        /// </summary>
        public readonly Object Def;
    }



}
