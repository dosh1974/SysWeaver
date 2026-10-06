using System;
using System.IO;
using System.Text;

namespace SysWeaver.IO
{
    /// <summary>
    /// A <see cref="BinaryReader"/> that reads multi byte numbers with the bytes in the reverse order of the machine endianness
    /// (i.e. big endian on little endian machines). Created using <see cref="EndianAwareBinaryReader"/>.
    /// </summary>
    /// <remarks>
    /// Only the 16, 32 and 64 bit integers, <see cref="float"/> and <see cref="double"/> are reversed, other members (ex: ReadDecimal, ReadHalf, ReadChar, strings) behaves as the base <see cref="BinaryReader"/>.
    /// Unlike the base reader, the overridden members do NOT throw an <see cref="EndOfStreamException"/> if the stream ends (or a read returns fewer bytes), the missing bytes are treated as zero.
    /// </remarks>
    public sealed class ReversedEndianBinaryReader : BinaryReader
    {
        /// <summary>
        /// Create a reader
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="encoding">The text encoding to use for chars and strings</param>
        /// <param name="leaveOpen">If true, the stream is not disposed when the reader is disposed</param>
        internal ReversedEndianBinaryReader(Stream stream, Encoding encoding, bool leaveOpen = false) : base(stream, encoding, leaveOpen)
        {
        }
        /// <summary>
        /// Create a reader that uses UTF-8 for chars and strings
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="leaveOpen">If true, the stream is not disposed when the reader is disposed</param>
        internal ReversedEndianBinaryReader(Stream stream, bool leaveOpen = false)
            : base(stream, Encoding.UTF8, leaveOpen)
        {
        }
     
        /// <summary>
        /// Read an 8 byte floating point value stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
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
        
        /// <summary>
        /// Read a 4 byte floating point value stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
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


        /// <summary>
        /// Read a 2 byte signed integer stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
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

        /// <summary>
        /// Read a 4 byte signed integer stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
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

        /// <summary>
        /// Read an 8 byte signed integer stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
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

        /// <summary>
        /// Read a 2 byte unsigned integer stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
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

        /// <summary>
        /// Read a 4 byte unsigned integer stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
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

        /// <summary>
        /// Read an 8 byte unsigned integer stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
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
