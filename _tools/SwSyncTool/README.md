# SwSyncTool

[⬆ _tools](../README.md)

> Pre-built, self-contained Windows x64 build of SwSyncTool, the command line client that pushes and pulls folders to and from a SysWeaver folder sync service and maintains local `.swcompact` archives.

| | |
|---|---|
| **Project** | [SwSyncTool](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwSyncTool/README.md) (source in SysWeaver.Tools) |
| **Kind** | Pre-built tool |
| **Used by the builds** | No |

## Purpose

This is the publish output of the `SwSyncTool` project in [SysWeaver.Tools](https://github.com/dosh1974/SysWeaver.Tools), synced into this folder by its [`Publish_All.bat`](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Publish_All.bat) (`.pdb` files and `createdump.exe` are left out, and only the `SysWeaver.*.xml` and `SwSyncTool.xml` documentation files are kept). It is self-contained (.NET 10 runtime included), so no .NET installation is needed to run it.

No build step or script in this repository calls it; it is a ready to use client for [SysWeaver.MicroService.FolderSync](../../SysWeaver.MicroService.FolderSync/README.md).

## Contents

| Item | Description |
|---|---|
| [`SwSyncTool.exe`](SwSyncTool.exe) | The executable (entry point) |
| `SwSyncTool.dll` | The tool's code |
| [`SwSyncTool.Config.json`](SwSyncTool.Config.json) | Application config; all values are commented out (documents the `AllFolders` / `UserFolders` data path overrides) |
| [`SwSyncTool.deps.json`](SwSyncTool.deps.json), [`SwSyncTool.runtimeconfig.json`](SwSyncTool.runtimeconfig.json) | .NET dependency manifest and runtime configuration (`net10.0`, runtime 10.0.13 included) |
| `SwSyncTool.xml` | XML documentation of the tool |
| `SysWeaver.*.dll` (14) and matching `SysWeaver.*.xml` | SysWeaver libraries: CommandLine, Common (+ Linux / Windows), Compression, Docs, ExpressionEvaluator, FolderSync, MicroServices, Remote, Remote.Connection, Serialization, Storage, Storage.Cdc |
| `BitFaster.Caching.dll`, `CommunityToolkit.HighPerformance.dll` | Third-party libraries |
| other `*.dll` (~195) | The self-contained .NET runtime (`System.*`, `Microsoft.*`, `coreclr.dll`, `msquic.dll`, ...) |

## Relationships

- **Part of:** [_tools](../README.md)
- **Source:** [SwSyncTool](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwSyncTool/README.md) in SysWeaver.Tools
- **Builds on:** [SysWeaver.FolderSync](../../SysWeaver.FolderSync/README.md), [SysWeaver.Storage.Cdc](../../SysWeaver.Storage.Cdc/README.md), [SysWeaver.Remote](../../SysWeaver.Remote/README.md), [SysWeaver.CommandLine](../../SysWeaver.CommandLine/README.md)
- **Talks to:** [SysWeaver.MicroService.FolderSync](../../SysWeaver.MicroService.FolderSync/README.md)
