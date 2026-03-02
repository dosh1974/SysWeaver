using System;
using System.Buffers;
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

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression encoder</param>
        /// <param name="from">The memory to read uncompressed data from</param>
        /// <param name="level">The compression level to use</param>
        /// <param name="trim">The returned memory is trimnmed, this is useful for long living object to reduce memory usage</param>
        /// <returns>The compressed data</returns>
        public static Memory<Byte> GetCompressed(this ICompEncoder c, ReadOnlySpan<Byte> from, CompEncoderLevels level, bool trim = false)
        {
            var mem = ArrayPoolStream.Pool.Rent(from.Length + MaxCompressOverHead);
            var s = c.Compress(from, mem, level);
            return GetMem(mem, s, trim);

        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression encoder</param>
        /// <param name="from">The stream to read the uncompressed data from</param>
        /// <param name="level">The compression level to use</param>
        /// <param name="trim">The returned memory is trimnmed, this is useful for long living object to reduce memory usage</param>
        /// <returns>The compressed data</returns>
        public static Memory<Byte> GetCompressed(this ICompEncoder c, Stream from, CompEncoderLevels level, bool trim = false)
        {
            Byte[] mem = null;
            try
            {
                if (from.CanSeek)
                    mem = ArrayPoolStream.Pool.Rent((int)from.Length + MaxCompressOverHead);
            }
            catch
            {
            }
            if (mem != null)
            {
                var s = c.Compress(from, mem, level);
                return GetMem(mem, s, trim);
            }
            using var ms = new ArrayPoolStream(InititalGuess);
            c.Compress(from, ms, level);
            return GetMem(ms, trim);
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression encoder</param>
        /// <param name="from">The stream to read the uncompressed data from</param>
        /// <param name="level">The compression level to use</param>
        /// <param name="trim">The returned memory is trimnmed, this is useful for long living object to reduce memory usage</param>
        /// <returns>The compressed data</returns>
        public static async ValueTask<Memory<Byte>> GetCompressedAsync(this ICompEncoder c, Stream from, CompEncoderLevels level, bool trim = false)
        {
            Byte[] mem = null;
            try
            {
                if (from.CanSeek)
                    mem = ArrayPoolStream.Pool.Rent((int)from.Length + MaxCompressOverHead);
            }
            catch
            {
            }
            if (mem != null)
            {
                var s = await c.CompressAsync(from, mem, level).ConfigureAwait(false);
                return GetMem(mem, s, trim);
            }
            using (var ms = new ArrayPoolStream(InititalGuess))
            {
                await c.CompressAsync(from, ms, level).ConfigureAwait(false);
                return GetMem(ms, trim);
            }
        }

        #endregion//Compression


        #region Decompression



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetDecompressedSizeEstimate(int len)
        {
            len <<= 3;
            return len < 65536 ? 65536 : len;
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
            long l = 0;
            try
            {
                l = from.CanSeek ? from.Length : 0;
            }
            catch
            {
            }
            using var ms = new ArrayPoolStream(GetDecompressedSizeEstimate((int)l));
            c.Decompress(from, ms);
            return ms.ToArray();
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
            using var ms = new ArrayPoolStream(GetDecompressedSizeEstimate((int)l));
            await c.DecompressAsync(from, ms).ConfigureAwait(false);
            return ms.ToArray();
        }

        #endregion//Compression


        #region Unmanaged memory decompression

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The memory to read compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static IUnmanagedReadOnlyMemory<Byte> GetUnmanagedDecompressed(this ICompDecoder c, ReadOnlySpan<Byte> from)
        {
            using (var ms = new ArrayPoolStream((from.Length << 1) + 1024))
            {
                c.Decompress(from, ms);
                return ms.GetMemory();
            }
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static IUnmanagedReadOnlyMemory<Byte> GetUnmanagedDecompressed(this ICompDecoder c, Stream from)
        {
            long l = 0;
            try
            {
                l = from.CanSeek ? from.Length : 0;
            }
            catch
            {
            }
            using (var ms = new ArrayPoolStream((int)(l << 1) + 1024))
            {
                c.Decompress(from, ms);
                return ms.GetMemory();
            }
        }

        /// <summary>
        /// Get compressed data
        /// </summary>
        /// <param name="c">The compression decoder</param>
        /// <param name="from">The stream to read the compressed data from</param>
        /// <returns>The decompressed data</returns>
        public static async ValueTask<IUnmanagedReadOnlyMemory<Byte>> GetUnmanagedDecompressedAsync(this ICompDecoder c, Stream from)
        {
            long l = 0;
            try
            {
                l = from.CanSeek ? from.Length : 0;
            }
            catch
            {
            }
            using (var ms = new ArrayPoolStream((int)(l << 1) + 1024))
            {
                await c.DecompressAsync(from, ms).ConfigureAwait(false);
                return ms.GetMemory();
            }
        }

        #endregion//Unmanaged memory decompression


        /*
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static Memory<Byte> GetMem(ArrayPoolStream ms, bool trim)
                    => ms.ToArray();
        */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Memory<Byte> GetMem(ArrayPoolStream ms, bool trim)
            => GetMem(ms.Data, (int)ms.Length, trim);

        static Memory<Byte> GetMem(Byte[] mem, int s, bool trim)
        {
            var bufSize = mem.Length;
            long waste = bufSize - s;
            if ((!trim) || (waste < 1024) || ((waste << 3) < bufSize)) // Allow approx 1/8th the buffer size of waste to avoid a memory copy
                return new Memory<Byte>(mem, 0, s);
            var ret = GC.AllocateUninitializedArray<Byte>(s);
            mem.AsSpan()[..s].CopyTo(ret.AsSpan());
            ArrayPoolStream.Pool.Return(mem);
            return ret;
        }


    }
}
