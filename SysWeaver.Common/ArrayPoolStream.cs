using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    public sealed class ArrayPoolStream : Stream
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayPoolStream(int minInitialSize = 4096)
        {
            Data = Rent(minInitialSize <= 0 ? 1 : minInitialSize);
            State = BufferState.Owned;
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

        enum BufferState
        {
            /// <summary>
            /// The buffer is owned by this stream, and will be returned to the pool when replaced or disposed
            /// </summary>
            Owned,
            /// <summary>
            /// The buffer have been handed out as a managed array (GetBuffer, GetBufferMemory, ToArray), it will never be returned to the pool.
            /// It's safe to hand out again, but must not be modified (copy on write).
            /// </summary>
            Released,
            /// <summary>
            /// The buffer have been handed out by GetMemory, the receiver will return it to the pool.
            /// It must not be handed out again, and must not be modified (copy on write).
            /// </summary>
            Lent,
        }

        BufferState State;

        /// <summary>
        /// Internal buffer, never set manually.
        /// Only the first Length bytes are valid, the buffer must not be modified.
        /// </summary>
        public Byte[] Data;
        int Len;
        int Pos;


        public override bool CanRead => Data != null;

        public override bool CanSeek => Data != null;

        public override bool CanWrite => Data != null;

        public override long Length => Len;

        public override long Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Pos;
            set
            {
                ThrowIfDisposed();
                if (value < 0)
                    value = 0;
                var maxP = Len;
                if (value > maxP)
                    value = maxP;
                Pos = (int)value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ThrowIfDisposed()
        {
            if (Data == null)
                ThrowDisposed();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void ThrowDisposed() => throw new ObjectDisposedException(nameof(ArrayPoolStream));

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
            => cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;

        #region Read

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return Read(new Span<Byte>(buffer, offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            ThrowIfDisposed();
            var pos = Pos;
            var count = Len - pos;
            if (count > buffer.Length)
                count = buffer.Length;
            if (count <= 0)
                return 0;
            new ReadOnlySpan<Byte>(Data, pos, count).CopyTo(buffer);
            Pos = pos + count;
            return count;
        }

        public override int ReadByte()
        {
            ThrowIfDisposed();
            var pos = Pos;
            if (pos >= Len)
                return -1;
            Pos = pos + 1;
            return Data[pos];
        }

        /// <summary>
        /// Cache the last completed read task to avoid allocations when reading the same size repeatedly
        /// </summary>
        Task<int> LastReadTask;

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<int>(cancellationToken);
            try
            {
                var n = Read(new Span<Byte>(buffer, offset, count));
                var t = LastReadTask;
                if ((t != null) && (t.Result == n))
                    return t;
                return LastReadTask = Task.FromResult(n);
            }
            catch (Exception ex)
            {
                return Task.FromException<int>(ex);
            }
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return ValueTask.FromCanceled<int>(cancellationToken);
            try
            {
                return new ValueTask<int>(Read(buffer.Span));
            }
            catch (Exception ex)
            {
                return ValueTask.FromException<int>(ex);
            }
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            ValidateCopyToArguments(destination, bufferSize);
            ThrowIfDisposed();
            var pos = Pos;
            var count = Len - pos;
            if (count <= 0)
                return;
            Pos = pos + count;
            destination.Write(Data, pos, count);
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            ValidateCopyToArguments(destination, bufferSize);
            ThrowIfDisposed();
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);
            var pos = Pos;
            var count = Len - pos;
            if (count <= 0)
                return Task.CompletedTask;
            Pos = pos + count;
            return destination.WriteAsync(new ReadOnlyMemory<Byte>(Data, pos, count), cancellationToken).AsTask();
        }

        #endregion//Read

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            long basePos = origin switch
            {
                SeekOrigin.Begin => 0,
                SeekOrigin.Current => Pos,
                SeekOrigin.End => Len,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            //  Clamp to [0, Length] (without overflowing)
            long p;
            if (offset <= -basePos)
                p = 0;
            else if (offset >= Len - basePos)
                p = Len;
            else
                p = basePos + offset;
            Pos = (int)p;
            return p;
        }

        public override void SetLength(long value)
        {
            ThrowIfDisposed();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, Array.MaxLength);
            var newLen = (int)value;
            var len = Len;
            if (newLen > len)
            {
                //  Growing, new bytes must be zero
                EnsureWritable(newLen);
                Data.AsSpan(len, newLen - len).Clear();
            }
            Len = newLen;
            if (Pos > newLen)
                Pos = newLen;
        }

        #region Write

        /// <summary>
        /// Make sure that the buffer can hold at least end bytes, and that it may be modified
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void EnsureWritable(int end)
        {
            var data = Data;
            if ((end > data.Length) || (State != BufferState.Owned))
                Resize(end);
        }

        void Resize(int end)
        {
            var data = Data;
            //  Grow geometrically, the pool rounds up to powers of 2 anyway, but not for very large buffers
            var size = (int)Math.Min(Math.Max(end, (long)data.Length << 1), Array.MaxLength);
            if (size < end)
                size = end;
            var next = Rent(size);
            var len = Len;
            if (len > 0)
                new ReadOnlySpan<Byte>(data, 0, len).CopyTo(next.AsSpan());
            if (State == BufferState.Owned)
                Return(data);
            State = BufferState.Owned;
            Data = next;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            Write(new ReadOnlySpan<Byte>(buffer, offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowIfDisposed();
            var count = buffer.Length;
            if (count <= 0)
                return;
            var pos = Pos;
            var end = (long)pos + count;
            if (end > Array.MaxLength)
                throw new IOException("Stream too long");
            var iend = (int)end;
            EnsureWritable(iend);
            buffer.CopyTo(Data.AsSpan(pos, count));
            if (iend > Len)
                Len = iend;
            Pos = iend;
        }

        public override void WriteByte(byte value)
        {
            ThrowIfDisposed();
            var pos = Pos;
            var end = pos + 1;
            if (end > Array.MaxLength)
                throw new IOException("Stream too long");
            EnsureWritable(end);
            Data[pos] = value;
            if (end > Len)
                Len = end;
            Pos = end;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);
            try
            {
                Write(new ReadOnlySpan<Byte>(buffer, offset, count));
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return ValueTask.FromCanceled(cancellationToken);
            try
            {
                Write(buffer.Span);
                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
            }
        }

        #endregion//Write

        #region Get data

        /// <summary>
        /// Check if the buffer can be handed out as a managed array, else a copy must be made
        /// </summary>
        /// <returns>True if the buffer may be handed out</returns>
        bool TryRelease()
        {
            if (State == BufferState.Lent)
                return false;
            State = BufferState.Released;
            return true;
        }

        /// <summary>
        /// The internal buffer or trimmed array.
        /// Please consider using GetMemory with the using pattern instead.
        /// </summary>
        /// <returns></returns>
        public Byte[] GetBuffer()
        {
            ThrowIfDisposed();
            var len = Len;
            if (len <= 0)
                return Array.Empty<Byte>();
            var d = Data;
            var dl = d.Length;
            long waste = dl - len;
            if (((waste > 1024) && ((waste << 3) >= dl)) || (!TryRelease())) // Allow approx 1/8th the buffer size of waste to avoid a memory copy
            {
                var t = GC.AllocateUninitializedArray<Byte>(len);
                d.AsSpan().Slice(0, len).CopyTo(t.AsSpan());
                return t;
            }
            return d;
        }

        /// <summary>
        /// The internal buffer or trimmed array.
        /// Please consider using GetMemory with the using pattern instead.
        /// </summary>
        /// <param name="trim">If true, a trimmed copy is returned if the internal buffer is a lot larger than the data</param>
        /// <returns></returns>
        public Memory<Byte> GetBufferMemory(bool trim = true)
        {
            ThrowIfDisposed();
            var len = Len;
            if (len <= 0)
                return Array.Empty<Byte>();
            var d = Data;
            var dl = d.Length;
            long waste = dl - len;
            if ((trim && (waste > 1024) && ((waste << 3) >= dl)) || (!TryRelease())) // Allow approx 1/8th the buffer size of waste to avoid a memory copy
            {
                var t = GC.AllocateUninitializedArray<Byte>(len);
                d.AsSpan().Slice(0, len).CopyTo(t.AsSpan());
                return t;
            }
            return d.AsMemory().Slice(0, len);
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

        /// <summary>
        /// Get the data as memory, dispose the returned object when done to return the buffer to the pool.
        /// The stream may still be read until the returned object is disposed, writes are safe (the buffer is copied).
        /// </summary>
        /// <returns></returns>
        public IUnmanagedReadOnlyMemory<Byte> GetMemory()
        {
            ThrowIfDisposed();
            var l = Len;
            if (l <= 0)
                return UnmanagedMemory.Empty<Byte>();
            var d = Data;
            if (State != BufferState.Owned)
            {
                //  Someone else already have the buffer, hand out a copy
                var c = Rent(l);
                new ReadOnlySpan<Byte>(d, 0, l).CopyTo(c.AsSpan());
                return new S(c, l);
            }
            State = BufferState.Lent;
            return new S(d, l);
        }

        public Byte[] ToArray()
        {
            ThrowIfDisposed();
            var d = Data;
            var len = Len;
            if (len <= 0)
                return Array.Empty<Byte>();
            if ((len == d.Length) && TryRelease())
                return d;
            var t = GC.AllocateUninitializedArray<Byte>(len);
            d.AsSpan().Slice(0, len).CopyTo(t.AsSpan());
            return t;
        }

        #endregion//Get data

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            var d = Data;
            if (d == null)
                return;
            Data = null;
            if (State == BufferState.Owned)
                Return(d);
            State = BufferState.Released;
        }

    }



}
