using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the allowed value range, if it's a string this is the range of the number of chars
    /// </summary>
    /// <remarks>
    /// For collections this is the range of the number of elements.
    /// Takes precedence over <see cref="EditMinAttribute"/> and <see cref="EditMaxAttribute"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]

    public sealed class EditRangeAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the allowed value range, if it's a string this is the range of the number of chars
        /// </summary>
        /// <param name="minValue">The minimum allowed value, inclusive (must be convertible to the member type)</param>
        /// <param name="maxValue">The maximum allowed value, inclusive (must be convertible to the member type)</param>
        public EditRangeAttribute(Object minValue, Object maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }
        /// <summary>
        /// The minimum allowed value, inclusive
        /// </summary>
        public readonly Object MinValue;
        /// <summary>
        /// The maximum allowed value, inclusive
        /// </summary>
        public readonly Object MaxValue;
    }


}
