using System;
using System.IO;

namespace SysWeaver.IO
{
    /// <summary>
    /// A read only view of a part of another stream, starting at the current position of the stream and limited to a maximum length.
    /// Typically used to let a decoder read a chunk of a file without being able to read past it (ex: PNG IDAT chunks).
    /// </summary>
    /// <remarks>
    /// The underlying stream must be seekable (the position is used to compute the remaining length) and is NOT disposed by this stream.
    /// Reads and seeks moves the position of the underlying stream, so the underlying stream must not be used at the same time.
    /// </remarks>
    public sealed class LengthLimitedStream : Stream
    {

        /// <summary>
        /// Create a view of a stream, starting at the current position of the stream
        /// </summary>
        /// <param name="s">The stream to read from, must be seekable</param>
        /// <param name="maxLength">The maximum number of bytes that can be read from this stream</param>
        /// <exception cref="NullReferenceException"><paramref name="s"/> is null</exception>
        /// <exception cref="NotSupportedException"><paramref name="s"/> doesn't support seeking</exception>
        public LengthLimitedStream(Stream s, long maxLength)
        {
            Start = s.Position;
            MaxLength = maxLength;
            S = s;
        }

        readonly long Start;
        readonly long MaxLength;
        readonly Stream S;

        /// <summary>
        /// True if the underlying stream can be read
        /// </summary>
        public override bool CanRead => S.CanRead;

        /// <summary>
        /// True if the underlying stream can seek
        /// </summary>
        public override bool CanSeek => S.CanSeek;

        /// <summary>
        /// Always false
        /// </summary>
        public override bool CanWrite => false;

        /// <summary>
        /// The max length, or the remaining length of the underlying stream (from the start position) if that is smaller
        /// </summary>
        public override long Length => Math.Min(MaxLength, S.Length - Start);

        /// <summary>
        /// The position relative to the start of this view
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The new position is greater than <see cref="Length"/> (negative values are not checked)</exception>
        public override long Position
        {
            get => S.Position - Start;
            set
            {
                if (value > Length)
                    throw new ArgumentOutOfRangeException();
                S.Position = value + Start;
            }
        }

        /// <summary>
        /// Flushes the underlying stream
        /// </summary>
        public override void Flush()
        {
            S.Flush();
        }

        /// <summary>
        /// Set the position within this view.
        /// Note: <see cref="SeekOrigin.End"/> treats a positive offset as the distance back from the end (Length - offset),
        /// and the returned value is the position of the underlying stream plus the start position (not the position within this view).
        /// </summary>
        /// <param name="offset">The offset relative to the origin</param>
        /// <param name="origin">The origin of the offset</param>
        /// <returns>The underlying stream position + the start position</returns>
        /// <exception cref="ArgumentOutOfRangeException">The new position is outside of [0, Length]</exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Current)
                offset += S.Position - Start;
            if (origin == SeekOrigin.End)
                offset = Length - offset;
            if ((offset < 0) || (offset > Length))
                throw new ArgumentOutOfRangeException();
            offset -= Position;
            return S.Seek(offset, SeekOrigin.Current) + Start;
        }

        /// <summary>
        /// Not supported, always throws
        /// </summary>
        /// <param name="value">Not used</param>
        /// <exception cref="NotImplementedException">Always</exception>
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Read bytes from the underlying stream, without reading past the max length
        /// </summary>
        /// <param name="buffer">The buffer to read into</param>
        /// <param name="offset">The offset in the buffer to write the first byte to</param>
        /// <param name="count">The max number of bytes to read</param>
        /// <returns>The number of bytes read, 0 at the end of this view (or of the underlying stream)</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            count = (int)Math.Min(MaxLength - Position, count);
            if (count <= 0)
                return 0;
            return S.Read(buffer, offset, count);
        }

        /// <summary>
        /// Not supported, always throws
        /// </summary>
        /// <param name="buffer">Not used</param>
        /// <param name="offset">Not used</param>
        /// <param name="count">Not used</param>
        /// <exception cref="NotImplementedException">Always</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }

}
