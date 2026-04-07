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

        public Stream BaseStream => reader.BaseStream;

        public PsdBinaryReader(Stream stream, PsdBinaryReader reader)
          : this(stream, reader.encoding)
        {
        }

        public PsdBinaryReader(Stream stream, Encoding encoding)
        {
            this.encoding = encoding;

            // ReadPascalString and ReadUnicodeString handle encoding explicitly.
            // BinaryReader.ReadString() is never called, so it is constructed with
            // ASCII encoding to make accidental usage obvious.
            reader = new BinaryReader(stream, Encoding.ASCII);
        }

        public byte ReadByte()
        {
            return reader.ReadByte();
        }

        public byte[] ReadBytes(int count)
        {
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
        public string ReadAsciiChars(int count)
        {
            Span<Byte> bytes = stackalloc Byte[count];
            reader.ReadExactly(bytes);
            var s = Encoding.ASCII.GetString(bytes);
            return s;
        }

        /// <summary>
        /// Read a Pascal string using the specified encoding.
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

        public string ReadUnicodeString()
        {
            var numChars = ReadInt32();
            Span<Byte> bytes = stackalloc Byte[2 * numChars];
            reader.ReadExactly(bytes);
            var str = Encoding.BigEndianUnicode.GetString(bytes);
            return str;
        }

        public string ReadUnicodeString(int numChars)
        {
            Span<Byte> bytes = stackalloc Byte[2 * numChars];
            reader.ReadExactly(bytes);
            var str = Encoding.BigEndianUnicode.GetString(bytes);
            return str;
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