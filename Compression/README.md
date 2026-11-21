# Compression

[⬆ SysWeaver overview](../README.md)

> Groups the compression codec plug-in projects that add codecs to the registry of SysWeaver.Compression.

| | |
|---|---|
| **Repository** | [SysWeaver](../README.md) |
| **Kind** | Group of projects |

## Purpose

[SysWeaver.Compression](../SysWeaver.Compression/README.md) defines the compression abstraction and its registry of named, prioritised codecs (Brotli, Deflate, GZip and Zstandard are built in). The projects in this folder are optional plug-ins that replace a built-in codec with another implementation: native Brotli and fully managed Zstandard. The folder itself only holds the project folders.

## Contents

| Item | Description |
|---|---|
| [`SysWeaver.Compression.BrotliNativeNET/`](SysWeaver.Compression.BrotliNativeNET/README.md) | Codec plug-in: **br** (Brotli) backed by the native Brotli library of Brotli.NET, preferred over the built-in .NET Brotli codec when registered. |
| [`SysWeaver.Compression.ZstdSharp/`](SysWeaver.Compression.ZstdSharp/README.md) | Codec plug-in: **zstd** (Zstandard) backed by the fully managed ZstdSharp.Port, preferred over the built-in .NET Zstandard codec when registered. |

## Relationships

- **Part of:** [SysWeaver](../README.md)
- **Plug-ins for:** [SysWeaver.Compression](../SysWeaver.Compression/README.md)
- **Related:** [SysWeaver.HttpTransformer.Compress](../SysWeaver.HttpTransformer.Compress/README.md) (pre-compresses served files with the registered codecs)
