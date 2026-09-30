# SysWeaver.HttpServer.IsoDataModule

[⬆ SysWeaver overview](../README.md)

> HTTP module that publishes the ISO reference data (countries, currencies, languages, phone prefixes) as web tables and serves the related SVG assets.

| | |
|---|---|
| **Layer** | HTTP (module) |
| **Kind** | HTTP module |

## Purpose

Expose [SysWeaver.IsoData](../SysWeaver.IsoData/README.md) to the web UI: debug tables for inspection and assets (such as flags) for editors and pages that display countries or languages.

## Limitations and considerations

- The tables are debug-role endpoints; end-user pages use the assets and the editor integration of [SysWeaver.MicroService.Edit](../SysWeaver.MicroService.Edit/README.md).

## Relationships

- **Project:** [`SysWeaver.HttpServer.IsoDataModule.csproj`](SysWeaver.HttpServer.IsoDataModule.csproj)
- **Builds on:** [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.IsoData](../SysWeaver.IsoData/README.md)
- **Used by:** [SysWeaver.MicroService.Edit](../SysWeaver.MicroService.Edit/README.md)
