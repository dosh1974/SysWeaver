using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// A seekable, readable and writable in-memory stream (like MemoryStream) that uses buffers rented from the shared ArrayPool.
    /// Dispose the stream when done to return the buffer to the pool.
    /// The data can be retrieved without a copy using GetMemory (pooled, dispose the result), GetBuffer, GetBufferMemory or ToArray (the buffer is handed out and never returned to the pool).
    /// </summary>
    /// <remarks>
    /// Differences from MemoryStream: Position and Seek are clamped to [0, Length] (no exception, and no gap can be created).
    /// Buffers that have been handed out are never modified (copy on write).
    /// The stream is not thread safe.
    /// </remarks>
    public sealed class ArrayPoolStream : Stream
    {
        /// <summary>
        /// Create an empty stream.
        /// </summary>
        /// <param name="minInitialSize">The minimum initial capacity in bytes, values less than 1 are treated as 1</param>
        /// <exception cref="OutOfMemoryException">The initial size is too large</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayPoolStream(int minInitialSize = 4096)
        {
            Data = Rent(minInitialSize <= 0 ? 1 : minInitialSize);
            State = BufferState.Owned;
        }

        /// <summary>
        /// Rent a byte buffer from the pool used by all ArrayPoolStream's (the shared ArrayPool).
        /// Return it using <see cref="Return"/> when done.
        /// </summary>
        /// <param name="size">The minimum size of the buffer in bytes</param>
        /// <returns>A buffer with at least size bytes (the content is undefined), may be an empty array for a size of 0</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative</exception>
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

        /// <summary>
        /// Return a buffer that was rented using <see cref="Rent"/> to the pool.
        /// The buffer must not be used after it's returned (and must not be returned twice).
        /// </summary>
        /// <param name="buf">The buffer to return</param>
        /// <exception cref="ArgumentNullException"><paramref name="buf"/> is null</exception>
        /// <exception cref="ArgumentException"><paramref name="buf"/> is not from the pool (has a size that the pool doesn't use)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(Byte[] buf)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(buf);
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

        /// <summary>
        /// The ownership state of <see cref="Data"/>
        /// </summary>
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
            /// The buffer have been handed out by GetMemory, it's shared by the stream and the receiver (see <see cref="CurrentLease"/>),
            /// and returned to the pool when both are done with it.
            /// It must not be handed out again, and must not be modified (copy on write).
            /// </summary>
            Lent,
        }

        BufferState State;

        /// <summary>
        /// The lease of the buffer when the state is Lent (the stream holds one reference), else null
        /// </summary>
        Lease CurrentLease;

        /// <summary>
        /// Release the stream's reference to a lent buffer (the buffer is returned to the pool when the receiver is done too)
        /// </summary>
        void ReleaseLease()
        {
            var l = CurrentLease;
            CurrentLease = null;
            l?.Release();
        }

        /// <summary>
        /// Internal buffer, never set manually.
        /// Only the first Length bytes are valid, the buffer must not be modified.
        /// Null when the stream is disposed.
        /// </summary>
        public Byte[] Data;
        int Len;
        int Pos;


        /// <summary>
        /// True until the stream is disposed
        /// </summary>
        public override bool CanRead => Data != null;

        /// <summary>
        /// True until the stream is disposed
        /// </summary>
        public override bool CanSeek => Data != null;

        /// <summary>
        /// True until the stream is disposed
        /// </summary>
        public override bool CanWrite => Data != null;

        /// <summary>
        /// The number of bytes in the stream (also valid after the stream is disposed)
        /// </summary>
        public override long Length => Len;

        /// <summary>
        /// The current position in the stream.
        /// Setting the position clamps the value to [0, Length] (no exception is thrown for out of range values, unlike MemoryStream).
        /// </summary>
        /// <exception cref="ObjectDisposedException">The position is set and the stream is disposed</exception>
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

        /// <summary>
        /// Does nothing (all data is in memory)
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>
        /// Does nothing (all data is in memory)
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A completed task (or a cancelled task if the token is cancelled)</returns>
        public override Task FlushAsync(CancellationToken cancellationToken)
            => cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;

        #region Read

        /// <summary>
        /// Read bytes from the current position (and advance the position).
        /// </summary>
        /// <param name="buffer">The buffer to read into</param>
        /// <param name="offset">The offset in the buffer to write the first byte to</param>
        /// <param name="count">The max number of bytes to read</param>
        /// <returns>The number of bytes read, 0 at the end of the stream</returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="count"/> is negative, or the range is outside of the buffer</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return Read(new Span<Byte>(buffer, offset, count));
        }

        /// <summary>
        /// Read bytes from the current position (and advance the position).
        /// </summary>
        /// <param name="buffer">The buffer to read into</param>
        /// <returns>The number of bytes read, 0 at the end of the stream (or if the buffer is empty)</returns>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
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

        /// <summary>
        /// Read a byte from the current position (and advance the position).
        /// </summary>
        /// <returns>The byte (0 - 255) or -1 at the end of the stream</returns>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
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

        /// <summary>
        /// Read bytes from the current position (and advance the position), completes synchronously.
        /// The returned task is reused if the same number of bytes is read again.
        /// </summary>
        /// <param name="buffer">The buffer to read into</param>
        /// <param name="offset">The offset in the buffer to write the first byte to</param>
        /// <param name="count">The max number of bytes to read</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The number of bytes read, 0 at the end of the stream. A cancelled task if the token is cancelled, a faulted task (ObjectDisposedException) if the stream is disposed</returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="count"/> is negative, or the range is outside of the buffer</exception>
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

        /// <summary>
        /// Read bytes from the current position (and advance the position), completes synchronously.
        /// </summary>
        /// <param name="buffer">The buffer to read into</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The number of bytes read, 0 at the end of the stream. A cancelled task if the token is cancelled, a faulted task (ObjectDisposedException) if the stream is disposed</returns>
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

        /// <summary>
        /// Write the data from the current position to the end of the stream to another stream (in a single write), the position is moved to the end.
        /// </summary>
        /// <param name="destination">The stream to write to</param>
        /// <param name="bufferSize">Not used (must be positive)</param>
        /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is not positive</exception>
        /// <exception cref="NotSupportedException"><paramref name="destination"/> doesn't support writing</exception>
        /// <exception cref="ObjectDisposedException">This stream or the destination is disposed</exception>
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

        /// <summary>
        /// Write the data from the current position to the end of the stream to another stream (in a single write), the position is moved to the end.
        /// This stream must not be modified or disposed until the returned task completes.
        /// </summary>
        /// <param name="destination">The stream to write to</param>
        /// <param name="bufferSize">Not used (must be positive)</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The write task of the destination</returns>
        /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is not positive</exception>
        /// <exception cref="NotSupportedException"><paramref name="destination"/> doesn't support writing</exception>
        /// <exception cref="ObjectDisposedException">This stream or the destination is disposed</exception>
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

        /// <summary>
        /// Set the position, the new position is clamped to [0, Length] (no exception is thrown for out of range values, unlike MemoryStream).
        /// </summary>
        /// <param name="offset">The offset relative to the origin</param>
        /// <param name="origin">The origin of the offset</param>
        /// <returns>The new position</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="origin"/> is not a valid SeekOrigin</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
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

        /// <summary>
        /// Set the length of the stream, new bytes are zero. The position is clamped to the new length.
        /// </summary>
        /// <param name="value">The new length</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative or larger than Array.MaxLength</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
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
            else if (State == BufferState.Lent)
                ReleaseLease();
            State = BufferState.Owned;
            Data = next;
        }

        /// <summary>
        /// Write bytes at the current position (and advance the position), the stream grows as needed.
        /// </summary>
        /// <param name="buffer">The buffer to write from</param>
        /// <param name="offset">The offset of the first byte in the buffer to write</param>
        /// <param name="count">The number of bytes to write</param>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="count"/> is negative, or the range is outside of the buffer</exception>
        /// <exception cref="IOException">The stream would be longer than Array.MaxLength</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            Write(new ReadOnlySpan<Byte>(buffer, offset, count));
        }

        /// <summary>
        /// Write bytes at the current position (and advance the position), the stream grows as needed.
        /// </summary>
        /// <param name="buffer">The bytes to write</param>
        /// <exception cref="IOException">The stream would be longer than Array.MaxLength</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
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

        /// <summary>
        /// Write a byte at the current position (and advance the position), the stream grows as needed.
        /// </summary>
        /// <param name="value">The byte to write</param>
        /// <exception cref="IOException">The stream would be longer than Array.MaxLength</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
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

        /// <summary>
        /// Write bytes at the current position (and advance the position), completes synchronously.
        /// </summary>
        /// <param name="buffer">The buffer to write from</param>
        /// <param name="offset">The offset of the first byte in the buffer to write</param>
        /// <param name="count">The number of bytes to write</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A completed task. A cancelled task if the token is cancelled, a faulted task (ObjectDisposedException, IOException) if the write failed</returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="count"/> is negative, or the range is outside of the buffer</exception>
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

        /// <summary>
        /// Write bytes at the current position (and advance the position), completes synchronously.
        /// </summary>
        /// <param name="buffer">The bytes to write</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A completed task. A cancelled task if the token is cancelled, a faulted task (ObjectDisposedException, IOException) if the write failed</returns>
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
        /// <returns>
        /// An array where the first Length bytes are the data (the array may be longer, a trimmed copy is returned if the buffer is a lot larger than the data).
        /// If the internal buffer is returned it's never returned to the pool or modified by the stream (later writes copies it).
        /// An empty array if the stream is empty.
        /// </returns>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
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
        /// <returns>The data (Length bytes), the memory is never returned to the pool or modified by the stream (later writes copies it)</returns>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
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

        /// <summary>
        /// Memory handed out by GetMemory, the buffer is returned to the pool when all references are released.
        /// A lent stream buffer has two references (the stream and the receiver), so it stays valid for the stream after the receiver disposes it.
        /// </summary>
        sealed class Lease : IUnmanagedReadOnlyMemory<Byte>
        {
            /// <summary>
            /// Create a lease of a pooled buffer
            /// </summary>
            /// <param name="buffer">The pooled buffer (ownership is transferred to the lease)</param>
            /// <param name="size">The number of valid bytes</param>
            /// <param name="references">The initial number of references (1 or 2)</param>
            public Lease(Byte[] buffer, int size, int references)
            {
                Buffer = buffer;
                Memory = new ReadOnlyMemory<byte>(buffer, 0, size);
                References = references;
            }
            readonly Byte[] Buffer;
            int References;
            int Disposed;

            /// <summary>
            /// The valid bytes of the leased buffer (must not be used after the lease is disposed)
            /// </summary>
            public ReadOnlyMemory<Byte> Memory { get; }

            /// <summary>
            /// Release one reference, the last one returns the buffer to the pool
            /// </summary>
            public void Release()
            {
                if (Interlocked.Decrement(ref References) == 0)
                    ArrayPoolStream.Return(Buffer);
            }

            /// <summary>
            /// Release the receiver's reference (only once)
            /// </summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref Disposed, 1) == 0)
                    Release();
            }
        }

        /// <summary>
        /// Get the data as memory, dispose the returned object when done to return the buffer to the pool.
        /// The stream can still be used (also after the returned object is disposed), writes are safe (the buffer is copied).
        /// </summary>
        /// <returns>The data (Length bytes), dispose it when done (the memory must not be used after that). An empty instance if the stream is empty</returns>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
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
                return new Lease(c, l, 1);
            }
            //  Shared by the stream and the receiver
            var lease = new Lease(d, l, 2);
            CurrentLease = lease;
            State = BufferState.Lent;
            return lease;
        }

        /// <summary>
        /// Get the data as an array.
        /// </summary>
        /// <returns>
        /// An array with exactly Length bytes (the internal buffer if it has the exact size, it's then never returned to the pool or modified by the stream).
        /// An empty array if the stream is empty.
        /// </returns>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
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

        /// <summary>
        /// Wrap a buffer rented with <see cref="Rent"/> as memory that returns the buffer to the pool when disposed.
        /// The ownership of the buffer is transferred to the returned object.
        /// </summary>
        /// <param name="pooled">A buffer rented using <see cref="Rent"/></param>
        /// <param name="length">The number of valid bytes in the buffer</param>
        /// <returns>The memory (an empty instance if the length is 0, the buffer is then returned immediately)</returns>
        internal static IUnmanagedReadOnlyMemory<Byte> Lend(Byte[] pooled, int length)
        {
            if (length <= 0)
            {
                Return(pooled);
                return UnmanagedMemory.Empty<Byte>();
            }
            return new Lease(pooled, length, 1);
        }

        /// <summary>
        /// Take the pooled buffer from the stream and dispose the stream.
        /// The caller must return the buffer with <see cref="Return"/>.
        /// </summary>
        /// <param name="length">The number of valid bytes in the buffer (the Length of the stream)</param>
        /// <returns>A buffer rented using <see cref="Rent"/></returns>
        /// <exception cref="ObjectDisposedException">The stream is disposed</exception>
        internal Byte[] Detach(out int length)
        {
            ThrowIfDisposed();
            var l = Len;
            length = l;
            var d = Data;
            if (State != BufferState.Owned)
            {
                //  Someone else have the buffer, copy it
                var c = Rent(l);
                new ReadOnlySpan<Byte>(d, 0, l).CopyTo(c.AsSpan());
                Dispose();
                return c;
            }
            Data = null;
            State = BufferState.Released;
            Dispose();
            return d;
        }

        #endregion//Get data

        /// <summary>
        /// Release the buffer (it's returned to the pool unless it's handed out).
        /// Length and the Position getter are still valid after the stream is disposed, CanRead / CanSeek / CanWrite returns false,
        /// Flush does nothing and the other members throws an ObjectDisposedException.
        /// </summary>
        /// <param name="disposing">True if called from Dispose</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            var d = Data;
            if (d == null)
                return;
            Data = null;
            if (State == BufferState.Owned)
                Return(d);
            else if (State == BufferState.Lent)
                ReleaseLease();
            State = BufferState.Released;
        }

    }



}
