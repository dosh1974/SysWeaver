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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PhotoshopFile.Compression;

namespace PhotoshopFile
{
  public enum PsdColorMode
  {
    Bitmap = 0,
    Grayscale = 1,
    Indexed = 2,
    RGB = 3,
    CMYK = 4,
    Multichannel = 7,
    Duotone = 8,
    Lab = 9
  };

  public enum PsdFileVersion : short
  {
    Psd = 1,
    PsbLargeDocument = 2
  }

  public class PsdFile
  {
    #region Constructors

    /// <summary>
    /// Create an empty document (no layers, no image resources, no channels, 0 x 0 pixels).
    /// Set <see cref="ChannelCount"/>, <see cref="RowCount"/>, <see cref="ColumnCount"/>, <see cref="BitDepth"/>, <see cref="ColorMode"/>
    /// and the <see cref="BaseLayer"/> channels before saving.
    /// </summary>
    /// <param name="version">PSD or PSB (large document) format.</param>
    public PsdFile(PsdFileVersion version = PsdFileVersion.Psd)
    {
      Version = version;

      BaseLayer = new Layer(this);
      ImageResources = new ImageResources();
      Layers = new List<Layer>();
      AdditionalInfo = new List<LayerInfo>();
    }

    /// <summary>
    /// Load a PSD or PSB file.
    /// The image data stays compressed, call <see cref="Channel.DecodeImageData"/> to decompress a channel.
    /// </summary>
    /// <param name="filename">The file to load.</param>
    /// <param name="loadContext">The Pascal string encoding and load callbacks.</param>
    /// <exception cref="PsdInvalidException">The file is not a valid PSD file (bad signature, version or structure).</exception>
    /// <exception cref="System.IO.EndOfStreamException">The file is truncated.</exception>
    /// <exception cref="ArgumentException">A header value (channel count, dimension, bit depth) is out of range.</exception>
    public PsdFile(string filename, LoadContext loadContext)
      : this()
    {
      using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
      { 
        Load(stream, loadContext);
      }
    }

    /// <summary>
    /// Load a PSD or PSB file from the current position of a stream (the stream is not closed).
    /// The image data stays compressed, call <see cref="Channel.DecodeImageData"/> to decompress a channel.
    /// </summary>
    /// <param name="stream">The stream to read, must be seekable.</param>
    /// <param name="loadContext">The Pascal string encoding and load callbacks.</param>
    /// <exception cref="PsdInvalidException">The data is not a valid PSD file (bad signature, version or structure).</exception>
    /// <exception cref="System.IO.EndOfStreamException">The data is truncated.</exception>
    /// <exception cref="ArgumentException">A header value (channel count, dimension, bit depth) is out of range.</exception>
    public PsdFile(Stream stream, LoadContext loadContext)
      : this()
    {
      Load(stream, loadContext);
    }

    #endregion

    #region Load and save

    internal LoadContext LoadContext { get; private set; }

    private void Load(Stream stream, LoadContext loadContext)
    {
      LoadContext = loadContext;
      var reader = new PsdBinaryReader(stream, loadContext.Encoding);

      LoadHeader(reader);
      LoadColorModeData(reader);
      LoadImageResources(reader);
      LoadLayerAndMaskInfo(reader);

      LoadImage(reader);
      //DecompressImages();
    }

    /// <summary>
    /// Save the document to a file (created or overwritten), see <see cref="Save(Stream, Encoding)"/>.
    /// </summary>
    public void Save(string fileName, Encoding encoding)
    {
      using (var stream = new FileStream(fileName, FileMode.Create))
      {
        Save(stream, encoding);
      }
    }

    /// <summary>
    /// Save the document to a seekable stream (the stream is not closed).
    /// Calls <see cref="PrepareSave"/> first, which compresses all channels that have <see cref="Channel.ImageData"/> but no <see cref="Channel.ImageDataRaw"/>.
    /// </summary>
    /// <param name="stream">The output stream, must be seekable (block lengths are patched after writing a block).</param>
    /// <param name="encoding">The encoding used for Pascal strings (layer names, resource names).</param>
    public void Save(Stream stream, Encoding encoding)
    {
      PrepareSave();

      using (var writer = new PsdBinaryWriter(stream, encoding))
      {
        SaveHeader(writer);
        SaveColorModeData(writer);
        SaveImageResources(writer);
        SaveLayerAndMaskInfo(writer);
        SaveImage(writer);
      }
    }

    #endregion

    #region Header

    /// <summary>
    /// Photoshop file format version.
    /// </summary>
    public PsdFileVersion Version { get; private set; }

    public bool IsLargeDocument =>
      (Version == PsdFileVersion.PsbLargeDocument);

    private Int16 channelCount;
    /// <summary>
    /// The number of channels in the image, including any alpha channels.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not 1 to 56.</exception>
    public Int16 ChannelCount
    {
      get => channelCount;
      set
      {
        if (value < 1 || value > 56)
          throw new ArgumentOutOfRangeException(nameof(value), value, "Number of channels must be from 1 to 56.");
        channelCount = value;
      }
    }

    private void CheckDimension(int dimension)
    {
      if (dimension < 1)
      {
        throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Image dimension must be at least 1.");
      }
      if ((Version == PsdFileVersion.Psd) && (dimension > 30000))
      {
        throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "PSD image dimension cannot exceed 30000.");
      }
      if ((Version == PsdFileVersion.PsbLargeDocument) && (dimension > 300000))
      {
        throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "PSB image dimension cannot exceed 300000.");
      }
    }

    /// <summary>
    /// The height of the image in pixels (the height of <see cref="BaseLayer"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1, or larger than 30000 (PSD) / 300000 (PSB).</exception>
    public int RowCount
    {
      get => this.BaseLayer.Rect.Height;
      set
      {
        CheckDimension(value);
        BaseLayer.Rect = new Rectangle(0, 0, BaseLayer.Rect.Width, value);
      }
    }


    /// <summary>
    /// The width of the image in pixels (the width of <see cref="BaseLayer"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1, or larger than 30000 (PSD) / 300000 (PSB).</exception>
    public int ColumnCount
    {
      get => this.BaseLayer.Rect.Width;
      set
      {
        CheckDimension(value);
        BaseLayer.Rect = new Rectangle(0, 0, value, BaseLayer.Rect.Height);
      }
    }

    private int bitDepth;
    /// <summary>
    /// The number of bits per channel. Supported values are 1, 8, 16, and 32.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not 1, 8, 16 or 32.</exception>
    public int BitDepth
    {
      get => bitDepth;
      set
      {
        switch (value)
        {
          case 1:
          case 8:
          case 16:
          case 32:
            bitDepth = value;
            break;
          default:
            throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid bit depth (supported: 1, 8, 16 and 32).");
        }
      }
    }

    /// <summary>
    /// The color mode of the file.
    /// </summary>
    public PsdColorMode ColorMode { get; set; }

    ///////////////////////////////////////////////////////////////////////////

    private void LoadHeader(PsdBinaryReader reader)
    {
      Util.DebugMessage(reader.BaseStream, "Load, Begin, File header");

      var signature = reader.ReadAsciiChars(4);
      if (signature != "8BPS")
        throw new PsdInvalidException("The given stream is not a valid PSD file");

      Version = (PsdFileVersion)reader.ReadInt16();
      Util.DebugMessage(reader.BaseStream, $"Load, Info, Version {(int)Version}");
      if ((Version != PsdFileVersion.Psd)
        && (Version != PsdFileVersion.PsbLargeDocument))
      {
        throw new PsdInvalidException("The PSD file has an unknown version");
      }

      //6 bytes reserved
      reader.BaseStream.Position += 6;

      this.ChannelCount = reader.ReadInt16();
      this.RowCount = reader.ReadInt32();
      this.ColumnCount = reader.ReadInt32();
      BitDepth = reader.ReadInt16();
      ColorMode = (PsdColorMode)reader.ReadInt16();

      Util.DebugMessage(reader.BaseStream, "Load, End, File header");
    }

    ///////////////////////////////////////////////////////////////////////////

    private void SaveHeader(PsdBinaryWriter writer)
    {
      Util.DebugMessage(writer.BaseStream, "Save, Begin, File header");

      string signature = "8BPS";
      writer.WriteAsciiChars(signature);
      writer.Write((Int16)Version);
      writer.Write(new byte[] { 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, });
      writer.Write(ChannelCount);
      writer.Write(RowCount);
      writer.Write(ColumnCount);
      writer.Write((Int16)BitDepth);
      writer.Write((Int16)ColorMode);

      Util.DebugMessage(writer.BaseStream, "Save, End, File header");
    }

    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region ColorModeData

    /// <summary>
    /// If ColorMode is ColorModes.Indexed, the following 768 bytes will contain 
    /// a 256-color palette. If the ColorMode is ColorModes.Duotone, the data 
    /// following presumably consists of screen parameters and other related information. 
    /// Unfortunately, it is intentionally not documented by Adobe, and non-Photoshop 
    /// readers are advised to treat duotone images as gray-scale images.
    /// </summary>
    public byte[] ColorModeData = new byte[0];

    private void LoadColorModeData(PsdBinaryReader reader)
    {
      Util.DebugMessage(reader.BaseStream, "Load, Begin, ColorModeData");

      var paletteLength = reader.ReadUInt32();
      if (paletteLength > 0)
      {
        ColorModeData = reader.ReadBytes((int)paletteLength);
      }

      Util.DebugMessage(reader.BaseStream, "Load, End, ColorModeData");
    }

    private void SaveColorModeData(PsdBinaryWriter writer)
    {
      Util.DebugMessage(writer.BaseStream, "Save, Begin, ColorModeData");

      writer.Write((UInt32)ColorModeData.Length);
      writer.Write(ColorModeData);

      Util.DebugMessage(writer.BaseStream, "Save, End, ColorModeData");
    }

    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region ImageResources

    /// <summary>
    /// The Image resource blocks for the file
    /// </summary>
    public ImageResources ImageResources { get; set; }

    public ResolutionInfo Resolution
    {
      get => (ResolutionInfo)ImageResources.Get(ResourceID.ResolutionInfo);
      set => ImageResources.Set(value);
    }


    ///////////////////////////////////////////////////////////////////////////

    private void LoadImageResources(PsdBinaryReader reader)
    {
      Util.DebugMessage(reader.BaseStream, "Load, Begin, ImageResources");

      var imageResourcesLength = reader.ReadUInt32();
      if (imageResourcesLength <= 0)
        return;

      var startPosition = reader.BaseStream.Position;
      var endPosition = startPosition + imageResourcesLength;
      while (reader.BaseStream.Position < endPosition)
      {
        var imageResource = ImageResourceFactory.CreateImageResource(reader);
        ImageResources.Add(imageResource);
      }

      Util.DebugMessage(reader.BaseStream, "Load, End, ImageResources");

      //-----------------------------------------------------------------------
      // make sure we are not on a wrong offset, so set the stream position 
      // manually
      reader.BaseStream.Position = startPosition + imageResourcesLength;
    }

    ///////////////////////////////////////////////////////////////////////////

    private void SaveImageResources(PsdBinaryWriter writer)
    {
      Util.DebugMessage(writer.BaseStream, "Save, Begin, ImageResources");

      using (new PsdBlockLengthWriter(writer))
      {
        foreach (var imgRes in ImageResources)
          imgRes.Save(writer);
      }

      Util.DebugMessage(writer.BaseStream, "Save, End, ImageResources");
    }

    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region LayerAndMaskInfo

    public List<Layer> Layers { get; private set; }

    public List<LayerInfo> AdditionalInfo { get; private set; }

    public bool AbsoluteAlpha { get; set; }

    ///////////////////////////////////////////////////////////////////////////

    private void LoadLayerAndMaskInfo(PsdBinaryReader reader)
    {
      Util.DebugMessage(reader.BaseStream, "Load, Begin, Layer and mask info");

      var layersAndMaskLength = IsLargeDocument
        ? reader.ReadInt64()
        : reader.ReadUInt32();
      if (layersAndMaskLength <= 0)
        return;

      var startPosition = reader.BaseStream.Position;
      var endPosition = startPosition + layersAndMaskLength;

      LoadLayers(reader, true);
      LoadGlobalLayerMask(reader);

      //-----------------------------------------------------------------------
      // Load Additional Layer Information

      while (reader.BaseStream.Position < endPosition)
      {
        var info = LayerInfoFactory.Load(reader,
          psdFile: this,
          globalLayerInfo: true);
        AdditionalInfo.Add(info);

        if (info is RawLayerInfo layerInfo)
        {
          switch (layerInfo.Key)
          {
            case "LMsk":
              GlobalLayerMaskData = layerInfo.Data;
              break;
          }
        }
      }

      Util.DebugMessage(reader.BaseStream, "Load, End, Layer and mask info");

      //-----------------------------------------------------------------------
      // make sure we are not on a wrong offset, so set the stream position 
      // manually
      reader.BaseStream.Position = startPosition + layersAndMaskLength;
    }

    ///////////////////////////////////////////////////////////////////////////

    private void SaveLayerAndMaskInfo(PsdBinaryWriter writer)
    {
      Util.DebugMessage(writer.BaseStream, "Save, Begin, Layer and mask info");

      using (new PsdBlockLengthWriter(writer, IsLargeDocument))
      {
        var startPosition = writer.BaseStream.Position;

        SaveLayers(writer);
        SaveGlobalLayerMask(writer);
        
        foreach (var info in AdditionalInfo)
        {
          info.Save(writer,
            globalLayerInfo: true,
            isLargeDocument: IsLargeDocument);
        }

        writer.WritePadding(startPosition, 2);
      }

      Util.DebugMessage(writer.BaseStream, "Save, End, Layer and mask info");
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Load Layers Info section, including image data.
    /// </summary>
    /// <param name="reader">PSD reader.</param>
    /// <param name="hasHeader">Whether the Layers Info section has a length header.</param>
    internal void LoadLayers(PsdBinaryReader reader, bool hasHeader)
    {
      Util.DebugMessage(reader.BaseStream, "Load, Begin, Layers Info section");

      long sectionLength = 0;
      if (hasHeader)
      {
        sectionLength = IsLargeDocument
          ? reader.ReadInt64()
          : reader.ReadUInt32();

        if (sectionLength <= 0)
        {
          // The callback may take action when there are 0 layers, so it must
          // be called even though the Layers Info section is empty.
          LoadContext.OnLoadLayersHeader(this);
          Util.DebugMessage(reader.BaseStream, "Load, End, Layers Info section");
          return;
        }
      }

      var startPosition = reader.BaseStream.Position;
      var numLayers = reader.ReadInt16();

      // If numLayers < 0, then number of layers is absolute value,
      // and the first alpha channel contains the transparency data for
      // the merged result.
      if (numLayers < 0)
      {
        AbsoluteAlpha = true;
        numLayers = Math.Abs(numLayers);
      }

      for (int i = 0; i < numLayers; i++)
      {
        var layer = new Layer(reader, this);
        Layers.Add(layer);
      }

      // Header is complete just before loading pixel data
      LoadContext.OnLoadLayersHeader(this);

      //-----------------------------------------------------------------------

      // Load image data for all channels.
      foreach (var layer in Layers)
      {
        Util.DebugMessage(reader.BaseStream,
          $"Load, Begin, Layer image, {layer.Name}");
        foreach (var channel in layer.Channels)
        {
          channel.LoadPixelData(reader);
        }
        Util.DebugMessage(reader.BaseStream,
          $"Load, End, Layer image, {layer.Name}");
      }

      // Length is set to 0 when called on higher bitdepth layers.
      if (sectionLength > 0)
      {
        // Layers Info section is documented to be even-padded, but Photoshop
        // actually pads to 4 bytes.
        var endPosition = startPosition + sectionLength;
        // Unread bytes (more padding than expected) are skipped below.
        if (reader.BaseStream.Position > endPosition)
          throw new PsdInvalidException("The layers and their image data are longer than the Layers Info section.");

        if (reader.BaseStream.Position < endPosition)
          reader.BaseStream.Position = endPosition;
      }

      Util.DebugMessage(reader.BaseStream, "Load, End, Layers");
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Decompress the document image data and all the layers' image data, in parallel.
    /// </summary>
    private void DecompressImages()
    {
      var layersAndComposite = Layers.Concat(new[] { BaseLayer });
      var channels = layersAndComposite.SelectMany(x => x.Channels);
      Parallel.ForEach(channels, channel =>
      {
        channel.DecodeImageData();
      });

      foreach (var layer in Layers)
      {
        foreach (var channel in layer.Channels)
        {
          if (channel.ID == -2)
            layer.Masks.LayerMask.ImageData = channel.ImageData;
          else if (channel.ID == -3)
            layer.Masks.UserMask.ImageData = channel.ImageData;
        }
      }
    }

    /// <summary>
    /// Check the validity of the PSD file and generate necessary data.
    /// The composite channels are converted to the compression that is saved (raw or RLE, see <see cref="ImageCompression"/>),
    /// then all channels without <see cref="Channel.ImageDataRaw"/> are compressed.
    /// </summary>
    public void PrepareSave()
    {
      CheckDimension(ColumnCount);
      CheckDimension(RowCount);
      VerifyInfoLayers();
      VerifyLayerSections();
      PrepareSaveComposite();

      var imageLayers = Layers.Concat(new List<Layer>() { this.BaseLayer }).ToList();

      foreach (var layer in imageLayers)
      {
        layer.PrepareSave();
      }

      SetVersionInfo();
    }

    /// <summary>
    /// Verifies that any Additional Info layers are consistent.
    /// </summary>
    private void VerifyInfoLayers()
    {
      var infoLayersCount = AdditionalInfo.Count(x => x is InfoLayers);
      if (infoLayersCount > 1)
      {
        throw new PsdInvalidException(
          $"Cannot have more than one {nameof(InfoLayers)} in a PSD file.");
      }
      if ((infoLayersCount > 0) && (Layers.Count == 0))
      {
        throw new PsdInvalidException(
          $"{nameof(InfoLayers)} cannot exist when there are 0 layers.");
      }
    }

    /// <summary>
    /// Verify validity of layer sections.  Each start marker should have a
    /// matching end marker.
    /// </summary>
    internal void VerifyLayerSections()
    {
      int depth = 0;
      foreach (var layer in Enumerable.Reverse(Layers))
      {
        var layerSectionInfo = layer.AdditionalInfo.SingleOrDefault(
          x => x is LayerSectionInfo);
        if (layerSectionInfo == null)
          continue;

        var sectionInfo = (LayerSectionInfo)layerSectionInfo;
        switch (sectionInfo.SectionType)
        {
          case LayerSectionType.Layer:
            break;

          case LayerSectionType.OpenFolder:
          case LayerSectionType.ClosedFolder:
            depth++;
            break;

          case LayerSectionType.SectionDivider:
            depth--;
            if (depth < 0)
              throw new PsdInvalidException("Layer section ended without matching start marker.");
            break;

          default:
            throw new PsdInvalidException("Unrecognized layer section type.");
        }
      }

      if (depth != 0)
        throw new PsdInvalidException("Layer section not closed by end marker.");
    }

    /// <summary>
    /// Set the VersionInfo resource on the file.
    /// </summary>
    public void SetVersionInfo()
    {
      var versionInfo = (VersionInfo)ImageResources.Get(ResourceID.VersionInfo);
      if (versionInfo == null)
      {
        versionInfo = new VersionInfo();
        ImageResources.Set(versionInfo);
        
        // Get the version string.  We don't use the fourth part (revision).
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var versionString = version.Major + "." + version.Minor + "." + version.Build;

        // Strings are not localized since they are not shown to the user.
        versionInfo.Version = 1;
        versionInfo.HasRealMergedData = true;
        versionInfo.ReaderName = "Paint.NET PSD Plugin";
        versionInfo.WriterName = "Paint.NET PSD Plugin " + versionString;
        versionInfo.FileVersion = 1;
      }
    }


    /// <summary>
    /// Saves the Layers Info section, including headers and padding.
    /// </summary>
    /// <param name="writer">The PSD writer.</param>
    internal void SaveLayers(PsdBinaryWriter writer)
    {
      Util.DebugMessage(writer.BaseStream, "Save, Begin, Layers Info section");

      using (new PsdBlockLengthWriter(writer, IsLargeDocument))
      {
        var startPosition = writer.BaseStream.Position;

        // Only one set of Layers can exist in the file.  If layers will be
        // written to the Additional Info section, then the Layers section
        // must be empty to avoid conflict.
        var hasInfoLayers = AdditionalInfo.Exists(x => x is InfoLayers);
        if (!hasInfoLayers)
        {
          SaveLayersData(writer);
        }

        // Documentation states that the Layers Info section is even-padded,
        // but it is actually padded to a multiple of 4.
        writer.WritePadding(startPosition, 4);
      }

      Util.DebugMessage(writer.BaseStream, "Save, End, Layers Info section");
    }

    /// <summary>
    /// Saves the layer data, excluding headers and padding.
    /// </summary>
    /// <param name="writer">The PSD writer.</param>
    internal void SaveLayersData(PsdBinaryWriter writer)
    {
      Util.DebugMessage(writer.BaseStream, "Save, Begin, Layers");

      var numLayers = (Int16)Layers.Count;
      if (AbsoluteAlpha)
      {
        numLayers = (Int16)(-numLayers);
      }

      // Photoshop will not load files that have a layer count of 0 in the
      // compatible Layers section.  Instead, the Layers section must be
      // entirely empty.
      if (numLayers == 0)
      {
        return;
      }

      writer.Write(numLayers);
      
      foreach (var layer in Layers)
      {
        layer.Save(writer);
      }
      
      foreach (var layer in Layers)
      {
        Util.DebugMessage(writer.BaseStream,
          $"Save, Begin, Layer image, {layer.Name}");
        foreach (var channel in layer.Channels)
        {
          channel.SavePixelData(writer);
        }
        Util.DebugMessage(writer.BaseStream,
          $"Save, End, Layer image, {layer.Name}");
      }

      // The caller is responsible for padding.  Photoshop writes padded
      // lengths for compatible layers, but unpadded lengths for Additional
      // Info layers.

      Util.DebugMessage(writer.BaseStream, "Save, End, Layers");
    }

    ///////////////////////////////////////////////////////////////////////////

    byte[] GlobalLayerMaskData = new byte[0];

    private void LoadGlobalLayerMask(PsdBinaryReader reader)
    {
      Util.DebugMessage(reader.BaseStream, "Load, Begin, GlobalLayerMask");

      var maskLength = reader.ReadUInt32();
      if (maskLength <= 0)
      {
        Util.DebugMessage(reader.BaseStream, "Load, End, GlobalLayerMask");
        return;
      }

      GlobalLayerMaskData = reader.ReadBytes((int)maskLength);

      Util.DebugMessage(reader.BaseStream, "Load, End, GlobalLayerMask");
    }

    ///////////////////////////////////////////////////////////////////////////

    private void SaveGlobalLayerMask(PsdBinaryWriter writer)
    {
      Util.DebugMessage(writer.BaseStream, "Save, Begin, GlobalLayerMask");

      if (AdditionalInfo.Exists(x => x.Key == "LMsk"))
      {
        writer.Write((UInt32)0);
        Util.DebugMessage(writer.BaseStream, "Save, End, GlobalLayerMask");
        return;
      }

      writer.Write((UInt32)GlobalLayerMaskData.Length);
      writer.Write(GlobalLayerMaskData);

      Util.DebugMessage(writer.BaseStream, "Save, End, GlobalLayerMask");
    }

    ///////////////////////////////////////////////////////////////////////////

    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region Composite image

    /// <summary>
    /// Represents the composite image.
    /// </summary>
    public Layer BaseLayer { get; set; }

    /// <summary>
    /// The compression of the composite image (<see cref="BaseLayer"/>).
    /// Loading sets the value stored in the file (raw, RLE, or ZIP with or without prediction).
    /// Saving writes the composite raw if this is <see cref="ImageCompression.Raw"/>, else RLE compressed (like Photoshop, which never writes a ZIP composite,
    /// so <see cref="ImageCompression.Zip"/> and <see cref="ImageCompression.ZipPrediction"/> are saved as RLE).
    /// The composite channels are converted to that compression when saving (<see cref="PrepareSave"/>).
    /// </summary>
    public ImageCompression ImageCompression { get; set; }

    /// <summary>
    /// The compression used to save the composite image: raw or RLE (Photoshop only writes these two).
    /// </summary>
    private ImageCompression CompositeSaveCompression =>
      (ImageCompression == ImageCompression.Raw) ? ImageCompression.Raw : ImageCompression.Rle;

    private void LoadImage(PsdBinaryReader reader)
    {
      Util.DebugMessage(reader.BaseStream, "Load, Begin, Composite image");

      ImageCompression = (ImageCompression)reader.ReadInt16();
      var isZip = (ImageCompression == ImageCompression.Zip) || (ImageCompression == ImageCompression.ZipPrediction);

      // Create channels
      for (Int16 i = 0; i < ChannelCount; i++)
      {
        Util.DebugMessage(reader.BaseStream, "Load, Begin, Channel image data");

        var channel = new Channel(i, this.BaseLayer);
        channel.ImageCompression = isZip ? ImageCompression.Raw : ImageCompression;
        channel.Length = (long)this.RowCount
          * Util.BytesPerRow(BaseLayer.Rect.Size, BitDepth);

        // The composite image stores all RLE headers up-front, rather than
        // with each channel.
        if (ImageCompression == ImageCompression.Rle)
        {
          channel.RleRowLengths = new RleRowLengths(reader, RowCount, IsLargeDocument);
          channel.Length = channel.RleRowLengths.Total;
        }

        BaseLayer.Channels.Add(channel);
        Util.DebugMessage(reader.BaseStream, "Load, End, Channel image data");
      }

      if (isZip)
      {
        LoadZipImage(reader);
      }
      else
      {
        foreach (var channel in this.BaseLayer.Channels)
        {
          Util.DebugMessage(reader.BaseStream, "Load, Begin, Channel image data");
          Util.CheckByteArrayLength(channel.Length);
          channel.ImageDataRaw = reader.ReadBytes((int)channel.Length);
          Util.DebugMessage(reader.BaseStream, "Load, End, Channel image data");
        }
      }

      // If there is exactly one more channel than we need, then it is the
      // alpha channel.
      if ((ColorMode != PsdColorMode.Multichannel)
        && (ChannelCount == ColorMode.MinChannelCount() + 1))
      {
        var alphaChannel = BaseLayer.Channels.Last();
        alphaChannel.ID = -1;
      }

      Util.DebugMessage(reader.BaseStream, "Load, End, Composite image");
    }

    /// <summary>
    /// Load a ZIP compressed composite image.
    /// The specification allows ZIP (2) and ZIP with prediction (3) for the composite but does not define the layout, Photoshop never writes it.
    /// Like other readers (e.g. psd-tools), the rest of the stream is read as one zlib stream with the planar data of all channels
    /// (channel after channel, prediction per row; 32 bit ZIP is read as ZIP with prediction, like for layers).
    /// The channels are decoded right away and stored as raw channels (<see cref="Channel.ImageData"/> in native byte order,
    /// <see cref="Channel.ImageDataRaw"/> in big endian on-disk format).
    /// </summary>
    private void LoadZipImage(PsdBinaryReader reader)
    {
      var stream = reader.BaseStream;
      var zipLength = stream.Length - stream.Position;
      Util.CheckByteArrayLength(zipLength);
      var zipData = reader.ReadBytes((int)zipLength);

      var size = BaseLayer.Rect.Size;
      var channelLength = (long)RowCount * Util.BytesPerRow(size, BitDepth);
      Util.CheckByteArrayLength(channelLength);

      // One decoder for all channels: each Read continues the zlib stream
      var decoder = ImageDataFactory.Create(ImageCompression, zipData, size, BitDepth, null);
      foreach (var channel in BaseLayer.Channels)
      {
        Util.DebugMessage(reader.BaseStream, "Load, Begin, Channel image data");
        var data = new byte[channelLength];
        decoder.Read(data);
        channel.ImageData = data;
        channel.Length = channelLength;
        if (BitDepth <= 8)
        {
          channel.ImageDataRaw = data;
        }
        else
        {
          var raw = (byte[])data.Clone();
          Util.SwapByteArray(BitDepth, raw, 0, size.Width * size.Height);
          channel.ImageDataRaw = raw;
        }
        Util.DebugMessage(reader.BaseStream, "Load, End, Channel image data");
      }
    }

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Convert the composite channels to the compression used for saving (see <see cref="ImageCompression"/>), decoding them if needed.
    /// </summary>
    private void PrepareSaveComposite()
    {
      var compression = CompositeSaveCompression;
      foreach (var channel in BaseLayer.Channels)
      {
        if (channel.ImageCompression == compression)
          continue;
        if (channel.ImageDataRaw != null)
        {
          if (channel.ImageData == null)
            channel.DecodeImageData();
          channel.ImageDataRaw = null;
        }
        channel.ImageCompression = compression;
      }
    }

    private void SaveImage(PsdBinaryWriter writer)
    {
      Util.DebugMessage(writer.BaseStream, "Save, Begin, Composite image");

      var compression = CompositeSaveCompression;
      writer.Write((short)compression);
      if (compression == PhotoshopFile.ImageCompression.Rle)
      {
        foreach (var channel in this.BaseLayer.Channels)
        {
          Util.DebugMessage(writer.BaseStream, "Save, Begin, RLE header");
          channel.RleRowLengths.Write(writer, IsLargeDocument);
          Util.DebugMessage(writer.BaseStream, "Save, End, RLE header");
        }
      }
      foreach (var channel in this.BaseLayer.Channels)
      {
        Util.DebugMessage(writer.BaseStream, "Save, Begin, Channel image data");
        writer.Write(channel.ImageDataRaw);
        Util.DebugMessage(writer.BaseStream, "Save, End, Channel image data");
      }

      Util.DebugMessage(writer.BaseStream, "Save, End, Composite image");
    }

    #endregion
  }


  /// <summary>
  /// The possible Compression methods.
  /// </summary>
  public enum ImageCompression
  {
    /// <summary>
    /// Raw data
    /// </summary>
    Raw = 0,
    /// <summary>
    /// RLE compressed
    /// </summary>
    Rle = 1,
    /// <summary>
    /// ZIP without prediction.
    /// </summary>
    Zip = 2,
    /// <summary>
    /// ZIP with prediction.
    /// </summary>
    ZipPrediction = 3
  }

}
