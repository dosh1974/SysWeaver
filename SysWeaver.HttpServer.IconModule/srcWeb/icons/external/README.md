# external

[⬆ icons](../README.md)

> Logos of external web services, served at `icons/external/<name>.svg`.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.IconModule](../../../README.md) |
| **Kind** | Icons |

## Purpose

The build optimises `icons\external\*.svg` with `SvgOpt.exe -RemoveSize` (fill kept, compared against the untracked `WebCompareExternal` folder) into `icons\.opt\external\`, which is then compressed into `web/icons/external`. Used as the icons of links to the corresponding external services.

## Contents

| Item | Description |
|---|---|
| [`GoogleMaps.svg`](GoogleMaps.svg) | Google Maps logo |
| [`GoogleSearch.svg`](GoogleSearch.svg) | Google Search logo |
| [`Wikipedia.svg`](Wikipedia.svg) | Wikipedia logo |

## Relationships

- **Part of:** [SysWeaver.HttpServer.IconModule](../../../README.md)
- **Used by:** the table data externals of [SysWeaver.Common](../../../../SysWeaver.Common/README.md) (`Data/TableDataExternals.cs`)
