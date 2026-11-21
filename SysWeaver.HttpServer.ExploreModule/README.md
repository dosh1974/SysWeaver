# SysWeaver.HttpServer.ExploreModule

[⬆ SysWeaver overview](../README.md)

> Developer-facing explorer pages for a running SysWeaver server: API browser, generic interactive table viewer, icon browser, serializer/compression inventories and data references.

| | |
|---|---|
| **Layer** | Web platform |
| **Kind** | Library (HTTP module with embedded web pages) |

## Purpose

Make a running system self-describing. Developers and operators can browse the server's end points and published APIs, open any table-data API in a generic grid, see which icons exist, and inspect which serializers and compressors are available — without extra tooling.

## How it fits into SysWeaver

```mermaid
flowchart LR
  ExpS["ExploreHttpServerService"] --> Mod["ExploreHttpServerModule"]
  Static["StaticDataHttpServerModule"] --> Mod
  Icons["IconHttpServerModule"] --> Mod
  Mod --> Pages["explore pages<br/>end points, api, table, icons, logo"]
  Mod --> Api["debug/explore APIs"]
  Api --> SerComp["serializer and compressor registries"]
  Api --> Refs["table data references"]
```

## Key features

- Append `explore` to any folder URL (e.g. `/explore`, `/Api/explore`) to list the server's end points below it, with type, auth, caching, compression, size and location.
- API page and a generic interactive table page for any table-data API or session data reference (`GetTableDataRefUrl` builds the link).
- Icon gallery of the shared icon set, the app theme page (logo seed designer) with a theme preview (`GetAppSeed`, `GetSeedColors`, `PreviewTheme.css`).
- "How to call APIs" help page (`apihelp.html`) and a csv / tsv viewer (`csv.html`, registered as the `.csv` / `.tsv` extension viewer by `ExploreHttpServerService`).
- Debug tables of the registered serializers, compression formats and the live table data references.
- Menu entries under *Debug* in the web UI.

## Key components

- [`ExploreHttpServerModule`](ExploreHttpServerModule.cs) — the module, request handler and `debug/explore` APIs
- [`ExploreHttpServerModuleParams`](ExploreHttpServerModuleParams.cs) — the auth (default `Debug,Dev,Ops`), compression and client cache options of `BaseHttpServerModuleParams`, and `PerMon` (performance monitoring, on by default)
- [`ExploreItem`](ExploreItem.cs) / [`ExploreData`](ExploreData.cs) — the end point list passed to `explore.js` `Build(data)`
- [`SerData`](SerData.cs) / [`CompData`](CompData.cs) — the rows of the serializer and compression format tables

## Using it

Added by `ExploreHttpServerService` from [SysWeaver.MicroService.HttpServer](../SysWeaver.MicroService.HttpServer/README.md) (after the static data and icon services).

```json
{ "Type": "SysWeaver.MicroService.ExploreHttpServerService, SysWeaver.MicroService.HttpServer" }
```

## Limitations and considerations

- The pages and most APIs are restricted to the Debug, Dev or Ops roles; the module is intended for development and operations, not end users.
- `DataRefTable` returns at most `TableDataTools.DefaultMaxAllowedRows` rows per request (a request with no row limit gets the cap).
- The build pre-compresses the embedded pages with a Windows tool from `_tools`.

## Folder layout

| Folder | Content |
|---|---|
| [`srcData/`](srcData/README.md) | Template of the generated end point list page, loaded by the module |
| [`srcWeb/`](srcWeb/README.md) | Source of the explorer pages served under `explore/` |

## Relationships

- **Project:** [`SysWeaver.HttpServer.ExploreModule.csproj`](SysWeaver.HttpServer.ExploreModule.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.HttpServer.IconModule](../SysWeaver.HttpServer.IconModule/README.md), [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md)
- **Used by:** [SysWeaver.MicroService.HttpServer](../SysWeaver.MicroService.HttpServer/README.md), [SwLanCertManager](https://github.com/dosh1974/SysWeaver.Services/blob/main/SwLanCertManager/README.md), [SwServerManager](https://github.com/dosh1974/SysWeaver.Services/blob/main/SwServerManager/README.md), [TranslationService](https://github.com/dosh1974/SysWeaver.Services/blob/main/TranslationService/README.md)
