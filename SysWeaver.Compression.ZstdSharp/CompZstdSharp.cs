using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ZstdSharp;

using Codec = SysWeaver.Compression.CompStreamCodec<SysWeaver.Compression.CompZstdSharp.ZstdEncoder, SysWeaver.Compression.CompZstdSharp.ZstdDecoder>;

namespace SysWeaver.Compression
{
    public class CompZstdSharp : ICompType
    {
        const String CompName = "ZstdSharp";

        const String CompHttpCode = "zstd";

        const int CompPrio = 1;

        static readonly IReadOnlySet<String> CompExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            "zstd",
        }.ToFrozenSet(StringComparer.Ordinal);

        #region Lifetime

        CompZstdSharp()
        {
        }

        /// <summary>
        /// Call once to register this compression type to the compression manager
        /// </summary>
        public static void Register() => CompManager.AddType(Instance);

        /// <summary>
        /// The instance of the compressor
        /// </summary>
        public static ICompType Instance = new CompZstdSharp();

        static readonly String CompTS = String.Concat('[', CompHttpCode, "] ", CompName, " @ prio ", CompPrio, " for extensions: ", String.Join(", ", CompExtensions));

        public override string ToString() => CompTS;

        #endregion//Lifetime

        #region Info

        public string Name => CompName;

        public string HttpCode => CompHttpCode;

        public int Prio => CompPrio;

        public IReadOnlyCollection<String> FileExtensions => CompExtensions;

        #endregion//Info

        #region Encoder / decoder

        static readonly int[] Levels =
        [
             1,
             9,
             22,
        ];

        /// <summary>
        /// Pooled compressors (one pool per level), the native context is kept between uses
        /// </summary>
        static readonly CompInstancePool<Compressor>[] Compressors = [new(), new(), new()];

        /// <summary>
        /// Pooled decompressors, the native context is kept between uses
        /// </summary>
        static readonly CompInstancePool<Decompressor> Decompressors = new();

        static Compressor RentCompressor(CompEncoderLevels level) => Compressors[(int)level].TryRent() ?? new Compressor(Levels[(int)level]);

        static void Return(CompEncoderLevels level, Compressor c)
        {
            c.ResetStream();
            Compressors[(int)level].Return(c);
        }

        static Decompressor RentDecompressor() => Decompressors.TryRent() ?? new Decompressor();

        static void Return(Decompressor d)
        {
            d.ResetStream();
            Decompressors.Return(d);
        }

        /// <summary>
        /// A pooled zstd compressor
        /// </summary>
        public struct ZstdEncoder : ICompStreamEncoder<ZstdEncoder>
        {
            Compressor C;
            CompEncoderLevels Level;

            public static ZstdEncoder Create(CompEncoderLevels level) => new ZstdEncoder
            {
                C = RentCompressor(level),
                Level = level,
            };

            public static int GetMaxCompressedLength(int inputSize) => Compressor.GetCompressBound(inputSize);

            public static bool TryCompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesWritten, CompEncoderLevels level)
            {
                var bound = Compressor.GetCompressBound(source.Length);
                var c = RentCompressor(level);
                try
                {
                    if (destination.Length >= bound)
                        return c.TryWrap(source, destination, out bytesWritten);
                    //  Zstd fails if the destination is smaller than the worst case size, even if the compressed data fits.
                    //  Compress to a temp buffer and copy the result if it fits.
                    var temp = ArrayPoolStream.Rent(bound);
                    try
                    {
                        if (!c.TryWrap(source, temp.AsSpan(0, bound), out var size) || (size > destination.Length))
                        {
                            bytesWritten = 0;
                            return false;
                        }
                        temp.AsSpan(0, size).CopyTo(destination);
                        bytesWritten = size;
                        return true;
                    }
                    finally
                    {
                        ArrayPoolStream.Return(temp);
                    }
                }
                finally
                {
                    Return(level, c);
                }
            }

            public OperationStatus Compress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock)
                => C.WrapStream(source, destination, out bytesConsumed, out bytesWritten, isFinalBlock);

            public void Dispose()
            {
                var c = C;
                if (c == null)
                    return;
                C = null;
                Return(Level, c);
            }
        }

        /// <summary>
        /// A pooled zstd decompressor
        /// </summary>
        public struct ZstdDecoder : ICompStreamDecoder<ZstdDecoder>
        {
            Decompressor D;

            public static ZstdDecoder Create() => new ZstdDecoder
            {
                D = RentDecompressor(),
            };

            public static bool TryDecompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesWritten)
            {
                bytesWritten = 0;
                //  A zstd frame is never empty
                if (source.IsEmpty)
                    return false;
                var d = RentDecompressor();
                try
                {
                    return d.TryUnwrap(source, destination, out bytesWritten);
                }
                catch (ZstdException)
                {
                    return false;
                }
                finally
                {
                    Return(d);
                }
            }

            /// <summary>
            /// Concatenated zstd frames are valid zstd data
            /// </summary>
            public static int NextHeaderSize => 4;

            /// <summary>
            /// The decompressor is ready for the next frame when a frame is done
            /// </summary>
            public bool BeginNext(ReadOnlySpan<Byte> next) => CompHelpers.IsZstdFrame(next);

            public OperationStatus Decompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten)
                => D.UnwrapStream(source, destination, out bytesConsumed, out bytesWritten);

            public void Dispose()
            {
                var d = D;
                if (d == null)
                    return;
                D = null;
                Return(d);
            }
        }

        #endregion//Encoder / decoder

        #region Compress

        public void Compress(Stream from, Stream to, CompEncoderLevels level) => Codec.Compress(from, to, level);

        public int Compress(Stream from, Span<Byte> to, CompEncoderLevels level) => Codec.Compress(from, to, level);

        public int Compress(ReadOnlySpan<Byte> from, Span<Byte> to, CompEncoderLevels level) => Codec.Compress(from, to, level);

        public void Compress(ReadOnlySpan<Byte> from, Stream to, CompEncoderLevels level) => Codec.Compress(from, to, level);

        public Task CompressAsync(Stream from, Stream to, CompEncoderLevels level) => Codec.CompressAsync(from, to, level);

        public Task<int> CompressAsync(Stream from, Memory<Byte> to, CompEncoderLevels level) => Codec.CompressAsync(from, to, level);

        public Task CompressAsync(ReadOnlyMemory<Byte> from, Stream to, CompEncoderLevels level) => Codec.CompressAsync(from, to, level);

        #endregion//Compress


        #region Decompress

        public void Decompress(Stream from, Stream to) => Codec.Decompress(from, to);

        public int Decompress(Stream from, Span<Byte> to) => Codec.Decompress(from, to);

        public int Decompress(ReadOnlySpan<Byte> from, Span<Byte> to) => Codec.Decompress(from, to);

        public void Decompress(ReadOnlySpan<Byte> from, Stream to) => Codec.Decompress(from, to);

        public Task DecompressAsync(Stream from, Stream to) => Codec.DecompressAsync(from, to);

        public Task<int> DecompressAsync(Stream from, Memory<Byte> to) => Codec.DecompressAsync(from, to);

        public Task DecompressAsync(ReadOnlyMemory<Byte> from, Stream to) => Codec.DecompressAsync(from, to);

        #endregion//Decompress

    }
}
