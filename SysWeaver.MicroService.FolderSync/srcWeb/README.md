# srcWeb

[⬆ SysWeaver.MicroService.FolderSync](../README.md)

> Source of the embedded web assets of the folder sync service: the card templates of its folder tables.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.FolderSync](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

The `EmbedLocal` target in [`SysWeaver.MicroService.FolderSync.csproj`](../SysWeaver.MicroService.FolderSync.csproj) runs `Compress.exe -Methods br .\srcWeb\foldersync\* .\web\foldersync` before each build. The generated `web/**/*.br` files (not in git) are embedded as resources and served as `foldersync/...` relative to the site root.

The row types of the three operations tables point at these templates with `[TableDataCard]` (see [`FolderSyncService.Data.cs`](../FolderSyncService.Data.cs)), so the tables can show each row as a card (the "Card templates" section of the [explore README](../../SysWeaver.HttpServer.ExploreModule/srcWeb/explore/README.md#card-templates-card) describes the format).

## Contents

| Item | Description |
|---|---|
| [`foldersync/cards.html`](foldersync/cards.html) | Card templates, not a page: `folder` (`ManagedFolderData`, `ManagedFoldersTable`, table by default), `shared` (`SharedFolderData`, `SharedFoldersTable`, table by default) and `remote` (`RemoteFolderData`, `RemoteFoldersTable`, cards by default; the badge shows the failure count: 1 or more is a warning, 20 or more an error) |

## Relationships

- **Part of:** [SysWeaver.MicroService.FolderSync](../README.md)
- **Used by:** `explore/table.js` of [SysWeaver.HttpServer.ExploreModule](../../SysWeaver.HttpServer.ExploreModule/README.md) (loads the templates)
