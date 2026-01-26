using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Create a stream as the contatenation of several streams
    /// </summary>
    public class ChunkedStream : Stream
    {
        /// <summary>
        /// Create a stream as the contatenation of several streams
        /// </summary>
        /// <param name="streamOpener">A function that opens one stream chunk, the paramter start at 0 and is incremented every time a new chunk is required, return null to signal end of data</param>
        public ChunkedStream(Func<int, Stream> streamOpener)
        {
            OpenStream = streamOpener;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        public override void Flush() => throw new NotImplementedException();

        public override void Close()
        {
            Current?.Dispose();
            Current = null;
        }

  

        readonly Func<int, Stream> OpenStream;
        int ChunkIndex = -1;
        Stream Current;

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

        Stream GetNextStream()
        {
            var i = ChunkIndex;
            ++i;
            ChunkIndex = i;
            var c = OpenStream(i);
            Current = c;
            return c;
        }

        public override long Length => throw new NotImplementedException();

        public override long Position { get; set; }

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override bool CanTimeout => false;


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
                Position += numBytesRead;
                if (count <= 0)
                    break;
                read += numBytesRead;
                offset += numBytesRead;
                currentSteam.Dispose();
                currentSteam = GetNextStream();
            }
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var count = buffer.Length;
            int offset = 0;
            var currentSteam = GetStream();
            int read = 0;
            while (count > 0)
            {
                if (currentSteam == null)
                    break;
                // Read what we can from the current stream
                int numBytesRead = currentSteam.Read(buffer.Slice(offset, count));
                count -= numBytesRead;
                Position += numBytesRead;
                if (count <= 0)
                    break;
                read += numBytesRead;
                offset += numBytesRead;
                currentSteam.Dispose();
                currentSteam = GetNextStream();
            }
            return read;
        }

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
                Position += numBytesRead;
                if (count <= 0)
                    break;
                read += numBytesRead;
                offset += numBytesRead;
                currentSteam.Dispose();
                currentSteam = GetNextStream();
            }
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = buffer.Length;
            int offset = 0;
            var currentSteam = GetStream();
            int read = 0;
            while (count > 0)
            {
                if (currentSteam == null)
                    break;
                // Read what we can from the current stream
                int numBytesRead = await currentSteam.ReadAsync(buffer.Slice(offset, count), cancellationToken).ConfigureAwait(false);
                count -= numBytesRead;
                Position += numBytesRead;
                if (count <= 0)
                    break;
                read += numBytesRead;
                offset += numBytesRead;
                currentSteam.Dispose();
                currentSteam = GetNextStream();
            }
            return read;
        }

    }




}
