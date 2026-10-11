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
using System.Diagnostics;

namespace PhotoshopFile
{
  [DebuggerDisplay("Layer Info: { key }")]
  public class RawLayerInfo : LayerInfo
  {
    private string signature;
    public override string Signature => signature;

    private string key;
    public override string Key => key;

    /// <summary>
    /// The layer info data (without the signature, key and length), saved as is.
    /// </summary>
    public byte[] Data { get; private set; }

    /// <summary>
    /// Create a layer info with no data (an empty <see cref="Data"/> array).
    /// </summary>
    /// <param name="key">The 4 character key.</param>
    /// <param name="signature">The signature, "8BIM" or "8B64".</param>
    public RawLayerInfo(string key, string signature = "8BIM")
      : this(key, Array.Empty<byte>(), signature)
    {
    }

    /// <summary>
    /// Create a layer info with data.
    /// </summary>
    /// <param name="key">The 4 character key.</param>
    /// <param name="data">The data, saved as is (not copied).</param>
    /// <param name="signature">The signature, "8BIM" or "8B64".</param>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
    public RawLayerInfo(string key, byte[] data, string signature = "8BIM")
    {
      ArgumentNullException.ThrowIfNull(data);
      this.signature = signature;
      this.key = key;
      Data = data;
    }

    public RawLayerInfo(PsdBinaryReader reader, string signature, string key,
      long dataLength)
    {
      this.signature = signature;
      this.key = key;

      Util.CheckByteArrayLength(dataLength);
      Data = reader.ReadBytes((int)dataLength);
    }

    protected override void WriteData(PsdBinaryWriter writer)
    {
      writer.Write(Data);
    }
  }
}
