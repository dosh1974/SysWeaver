using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

using CompStream = System.IO.Compression.BrotliStream;

namespace SysWeaver.Compression
{

    /// <summary>
    /// A compression type that uses brotli for compression
    /// </summary>
    public sealed class CompBrotliNET : ICompType
    {
        const String CompName = ".NET brotli stream";

        const String CompHttpCode = "br";

        const int CompPrio = -1;

        static readonly IReadOnlySet<String> CompExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            "br",
        }.ToFrozenSet(StringComparer.Ordinal);

        #region Lifetime

        CompBrotliNET()
        {
        }

        /// <summary>
        /// Call once to register this compression type to the compression manager
        /// </summary>
        public static void Register() => CompManager.AddType(Instance);

        /// <summary>
        /// The instance of the compressor
        /// </summary>
        public static ICompType Instance = new CompBrotliNET();

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
            using var cs = new CompStream(to, CompHelpers.GetStreamLevel(level), true);
            CompStreamHelpers.CopyFull(from, cs);
        }

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

        public void Compress(ReadOnlySpan<Byte> from, Stream to, CompEncoderLevels level)
        {
            using var cs = new CompStream(to, CompHelpers.GetStreamLevel(level), true);
            cs.Write(from);
        }

        public async Task CompressAsync(Stream from, Stream to, CompEncoderLevels level)
        {
            using var cs = new CompStream(to, CompHelpers.GetStreamLevel(level), true);
            await CompStreamHelpers.CopyFullAsync(from, cs).ConfigureAwait(false);
        }

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

        public async Task CompressAsync(ReadOnlyMemory<Byte> from, Stream to, CompEncoderLevels level)
        {
            using var cs = new CompStream(to, CompHelpers.GetStreamLevel(level), true);
            await cs.WriteAsync(from).ConfigureAwait(false);
        }

        #endregion//Compress


        #region Decompress

        //  The BrotliStream silently returns the data decompressed so far if the data is truncated (unless the process wide
        //  "System.IO.Compression.UseStrictValidation" switch is set), so decompression uses the BrotliDecoder based implementation.

        static readonly ICompDecoder Decoder = CompBrotliNETNew.Instance;

        public void Decompress(Stream from, Stream to) => Decoder.Decompress(from, to);

        public int Decompress(Stream from, Span<Byte> to) => Decoder.Decompress(from, to);

        public int Decompress(ReadOnlySpan<Byte> from, Span<Byte> to) => Decoder.Decompress(from, to);

        public void Decompress(ReadOnlySpan<Byte> from, Stream to) => Decoder.Decompress(from, to);

        public Task DecompressAsync(Stream from, Stream to) => Decoder.DecompressAsync(from, to);

        public Task<int> DecompressAsync(Stream from, Memory<Byte> to) => Decoder.DecompressAsync(from, to);

        public Task DecompressAsync(ReadOnlyMemory<Byte> from, Stream to) => Decoder.DecompressAsync(from, to);

        #endregion//Decompress

    }
}
