# ImageResources

[⬆ PsdFile](../README.md)

> Typed image resource blocks of a PSD file: resolution, alpha channel names, thumbnail and version information.

| | |
|---|---|
| **Project** | [SysWeaver.Media.Psd](../../README.md) |
| **Kind** | Vendored library (source code) |

## Purpose

The image resources section of a PSD file is a list of blocks identified by a `ResourceID`. [`ImageResourceFactory`](../ImageResource.cs) creates the classes in this folder for the IDs it knows; every other block is kept unchanged as a `RawImageResource`, so it survives a load and save round trip.

Part of the vendored Paint.NET PSD plugin code (see [`PsdFile/`](../README.md)).

## Contents

| Item | Description |
|---|---|
| [`RawImageResource.cs`](RawImageResource.cs) | A resource block kept as raw bytes (used for all unknown IDs) |
| [`ResolutionInfo.cs`](ResolutionInfo.cs) | Horizontal / vertical resolution with units |
| [`AlphaChannelNames.cs`](AlphaChannelNames.cs) | Names of the alpha channels (Pascal strings) |
| [`UnicodeAlphaNames.cs`](UnicodeAlphaNames.cs) | Names of the alpha channels (Unicode strings) |
| [`Thumbnail.cs`](Thumbnail.cs) | The embedded thumbnail image (derived from `RawImageResource`) |
| [`VersionInfo.cs`](VersionInfo.cs) | Version information (writer, reader, file version) |

## Limitations and considerations

- The factory cases for the thumbnail resource IDs are commented out in `ImageResource.cs`, so thumbnails are loaded as `RawImageResource`, not as `Thumbnail`.

## Relationships

- **Part of:** [PsdFile](../README.md), [SysWeaver.Media.Psd](../../README.md)
- **Uses:** [`ImageResource.cs`](../ImageResource.cs) (base class and factory)
