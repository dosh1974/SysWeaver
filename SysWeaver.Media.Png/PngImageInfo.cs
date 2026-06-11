using System;

namespace SysWeaver.Media.Png
{
    public class PngImageInfo
    {
        public int Width;
        public int Height;
        public Byte BitDepth;
        public Byte ColorType;
        public Byte CompressionMethod;
        public Byte FilterMethod;
        public Byte InterlaceMethodMethod;
        public Byte ChannelCount;
        public int Pitch;
        public int PixelBytes;

        public const int SizeIDAT = 13;

        public PngChunk ToChunk()
        {
            var dest = new Byte[SizeIDAT];
            var d = Width;
            dest[0] = (Byte)(d >> 0);
            dest[1] = (Byte)(d >> 8);
            dest[2] = (Byte)(d >> 16);
            dest[3] = (Byte)(d >> 24);
            d = Height;
            dest[4] = (Byte)(d >> 0);
            dest[5] = (Byte)(d >> 8);
            dest[6] = (Byte)(d >> 16);
            dest[7] = (Byte)(d >> 24);

            dest[8] = BitDepth;
            dest[9] = ColorType;
            dest[10] = CompressionMethod;
            dest[11] = FilterMethod;
            dest[12] = InterlaceMethodMethod;

            return new PngChunk(SizeIDAT, PngChunkIds.IHDR, dest);
        }


    }

}
