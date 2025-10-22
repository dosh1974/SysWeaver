using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to specify the minimum allowed value, if it's a string this is the minimum number of chars
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]

    public sealed class EditMinAttribute : Attribute
    {
        /// <summary>
        /// Put this to specify the minimum allowed value, if it's a string this is the minimum number of chars
        /// </summary>
        /// <param name="minValue">The minimum allow value, inclusive</param>
        public EditMinAttribute(Object minValue)
        {
            MinValue = minValue;
        }
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
