using System;

namespace SysWeaver
{
    /// <summary>
    /// An immutable container for a 32 bit per pixel (RGBA, 8 bits per channel) bitmap.
    /// </summary>
    /// <remarks>
    /// No validation is performed, the caller must ensure that <see cref="Data"/> holds (at least) <c>Width * Height * 4</c> bytes.
    /// The pixel data is not copied, so the owner of the underlying memory must not modify it while the instance is in use.
    /// </remarks>
    public sealed class ImageData32
    {
#if DEBUG
        public override string ToString() => String.Concat(Width, 'x', Height);
#endif//DEBUG

        /// <summary>
        /// Width of the image in pixels.
        /// </summary>
        public readonly int Width;

        /// <summary>
        /// Height of the image in pixels.
        /// </summary>
        public readonly int Height;

        /// <summary>
        /// The pixel data, rows stored top to bottom with no padding (stride = Width * 4).
        /// Byte order: R, G, B, A
        /// </summary>
        public readonly ReadOnlyMemory<Byte> Data;

        /// <summary>
        /// Create an image from existing pixel data (the data is not copied).
        /// </summary>
        /// <param name="width">Width of the image in pixels.</param>
        /// <param name="height">Height of the image in pixels.</param>
        /// <param name="data">The pixel data, R, G, B, A byte order, stride = <paramref name="width"/> * 4.</param>
        public ImageData32(int width, int height, ReadOnlyMemory<byte> data)
        {
            Width = width;
            Height = height;
            Data = data;
        }
    }


}
