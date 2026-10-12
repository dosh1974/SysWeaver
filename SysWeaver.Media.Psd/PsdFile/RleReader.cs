/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop PSD FileType Plugin for Paint.NET
// http://psdplugin.codeplex.com/
//
// This software is provided under the MIT License:
//   Copyright (c) 2006-2007 Frank Blumenberg
//   Copyright (c) 2010-2013 Tao Yue
//
// Portions of this file are provided under the BSD 3-clause License:
//   Copyright (c) 2006, Jonas Beckeman
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;


namespace PhotoshopFile
{
  /// <summary>
  /// Decodes PackBits RLE data from a stream.
  /// </summary>
  public class RleReader
  {
    private Stream stream;

    /// <summary>
    /// Create a decoder reading from the current position of a stream.
    /// </summary>
    /// <param name="stream">The stream with the RLE data.</param>
    public RleReader(Stream stream)
    {
      this.stream = stream;
    }

    /// <summary>
    /// Decodes a PackBits RLE stream.
    /// A flag byte n of 0..127 is followed by n + 1 literal bytes, -1..-127 by one byte repeated 1 - n times, -128 is skipped.
    /// </summary>
    /// <param name="buffer">Output buffer for decoded data.</param>
    /// <param name="offset">Offset at which to begin writing.</param>
    /// <param name="count">Number of bytes to decode from the stream.</param>
    /// <returns><paramref name="count"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="count"/> is negative, or their sum exceeds the buffer length.</exception>
    /// <exception cref="RleException">A packet would decode past <paramref name="count"/> bytes.</exception>
    /// <exception cref="EndOfStreamException">The stream ends before <paramref name="count"/> bytes are decoded.</exception>
    unsafe public int Read(byte[] buffer, int offset, int count)
    {
      if (!Util.CheckBufferBounds(buffer, offset, count))
        throw new ArgumentOutOfRangeException(nameof(count), "The offset and count must be inside the buffer.");

      // Pin the entire buffer now, so that we don't keep pinning and unpinning
      // for each RLE packet.
      fixed (byte* ptrBuffer = buffer)
      {
        int bytesLeft = count;
        int bufferIdx = offset;
        while (bytesLeft > 0)
        {
          // ReadByte returns an unsigned byte, but we want a signed byte.
          var flagByte = stream.ReadByte();
          if (flagByte < 0)
            throw new EndOfStreamException("RLE data ended before " + count + " bytes were decoded.");
          var flagCounter = unchecked((sbyte)flagByte);

          // Raw packet
          if (flagCounter > 0)
          {
            var readLength = flagCounter + 1;
            if (bytesLeft < readLength)
              throw new RleException("Raw packet overruns the decode window.");

            stream.ReadExactly(buffer, bufferIdx, readLength);

            bufferIdx += readLength;
            bytesLeft -= readLength;
          }
          // RLE packet
          else if (flagCounter > -128)
          {
            var runLength = 1 - flagCounter;
            var nextByte = stream.ReadByte();
            if (nextByte < 0)
              throw new EndOfStreamException("RLE data ended before " + count + " bytes were decoded.");
            var byteValue = (byte)nextByte;
            if (runLength > bytesLeft)
              throw new RleException("RLE packet overruns the decode window.");

            //  Vectorized fill (the run length is 2..128 and inside the decode window)
            new Span<byte>(ptrBuffer + bufferIdx, runLength).Fill(byteValue);

            bufferIdx += runLength;
            bytesLeft -= runLength;
          }
          else
          {
            // The canonical PackBits algorithm will never emit 0x80 (-128), but
            // some programs do.  Simply skip over the byte.
          }
        }

        Debug.Assert(bytesLeft == 0);
        return count - bytesLeft;
      }
    }
  }
}
