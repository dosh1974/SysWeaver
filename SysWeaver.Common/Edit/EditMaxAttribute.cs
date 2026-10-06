using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the maximum allowed value, if it's a string this is the maximum number of chars
    /// </summary>
    /// <remarks>
    /// For collections this is the maximum number of elements.
    /// Ignored if the member has an <see cref="EditRangeAttribute"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]

    public sealed class EditMaxAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the maximum allowed value, if it's a string this is the maximum number of chars
        /// </summary>
        /// <param name="maxValue">The maximum allowed value, inclusive (must be convertible to the member type)</param>
        public EditMaxAttribute(Object maxValue)
        {
            MaxValue = maxValue;
        }
        /// <summary>
        /// The maximum allowed value, inclusive
        /// </summary>
        public readonly Object MaxValue;
    }

}
