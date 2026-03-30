using System;

namespace SysWeaver
{
    public sealed class ImageData32
    {
#if DEBUG
        public override string ToString() => String.Concat(Width, 'x', Height);
#endif//DEBUG

        public readonly int Width;

        public readonly int Height;

        /// <summary>
        /// Byte order: R, G, B, A
        /// </summary>
        public readonly ReadOnlyMemory<Byte> Data;

        public ImageData32(int width, int height, ReadOnlyMemory<byte> data)
        {
            Width = width;
            Height = height;
            Data = data;
        }
    }


}
