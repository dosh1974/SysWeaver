using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this on a string member to allow multiple lines of text when editing
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false)]

    public sealed class EditMultilineAttribute : Attribute
    {
        /// <summary>
        /// Put this on a string member to allow multiple lines of text when editing
        /// </summary>
        /// <param name="allowMultipleLines">True to allow for multi line</param>
        public EditMultilineAttribute(bool allowMultipleLines = true)
        {
            AllowMultiLine = allowMultipleLines;
        }
        /// <summary>
        /// True if multiple lines are allowed
        /// </summary>
        public readonly bool AllowMultiLine;
    }


}
