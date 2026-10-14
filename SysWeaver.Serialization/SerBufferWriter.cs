using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SysWeaver.Serialization
{
    /// <summary>
    /// A buffer writer that writes into pooled buffers (<see cref="ArrayPoolStream.Rent(int)"/>), for serialized data that is only needed temporarily
    /// (ex: <see cref="ISerializer.Serialize{T}(T, IBufferWriter{byte}, SerializerOptions)"/> followed by a deserialization) without allocating an array for it.
    /// </summary>
    /// <remarks>
    /// Not thread safe. Get an instance using <see cref="Rent(int)"/> and dispose it when the data is no longer used (the buffer is returned to the pool and the instance is reused,
    /// one instance is cached per thread, so renting usually doesn't allocate).
    /// The instance must not be used after it's disposed.
    /// </remarks>
    public sealed class SerBufferWriter : IBufferWriter<Byte>, IDisposable
    {
        SerBufferWriter()
        {
        }

        [ThreadStatic]
        static SerBufferWriter Cached;

        /// <summary>
        /// Get an empty writer (dispose it when done)
        /// </summary>
        /// <param name="initialSize">The minimum size of the first buffer in bytes (at least 256 bytes are used)</param>
        /// <returns>An empty writer</returns>
        public static SerBufferWriter Rent(int initialSize = 4096)
        {
            var b = ArrayPoolStream.Rent(initialSize < 256 ? 256 : initialSize);
            var w = Cached;
            if (w != null)
                Cached = null;
            else
                w = new SerBufferWriter();
            w.Buffer = b;
            w.Pos = 0;
            return w;
        }

        Byte[] Buffer;
        int Pos;

        /// <summary>
        /// The number of bytes written
        /// </summary>
        public int WrittenCount => Pos;

        /// <summary>
        /// The data written (valid until more data is written or the writer is disposed)
        /// </summary>
        public ReadOnlySpan<Byte> WrittenSpan => new ReadOnlySpan<Byte>(Buffer, 0, Pos);

        /// <summary>
        /// The data written (valid until more data is written or the writer is disposed)
        /// </summary>
        public ReadOnlyMemory<Byte> WrittenMemory => new ReadOnlyMemory<Byte>(Buffer, 0, Pos);

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative or larger than the space of the last requested buffer.</exception>
        public void Advance(int count)
        {
            if ((uint)count > (uint)(Buffer.Length - Pos))
                throw new ArgumentOutOfRangeException(nameof(count));
            Pos += count;
        }

        /// <inheritdoc/>
        public Memory<Byte> GetMemory(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return Buffer.AsMemory(Pos);
        }

        /// <inheritdoc/>
        public Span<Byte> GetSpan(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return Buffer.AsSpan(Pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Ensure(int sizeHint)
        {
            if (sizeHint <= 0)
                sizeHint = 1;
            if (sizeHint > (Buffer.Length - Pos))
                Grow(sizeHint);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void Grow(int sizeHint)
        {
            var old = Buffer;
            var pos = Pos;
            var need = (long)pos + sizeHint;
            if (need > Array.MaxLength)
                throw new OutOfMemoryException();
            var size = Math.Min(Math.Max((long)old.Length << 1, need), Array.MaxLength);
            var b = ArrayPoolStream.Rent((int)size);
            old.AsSpan(0, pos).CopyTo(b);
            Buffer = b;
            ArrayPoolStream.Return(old);
        }

        /// <summary>
        /// Return the buffer to the pool, the writer must not be used after this call
        /// </summary>
        public void Dispose()
        {
            var b = Buffer;
            if (b == null)
                return;
            Buffer = null;
            Pos = 0;
            ArrayPoolStream.Return(b);
            Cached = this;
        }
    }
}
