#if NET11_0_OR_GREATER

using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Codec = SysWeaver.Compression.CompStreamCodec<SysWeaver.Compression.CompGZipNETNew.GZipEncoder, SysWeaver.Compression.CompGZipNETNew.GZipDecoder>;

namespace SysWeaver.Compression
{
    /// <summary>
    /// A compression type that uses GZip (the .NET 11+ System.IO.Compression.GZipEncoder / GZipDecoder) for compression
    /// </summary>
    public sealed class CompGZipNETNew : ICompType
    {
        const String CompName = ".NET gzip";

        const String CompHttpCode = "gzip";

        const int CompPrio = 0;

        static readonly IReadOnlySet<String> CompExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            "gz", "gzip"
        }.ToFrozenSet(StringComparer.Ordinal);

        #region Lifetime

        CompGZipNETNew()
        {
        }

        /// <summary>
        /// Call once to register this compression type to the compression manager
        /// </summary>
        public static void Register() => CompManager.AddType(Instance);

        /// <summary>
        /// The instance of the compressor
        /// </summary>
        public static readonly ICompType Instance = new CompGZipNETNew();

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
        /// zlib quality (0-9) for each level
        /// </summary>
        static ReadOnlySpan<int> Quality =>
        [
            1, 6, 9
        ];

        /// <summary>
        /// zlib window size (8-15)
        /// </summary>
        const int EncoderWindow = 15;

        /// <summary>
        /// The encoders and decoders are classes holding native state that is expensive to create, so they are pooled and reused
        /// </summary>
        static readonly CompInstancePool<System.IO.Compression.GZipEncoder>[] EncoderPools = [new(), new(), new()];

        static readonly CompInstancePool<System.IO.Compression.GZipDecoder> DecoderPool = new();

        static System.IO.Compression.GZipEncoder RentEncoder(CompEncoderLevels level)
            => EncoderPools[(int)level].TryRent() ?? new System.IO.Compression.GZipEncoder(Quality[(int)level], EncoderWindow);

        static void ReturnEncoder(CompEncoderLevels level, System.IO.Compression.GZipEncoder enc)
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

        static System.IO.Compression.GZipDecoder RentDecoder() => DecoderPool.TryRent() ?? new System.IO.Compression.GZipDecoder();

        static void ReturnDecoder(System.IO.Compression.GZipDecoder dec)
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
        /// A pooled .NET gzip encoder
        /// </summary>
        public struct GZipEncoder : ICompStreamEncoder<GZipEncoder>
        {
            System.IO.Compression.GZipEncoder E;
            CompEncoderLevels Level;

            /// <summary>
            /// True when the encoder completed a stream (the last block was compressed).
            /// Only completed encoders are reused, GZipEncoder.Reset() doesn't reset an encoder that didn't complete a stream (.NET 11 RC1), the next stream is corrupted.
            /// </summary>
            bool Completed;

            public static GZipEncoder Create(CompEncoderLevels level) => new GZipEncoder
            {
                E = RentEncoder(level),
                Level = level,
            };

            public static int GetMaxCompressedLength(int inputSize) => (int)Math.Min(System.IO.Compression.GZipEncoder.GetMaxCompressedLength(inputSize), Array.MaxLength);

            /// <summary>
            /// Not using GZipEncoder.TryCompress since it creates (allocates) a new encoder on every call
            /// </summary>
            public static bool TryCompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesWritten, CompEncoderLevels level)
            {
                var enc = Create(level);
                try
                {
                    return Codec.TryCompress(ref enc, source, destination, out bytesWritten);
                }
                finally
                {
                    enc.Dispose();
                }
            }

            public OperationStatus Compress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock)
            {
                var status = E.Compress(source, destination, out bytesConsumed, out bytesWritten, isFinalBlock);
                Completed = isFinalBlock && (status == OperationStatus.Done);
                return status;
            }

            public void Dispose()
            {
                var e = E;
                if (e == null)
                    return;
                E = null;
                if (Completed)
                    ReturnEncoder(Level, e);
                else
                    e.Dispose();
            }
        }

        /// <summary>
        /// A pooled .NET gzip decoder
        /// </summary>
        public struct GZipDecoder : ICompStreamDecoder<GZipDecoder>
        {
            System.IO.Compression.GZipDecoder D;

            public static GZipDecoder Create() => new GZipDecoder
            {
                D = RentDecoder(),
            };

            /// <summary>
            /// Not using GZipDecoder.TryDecompress since it creates (allocates) a new decoder on every call, the (pooled) streaming decoder is used instead
            /// </summary>
            public static bool TryDecompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesWritten)
            {
                bytesWritten = 0;
                return false;
            }

            /// <summary>
            /// Concatenated gzip members are valid gzip data (and supported by GZipStream), but the GZipDecoder stops after the first member
            /// </summary>
            public static int NextHeaderSize => 2;

            public bool BeginNext(ReadOnlySpan<Byte> next)
            {
                if (!CompHelpers.IsGZipMember(next))
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
