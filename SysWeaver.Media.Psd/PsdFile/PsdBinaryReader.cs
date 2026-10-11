/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop PSD FileType Plugin for Paint.NET
// http://psdplugin.codeplex.com/
//
// This software is provided under the MIT License:
//   Copyright (c) 2006-2007 Frank Blumenberg
//   Copyright (c) 2010-2017 Tao Yue
//
// Portions of this file are provided under the BSD 3-clause License:
//   Copyright (c) 2006, Jonas Beckeman
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Buffers.Binary;
using System.Drawing;
using System.IO;
using System.Text;

namespace PhotoshopFile
{
    /// <summary>
    /// Reads PSD data types in big-endian byte order.
    /// </summary>
    public class PsdBinaryReader : IDisposable
    {
        private BinaryReader reader;
        private Encoding encoding;

        /// <summary>
        /// The underlying stream.
        /// </summary>
        public Stream BaseStream => reader.BaseStream;

        /// <summary>
        /// Create a reader for another stream, using the same Pascal string encoding as an existing reader.
        /// </summary>
        /// <param name="stream">The stream to read from (it is closed when this reader is disposed).</param>
        /// <param name="reader">The reader to take the encoding from.</param>
        public PsdBinaryReader(Stream stream, PsdBinaryReader reader)
          : this(stream, reader.encoding)
        {
        }

        /// <summary>
        /// Create a big-endian reader.
        /// </summary>
        /// <param name="stream">The stream to read from (it is closed when this reader is disposed).</param>
        /// <param name="encoding">The encoding used by <see cref="ReadPascalString(int)"/>.</param>
        public PsdBinaryReader(Stream stream, Encoding encoding)
        {
            this.encoding = encoding;

            // ReadPascalString and ReadUnicodeString handle encoding explicitly.
            // BinaryReader.ReadString() is never called, so it is constructed with
            // ASCII encoding to make accidental usage obvious.
            reader = new BinaryReader(stream, Encoding.ASCII);
        }

        /// <summary>
        /// Strings up to this number of bytes are read into a stack buffer.
        /// </summary>
        const int MaxStackBytes = 1024;

        /// <summary>
        /// Throws if a seekable stream has less than <paramref name="count"/> bytes left.
        /// Prevents huge (stack) allocations caused by corrupt length fields.
        /// </summary>
        internal void CheckAvailable(long count)
        {
            var s = reader.BaseStream;
            if (s.CanSeek && (count > (s.Length - s.Position)))
                throw new EndOfStreamException("Can't read " + count + " bytes at position " + s.Position + ", the stream only has " + (s.Length - s.Position) + " bytes left (truncated or corrupt data).");
        }

        /// <summary>
        /// Read a string of byteCount bytes, using a stack buffer for short strings.
        /// </summary>
        string ReadString(long byteCount, Encoding enc)
        {
            CheckAvailable(byteCount);
            if (byteCount <= MaxStackBytes)
            {
                Span<Byte> bytes = stackalloc Byte[(int)byteCount];
                reader.ReadExactly(bytes);
                return enc.GetString(bytes);
            }
            Util.CheckByteArrayLength(byteCount);
            var data = new byte[byteCount];
            reader.ReadExactly(data);
            return enc.GetString(data);
        }

        /// <summary>
        /// Read a byte.
        /// </summary>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        public byte ReadByte()
        {
            return reader.ReadByte();
        }

        /// <summary>
        /// Read <paramref name="count"/> bytes.
        /// </summary>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The bytes read. If the stream is not seekable, this can be shorter than <paramref name="count"/> at the end of the stream.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The stream is seekable and has less than <paramref name="count"/> bytes left.</exception>
        public byte[] ReadBytes(int count)
        {
            if (count > 0)
                CheckAvailable(count);
            return reader.ReadBytes(count);
        }

        public bool ReadBoolean()
        {
            return reader.ReadBoolean();
        }

        public Int16 ReadInt16()
        {
            return BinaryPrimitives.ReverseEndianness(reader.ReadInt16());
        }

        public Int32 ReadInt32()
        {
            return BinaryPrimitives.ReverseEndianness(reader.ReadInt32());
        }

        public Int64 ReadInt64()
        {
            return BinaryPrimitives.ReverseEndianness(reader.ReadInt64());
        }

        public UInt16 ReadUInt16()
        {
            return BinaryPrimitives.ReverseEndianness(reader.ReadUInt16());
        }

        public UInt32 ReadUInt32()
        {
            return BinaryPrimitives.ReverseEndianness(reader.ReadUInt32());
        }

        public UInt64 ReadUInt64()
        {
            return BinaryPrimitives.ReverseEndianness(reader.ReadUInt64());
        }

        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Read padding to get to the byte multiple for the block.
        /// </summary>
        /// <param name="startPosition">Starting position of the padded block.</param>
        /// <param name="padMultiple">Byte multiple that the block is padded to.</param>
        public void ReadPadding(long startPosition, int padMultiple)
        {
            // Pad to specified byte multiple
            var totalLength = reader.BaseStream.Position - startPosition;
            var padBytes = Util.GetPadding((int)totalLength, padMultiple);
            if (padBytes > 0)
            {
                Span<Byte> bytes = stackalloc Byte[padBytes];
                reader.ReadExactly(bytes);
            }
        }

        /// <summary>
        /// Read a rectangle stored as four big-endian Int32 values: top, left, bottom, right.
        /// </summary>
        public Rectangle ReadRectangle()
        {
            var rect = new Rectangle();
            rect.Y = ReadInt32();
            rect.X = ReadInt32();
            rect.Height = ReadInt32() - rect.Y;
            rect.Width = ReadInt32() - rect.X;
            return rect;
        }

        /// <summary>
        /// Read a fixed-length ASCII string.
        /// </summary>
        /// <param name="count">The number of characters (bytes) to read.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The stream has less than <paramref name="count"/> bytes left.</exception>
        public string ReadAsciiChars(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            return ReadString(count, Encoding.ASCII);
        }

        /// <summary>
        /// Read a Pascal string (a length byte followed by the bytes) using the encoding given to the constructor.
        /// The padding bytes (counted from the length byte) are skipped.
        /// </summary>
        /// <param name="padMultiple">Byte multiple that the Pascal string is padded to.</param>
        public string ReadPascalString(int padMultiple)
        {
            var startPosition = reader.BaseStream.Position;

            byte stringLength = ReadByte();
            Span<Byte> bytes = stackalloc Byte[stringLength];
            reader.ReadExactly(bytes);
            ReadPadding(startPosition, padMultiple);

            // Default decoder uses best-fit fallback, so it will not throw any
            // exceptions if unknown characters are encountered.
            var str = encoding.GetString(bytes);
            return str;
        }

        /// <summary>
        /// Read a Unicode string: a big-endian Int32 character count followed by the UTF-16 big-endian characters.
        /// </summary>
        /// <exception cref="PsdInvalidException">The character count is negative.</exception>
        /// <exception cref="EndOfStreamException">The stream ends before the string does.</exception>
        public string ReadUnicodeString()
        {
            var numChars = ReadInt32();
            if (numChars < 0)
                throw new PsdInvalidException("Unicode string has a negative length (" + numChars + ").");
            return ReadString(2L * numChars, Encoding.BigEndianUnicode);
        }

        /// <summary>
        /// Read <paramref name="numChars"/> UTF-16 big-endian characters (no length prefix).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="numChars"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The stream ends before the string does.</exception>
        public string ReadUnicodeString(int numChars)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(numChars);
            return ReadString(2L * numChars, Encoding.BigEndianUnicode);
        }

        //////////////////////////////////////////////////////////////////

        #region IDisposable

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called. 
            if (disposed)
                return;

            if (disposing)
            {
                if (reader != null)
                {
                    // BinaryReader.Dispose() is protected.
                    reader.Close();
                    reader = null;
                }
            }

            disposed = true;
        }

        #endregion

    }

}