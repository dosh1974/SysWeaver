# OnDuplicates

[⬆ _tools](../README.md)

> Pre-built, self-contained Windows x64 build of OnDuplicates, which finds files with identical content in folder trees and lists them, or runs a command for each duplicate.

| | |
|---|---|
| **Project** | [OnDuplicates](https://github.com/dosh1974/SysWeaver.Tools/blob/main/OnDuplicates/README.md) (source in SysWeaver.Tools) |
| **Kind** | Pre-built tool |
| **Used by the builds** | No |

## Purpose

This is the publish output of the `OnDuplicates` project in [SysWeaver.Tools](https://github.com/dosh1974/SysWeaver.Tools), synced into this folder by its [`Publish_All.bat`](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Publish_All.bat) (`.pdb` files and `createdump.exe` are left out, and only the `SysWeaver.*.xml` and `OnDuplicates.xml` documentation files are kept). It is self-contained (.NET 10 runtime included), so no .NET installation is needed to run it.

No build step calls it. [`SysWeaver.MicroService.Avatar/RemoveDuplicates.bat`](../../SysWeaver.MicroService.Avatar/RemoveDuplicates.bat) uses it to delete duplicate files from the avatar `srcData` folder (`OnDuplicates.exe srcData "cmd.exe /C del ""{0}"""`).

## Contents

| Item | Description |
|---|---|
| [`OnDuplicates.exe`](OnDuplicates.exe) | The executable (entry point) |
| `OnDuplicates.dll` | The tool's code |
| [`OnDuplicates.deps.json`](OnDuplicates.deps.json), [`OnDuplicates.runtimeconfig.json`](OnDuplicates.runtimeconfig.json) | .NET dependency manifest and runtime configuration (`net10.0`, runtime 10.0.13 included) |
| `OnDuplicates.xml` | XML documentation of the tool |
| `SysWeaver.*.dll` (5) and matching `SysWeaver.*.xml` | SysWeaver libraries: CommandLine, Common, Docs, ExpressionEvaluator, MicroServices |
| other `*.dll` (~185) | The self-contained .NET runtime (`System.*`, `Microsoft.*`, `coreclr.dll`, `msquic.dll`, ...) |

## Relationships

- **Part of:** [_tools](../README.md)
- **Source:** [OnDuplicates](https://github.com/dosh1974/SysWeaver.Tools/blob/main/OnDuplicates/README.md) in SysWeaver.Tools
- **Builds on:** [SysWeaver.CommandLine](../../SysWeaver.CommandLine/README.md), [SysWeaver.Common](../../SysWeaver.Common/README.md)
- **Used by:** [SysWeaver.MicroService.Avatar](../../SysWeaver.MicroService.Avatar/README.md) (`RemoveDuplicates.bat`)
