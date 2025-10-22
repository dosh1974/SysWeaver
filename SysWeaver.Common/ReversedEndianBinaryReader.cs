using System;
using System.IO;
using System.Text;

namespace SysWeaver
{
    public sealed class ReversedEndianBinaryReader : BinaryReader
    {
        internal ReversedEndianBinaryReader(Stream stream, Encoding encoding, bool leaveOpen = false) : base(stream, encoding, leaveOpen)
        {
        }
        internal ReversedEndianBinaryReader(Stream stream, bool leaveOpen = false)
            : base(stream, Encoding.UTF8, leaveOpen)
        {
        }
     
        public override Double ReadDouble()
        {
            Span<Byte> t = stackalloc Byte[8];
            Read(t);
            Byte b0 = t[0];
            Byte b1 = t[1];
            Byte b2 = t[2];
            Byte b3 = t[3];
            Byte b4 = t[4];
            Byte b5 = t[5];
            Byte b6 = t[6];
            Byte b7 = t[7];
            t[4] = b3;
            t[5] = b2;
            t[6] = b1;
            t[7] = b0;
            t[0] = b7;
            t[1] = b6;
            t[2] = b5;
            t[3] = b4;
            return BitConverter.ToDouble(t);
        }
        
        public override Single ReadSingle()
        {
            Span<Byte> t = stackalloc Byte[4];
            Read(t);
            Byte b0 = t[0];
            Byte b1 = t[1];
            Byte b2 = t[2];
            Byte b3 = t[3];
            t[2] = b1;
            t[3] = b0;
            t[0] = b3;
            t[1] = b2;
            return BitConverter.ToSingle(t);
        }


        public override Int16 ReadInt16()
        {
            Span<Byte> t = stackalloc Byte[2];
            Read(t);
            Byte b0 = t[0];
            Byte b1 = t[1];
            t[1] = b0;
            t[0] = b1;
            return BitConverter.ToInt16(t);
        }

        public override Int32 ReadInt32()
        {
            Span<Byte> t = stackalloc Byte[4];
            Read(t);
            Byte b0 = t[0];
            Byte b1 = t[1];
            Byte b2 = t[2];
            Byte b3 = t[3];
            t[2] = b1;
            t[3] = b0;
            t[0] = b3;
            t[1] = b2;
            return BitConverter.ToInt32(t);
        }

        public override Int64 ReadInt64()
        {
            Span<Byte> t = stackalloc Byte[8];
            Read(t);
            Byte b0 = t[0];
            Byte b1 = t[1];
            Byte b2 = t[2];
            Byte b3 = t[3];
            Byte b4 = t[4];
            Byte b5 = t[5];
            Byte b6 = t[6];
            Byte b7 = t[7];
            t[4] = b3;
            t[5] = b2;
            t[6] = b1;
            t[7] = b0;
            t[0] = b7;
            t[1] = b6;
            t[2] = b5;
            t[3] = b4;
            return BitConverter.ToInt64(t);
        }

        public override UInt16 ReadUInt16()
        {
            Span<Byte> t = stackalloc Byte[2];
            Read(t);
            Byte b0 = t[0];
            Byte b1 = t[1];
            t[1] = b0;
            t[0] = b1;
            return BitConverter.ToUInt16(t);
        }

        public override UInt32 ReadUInt32()
        {
            Span<Byte> t = stackalloc Byte[4];
            Read(t);
            Byte b0 = t[0];
            Byte b1 = t[1];
            Byte b2 = t[2];
            Byte b3 = t[3];
            t[2] = b1;
            t[3] = b0;
            t[0] = b3;
            t[1] = b2;
            return BitConverter.ToUInt32(t);
        }

        public override UInt64 ReadUInt64()
        {
            Span<Byte> t = stackalloc Byte[8];
            Read(t);
            Byte b0 = t[0];
            Byte b1 = t[1];
            Byte b2 = t[2];
            Byte b3 = t[3];
            Byte b4 = t[4];
            Byte b5 = t[5];
            Byte b6 = t[6];
            Byte b7 = t[7];
            t[4] = b3;
            t[5] = b2;
            t[6] = b1;
            t[7] = b0;
            t[0] = b7;
            t[1] = b6;
            t[2] = b5;
            t[3] = b4;
            return BitConverter.ToUInt64(t);
        }

    }
}
