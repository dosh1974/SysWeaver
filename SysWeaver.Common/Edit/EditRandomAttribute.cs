using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this on an integer or floating point field or property (ex: a seed) to let the user set a random value: the editor shows a
    /// dice button (and a "Random" member menu item) that sets a random value within the min and max of the member (see
    /// <see cref="EditRangeAttribute"/>, <see cref="EditMinAttribute"/>, <see cref="EditMaxAttribute"/>), or of the type when not set.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]

    public sealed class EditRandomAttribute : Attribute
    {
        /// <summary>
        /// Put this on an integer or floating point field or property to let the user set a random value
        /// </summary>
        /// <param name="random">True to allow random values (false turns off an inherited attribute)</param>
        public EditRandomAttribute(bool random = true)
        {
            Random = random;
        }
        /// <summary>
        /// True to allow random values
        /// </summary>
        public readonly bool Random;
    }

}
