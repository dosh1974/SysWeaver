using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver.Serialization.SwJson.Writer
{

    [SkipLocalsInit]
    unsafe public ref struct BufferWriter : IDisposable
    {
        public bool TypeIsOptional = false;

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

        public GCHandle PinHandle;
        public Byte* DataPtr;

        public Byte[] Data;
        public int Offset;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Byte> AsSpan()
        {
            var o = Offset;
            return new Span<byte>(DataPtr + o, S - o);
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Byte[] GetBuffer() => Data;

        public int Position => Offset;


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


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Ensure(int size)
        {
            // Using a long so that Offset + size can't overflow
            var end = (long)Offset + size;
            if (end > S)
                Grow(end);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(Byte value)
        {
            Validate(1);
            var o = Offset;
            DataPtr[o] = value;
            ++o;
            Offset = o;
        }

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
