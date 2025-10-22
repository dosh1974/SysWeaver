using System;
using System.IO;
using System.Threading.Tasks;

namespace SysWeaver.Compression
{

    public interface ICompEncoder : ICompInfo
    {

        #region Sync

        /// <summary>
        /// Compress data
        /// </summary>
        /// <param name="from">The stream to read the uncompressed data from</param>
        /// <param name="to">The stream to write the compressed data to</param>
        /// <param name="level">The compression level to use</param>
        void Compress(Stream from, Stream to, CompEncoderLevels level);

        /// <summary>
        /// Compress data
        /// </summary>
        /// <param name="from">The stream to read the uncompressed data from</param>
        /// <param name="to">The memory to write the compressed data to</param>
        /// <param name="level">The compression level to use</param>
        /// <returns>The number of compressed bytes written</returns>
        int Compress(Stream from, Span<Byte> to, CompEncoderLevels level);

        /// <summary>
        /// Compress data
        /// </summary>
        /// <param name="from">The memory to read uncompressed data from</param>
        /// <param name="to">The memory to write the compressed data to</param>
        /// <param name="level">The compression level to use</param>
        /// <returns>The number of compressed bytes written</returns>
        int Compress(ReadOnlySpan<Byte> from, Span<Byte> to, CompEncoderLevels level);

        /// <summary>
        /// Compress data
        /// </summary>
        /// <param name="from">The memory to read uncompressed data from</param>
        /// <param name="to">The stream to write the compressed data to</param>
        /// <param name="level">The compression level to use</param>
        void Compress(ReadOnlySpan<Byte> from, Stream to, CompEncoderLevels level);

        #endregion//Sync

        #region Async


        /// <summary>
        /// Compress data
        /// </summary>
        /// <param name="from">The stream to read the uncompressed data from</param>
        /// <param name="to">The stream to write the compressed data to</param>
        /// <param name="level">The compression level to use</param>
        ValueTask CompressAsync(Stream from, Stream to, CompEncoderLevels level);


        /// <summary>
        /// Compress data
        /// </summary>
        /// <param name="from">The stream to read the uncompressed data from</param>
        /// <param name="to">The memory to write the compressed data to</param>
        /// <param name="level">The compression level to use</param>
        /// <returns>The number of compressed bytes written</returns>
        ValueTask<int> CompressAsync(Stream from, Memory<Byte> to, CompEncoderLevels level);


        /// <summary>
        /// Compress data
        /// </summary>
        /// <param name="from">The memory to read uncompressed data from</param>
        /// <param name="to">The stream to write the compressed data to</param>
        /// <param name="level">The compression level to use</param>
        ValueTask CompressAsync(ReadOnlyMemory<Byte> from, Stream to, CompEncoderLevels level);

        #endregion//Async


    }

}
