using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Compression
{
    /// <summary>
    /// Helpers for compression implementations that are built on top of compression / decompression streams
    /// (copying in large chunks, reading into memory and pooled streams over pinned memory).
    /// </summary>
    public static class CompStreamHelpers
    {
        /// <summary>
        /// The number of bytes to read before writing to an encoder stream
        /// </summary>
        const int CopyChunkSize = 1 << 16;

        /// <summary>
        /// Copy all data from a stream to an encoder stream.
        /// The data is written in large chunks even if the source stream returns a few bytes per read (like a network stream),
        /// some encoders (like brotli at low qualities) compress each write separately and produces a lot more data when fed small chunks.
        /// </summary>
        /// <param name="from">The stream to read from</param>
        /// <param name="to">The stream to write to</param>
        /// <remarks>In-memory sources (<see cref="MemoryStream"/>, <see cref="UnmanagedMemoryStream"/>, <see cref="PointerReadStream"/>) are copied using <see cref="Stream.CopyTo(Stream)"/>, since they write everything at once.</remarks>
        public static void CopyFull(Stream from, Stream to)
        {
            if (IsInMemory(from))
            {
                from.CopyTo(to);
                return;
            }
            var buf = ArrayPoolStream.Rent(CopyChunkSize);
            try
            {
                int read;
                while ((read = from.ReadAtLeast(buf.AsSpan(0, CopyChunkSize), CopyChunkSize, false)) > 0)
                    to.Write(buf, 0, read);
            }
            finally
            {
                ArrayPoolStream.Return(buf);
            }
        }

        /// <summary>
        /// Copy all data from a stream to an encoder stream.
        /// The data is written in large chunks even if the source stream returns a few bytes per read (like a network stream),
        /// some encoders (like brotli at low qualities) compress each write separately and produces a lot more data when fed small chunks.
        /// </summary>
        /// <param name="from">The stream to read from</param>
        /// <param name="to">The stream to write to</param>
        /// <remarks>In-memory sources (<see cref="MemoryStream"/>, <see cref="UnmanagedMemoryStream"/>, <see cref="PointerReadStream"/>) are copied using <see cref="Stream.CopyToAsync(Stream)"/>, since they write everything at once.</remarks>
        public static async ValueTask CopyFullAsync(Stream from, Stream to)
        {
            if (IsInMemory(from))
            {
                await from.CopyToAsync(to).ConfigureAwait(false);
                return;
            }
            var buf = ArrayPoolStream.Rent(CopyChunkSize);
            try
            {
                int read;
                while ((read = await from.ReadAtLeastAsync(buf.AsMemory(0, CopyChunkSize), CopyChunkSize, false).ConfigureAwait(false)) > 0)
                    await to.WriteAsync(buf.AsMemory(0, read)).ConfigureAwait(false);
            }
            finally
            {
                ArrayPoolStream.Return(buf);
            }
        }

        /// <summary>
        /// Memory streams always return all requested data, and their CopyTo writes all data at once
        /// </summary>
        static bool IsInMemory(Stream s) => s is MemoryStream or UnmanagedMemoryStream or PointerReadStream;

        /// <summary>
        /// Read all data from a decoder stream into memory.
        /// When the destination is full, a single byte is read to verify that the stream has ended.
        /// </summary>
        /// <param name="from">The decoder stream</param>
        /// <param name="to">The memory to write the data to</param>
        /// <returns>The number of bytes written</returns>
        /// <exception cref="ArgumentException">The data doesn't fit in the destination</exception>
        public static int ReadAll(Stream from, Span<Byte> to)
        {
            int pos = 0;
            for (; ; )
            {
                if (pos == to.Length)
                {
                    Span<Byte> probe = stackalloc Byte[1];
                    if (from.Read(probe) > 0)
                        throw new ArgumentException(CompHelpers.DecDestTooSmall, nameof(to));
                    return pos;
                }
                var read = from.Read(to.Slice(pos));
                if (read <= 0)
                    return pos;
                pos += read;
            }
        }

        /// <summary>
        /// Read all data from a decoder stream into memory asynchronously.
        /// When the destination is full, a single byte is read to verify that the stream has ended.
        /// </summary>
        /// <param name="from">The decoder stream</param>
        /// <param name="to">The memory to write the data to</param>
        /// <returns>The number of bytes written</returns>
        /// <exception cref="ArgumentException">The data doesn't fit in the destination</exception>
        public static async ValueTask<int> ReadAllAsync(Stream from, Memory<Byte> to)
        {
            int pos = 0;
            for (; ; )
            {
                if (pos == to.Length)
                {
                    var probe = ArrayPoolStream.Rent(1);
                    try
                    {
                        if (await from.ReadAsync(probe.AsMemory(0, 1)).ConfigureAwait(false) > 0)
                            throw new ArgumentException(CompHelpers.DecDestTooSmall, nameof(to));
                    }
                    finally
                    {
                        ArrayPoolStream.Return(probe);
                    }
                    return pos;
                }
                var read = await from.ReadAsync(to.Slice(pos)).ConfigureAwait(false);
                if (read <= 0)
                    return pos;
                pos += read;
            }
        }

        #region Pooled memory streams

        static readonly CompInstancePool<PointerReadStream> Readers = new(16);
        static readonly CompInstancePool<PointerWriteStream> Writers = new(16);

        /// <summary>
        /// Rent a stream that reads from pinned memory (works for empty memory, where the pointer may be null).
        /// Return it using <see cref="Return(PointerReadStream)"/> when done (and not used by anything else).
        /// </summary>
        /// <param name="data">A pointer to the data, must be pinned until the stream is returned</param>
        /// <param name="length">The number of bytes</param>
        /// <returns>A readable stream</returns>
        public static unsafe PointerReadStream RentReader(Byte* data, int length)
        {
            var s = Readers.TryRent() ?? new PointerReadStream();
            s.Init(data, length, default);
            return s;
        }

        /// <summary>
        /// Rent a stream that reads from memory (the memory is pinned until the stream is returned).
        /// Return it using <see cref="Return(PointerReadStream)"/> when done (and not used by anything else).
        /// </summary>
        /// <param name="data">The data</param>
        /// <returns>A readable stream</returns>
        public static unsafe PointerReadStream RentReader(ReadOnlyMemory<Byte> data)
        {
            var s = Readers.TryRent() ?? new PointerReadStream();
            var h = data.Pin();
            s.Init((Byte*)h.Pointer, data.Length, h);
            return s;
        }

        /// <summary>
        /// Return a stream rented using <see cref="RentReader(ReadOnlyMemory{byte})"/> or <see cref="RentReader(byte*, int)"/>, unpinning any memory pinned by the rent.
        /// </summary>
        /// <param name="s">The stream, must not be used after this call</param>
        public static void Return(PointerReadStream s)
        {
            s.Release();
            Readers.Return(s);
        }

        /// <summary>
        /// Rent a stream that writes to pinned memory (works for empty memory, where the pointer may be null).
        /// Writing more data than fits throws an ArgumentException.
        /// Return it using <see cref="Return(PointerWriteStream)"/> when done (and not used by anything else).
        /// </summary>
        /// <param name="data">A pointer to the memory, must be pinned until the stream is returned</param>
        /// <param name="length">The number of bytes available</param>
        /// <returns>A writable stream, use <see cref="PointerWriteStream.Written"/> to get the number of bytes written</returns>
        public static unsafe PointerWriteStream RentWriter(Byte* data, int length)
        {
            var s = Writers.TryRent() ?? new PointerWriteStream();
            s.Init(data, length, default);
            return s;
        }

        /// <summary>
        /// Rent a stream that writes to memory (the memory is pinned until the stream is returned).
        /// Writing more data than fits throws an ArgumentException.
        /// Return it using <see cref="Return(PointerWriteStream)"/> when done (and not used by anything else).
        /// </summary>
        /// <param name="data">The memory to write to</param>
        /// <returns>A writable stream, use <see cref="PointerWriteStream.Written"/> to get the number of bytes written</returns>
        public static unsafe PointerWriteStream RentWriter(Memory<Byte> data)
        {
            var s = Writers.TryRent() ?? new PointerWriteStream();
            var h = data.Pin();
            s.Init((Byte*)h.Pointer, data.Length, h);
            return s;
        }

        /// <summary>
        /// Return a stream rented using <see cref="RentWriter(Memory{byte})"/> or <see cref="RentWriter(byte*, int)"/>, unpinning any memory pinned by the rent.
        /// </summary>
        /// <param name="s">The stream, must not be used after this call</param>
        public static void Return(PointerWriteStream s)
        {
            s.Release();
            Writers.Return(s);
        }

        #endregion//Pooled memory streams
    }


    /// <summary>
    /// A seekable read only stream that reads from pinned memory, rent using <see cref="CompStreamHelpers.RentReader(ReadOnlyMemory{byte})"/>
    /// </summary>
    /// <remarks>
    /// Not thread safe. The instances are pooled, so never keep a reference after returning it using <see cref="CompStreamHelpers.Return(PointerReadStream)"/>.
    /// Disposing the stream has no effect (it must still be returned).
    /// </remarks>
    public sealed unsafe class PointerReadStream : Stream
    {
        internal PointerReadStream()
        {
        }

        Byte* Data;
        int Len;
        int Pos;
        MemoryHandle Handle;

        /// <summary>
        /// Start reading from some memory
        /// </summary>
        /// <param name="data">The data, must be pinned until <see cref="Release"/> is called</param>
        /// <param name="length">The number of bytes</param>
        /// <param name="handle">A pin handle that is disposed when the stream is released (or default)</param>
        internal void Init(Byte* data, int length, MemoryHandle handle)
        {
            Data = data;
            Len = length;
            Pos = 0;
            Handle = handle;
        }

        /// <summary>
        /// Stop using the memory, disposing the pin handle
        /// </summary>
        internal void Release()
        {
            Handle.Dispose();
            Handle = default;
            Data = null;
            Len = 0;
            Pos = 0;
        }

        /// <inheritdoc/>
        public override bool CanRead => true;
        /// <inheritdoc/>
        public override bool CanSeek => true;
        /// <inheritdoc/>
        public override bool CanWrite => false;
        /// <inheritdoc/>
        public override long Length => Len;

        /// <inheritdoc/>
        public override long Position
        {
            get => Pos;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                Pos = (int)Math.Min(value, Len);
            }
        }

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            var p = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Pos + offset,
                _ => Len + offset,
            };
            Position = p;
            return Pos;
        }

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <inheritdoc/>
        public override void Write(Byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override int Read(Byte[] buffer, int offset, int count) => Read(new Span<Byte>(buffer, offset, count));

        /// <inheritdoc/>
        public override int Read(Span<Byte> buffer)
        {
            var n = Math.Min(buffer.Length, Len - Pos);
            if (n <= 0)
                return 0;
            new ReadOnlySpan<Byte>(Data + Pos, n).CopyTo(buffer);
            Pos += n;
            return n;
        }

        /// <inheritdoc/>
        public override int ReadByte() => Pos < Len ? Data[Pos++] : -1;

        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<Byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Read(buffer.Span));

        /// <inheritdoc/>
        public override Task<int> ReadAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromResult(Read(new Span<Byte>(buffer, offset, count)));

        /// <inheritdoc/>
        public override void CopyTo(Stream destination, int bufferSize)
        {
            var n = Len - Pos;
            if (n <= 0)
                return;
            destination.Write(new ReadOnlySpan<Byte>(Data + Pos, n));
            Pos = Len;
        }
    }


    /// <summary>
    /// A write only stream that writes to pinned memory, writing more data than fits throws an ArgumentException.
    /// Rent using <see cref="CompStreamHelpers.RentWriter(Memory{byte})"/>.
    /// </summary>
    /// <remarks>
    /// Not thread safe. The instances are pooled, so never keep a reference after returning it using <see cref="CompStreamHelpers.Return(PointerWriteStream)"/>.
    /// Disposing the stream has no effect (it must still be returned).
    /// A write that doesn't fit throws without writing anything.
    /// </remarks>
    public sealed unsafe class PointerWriteStream : Stream
    {
        internal PointerWriteStream()
        {
        }

        Byte* Data;
        int Len;
        int Pos;
        MemoryHandle Handle;

        /// <summary>
        /// Start writing to some memory
        /// </summary>
        /// <param name="data">The memory, must be pinned until <see cref="Release"/> is called</param>
        /// <param name="length">The number of bytes available</param>
        /// <param name="handle">A pin handle that is disposed when the stream is released (or default)</param>
        internal void Init(Byte* data, int length, MemoryHandle handle)
        {
            Data = data;
            Len = length;
            Pos = 0;
            Handle = handle;
        }

        /// <summary>
        /// Stop using the memory, disposing the pin handle
        /// </summary>
        internal void Release()
        {
            Handle.Dispose();
            Handle = default;
            Data = null;
            Len = 0;
            Pos = 0;
        }

        /// <summary>
        /// The number of bytes written
        /// </summary>
        public int Written => Pos;

        /// <inheritdoc/>
        public override bool CanRead => false;
        /// <inheritdoc/>
        public override bool CanSeek => false;
        /// <inheritdoc/>
        public override bool CanWrite => true;
        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();
        /// <inheritdoc/>
        public override long Position { get => Pos; set => throw new NotSupportedException(); }
        /// <inheritdoc/>
        public override void Flush() { }
        /// <inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        /// <inheritdoc/>
        public override int Read(Byte[] buffer, int offset, int count) => throw new NotSupportedException();
        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(Byte[] buffer, int offset, int count) => Write(new ReadOnlySpan<Byte>(buffer, offset, count));

        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<Byte> buffer)
        {
            var l = buffer.Length;
            if (l > (Len - Pos))
                throw new ArgumentException(CompHelpers.EncDestTooSmall, "to");
            buffer.CopyTo(new Span<Byte>(Data + Pos, l));
            Pos += l;
        }

        /// <inheritdoc/>
        public override void WriteByte(Byte value) => Write(new ReadOnlySpan<Byte>(&value, 1));

        /// <inheritdoc/>
        public override Task WriteAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(new ReadOnlySpan<Byte>(buffer, offset, count));
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override ValueTask WriteAsync(ReadOnlyMemory<Byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
    }
}
