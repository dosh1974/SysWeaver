using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the minimum allowed value, if it's a string this is the minimum number of chars
    /// </summary>
    /// <remarks>
    /// For collections this is the minimum number of elements.
    /// Ignored if the member has an <see cref="EditRangeAttribute"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]

    public sealed class EditMinAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the minimum allowed value, if it's a string this is the minimum number of chars
        /// </summary>
        /// <param name="minValue">The minimum allowed value, inclusive (must be convertible to the member type)</param>
        public EditMinAttribute(Object minValue)
        {
            MinValue = minValue;
        }
        /// <summary>
        /// The minimum allowed value, inclusive
        /// </summary>
        public readonly Object MinValue;
    }


    /// <summary>
    /// Put this on a DateTime or DateOnly property to indicate that the date part is edited as unspecified
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]

    public sealed class EditDateUnspecifiedAttribute : Attribute
    {

    }




}
