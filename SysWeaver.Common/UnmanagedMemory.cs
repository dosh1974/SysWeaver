
using System;
using System.Runtime.CompilerServices;

namespace SysWeaver
{
    /// <summary>
    /// Factory methods for IUnmanagedReadOnlyMemory and IUnmanagedMemory (wrap existing memory, optionally with an action to perform on dispose)
    /// </summary>
    public static class UnmanagedMemory
    {

        /// <summary>
        /// Get an empty instance (a shared instance, disposing it does nothing).
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <returns>A shared empty instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<T> Empty<T>() where T : unmanaged
            => UnmanagedMemory<T>.EmptyReadOnlyMemory;

        /// <summary>
        /// Wrap some memory, disposing the returned object does nothing.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="data">The memory to wrap</param>
        /// <returns>An object that exposes the memory (the shared empty instance if the memory is empty)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<T> Create<T>(ReadOnlyMemory<T> data) where T : unmanaged
            => data.IsEmpty ? UnmanagedMemory<T>.EmptyReadOnlyMemory : new UnmanagedMemory<T>.CustomReadOnlyMemory(data);

        /// <summary>
        /// Wrap some memory, the onDispose action is called with the memory when the returned object is disposed.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="data">The memory to wrap</param>
        /// <param name="onDispose">The action to call on dispose (also for empty memory), may be null</param>
        /// <returns>An object that exposes the memory</returns>
        /// <remarks>The action is called every time Dispose is called (the object doesn't track if it's already disposed)</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<T> Create<T>(ReadOnlyMemory<T> data, Action<ReadOnlyMemory<T>> onDispose) where T : unmanaged
            => new UnmanagedMemory<T>.CustomReadOnlyMemoryD(data, onDispose);

        /// <summary>
        /// Wrap some writable memory, disposing the returned object does nothing.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="data">The memory to wrap</param>
        /// <returns>An object that exposes the memory (the shared empty instance if the memory is empty)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedMemory<T> Create<T>(Memory<T> data) where T : unmanaged
            => data.IsEmpty ? UnmanagedMemory<T>.EmptyMemory : new UnmanagedMemory<T>.CustomMemory(data);

        /// <summary>
        /// Wrap some writable memory, the onDispose action is called with the memory when the returned object is disposed.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="data">The memory to wrap</param>
        /// <param name="onDispose">The action to call on dispose (also for empty memory), may be null</param>
        /// <returns>An object that exposes the memory</returns>
        /// <remarks>The action is called every time Dispose is called (the object doesn't track if it's already disposed)</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedMemory<T> Create<T>(Memory<T> data, Action<Memory<T>> onDispose) where T : unmanaged
            => new UnmanagedMemory<T>.CustomMemoryD(data, onDispose);

    }


    /// <summary>
    /// Represents the readonly content of some unmanaged memory resource.
    /// Dispose when no more copies of the Memory is in use (Span's and pointers derived from it etc too).
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    public interface IUnmanagedReadOnlyMemory<T> : IDisposable where T : unmanaged
    {
        /// <summary>
        /// The readonly content of the unmanaged memory resource.
        /// </summary>
        ReadOnlyMemory<T> Memory { get; }
    }



    /// <summary>
    /// Represents the content of some unmanaged memory resource.
    /// Dispose when no more copies of the Memory is in use (Span's and pointers derived from it etc too).
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    public interface IUnmanagedMemory<T> : IUnmanagedReadOnlyMemory<T> where T : unmanaged
    {
        /// <summary>
        /// The content of the unmanaged memory resource.
        /// </summary>
        new Memory<T> Memory { get; }
    }


    internal static class UnmanagedMemory<T> where T : unmanaged
    {
        public static readonly IUnmanagedReadOnlyMemory<T> EmptyReadOnlyMemory = new CustomReadOnlyMemory(ReadOnlyMemory<T>.Empty);

        public static readonly IUnmanagedMemory<T> EmptyMemory = new CustomMemory(Memory<T>.Empty);

        public struct CustomReadOnlyMemory : IUnmanagedReadOnlyMemory<T>
        {
            public CustomReadOnlyMemory(ReadOnlyMemory<T> mem)
            {
                Mem = mem;
            }

            readonly ReadOnlyMemory<T> Mem;
            public ReadOnlyMemory<T> Memory => Mem;

            public void Dispose()
            {
            }
        }


        public struct CustomReadOnlyMemoryD : IUnmanagedReadOnlyMemory<T>
        {
            public CustomReadOnlyMemoryD(ReadOnlyMemory<T> mem, Action<ReadOnlyMemory<T>> onDispose)
            {
                Mem = mem;
                D = onDispose;
            }

            readonly ReadOnlyMemory<T> Mem;
            readonly Action<ReadOnlyMemory<T>> D;
            public ReadOnlyMemory<T> Memory => Mem;

            public void Dispose()
            {
                D?.Invoke(Mem);
            }
        }


        public struct CustomMemory : IUnmanagedMemory<T>
        {
            public CustomMemory(Memory<T> mem)
            {
                Mem = mem;
            }

            readonly Memory<T> Mem;
            public Memory<T> Memory => Mem;

            ReadOnlyMemory<T> IUnmanagedReadOnlyMemory<T>.Memory => Mem;

            public void Dispose()
            {
            }
        }


        public struct CustomMemoryD : IUnmanagedMemory<T>
        {
            public CustomMemoryD(Memory<T> mem, Action<Memory<T>> onDispose)
            {
                Mem = mem;
                D = onDispose;
            }

            readonly Memory<T> Mem;
            readonly Action<Memory<T>> D;
            public Memory<T> Memory => Mem;

            ReadOnlyMemory<T> IUnmanagedReadOnlyMemory<T>.Memory => Mem;

            public void Dispose()
            {
                D?.Invoke(Mem);
            }
        }

    }



}
