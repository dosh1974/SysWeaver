using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SysWeaver.Compression
{
    public static class CompExt
    {

        #region Compression

        const int MaxCompressOverHead = 32;
        const int InititalGuess = 1024;

        const int MaxStackAlloc = 8192;

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression encoder</param>
        /// <param name="from">The memory to read uncompressed data from</param>
        /// <param name="level">The compression level to use</param>
        /// <param name="trim">Not used, the returned memory is always trimmed (a single allocation of the exact size)</param>
        /// <returns>The compressed data</returns>
        public static Memory<Byte> GetCompressed(this ICompEncoder c, ReadOnlySpan<Byte> from, CompEncoderLevels level, bool trim = false)
        {
            var size = from.Length + MaxCompressOverHead;
            if (size <= MaxStackAlloc)
            {
                Span<Byte> mem = stackalloc Byte[size];
                int s;
                try
                {
                    s = c.Compress(from, mem, level);
                }
                catch (ArgumentException)
                {
                    //  Incompressible data can grow more than the guessed overhead
                    return CompressToStream(c, from, level);
                }
                return ToArray(mem.Slice(0, s));
            }
            var temp = ArrayPoolStream.Rent(size);
            try
            {
                int s;
                try
                {
                    s = c.Compress(from, temp, level);
                }
                catch (ArgumentException)
                {
                    //  Incompressible data can grow more than the guessed overhead
                    return CompressToStream(c, from, level);
                }
                return ToArray(temp.AsSpan(0, s));
            }
            finally
            {
                ArrayPoolStream.Return(temp);
            }
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression encoder</param>
        /// <param name="from">The stream to read the uncompressed data from</param>
        /// <param name="level">The compression level to use</param>
        /// <param name="trim">Not used, the returned memory is always trimmed (a single allocation of the exact size)</param>
        /// <returns>The compressed data</returns>
        public static Memory<Byte> GetCompressed(this ICompEncoder c, Stream from, CompEncoderLevels level, bool trim = false)
        {
            var size = GetSeekableSize(from, out var start);
            if (size > 0)
            {
                if (size <= MaxStackAlloc)
                {
                    Span<Byte> mem = stackalloc Byte[size];
                    try
                    {
                        var s = c.Compress(from, mem, level);
                        return ToArray(mem.Slice(0, s));
                    }
                    catch (ArgumentException)
                    {
                        //  Incompressible data can grow more than the guessed overhead, try again using a stream
                        from.Position = start;
                    }
                }
                else
                {
                    var temp = ArrayPoolStream.Rent(size);
                    try
                    {
                        var s = c.Compress(from, temp, level);
                        return ToArray(temp.AsSpan(0, s));
                    }
                    catch (ArgumentException)
                    {
                        //  Incompressible data can grow more than the guessed overhead, try again using a stream
                        from.Position = start;
                    }
                    finally
                    {
                        ArrayPoolStream.Return(temp);
                    }
                }
            }
            using var ms = new ArrayPoolStream(GrowGuess(size));
            c.Compress(from, ms, level);
            return ms.ToArray();
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression encoder</param>
        /// <param name="from">The stream to read the uncompressed data from</param>
        /// <param name="level">The compression level to use</param>
        /// <param name="trim">Not used, the returned memory is always trimmed (a single allocation of the exact size)</param>
        /// <returns>The compressed data</returns>
        public static async Task<Memory<Byte>> GetCompressedAsync(this ICompEncoder c, Stream from, CompEncoderLevels level, bool trim = false)
        {
            var size = GetSeekableSize(from, out var start);
            if (size > 0)
            {
                var temp = ArrayPoolStream.Rent(size);
                try
                {
                    var s = await c.CompressAsync(from, temp, level).ConfigureAwait(false);
                    return ToArray(temp.AsSpan(0, s));
                }
                catch (ArgumentException)
                {
                    //  Incompressible data can grow more than the guessed overhead, try again using a stream
                    from.Position = start;
                }
                finally
                {
                    ArrayPoolStream.Return(temp);
                }
            }
            using var ms = new ArrayPoolStream(GrowGuess(size));
            await c.CompressAsync(from, ms, level).ConfigureAwait(false);
            return ms.ToArray();
        }

        /// <summary>
        /// Compress to a (growing) stream, used when the compressed data doesn't fit in the guessed size
        /// </summary>
        static Byte[] CompressToStream(ICompEncoder c, ReadOnlySpan<Byte> from, CompEncoderLevels level)
        {
            using var ms = new ArrayPoolStream(GrowGuess(from.Length + MaxCompressOverHead));
            c.Compress(from, ms, level);
            return ms.ToArray();
        }

        /// <summary>
        /// Get the size of the destination to compress to, for a seekable stream
        /// </summary>
        /// <param name="from">The stream</param>
        /// <param name="start">The current position of the stream</param>
        /// <returns>The size of the destination, or 0 if the size is unknown</returns>
        static int GetSeekableSize(Stream from, out long start)
        {
            start = 0;
            try
            {
                if (!from.CanSeek)
                    return 0;
                start = from.Position;
                var size = from.Length - start + MaxCompressOverHead;
                return size <= Array.MaxLength ? (int)size : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// The initial size of a stream to compress to
        /// </summary>
        static int GrowGuess(int size) => size > 0 ? (int)Math.Min(size + (size >> 4) + 1024L, Array.MaxLength) : InititalGuess;

        /// <summary>
        /// Copy data to an array of the exact size
        /// </summary>
        static Byte[] ToArray(ReadOnlySpan<Byte> data)
        {
            if (data.IsEmpty)
                return Array.Empty<Byte>();
            var d = GC.AllocateUninitializedArray<Byte>(data.Length);
            data.CopyTo(d);
            return d;
        }

        #endregion//Compression


        #region Decompression

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetDecompressedSizeEstimate(long len)
        {
            len <<= 3;
            return len < 65536 ? 65536 : (int)Math.Min(len, Array.MaxLength);
        }

        static long GetRemaining(Stream from)
        {
            try
            {
                return from.CanSeek ? from.Length - from.Position : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The memory to read compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static Memory<Byte> GetDecompressed(this ICompDecoder c, ReadOnlySpan<Byte> from)
        {
            using var ms = new ArrayPoolStream(GetDecompressedSizeEstimate(from.Length));
            c.Decompress(from, ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static Memory<Byte> GetDecompressed(this ICompDecoder c, Stream from)
        {
            using var ms = new ArrayPoolStream(GetDecompressedSizeEstimate(GetRemaining(from)));
            c.Decompress(from, ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static async Task<Memory<Byte>> GetDecompressedAsync(this ICompDecoder c, Stream from)
        {
            using var ms = new ArrayPoolStream(GetDecompressedSizeEstimate(GetRemaining(from)));
            await c.DecompressAsync(from, ms).ConfigureAwait(false);
            return ms.ToArray();
        }

        /// <summary>
        /// Get decompressed data as an array
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The memory to read compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static Byte[] GetDecompressedArray(this ICompDecoder c, ReadOnlySpan<Byte> from)
        {
            using var ms = new ArrayPoolStream(GetDecompressedSizeEstimate(from.Length));
            c.Decompress(from, ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Get decompressed data as an array
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static Byte[] GetDecompressedArray(this ICompDecoder c, Stream from)
        {
            using var ms = new ArrayPoolStream(GetDecompressedSizeEstimate(GetRemaining(from)));
            c.Decompress(from, ms);
            return ms.ToArray();
        }

        #endregion//Decompression


        #region Unmanaged memory decompression

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The memory to read compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static IUnmanagedReadOnlyMemory<Byte> GetUnmanagedDecompressed(this ICompDecoder c, ReadOnlySpan<Byte> from)
        {
            using var ms = new ArrayPoolStream(GetDecompressedSizeEstimate(from.Length));
            c.Decompress(from, ms);
            return ms.GetMemory();
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static IUnmanagedReadOnlyMemory<Byte> GetUnmanagedDecompressed(this ICompDecoder c, Stream from)
        {
            using var ms = new ArrayPoolStream(GetDecompressedSizeEstimate(GetRemaining(from)));
            c.Decompress(from, ms);
            return ms.GetMemory();
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static async Task<IUnmanagedReadOnlyMemory<Byte>> GetUnmanagedDecompressedAsync(this ICompDecoder c, Stream from)
        {
            using var ms = new ArrayPoolStream(GetDecompressedSizeEstimate(GetRemaining(from)));
            await c.DecompressAsync(from, ms).ConfigureAwait(false);
            return ms.GetMemory();
        }

        #endregion//Unmanaged memory decompression

    }
}
