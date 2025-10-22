using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the maximum allowed value, if it's a string this is the maximum number of chars
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]

    public sealed class EditMaxAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the maximum allowed value, if it's a string this is the maximum number of chars
        /// </summary>
        /// <param name="maxValue">The maximum allow value, inclusive</param>
        public EditMaxAttribute(Object maxValue)
        {
            MaxValue = maxValue;
        }
        public readonly Object MaxValue;
    }

}
