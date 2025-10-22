using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the default value to use when editing
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditDefaultAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the default value to use when editing
        /// </summary>
        /// <param name="def">The default value, must be convertable</param>
        public EditDefaultAttribute(Object def)
        {
            Def = def;
        }
        public readonly Object Def;
    }



}
