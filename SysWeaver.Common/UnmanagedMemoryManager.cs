using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace SysWeaver.Memory
{
    /// <summary>
    /// A <see cref="MemoryManager{T}"/> over a raw pointer, used to expose unmanaged (or externally pinned) memory as <see cref="Memory{T}"/> without copying.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <remarks>
    /// The pointer is assumed to be fully unmanaged, or externally pinned - no attempt will be made to pin this data.
    /// The manager doesn't own the memory, disposing it does nothing, the caller must keep the memory alive (and pinned) for as long as any derived Memory / Span is used.
    /// </remarks>
    public sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T>
        where T : unmanaged
    {
        readonly T* _pointer;
        readonly int _length;

        /// <summary>
        /// Create a new UnmanagedMemoryManager instance that references the same memory as a span
        /// </summary>
        /// <param name="span">The memory to reference</param>
        /// <remarks>It is assumed that the span provided is already unmanaged or externally pinned</remarks>
        public UnmanagedMemoryManager(Span<T> span)
        {
            fixed (T* ptr = &MemoryMarshal.GetReference(span))
            {
                _pointer = ptr;
                _length = span.Length;
            }
        }

        /// <summary>
        /// Create a new UnmanagedMemoryManager instance that references the same memory as a readonly span.
        /// Note that the memory exposed by the manager is writable.
        /// </summary>
        /// <param name="span">The memory to reference</param>
        /// <remarks>It is assumed that the span provided is already unmanaged or externally pinned</remarks>
        public UnmanagedMemoryManager(ReadOnlySpan<T> span)
        {
            fixed (T* ptr = &MemoryMarshal.GetReference(span))
            {
                _pointer = ptr;
                _length = span.Length;
            }
        }

        /// <summary>
        /// Create a new UnmanagedMemoryManager instance at the given pointer and size
        /// </summary>
        /// <param name="pointer">The address of the first element</param>
        /// <param name="length">The number of elements</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative</exception>
        public UnmanagedMemoryManager(T* pointer, int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            _pointer = pointer;
            _length = length;
        }

        /// <summary>
        /// Create a new UnmanagedMemoryManager instance at the given pointer and size
        /// </summary>
        /// <param name="pointer">The address of the first element</param>
        /// <param name="length">The number of elements</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative</exception>
        public UnmanagedMemoryManager(IntPtr pointer, int length) : this((T*)pointer.ToPointer(), length) { }

        /// <summary>
        /// Obtains a span that represents the region
        /// </summary>
        /// <returns>A span over the whole region</returns>
        public override Span<T> GetSpan() => new Span<T>(_pointer, _length);

        /// <summary>
        /// Provides access to a pointer that represents the data (note: no actual pin occurs)
        /// </summary>
        /// <param name="elementIndex">The index of the element to get a pointer to</param>
        /// <returns>A handle with the address of the element (disposing it does nothing)</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="elementIndex"/> is negative or greater than the length</exception>
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex > _length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            return new MemoryHandle(_pointer + elementIndex);
        }
        
        /// <summary>
        /// Has no effect
        /// </summary>
        public override void Unpin() { }

        /// <summary>
        /// Does nothing (the memory isn't owned by the manager)
        /// </summary>
        /// <param name="disposing">Ignored</param>
        protected override void Dispose(bool disposing) { }

        /// <summary>
        /// Get the whole region as readonly memory (same as <see cref="MemoryManager{T}.Memory"/>)
        /// </summary>
        public ReadOnlyMemory<T> ReadOnlyMemory => Memory;

    }




}
