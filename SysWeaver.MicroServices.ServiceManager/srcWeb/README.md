# srcWeb

[⬆ SysWeaver.MicroServices.ServiceManager](../README.md)

> Source of the embedded card templates used by the service manager's monitoring tables.

| | |
|---|---|
| **Project** | [SysWeaver.MicroServices.ServiceManager](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

This folder only contains the `services/` sub folder. The `EmbedLocal` target in [`SysWeaver.MicroServices.ServiceManager.csproj`](../SysWeaver.MicroServices.ServiceManager.csproj) runs `Compress.exe -Methods br .\srcWeb\services\* .\web\services` before each build. The generated `web/**/*.br` files (not in git) are embedded as resources and served as `services/...` relative to the site root.

## Contents

| Item | Description |
|---|---|
| [`services/cards.html`](services/cards.html) | Card templates for the table viewer (not a page): `problem` (rows of `ProblemsTable`, shown as cards by default) and `task` (rows of `SchedulerTable`, shown as a table by default) |

## Relationships

- **Part of:** [SysWeaver.MicroServices.ServiceManager](../README.md)
- **Referenced by:** `ServiceManager.ProblemRow` (`[TableDataCard("problem@services/cards")]`) and `ServiceManager.SchedulerTable` (`WithCard("+task@services/cards")`)
