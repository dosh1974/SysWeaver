# srcWeb

[⬆ SysWeaver.MicroServices.Log](../README.md)

> Source of the embedded log file viewer page.

| | |
|---|---|
| **Project** | [SysWeaver.MicroServices.Log](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

This folder only contains the `logFile/` sub folder. The `EmbedLocal` target in [`SysWeaver.MicroServices.Log.csproj`](../SysWeaver.MicroServices.Log.csproj) runs `Compress.exe -Methods br .\srcWeb\*+ .\web` before each build. The generated `web/**/*.br` files (not in git) are embedded as resources and served as `logFile/...` relative to the site root.

## Contents

| Item | Description |
|---|---|
| [`logFile/`](logFile/README.md) | The *Debug / Log file* page |

## Relationships

- **Part of:** [SysWeaver.MicroServices.Log](../README.md)
