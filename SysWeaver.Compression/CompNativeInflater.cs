using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver.Compression
{
    /// <summary>
    /// A raw deflate decoder for memory input that calls the zlib of the .NET runtime (System.IO.Compression.Native) directly,
    /// with the same results and exceptions as a <see cref="DeflateStream"/> followed by <see cref="CompInflaterState.ThrowIfTruncated(Stream)"/>.
    /// The native state is pooled and reset between uses, so a call doesn't allocate (a DeflateStream allocates ~270 bytes in 4 objects, a finalizable handle and the native zlib state per use).
    /// </summary>
    /// <remarks>
    /// Only used when the native exports, the .NET exception messages and the internals checked by <see cref="CompInflaterState"/> are as expected
    /// (and the strict validation switch of the DeflateStream is off), else <see cref="TryRent"/> returns null and the caller uses the DeflateStream.
    /// The amount of data written to the destination before an exception is thrown may differ from the DeflateStream (it writes in different chunks).
    /// </remarks>
    sealed unsafe class CompNativeInflater : IDisposable
    {
        /// <summary>
        /// The native stream (layout of PAL_ZStream in System.IO.Compression.Native).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        struct ZStream
        {
            public Byte* NextIn;
            public Byte* NextOut;
            public IntPtr Msg;
            public IntPtr State;
            public uint AvailIn;
            public uint AvailOut;
        }

        const int ZOk = 0;
        const int ZStreamEnd = 1;
        const int ZStreamError = -2;
        const int ZDataError = -3;
        const int ZMemError = -4;
        const int ZBufError = -5;

        const int ZNoFlush = 0;

        /// <summary>
        /// Raw deflate with a 32 KB window (the same as the DeflateStream decoder)
        /// </summary>
        const int WindowBits = -15;

        /// <summary>
        /// The size of the first output chunk written to a destination stream (decoded on the stack)
        /// </summary>
        const int ChunkSize = 4096;

        /// <summary>
        /// The size of the next output chunks written to a destination stream (a pooled buffer)
        /// </summary>
        const int LargeChunkSize = 65536;

        static readonly delegate* unmanaged<ZStream*, int, int> InflateInit2;
        static readonly delegate* unmanaged<ZStream*, int, int> InflateReset2;
        static readonly delegate* unmanaged<ZStream*, int, int> Inflate;
        static readonly delegate* unmanaged<ZStream*, int> InflateEnd;

        /// <summary>
        /// The message of the exceptions thrown by the .NET inflater, read from the System.IO.Compression resources
        /// </summary>
        static readonly String MsgUnsupported, MsgNoMemory, MsgInconsistent, MsgUnexpected;

        /// <summary>
        /// The ZLibException(String message, String zlibErrorContext, int zlibErrorCode, String zlibErrorMessage) constructor (not in the reference assemblies)
        /// </summary>
        static readonly ConstructorInfo ZLibExceptionCtor;

        /// <summary>
        /// True if the native decoder can be used
        /// </summary>
        static readonly bool Available;

        static CompNativeInflater()
        {
            try
            {
                if (!CompInflaterState.CanDetectDeflateTruncation)
                    return;
                var asm = typeof(DeflateStream).Assembly;
                //  The strict validation of the DeflateStream throws different exceptions, use the DeflateStream when enabled
                var strict = typeof(DeflateStream).GetField("s_useStrictValidation", BindingFlags.Static | BindingFlags.NonPublic);
                if ((strict == null) || (strict.FieldType != typeof(bool)) || ((bool)strict.GetValue(null)))
                    return;
                var sr = asm.GetType("System.SR");
                if (sr == null)
                    return;
                static String Get(Type sr, String name) => sr.GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null) as String;
                MsgUnsupported = Get(sr, "UnsupportedCompression");
                MsgNoMemory = Get(sr, "ZLibErrorNotEnoughMemory");
                MsgInconsistent = Get(sr, "ZLibErrorInconsistentStream");
                MsgUnexpected = Get(sr, "ZLibErrorUnexpected");
                if ((MsgUnsupported == null) || (MsgNoMemory == null) || (MsgInconsistent == null) || (MsgUnexpected == null))
                    return;
                ZLibExceptionCtor = asm.GetType("System.IO.Compression.ZLibException")?.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, [typeof(String), typeof(String), typeof(int), typeof(String)]);
                if (ZLibExceptionCtor == null)
                    return;
                if (!NativeLibrary.TryLoad("System.IO.Compression.Native", asm, null, out var lib))
                    return;
                if (!NativeLibrary.TryGetExport(lib, "CompressionNative_InflateInit2_", out var pInit))
                    return;
                if (!NativeLibrary.TryGetExport(lib, "CompressionNative_InflateReset2_", out var pReset))
                    return;
                if (!NativeLibrary.TryGetExport(lib, "CompressionNative_Inflate", out var pInflate))
                    return;
                if (!NativeLibrary.TryGetExport(lib, "CompressionNative_InflateEnd", out var pEnd))
                    return;
                InflateInit2 = (delegate* unmanaged<ZStream*, int, int>)pInit;
                InflateReset2 = (delegate* unmanaged<ZStream*, int, int>)pReset;
                Inflate = (delegate* unmanaged<ZStream*, int, int>)pInflate;
                InflateEnd = (delegate* unmanaged<ZStream*, int>)pEnd;
                Available = true;
            }
            catch
            {
            }
        }

        static readonly CompInstancePool<CompNativeInflater> Pool = new();

        /// <summary>
        /// Get a decoder (from the pool or a new one), return it using <see cref="Return"/>
        /// </summary>
        /// <returns>A decoder or null if the native decoder can't be used (use the DeflateStream)</returns>
        public static CompNativeInflater TryRent()
        {
            if (!Available)
                return null;
            return Pool.TryRent() ?? Create();
        }

        static CompNativeInflater Create()
        {
            var z = (ZStream*)NativeMemory.AllocZeroed((nuint)sizeof(ZStream));
            int e;
            try
            {
                e = InflateInit2(z, WindowBits);
            }
            catch
            {
                NativeMemory.Free(z);
                return null;
            }
            if (e != ZOk)
            {
                NativeMemory.Free(z);
                return null;
            }
            return new CompNativeInflater(z);
        }

        CompNativeInflater(ZStream* z)
        {
            Z = z;
        }

        ZStream* Z;

        /// <summary>
        /// Reset the decoder and return it to the pool (disposed if it can't be reset)
        /// </summary>
        public void Return()
        {
            var z = Z;
            z->NextIn = null;
            z->AvailIn = 0;
            z->NextOut = null;
            z->AvailOut = 0;
            if (InflateReset2(z, WindowBits) != ZOk)
            {
                Dispose();
                return;
            }
            Pool.Return(this);
        }

        /// <summary>
        /// Free the native state
        /// </summary>
        public void Dispose()
        {
            var z = Z;
            if (z == null)
                return;
            Z = null;
            InflateEnd(z);
            NativeMemory.Free(z);
            GC.SuppressFinalize(this);
        }

        ~CompNativeInflater()
        {
            var z = Z;
            if (z == null)
                return;
            Z = null;
            InflateEnd(z);
            NativeMemory.Free(z);
        }

        /// <summary>
        /// Throw the exception that the .NET inflater throws for an error code (returns for the non error codes)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ThrowIfError(int e)
        {
            if ((e == ZOk) || (e == ZStreamEnd) || (e == ZBufError))
                return;
            Throw(e);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void Throw(int e)
        {
            if (e == ZDataError)
                throw new InvalidDataException(MsgUnsupported);
            var m = Z->Msg;
            var msg = m != IntPtr.Zero ? Marshal.PtrToStringUTF8(m) : String.Empty;
            throw (Exception)ZLibExceptionCtor.Invoke([e == ZMemError ? MsgNoMemory : (e == ZStreamError ? MsgInconsistent : MsgUnexpected), "inflate_", e, msg]);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowTruncated() => throw new InvalidDataException(CompHelpers.DecTruncated);

        /// <summary>
        /// Decompress raw deflate data to a stream, the same as <see cref="CompDeflateNET.Decompress(Stream, Stream)"/> reading from a memory stream
        /// </summary>
        /// <param name="from">The compressed data</param>
        /// <param name="to">The stream to write the decompressed data to</param>
        [SkipLocalsInit]
        public void Decompress(ReadOnlySpan<Byte> from, Stream to)
        {
            if (from.IsEmpty)
            {
                if (!CompInflaterState.DeflateEmptyIsValid)
                    ThrowTruncated();
                return;
            }
            var z = Z;
            fixed (Byte* i = from)
            {
                z->NextIn = i;
                z->AvailIn = (uint)from.Length;
                //  The first chunk is decoded on the stack (no pool rent for small data)
                Span<Byte> buf = stackalloc Byte[ChunkSize];
                fixed (Byte* o = buf)
                {
                    if (Next(o, ChunkSize, to))
                        return;
                }
                //  Large output: small chunks are much slower (more native calls and window updates)
                var large = ArrayPoolStream.Rent(LargeChunkSize);
                try
                {
                    fixed (Byte* o = large)
                    {
                        while (!Next(o, LargeChunkSize, to))
                        {
                        }
                    }
                }
                finally
                {
                    ArrayPoolStream.Return(large);
                }
            }
        }

        /// <summary>
        /// Decode the next chunk of data (the input is set) and write it to a stream
        /// </summary>
        /// <param name="o">The chunk buffer</param>
        /// <param name="size">The size of the chunk buffer</param>
        /// <param name="to">The stream to write the decompressed data to</param>
        /// <returns>True when the end of the compressed data was reached</returns>
        bool Next(Byte* o, int size, Stream to)
        {
            var z = Z;
            z->NextOut = o;
            z->AvailOut = (uint)size;
            var e = Inflate(z, ZNoFlush);
            ThrowIfError(e);
            var n = size - (int)z->AvailOut;
            if (n > 0)
                to.Write(new ReadOnlySpan<Byte>(o, n));
            if (e == ZStreamEnd)
                return true;
            //  No progress or output space left: all input is consumed without reaching the end
            if ((e == ZBufError) || ((n < size) && (z->AvailIn == 0)))
                ThrowTruncated();
            return false;
        }

        /// <summary>
        /// Decompress raw deflate data to memory, the same as <see cref="CompDeflateNET.Decompress(Stream, Span{Byte})"/> reading from a memory stream
        /// </summary>
        /// <param name="from">The compressed data</param>
        /// <param name="to">The memory to write the decompressed data to</param>
        /// <returns>The number of bytes written</returns>
        /// <exception cref="ArgumentException">The decompressed data doesn't fit in <paramref name="to"/>.</exception>
        public int Decompress(ReadOnlySpan<Byte> from, Span<Byte> to)
        {
            if (from.IsEmpty)
            {
                if (!CompInflaterState.DeflateEmptyIsValid)
                    ThrowTruncated();
                return 0;
            }
            var z = Z;
            Byte probe;
            fixed (Byte* i = from)
            fixed (Byte* o = to)
            {
                z->NextIn = i;
                z->AvailIn = (uint)from.Length;
                z->NextOut = o;
                z->AvailOut = (uint)to.Length;
                bool probing = false;
                for (; ; )
                {
                    if (z->AvailOut == 0)
                    {
                        //  Full, check (with a single byte) that the data ends here (like CompStreamHelpers.ReadAll)
                        z->NextOut = &probe;
                        z->AvailOut = 1;
                        probing = true;
                    }
                    var e = Inflate(z, ZNoFlush);
                    ThrowIfError(e);
                    if (probing && (z->AvailOut == 0))
                        throw new ArgumentException(CompHelpers.DecDestTooSmall, nameof(to));
                    if (e == ZStreamEnd)
                        return probing ? to.Length : to.Length - (int)z->AvailOut;
                    //  No progress or output space left: all input is consumed without reaching the end
                    if ((e == ZBufError) || ((z->AvailOut != 0) && (z->AvailIn == 0)))
                        ThrowTruncated();
                }
            }
        }
    }
}
