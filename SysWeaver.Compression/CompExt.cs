using System;
using System.Buffers;
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
            var pool = ArrayPool<Byte>.Shared;
            var bufSize = from.Length + 1024;
            var mem = pool.Rent(bufSize);
            try
            {
                bufSize = mem.Length;
                var sm = mem.AsSpan();
                var s = c.Compress(from, sm, level);
                long waste = bufSize - s;
                if ((waste < 2048) || ((waste << 3) < bufSize)) // Allow approx 1/8th the buffer size of waste
                {
                    pool = null;
                    return new Memory<Byte>(mem, 0, s);
                }
                var ret = new Byte[s];
                sm[..s].CopyTo(ret.AsSpan());
                return ret;
            }
            finally
            {
                pool?.Return(mem);
            }

            //var temp = new Byte[from.Length + 1024];
            //var s = c.Compress(from, temp.AsSpan(), level);
            //return temp.AsMemory()[..s];


/*            using (var ms = new MemoryStream(from.Length + 1024))
            {
                c.Compress(from, ms, level);
                return new Memory<byte>(ms.GetBuffer(), 0, (int)ms.Length);
            }*/
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
            long l = 65536;
            try
            {
                if (from.CanSeek)
                {
                    var ll = from.Length;
                    if (ll > 0)
                    {
                        l = ll;
                        var pool = ArrayPool<Byte>.Shared;
                        var bufSize = l + 1024;
                        var mem = pool.Rent((int)bufSize);
                        try
                        {
                            bufSize = mem.Length;
                            var sm = mem.AsSpan();
                            var s = c.Compress(from, sm, level);
                            long waste = bufSize - s;
                            if ((waste < 2048) || ((waste << 3) < bufSize)) // Allow approx 1/8th the buffer size of waste
                            {
                                pool = null;
                                return new Memory<Byte>(mem, 0, s);
                            }
                            var ret = new Byte[s];
                            sm[..s].CopyTo(ret.AsSpan());
                            return ret;
                        }
                        finally
                        {
                            pool?.Return(mem);
                        }
                    }
                }
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
        public static async ValueTask<Memory<Byte>> GetCompressedAsync(this ICompEncoder c, Stream from, CompEncoderLevels level)
        {
            long l = 65536;
            try
            {
                if (from.CanSeek)
                {
                    var ll = from.Length;
                    if (ll > 0)
                    {
                        l = ll;
                        var pool = ArrayPool<Byte>.Shared;
                        var bufSize = l + 1024;
                        var mem = pool.Rent((int)bufSize);
                        try
                        {
                            bufSize = mem.Length;
                            var sm = mem.AsMemory();
                            var s = await c.CompressAsync(from, sm, level).ConfigureAwait(false);
                            long waste = bufSize - s;
                            if ((waste < 2048) || ((waste << 3) < bufSize)) // Allow approx 1/8th the buffer size of waste
                            {
                                pool = null;
                                return sm[..s];
                            }
                            var ret = new Byte[s];
                            sm[..s].CopyTo(ret);
                            return ret;
                        }
                        finally
                        {
                            pool?.Return(mem);
                        }
                    }
                }
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
