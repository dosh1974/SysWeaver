using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    /// <summary>
    /// A read only, non seekable stream that is fed with chunks of data (server sent events) while it's being read.
    /// The server copies the stream to the response as data arrives, so every written chunk is sent to the client at once.
    /// Disposing the stream (the server does it when the response ends, ex: the client disconnected) closes it, further writes are ignored.
    /// </summary>
    sealed class McpEventStream : Stream
    {
        readonly Channel<ReadOnlyMemory<Byte>> Chunks = Channel.CreateUnbounded<ReadOnlyMemory<Byte>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        ReadOnlyMemory<Byte> Current;
        int Closed;

        /// <summary>
        /// True if the stream was disposed (the response ended), there is no point in writing more data
        /// </summary>
        public bool IsClosed => Volatile.Read(ref Closed) != 0;

        /// <summary>
        /// Queue a chunk of data to send
        /// </summary>
        /// <param name="data">The data (must not be modified afterwards)</param>
        /// <returns>True if the chunk was queued, false if the stream is closed or completed</returns>
        public bool Write(ReadOnlyMemory<Byte> data)
            => (!IsClosed) && Chunks.Writer.TryWrite(data);

        /// <summary>
        /// Signal that no more data will be written (the reader gets end of stream once the queued data is read)
        /// </summary>
        public void Complete()
            => Chunks.Writer.TryComplete();

        public override async ValueTask<int> ReadAsync(Memory<Byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty)
                return 0;
            var c = Current;
            while (c.IsEmpty)
            {
                var r = Chunks.Reader;
                if (!r.TryRead(out c))
                {
                    if (!await r.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                        return 0;
                    continue;
                }
            }
            var n = Math.Min(c.Length, buffer.Length);
            c.Span.Slice(0, n).CopyTo(buffer.Span);
            Current = c.Slice(n);
            return n;
        }

        public override Task<int> ReadAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(Byte[] buffer, int offset, int count)
            => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref Closed, 1) == 0)
            {
                Chunks.Writer.TryComplete();
                Current = default;
                //  Release queued data
                while (Chunks.Reader.TryRead(out var _))
                {
                }
            }
            base.Dispose(disposing);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(Byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
