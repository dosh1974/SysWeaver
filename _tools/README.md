# _tools — pre-built tools

[⬆ SysWeaver overview](../README.md)

> Pre-built command line tools used by the build steps of the SysWeaver projects (and a few more kept for convenience).

| | |
|---|---|
| **Repository** | [SysWeaver](../README.md) |
| **Kind** | Pre-built tools (Windows x64 binaries) |

## Purpose

Self-contained Windows x64 builds of command line tools built on SysWeaver. Their source is not in this repository: it lives in the [SysWeaver.Tools](https://github.com/dosh1974/SysWeaver.Tools) repository (cloned next to this one), whose `Publish_All.bat` publishes the tools and syncs them into this folder.

## Contents

| Folder | Tool | Role |
|---|---|---|
| [`compress/`](compress) | [Compress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Compress/README.md) | Pre-compresses web and data assets (the builds use Brotli) during project builds, so embedded files can be served without runtime compression; minifies web files first (its `tools` sub folder holds the minifier and the PNG / shader helpers). |
| [`svg_opt/`](svg_opt) | [SvgOpt](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SvgOpt/README.md) | Optimises SVG icons and data during builds (based on [SysWeaver.Minifier.Svg](../SysWeaver.Minifier.Svg/README.md)); also used by `SysWeaver.HttpServer.IconModule/FixIcons.bat`. |
| [`decompress/`](decompress) | [Decompress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Decompress/README.md) | Decompresses `.br`, `.gz`, `.zstd` and `.deflate` files (the reverse of `compress`). Not used by the builds. |
| [`SwSyncTool/`](SwSyncTool) | [SwSyncTool](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwSyncTool/README.md) | Command line folder synchronisation client (push/pull) for [SysWeaver.MicroService.FolderSync](../SysWeaver.MicroService.FolderSync/README.md), plus local `.swcompact` archive operations. Not used by the builds. |
| [`SwCurl/`](SwCurl) | [SwCurl](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwCurl/README.md) | HTTP command line client built on SysWeaver's remote and Tor components. Not used by the builds. |
| [`VsClean/`](VsClean) | [VsClean](https://github.com/dosh1974/SysWeaver.Tools/blob/main/VsClean/README.md) | Deletes build outputs (`bin`, `obj`, generated `web` / `data`); used by the `Clean_All.bat` scripts, not by the builds. |
| [`OnDuplicates/`](OnDuplicates) | [OnDuplicates](https://github.com/dosh1974/SysWeaver.Tools/blob/main/OnDuplicates/README.md) | Finds files with identical content; used by `SysWeaver.MicroService.Avatar/RemoveDuplicates.bat`, not by the builds. |
| [`ComfyApiToCs/`](ComfyApiToCs) | [ComfyApiToCs](https://github.com/dosh1974/SysWeaver.Tools/blob/main/ComfyApiToCs/README.md) | Converts ComfyUI API format workflows into C# input / output types (based on SysWeaver.AI.ComfyUi). Not used by the builds. |
| [`RunComfyUi/`](RunComfyUi) | [RunComfyUi](https://github.com/dosh1974/SysWeaver.Tools/blob/main/RunComfyUi/README.md) | Runs a ComfyUI workflow with its inputs as command line options and saves the results. Not used by the builds. |
| [`JsConvert/`](JsConvert) | [JsConvert](https://github.com/dosh1974/SysWeaver.Tools/blob/main/JsConvert/README.md) | Converts JavaScript files and the scripts in html pages to older JavaScript versions (based on [SysWeaver.Converter.Js](../Converter/SysWeaver.Converter.Js/README.md)). Not used by the builds. |
| [`HttpBench/`](HttpBench) | [HttpBench](https://github.com/dosh1974/SysWeaver.Tools/blob/main/HttpBench/README.md) | Performance tests a SysWeaver web server from the command line and writes a markdown report (based on [SysWeaver.HttpBench.Core](../SysWeaver.HttpBench.Core/README.md)). Not used by the builds. |
| [`SetDotNetVersion/`](SetDotNetVersion) | [SetDotNetVersion](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SetDotNetVersion/README.md) | Changes the target framework of all projects in a folder tree and optionally switches the .NET 11 runtime async on or off. Not used by the builds. |

## Limitations and considerations

Only `compress` and `svg_opt` are called by project builds (`Exec` build steps in many SysWeaver projects, and `compress` in `FakeEmailService` / `FakeTextService` of SysWeaver.Services, which reference it as `..\..\SysWeaver\_tools`). Because of that, building the repository as-is requires Windows.

Exit codes of all tools: see [EXIT CODES](https://github.com/dosh1974/SysWeaver.Tools/blob/main/README.md#exit-codes) in the SysWeaver.Tools README.
