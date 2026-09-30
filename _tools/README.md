# _tools — pre-built tools

[⬆ Back to the SysWeaver overview](../README.md)

Self-contained Windows executables built from SysWeaver itself. Their source is not part of the repository.

| Tool | Role |
|---|---|
| `compress` | Pre-compresses web and data assets (e.g. to Brotli) during project builds, so embedded files can be served without runtime compression. |
| `svg_opt` | Optimises SVG icons during builds (based on [SysWeaver.Minifier.Svg](../SysWeaver.Minifier.Svg/README.md)). |
| `SwSyncTool` | Command line folder synchronisation client (push/pull) for [SysWeaver.MicroService.FolderSync](../SysWeaver.MicroService.FolderSync/README.md). |
| `SwCurl` | HTTP command line client built on SysWeaver's remote and Tor components. |
| `OnDuplicates`, `VsClean` | Utility tools not used by the build. |

Because the project builds call `compress` and `svg_opt`, building the repository as-is requires Windows.
