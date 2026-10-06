using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
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
    /// Requires the Brolib native binaries for the current platform and architecture.
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
        /// Call once to register this compression type to the compression manager
        /// </summary>
        public static void Register() => CompManager.AddType(Instance);

        /// <summary>
        /// The instance of the compressor
        /// </summary>
        public static ICompType Instance = new CompBrotliNativeNET();

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
