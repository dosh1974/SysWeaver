# SwCurl

[⬆ _tools](../README.md)

> Pre-built, self-contained Windows x64 build of SwCurl, a curl-like HTTP client for SysWeaver APIs (SysWeaver login or HTTP authentication, optional compression and Tor routing).

| | |
|---|---|
| **Project** | [SwCurl](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwCurl/README.md) (source in SysWeaver.Tools) |
| **Kind** | Pre-built tool |
| **Used by the builds** | No |

## Purpose

This is the publish output of the `SwCurl` project in [SysWeaver.Tools](https://github.com/dosh1974/SysWeaver.Tools), synced into this folder by its [`Publish_All.bat`](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Publish_All.bat) (`.pdb` files and `createdump.exe` are left out, and only the `SysWeaver.*.xml` and `SwCurl.xml` documentation files are kept). It is self-contained (.NET 10 runtime included), so no .NET installation is needed to run it.

No build step or script in this repository calls it; it is a manual utility for calling SysWeaver APIs from the command line.

## Contents

| Item | Description |
|---|---|
| [`SwCurl.exe`](SwCurl.exe) | The executable (entry point) |
| `SwCurl.dll` | The tool's code |
| [`SwCurl.deps.json`](SwCurl.deps.json), [`SwCurl.runtimeconfig.json`](SwCurl.runtimeconfig.json) | .NET dependency manifest and runtime configuration (`net10.0`, runtime 10.0.13 included) |
| `SwCurl.xml` | XML documentation of the tool |
| `SysWeaver.*.dll` (10) and matching `SysWeaver.*.xml` | SysWeaver libraries: CommandLine, Common, Compression, Docs, ExpressionEvaluator, MicroServices, Remote, Remote.Connection, Serialization, Tor |
| `Knapcode.TorSharp.dll`, `SharpCompress.dll`, `BitFaster.Caching.dll`, `CommunityToolkit.HighPerformance.dll` | Third-party libraries |
| other `*.dll` (~185) | The self-contained .NET runtime (`System.*`, `Microsoft.*`, `coreclr.dll`, `msquic.dll`, ...) |

## Relationships

- **Part of:** [_tools](../README.md)
- **Source:** [SwCurl](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwCurl/README.md) in SysWeaver.Tools
- **Builds on:** [SysWeaver.Remote](../../SysWeaver.Remote/README.md), [SysWeaver.Remote.Connection](../../SysWeaver.Remote.Connection/README.md), [SysWeaver.Tor](../../SysWeaver.Tor/README.md), [SysWeaver.CommandLine](../../SysWeaver.CommandLine/README.md)
