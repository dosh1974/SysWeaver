using System;

namespace SysWeaver
{
    /// <summary>
    /// The unit of a number member marked with <see cref="EditUnitAttribute"/>.
    /// The stored value is always a plain number in this unit; the editor shows the unit, a readable form ("= 2 h 30 min", "= 2 MB", "= 25 %")
    /// and converts typed values with a unit ("90s", "1h 30m", "2 MB", "512k", "25%").
    /// </summary>
    public enum EditUnits
    {
        /// <summary>
        /// A duration in milliseconds
        /// </summary>
        Milliseconds = 0,
        /// <summary>
        /// A duration in seconds
        /// </summary>
        Seconds,
        /// <summary>
        /// A duration in minutes
        /// </summary>
        Minutes,
        /// <summary>
        /// A duration in hours
        /// </summary>
        Hours,
        /// <summary>
        /// A duration in days
        /// </summary>
        Days,
        /// <summary>
        /// A size in bytes
        /// </summary>
        Bytes,
        /// <summary>
        /// A size in kilo bytes (1024 bytes)
        /// </summary>
        KiloBytes,
        /// <summary>
        /// A size in mega bytes (1024 * 1024 bytes)
        /// </summary>
        MegaBytes,
        /// <summary>
        /// A size in giga bytes (1024 * 1024 * 1024 bytes)
        /// </summary>
        GigaBytes,
        /// <summary>
        /// A percentage, 100 is all (use <see cref="EditRangeAttribute"/> for the limits, ex: 0 to 100)
        /// </summary>
        Percent,
        /// <summary>
        /// A ratio, 1 is all (shown as a percent, "25%" is typed as 0.25)
        /// </summary>
        Ratio,
    }


    /// <summary>
    /// Put this on a number member (int, long, double ...) to say what unit the value is in, ex: [EditUnit(EditUnits.Seconds)] on "NetworkTimeout".
    /// The editor shows the unit after the value and a readable form, and accepts values typed with a unit ("90s", "1h 30m", "2 MB", "25%"), converted to the member's unit.
    /// <see cref="EditRangeAttribute"/>, <see cref="EditMinAttribute"/>, <see cref="EditMaxAttribute"/>, <see cref="EditSliderAttribute"/> and <see cref="EditDefaultAttribute"/> work as for any number (in the member's unit).
    /// </summary>
    /// <remarks>
    /// This is an editor type (encoded as "Unit:&lt;Unit&gt;", see <see cref="EditTypes.Unit"/>), so it can't be combined with another <see cref="EditTypeAttribute"/> (ex: <see cref="EditHideIfAttribute"/>).
    /// Clients that don't know the spec show the plain number editor.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class EditUnitAttribute : EditTypeAttribute
    {
        /// <summary>
        /// The value of this number member is in a unit
        /// </summary>
        /// <param name="unit">The unit of the stored value</param>
        public EditUnitAttribute(EditUnits unit)
            : base(String.Concat(EditTypes.Unit, ":", unit.ToString()))
        {
            Unit = unit;
        }

        /// <summary>
        /// The unit of the stored value
        /// </summary>
        public readonly EditUnits Unit;
    }

}
