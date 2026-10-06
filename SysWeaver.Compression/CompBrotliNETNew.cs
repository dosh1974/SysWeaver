using System;
using System.Buffers;
using System.Collections.Generic;

using System.IO;

using System.Collections.Frozen;

using System.Threading.Tasks;

using Codec = SysWeaver.Compression.CompStreamCodec<SysWeaver.Compression.CompBrotliNETNew.BrotliEncoder, SysWeaver.Compression.CompBrotliNETNew.BrotliDecoder>;

namespace SysWeaver.Compression
{
    /// <summary>
    /// The default Brotli ("br") implementation (priority 0, registered by default), using the .NET <see cref="System.IO.Compression.BrotliEncoder"/> / <see cref="System.IO.Compression.BrotliDecoder"/>
    /// through <see cref="CompStreamCodec{TEncoder, TDecoder}"/>.
    /// </summary>
    /// <remarks>
    /// Uses brotli quality 1 / 4 / 11 for <see cref="CompEncoderLevels.Fast"/> / <see cref="CompEncoderLevels.Balanced"/> / <see cref="CompEncoderLevels.Best"/> with a 2^24 byte window.
    /// Truncated or invalid data throws an <see cref="InvalidDataException"/>, concatenated brotli streams are not supported (data after the first stream is ignored).
    /// </remarks>
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
        public static readonly ICompType Instance = new CompBrotliNETNew();

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

        static ReadOnlySpan<int> Quality =>
        [
            1, 4, 11
        ];

        const int EncoderWindow = 24;

        /// <summary>
        /// The .NET brotli encoder (a struct, the native state can't be reset so it's not pooled)
        /// </summary>
        public struct BrotliEncoder : ICompStreamEncoder<BrotliEncoder>
        {
            System.IO.Compression.BrotliEncoder E;

            /// <inheritdoc/>
            public static BrotliEncoder Create(CompEncoderLevels level) => new BrotliEncoder
            {
                E = new System.IO.Compression.BrotliEncoder(Quality[(int)level], EncoderWindow),
            };

            /// <inheritdoc/>
            public static int GetMaxCompressedLength(int inputSize) => System.IO.Compression.BrotliEncoder.GetMaxCompressedLength(inputSize);

            /// <summary>
            /// Uses the one-shot native compression (no encoder state is created)
            /// </summary>
            public static bool TryCompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesWritten, CompEncoderLevels level)
                => System.IO.Compression.BrotliEncoder.TryCompress(source, destination, out bytesWritten, Quality[(int)level], EncoderWindow);

            /// <inheritdoc/>
            public OperationStatus Compress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock)
                => E.Compress(source, destination, out bytesConsumed, out bytesWritten, isFinalBlock);

            /// <inheritdoc/>
            public void Dispose() => E.Dispose();
        }

        /// <summary>
        /// The .NET brotli decoder (a struct, the native state can't be reset so it's not pooled)
        /// </summary>
        public struct BrotliDecoder : ICompStreamDecoder<BrotliDecoder>
        {
            System.IO.Compression.BrotliDecoder D;

            /// <inheritdoc/>
            public static BrotliDecoder Create() => new BrotliDecoder
            {
                D = new System.IO.Compression.BrotliDecoder(),
            };

            /// <summary>
            /// Uses the one-shot native decompression (no decoder state is created)
            /// </summary>
            public static bool TryDecompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesWritten)
                => System.IO.Compression.BrotliDecoder.TryDecompress(source, destination, out bytesWritten);

            /// <summary>
            /// Concatenated brotli streams are not supported
            /// </summary>
            public static int NextHeaderSize => 0;

            /// <inheritdoc/>
            public bool BeginNext(ReadOnlySpan<Byte> next) => false;

            /// <inheritdoc/>
            public OperationStatus Decompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten)
                => D.Decompress(source, destination, out bytesConsumed, out bytesWritten);

            /// <inheritdoc/>
            public void Dispose() => D.Dispose();
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
