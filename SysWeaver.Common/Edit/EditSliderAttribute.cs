using System;

namespace SysWeaver
{
    /// <summary>
    /// Put this to on a value field or property to use a slider (required a min and max)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]

    public sealed class EditSliderAttribute : Attribute
    {
        /// <summary>
        /// Put this to on a value field or property to use a slider (required a min and max)
        /// </summary>
        /// <param name="useSlider">True to use a slider</param>
        public EditSliderAttribute(bool useSlider = true)
        {
            UseSlider = useSlider;
        }
        public EditSliderAttribute(double step)
        {
            UseSlider = true;
            Step = step;
        }
        public readonly bool UseSlider;
        public readonly double Step;
    }

}
