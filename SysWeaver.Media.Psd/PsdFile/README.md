# PsdFile

[⬆ SysWeaver.Media.Psd](../README.md)

> The vendored Photoshop PSD/PSB document model (namespace `PhotoshopFile`): file header, colour mode data, image resources, layers, masks and the composite image, with binary readers and writers.

| | |
|---|---|
| **Project** | [SysWeaver.Media.Psd](../README.md) |
| **Kind** | Vendored library (source code) |

## Purpose

This folder holds the code of the "Photoshop PSD FileType Plugin for Paint.NET" by Frank Blumenberg and Tao Yue (MIT License, portions BSD 3-clause by Jonas Beckeman), as stated in the header of each file. It does the actual parsing and writing of PSD and PSB files. The SysWeaver helpers [`PsdImage`](../PsdImage.cs) and [`PsdTextLayer`](../PsdTextLayer.cs) in the project folder are built on top of it.

`PsdFile` loads a document section by section: header, colour mode data, image resources ([`ImageResources/`](ImageResources/README.md)), layer and mask information ([`Layers/`](Layers/README.md)) and the composite image data. The channel data of the layers and the composite image is decoded by the codecs in [`Compression/`](Compression/README.md).

## Contents

| Item | Description |
|---|---|
| [`Compression/`](Compression/README.md) | Image data codecs: raw, RLE, ZIP and ZIP with prediction |
| [`ImageResources/`](ImageResources/README.md) | Typed image resource blocks (resolution, alpha channel names, thumbnail, version info) |
| [`Layers/`](Layers/README.md) | Layers, channels, masks, blending ranges and additional layer information |
| [`PsdFile.cs`](PsdFile.cs) | `PsdFile`: the document (load, save, header fields, layers, composite image), plus the `PsdColorMode` and `PsdFileVersion` enums |
| [`ImageResource.cs`](ImageResource.cs) | `ImageResource` base class, the `ResourceID` enum and `ImageResourceFactory` |
| [`LoadContext.cs`](LoadContext.cs) | `LoadContext`: text encoding and virtual callbacks invoked while layers are loaded |
| [`PsdBinaryReader.cs`](PsdBinaryReader.cs) | Big-endian reader for PSD primitives (Pascal / Unicode strings, rectangles, fixed point values) |
| [`PsdBinaryWriter.cs`](PsdBinaryWriter.cs) | Big-endian writer, the counterpart of `PsdBinaryReader` |
| [`PsdBlockLengthWriter.cs`](PsdBlockLengthWriter.cs) | Writes a block length placeholder and patches it when the block is complete |
| [`PsdBlendMode.cs`](PsdBlendMode.cs) | The four-character blend mode keys (`norm`, `mul `, `scrn`, ...) |
| [`RleReader.cs`](RleReader.cs) | PackBits RLE decoder |
| [`RleWriter.cs`](RleWriter.cs) | PackBits RLE encoder |
| [`RleRowLengths.cs`](RleRowLengths.cs) | The per-row byte counts stored in front of RLE compressed image data |
| [`Exceptions.cs`](Exceptions.cs) | `PsdInvalidException` and `RleException` |
| [`Util.cs`](Util.cs) | Byte and endian helpers, rectangle helpers and the `UFixed16_16` fixed point type |

## Limitations and considerations

- The files refer to a `LICENSE.txt` with the complete licensing information; that file is not part of this folder.
- See the project README for the limitations of decoding and saving.

## Relationships

- **Part of:** [SysWeaver.Media.Psd](../README.md)
- **Used by:** [`PsdImage`](../PsdImage.cs) (image decoding and text layer extraction)
