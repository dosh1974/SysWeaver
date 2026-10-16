# Compression

[⬆ PsdFile](../README.md)

> Codecs for the channel image data of PSD files (namespace `PhotoshopFile.Compression`): raw, PackBits RLE, ZIP and ZIP with delta prediction.

| | |
|---|---|
| **Project** | [SysWeaver.Media.Psd](../../README.md) |
| **Kind** | Vendored library (source code) |

## Purpose

Every channel of a layer, and the composite image, stores its pixels with one of the PSD compression methods. `ImageDataFactory` picks the matching `ImageData` implementation for a compression method and bit depth, so the rest of the document model reads and writes plain uncompressed rows.

Part of the vendored Paint.NET PSD plugin code (see [`PsdFile/`](../README.md)).

## Contents

| Item | Description |
|---|---|
| [`ImageData.cs`](ImageData.cs) | `ImageData`: abstract base class, reading and writing of whole images or single rows |
| [`ImageDataFactory.cs`](ImageDataFactory.cs) | Creates the `ImageData` for a compression method and bit depth |
| [`RawImage.cs`](RawImage.cs) | Uncompressed image data |
| [`RleImage.cs`](RleImage.cs) | PackBits RLE compressed image data |
| [`ZipImage.cs`](ZipImage.cs) | ZIP (deflate) compressed image data |
| [`ZipPredict16Image.cs`](ZipPredict16Image.cs) | ZIP with delta prediction for 16 bits per channel |
| [`ZipPredict32Image.cs`](ZipPredict32Image.cs) | ZIP with delta prediction for 32 bits per channel |
| [`EndianReverser.cs`](EndianReverser.cs) | Wraps another `ImageData` and reverses the byte order of 16 and 32 bit samples |

## Relationships

- **Part of:** [PsdFile](../README.md), [SysWeaver.Media.Psd](../../README.md)
- **Used by:** [`Layers/`](../Layers/README.md) (channel data) and [`PsdFile.cs`](../PsdFile.cs) (composite image)
