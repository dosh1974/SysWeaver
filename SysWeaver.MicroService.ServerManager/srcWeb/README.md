# srcWeb

[⬆ SysWeaver.MicroService.ServerManager](../README.md)

> Source of the embedded server manager web pages.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.ServerManager](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

This folder only contains the `ServerManager/` sub folder. The `EmbedLocal` target in [`SysWeaver.MicroService.ServerManager.csproj`](../SysWeaver.MicroService.ServerManager.csproj) runs `Compress.exe -Methods br .\srcWeb\ServerManager\* .\web\ServerManager` before each build. The generated `web/**/*.br` files (not in git) are embedded as resources and served as `ServerManager/...` relative to the site root.

## Contents

| Item | Description |
|---|---|
| [`ServerManager/`](ServerManager/README.md) | Server, managed services, service/version details, metrics and key file pages |

## Relationships

- **Part of:** [SysWeaver.MicroService.ServerManager](../README.md)
