using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver.Serialization.SwJson.Writer
{

    unsafe public sealed class BufferWriter : IDisposable
    {
        public static readonly Type Type = typeof(BufferWriter);

        public bool TypeIsOptional = false;

        public BufferWriter(Byte[] initData, int startOffset = 0)
        {
            var d = initData ?? GC.AllocateUninitializedArray<Byte>(4096);
            Data = d;
            PinHandle = GCHandle.Alloc(d, GCHandleType.Pinned);
            DataPtr = (Byte*)PinHandle.AddrOfPinnedObject().ToPointer();
            S = d.Length;
            Offset = startOffset;
        }

        public readonly Memory<Char> Temp = GC.AllocateUninitializedArray<Char>(128);

        public void WriteCharTempAsAscci(int count)
        {
            var t = Temp.Span;
            var o = Offset;
            var p = DataPtr + o;
            for (int i = 0; i < count; ++ i)
            {
                var c = (Byte)t[i];
                *p = c;
                ++p;
            }
            Offset = o + count;
        }
        public void WriteCharTempAsAscii(int count, Byte quote)
        {
            var t = Temp.Span;
            var o = Offset;
            var p = DataPtr + o;
            *p = quote;
            ++p;
            for (int i = 0; i < count; ++i)
            {
                var c = (Byte)t[i];
                *p = c;
                ++p;
            }
            *p = quote;
            Offset = o + count + 2;
        }

        public void Dispose()
        {
            PinHandle.Free();
            Data = null;
            GC.SuppressFinalize(this);
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
