#if NET11_0_OR_GREATER

using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Codec = SysWeaver.Compression.CompStreamCodec<SysWeaver.Compression.CompZStdNETNew.ZstdEncoder, SysWeaver.Compression.CompZStdNETNew.ZstdDecoder>;

namespace SysWeaver.Compression
{
    /// <summary>
    /// A compression type that uses Zstandard (the .NET 11+ System.IO.Compression.ZstandardEncoder / ZstandardDecoder) for compression
    /// </summary>
    public sealed class CompZStdNETNew : ICompType
    {
        const String CompName = ".NET zstd";

        const String CompHttpCode = "zstd";

        const int CompPrio = 0;

        static readonly IReadOnlySet<String> CompExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            "zstd",
        }.ToFrozenSet(StringComparer.Ordinal);

        #region Lifetime

        CompZStdNETNew()
        {
        }

        /// <summary>
        /// Call once to register this compression type to the compression manager
        /// </summary>
        public static void Register() => CompManager.AddType(Instance);

        /// <summary>
        /// The instance of the compressor
        /// </summary>
        public static readonly ICompType Instance = new CompZStdNETNew();

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

        /// <summary>
        /// Zstandard quality (1-22) for each level
        /// </summary>
        static ReadOnlySpan<int> Quality =>
        [
            1, 9, 22
        ];

        /// <summary>
        /// The encoders and decoders are classes holding native state that is expensive to create, so they are pooled and reused
        /// </summary>
        static readonly CompInstancePool<System.IO.Compression.ZstandardEncoder>[] EncoderPools = [new(), new(), new()];

        static readonly CompInstancePool<System.IO.Compression.ZstandardDecoder> DecoderPool = new();

        static System.IO.Compression.ZstandardEncoder RentEncoder(CompEncoderLevels level)
            => EncoderPools[(int)level].TryRent() ?? new System.IO.Compression.ZstandardEncoder(Quality[(int)level]);

        static void ReturnEncoder(CompEncoderLevels level, System.IO.Compression.ZstandardEncoder enc)
        {
            try
            {
                enc.Reset();
            }
            catch
            {
                enc.Dispose();
                return;
            }
            EncoderPools[(int)level].Return(enc);
        }

        static System.IO.Compression.ZstandardDecoder RentDecoder() => DecoderPool.TryRent() ?? new System.IO.Compression.ZstandardDecoder();

        static void ReturnDecoder(System.IO.Compression.ZstandardDecoder dec)
        {
            try
            {
                dec.Reset();
            }
            catch
            {
                dec.Dispose();
                return;
            }
            DecoderPool.Return(dec);
        }

        /// <summary>
        /// A pooled .NET zstd encoder
        /// </summary>
        public struct ZstdEncoder : ICompStreamEncoder<ZstdEncoder>
        {
            System.IO.Compression.ZstandardEncoder E;
            CompEncoderLevels Level;

            public static ZstdEncoder Create(CompEncoderLevels level) => new ZstdEncoder
            {
                E = RentEncoder(level),
                Level = level,
            };

            public static int GetMaxCompressedLength(int inputSize) => (int)Math.Min(System.IO.Compression.ZstandardEncoder.GetMaxCompressedLength(inputSize), Array.MaxLength);

            /// <summary>
            /// Not using ZstandardEncoder.TryCompress since it creates (allocates) a new encoder on every call
            /// </summary>
            public static bool TryCompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesWritten, CompEncoderLevels level)
            {
                var enc = Create(level);
                try
                {
                    //  Lets zstd select the parameters for the size, a lot faster for small data at high levels
                    enc.E.SetSourceLength(source.Length);
                    return Codec.TryCompress(ref enc, source, destination, out bytesWritten);
                }
                finally
                {
                    enc.Dispose();
                }
            }

            public OperationStatus Compress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock)
                => E.Compress(source, destination, out bytesConsumed, out bytesWritten, isFinalBlock);

            public void Dispose()
            {
                var e = E;
                if (e == null)
                    return;
                E = null;
                ReturnEncoder(Level, e);
            }
        }

        /// <summary>
        /// A pooled .NET zstd decoder
        /// </summary>
        public struct ZstdDecoder : ICompStreamDecoder<ZstdDecoder>
        {
            System.IO.Compression.ZstandardDecoder D;

            public static ZstdDecoder Create() => new ZstdDecoder
            {
                D = RentDecoder(),
            };

            /// <summary>
            /// Not using ZstandardDecoder.TryDecompress since it creates (allocates) a new decoder on every call, the (pooled) streaming decoder is used instead
            /// </summary>
            public static bool TryDecompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesWritten)
            {
                bytesWritten = 0;
                return false;
            }

            /// <summary>
            /// Concatenated zstd frames are valid zstd data
            /// </summary>
            public static int NextHeaderSize => 4;

            public bool BeginNext(ReadOnlySpan<Byte> next)
            {
                if (!CompHelpers.IsZstdFrame(next))
                    return false;
                D.Reset();
                return true;
            }

            public OperationStatus Decompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten)
                => D.Decompress(source, destination, out bytesConsumed, out bytesWritten);

            public void Dispose()
            {
                var d = D;
                if (d == null)
                    return;
                D = null;
                ReturnDecoder(d);
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

#endif//NET11_0_OR_GREATER
