using System;
using System.IO;
using System.Threading.Tasks;

namespace SysWeaver.Compression
{
    /// <summary>
    /// Decompresses data, from / to streams or memory.
    /// </summary>
    /// <remarks>
    /// Implementations are thread safe (any per-call state is created or rented per call).
    /// Streams passed in are never disposed by the decoder, reading starts at the current position of the source stream.
    /// When reading from a stream, the decoder may read (and consume) bytes past the end of the compressed data, so don't rely on the source position afterwards.
    /// Invalid or truncated compressed data throws (typically an <see cref="InvalidDataException"/>), the built-in implementations never silently return partial data.
    /// Use the extension methods in <see cref="CompExt"/> to decompress into a new array without knowing the size in advance.
    /// </remarks>
    public interface ICompDecoder : ICompInfo
    {

        #region Sync


        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <param name="to">The stream to write the uncompressed data to</param>
        void Decompress(Stream from, Stream to);


        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <param name="to">The memory to write the uncompressed data to</param>
        /// <returns>The number of uncompressed bytes written</returns>
        /// <exception cref="ArgumentException">The decompressed data doesn't fit in <paramref name="to"/>.</exception>
        int Decompress(Stream from, Span<Byte> to);

        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="from">The memory to read compressed data from</param>
        /// <param name="to">The memory to write the uncompressed data to</param>
        /// <returns>The number of uncompressed bytes written</returns>
        /// <exception cref="ArgumentException">The decompressed data doesn't fit in <paramref name="to"/>.</exception>
        int Decompress(ReadOnlySpan<Byte> from, Span<Byte> to);

        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="from">The memory to read compressed data from</param>
        /// <param name="to">The stream to write the uncompressed data to</param>
        void Decompress(ReadOnlySpan<Byte> from, Stream to);

        #endregion//Sync

        #region Async

        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <param name="to">The stream to write the uncompressed data to</param>
        Task DecompressAsync(Stream from, Stream to);

        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <param name="to">The memory to write the uncompressed data to</param>
        /// <returns>The number of uncompressed bytes written</returns>
        /// <exception cref="ArgumentException">The decompressed data doesn't fit in <paramref name="to"/>.</exception>
        Task<int> DecompressAsync(Stream from, Memory<Byte> to);

        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="from">The memory to read compressed data from</param>
        /// <param name="to">The stream to write the uncompressed data to</param>
        Task DecompressAsync(ReadOnlyMemory<Byte> from, Stream to);

        #endregion//Async


    }

}
