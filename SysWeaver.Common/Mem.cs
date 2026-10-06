using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver.Memory
{

    /// <summary>
    /// Helpers to wrap spans and pointers as Memory (no copying), to process spans as streams and to get the array behind some memory
    /// </summary>
    public static class Mem
    {
        /// <summary>
        /// Get as memory (no copying is done).
        /// Lifetime management must be done by the callee.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="t">The source data, must be unmanaged memory or pinned for as long as the returned memory is used</param>
        /// <returns>Memory that references the same data as the span</returns>
        public static Memory<T> ToMemory<T>(this Span<T> t) where T : unmanaged => new UnmanagedMemoryManager<T>(t).Memory;

        /// <summary>
        /// Get as memory (no copying is done).
        /// Lifetime management must be done by the callee.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="ptr">The memory address</param>
        /// <param name="length">The length as the number of T's</param>
        /// <returns>Memory that references the data at the address</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative</exception>
        public static Memory<T> ToMemory<T>(this IntPtr ptr, int length) where T : unmanaged => new UnmanagedMemoryManager<T>(ptr, length).Memory;

        /// <summary>
        /// Get as memory (no copying is done).
        /// Lifetime management must be done by the callee.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="ptr">The memory address</param>
        /// <param name="length">The length as the number of T's</param>
        /// <returns>Memory that references the data at the address</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative</exception>
        public static unsafe Memory<T> ToMemory<T>(T* ptr, int length) where T : unmanaged => new UnmanagedMemoryManager<T>(ptr, length).Memory;


        /// <summary>
        /// Get as memory (no copying is done)
        /// Lifetime management must be done by the callee.
        /// </summary>
        /// <param name="ptr">The memory address</param>
        /// <param name="length">The length as the number of bytes</param>
        /// <returns>Memory that references the data at the address</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative</exception>
        public static unsafe Memory<Byte> ToMemory(this IntPtr ptr, int length) => new UnmanagedMemoryManager<Byte>(ptr, length).Memory;


        /// <summary>
        /// Get as memory (no copying is done)
        /// Lifetime management must be done by the callee.
        /// </summary>
        /// <param name="ptr">The memory address</param>
        /// <param name="length">The length as the number of bytes</param>
        /// <returns>Memory that references the data at the address</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative</exception>
        public static unsafe Memory<Byte> ToMemory(void* ptr, int length) => new UnmanagedMemoryManager<Byte>((byte*)ptr, length).Memory;

        /// <summary>
        /// Get as readonly memory (no copying is done)
        /// Lifetime management must be done by the callee.
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="t">The source data, must be unmanaged memory or pinned for as long as the returned memory is used</param>
        /// <returns>Memory that references the same data as the span</returns>
        public static ReadOnlyMemory<T> ToMemory<T>(this ReadOnlySpan<T> t) where T : unmanaged => new UnmanagedMemoryManager<T>(t).ReadOnlyMemory;

        /// <summary>
        /// Process a span as a stream ( no copying is done).
        /// The stream is a readonly, seekable stream with all bytes of the elements (Length = mem.Length * sizeof(T)), it's disposed when the action returns.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="mem">The memory to read from</param>
        /// <param name="onStream">The action to perform on the stream (the stream must not be used after the action returns)</param>
        /// <exception cref="ArgumentNullException"><paramref name="onStream"/> is null</exception>
        public unsafe static void StreamProcess<T>(this ReadOnlySpan<T> mem, Action<Stream> onStream) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(onStream);
            fixed (T* bp = mem)
            {
                // fixed gives a null pointer for an empty span, UnmanagedMemoryStream doesn't accept that
                byte d0;
                using var ms = new UnmanagedMemoryStream(bp == null ? &d0 : (byte*)bp, (long)mem.Length * sizeof(T));
                onStream(ms);
            }
        }

        /// <summary>
        /// Process a span as a stream ( no copying is done).
        /// The stream is a readonly, seekable stream with all bytes of the elements (Length = mem.Length * sizeof(T)), it's disposed when the function returns.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <typeparam name="R">The return type</typeparam>
        /// <param name="mem">The memory to read from</param>
        /// <param name="onStream">The function to perform on the stream (the stream must not be used after the function returns)</param>
        /// <returns>The result of the onStream function</returns>
        /// <exception cref="ArgumentNullException"><paramref name="onStream"/> is null</exception>
        public unsafe static R StreamProcess<R, T>(this ReadOnlySpan<T> mem, Func<Stream, R> onStream) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(onStream);
            fixed (T* bp = mem)
            {
                // fixed gives a null pointer for an empty span, UnmanagedMemoryStream doesn't accept that
                byte d0;
                using var ms = new UnmanagedMemoryStream(bp == null ? &d0 : (byte*)bp, (long)mem.Length * sizeof(T));
                return onStream(ms);
            }
        }

        /// <summary>
        /// Process a span as a stream ( no copying is done).
        /// The stream is a readonly, seekable stream with all bytes of the elements (Length = mem.Length * sizeof(T)), it's disposed when the action returns.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <typeparam name="A">The custom argument type</typeparam>
        /// <param name="mem">The memory to read from</param>
        /// <param name="onStream">The action to perform on the stream (the stream must not be used after the action returns)</param>
        /// <param name="arg">An custom argument that is passed to the on stream action</param>
        /// <exception cref="ArgumentNullException"><paramref name="onStream"/> is null</exception>
        public unsafe static void StreamProcess<T, A>(this ReadOnlySpan<T> mem, Action<Stream, A> onStream, A arg) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(onStream);
            fixed (T* bp = mem)
            {
                // fixed gives a null pointer for an empty span, UnmanagedMemoryStream doesn't accept that
                byte d0;
                using var ms = new UnmanagedMemoryStream(bp == null ? &d0 : (byte*)bp, (long)mem.Length * sizeof(T));
                onStream(ms, arg);
            }
        }

        /// <summary>
        /// Process a span as a stream ( no copying is done).
        /// The stream is a readonly, seekable stream with all bytes of the elements (Length = mem.Length * sizeof(T)), it's disposed when the function returns.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <typeparam name="R">The return type</typeparam>
        /// <typeparam name="A">The custom argument type</typeparam>
        /// <param name="mem">The memory to read from</param>
        /// <param name="onStream">The function to perform on the stream (the stream must not be used after the function returns)</param>
        /// <param name="arg">An custom argument that is passed to the on stream function</param>
        /// <returns>The result of the onStream function</returns>
        /// <exception cref="ArgumentNullException"><paramref name="onStream"/> is null</exception>
        public unsafe static R StreamProcess<R, T, A>(this ReadOnlySpan<T> mem, Func<Stream, A, R> onStream, A arg) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(onStream);
            fixed (T* bp = mem)
            {
                // fixed gives a null pointer for an empty span, UnmanagedMemoryStream doesn't accept that
                byte d0;
                using var ms = new UnmanagedMemoryStream(bp == null ? &d0 : (byte*)bp, (long)mem.Length * sizeof(T));
                return onStream(ms, arg);
            }
        }

        /// <summary>
        /// Try to get the array behind some memory.
        /// Note that the memory may only be a part of the array (the offset and length are not returned), use MemoryMarshal.TryGetArray to get the segment.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="mem">The memory to get an array from</param>
        /// <returns>The array behind the memory region (or null if the memory isn't backed by an array). Empty memory may return an empty array</returns>
        public static T[] TryGetArray<T>(this Memory<T> mem) where T : unmanaged
        {
            if (!MemoryMarshal.TryGetArray<T>(mem, out var seg))
                return null;
            return seg.Array;
        }

        /// <summary>
        /// Try to get the array behind some readonly memory.
        /// Note that the memory may only be a part of the array (the offset and length are not returned), use MemoryMarshal.TryGetArray to get the segment.
        /// Warning! The returned array may be written to but since this is supposed to be readonly memory, don't do it (it may also crash).
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="mem">The memory to get an array from</param>
        /// <returns>The array behind the memory region (or null if the memory isn't backed by an array). Empty memory may return an empty array</returns>
        public static T[] TryGetArray<T>(this ReadOnlyMemory<T> mem) where T : unmanaged
        {
            if (!MemoryMarshal.TryGetArray<T>(mem, out var seg))
                return null;
            return seg.Array;
        }


    }



}
