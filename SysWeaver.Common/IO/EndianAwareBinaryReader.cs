using System;
using System.Text;
using System.IO;

namespace SysWeaver.IO
{


    /// <summary>
    /// Contains method that create BinaryReader's with specific endianness requirements.
    /// </summary>
    /// <remarks>
    /// The standard <see cref="BinaryReader"/> always reads little endian (regardless of the machine endianness),
    /// so little endian data is read using a <see cref="BinaryReader"/> and big endian data is read using a <see cref="ReversedEndianBinaryReader"/>.
    /// </remarks>
    public static class EndianAwareBinaryReader
    {
        /// <summary>
        /// Creates a BinaryReader that reads data stored in little endian
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="leaveOpen">Optionally leave the <paramref name="stream"/> open when the binary reader is disposed</param>
        /// <returns>A binary reader that reads data stored as little endian</returns>
        public static BinaryReader OpenLittleEndian(Stream stream, bool leaveOpen = false)
            => new BinaryReader(stream, Encoding.UTF8, leaveOpen);

        /// <summary>
        /// Creates a BinaryReader that reads data stored in little endian
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="encoding">The text encoding of the stream</param>
        /// <param name="leaveOpen">Optionally leave the <paramref name="stream"/> open when the binary reader is disposed</param>
        /// <returns>A binary reader that reads data stored as little endian</returns>
        public static BinaryReader OpenLittleEndian(Stream stream, Encoding encoding, bool leaveOpen = false)
            => new BinaryReader(stream, encoding, leaveOpen);

        /// <summary>
        /// Creates a BinaryReader that reads data stored in big endian
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="leaveOpen">Optionally leave the <paramref name="stream"/> open when the binary reader is disposed</param>
        /// <returns>A binary reader that reads data stored as big endian</returns>
        public static BinaryReader OpenBigEndian(Stream stream, bool leaveOpen = false)
            => new ReversedEndianBinaryReader(stream, leaveOpen);

        /// <summary>
        /// Creates a BinaryReader that reads data stored in big endian
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="encoding">The text encoding of the stream</param>
        /// <param name="leaveOpen">Optionally leave the <paramref name="stream"/> open when the binary reader is disposed</param>
        /// <returns>A binary reader that reads data stored as big endian</returns>
        public static BinaryReader OpenBigEndian(Stream stream, Encoding encoding, bool leaveOpen = false)
            => new ReversedEndianBinaryReader(stream, encoding, leaveOpen);

        /// <summary>
        /// Creates a BinaryReader that reads data using the current endian (of the current process)
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="leaveOpen">Optionally leave the <paramref name="stream"/> open when the binary reader is disposed</param>
        /// <returns>A binary reader that reads data stored using the current endian (of the current process)</returns>
        public static BinaryReader Open(Stream stream, bool leaveOpen = false)
            => Open(stream, Endianess.Current, Encoding.UTF8, leaveOpen);

        /// <summary>
        /// Creates a BinaryReader that reads data using the specified endianness (strings and chars are read as UTF-8)
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="endian">What endian to read data in</param>
        /// <param name="leaveOpen">Optionally leave the <paramref name="stream"/> open when the binary reader is disposed</param>
        /// <returns>A binary reader that reads data using the specified endianness</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endian"/> isn't a valid value</exception>
        public static BinaryReader Open(Stream stream, Endianess endian, bool leaveOpen = false)
            => Open(stream, endian, Encoding.UTF8, leaveOpen);

        /// <summary>
        /// Create a BinaryReader that reads data using the specified endianness
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="endian">The desired endianness</param>
        /// <param name="encoding">The text encoding of the stream</param>
        /// <param name="leaveOpen">Optionally leave the <paramref name="stream"/> open when the binary reader is disposed</param>
        /// <returns>A binary reader that reads data using the specified endianness</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endian"/> isn't a valid value</exception>
        public static BinaryReader Open(Stream stream, Endianess endian, Encoding encoding, bool leaveOpen = false)
        {
            switch (endian)
            {
                case Endianess.Current:
                    return BitConverter.IsLittleEndian ? OpenLittleEndian(stream, encoding, leaveOpen) : OpenBigEndian(stream, encoding, leaveOpen);
                case Endianess.Big:
                    return OpenBigEndian(stream, encoding, leaveOpen);
                case Endianess.Little:
                    return OpenLittleEndian(stream, encoding, leaveOpen);
            }
            throw new ArgumentOutOfRangeException(nameof(endian), "Endian " + endian + " is not valid!");
        }
    
    }


    /// <summary>
    /// Represents an endianness
    /// </summary>
    public enum Endianess
    {
        /// <summary>
        /// Use current endianness
        /// </summary>
        Current = 0,
        /// <summary>
        /// Use little endianness
        /// </summary>
        Little,
        /// <summary>
        /// Use big endianness
        /// </summary>
        Big,
    }

}
