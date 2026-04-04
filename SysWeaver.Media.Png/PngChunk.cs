using System;

namespace SysWeaver.Media.Png
{
    /// <summary>
    /// Represents a png chunk
    /// </summary>
    public sealed class PngChunk
    {
        public PngChunk(uint length, uint id, Byte[] data, uint crc32)
        {
            Length = length;
            Id = id;
            Data = data;
            Crc32 = crc32;
        }

        public PngChunk(uint length, uint id, Byte[] data)
        {
            Length = length;
            Id = id;
            Data = data;
            Span<Byte> w = stackalloc Byte[4];
            w[0] = (Byte)(id >> 24);
            w[1] = (Byte)(id >> 16);
            w[2] = (Byte)(id >> 8);
            w[3] = (Byte)(id >> 0);
            var c = PngTools.CalcCrc32(w);
            if (length > 0)
                c = PngTools.CalcCrc32(data, c);
            Crc32 = c;
        }

        public override string ToString()
        {
            return String.Join("", (Char)((Id >> 24) & 0xff), (Char)((Id >> 16) & 0xff), (Char)((Id >> 8) & 0xff), (Char)((Id >> 0) & 0xff), " [0x", Id.ToString("x8"), "] @ ", Length);
        }
        public readonly uint Length;
        public readonly uint Id;
        public readonly Byte[] Data;
        public readonly uint Crc32;
    }

}
