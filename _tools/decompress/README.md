# decompress

[⬆ _tools](../README.md)

> Pre-built, self-contained Windows x64 build of the Decompress tool, which decompresses Brotli, gzip, zstd and deflate compressed files (the reverse of `compress`).

| | |
|---|---|
| **Project** | [Decompress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Decompress/README.md) (source in SysWeaver.Tools) |
| **Kind** | Pre-built tool |
| **Used by the builds** | No |

## Purpose

This is the publish output of the `Decompress` project in [SysWeaver.Tools](https://github.com/dosh1974/SysWeaver.Tools), synced into this folder by its [`Publish_All.bat`](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Publish_All.bat) (`.pdb` files and `createdump.exe` are left out, and only the `SysWeaver.*.xml` and `Decompress.xml` documentation files are kept). It is self-contained (.NET 10 runtime included), so no .NET installation is needed to run it.

No build step calls it; it is a manual utility, ex: to inspect the compressed files that `compress` generates.

## Contents

| Item | Description |
|---|---|
| [`runtimes/`](runtimes/README.md) | Native Brotli libraries per platform (from the Brotli.NET package) |
| [`Decompress.exe`](Decompress.exe) | The executable (entry point) |
| `Decompress.dll` | The tool's code |
| [`Decompress.deps.json`](Decompress.deps.json), [`Decompress.runtimeconfig.json`](Decompress.runtimeconfig.json) | .NET dependency manifest and runtime configuration (`net10.0`, runtime 10.0.13 included) |
| `Decompress.xml` | XML documentation of the tool |
| `SysWeaver.*.dll` (8) and matching `SysWeaver.*.xml` | SysWeaver libraries: CommandLine, Common, Compression (+ BrotliNativeNET / ZstdSharp), Docs, ExpressionEvaluator, MicroServices |
| `Brotli.Core.dll`, `brolib_x64.dll`, `brolib_x86.dll`, `ZstdSharp.dll`, `CommunityToolkit.HighPerformance.dll` | Third-party libraries (the two `brolib_*.dll` are identical to the ones in `runtimes/win/native`) |
| other `*.dll` (~185) | The self-contained .NET runtime (`System.*`, `Microsoft.*`, `coreclr.dll`, `msquic.dll`, ...) |

## Relationships

- **Part of:** [_tools](../README.md)
- **Source:** [Decompress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Decompress/README.md) in SysWeaver.Tools
- **Builds on:** [SysWeaver.CommandLine](../../SysWeaver.CommandLine/README.md), [SysWeaver.Compression](../../SysWeaver.Compression/README.md), [SysWeaver.Compression.BrotliNativeNET](../../Compression/SysWeaver.Compression.BrotliNativeNET/README.md), [SysWeaver.Compression.ZstdSharp](../../Compression/SysWeaver.Compression.ZstdSharp/README.md)
- **Related:** [compress](../compress/README.md)
