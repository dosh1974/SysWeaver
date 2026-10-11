# SysWeaver.Media.Psd

[⬆ SysWeaver overview](../README.md)

> Reader/writer for Adobe Photoshop (PSD / PSB) files: layers, masks, channels, groups and image resources, plus helpers to decode RGBA images and extract text layers.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Library (vendored code) |

## Purpose

Access layered Photoshop documents, e.g. to extract layers or composite images in content pipelines.

## Origin

File headers identify the code in `PsdFile/` (namespaces `PhotoshopFile`, `PhotoshopFile.Compression`) as the "Photoshop PSD FileType Plugin for Paint.NET"
by Frank Blumenberg and Tao Yue, under the MIT License (portions BSD 3-clause, Jonas Beckeman). `PsdImage.cs` and `PsdTextLayer.cs` (namespace `SysWeaver.Media.Psd`) are SysWeaver code.

## API overview

- **`SysWeaver.Media.Psd.PsdImage`** (static):
  - `Load(filename)`: decode the composite image to an `ImageData32` (RGBA, 8 bits per channel).
  - `DecodeLayer(layer)`: decode one layer (its rect size). Missing color channels are white (255), no alpha channel means alpha 255, 16 bit values are reduced to their high byte.
  - `LoadAndExtractTextLayers(out layers[, out props], filename)`: visible text layers (Photoshop 6+ `TySh` text, the layer also needs an `fxrp` info) as `PsdTextLayer`
    (name, text, layer rect, `Baseline` = vertical translation of the text transform); visible non-text layers named `key=value` become properties (trimmed, last one wins).
  - `OnPsdLayers`, `GetImageAndProcessPsdLayers`: call a function for every layer (file order = bottom to top, hidden layers included), return false to stop.
  - `GetNamedLayers(filename)`: name and rect of the visible layers whose rect is not `Rectangle.IsEmpty` (all values 0).
- **`PhotoshopFile.PsdFile`**: load from a file or a (seekable) stream, edit, `Save` to a file or a seekable stream. Image data stays compressed until `Channel.DecodeImageData()`.
  `Layers`, `BaseLayer` (composite), `ImageResources` (`ResolutionInfo`, `VersionInfo`, `AlphaChannelNames`, `UnicodeAlphaNames`, others raw), `AdditionalInfo`
  (`InfoLayers` = `Layr`/`Lr16`/`Lr32` layers, `LayerSectionInfo` = groups, `LayerUnicodeName`, others raw).
- **Codecs** (`PhotoshopFile.Compression`): raw, PackBits RLE (`RleReader` / `RleWriter`), ZIP (`ZipImage`), ZIP with prediction (`ZipPredict16Image`, `ZipPredict32Image`),
  `EndianReverser` for 16 / 32 bit data. Decoded data is in native byte order.

## Limitations and considerations

- `PsdImage` only decodes **RGB documents with 8 or 16 bits per channel**, anything else throws `NotSupportedException`. The `PhotoshopFile` classes read all modes and depths (1, 8, 16, 32 bits).
- Composite image (`PsdFile.ImageCompression`): it is saved raw (`Raw`) or RLE compressed (anything else), like Photoshop, which never writes a ZIP composite:
  `Zip` / `ZipPrediction` are saved as RLE (a file saved with ZIP couldn't be read back, psd-cov-10). The composite channels are converted when saving.
  A ZIP composite (allowed by the specification, layout not defined) is read like other readers (e.g. psd-tools) do: the rest of the file is one zlib stream
  with the planar data of all channels; the channels are decoded at load time and stored as raw channels (`ImageCompression` keeps the file's value).
- A layer created with `new Layer(psd)` has an empty `MaskInfo` (no masks) and empty `BlendingRangesData`, it can be saved as is.
  Layers are not added to the document automatically. Channels can only be created through `Layer.CreateMissingChannels()` (the constructor is internal).
- `Layer.CreateMissingChannels()` creates `BytesPerRow * height` bytes per channel for the document bit depth, set to the maximum value
  (all bits set for 1, 8 and 16 bits, 1.0f for 32 bits).
- Corrupt files: lengths are checked against the stream length (seekable streams), so truncated or corrupt files throw `EndOfStreamException`, `PsdInvalidException`,
  `RleException`, `InvalidDataException` or `ArgumentException` instead of crashing or allocating huge buffers (no `Debug.Assert` on file data).
  A layer may be bigger than the document, but `Channel.DecodeImageData()` and `Layer.CreateMissingChannels()` throw `PsdInvalidException` before allocating
  if the layer (or mask) rect is negative or more than 5 times (`Util.MaxLayerSizeFactor`) wider or higher than the document.
  RLE row lengths that don't sum to the channel data length are tolerated (rows are decoded sequentially), row lengths longer than the channel data,
  or layer data that overruns the Layers Info section, throw `PsdInvalidException`.
  Text descriptors (and lists) nested deeper than 64 levels throw `PsdInvalidException` (no stack overflow on malicious files).
- The ZIP compressor writes a complete zlib stream (header, deflate data, Adler-32 trailer). The decompressor does not check the trailer (data without one is read).
- A channel without data (`ImageData` and `ImageDataRaw` null) is saved with length 0 and no data, and loaded back as an empty raw channel.
- `RawLayerInfo(key)` has empty data, `RawLayerInfo(key, data)` sets the data.
- Saving writes a `VersionInfo` resource naming "Paint.NET PSD Plugin" if none exists.
- Not included in the main solution file.

## Known bugs

None known (see `_docs/BugReview-2026-10-06.md`, the psd-cov bugs are fixed).

## Relationships

- **Project:** [`SysWeaver.Media.Psd.csproj`](SysWeaver.Media.Psd.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
- **Tests:** `SysWeaver.Tests\UnitTests\MediaPsd`
