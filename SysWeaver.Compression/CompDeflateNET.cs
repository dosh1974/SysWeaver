using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

using CompStream = System.IO.Compression.DeflateStream;

namespace SysWeaver.Compression
{

    /// <summary>
    /// A compression type that uses deflate for compression
    /// </summary>
    public sealed class CompDeflateNET : ICompType
    {
        const String CompName = ".NET deflate stream";
        
        const String CompHttpCode = "deflate";

        const int CompPrio = 0;

        static readonly IReadOnlySet<String> CompExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            "deflate",
        }.ToFrozenSet(StringComparer.Ordinal);

        #region Lifetime

        CompDeflateNET()
        {
        }

        /// <summary>
        /// Call once to register this compression type to the compression manager
        /// </summary>
        public static void Register() => CompManager.AddType(Instance);

        /// <summary>
        /// The instance of the compressor
        /// </summary>
        public static ICompType Instance = new CompDeflateNET();

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

        public void Decompress(Stream from, Stream to)
        {
            using var cs = new CompStream(from, CompressionMode.Decompress, true);
            cs.CopyTo(to);
            CompInflaterState.ThrowIfTruncated(cs);
        }

        public int Decompress(Stream from, Span<Byte> to)
        {
            using var cs = new CompStream(from, CompressionMode.Decompress, true);
            var size = CompStreamHelpers.ReadAll(cs, to);
            CompInflaterState.ThrowIfTruncated(cs);
            return size;
        }

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

        public async Task DecompressAsync(Stream from, Stream to)
        {
            using var cs = new CompStream(from, CompressionMode.Decompress, true);
            await cs.CopyToAsync(to).ConfigureAwait(false);
            CompInflaterState.ThrowIfTruncated(cs);
        }

        public async Task<int> DecompressAsync(Stream from, Memory<Byte> to)
        {
            using var cs = new CompStream(from, CompressionMode.Decompress, true);
            var size = await CompStreamHelpers.ReadAllAsync(cs, to).ConfigureAwait(false);
            CompInflaterState.ThrowIfTruncated(cs);
            return size;
        }

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
