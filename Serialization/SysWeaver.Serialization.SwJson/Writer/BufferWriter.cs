using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver.Serialization.SwJson.Writer
{

    /// <summary>
    /// A growable, pinned byte buffer that the <see cref="JsonWriter"/> writes UTF8 json to, using raw pointers.
    /// Writers call <see cref="Ensure"/> before writing, the write methods themselves don't check bounds (except <see cref="Validate"/> in DEBUG builds).
    /// </summary>
    /// <remarks>
    /// The buffer is always pinned (by the caller using fixed, or by a <see cref="GCHandle"/> owned by the writer), so <see cref="DataPtr"/> is stable until the buffer grows.
    /// Growing replaces <see cref="Data"/> and <see cref="DataPtr"/>, any pointer into the old buffer is invalid after a call to <see cref="Ensure"/>.
    /// Must be disposed (frees the pin handle and returns rented buffers).
    /// Don't copy an instance (copies share the pin handle and rented buffer, disposing more than one copy frees or returns them twice).
    /// Not thread safe.
    /// </remarks>
    [SkipLocalsInit]
    unsafe public ref struct BufferWriter : IDisposable
    {
        /// <summary>
        /// If true, boxed values of primitive like types (numbers, strings, bool, char, date / time types, Guid and enums) are written without type information ("$type"),
        /// else every boxed value (where the runtime type differs from the declared type) is written with type information.
        /// Set by the <see cref="JsonWriter"/> entry points from their typeIsOptional parameter.
        /// </summary>
        public bool TypeIsOptional = false;

        /// <summary>
        /// Create a writer that pins the buffer using a <see cref="GCHandle"/> and grows by allocating new arrays (not pooled).
        /// </summary>
        /// <param name="initData">The initial buffer, if null a 4 KB buffer is allocated. Written to in place until it needs to grow, after that the new buffer is available using <see cref="GetBuffer"/></param>
        /// <param name="startOffset">The offset to start writing at</param>
        public BufferWriter(Byte[] initData, int startOffset = 0)
        {
            var d = initData ?? GC.AllocateUninitializedArray<Byte>(4096);//  (Rented =ArrayPoolStream.Rent(4096));
            Data = d;
            PinHandle = GCHandle.Alloc(d, GCHandleType.Pinned);
            DataPtr = (Byte*)PinHandle.AddrOfPinnedObject().ToPointer();
            S = d.Length;
            Offset = startOffset;
        }

        /// <summary>
        /// Use a buffer that is already pinned by the caller (using fixed, cheaper than a GCHandle), the buffer must stay pinned until Dispose is called.
        /// If the buffer grows, the new buffer is pinned (and freed) by the writer.
        /// </summary>
        /// <param name="pinnedData">The buffer (pinned)</param>
        /// <param name="pinnedPtr">The address of the first byte in the buffer (null for an empty buffer)</param>
        /// <param name="startOffset">The offset to start writing at</param>
        internal BufferWriter(Byte[] pinnedData, Byte* pinnedPtr, int startOffset)
        {
            Data = pinnedData;
            DataPtr = pinnedPtr;
            S = pinnedData.Length;
            Offset = startOffset;
        }

        /// <summary>
        /// A writer that grows using pooled buffers (see <see cref="Pooled"/>).
        /// </summary>
        /// <param name="pinnedData">The initial buffer (pinned by the caller until Dispose is called)</param>
        /// <param name="pinnedPtr">The address of the first byte in the buffer (null for an empty buffer)</param>
        /// <param name="startOffset">The offset to start writing at</param>
        /// <param name="rented">True if the initial buffer is rented from <see cref="ArrayPoolStream"/>, it's then returned by the writer (when growing or disposed)</param>
        /// <param name="capacity">The (logical) capacity, at most the length of the buffer</param>
        internal BufferWriter(Byte[] pinnedData, Byte* pinnedPtr, int startOffset, bool rented, int capacity)
        {
            Data = pinnedData;
            DataPtr = pinnedPtr;
            S = capacity;
            Offset = startOffset;
            Pooled = true;
            Rented = rented;
        }

        /// <summary>
        /// If true the writer grows using buffers rented from <see cref="ArrayPoolStream"/> (no garbage), the buffer must then be detached (see <see cref="DetachBuffer"/>) or copied before the writer is disposed.
        /// The capacity is tracked separately from the length of the (rented) buffer, so that a detached buffer is only as big as needed (the largest size ensured + a small margin),
        /// a caller can reuse it for the same data without growing.
        /// </summary>
        internal readonly bool Pooled;

        /// <summary>
        /// True if the current buffer is rented from <see cref="ArrayPoolStream"/> by the writer (it's returned when the writer grows or is disposed)
        /// </summary>
        internal bool Rented;

        /// <summary>
        /// Get a buffer that the caller owns, with everything written so far: the current buffer if it isn't rented, else a copy (with the same capacity).
        /// </summary>
        /// <returns>A buffer with the first <see cref="Position"/> bytes written, it's at least <see cref="Position"/> bytes long (the content after that is undefined)</returns>
        internal Byte[] DetachBuffer()
        {
            var d = Data;
            if (!Rented)
                return d;
            var o = Offset;
            var b = GC.AllocateUninitializedArray<Byte>(Math.Max(S, o));
            new ReadOnlySpan<Byte>(DataPtr, o).CopyTo(b);
            return b;
        }

        /// <summary>
        /// Free the pin handle (if any) and return the current buffer to the pool if it's rented.
        /// The <see cref="Data"/> is set to null, the writer must not be used after this.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (PinHandle.IsAllocated)
                PinHandle.Free();
            if (Rented)
            {
                Rented = false;
                ArrayPoolStream.Return(Data);
            }
            Data = null;
        }

        /// <summary>
        /// The pin handle of the current buffer, if it's pinned by the writer (not allocated if the caller pinned it using fixed)
        /// </summary>
        public GCHandle PinHandle;
        /// <summary>
        /// The address of the first byte of <see cref="Data"/> (changes when the buffer grows)
        /// </summary>
        public Byte* DataPtr;

        /// <summary>
        /// The current buffer (replaced when the buffer grows)
        /// </summary>
        public Byte[] Data;
        /// <summary>
        /// The current write position (from the start of the buffer), also the number of bytes used when writing started at offset 0
        /// </summary>
        public int Offset;


        /// <summary>
        /// Get the free space of the buffer, from the current position to the <see cref="Capacity"/>.
        /// Used with TryFormat methods, the caller must advance <see cref="Offset"/> by the number of bytes written.
        /// </summary>
        /// <returns>A span over the unused part of the buffer (invalid after the buffer grows)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Byte> AsSpan()
        {
            var o = Offset;
            return new Span<byte>(DataPtr + o, S - o);
        }

        /// <summary>
        /// The capacity (see <see cref="Capacity"/>)
        /// </summary>
        int S;

        /// <summary>
        /// The number of bytes that can be written (from the start of the buffer) without growing.
        /// Can be less than the length of the buffer (see <see cref="Pooled"/>).
        /// </summary>
        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => S;
        }

        /// <summary>
        /// Get the current buffer (may be longer than the data written, see <see cref="Position"/>).
        /// For a pooled writer the buffer may be rented and is returned to the pool when the writer is disposed, use <see cref="DetachBuffer"/> to keep it.
        /// </summary>
        /// <returns>The current buffer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Byte[] GetBuffer() => Data;

        /// <summary>
        /// The current write position, same as <see cref="Offset"/>
        /// </summary>
        public int Position => Offset;


        /// <summary>
        /// DEBUG builds only: throws if less than <paramref name="size"/> bytes are available at the current position
        /// </summary>
        /// <param name="size">The number of bytes about to be written</param>
        /// <exception cref="Exception">Not enough space was ensured</exception>
        [Conditional("DEBUG")]
        void Validate(int size)
        {
            var o = Offset;
            var end = o + size;
            if (end > S)
                throw new Exception("Not enough data enured before write!");
        }

        /// <summary>
        /// Below this capacity the buffer grows linearly (needed size + 4 KB), above it the capacity is doubled.
        /// Linear growth is faster for small buffers (no large object heap allocations), doubling avoids O(n^2) copying for big ones.
        /// </summary>
        const int LinearGrowthLimit = 64 * 1024;

        /// <summary>
        /// The minimum margin added to the capacity when a pooled writer grows
        /// </summary>
        const int PooledMinMargin = 256;

        /// <summary>
        /// Grow the buffer so that at least <paramref name="end"/> bytes (from the start) can be written, keeping the data written so far
        /// </summary>
        /// <param name="end">The required capacity</param>
        /// <exception cref="OutOfMemoryException"><paramref name="end"/> is larger than <see cref="Array.MaxLength"/></exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        void Grow(long end)
        {
            if (end > Array.MaxLength)
                throw new OutOfMemoryException("Can't grow the buffer to " + end + " bytes, the max size is " + Array.MaxLength + " bytes");
            if (Pooled)
            {
                GrowPooled(end);
                return;
            }
            long size = end + 4096;
            var s = S;
            if (s >= LinearGrowthLimit)
                size = Math.Max(size, (long)s << 1);
            size = (size + 4095) & ~4095L;
            if (size > Array.MaxLength)
                size = Array.MaxLength;
            var b = GC.AllocateUninitializedArray<Byte>((int)size);
            var o = Offset;
            if (o > 0)
                Data.AsSpan<Byte>().Slice(0, o).CopyTo(b.AsSpan<Byte>().Slice(0, o));
            Data = b;
            if (PinHandle.IsAllocated)
                PinHandle.Free();
            PinHandle = GCHandle.Alloc(b, GCHandleType.Pinned);
            DataPtr = (Byte*)PinHandle.AddrOfPinnedObject().ToPointer();
            S = (int)size;
        }

        /// <summary>
        /// Grow the capacity to the needed size + 1/16 (at least 256 bytes), only replace the buffer if it's too small (doubling the length, using a rented buffer)
        /// </summary>
        void GrowPooled(long end)
        {
            long size = Math.Min(end + Math.Max(PooledMinMargin, end >> 4), Array.MaxLength);
            var old = Data;
            var length = old.Length;
            if (size > length)
            {
                var b = ArrayPoolStream.Rent((int)Math.Min(Math.Max(size, (long)length << 1), Array.MaxLength));
                var o = Offset;
                if (o > 0)
                    new ReadOnlySpan<Byte>(DataPtr, o).CopyTo(b);
                if (PinHandle.IsAllocated)
                    PinHandle.Free();
                if (Rented)
                    ArrayPoolStream.Return(old);
                Data = b;
                Rented = true;
                PinHandle = GCHandle.Alloc(b, GCHandleType.Pinned);
                DataPtr = (Byte*)PinHandle.AddrOfPinnedObject().ToPointer();
            }
            S = (int)size;
        }


        /// <summary>
        /// Make sure that at least <paramref name="size"/> bytes can be written at the current position, growing the buffer if needed.
        /// </summary>
        /// <remarks>
        /// The buffer (<see cref="Data"/> and <see cref="DataPtr"/>) may be replaced, so pointers and spans into the buffer must be re-read after this call.
        /// The json writers ensure some slack (usually 64 bytes) since many writes store 8 or 16 bytes at a time.
        /// </remarks>
        /// <param name="size">The number of bytes that will be written</param>
        /// <exception cref="OutOfMemoryException">The required size is larger than <see cref="Array.MaxLength"/></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Ensure(int size)
        {
            // Using a long so that Offset + size can't overflow
            var end = (long)Offset + size;
            if (end > S)
                Grow(end);
        }

        /// <summary>
        /// Write a byte at the current position (the space must be ensured)
        /// </summary>
        /// <param name="value">The byte to write</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(Byte value)
        {
            Validate(1);
            var o = Offset;
            DataPtr[o] = value;
            ++o;
            Offset = o;
        }

        /// <summary>
        /// Write two bytes at the current position (the space must be ensured)
        /// </summary>
        /// <param name="a">The first byte</param>
        /// <param name="b">The second byte</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(Byte a, Byte b)
        {
            Validate(2);
            var o = Offset;
            var ptr = DataPtr + o;
            *ptr = a;
            ++ptr;
            *ptr = b;
            Offset = o + 2;
        }

    }


}
