using System;

namespace SysWeaver.Media.Png
{
    /// <summary>
    /// The information in a png IHDR chunk (see <see cref="PngTools.DecodeIHDR"/>), and some values computed from it
    /// </summary>
    public class PngImageInfo
    {
        /// <summary>
        /// Width of the image in pixels
        /// </summary>
        public int Width;
        /// <summary>
        /// Height of the image in pixels
        /// </summary>
        public int Height;
        /// <summary>
        /// Bits per sample (channel) or palette index: 1, 2, 4, 8 or 16
        /// </summary>
        public Byte BitDepth;
        /// <summary>
        /// The png color type, see <see cref="PngTools.Grayscale"/>, <see cref="PngTools.Rgb"/>, <see cref="PngTools.Indexed"/>, <see cref="PngTools.GrayscaleAlpha"/> and <see cref="PngTools.Rgba"/>
        /// </summary>
        public Byte ColorType;
        /// <summary>
        /// The compression method (0 is the only one defined)
        /// </summary>
        public Byte CompressionMethod;
        /// <summary>
        /// The filter method (0 is the only one defined)
        /// </summary>
        public Byte FilterMethod;
        /// <summary>
        /// The interlace method: 0 for none, 1 for Adam7
        /// </summary>
        public Byte InterlaceMethodMethod;
        /// <summary>
        /// Computed: the number of channels (samples per pixel) of the color type, not written by <see cref="ToChunk"/>
        /// </summary>
        public Byte ChannelCount;
        /// <summary>
        /// Computed: the number of bytes in a scanline (excluding the filter byte), not written by <see cref="ToChunk"/>
        /// </summary>
        public int Pitch;
        /// <summary>
        /// Computed: the number of bytes per pixel, rounded up (at least 1), not written by <see cref="ToChunk"/>
        /// </summary>
        public int PixelBytes;

        /// <summary>
        /// The size of the IHDR chunk data in bytes (the name is a misnomer)
        /// </summary>
        public const int SizeIDAT = 13;

        /// <summary>
        /// Create an IHDR chunk from the values (Width and Height are written big endian, as required by png)
        /// </summary>
        /// <returns>The IHDR chunk</returns>
        public PngChunk ToChunk()
        {
            var dest = new Byte[SizeIDAT];
            var d = Width;
            dest[0] = (Byte)(d >> 24);
            dest[1] = (Byte)(d >> 16);
            dest[2] = (Byte)(d >> 8);
            dest[3] = (Byte)(d >> 0);
            d = Height;
            dest[4] = (Byte)(d >> 24);
            dest[5] = (Byte)(d >> 16);
            dest[6] = (Byte)(d >> 8);
            dest[7] = (Byte)(d >> 0);

            dest[8] = BitDepth;
            dest[9] = ColorType;
            dest[10] = CompressionMethod;
            dest[11] = FilterMethod;
            dest[12] = InterlaceMethodMethod;

            return new PngChunk(SizeIDAT, PngChunkIds.IHDR, dest);
        }


    }

}
