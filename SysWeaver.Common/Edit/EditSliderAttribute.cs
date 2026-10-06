using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this on a value field or property to use a slider (requires a min and max, see <see cref="EditRangeAttribute"/>)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditSliderAttribute : Attribute
    {
        /// <summary>
        /// Put this on a value field or property to use a slider (requires a min and max)
        /// </summary>
        /// <param name="useSlider">True to use a slider</param>
        public EditSliderAttribute(bool useSlider = true)
        {
            UseSlider = useSlider;
        }
        /// <summary>
        /// Use a slider with a specific step size.
        /// </summary>
        /// <param name="step">The step size, zero or less uses the default step</param>
        public EditSliderAttribute(double step)
        {
            UseSlider = true;
            Step = step;
        }
        /// <summary>
        /// True to use a slider
        /// </summary>
        public readonly bool UseSlider;
        /// <summary>
        /// The step size, zero or less uses the default step
        /// </summary>
        public readonly double Step;
    }

}
