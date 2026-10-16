# runtimes

[⬆ decompress](../README.md)

> Platform specific native Brotli libraries that came with the publish output of Decompress.

| | |
|---|---|
| **Project** | [Decompress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Decompress/README.md) (source in SysWeaver.Tools) |
| **Kind** | Native libraries |
| **Used by the builds** | No |

## Purpose

The `Brotli.NET` package (used by [SysWeaver.Compression.BrotliNativeNET](../../../Compression/SysWeaver.Compression.BrotliNativeNET/README.md)) ships its native Brotli library (`brolib`) per platform in the standard .NET `runtimes/<os>/native` layout, and the publish copies all of them here. Decompress is published for Windows x64 only, so only the Windows libraries are relevant to it (copies of them are also next to `Decompress.exe`); the Linux and macOS libraries are carried along by the publish.

## Contents

| Item | Description |
|---|---|
| [`linux/`](linux/README.md) | Linux native libraries |
| [`osx/`](osx/README.md) | macOS native library |
| [`win/`](win/README.md) | Windows native libraries |

## Relationships

- **Part of:** [decompress](../README.md)
- **Used by:** [SysWeaver.Compression.BrotliNativeNET](../../../Compression/SysWeaver.Compression.BrotliNativeNET/README.md) (through the `Brotli.NET` package)
