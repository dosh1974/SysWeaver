using System;
using System.Collections.Generic;

using System.IO;
using System.IO.Compression;
using System.Buffers;

using System.Collections.Frozen;

using CompStream = System.IO.Compression.BrotliStream;
using Decoder = System.IO.Compression.BrotliDecoder;
using Encoder = System.IO.Compression.BrotliEncoder;


using System.Threading.Tasks;
using CommunityToolkit.HighPerformance;

namespace SysWeaver.Compression
{
    /// <summary>
    /// A compression type that uses brotli for compression
    /// </summary>
    public sealed class CompBrotliNETNew : ICompType
    {
        const String CompName = ".NET brotli";

        const String CompHttpCode = "br";

        const int CompPrio = 0;

        static readonly IReadOnlySet<String> CompExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            "br",
        }.ToFrozenSet(StringComparer.Ordinal);

        #region Lifetime

        CompBrotliNETNew()
        {
        }

        /// <summary>
        /// Call once to register this compression type to the compression manager
        /// </summary>
        public static void Register() => CompManager.AddType(Instance);

        /// <summary>
        /// The instance of the compressor
        /// </summary>
        public static ICompType Instance = new CompBrotliNETNew();

        static readonly String CompTS = String.Concat('[', CompHttpCode, "] ", CompName, " @ prio ", CompPrio, " for extensions: ", String.Join(", ", CompExtensions));

        public override string ToString() => CompTS;

        #endregion//Lifetime


        #region Info

        public string Name => CompName;

        public string HttpCode => CompHttpCode;

        public int Prio => CompPrio;

        public IReadOnlyCollection<String> FileExtensions => CompExtensions;

        #endregion//Info

        #region Compress

        public void Compress(Stream from, Stream to, CompEncoderLevels level)
        {
            try
            {
                if (from.CanSeek)
                {
                    var len = from.Length;
                    if (len < MaxStack)
                    {
                        Span<Byte> src = stackalloc Byte[(int)len];
                        len = from.Read(src);
                        Span<Byte> buf = stackalloc Byte[(int)len + MaxOverhead];
                        var s = Compress(src[..(int)len], buf, level);
                        to.Write(buf[..s]);
                        return;
                    }

                    if (len < MaxBuffered)
                    {
                        var src = ArrayPoolStream.Rent((int)len);
                        try
                        {
                            len = from.Read(src, 0, (int)len);
                            var sm = src.AsSpan()[..(int)len];
                            var buf = ArrayPoolStream.Rent((int)len + MaxOverhead);
                            try
                            {

                                var bs = buf.AsSpan();
                                var s = Compress(sm, bs, level);
                                to.Write(bs[..s]);
                                return;
                            }
                            finally
                            {
                                ArrayPoolStream.Return(buf);
                            }
                        }
                        finally
                        {
                            ArrayPoolStream.Return(src);
                        }
                    }
                }
            }
            catch
            {
            }
            using var cs = new CompStream(to, CompHelpers.StreamLevels[(int)level], true);
            from.CopyTo(cs);
        }

        public int Compress(Stream from, Span<Byte> to, CompEncoderLevels level)
        {
            try
            {
                if (from.CanSeek)
                {
                    var len = from.Length;
                    if (len < MaxStack)
                    {
                        Span<Byte> src = stackalloc Byte[(int)len];
                        len = from.Read(src);
                        var s = Compress(src[..(int)len], to, level);
                        return s;
                    }

                    if (len < MaxBuffered)
                    {
                        var src = ArrayPoolStream.Rent((int)len);
                        try
                        {
                            len = from.Read(src, 0, (int)len);
                            var sm = src.AsSpan()[..(int)len];
                            var s = Compress(sm, to, level);
                            return s;
                        }
                        finally
                        {
                            ArrayPoolStream.Return(src);
                        }
                    }
                }
            }
            catch
            {
            }
            var l = to.Length;
            unsafe
            {
                fixed (byte* bp = to)
                {
                    using var ms = new UnmanagedMemoryStream(bp, l, l, FileAccess.Write);
                    Compress(from, ms, level);
                    return (int)ms.Position;
                }
            }
        }

        static readonly int[] Quality =
        [
            1, 4, 11
        ];

        const int EncoderWindow = 24;
        const int MaxOverhead = 128;
        const int MaxStack = (1 << 10) - MaxOverhead;
        const int MaxBuffered = (1 << 16) - MaxOverhead;

        public int Compress(ReadOnlySpan<Byte> from, Span<Byte> to, CompEncoderLevels level)
        {
#if DEBUG
            if (!Encoder.TryCompress(from, to, out var written, Quality[(int)level], EncoderWindow))
                throw new Exception("Failed to compress!");
#else//DEBUG
            Encoder.TryCompress(from, to, out var written, Quality[(int)level], EncoderWindow);
#endif//DEBUG
            return written;
        }

        public void Compress(ReadOnlySpan<Byte> from, Stream to, CompEncoderLevels level)
        {
            var len = from.Length;
            if (len < MaxStack)
            {
                Span<Byte> buf = stackalloc Byte[len + MaxOverhead];
                var s = Compress(from, buf, level);
                to.Write(buf[..s]);
                return;
            }

            if (len < MaxBuffered)
            {
                var buf = ArrayPoolStream.Rent(len + MaxOverhead);
                try
                {
                    var bs = buf.AsSpan();
                    var s = Compress(from, bs, level);
                    to.Write(bs[..s]);
                    return;
                }
                finally
                {
                    ArrayPoolStream.Return(buf);
                }
            }
            using var cs = new CompStream(to, CompHelpers.StreamLevels[(int)level], true);
            cs.Write(from);
        }

        public async ValueTask CompressAsync(Stream from, Stream to, CompEncoderLevels level)
        {
            try
            {
                if (from.CanSeek)
                {
                    var len = from.Length;
                    if (len < MaxBuffered)
                    {
                        var src = ArrayPoolStream.Rent((int)len);
                        try
                        {
                            len = await from.ReadAsync(src, 0, (int)len).ConfigureAwait(false);
                            var sm = src.AsSpan()[..(int)len];
                            var buf = ArrayPoolStream.Rent((int)len + MaxOverhead);
                            try
                            {

                                var bs = buf.AsSpan();
                                var s = Compress(sm, bs, level);
                                await to.WriteAsync(buf, 0, s).ConfigureAwait(false);
                                return;
                            }
                            finally
                            {
                                ArrayPoolStream.Return(buf);
                            }
                        }
                        finally
                        {
                            ArrayPoolStream.Return(src);
                        }
                    }
                }
            }
            catch
            {
            }

            using var cs = new CompStream(to, CompHelpers.StreamLevels[(int)level], true);
            await from.CopyToAsync(cs).ConfigureAwait(false);
        }

        public async ValueTask<int> CompressAsync(Stream from, Memory<Byte> to, CompEncoderLevels level)
        {
            try
            {
                if (from.CanSeek)
                {
                    var len = from.Length;
                    if (len < MaxBuffered)
                    {
                        var src = ArrayPoolStream.Rent((int)len);
                        try
                        {
                            len = await from.ReadAsync(src, 0, (int)len).ConfigureAwait(false);
                            var sm = src.AsSpan()[..(int)len];
                            var s = Compress(sm, to.Span, level);
                            return s;
                        }
                        finally
                        {
                            ArrayPoolStream.Return(src);
                        }
                    }
                }
            }
            catch
            {
            }
            using var ms = to.AsStream();
            await CompressAsync(from, ms, level).ConfigureAwait(false);
            return (int)ms.Position;
        }

        public async ValueTask CompressAsync(ReadOnlyMemory<Byte> from, Stream to, CompEncoderLevels level)
        {
            var len = from.Length;
            if (len < MaxBuffered)
            {
                var buf = ArrayPoolStream.Rent(len + MaxOverhead);
                try
                {
                    var bs = buf.AsSpan();
                    var s = Compress(from.Span, bs, level);
                    await to.WriteAsync(buf, 0, s).ConfigureAwait(false);
                    return;
                }
                finally
                {
                    ArrayPoolStream.Return(buf);
                }
            }
            using var ms = from.AsStream();
            await CompressAsync(ms, to, level).ConfigureAwait(false);
        }

        #endregion//Compress


        #region Decompress

        public void Decompress(Stream from, Stream to)
        {
            using var cs = new CompStream(from, CompressionMode.Decompress, true);
            cs.CopyTo(to);
        }

        public int Decompress(Stream from, Span<Byte> to)
        {
            var cs = new CompStream(from, CompressionMode.Decompress, true);
            var size = cs.Read(to);
            if (cs.Read(to) > 0)
                throw new ArgumentException(CompHelpers.DecDestTooSmall, nameof(to));
            return size;
        }

        public int Decompress(ReadOnlySpan<Byte> from, Span<Byte> to)
        {
            if (!Decoder.TryDecompress(from, to, out var written))
                throw new Exception("Failed to decompress!");
            return written;
        }

        public void Decompress(ReadOnlySpan<Byte> from, Stream to)
        {
            unsafe
            {
                fixed (byte* bp = from)
                {
                    using var ms = new UnmanagedMemoryStream(bp, from.Length);
                    Decompress(ms, to);
                }
            }
        }

        public async ValueTask DecompressAsync(Stream from, Stream to)
        {
            using var cs = new CompStream(from, CompressionMode.Decompress, true);
            await cs.CopyToAsync(to).ConfigureAwait(false);
        }

        public async ValueTask<int> DecompressAsync(Stream from, Memory<Byte> to)
        {
            using var cs = new CompStream(from, CompressionMode.Decompress, true);
            var size = await cs.ReadAsync(to).ConfigureAwait(false);
            if (await cs.ReadAsync(to).ConfigureAwait(false) > 0)
                throw new ArgumentException(CompHelpers.DecDestTooSmall, nameof(to));
            return size;
        }

        public async ValueTask DecompressAsync(ReadOnlyMemory<Byte> from, Stream to)
        {
            using var ms = from.AsStream();
            await DecompressAsync(ms, to).ConfigureAwait(false);
        }


        #endregion//Decompress

    }



}
