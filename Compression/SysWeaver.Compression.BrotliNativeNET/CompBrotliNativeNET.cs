using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Brotli;

using Codec = SysWeaver.Compression.CompStreamCodec<SysWeaver.Compression.CompBrotliNativeNET.NativeEncoder, SysWeaver.Compression.CompBrotliNativeNET.NativeDecoder>;

namespace SysWeaver.Compression
{

    /// <summary>
    /// A Brotli ("br") plug-in that calls the native brotli library shipped with the Brotli.NET package directly, through <see cref="CompStreamCodec{TEncoder, TDecoder}"/>.
    /// Registered with priority 1, so it's preferred over the built-in <see cref="CompBrotliNETNew"/> (priority 0) once <see cref="Register"/> is called.
    /// </summary>
    /// <remarks>
    /// Uses brotli quality 1 / 4 / 11 for <see cref="CompEncoderLevels.Fast"/> / <see cref="CompEncoderLevels.Balanced"/> / <see cref="CompEncoderLevels.Best"/> with a 2^22 byte window.
    /// A new native encoder / decoder state is created (and destroyed) per call, there is no one-shot api.
    /// Concatenated brotli streams are not supported (data after the first stream is ignored).
    /// Requires the Brolib native binaries for the current platform and architecture: the Brotli.NET package only ships
    /// win-x64, win-x86, linux-x64 (glibc), linux-x86 (glibc) and osx-x64 binaries (use <see cref="IsSupported"/>).
    /// </remarks>
    public sealed class CompBrotliNativeNET : ICompType
    {
        const String CompName = "Native Brotli.NET";

        const String CompHttpCode = "br";

        const int CompPrio = 1;

        static readonly IReadOnlySet<String> CompExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            "br",
        }.ToFrozenSet(StringComparer.Ordinal);

        #region Lifetime

        CompBrotliNativeNET()
        {
        }

        /// <summary>
        /// Call once to register this compression type to the compression manager.
        /// Does nothing if the native library can't be loaded on this platform (see <see cref="IsSupported"/>), "br" then uses the built-in codec.
        /// </summary>
        public static void Register()
        {
            if (IsSupported)
                CompManager.AddType(Instance);
        }

        /// <summary>
        /// True if the native brotli library of the Brotli.NET package could be loaded on this platform / architecture.
        /// If false, every compress / decompress call throws (a <see cref="TypeInitializationException"/>).
        /// </summary>
        public static bool IsSupported => Support.Value;

        static readonly Lazy<bool> Support = new Lazy<bool>(() =>
        {
            try
            {
                Brolib.BrotliDecoderVersion();
                return true;
            }
            catch
            {
                return false;
            }
        });

        /// <summary>
        /// The Brotli.NET loader imports dlopen / dlsym / dlerror / dlclose from "libdl.so" on Linux (not using the .NET Core loader on .NET 5+),
        /// that file is only the development symlink (package libc6-dev), runtime systems have "libdl.so.2" (or dlopen in libc since glibc 2.34).
        /// Registered when this assembly is loaded, so before the Brotli.NET loader is used from here.
        /// </summary>
#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void InitLinuxLoader()
        {
            if (!OperatingSystem.IsLinux())
                return;
            try
            {
                NativeLibrary.SetDllImportResolver(typeof(Brolib).Assembly, ResolveLibDl);
            }
            catch (InvalidOperationException)
            {
                //  Someone else already registered a resolver for the Brotli.NET assembly
            }
        }

        static IntPtr ResolveLibDl(String libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (!String.Equals(libraryName, "libdl.so", StringComparison.Ordinal))
                return IntPtr.Zero;
            foreach (var name in LibDlNames)
                if (NativeLibrary.TryLoad(name, out var handle))
                    return handle;
            return IntPtr.Zero;
        }

        /// <summary>
        /// glibc: "libdl.so.2" (still present after glibc 2.34 as a stub), then libc itself.
        /// If none loads the default probing is used (musl maps "libdl.so" to libc by itself).
        /// </summary>
        static readonly String[] LibDlNames =
        [
            "libdl.so.2",
            "libc.so.6",
        ];

        /// <summary>
        /// The instance of the compressor
        /// </summary>
        public static ICompType Instance = new CompBrotliNativeNET();

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

        #region Encoder / decoder

        static ReadOnlySpan<uint> Quality =>
        [
            1, 4, 11
        ];

        /// <summary>
        /// The window size (log2), same as the Brotli.NET BrotliStream
        /// </summary>
        const uint Window = 22;

        /// <summary>
        /// A native brotli encoder (the Brotli.NET native functions are called directly, using pooled buffers instead of the managed buffers of the Brotli.NET BrotliStream)
        /// </summary>
        public unsafe struct NativeEncoder : ICompStreamEncoder<NativeEncoder>
        {
            IntPtr State;

            /// <inheritdoc/>
            public static NativeEncoder Create(CompEncoderLevels level)
            {
                var state = Brolib.BrotliEncoderCreateInstance();
                if (state == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to create a brotli encoder");
                Brolib.BrotliEncoderSetParameter(state, BrotliEncoderParameter.Quality, Quality[(int)level]);
                Brolib.BrotliEncoderSetParameter(state, BrotliEncoderParameter.LGWin, Window);
                return new NativeEncoder
                {
                    State = state,
                };
            }

            /// <inheritdoc/>
            public static int GetMaxCompressedLength(int inputSize) => System.IO.Compression.BrotliEncoder.GetMaxCompressedLength(inputSize);

            /// <inheritdoc/>
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

            /// <inheritdoc/>
            public OperationStatus Compress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock)
            {
                fixed (Byte* src = source)
                fixed (Byte* dst = destination)
                {
                    uint availIn = (uint)source.Length;
                    IntPtr nextIn = (IntPtr)src;
                    uint availOut = (uint)destination.Length;
                    IntPtr nextOut = (IntPtr)dst;
                    var ok = Brolib.BrotliEncoderCompressStream(State, isFinalBlock ? BrotliEncoderOperation.Finish : BrotliEncoderOperation.Process, ref availIn, ref nextIn, ref availOut, ref nextOut, out _);
                    bytesConsumed = source.Length - (int)availIn;
                    bytesWritten = destination.Length - (int)availOut;
                    if (!ok)
                        return OperationStatus.InvalidData;
                    if (isFinalBlock)
                        return Brolib.BrotliEncoderIsFinished(State) ? OperationStatus.Done : OperationStatus.DestinationTooSmall;
                    return (availIn == 0) && (availOut > 0) ? OperationStatus.Done : OperationStatus.DestinationTooSmall;
                }
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                var s = State;
                if (s == IntPtr.Zero)
                    return;
                State = IntPtr.Zero;
                Brolib.BrotliEncoderDestroyInstance(s);
            }
        }

        /// <summary>
        /// A native brotli decoder (the Brotli.NET native functions are called directly, using pooled buffers instead of the managed buffers of the Brotli.NET BrotliStream)
        /// </summary>
        public unsafe struct NativeDecoder : ICompStreamDecoder<NativeDecoder>
        {
            IntPtr State;

            /// <inheritdoc/>
            public static NativeDecoder Create()
            {
                var state = Brolib.BrotliDecoderCreateInstance();
                if (state == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to create a brotli decoder");
                return new NativeDecoder
                {
                    State = state,
                };
            }

            /// <summary>
            /// There is no one-shot api, the streaming decoder is used
            /// </summary>
            public static bool TryDecompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesWritten)
            {
                bytesWritten = 0;
                return false;
            }

            /// <summary>
            /// Concatenated brotli streams are not supported
            /// </summary>
            public static int NextHeaderSize => 0;

            /// <inheritdoc/>
            public bool BeginNext(ReadOnlySpan<Byte> next) => false;

            /// <inheritdoc/>
            public OperationStatus Decompress(ReadOnlySpan<Byte> source, Span<Byte> destination, out int bytesConsumed, out int bytesWritten)
            {
                fixed (Byte* src = source)
                fixed (Byte* dst = destination)
                {
                    uint availIn = (uint)source.Length;
                    IntPtr nextIn = (IntPtr)src;
                    uint availOut = (uint)destination.Length;
                    IntPtr nextOut = (IntPtr)dst;
                    var res = Brolib.BrotliDecoderDecompressStream(State, ref availIn, ref nextIn, ref availOut, ref nextOut, out _);
                    bytesConsumed = source.Length - (int)availIn;
                    bytesWritten = destination.Length - (int)availOut;
                    return res switch
                    {
                        BrotliDecoderResult.Success => OperationStatus.Done,
                        BrotliDecoderResult.NeedsMoreInput => OperationStatus.NeedMoreData,
                        BrotliDecoderResult.NeedsMoreOutput => OperationStatus.DestinationTooSmall,
                        _ => OperationStatus.InvalidData,
                    };
                }
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                var s = State;
                if (s == IntPtr.Zero)
                    return;
                State = IntPtr.Zero;
                Brolib.BrotliDecoderDestroyInstance(s);
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
