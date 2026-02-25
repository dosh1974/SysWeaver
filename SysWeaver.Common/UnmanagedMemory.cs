
using System;
using System.Runtime.CompilerServices;

namespace SysWeaver
{
    public static class UnmanagedMemory
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<T> Empty<T>() where T : unmanaged
            => UnmanagedMemory<T>.EmptyReadOnlyMemory;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<T> Create<T>(ReadOnlyMemory<T> data) where T : unmanaged
            => data.IsEmpty ? UnmanagedMemory<T>.EmptyReadOnlyMemory : new UnmanagedMemory<T>.CustomReadOnlyMemory(data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedReadOnlyMemory<T> Create<T>(ReadOnlyMemory<T> data, Action<ReadOnlyMemory<T>> onDispose) where T : unmanaged
            => new UnmanagedMemory<T>.CustomReadOnlyMemoryD(data, onDispose);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedMemory<T> Create<T>(Memory<T> data) where T : unmanaged
            => data.IsEmpty ? UnmanagedMemory<T>.EmptyMemory : new UnmanagedMemory<T>.CustomMemory(data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IUnmanagedMemory<T> Create<T>(Memory<T> data, Action<Memory<T>> onDispose) where T : unmanaged
            => new UnmanagedMemory<T>.CustomMemoryD(data, onDispose);

    }


    /// <summary>
    /// Represents the readonly content of some unmanaged memory resource.
    /// Dispose when no more copies of the Memory is in use (Span's and pointers derived from it etc too).
    /// </summary>
    /// <typeparam name="T"></typeparam>
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
    /// <typeparam name="T"></typeparam>
    public interface IUnmanagedMemory<T> : IUnmanagedReadOnlyMemory<T> where T : unmanaged
    {
        /// <summary>
        /// The content of the unmanaged memory resource.
        /// </summary>
        new Memory<T> Memory { get; }
    }


    internal static class UnmanagedMemory<T> where T : unmanaged
    {
        public static IUnmanagedReadOnlyMemory<T> EmptyReadOnlyMemory = new CustomReadOnlyMemory(ReadOnlyMemory<T>.Empty);

        public static IUnmanagedMemory<T> EmptyMemory = new CustomMemory(Memory<T>.Empty);

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
