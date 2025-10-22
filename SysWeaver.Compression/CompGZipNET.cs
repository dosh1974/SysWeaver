using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance;

using CompStream = System.IO.Compression.GZipStream;

namespace SysWeaver.Compression
{

    /// <summary>
    /// A compression type that uses gzip for compression
    /// </summary>
    public sealed class CompGZipNET : ICompType
    {
        const String CompName = ".NET gzip stream";

        const String CompHttpCode = "gzip";

        const int CompPrio = 0;

        static readonly IReadOnlySet<String> CompExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            "gz", "gzip"
        }.ToFrozenSet(StringComparer.Ordinal);

        #region Lifetime

        CompGZipNET()
        {
        }

        /// <summary>
        /// Call once to register this compression type to the compression manager
        /// </summary>
        public static void Register() => CompManager.AddType(Instance);


        /// <summary>
        /// The instance of the compressor
        /// </summary>
        public static ICompType Instance = new CompGZipNET();

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
            using var cs = new CompStream(to, CompHelpers.StreamLevels[(int)level], true);
            from.CopyTo(cs);
        }

        public int Compress(Stream from, Span<Byte> to, CompEncoderLevels level)
        {
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

        public int Compress(ReadOnlySpan<Byte> from, Span<Byte> to, CompEncoderLevels level)
        {
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

        public void Compress(ReadOnlySpan<Byte> from, Stream to, CompEncoderLevels level)
        {
            using var cs = new CompStream(to, CompHelpers.StreamLevels[(int)level], true);
            cs.Write(from);
        }

        public async ValueTask CompressAsync(Stream from, Stream to, CompEncoderLevels level)
        {
            using var cs = new CompStream(to, CompHelpers.StreamLevels[(int)level], true);
            await from.CopyToAsync(cs).ConfigureAwait(false);
        }

        public async ValueTask<int> CompressAsync(Stream from, Memory<Byte> to, CompEncoderLevels level)
        {
            using var ms = to.AsStream();
            await CompressAsync(from, ms, level).ConfigureAwait(false);
            return (int)ms.Position;
        }

        public async ValueTask CompressAsync(ReadOnlyMemory<Byte> from, Stream to, CompEncoderLevels level)
        {
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
            unsafe
            {
                fixed (byte* bp = from)
                {
                    using var ms = new UnmanagedMemoryStream(bp, from.Length);
                    return Decompress(ms, to);
                }
            }
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


