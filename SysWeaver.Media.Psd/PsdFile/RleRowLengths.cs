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
using System.Linq;

namespace PhotoshopFile
{
  /// <summary>
  /// The compressed length (in bytes) of each row of an RLE compressed channel.
  /// </summary>
  public class RleRowLengths
  {
    /// <summary>
    /// The compressed length of each row.
    /// </summary>
    public int[] Values { get; private set; }

    /// <summary>
    /// The sum of all row lengths.
    /// </summary>
    public long Total => Sum(Values);

    /// <summary>
    /// Same as values.Sum(x => (long)x) (checked, null throws an ArgumentNullException) without the delegate
    /// </summary>
    static long Sum(int[] values)
    {
      ArgumentNullException.ThrowIfNull(values, "source");
      long s = 0;
      foreach (var x in values)
        s = checked(s + x);
      return s;
    }

    public int this[int i]
    {
      get => Values[i];
      set => Values[i] = value;
    }

    /// <summary>
    /// Create row lengths (all 0) for <paramref name="rowCount"/> rows.
    /// </summary>
    public RleRowLengths(int rowCount)
    {
      Values = new int[rowCount];
    }

    /// <summary>
    /// Read the row lengths: big-endian UInt16 values (PSD) or Int32 values (PSB, <paramref name="isLargeDocument"/>).
    /// </summary>
    /// <exception cref="PsdInvalidException"><paramref name="rowCount"/> is negative.</exception>
    /// <exception cref="System.IO.EndOfStreamException">The stream is too short.</exception>
    public RleRowLengths(PsdBinaryReader reader, int rowCount, bool isLargeDocument)
      : this(CheckRowCount(reader, rowCount, isLargeDocument))
    {
      for (int i = 0; i < rowCount; i++)
      {
        Values[i] = isLargeDocument
          ? reader.ReadInt32()
          : reader.ReadUInt16();
      }
    }

    /// <summary>
    /// Validate a row count read from a file before the row lengths array is allocated.
    /// </summary>
    static int CheckRowCount(PsdBinaryReader reader, int rowCount, bool isLargeDocument)
    {
      if (rowCount < 0)
        throw new PsdInvalidException("Negative RLE row count (" + rowCount + ").");
      reader.CheckAvailable((long)rowCount * (isLargeDocument ? 4 : 2));
      return rowCount;
    }

    /// <summary>
    /// Write the row lengths: big-endian UInt16 values (PSD) or Int32 values (PSB, <paramref name="isLargeDocument"/>).
    /// </summary>
    public void Write(PsdBinaryWriter writer, bool isLargeDocument)
    {
      for (int i = 0; i < Values.Length; i++)
      {
        if (isLargeDocument)
        {
          writer.Write(Values[i]);
        }
        else
        {
          writer.Write((UInt16)Values[i]);
        }
      }
    }
  }

}
