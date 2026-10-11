using System;

namespace SysWeaver.Media.Psd
{
    /// <summary>
    /// A text layer extracted by <see cref="PsdImage.LoadAndExtractTextLayers(out System.Collections.Generic.List{PsdTextLayer}, string)"/>.
    /// </summary>
    public sealed class PsdTextLayer
    {
        /// <summary>
        /// Returns "WxH @ X, Y "Glyphs"".
        /// </summary>
        public override string ToString() => String.Join("", Width, 'x', Height, " @ ", X, ", ", Y, " \"", Glyphs, '"');

        public PsdTextLayer(String name, String glyphs, int x, int y, int width, int height, float baseLine)
        {
            Name = name;
            Glyphs = glyphs;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Baseline = baseLine;
        }
        /// <summary>
        /// The layer name.
        /// </summary>
        public readonly String Name;
        /// <summary>
        /// The text of the layer (can be null if the text descriptor has no text item).
        /// </summary>
        public readonly String Glyphs;
        /// <summary>
        /// Left edge of the layer rect.
        /// </summary>
        public readonly int X;
        /// <summary>
        /// Top edge of the layer rect.
        /// </summary>
        public readonly int Y;
        /// <summary>
        /// Width of the layer rect.
        /// </summary>
        public readonly int Width;
        /// <summary>
        /// Height of the layer rect.
        /// </summary>
        public readonly int Height;
        /// <summary>
        /// The vertical translation of the text transform (document pixels).
        /// </summary>
        public readonly float Baseline;
    }
}
