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
    /// <summary>
    /// A Zstandard ("zstd", extension "zstd") plug-in using the fully managed ZstdSharp.Port library, through <see cref="CompStreamCodec{TEncoder, TDecoder}"/>.
    /// Registered with priority 1 once <see cref="Register"/> is called.
    /// </summary>
    /// <remarks>
    /// Uses zstd level 1 / 9 / 22 for <see cref="CompEncoderLevels.Fast"/> / <see cref="CompEncoderLevels.Balanced"/> / <see cref="CompEncoderLevels.Best"/>.
    /// The <see cref="Compressor"/> / <see cref="Decompressor"/> contexts are pooled (per level for compressors) and reset between uses.
    /// Concatenated (and skippable) zstd frames are decompressed.
    /// </remarks>
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

        /// <inheritdoc/>
        public override string ToString() => CompTS;

        #endregion//Lifetime

        #region Info

        /// <inheritdoc/>
        public string Name => CompName;

        /// <inheritdoc/>
        public string HttpCode => CompHttpCode;

        /// <inheritdoc/>
        public int Prio => CompPrio;

        /// <inheritdoc/>
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

            /// <inheritdoc/>
            public static ZstdEncoder Create(CompEncoderLevels level) => new ZstdEncoder
            {
                C = RentCompressor(level),
                Level = level,
            };

            /// <inheritdoc/>
            public static int GetMaxCompressedLength(int inputSize) => Compressor.GetCompressBound(inputSize);

            /// <inheritdoc/>
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

            /// <inheritdoc/>
            public OperationStatus Compress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock)
                => C.WrapStream(source, destination, out bytesConsumed, out bytesWritten, isFinalBlock);

            /// <inheritdoc/>
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

            /// <inheritdoc/>
            public static ZstdDecoder Create() => new ZstdDecoder
            {
                D = RentDecompressor(),
            };

            /// <inheritdoc/>
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
            /// The decompressor is ready for the next frame when a frame is done, so this only checks that the data starts with a zstd (or skippable) frame
            /// </summary>
            public bool BeginNext(ReadOnlySpan<Byte> next) => CompHelpers.IsZstdFrame(next);

            /// <inheritdoc/>
            public OperationStatus Decompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten)
            {
                try
                {
                    return D.UnwrapStream(source, destination, out bytesConsumed, out bytesWritten);
                }
                catch (ZstdException)
                {
                    bytesConsumed = 0;
                    bytesWritten = 0;
                    return OperationStatus.InvalidData;
                }
            }

            /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void Compress(Stream from, Stream to, CompEncoderLevels level) => Codec.Compress(from, to, level);

        /// <inheritdoc/>
        public int Compress(Stream from, Span<Byte> to, CompEncoderLevels level) => Codec.Compress(from, to, level);

        /// <inheritdoc/>
        public int Compress(ReadOnlySpan<Byte> from, Span<Byte> to, CompEncoderLevels level) => Codec.Compress(from, to, level);

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<Byte> from, Stream to, CompEncoderLevels level) => Codec.Compress(from, to, level);

        /// <inheritdoc/>
        public Task CompressAsync(Stream from, Stream to, CompEncoderLevels level) => Codec.CompressAsync(from, to, level);

        /// <inheritdoc/>
        public Task<int> CompressAsync(Stream from, Memory<Byte> to, CompEncoderLevels level) => Codec.CompressAsync(from, to, level);

        /// <inheritdoc/>
        public Task CompressAsync(ReadOnlyMemory<Byte> from, Stream to, CompEncoderLevels level) => Codec.CompressAsync(from, to, level);

        #endregion//Compress


        #region Decompress

        /// <inheritdoc/>
        public void Decompress(Stream from, Stream to) => Codec.Decompress(from, to);

        /// <inheritdoc/>
        public int Decompress(Stream from, Span<Byte> to) => Codec.Decompress(from, to);

        /// <inheritdoc/>
        public int Decompress(ReadOnlySpan<Byte> from, Span<Byte> to) => Codec.Decompress(from, to);

        /// <inheritdoc/>
        public void Decompress(ReadOnlySpan<Byte> from, Stream to) => Codec.Decompress(from, to);

        /// <inheritdoc/>
        public Task DecompressAsync(Stream from, Stream to) => Codec.DecompressAsync(from, to);

        /// <inheritdoc/>
        public Task<int> DecompressAsync(Stream from, Memory<Byte> to) => Codec.DecompressAsync(from, to);

        /// <inheritdoc/>
        public Task DecompressAsync(ReadOnlyMemory<Byte> from, Stream to) => Codec.DecompressAsync(from, to);

        #endregion//Decompress

    }
}
