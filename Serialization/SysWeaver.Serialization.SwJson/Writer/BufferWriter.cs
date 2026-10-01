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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (PinHandle.IsAllocated)
                PinHandle.Free();
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        void Grow(long end)
        {
            if (end > Array.MaxLength)
                throw new OutOfMemoryException("Can't grow the buffer to " + end + " bytes, the max size is " + Array.MaxLength + " bytes");
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
