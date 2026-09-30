# SysWeaver.Media.Psd

[⬆ SysWeaver overview](../README.md)

> Reader/writer for Adobe Photoshop (PSD) files: layers, masks, channels, groups and image resources.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Library (vendored code) |

## Purpose

Access layered Photoshop documents, e.g. to extract layers or composite images in content pipelines.

## Origin

File headers identify the code as the "Photoshop PSD FileType Plugin for Paint.NET" by Frank Blumenberg, under the MIT License.

## Limitations and considerations

- Not every colour mode / bit depth combination is supported (unsupported combinations throw).
- Not included in the main solution file.

## Relationships

- **Project:** [`SysWeaver.Media.Psd.csproj`](SysWeaver.Media.Psd.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
