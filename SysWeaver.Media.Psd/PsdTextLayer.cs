using System;

namespace SysWeaver.Media.Psd
{
    public sealed class PsdTextLayer
    {
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
        public readonly String Name;
        public readonly String Glyphs;
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;
        public readonly float Baseline;
    }
}
