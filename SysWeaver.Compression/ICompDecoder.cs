using System;
using System.IO;
using System.Threading.Tasks;

namespace SysWeaver.Compression
{
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
        int Decompress(Stream from, Span<Byte> to);

        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="from">The memory to read compressed data from</param>
        /// <param name="to">The memory to write the uncompressed data to</param>
        /// <returns>The number of uncompressed bytes written</returns>
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
        ValueTask DecompressAsync(Stream from, Stream to);

        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <param name="to">The memory to write the uncompressed data to</param>
        /// <returns>The number of uncompressed bytes written</returns>
        ValueTask<int> DecompressAsync(Stream from, Memory<Byte> to);

        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="from">The memory to read ompressed data from</param>
        /// <param name="to">The stream to write the uncompressed data to</param>
        ValueTask DecompressAsync(ReadOnlyMemory<Byte> from, Stream to);

        #endregion//Async


    }

}
