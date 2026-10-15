# compress

[⬆ _tools](../README.md)

> Pre-built, self-contained Windows x64 build of the Compress tool, which pre-compresses web and data assets into side-by-side compressed copies during the SysWeaver builds.

| | |
|---|---|
| **Project** | [Compress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Compress/README.md) (source in SysWeaver.Tools) |
| **Kind** | Pre-built tool |
| **Used by the builds** | Yes |

## Purpose

This is the publish output of the `Compress` project in [SysWeaver.Tools](https://github.com/dosh1974/SysWeaver.Tools), synced into this folder by its [`Publish_All.bat`](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Publish_All.bat) (`.pdb` files and `createdump.exe` are left out, and only the `SysWeaver.*.xml` and `Compress.xml` documentation files are kept). It is self-contained (.NET 10 runtime included), so no .NET installation is needed to run it.

Project builds call it from `Exec` build steps, ex: `..\_tools\compress\Compress.exe -Methods br .\srcWeb\*+ .\web`: about 40 build steps in 29 SysWeaver projects compress `srcWeb` / `srcData` (or SvgOpt's `.opt` output) into the generated `web` / `data` folders that are embedded into the assemblies. `SysWeaver.Media.Video` uses `-Force -Methods gzip` to pack its `runtimes` folder into `packed`. In [SysWeaver.Services](https://github.com/dosh1974/SysWeaver.Services), `FakeEmailService` and `FakeTextService` call it the same way. Because it is a Windows executable, building these projects requires Windows.

## Contents

| Item | Description |
|---|---|
| [`runtimes/`](runtimes/README.md) | Native Brotli libraries per platform (from the Brotli.NET package) |
| [`tools/`](tools/README.md) | Windows helper executables (minifier, PNG recompressor, shader tools) that Compress runs for type specific pre-processing |
| [`Compress.exe`](Compress.exe) | The executable (entry point) |
| `Compress.dll` | The tool's code |
| [`Compress.deps.json`](Compress.deps.json), [`Compress.runtimeconfig.json`](Compress.runtimeconfig.json) | .NET dependency manifest and runtime configuration (`net10.0`, runtime 10.0.13 included) |
| `Compress.xml` | XML documentation of the tool |
| `SysWeaver.*.dll` (13) and matching `SysWeaver.*.xml` | SysWeaver libraries: CommandLine, Common (+ Linux / Windows), Compression (+ BrotliNativeNET / ZstdSharp), Docs, ExpressionEvaluator, MicroServices, Serialization (+ NewtonsoftJson), Storage |
| `Brotli.Core.dll`, `brolib_x64.dll`, `brolib_x86.dll`, `ZstdSharp.dll`, `Newtonsoft.Json.dll`, `CommunityToolkit.HighPerformance.dll` | Third-party libraries (the two `brolib_*.dll` are identical to the ones in `runtimes/win/native`) |
| other `*.dll` (~190) | The self-contained .NET runtime (`System.*`, `Microsoft.*`, `coreclr.dll`, `msquic.dll`, ...) |

## Relationships

- **Part of:** [_tools](../README.md)
- **Source:** [Compress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Compress/README.md) in SysWeaver.Tools
- **Builds on:** [SysWeaver.CommandLine](../../SysWeaver.CommandLine/README.md), [SysWeaver.Compression](../../SysWeaver.Compression/README.md), [SysWeaver.Compression.BrotliNativeNET](../../Compression/SysWeaver.Compression.BrotliNativeNET/README.md), [SysWeaver.Compression.ZstdSharp](../../Compression/SysWeaver.Compression.ZstdSharp/README.md)
- **Related:** [decompress](../decompress/README.md) (the reverse operation), [svg_opt](../svg_opt/README.md) (optimises SVG icons before they are compressed)
