using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the allowed value range, if it's a string this is the range of the number of chars
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]

    public sealed class EditRangeAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the allowed value range, if it's a string this is the range of the number of chars
        /// </summary>
        /// <param name="minValue">The minimum allow value, inclusive</param>
        /// <param name="maxValue">The maximum allow value, inclusive</param>
        public EditRangeAttribute(Object minValue, Object maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }
        public readonly Object MinValue;
        public readonly Object MaxValue;
    }


}
