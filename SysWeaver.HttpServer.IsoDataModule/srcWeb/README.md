# srcWeb

[⬆ SysWeaver.HttpServer.IsoDataModule](../README.md)

> Source of the static web assets embedded in the module, served under `iso_data/`.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.IsoDataModule](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Only the fall-back icon is a static file; the `iso_data/*.js` scripts are generated at run time by [`IsoDataHttpServerModule`](../IsoDataHttpServerModule.cs) and the flags come from [SysWeaver.HttpServer.IconModule](../../SysWeaver.HttpServer.IconModule/srcData/flags/README.md). The build optimises `srcWeb\iso_data\icons\*.svg` with SvgOpt and compresses the result into `web/iso_data/icons`, which is embedded.

## Contents

| Item | Description |
|---|---|
| [`iso_data/`](iso_data/README.md) | Static assets of the `iso_data/` URL folder |

## Relationships

- **Part of:** [SysWeaver.HttpServer.IsoDataModule](../README.md)
