using System;
using System.IO;
using System.Threading.Tasks;

namespace SysWeaver.Compression
{

    /// <summary>
    /// Compresses data, from / to streams or memory.
    /// </summary>
    /// <remarks>
    /// Implementations are thread safe (any per-call state is created or rented per call).
    /// Streams passed in are never disposed or flushed by the encoder, reading starts at the current position of the source stream and continues to its end.
    /// The output is a complete, self contained compressed stream in the format given by <see cref="ICompInfo.HttpCode"/>.
    /// Use the extension methods in <see cref="CompExt"/> to compress into a new array without knowing the size in advance.
    /// </remarks>
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
        /// <exception cref="ArgumentException">The compressed data doesn't fit in <paramref name="to"/> (the content of <paramref name="to"/> is undefined).</exception>
        int Compress(Stream from, Span<Byte> to, CompEncoderLevels level);

        /// <summary>
        /// Compress data
        /// </summary>
        /// <param name="from">The memory to read uncompressed data from</param>
        /// <param name="to">The memory to write the compressed data to</param>
        /// <param name="level">The compression level to use</param>
        /// <returns>The number of compressed bytes written</returns>
        /// <exception cref="ArgumentException">The compressed data doesn't fit in <paramref name="to"/> (the content of <paramref name="to"/> is undefined).</exception>
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
        Task CompressAsync(Stream from, Stream to, CompEncoderLevels level);


        /// <summary>
        /// Compress data
        /// </summary>
        /// <param name="from">The stream to read the uncompressed data from</param>
        /// <param name="to">The memory to write the compressed data to</param>
        /// <param name="level">The compression level to use</param>
        /// <returns>The number of compressed bytes written</returns>
        /// <exception cref="ArgumentException">The compressed data doesn't fit in <paramref name="to"/> (the content of <paramref name="to"/> is undefined).</exception>
        Task<int> CompressAsync(Stream from, Memory<Byte> to, CompEncoderLevels level);


        /// <summary>
        /// Compress data
        /// </summary>
        /// <param name="from">The memory to read uncompressed data from</param>
        /// <param name="to">The stream to write the compressed data to</param>
        /// <param name="level">The compression level to use</param>
        Task CompressAsync(ReadOnlyMemory<Byte> from, Stream to, CompEncoderLevels level);

        #endregion//Async


    }

}
