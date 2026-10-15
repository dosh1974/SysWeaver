# svg_opt

[⬆ _tools](../README.md)

> Pre-built, self-contained Windows x64 build of the SvgOpt tool, which optimises SVG icons during the SysWeaver builds.

| | |
|---|---|
| **Project** | [SvgOpt](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SvgOpt/README.md) (source in SysWeaver.Tools) |
| **Kind** | Pre-built tool |
| **Used by the builds** | Yes |

## Purpose

This is the publish output of the `SvgOpt` project in [SysWeaver.Tools](https://github.com/dosh1974/SysWeaver.Tools), synced into this folder (renamed to `svg_opt`) by its [`Publish_All.bat`](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Publish_All.bat) (`.pdb` files and `createdump.exe` are left out, and only the `SysWeaver.*.xml` and `SvgOpt.xml` documentation files are kept). It is self-contained (.NET 10 runtime included), so no .NET installation is needed to run it.

Project builds call it from `Exec` build steps (16 steps in 11 SysWeaver projects, ex: SysWeaver.AI, SysWeaver.Chat, SysWeaver.HttpServer.IconModule, SysWeaver.MicroService.Auth), ex: `..\_tools\svg_opt\SvgOpt.exe -Compare .\WebCompare -RemoveFill -RemoveSize .\srcWeb\icons\*.svg .\srcWeb\icons\.opt\`. The optimised icons in `.opt` are then compressed by [compress](../compress/README.md) into the embedded `web` / `data` folders. `SysWeaver.HttpServer.IconModule/FixIcons.bat` also calls it. Because it is a Windows executable, building these projects requires Windows.

## Contents

| Item | Description |
|---|---|
| [`SvgOpt.exe`](SvgOpt.exe) | The executable (entry point) |
| `SvgOpt.dll` | The tool's code |
| [`SvgOpt.deps.json`](SvgOpt.deps.json), [`SvgOpt.runtimeconfig.json`](SvgOpt.runtimeconfig.json) | .NET dependency manifest and runtime configuration (`net10.0`, runtime 10.0.13 included) |
| `SvgOpt.xml` | XML documentation of the tool |
| `SysWeaver.*.dll` (11) and matching `SysWeaver.*.xml` | SysWeaver libraries: CommandLine, Common (+ Linux / Windows), Compression, Docs, ExpressionEvaluator, MicroServices, Minifier.Svg, Serialization, Storage |
| `Svg.dll`, `ExCSS.dll`, `CommunityToolkit.HighPerformance.dll` | Third-party libraries (SVG parsing and CSS) |
| other `*.dll` (~195) | The self-contained .NET runtime (`System.*`, `Microsoft.*`, `coreclr.dll`, `msquic.dll`, ...) |

## Relationships

- **Part of:** [_tools](../README.md)
- **Source:** [SvgOpt](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SvgOpt/README.md) in SysWeaver.Tools
- **Builds on:** [SysWeaver.Minifier.Svg](../../SysWeaver.Minifier.Svg/README.md), [SysWeaver.CommandLine](../../SysWeaver.CommandLine/README.md)
- **Related:** [compress](../compress/README.md) (compresses the optimised icons)
