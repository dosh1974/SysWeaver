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
using System.Drawing;
using System.IO;
using System.IO.Compression;

namespace PhotoshopFile.Compression
{
  /// <summary>
  /// ZIP (zlib header + deflate) compressed image data, without prediction.
  /// An instance is either a decompressor (created with data) or a compressor (created with null data).
  /// </summary>
  public class ZipImage : ImageData
  {
    private MemoryStream zipDataStream;
    private Stream zipStream;
    private bool hasData;

    protected override bool AltersWrittenData => false;

    /// <summary>
    /// Create a decompressor or a compressor.
    /// </summary>
    /// <param name="data">The compressed data (starting with the 2 byte zlib header, which is skipped, the Adler-32 trailer is not checked),
    /// or null to create a compressor.
    /// A compressor writes a complete zlib (RFC 1950) stream: header, deflate data and Adler-32 checksum (nothing at all for empty image data).</param>
    /// <param name="size">The image size in pixels.</param>
    /// <param name="bitDepth">The bit depth (1, 8, 16 or 32), this class does not change the byte order.</param>
    /// <exception cref="ArgumentException"><paramref name="bitDepth"/> is invalid.</exception>
    public ZipImage(byte[] data, Size size, int bitDepth)
      : base(size, bitDepth)
    {
      if (data == null)
      {
        InitCompress();
      }
      else
      {
        InitDecompress(data);
      }
    }

    private void InitCompress()
    {
      zipDataStream = new MemoryStream();

      // ZLibStream writes the zlib (RFC 1950) header and the Adler-32 trailer
      zipStream = new ZLibStream(zipDataStream, CompressionMode.Compress,
        true);
    }

    private void InitDecompress(byte[] data)
    {
      zipDataStream = new MemoryStream(data);

      // Skip the 2 byte zlib (RFC 1950) header and decode the raw deflate data,
      // a missing or wrong Adler-32 trailer is tolerated (older versions of this library wrote none).
      zipDataStream.ReadByte();
      zipDataStream.ReadByte();
      zipStream = new DeflateStream(zipDataStream, CompressionMode.Decompress,
        true);
    }

    internal override void Read(byte[] buffer)
    {
      var bytesToRead = (long)Size.Height * BytesPerRow;
      Util.CheckByteArrayLength(bytesToRead);

      // DeflateStream.Read may return fewer bytes than requested, read until done.
      var bytesRead = zipStream.ReadAtLeast(buffer.AsSpan(0, (int)bytesToRead), (int)bytesToRead, false);
      if (bytesRead != bytesToRead)
      {
        throw new InvalidDataException("ZIP stream was not fully decompressed.");
      }
    }

    public override byte[] ReadCompressed()
    {
      // Write out the last block.  (Flush leaves the last block open.)
      zipStream.Close();

      // Do not write anything when the image data is empty
      var result = !hasData
        ? new byte[0]
        : zipDataStream.ToArray();
      return result;
    }

    internal override void WriteInternal(byte[] array)
    {
      hasData |= (array.Length > 0);
      zipStream.Write(array, 0, array.Length);
    }
  }
}
