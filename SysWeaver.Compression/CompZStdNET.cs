#if NET11_0_OR_GREATER

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

using CompStream = System.IO.Compression.ZstandardStream;

namespace SysWeaver.Compression
{

    /// <summary>
    /// A ZStd ("zstd") implementation that compresses using the .NET <see cref="ZstandardStream"/> (priority 0, not registered by default).
    /// Decompression is forwarded to <see cref="CompZStdNETNew"/>, since the stream silently accepts truncated data.
    /// </summary>
    /// <remarks>
    /// Kept as a reference / benchmark implementation, <see cref="CompZStdNETNew"/> is the one registered in the <see cref="CompManager"/>.
    /// The stream is created with a <see cref="System.IO.Compression.CompressionLevel"/> (see <see cref="CompHelpers.GetStreamLevel(CompEncoderLevels)"/>), so the ZStd quality differs from <see cref="CompZStdNETNew"/>.
    /// </remarks>
    public sealed class CompZStdNET : ICompType
    {
        const String CompName = ".NET zstd stream";

        const String CompHttpCode = "zstd";

        const int CompPrio = 0;

        static readonly IReadOnlySet<String> CompExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            "zstd",
        }.ToFrozenSet(StringComparer.Ordinal);

        #region Lifetime

        CompZStdNET()
        {
        }

        /// <summary>
        /// Call once to register this compression type to the compression manager
        /// </summary>
        public static void Register() => CompManager.AddType(Instance);

        /// <summary>
        /// The instance of the compressor
        /// </summary>
        public static ICompType Instance = new CompZStdNET();

        /// <summary>
        /// Implements <see cref="ICompType.Instance"/> (so it works through a generic type parameter), returns <see cref="Instance"/>
        /// </summary>
        static ICompType ICompType.Instance => Instance;

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

        #region Compress

        /// <inheritdoc/>
        public void Compress(Stream from, Stream to, CompEncoderLevels level)
        {
            using var cs = new CompStream(to, CompHelpers.GetStreamLevel(level), true);
            CompStreamHelpers.CopyFull(from, cs);
        }

        /// <inheritdoc/>
        public int Compress(Stream from, Span<Byte> to, CompEncoderLevels level)
        {
            unsafe
            {
                fixed (byte* bp = to)
                {
                    var ms = CompStreamHelpers.RentWriter(bp, to.Length);
                    try
                    {
                        Compress(from, ms, level);
                        return ms.Written;
                    }
                    finally
                    {
                        CompStreamHelpers.Return(ms);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public int Compress(ReadOnlySpan<Byte> from, Span<Byte> to, CompEncoderLevels level)
        {
            unsafe
            {
                fixed (byte* bp = to)
                {
                    var ms = CompStreamHelpers.RentWriter(bp, to.Length);
                    try
                    {
                        Compress(from, ms, level);
                        return ms.Written;
                    }
                    finally
                    {
                        CompStreamHelpers.Return(ms);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<Byte> from, Stream to, CompEncoderLevels level)
        {
            using var cs = new CompStream(to, CompHelpers.GetStreamLevel(level), true);
            cs.Write(from);
        }

        /// <inheritdoc/>
        public async Task CompressAsync(Stream from, Stream to, CompEncoderLevels level)
        {
            using var cs = new CompStream(to, CompHelpers.GetStreamLevel(level), true);
            await CompStreamHelpers.CopyFullAsync(from, cs).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<int> CompressAsync(Stream from, Memory<Byte> to, CompEncoderLevels level)
        {
            var ms = CompStreamHelpers.RentWriter(to);
            try
            {
                await CompressAsync(from, ms, level).ConfigureAwait(false);
                return ms.Written;
            }
            finally
            {
                CompStreamHelpers.Return(ms);
            }
        }

        /// <inheritdoc/>
        public async Task CompressAsync(ReadOnlyMemory<Byte> from, Stream to, CompEncoderLevels level)
        {
            using var cs = new CompStream(to, CompHelpers.GetStreamLevel(level), true);
            await cs.WriteAsync(from).ConfigureAwait(false);
        }

        #endregion//Compress


        #region Decompress

        //  The ZStdStream silently returns the data decompressed so far if the data is truncated (unless the process wide
        //  "System.IO.Compression.UseStrictValidation" switch is set), so decompression uses the ZStdDecoder based implementation.

        static readonly ICompDecoder Decoder = CompZStdNETNew.Instance;

        /// <inheritdoc/>
        public void Decompress(Stream from, Stream to) => Decoder.Decompress(from, to);

        /// <inheritdoc/>
        public int Decompress(Stream from, Span<Byte> to) => Decoder.Decompress(from, to);

        /// <inheritdoc/>
        public int Decompress(ReadOnlySpan<Byte> from, Span<Byte> to) => Decoder.Decompress(from, to);

        /// <inheritdoc/>
        public void Decompress(ReadOnlySpan<Byte> from, Stream to) => Decoder.Decompress(from, to);

        /// <inheritdoc/>
        public Task DecompressAsync(Stream from, Stream to) => Decoder.DecompressAsync(from, to);

        /// <inheritdoc/>
        public Task<int> DecompressAsync(Stream from, Memory<Byte> to) => Decoder.DecompressAsync(from, to);

        /// <inheritdoc/>
        public Task DecompressAsync(ReadOnlyMemory<Byte> from, Stream to) => Decoder.DecompressAsync(from, to);

        #endregion//Decompress

    }
}

#endif//NET11_0_OR_GREATER
