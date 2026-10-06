using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

using CompStream = System.IO.Compression.GZipStream;

namespace SysWeaver.Compression
{

    /// <summary>
    /// The default GZip ("gzip", extensions "gz" and "gzip") implementation (priority 0, registered by default), using the .NET <see cref="GZipStream"/>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="CompHelpers.GetStreamLevel(CompEncoderLevels)"/> to map the level.
    /// Concatenated gzip members are decompressed (as supported by <see cref="GZipStream"/>).
    /// Truncated data is detected using <see cref="CompInflaterState"/> (reflection on the .NET internals), so it throws an <see cref="InvalidDataException"/> instead of returning partial data.
    /// </remarks>
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

        /// <inheritdoc/>
        public void Decompress(Stream from, Stream to)
        {
            using var cs = new CompStream(from, CompressionMode.Decompress, true);
            cs.CopyTo(to);
            CompInflaterState.ThrowIfTruncated(cs);
        }

        /// <inheritdoc/>
        public int Decompress(Stream from, Span<Byte> to)
        {
            using var cs = new CompStream(from, CompressionMode.Decompress, true);
            var size = CompStreamHelpers.ReadAll(cs, to);
            CompInflaterState.ThrowIfTruncated(cs);
            return size;
        }

        /// <inheritdoc/>
        public int Decompress(ReadOnlySpan<Byte> from, Span<Byte> to)
        {
            unsafe
            {
                fixed (byte* bp = from)
                {
                    var ms = CompStreamHelpers.RentReader(bp, from.Length);
                    try
                    {
                        return Decompress(ms, to);
                    }
                    finally
                    {
                        CompStreamHelpers.Return(ms);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Decompress(ReadOnlySpan<Byte> from, Stream to)
        {
            unsafe
            {
                fixed (byte* bp = from)
                {
                    var ms = CompStreamHelpers.RentReader(bp, from.Length);
                    try
                    {
                        Decompress(ms, to);
                    }
                    finally
                    {
                        CompStreamHelpers.Return(ms);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public async Task DecompressAsync(Stream from, Stream to)
        {
            using var cs = new CompStream(from, CompressionMode.Decompress, true);
            await cs.CopyToAsync(to).ConfigureAwait(false);
            CompInflaterState.ThrowIfTruncated(cs);
        }

        /// <inheritdoc/>
        public async Task<int> DecompressAsync(Stream from, Memory<Byte> to)
        {
            using var cs = new CompStream(from, CompressionMode.Decompress, true);
            var size = await CompStreamHelpers.ReadAllAsync(cs, to).ConfigureAwait(false);
            CompInflaterState.ThrowIfTruncated(cs);
            return size;
        }

        /// <inheritdoc/>
        public async Task DecompressAsync(ReadOnlyMemory<Byte> from, Stream to)
        {
            var ms = CompStreamHelpers.RentReader(from);
            try
            {
                await DecompressAsync(ms, to).ConfigureAwait(false);
            }
            finally
            {
                CompStreamHelpers.Return(ms);
            }
        }


        #endregion//Decompress

    }

}


