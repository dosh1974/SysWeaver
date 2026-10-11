using System;

namespace SysWeaver.Media.Png
{
    /// <summary>
    /// Represents a png chunk
    /// </summary>
    public sealed class PngChunk
    {
        /// <summary>
        /// Create a chunk with all values given (nothing is validated)
        /// </summary>
        /// <param name="length">The length of the chunk data in bytes</param>
        /// <param name="id">The chunk type, see <see cref="PngChunkIds"/></param>
        /// <param name="data">The chunk data</param>
        /// <param name="crc32">The crc of the chunk type and data</param>
        public PngChunk(uint length, uint id, Byte[] data, uint crc32)
        {
            Length = length;
            Id = id;
            Data = data;
            Crc32 = crc32;
        }

        /// <summary>
        /// Create a chunk, the <see cref="Crc32"/> is computed from the id and the whole data array (if length is greater than 0)
        /// </summary>
        /// <param name="length">The length of the chunk data in bytes</param>
        /// <param name="id">The chunk type, see <see cref="PngChunkIds"/></param>
        /// <param name="data">The chunk data</param>
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

        /// <summary>
        /// The chunk type as text and hex, and the length. Ex: "IHDR [0x49484452] @ 13"
        /// </summary>
        /// <returns>A description of the chunk</returns>
        public override string ToString()
        {
            return String.Join("", (Char)((Id >> 24) & 0xff), (Char)((Id >> 16) & 0xff), (Char)((Id >> 8) & 0xff), (Char)((Id >> 0) & 0xff), " [0x", Id.ToString("x8"), "] @ ", Length);
        }
        /// <summary>
        /// The length of the chunk data in bytes (as read or given, see <see cref="PngTools.WriteChunk"/>)
        /// </summary>
        public readonly uint Length;
        /// <summary>
        /// The chunk type (4 ASCII chars, big endian), see <see cref="PngChunkIds"/>
        /// </summary>
        public readonly uint Id;
        /// <summary>
        /// The chunk data
        /// </summary>
        public readonly Byte[] Data;
        /// <summary>
        /// The crc of the chunk (as read from the file, given or computed), not validated
        /// </summary>
        public readonly uint Crc32;
    }

}
