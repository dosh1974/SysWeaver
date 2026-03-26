using System;
using System.IO;

namespace SysWeaver.IO
{
    public sealed class LengthLimitedStream : Stream
    {

        public LengthLimitedStream(Stream s, long maxLength)
        {
            Start = s.Position;
            MaxLength = maxLength;
            S = s;
        }

        readonly long Start;
        readonly long MaxLength;
        readonly Stream S;

        public override bool CanRead => S.CanRead;

        public override bool CanSeek => S.CanSeek;

        public override bool CanWrite => false;

        public override long Length => Math.Min(MaxLength, S.Length - Start);

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

        public override void Flush()
        {
            S.Flush();
        }

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

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            count = (int)Math.Min(MaxLength - Position, count);
            if (count <= 0)
                return 0;
            return S.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }

}
