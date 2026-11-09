using System;
using System.Globalization;

namespace SysWeaver
{
    /// <summary>
    /// How a color is stored in a member marked with <see cref="EditColorAttribute"/>.
    /// Components are red, green, blue and (optional) alpha; "0..1" means normalized floats.
    /// </summary>
    public enum EditColorFormat
    {
        /// <summary>
        /// Decide from the member type: String = <see cref="Css"/>, integers = <see cref="UInt32Argb"/>,
        /// byte arrays = <see cref="Rgb8"/> / <see cref="Rgba8"/> (by length), float / double arrays = <see cref="Rgb"/> / <see cref="Rgba"/> (by length),
        /// objects with R, G, B (, A) members = a struct of floats in 0..1.
        /// </summary>
        Auto = 0,
        /// <summary>
        /// A css color string, stored as "#rrggbb" (or "#rrggbbaa" when <see cref="EditColorAttribute.Alpha"/> is true).
        /// Any css color can be typed or pasted (named colors, rgb(), hsl() ...); a valid typed css color is kept as typed.
        /// </summary>
        Css,
        /// <summary>
        /// float[3] / double[3] (or an R, G, B struct) with components in 0..1.
        /// </summary>
        Rgb,
        /// <summary>
        /// float[4] / double[4] (or an R, G, B, A struct) with components in 0..1.
        /// </summary>
        Rgba,
        /// <summary>
        /// float[3] / double[3] (or an R, G, B struct), high dynamic range: components may exceed 1 (up to <see cref="EditColorAttribute.MaxIntensity"/>).
        /// The editor shows the color and an intensity (the stored value is color × intensity).
        /// </summary>
        HdrRgb,
        /// <summary>
        /// float[4] / double[4] (or an R, G, B, A struct), high dynamic range: r, g, b may exceed 1, alpha is 0..1.
        /// </summary>
        HdrRgba,
        /// <summary>
        /// byte[3] with components in 0..255.
        /// </summary>
        Rgb8,
        /// <summary>
        /// byte[4] with components in 0..255.
        /// </summary>
        Rgba8,
        /// <summary>
        /// An integer 0xRRGGBB (no alpha).
        /// </summary>
        UInt32Rgb,
        /// <summary>
        /// An integer 0xRRGGBBAA.
        /// </summary>
        UInt32Rgba,
        /// <summary>
        /// An integer 0xAARRGGBB (System.Drawing.Color.ToArgb, Windows COLORREF with alpha). Signed members (int) store the bit pattern as a signed value.
        /// </summary>
        UInt32Argb,
        /// <summary>
        /// An integer 0xAABBGGRR (the bytes R, G, B, A in memory order on little endian machines, ex: ImageData32 pixels).
        /// </summary>
        UInt32Abgr,
        /// <summary>
        /// A css color string that is always stored as "#rrggbb" (or "#rrggbbaa" when <see cref="EditColorAttribute.Alpha"/> is true):
        /// a typed or pasted css color (named, rgb(), hsl() ...) is converted, for members that are read as hex only.
        /// </summary>
        Hex,
        /// <summary>
        /// An object with integer R, G, B members in 0..255 and optionally A (0..255) or Opacity (0..100 percent), ex: a css colour class.
        /// </summary>
        Struct8,
    }


    /// <summary>
    /// Put this on a member to edit it with the color editor (a swatch with a color picker, a text box that takes any css color,
    /// an alpha slider for formats with alpha and an intensity box for HDR formats), and to say how the color is stored.
    /// Example: [EditColor] on a string ("#rrggbb"), [EditColor(EditColorFormat.HdrRgba)] on a float[4], [EditColor(EditColorFormat.UInt32Rgba)] on a uint.
    /// </summary>
    /// <remarks>
    /// This is an editor type (encoded as "Color:&lt;Format&gt;:&lt;Alpha&gt;:&lt;MaxIntensity&gt;", see <see cref="EditTypes.Color"/>), so it can't be combined with
    /// another <see cref="EditTypeAttribute"/> (ex: <see cref="EditHideIfAttribute"/>).
    /// For a list of colors (ex: String[]), put it on the element with <see cref="EditElementAttributesAttribute"/>; numeric arrays are one color.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class EditColorAttribute : EditTypeAttribute
    {
        /// <summary>
        /// Edit this member as a color
        /// </summary>
        /// <param name="format">How the color is stored (Auto: decided by the member type)</param>
        /// <param name="alpha">Css strings only: allow an alpha channel ("#rrggbbaa"); the other formats have alpha when their layout has it</param>
        /// <param name="maxIntensity">HDR formats only: the largest intensity (brightness above white), default 16</param>
        public EditColorAttribute(EditColorFormat format = EditColorFormat.Auto, bool alpha = false, float maxIntensity = 16)
            : base(String.Join(':', EditTypes.Color, format.ToString(), alpha ? "Alpha" : "", maxIntensity.ToString(CultureInfo.InvariantCulture)))
        {
            Format = format;
            Alpha = alpha;
            MaxIntensity = maxIntensity;
        }

        /// <summary>
        /// How the color is stored
        /// </summary>
        public readonly EditColorFormat Format;

        /// <summary>
        /// Css strings: allow an alpha channel
        /// </summary>
        public readonly bool Alpha;

        /// <summary>
        /// HDR formats: the largest intensity
        /// </summary>
        public readonly float MaxIntensity;
    }

}
