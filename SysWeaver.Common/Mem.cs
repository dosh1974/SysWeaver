using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SysWeaver.Memory
{


    public static class Mem
    {
        /// <summary>
        /// Get as memory (no copying is done).
        /// Lifetime management must be done by the callee.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="t">The source data</param>
        /// <returns>Memory</returns>
        public static Memory<T> ToMemory<T>(this Span<T> t) where T : unmanaged => new UnmanagedMemoryManager<T>(t).Memory;

        /// <summary>
        /// Get as memory (no copying is done).
        /// Lifetime management must be done by the callee.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="ptr">The memory address</param>
        /// <param name="length">The length as the number of T's</param>
        /// <returns>Memory</returns>
        public static Memory<T> ToMemory<T>(this IntPtr ptr, int length) where T : unmanaged => new UnmanagedMemoryManager<T>(ptr, length).Memory;

        /// <summary>
        /// Get as memory (no copying is done).
        /// Lifetime management must be done by the callee.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="ptr">The memory address</param>
        /// <param name="length">The length as the number of T's</param>
        /// <returns>Memory</returns>
        public static unsafe Memory<T> ToMemory<T>(T* ptr, int length) where T : unmanaged => new UnmanagedMemoryManager<T>(ptr, length).Memory;


        /// <summary>
        /// Get as memory (no copying is done)
        /// Lifetime management must be done by the callee.
        /// </summary>
        /// <param name="ptr">The memory address</param>
        /// <param name="length">The length as the number of bytes</param>
        /// <returns>Memory</returns>
        public static unsafe Memory<Byte> ToMemory(this IntPtr ptr, int length) => new UnmanagedMemoryManager<Byte>(ptr, length).Memory;


        /// <summary>
        /// Get as memory (no copying is done)
        /// Lifetime management must be done by the callee.
        /// </summary>
        /// <param name="ptr">The memory address</param>
        /// <param name="length">The length as the number of bytes</param>
        /// <returns>Memory</returns>
        public static unsafe Memory<Byte> ToMemory(void* ptr, int length) => new UnmanagedMemoryManager<Byte>((byte*)ptr, length).Memory;

        /// <summary>
        /// Get as readonly memory (no copying is done)
        /// Lifetime management must be done by the callee.
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        /// <param name="t">The source data</param>
        /// <returns>Memory</returns>
        public static ReadOnlyMemory<T> ToMemory<T>(this ReadOnlySpan<T> t) where T : unmanaged => new UnmanagedMemoryManager<T>(t).ReadOnlyMemory;

        /// <summary>
        /// Process a span as a stream ( no copying is done)
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="mem">The memory to read from</param>
        /// <param name="onStream">The action to perform on the stream</param>
        public unsafe static void StreamProcess<T>(this ReadOnlySpan<T> mem, Action<Stream> onStream) where T : unmanaged
        {
            fixed (T* bp = mem)
            {
                using var ms = new UnmanagedMemoryStream((byte*)bp, mem.Length * Marshal.SizeOf<T>());
                onStream(ms);
            }
        }

        /// <summary>
        /// Process a span as a stream ( no copying is done).
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <typeparam name="R">The return type</typeparam>
        /// <param name="mem">The memory to read from</param>
        /// <param name="onStream">The action to perform on the stream</param>
        /// <returns>The result of the onStream function</returns>
        public unsafe static R StreamProcess<R, T>(this ReadOnlySpan<T> mem, Func<Stream, R> onStream) where T : unmanaged
        {
            fixed (T* bp = mem)
            {
                using var ms = new UnmanagedMemoryStream((byte*)bp, mem.Length * Marshal.SizeOf<T>());
                return onStream(ms);
            }
        }

        /// <summary>
        /// Process a span as a stream ( no copying is done)
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <typeparam name="A">The custom argument type</typeparam>
        /// <param name="mem">The memory to read from</param>
        /// <param name="onStream">The action to perform on the stream</param>
        /// <param name="arg">An custom argument that is passed to the on stream action</param>
        public unsafe static void StreamProcess<T, A>(this ReadOnlySpan<T> mem, Action<Stream, A> onStream, A arg) where T : unmanaged
        {
            fixed (T* bp = mem)
            {
                using var ms = new UnmanagedMemoryStream((byte*)bp, mem.Length * Marshal.SizeOf<T>());
                onStream(ms, arg);
            }
        }

        /// <summary>
        /// Process a span as a stream ( no copying is done).
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <typeparam name="R">The return type</typeparam>
        /// <typeparam name="A">The custom argument type</typeparam>
        /// <param name="mem">The memory to read from</param>
        /// <param name="onStream">The action to perform on the stream</param>
        /// <param name="arg">An custom argument that is passed to the on stream function</param>
        /// <returns>The result of the onStream function</returns>
        public unsafe static R StreamProcess<R, T, A>(this ReadOnlySpan<T> mem, Func<Stream, A, R> onStream, A arg) where T : unmanaged
        {
            fixed (T* bp = mem)
            {
                using var ms = new UnmanagedMemoryStream((byte*)bp, mem.Length * Marshal.SizeOf<T>());
                return onStream(ms, arg);
            }
        }

        /// <summary>
        /// Try to get an array from some memory
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="mem">The memory to get an array from</param>
        /// <returns>An array from the memory region (or null if it failed)</returns>
        public static T[] TryGetArray<T>(this Memory<T> mem) where T : unmanaged
        {
            if (!MemoryMarshal.TryGetArray<T>(mem, out var seg))
                return null;
            return seg.Array;
        }

        /// <summary>
        /// Try to get an array from some readonly memory.
        /// Warning! The returned array may be written to but since this is supposed to be readonly memory, don't do it (it may also crash).
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="mem">The memory to get an array from</param>
        /// <returns>An array from the memory region (or null if it failed)</returns>
        public static T[] TryGetArray<T>(this ReadOnlyMemory<T> mem) where T : unmanaged
        {
            if (!MemoryMarshal.TryGetArray<T>(mem, out var seg))
                return null;
            return seg.Array;
        }


    }



}
