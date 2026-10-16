# SysWeaver.Media.Psd

[⬆ SysWeaver overview](../README.md)

> Reader/writer for Adobe Photoshop (PSD / PSB) files: layers, masks, channels, groups and image resources, plus helpers to decode RGB images and extract text layers.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Library (includes vendored code) |

## Purpose

Access layered Photoshop documents, e.g. to extract layers, text or the composite image in content pipelines.

## Key features

- Load, edit and save PSD/PSB documents: layers, masks, channels, groups (layer sections), image resources and additional layer info
- Codecs for raw, PackBits RLE, ZIP and ZIP with prediction image data
- Decoding of the composite image or a single layer to `ImageData32` (RGBA, 8 bits per channel)
- Extraction of visible text layers (`PsdTextLayer`: name, text, rect, baseline) and `key=value` named layers as properties
- Per-layer callbacks and listing of named visible layers
- Defensive parsing: corrupt or truncated files throw exceptions instead of allocating huge buffers or overflowing the stack

## Key components

- [`PsdImage`](PsdImage.cs) — SysWeaver helpers: decode images, enumerate layers, extract text layers
- [`PsdFile`](PsdFile/PsdFile.cs) — the document model (namespace `PhotoshopFile`), load and save
- [`PsdTextLayer`](PsdTextLayer.cs) — an extracted text layer

## Limitations and considerations

- The code in `PsdFile/` (namespaces `PhotoshopFile`, `PhotoshopFile.Compression`) comes from the "Photoshop PSD FileType Plugin for Paint.NET" by Frank Blumenberg and Tao Yue (MIT License, portions BSD 3-clause by Jonas Beckeman); `PsdImage.cs` and `PsdTextLayer.cs` are SysWeaver code
- `PsdImage` only decodes RGB documents with 8 or 16 bits per channel (else `NotSupportedException`); the `PhotoshopFile` classes read all modes and depths
- The composite image is always saved raw or RLE compressed (like Photoshop), never ZIP
- Saving writes a `VersionInfo` resource naming "Paint.NET PSD Plugin" if none exists

## Folder layout

| Folder | Content |
|---|---|
| [`PsdFile/`](PsdFile/README.md) | Vendored Paint.NET PSD plugin code: the `PhotoshopFile` document model, codecs, image resources and layers |

## Relationships

- **Project:** [`SysWeaver.Media.Psd.csproj`](SysWeaver.Media.Psd.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
