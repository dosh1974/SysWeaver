/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop PSD FileType Plugin for Paint.NET
// http://psdplugin.codeplex.com/
//
// This software is provided under the MIT License:
//   Copyright (c) 2006-2007 Frank Blumenberg
//   Copyright (c) 2010-2017 Tao Yue
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PhotoshopFile
{
    public static class Util
    {
        [DebuggerDisplay("Top = {Top}, Bottom = {Bottom}, Left = {Left}, Right = {Right}")]
        public struct RectanglePosition
        {
            public int Top { get; set; }
            public int Bottom { get; set; }
            public int Left { get; set; }
            public int Right { get; set; }
        }

        public static Rectangle IntersectWith(
          this Rectangle thisRect, Rectangle rect)
        {
            thisRect.Intersect(rect);
            return thisRect;
        }

        /////////////////////////////////////////////////////////////////////////// 

        /// <summary>
        /// Fills a buffer with a byte value.
        /// </summary>
        unsafe static public void Fill(byte* ptr, byte* ptrEnd, byte value)
        {
            //  Vectorized, nothing is written if the end is at or before the start
            if (ptrEnd > ptr)
                NativeMemory.Fill(ptr, (nuint)(ptrEnd - ptr), value);
        }

        unsafe static public void Invert(byte* ptr, byte* ptrEnd)
        {
            //  Vectorized (xor with 0xff is the same as a bitwise complement), then the remaining bytes
            var vc = System.Numerics.Vector<byte>.Count;
            while ((ptrEnd - ptr) >= vc)
            {
                var v = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Numerics.Vector<byte>>(ptr);
                System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ptr, ~v);
                ptr += vc;
            }
            while (ptr < ptrEnd)
            {
                *ptr = (byte)(*ptr ^ 0xff);
                ptr++;
            }
        }

        /////////////////////////////////////////////////////////////////////////// 

        /// <summary>
        /// Reverses the endianness of a 2-byte word.
        /// </summary>
        unsafe static public void SwapBytes2(byte* ptr)
        {
            var p1 = ptr + 1;
            var byte0 = *ptr;
            *ptr = *p1;
            *p1 = byte0;
        }

        /////////////////////////////////////////////////////////////////////////// 

        /// <summary>
        /// Reverses the endianness of a 4-byte word.
        /// </summary>
        unsafe static public void SwapBytes4(byte* ptr)
        {
            byte byte0 = *ptr;
            var byte1 = ptr[1];
            *ptr = ptr[3];
            ptr[1] = ptr[2];
            ptr[2] = byte1;
            ptr[3] = byte0;
        }

        /// <summary>
        /// Reverses the endianness of a word of arbitrary length.
        /// </summary>
        unsafe static public void SwapBytes(byte* ptr, int nLength)
        {
            //  Vectorized reverse, nothing is changed for a length less than 2 (also negative)
            if (nLength > 1)
                new Span<byte>(ptr, nLength).Reverse();
        }

        ///////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reverses the endianness of the words of a byte array, the word size is given by the bit depth (1 and 8 bit: nothing is changed).
        /// </summary>
        /// <param name="bitDepth">1, 8, 16 or 32.</param>
        /// <param name="byteArray">Byte array containing the sequence on which to swap endianness</param>
        /// <param name="startIdx">Byte index of the first word to swap</param>
        /// <param name="count">Number of words to swap</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bitDepth"/> is not 1, 8, 16 or 32, or the words are not inside the array.</exception>
        public static void SwapByteArray(int bitDepth,
          byte[] byteArray, int startIdx, int count)
        {
            switch (bitDepth)
            {
                case 1:
                case 8:
                    break;

                case 16:
                    SwapByteArray2(byteArray, startIdx, count);
                    break;

                case 32:
                    SwapByteArray4(byteArray, startIdx, count);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(bitDepth), bitDepth,
                      "Byte-swapping implemented only for 16-bit and 32-bit depths.");
            }
        }

        /// <summary>
        /// Reverses the endianness of 2-byte words in a byte array.
        /// </summary>
        /// <param name="byteArray">Byte array containing the sequence on which to swap endianness</param>
        /// <param name="startIdx">Byte index of the first word to swap</param>
        /// <param name="count">Number of words to swap</param>
        /// <exception cref="ArgumentOutOfRangeException">The words are not inside the array.</exception>
        public static void SwapByteArray2(byte[] byteArray, int startIdx, int count)
        {
            int endIdx = startIdx + count * 2;
            if (byteArray.Length < endIdx)
                throw new ArgumentOutOfRangeException(nameof(count), count, "The words to swap exceed the array length.");
            var s = MemoryMarshal.Cast<Byte, ushort>(byteArray.AsSpan().Slice(startIdx, endIdx - startIdx));
            BinaryPrimitives.ReverseEndianness(s, s);
            /*unsafe
            {
                fixed (byte* arrayPtr = &byteArray[0])
                {
                    byte* ptr = arrayPtr + startIdx;
                    byte* endPtr = arrayPtr + endIdx;
                    while (ptr < endPtr)
                    {
                        SwapBytes2(ptr);
                        ptr += 2;
                    }
                }
            }*/
        }

        /// <summary>
        /// Reverses the endianness of 4-byte words in a byte array.
        /// </summary>
        /// <param name="byteArray">Byte array containing the sequence on which to swap endianness</param>
        /// <param name="startIdx">Byte index of the first word to swap</param>
        /// <param name="count">Number of words to swap</param>
        /// <exception cref="ArgumentOutOfRangeException">The words are not inside the array.</exception>
        public static void SwapByteArray4(byte[] byteArray, int startIdx, int count)
        {
            int endIdx = startIdx + count * 4;
            if (byteArray.Length < endIdx)
                throw new ArgumentOutOfRangeException(nameof(count), count, "The words to swap exceed the array length.");
            var s = MemoryMarshal.Cast<Byte, uint>(byteArray.AsSpan().Slice(startIdx, endIdx - startIdx));
            BinaryPrimitives.ReverseEndianness(s, s);
            /*
            unsafe
            {
                fixed (byte* arrayPtr = &byteArray[0])
                {
                    byte* ptr = arrayPtr + startIdx;
                    byte* endPtr = arrayPtr + endIdx;
                    while (ptr < endPtr)
                    {
                        SwapBytes4(ptr);
                        ptr += 4;
                    }
                }
            }
            */
        }

        /////////////////////////////////////////////////////////////////////////// 

        /// <summary>
        /// Calculates the number of bytes required to store a row of an image
        /// with the specified bit depth.
        /// </summary>
        /// <param name="size">The size of the image in pixels.</param>
        /// <param name="bitDepth">The bit depth of the image.</param>
        /// <returns>The number of bytes needed to store a row of the image.</returns>
        public static int BytesPerRow(Size size, int bitDepth)
        {
            switch (bitDepth)
            {
                case 1:
                    return (size.Width + 7) / 8;
                default:
                    return size.Width * BytesFromBitDepth(bitDepth);
            }
        }

        /// <summary>
        /// Round the integer to a multiple.
        /// Positive values are rounded up, negative values are returned unchanged.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="value"/> and <paramref name="multiple"/> have opposite signs (or <paramref name="multiple"/> is 0 and <paramref name="value"/> is not).</exception>
        public static int RoundUp(int value, int multiple)
        {
            if (value == 0)
                return 0;

            if (Math.Sign(value) != Math.Sign(multiple))
            {
                throw new ArgumentException(
                  $"{nameof(value)} and {nameof(multiple)} cannot have opposite signs.");
            }

            var remainder = value % multiple;
            if (remainder > 0)
            {
                value += (multiple - remainder);
            }
            return value;
        }

        /// <summary>
        /// Get number of bytes required to pad to the specified multiple.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> or <paramref name="padMultiple"/> is negative.</exception>
        /// <exception cref="DivideByZeroException"><paramref name="padMultiple"/> is 0.</exception>
        public static int GetPadding(int length, int padMultiple)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            ArgumentOutOfRangeException.ThrowIfNegative(padMultiple);

            var remainder = length % padMultiple;
            if (remainder == 0)
                return 0;

            var padding = padMultiple - remainder;
            return padding;
        }

        /// <summary>
        /// Returns the number of bytes needed to store a single pixel of the
        /// specified bit depth.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is not 1, 8, 16 or 32.</exception>
        public static int BytesFromBitDepth(int depth)
        {
            switch (depth)
            {
                case 1:
                case 8:
                    return 1;
                case 16:
                    return 2;
                case 32:
                    return 4;
                default:
                    throw new ArgumentOutOfRangeException(nameof(depth), depth, "Invalid bit depth.");
            }
        }

        /// <summary>
        /// The number of color channels of a color mode (1 for Bitmap, Duotone, Grayscale, Indexed and Multichannel, 3 for Lab and RGB, 4 for CMYK).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="colorMode"/> is not a defined color mode.</exception>
        public static short MinChannelCount(this PsdColorMode colorMode)
        {
            switch (colorMode)
            {
                case PsdColorMode.Bitmap:
                case PsdColorMode.Duotone:
                case PsdColorMode.Grayscale:
                case PsdColorMode.Indexed:
                case PsdColorMode.Multichannel:
                    return 1;
                case PsdColorMode.Lab:
                case PsdColorMode.RGB:
                    return 3;
                case PsdColorMode.CMYK:
                    return 4;
            }

            throw new ArgumentOutOfRangeException(nameof(colorMode), colorMode, "Unknown color mode.");
        }

        /// <summary>
        /// Verify that the offset and count will remain within the bounds of the
        /// buffer.
        /// </summary>
        /// <returns>True if in bounds, false if out of bounds.</returns>
        public static bool CheckBufferBounds(byte[] data, int offset, int count)
        {
            if (offset < 0)
                return false;
            if (count < 0)
                return false;
            if (offset + count > data.Length)
                return false;

            return true;
        }

        /// <summary>
        /// Checks that a byte array of the given length can be allocated.
        /// </summary>
        /// <exception cref="PsdInvalidException"><paramref name="length"/> is negative (corrupt length field).</exception>
        /// <exception cref="OutOfMemoryException"><paramref name="length"/> is larger than 0x7fffffc7 (the maximum .NET byte array length).</exception>
        public static void CheckByteArrayLength(long length)
        {
            if (length < 0)
            {
                throw new PsdInvalidException("Byte array cannot have a negative length.");
            }
            if (length > 0x7fffffc7)
            {
                throw new OutOfMemoryException(
                  "Byte array cannot exceed 2,147,483,591 in length.");
            }
        }

        /// <summary>
        /// The maximum size of a layer (or mask) relative to the document: a layer may be bigger than the document,
        /// but not more than this many times wider or higher (protects against huge allocations from corrupt layer rects).
        /// </summary>
        public const int MaxLayerSizeFactor = 5;

        /// <summary>
        /// Checks the size of a layer or channel rect before image data is allocated for it.
        /// </summary>
        /// <param name="size">The layer / channel rect size.</param>
        /// <param name="psdFile">The document.</param>
        /// <exception cref="PsdInvalidException">The width or height is negative, or the width is more than <see cref="MaxLayerSizeFactor"/> times
        /// the document width (<see cref="PsdFile.ColumnCount"/>), or the height more than <see cref="MaxLayerSizeFactor"/> times the document height (<see cref="PsdFile.RowCount"/>).</exception>
        public static void CheckLayerSize(Size size, PsdFile psdFile)
        {
            if ((size.Width < 0) || (size.Height < 0))
            {
                throw new PsdInvalidException("Layer size " + size.Width + " x " + size.Height + " is negative.");
            }
            if (((long)size.Width > (long)MaxLayerSizeFactor * psdFile.ColumnCount) || ((long)size.Height > (long)MaxLayerSizeFactor * psdFile.RowCount))
            {
                throw new PsdInvalidException("Layer size " + size.Width + " x " + size.Height + " is more than " + MaxLayerSizeFactor + " times the image size " + psdFile.ColumnCount + " x " + psdFile.RowCount + ".");
            }
        }

        /// <summary>
        /// Writes a message to the debug console, indicating the current position
        /// in the stream in both decimal and hexadecimal formats.
        /// </summary>
        [Conditional("DEBUG")]
        public static void DebugMessage(Stream stream, string message)
        {
            //Debug.WriteLine($"0x{stream.Position:x}, {stream.Position}, {message}");
        }
    }

    /// <summary>
    /// Fixed-point decimal, with 16-bit integer and 16-bit fraction.
    /// </summary>
    public class UFixed16_16
    {
        public UInt16 Integer { get; set; }
        public UInt16 Fraction { get; set; }

        public UFixed16_16(UInt16 integer, UInt16 fraction)
        {
            Integer = integer;
            Fraction = fraction;
        }

        /// <summary>
        /// Split the high and low words of a 32-bit unsigned integer into a
        /// fixed-point number.
        /// </summary>
        public UFixed16_16(UInt32 value)
        {
            Integer = (UInt16)(value >> 16);
            Fraction = (UInt16)(value & 0x0000ffff);
        }

        /// <summary>
        /// Convert a double to the nearest fixed-point number.
        /// </summary>
        /// <exception cref="OverflowException"><paramref name="value"/> is negative or rounds to 65536 or more.</exception>
        public UFixed16_16(double value)
        {
            if (value >= 65536.0) throw new OverflowException();
            if (value < 0) throw new OverflowException();

            // Round instead of truncate, because doubles may not represent the
            // fraction exactly.  The whole 16.16 value is rounded, so that a
            // fraction that rounds up to 1 carries into the integer part.
            var fixedValue = Math.Floor(value * 65536 + 0.5);
            if (fixedValue > UInt32.MaxValue) throw new OverflowException();
            var bits = (UInt32)fixedValue;
            Integer = (UInt16)(bits >> 16);
            Fraction = (UInt16)(bits & 0x0000ffff);
        }

        public static implicit operator double(UFixed16_16 value)
        {
            return (double)value.Integer + value.Fraction / 65536.0;
        }

    }

}
