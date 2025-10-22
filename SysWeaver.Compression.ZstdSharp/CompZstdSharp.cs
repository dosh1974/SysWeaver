using CommunityToolkit.HighPerformance;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using ZstdSharp;

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

        #region Compress

        static readonly int[] Levels = new int[]
        {
             1,
             9,
             22,
        };

        sealed class MyCompressor : Compressor, IDisposable
        {
            public MyCompressor(ConcurrentStack<MyCompressor> s, int level) : base(level)
            {
                S = s;
            }
            readonly ConcurrentStack<MyCompressor> S;
            public new void Dispose()
            {
                S.Push(this);
            }
        }


        static readonly ConcurrentStack<MyCompressor>[] Compressors = new ConcurrentStack<MyCompressor>[]
        {
            new ConcurrentStack<MyCompressor> (),
            new ConcurrentStack<MyCompressor> (),
            new ConcurrentStack<MyCompressor> (),
        };

        static MyCompressor GetComp(CompEncoderLevels level)
        {
            var il = (int)level;
            var s = Compressors[il];
            if (s.TryPop(out var c))
                return c;
            c = new MyCompressor(s, Levels[il]);
            return c;
        }

        public void Compress(Stream from, Stream to, CompEncoderLevels level)
        {
            using var cs = new CompressionStream(to, Levels[(int)level]);
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
            using var c = GetComp(level);
            return c.Wrap(from, to);
        }

        public void Compress(ReadOnlySpan<Byte> from, Stream to, CompEncoderLevels level)
        {
            using var cs = new CompressionStream(to, Levels[(int)level]);
            cs.Write(from);
        }

        public async ValueTask CompressAsync(Stream from, Stream to, CompEncoderLevels level)
        {
            using var cs = new CompressionStream(to, Levels[(int)level]);
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
            using var cs = new CompressionStream(to, Levels[(int)level]);
            await cs.WriteAsync(from).ConfigureAwait(false);
        }

        #endregion//Compress


        #region Decompress


        sealed class MyDecompressor : Decompressor, IDisposable
        {
            public MyDecompressor(ConcurrentStack<MyDecompressor> s)
            {
                S = s;
            }
            readonly ConcurrentStack<MyDecompressor> S;
            public new void Dispose()
            {
                S.Push(this);
            }
        }

        static readonly ConcurrentStack<MyDecompressor> Decompressors = new ConcurrentStack<MyDecompressor>();


        static MyDecompressor GetDecomp()
        {
            var s = Decompressors;
            if (s.TryPop(out var c))
                return c;
            c = new MyDecompressor(s);
            return c;

        }
        public void Decompress(Stream from, Stream to)
        {
            using var cs = new DecompressionStream(from);
            cs.CopyTo(to);
        }

        public int Decompress(Stream from, Span<Byte> to)
        {
            using var cs = new DecompressionStream(from);
            var size = cs.Read(to);
            if (cs.Read(to) > 0)
                throw new ArgumentException(CompHelpers.DecDestTooSmall, nameof(to));
            return size;
        }

        public int Decompress(ReadOnlySpan<Byte> from, Span<Byte> to)
        {
            using var c = GetDecomp();
            return c.Unwrap(from, to);
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
            using var cs = new DecompressionStream(from);
            await cs.CopyToAsync(to).ConfigureAwait(false);
        }

        public async ValueTask<int> DecompressAsync(Stream from, Memory<Byte> to)
        {
            using var cs = new DecompressionStream(from);
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
