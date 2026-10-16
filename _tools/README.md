# _tools — pre-built tools

[⬆ Back to the SysWeaver overview](../README.md)

Self-contained Windows x64 builds of command line tools built on SysWeaver. Their source is not in this repository: it lives in the [SysWeaver.Tools](https://github.com/dosh1974/SysWeaver.Tools) repository (cloned next to this one), whose `Publish_All.bat` publishes the tools and syncs them into this folder.

| Folder | Tool | Role |
|---|---|---|
| `compress` | [Compress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Compress/README.md) | Pre-compresses web and data assets (the builds use Brotli) during project builds, so embedded files can be served without runtime compression; minifies web files first (its `tools` sub folder holds the minifier and the PNG / shader helpers). |
| `svg_opt` | [SvgOpt](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SvgOpt/README.md) | Optimises SVG icons during builds (based on [SysWeaver.Minifier.Svg](../SysWeaver.Minifier.Svg/README.md)). |
| `decompress` | [Decompress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Decompress/README.md) | Decompresses `.br`, `.gz`, `.zstd` and `.deflate` files (the reverse of `compress`). Not used by the builds. |
| `SwSyncTool` | [SwSyncTool](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwSyncTool/README.md) | Command line folder synchronisation client (push/pull) for [SysWeaver.MicroService.FolderSync](../SysWeaver.MicroService.FolderSync/README.md), plus local `.swcompact` archive operations. Not used by the builds. |
| `SwCurl` | [SwCurl](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwCurl/README.md) | HTTP command line client built on SysWeaver's remote and Tor components. Not used by the builds. |
| `VsClean` | [VsClean](https://github.com/dosh1974/SysWeaver.Tools/blob/main/VsClean/README.md) | Deletes build outputs (`bin`, `obj`, generated `web` / `data`); used by the `Clean_All.bat` scripts, not by the builds. |
| `OnDuplicates` | [OnDuplicates](https://github.com/dosh1974/SysWeaver.Tools/blob/main/OnDuplicates/README.md) | Finds files with identical content; used by `SysWeaver.MicroService.Avatar/RemoveDuplicates.bat`, not by the builds. |
| `ComfyApiToCs` | [ComfyApiToCs](https://github.com/dosh1974/SysWeaver.Tools/blob/main/ComfyApiToCs/README.md) | Converts ComfyUI API format workflows into C# input / output types (based on SysWeaver.AI.ComfyUi). Not used by the builds. |
| `RunComfyUi` | [RunComfyUi](https://github.com/dosh1974/SysWeaver.Tools/blob/main/RunComfyUi/README.md) | Runs a ComfyUI workflow with its inputs as command line options and saves the results. Not used by the builds. |

Only `compress` and `svg_opt` are called by project builds (`Exec` build steps in many SysWeaver projects, and `compress` in `FakeEmailService` / `FakeTextService` of SysWeaver.Services). Because of that, building the repository as-is requires Windows.

Exit codes of all tools: see [EXIT CODES](https://github.com/dosh1974/SysWeaver.Tools/blob/main/README.md#exit-codes) in the SysWeaver.Tools README.
