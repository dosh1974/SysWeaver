using System;
using System.IO;
using System.Threading.Tasks;

namespace SysWeaver.Compression
{
    public static class CompExt
    {

        #region Compression

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression encoder</param>
        /// <param name="from">The memory to read uncompressed data from</param>
        /// <param name="level">The compression level to use</param>
        /// <returns>The compressed data</returns>
        public static Memory<Byte> GetCompressed(this ICompEncoder c, ReadOnlySpan<Byte> from, CompEncoderLevels level)
        {
            using (var ms = new MemoryStream(from.Length + 1024))
            {
                c.Compress(from, ms, level);
                return new Memory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression encoder</param>
        /// <param name="from">The stream to read the uncompressed data from</param>
        /// <param name="level">The compression level to use</param>
        /// <returns>The compressed data</returns>
        public static Memory<Byte> GetCompressed(this ICompEncoder c, Stream from, CompEncoderLevels level)
        {
            long l = 0;
            try
            {
                l = from.CanSeek ? from.Length : 0;
            }
            catch
            {
            }
            using (var ms = new MemoryStream((int)l + 1024))
            {
                c.Compress(from, ms, level);
                return new Memory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression encoder</param>
        /// <param name="from">The stream to read the uncompressed data from</param>
        /// <param name="level">The compression level to use</param>
        /// <returns>The compressed data</returns>
        public static async Task<Memory<Byte>> GetCompressedAsync(this ICompEncoder c, Stream from, CompEncoderLevels level)
        {
            long l = 0;
            try
            {
                l = from.CanSeek ? from.Length : 0;
            }
            catch
            {
            }
            using (var ms = new MemoryStream((int)l + 1024))
            {
                await c.CompressAsync(from, ms, level).ConfigureAwait(false);
                return new Memory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        #endregion//Compression




        #region Decompression

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The memory to read compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static Memory<Byte> GetDecompressed(this ICompDecoder c, ReadOnlySpan<Byte> from)
        {
            using (var ms = new MemoryStream((from.Length << 1) + 1024))
            {
                c.Decompress(from, ms);
                return new Memory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static Memory<Byte> GetDecompressed(this ICompDecoder c, Stream from)
        {
            long l = 0;
            try
            {
                l = from.CanSeek ? from.Length : 0;
            }
            catch
            {
            }
            using (var ms = new MemoryStream((int)(l << 1) + 1024))
            {
                c.Decompress(from, ms);
                return new Memory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static async ValueTask<Memory<Byte>> GetDecompressedAsync(this ICompDecoder c, Stream from)
        {
            long l = 0;
            try
            {
                l = from.CanSeek ? from.Length : 0;
            }
            catch
            {
            }
            using (var ms = new MemoryStream((int)(l << 1) + 1024))
            {
                await c.DecompressAsync(from, ms).ConfigureAwait(false);
                return new Memory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        #endregion//Compression



    }
}
