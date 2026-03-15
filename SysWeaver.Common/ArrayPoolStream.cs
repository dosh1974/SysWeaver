using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SysWeaver
{
    public sealed class ArrayPoolStream : Stream
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayPoolStream(int minInitialSize = 4096)
        {
            Data = Rent(minInitialSize <= 0 ? 1 : minInitialSize);
            Owned = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Byte[] Rent(int size)
        {
            var buf = Pool.Rent(size);
#if DEBUG
            Interlocked.Increment(ref RentCount);
            Interlocked.Add(ref RentBytes, buf.Length);
#endif//DEBUG
            return buf;
            //return GC.AllocateUninitializedArray<Byte>(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(Byte[] buf)
        {
#if DEBUG
            Interlocked.Increment(ref ReturnCount);
            Interlocked.Add(ref ReturnBytes, buf.Length);
            Pool.Return(buf, true);
#else//DEBUG
            Pool.Return(buf);
#endif//DEBUG
        }

#if DEBUG
        static long RentCount;
        static long ReturnCount;

        static long RentBytes;
        static long ReturnBytes;
#endif//DEBUG

        static readonly ArrayPool<Byte> Pool = ArrayPool<Byte>.Shared;

        bool Owned;
        /// <summary>
        /// Internal buffer, never set manually
        /// </summary>
        public Byte[] Data;
        int Len;
        int Pos;


        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => Len;

        public override long Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Pos;
            set
            {
                if (value < 0)
                    value = 0;
                var maxP = Len;
                if (value > maxP)
                    value = maxP;
                Pos = maxP;
            }
        }

        public override void Flush()
        {
        }
            

        public override int Read(byte[] buffer, int offset, int count)
        {
            var pos = Pos;
            var maxC = Len - pos;
            if (count > maxC)
                count = (int)maxC;
            Data.AsSpan().Slice(pos, count).CopyTo(buffer.AsSpan().Slice(offset));
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = (int)Math.Min(offset, Len);
                    break;
                case SeekOrigin.End:
                    Position = (int)Math.Max(Len - offset, 0);
                    break;
                case SeekOrigin.Current:
                    Position = (int)Math.Min(Math.Max(Position + offset, 0), Len);
                    break;
            }
            return Position;
        }
        

        public override void SetLength(long value)
        {
            var len = Len;
            if (value > len)
                value = len;
            if (value < 0)
                value = 0;
            Len = (int)value;
        }

        Byte[] Resize(int end)
        {
            var next = Rent(end);
            var len = Len;
            var data = Data;
            if (len > 0)
                data.AsSpan().Slice(0, len).CopyTo(next.AsSpan());
            if (Owned)
                Return(data);
            Owned = true;
            Data = next;
            return next;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var pos = Pos;
            var end = pos + count;
            var data = Data;
            if (end > data.Length)
                data = Resize(end);
            buffer.AsSpan().Slice(offset, count).CopyTo(data.AsSpan(pos, count));
            pos += count;
            if (pos > Len)
                Len = pos;
            Pos = pos;
        }

        /// <summary>
        /// The internal buffer or trimmed array.
        /// Please consider using GetMemory with the using pattern instead.
        /// </summary>
        /// <returns></returns>
        public Byte[] GetBuffer()
        {
            var len = Len;
            if (len <= 0)
                return Array.Empty<Byte>();
            var d = Data;
            var dl = d.Length;
            long waste = dl - len;
            if ((waste > 1024) && ((waste << 3) >= dl)) // Allow approx 1/8th the buffer size of waste to avoid a memory copy
            {
                var t = GC.AllocateUninitializedArray<Byte>(len);
                d.AsSpan().Slice(0, len).CopyTo(t.AsSpan());
                return t;
            }
            Owned = false;
            return d;
        }

        /// <summary>
        /// The internal buffer or trimmed array.
        /// Please consider using GetMemory with the using pattern instead.
        /// </summary>
        /// <returns></returns>
        public Memory<Byte> GetBufferMemory()
        {
            var len = Len;
            if (len <= 0)
                return Array.Empty<Byte>();
            var d = Data;
            var dl = d.Length;
            long waste = dl - len;
            if ((waste > 1024) && ((waste << 3) >= dl)) // Allow approx 1/8th the buffer size of waste to avoid a memory copy
            {
                var t = GC.AllocateUninitializedArray<Byte>(len);
                d.AsSpan().Slice(0, len).CopyTo(t.AsSpan());
                return t;
            }
            Owned = false;
            return Data.AsMemory().Slice(0, len);
        }

        struct S : IUnmanagedReadOnlyMemory<Byte>
        {
            public S(Byte[] buffer, int size)
            {
                Buffer = buffer;
                Memory = new ReadOnlyMemory<byte>(buffer, 0, size);
            }
            Byte[] Buffer;
            public ReadOnlyMemory<Byte> Memory { get; init; }


            public void Dispose()
            {
                var b = Interlocked.Exchange(ref Buffer, null);
                if (b != null)
                    ArrayPoolStream.Return(b);
            }
        }

        public IUnmanagedReadOnlyMemory<Byte> GetMemory()
        {
            var l = Len;
            if (l <= 0)
                return UnmanagedMemory.Empty<Byte>();
            Owned = false;
            return new S(Data, Pos);
        }

        public Byte[] ToArray()
        {
            var d = Data;
            var len = Len;
            if (len <= 0)
                return Array.Empty<Byte>(); 
            if (len == d.Length)
            {
                Owned = false;
                return d;
            }
            var t = GC.AllocateUninitializedArray<Byte>(len);
            d.AsSpan().Slice(0, len).CopyTo(t.AsSpan());
            return t;
        }


        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (Owned)
            {
                Return(Data);
                Data = null;
            }
        }
        
    }






}
