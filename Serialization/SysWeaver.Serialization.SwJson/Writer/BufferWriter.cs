using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver.Serialization.SwJson.Writer
{

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
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

        void Grow(int end)
        {
            end += (4096 + 4095);
            end &= ~4095;
            var b = GC.AllocateUninitializedArray<Byte>(end);
            var o = Offset;
            if (o > 0)
                Data.AsSpan<Byte>().Slice(0, o).CopyTo(b.AsSpan<Byte>().Slice(0, o));
            Data = b;
            PinHandle.Free();
            PinHandle = GCHandle.Alloc(b, GCHandleType.Pinned);
            DataPtr = (Byte*)PinHandle.AddrOfPinnedObject().ToPointer();
            S = end;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Ensure(int size)
        {
            var end = Offset + size;
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
