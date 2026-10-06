using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace SysWeaver.IO
{
    /// <summary>
    /// A <see cref="BinaryReader"/> that reads multi byte numbers with the bytes in the reverse order of the machine endianness
    /// (i.e. big endian on little endian machines). Created using <see cref="EndianAwareBinaryReader"/>.
    /// </summary>
    /// <remarks>
    /// Only the 16, 32 and 64 bit integers, <see cref="Half"/>, <see cref="float"/> and <see cref="double"/> are reversed, other members (ex: ReadDecimal, ReadChar, strings) behaves as the base <see cref="BinaryReader"/>.
    /// As with the base reader, an <see cref="EndOfStreamException"/> is thrown if the stream ends before all bytes of a value are read.
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
        /// <exception cref="EndOfStreamException">The end of the stream is reached</exception>
        public override Double ReadDouble()
            => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(base.ReadInt64()));

        /// <summary>
        /// Read a 4 byte floating point value stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached</exception>
        public override Single ReadSingle()
            => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(base.ReadInt32()));

        /// <summary>
        /// Read a 2 byte floating point value stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached</exception>
        public override Half ReadHalf()
            => BitConverter.Int16BitsToHalf(BinaryPrimitives.ReverseEndianness(base.ReadInt16()));

        /// <summary>
        /// Read a 2 byte signed integer stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached</exception>
        public override Int16 ReadInt16()
            => BinaryPrimitives.ReverseEndianness(base.ReadInt16());

        /// <summary>
        /// Read a 4 byte signed integer stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached</exception>
        public override Int32 ReadInt32()
            => BinaryPrimitives.ReverseEndianness(base.ReadInt32());

        /// <summary>
        /// Read an 8 byte signed integer stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached</exception>
        public override Int64 ReadInt64()
            => BinaryPrimitives.ReverseEndianness(base.ReadInt64());

        /// <summary>
        /// Read a 2 byte unsigned integer stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached</exception>
        public override UInt16 ReadUInt16()
            => BinaryPrimitives.ReverseEndianness(base.ReadUInt16());

        /// <summary>
        /// Read a 4 byte unsigned integer stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached</exception>
        public override UInt32 ReadUInt32()
            => BinaryPrimitives.ReverseEndianness(base.ReadUInt32());

        /// <summary>
        /// Read an 8 byte unsigned integer stored with the reversed byte order
        /// </summary>
        /// <returns>The value</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached</exception>
        public override UInt64 ReadUInt64()
            => BinaryPrimitives.ReverseEndianness(base.ReadUInt64());

    }
}
