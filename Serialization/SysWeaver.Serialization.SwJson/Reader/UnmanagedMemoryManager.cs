using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace SysWeaver.Serialization.SwJson.Reader
{


    /// <summary>
    /// A <see cref="MemoryManager{T}"/> over a raw pointer, used to get a <see cref="Memory{T}"/> (for dictionary look ups) over the json data without copying.
    /// </summary>
    /// <remarks>
    /// The pointer is assumed to be fully unmanaged, or externally pinned - no attempt will be made to pin this data.
    /// Instances are reused (see <see cref="Set(T*, int)"/>), any <see cref="Memory{T}"/> obtained is only valid until the next <see cref="Set(T*, int)"/> and while the data is pinned.
    /// Not thread safe, each <see cref="JsonParserState"/> owns one.
    /// </remarks>
    /// <typeparam name="T">The element type</typeparam>
    sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T>
        where T : unmanaged
    {
        T* _pointer;
        int _length;

        /// <summary>
        /// Create a new UnmanagedMemoryManager instance at the given pointer and size
        /// </summary>
        /// <param name="pointer">The start of the memory</param>
        /// <param name="length">The number of elements</param>
        public UnmanagedMemoryManager(T* pointer = null, int length = 0)
        {
            _pointer = pointer;
            _length = length;
        }

        /// <summary>
        /// Change the memory region (invalidates previously obtained <see cref="Memory{T}"/> instances).
        /// </summary>
        /// <param name="pointer">The start of the memory</param>
        /// <param name="length">The number of elements</param>
        public void Set(T* pointer, int length)
        {
            _pointer = pointer;
            _length = length;
        }

        /// <summary>
        /// Obtains a span that represents the region
        /// </summary>
        public override Span<T> GetSpan() => new Span<T>(_pointer, _length);

        /// <summary>
        /// Provides access to a pointer that represents the data (note: no actual pin occurs)
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="elementIndex"/> is negative or not less than the length (so an empty region can't be pinned)</exception>
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex >= _length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            return new MemoryHandle(_pointer + elementIndex);
        }
        
        /// <summary>
        /// Has no effect
        /// </summary>
        public override void Unpin() { }

        /// <summary>
        /// Has no effect (the memory isn't owned)
        /// </summary>
        protected override void Dispose(bool disposing) { }

    }




}
