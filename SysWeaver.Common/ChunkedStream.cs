using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// A forward only, read only stream that is the concatenation of several streams (chunks), the chunks are opened lazily (one at a time) and disposed when fully read.
    /// </summary>
    /// <remarks>
    /// A chunk is considered fully read when a read from it returns 0 bytes, so any kind of chunk stream (including network, pipe and decompression streams that may return short reads) can be used.
    /// Seeking, writing, <see cref="Length"/> and <see cref="Flush"/> are not supported (throws <see cref="NotImplementedException"/>).
    /// The stream is not thread safe.
    /// Used by CompressedChunkedStream (SysWeaver.Storage) to read content defined chunks as one stream.
    /// </remarks>
    public class ChunkedStream : Stream
    {
        /// <summary>
        /// Create a stream as the concatenation of several streams
        /// </summary>
        /// <param name="streamOpener">A function that opens one stream chunk, the parameter starts at 0 and is incremented every time a new chunk is required, return null to signal end of data.
        /// The returned streams are owned (disposed) by this stream. The function is called on the first read, not in the constructor.</param>
        public ChunkedStream(Func<int, Stream> streamOpener)
        {
            OpenStream = streamOpener;
        }

        /// <summary>
        /// Not supported, always throws
        /// </summary>
        /// <exception cref="NotImplementedException">Always</exception>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        /// <summary>
        /// Not supported, always throws
        /// </summary>
        /// <exception cref="NotImplementedException">Always</exception>
        public override void SetLength(long value) => throw new NotImplementedException();

        /// <summary>
        /// Not supported, always throws
        /// </summary>
        /// <exception cref="NotImplementedException">Always</exception>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        /// <summary>
        /// Not supported, always throws (note that this differs from most read only streams where Flush does nothing)
        /// </summary>
        /// <exception cref="NotImplementedException">Always</exception>
        public override void Flush() => throw new NotImplementedException();

        /// <summary>
        /// Dispose the current chunk stream, subsequent reads returns 0 (end of stream).
        /// Note that the base implementation isn't called (so Dispose(bool) of a derived class isn't invoked by Close / Dispose).
        /// </summary>
        public override void Close()
        {
            Current?.Dispose();
            Current = null;
        }

  

        readonly Func<int, Stream> OpenStream;
        int ChunkIndex = -1;
        Stream Current;

        /// <summary>
        /// Get the current chunk stream, opens the first chunk on the first call.
        /// </summary>
        /// <returns>The current chunk stream, or null at the end of the data</returns>
        Stream GetStream()
        {
            var c = Current;
            if (c != null)
                return Current;
            var i = ChunkIndex;
            if (i >= 0)
                return null;
            ++i;
            ChunkIndex = i;
            c = OpenStream(i);
            Current = c;
            return c;
        }

        /// <summary>
        /// Open the next chunk stream (the caller must dispose the previous one)
        /// </summary>
        /// <returns>The next chunk stream, or null at the end of the data</returns>
        Stream GetNextStream()
        {
            var i = ChunkIndex;
            ++i;
            ChunkIndex = i;
            var c = OpenStream(i);
            Current = c;
            return c;
        }

        /// <summary>
        /// Not supported, always throws
        /// </summary>
        /// <exception cref="NotImplementedException">Always</exception>
        public override long Length => throw new NotImplementedException();

        /// <summary>
        /// The number of bytes read so far.
        /// Setting it only changes the reported value, it doesn't seek.
        /// </summary>
        public override long Position { get; set; }

        /// <summary>
        /// Always true
        /// </summary>
        public override bool CanRead => true;
        /// <summary>
        /// Always false
        /// </summary>
        public override bool CanWrite => false;
        /// <summary>
        /// Always false
        /// </summary>
        public override bool CanSeek => false;
        /// <summary>
        /// Always false
        /// </summary>
        public override bool CanTimeout => false;


        /// <summary>
        /// Read a single byte, moving to the next chunk(s) as needed.
        /// </summary>
        /// <returns>The byte (0 - 255) or -1 at the end of the data</returns>
        public override int ReadByte()
        {
            var currentSteam = GetStream();
            for (; ; )
            {
                if (currentSteam == null)
                    return -1;
                var b = currentSteam.ReadByte();
                if (b >= 0)
                { 
                    ++Position;
                    return b;
                }
                currentSteam.Dispose();
                currentSteam = GetNextStream();
            }
        }

        /// <summary>
        /// Read bytes, continuing with the next chunk(s) until the buffer is full or the end of the data is reached.
        /// </summary>
        /// <param name="buffer">The buffer to read into</param>
        /// <param name="offset">The offset in the buffer to write the first byte to</param>
        /// <param name="count">The max number of bytes to read</param>
        /// <returns>The number of bytes read, less than requested only at the end of the data (0 if no more data)</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            var currentSteam = GetStream();
            int read = 0;
            while (count > 0)
            {
                if (currentSteam == null)
                    break;
                // Read what we can from the current stream
                int numBytesRead = currentSteam.Read(buffer, offset, count);
                count -= numBytesRead;
                read += numBytesRead;
                Position += numBytesRead;
                if (count <= 0)
                    break;
                if (numBytesRead > 0)
                {
                    // A short read doesn't mean that the chunk is done, keep reading from the same chunk
                    offset += numBytesRead;
                    continue;
                }
                // End of this chunk, move to the next one
                currentSteam.Dispose();
                currentSteam = GetNextStream();
            }
            return read;
        }

        /// <summary>
        /// Read bytes, continuing with the next chunk(s) until the buffer is full or the end of the data is reached.
        /// </summary>
        /// <param name="buffer">The buffer to read into</param>
        /// <returns>The number of bytes read, less than requested only at the end of the data (0 if no more data)</returns>
        public override int Read(Span<byte> buffer)
        {
            var count = buffer.Length;
            var currentSteam = GetStream();
            int read = 0;
            while (count > 0)
            {
                if (currentSteam == null)
                    break;
                // Read what we can from the current stream
                int numBytesRead = currentSteam.Read(buffer);
                count -= numBytesRead;
                read += numBytesRead;
                Position += numBytesRead;
                if (count <= 0)
                    break;
                if (numBytesRead > 0)
                {
                    // A short read doesn't mean that the chunk is done, keep reading from the same chunk
                    buffer = buffer[numBytesRead..];
                    continue;
                }
                // End of this chunk, move to the next one
                currentSteam.Dispose();
                currentSteam = GetNextStream();
            }
            return read;
        }

        /// <summary>
        /// Read bytes, continuing with the next chunk(s) until the buffer is full or the end of the data is reached.
        /// </summary>
        /// <param name="buffer">The buffer to read into</param>
        /// <param name="offset">The offset in the buffer to write the first byte to</param>
        /// <param name="count">The max number of bytes to read</param>
        /// <param name="cancellationToken">The cancellation token (passed to the chunk streams)</param>
        /// <returns>The number of bytes read, less than requested only at the end of the data (0 if no more data)</returns>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var currentSteam = GetStream();
            int read = 0;
            while (count > 0)
            {
                if (currentSteam == null)
                    break;
                // Read what we can from the current stream
                int numBytesRead = await currentSteam.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                count -= numBytesRead;
                read += numBytesRead;
                Position += numBytesRead;
                if (count <= 0)
                    break;
                if (numBytesRead > 0)
                {
                    // A short read doesn't mean that the chunk is done, keep reading from the same chunk
                    offset += numBytesRead;
                    continue;
                }
                // End of this chunk, move to the next one
                currentSteam.Dispose();
                currentSteam = GetNextStream();
            }
            return read;
        }

        /// <summary>
        /// Read bytes, continuing with the next chunk(s) until the buffer is full or the end of the data is reached.
        /// </summary>
        /// <param name="buffer">The buffer to read into</param>
        /// <param name="cancellationToken">The cancellation token (passed to the chunk streams)</param>
        /// <returns>The number of bytes read, less than requested only at the end of the data (0 if no more data)</returns>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = buffer.Length;
            var currentSteam = GetStream();
            int read = 0;
            while (count > 0)
            {
                if (currentSteam == null)
                    break;
                // Read what we can from the current stream
                int numBytesRead = await currentSteam.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                count -= numBytesRead;
                read += numBytesRead;
                Position += numBytesRead;
                if (count <= 0)
                    break;
                if (numBytesRead > 0)
                {
                    // A short read doesn't mean that the chunk is done, keep reading from the same chunk
                    buffer = buffer[numBytesRead..];
                    continue;
                }
                // End of this chunk, move to the next one
                currentSteam.Dispose();
                currentSteam = GetNextStream();
            }
            return read;
        }

    }




}
