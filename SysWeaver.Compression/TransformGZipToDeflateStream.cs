using System;
using System.IO;
using System.Text;

namespace SysWeaver.Compression
{
    /// <summary>
    /// A stream that takes a seekable input stream containing GZip compressed data, and presents it as a deflate compressed data stream
    /// </summary>
    public sealed class TransformGZipToDeflateStream : Stream
    {
        const string Invalid = "Data is not valid GZip data";

        /// <summary>
        /// Get deflate memory from gzip memory
        /// </summary>
        /// <param name="gzipData">Memory containing GZip data</param>
        /// <returns>Memory with the deflate portion of the memory data</returns>
        /// <exception cref="Exception"></exception>
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
                int extraLen = ((int)data[offset - 2 + 1]) | ((int)data[offset - 2 + 0]);
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
        /// Create a defalte data stream from a gzip data stream
        /// </summary>
        /// <param name="gzipData">Stream containing GZIp data (must be a complete file and nothing after that)</param>
        /// <param name="leaveOpen">If true, the underlaying stream isn't disposed when this stream is disposed</param>
        /// <exception cref="Exception"></exception>
        public TransformGZipToDeflateStream(Stream gzipData, bool leaveOpen = false)
        {
            Span<Byte> tempData = stackalloc Byte[10];
            var tempData1 = tempData.Slice(0, 1);
            var tempData2 = tempData.Slice(0, 2);
            if (gzipData.Read(tempData) != 10)
                throw new Exception(Invalid);
            if ((tempData[0] != 0x1f) || (tempData[1] != 0x8b))
                throw new Exception("Input stream does not contain gzip compressed data!");
            if (tempData[2] != 0x08)
                throw new Exception("Only deflate compressed gzip data is supported!");
            var flags = tempData[3];
            if ((flags & 0x4) != 0) // FEXTRA 
            {
                if (gzipData.Read(tempData2) != 2)
                    throw new Exception(Invalid);
                int extraLen = ((int)tempData2[1]) | ((int)tempData2[0]);
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
            UnderlayingStream = gzipData;
            LeaveOpen = leaveOpen;
        }

        public readonly bool LeaveOpen;
        public readonly Stream UnderlayingStream;

        readonly long InternalStart;
        readonly long InternalEnd;
        readonly long InternalLength;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => InternalLength;

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

        public override void Flush()
        {
        }

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

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = InternalLength - offset;
                    break;
            }
            throw new ArgumentException("Invalid SeekOrigin", nameof(origin));
        }

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

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

