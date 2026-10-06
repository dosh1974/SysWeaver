using System;
using System.IO;
using System.Text;

namespace SysWeaver.Compression
{
    /// <summary>
    /// A stream that takes a seekable input stream containing GZip compressed data, and presents it as a raw deflate compressed data stream
    /// (the GZip header and the 8 byte CRC32 / size trailer are skipped, no recompression is done).
    /// </summary>
    /// <remarks>
    /// Used by the HTTP server to serve pre-compressed ".gz" data to clients that only accept "deflate".
    /// Only single member GZip data is supported (anything after the first member's deflate data is treated as part of it).
    /// Not thread safe, the position of the underlying stream is used as the position of this stream.
    /// </remarks>
    public sealed class TransformGZipToDeflateStream : Stream
    {
        const string Invalid = "Data is not valid GZip data";

        /// <summary>
        /// Get deflate memory from gzip memory
        /// </summary>
        /// <param name="gzipData">Memory containing GZip data</param>
        /// <returns>Memory with the deflate portion of the memory data (a slice of <paramref name="gzipData"/>, no copy is made)</returns>
        /// <exception cref="Exception">The data isn't valid GZip data, or isn't deflate compressed.</exception>
        public static ReadOnlyMemory<Byte> GetDeflateData(ReadOnlyMemory<byte> gzipData)
        {
            var data = gzipData.Span;
            int offset = 10;
            var l = gzipData.Length;
            if (offset > l)
                throw new Exception(Invalid);
            if ((data[0] != 0x1f) || (data[1] != 0x8b))
                throw new Exception("Input data does not contain gzip compressed data!");
            if (data[2] != 0x08)
                throw new Exception("Only deflate compressed gzip data is supported!");
            var flags = data[3];
            if ((flags & 0x4) != 0) // FEXTRA 
            {
                offset += 2;
                if (offset > l)
                    throw new Exception(Invalid);
                int extraLen = ((int)data[offset - 2]) | (((int)data[offset - 1]) << 8);
                offset += extraLen;
                if (offset > l)
                    throw new Exception(Invalid);
            }
            if ((flags & 0x8) != 0) // FNAME
            {
                for (; ; )
                {
                    ++offset;
                    if (offset > l)
                        throw new Exception(Invalid);
                    if (data[offset - 1] == 0)
                        break;
                }
            }
            if ((flags & 0x10) != 0) // FCOMMENT
            {
                for (; ; )
                {
                    ++offset;
                    if (offset > l)
                        throw new Exception(Invalid);
                    if (data[offset - 1] == 0)
                        break;
                }
            }
            if ((flags & 0x2) != 0) // FHCRC
                offset += 2;
            var len = l - offset - 8;
            if (len <= 0)
                throw new Exception(Invalid);
            return gzipData.Slice(offset, len);
        }


        /// <summary>
        /// Create a deflate data stream from a gzip data stream, the GZip header is read (and validated) from the current position
        /// </summary>
        /// <param name="gzipData">Seekable stream containing GZip data (must be a complete file and nothing after that)</param>
        /// <param name="leaveOpen">If true, the underlying stream isn't disposed when this stream is disposed</param>
        /// <exception cref="Exception">The data isn't valid GZip data, or isn't deflate compressed.</exception>
        /// <exception cref="NotSupportedException"><paramref name="gzipData"/> isn't seekable.</exception>
        public TransformGZipToDeflateStream(Stream gzipData, bool leaveOpen = false)
        {
            var l = gzipData.Length - gzipData.Position;
            if (l > 0)
            {
                Span<Byte> tempData = stackalloc Byte[10];
                var tempData1 = tempData.Slice(0, 1);
                var tempData2 = tempData.Slice(0, 2);
                if (gzipData.ReadAtLeast(tempData, 10, false) != 10)
                    throw new Exception(Invalid);
                if ((tempData[0] != 0x1f) || (tempData[1] != 0x8b))
                    throw new Exception("Input stream does not contain gzip compressed data!");
                if (tempData[2] != 0x08)
                    throw new Exception("Only deflate compressed gzip data is supported!");
                var flags = tempData[3];
                if ((flags & 0x4) != 0) // FEXTRA 
                {
                    if (gzipData.ReadAtLeast(tempData2, 2, false) != 2)
                        throw new Exception(Invalid);
                    int extraLen = ((int)tempData2[0]) | (((int)tempData2[1]) << 8);
                    gzipData.Position += extraLen;
                }
                if ((flags & 0x8) != 0) // FNAME
                {
                    for (; ; )
                    {
                        if (gzipData.Read(tempData1) != 1)
                            throw new Exception(Invalid);
                        if (tempData1[0] == 0)
                            break;
                    }
                }
                if ((flags & 0x10) != 0) // FCOMMENT
                {
                    var sb = new StringBuilder();
                    for (; ; )
                    {
                        if (gzipData.Read(tempData1) != 1)
                            throw new Exception(Invalid);
                        if (tempData1[0] == 0)
                            break;
                    }
                }
                if ((flags & 0x2) != 0) // FHCRC
                    gzipData.Position += 2;
                InternalStart = gzipData.Position;
                InternalLength = gzipData.Length - InternalStart - 8;
                if (InternalLength <= 0)
                    throw new Exception(Invalid);
                InternalEnd = InternalStart + InternalLength;
            }
            UnderlayingStream = gzipData;
            LeaveOpen = leaveOpen;
        }

        /// <summary>
        /// If true, the underlying stream isn't disposed when this stream is disposed
        /// </summary>
        public readonly bool LeaveOpen;

        /// <summary>
        /// The stream containing the GZip data
        /// </summary>
        public readonly Stream UnderlayingStream;

        readonly long InternalStart;
        readonly long InternalEnd;
        readonly long InternalLength;

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => true;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => InternalLength;

        /// <inheritdoc/>
        public override long Position
        {
            get => UnderlayingStream.Position - InternalStart;
            set
            {
                if (value < 0)
                    throw new Exception("Invalid stream position!");
                if (value > InternalLength)
                    throw new Exception("Invalid stream position!");
                UnderlayingStream.Position = InternalStart + value;
            }
        }

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            var pos = UnderlayingStream.Position;
            var end = pos + count;
            if (end > InternalEnd)
                end = InternalEnd;
            count = (int)(end - pos);
            if (count <= 0)
                return 0;
            return UnderlayingStream.Read(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    return Position;
                case SeekOrigin.Current:
                    Position += offset;
                    return Position;
                case SeekOrigin.End:
                    Position = InternalLength + offset;
                    return Position;
            }
            throw new ArgumentException("Invalid SeekOrigin", nameof(origin));
        }

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotImplementedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (!LeaveOpen)
                    UnderlayingStream.Dispose();
            }
        }

    }

}

