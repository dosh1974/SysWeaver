# VsClean

[⬆ _tools](../README.md)

> Pre-built, self-contained Windows x64 build of VsClean, which deletes build output folders (`bin` / `obj` next to a `.csproj`, generated `web` / `data` next to `srcWeb` / `srcData`).

| | |
|---|---|
| **Project** | [VsClean](https://github.com/dosh1974/SysWeaver.Tools/blob/main/VsClean/README.md) (source in SysWeaver.Tools) |
| **Kind** | Pre-built tool |
| **Used by the builds** | No (used by the `Clean_All.bat` scripts) |

## Purpose

This is the publish output of the `VsClean` project in [SysWeaver.Tools](https://github.com/dosh1974/SysWeaver.Tools), synced into this folder by its [`Publish_All.bat`](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Publish_All.bat) (`.pdb` files and `createdump.exe` are left out, and only the `SysWeaver.*.xml` and `VsClean.xml` documentation files are kept). It is self-contained (.NET 10 runtime included), so no .NET installation is needed to run it.

No build step calls it. The `Clean_All.bat` scripts of this repository ([`Clean_All.bat`](../../Clean_All.bat)), of [SysWeaver.Services](https://github.com/dosh1974/SysWeaver.Services/blob/main/Clean_All.bat) and of [SysWeaver.Tools](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Clean_All.bat) run `..\SysWeaver\_tools\VsClean\VsClean.exe .` to clean all projects (a full rebuild is needed afterwards).

## Contents

| Item | Description |
|---|---|
| [`VsClean.exe`](VsClean.exe) | The executable (entry point) |
| `VsClean.dll` | The tool's code |
| [`VsClean.deps.json`](VsClean.deps.json), [`VsClean.runtimeconfig.json`](VsClean.runtimeconfig.json) | .NET dependency manifest and runtime configuration (`net10.0`, runtime 10.0.13 included) |
| `VsClean.xml` | XML documentation of the tool |
| `SysWeaver.Common.dll`, `SysWeaver.Common.xml` | SysWeaver library |
| other `*.dll` (~185) | The self-contained .NET runtime (`System.*`, `Microsoft.*`, `coreclr.dll`, `msquic.dll`, ...) |

## Relationships

- **Part of:** [_tools](../README.md)
- **Source:** [VsClean](https://github.com/dosh1974/SysWeaver.Tools/blob/main/VsClean/README.md) in SysWeaver.Tools
- **Builds on:** [SysWeaver.Common](../../SysWeaver.Common/README.md)
- **Used by:** [`Clean_All.bat`](../../Clean_All.bat)
