# icons

[⬆ iso_data](../README.md)

> The fall-back icon returned when no flag matches a country, language or phone prefix.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.IsoDataModule](../../../README.md) |
| **Kind** | Icons |

## Purpose

The build runs `SvgOpt.exe -RemoveSize -MaxDecimals 2` on `srcWeb\iso_data\icons\*.svg` into `srcWeb\iso_data\icons\.opt\` (git-ignored) and compresses that into `web/iso_data/icons`. [`IsoDataHttpServerModule`](../../../IsoDataHttpServerModule.cs) looks up the handler of `iso_data/icons/unknown.svg` and returns it for flag requests (`iso_data/country/…`, `iso_data/language/…`, `iso_data/phone_prefix/…`) that don't resolve to a flag in `icons/flags/`.

## Contents

| Item | Description |
|---|---|
| [`unknown.svg`](unknown.svg) | Coloured "unknown" image (from SVG Repo, according to the comment in the file), served at `iso_data/icons/unknown.svg` |

## Relationships

- **Part of:** [SysWeaver.HttpServer.IsoDataModule](../../../README.md)
- **Related:** the flag icons in [SysWeaver.HttpServer.IconModule `srcData/flags/`](../../../../SysWeaver.HttpServer.IconModule/srcData/flags/README.md)
