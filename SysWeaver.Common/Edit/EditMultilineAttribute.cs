using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the default value to use when editing
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false)]

    public sealed class EditMultilineAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the default value to use when editing
        /// </summary>
        /// <param name="allowMultipleLines">True to allow for multi line </param>
        public EditMultilineAttribute(bool allowMultipleLines = true)
        {
            AllowMultiLine = allowMultipleLines;
        }
        public readonly bool AllowMultiLine;
    }


}
