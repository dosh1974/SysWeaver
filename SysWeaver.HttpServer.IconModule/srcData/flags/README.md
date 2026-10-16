# flags

[⬆ srcData](../README.md)

> Country and region flag icons, served at `icons/flags/<code>.svg`.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.IconModule](../../README.md) |
| **Kind** | Icons |

## Purpose

The build optimises these files with `SvgOpt.exe -RemoveSize` into `srcData\.opt\flags\` and compresses them into `data/flags/`. [`IconHttpServerModule`](../../IconHttpServerModule.cs) registers every embedded `flags.<code>` resource at `FlagPrefix + "<code>.svg"` (by default `icons/flags/`). The flags keep their colours (fill is not removed). The `id="flag-icons-…"` attributes in the files suggest they come from the open source *flag-icons* set.

## Contents

| Item | Description |
|---|---|
| `<iso2>.svg` (257 files) | Flags by lower case two letter code (ISO 3166-1 alpha-2, plus e.g. [`eu.svg`](eu.svg) and [`un.svg`](un.svg)), e.g. [`dk.svg`](dk.svg), [`us.svg`](us.svg), [`gb.svg`](gb.svg), [`za.svg`](za.svg) |
| Sub-national flags (10 files) | [`gb-eng.svg`](gb-eng.svg), [`gb-nir.svg`](gb-nir.svg), [`gb-sct.svg`](gb-sct.svg), [`gb-wls.svg`](gb-wls.svg), [`es-ct.svg`](es-ct.svg), [`es-ga.svg`](es-ga.svg), [`es-pv.svg`](es-pv.svg), [`sh-ac.svg`](sh-ac.svg), [`sh-hl.svg`](sh-hl.svg), [`sh-ta.svg`](sh-ta.svg) |
| Organisations (3 files) | [`arab.svg`](arab.svg) (Arab League), [`cefta.svg`](cefta.svg), [`eac.svg`](eac.svg) |

## Relationships

- **Part of:** [SysWeaver.HttpServer.IconModule](../../README.md)
- **Used by:** [SysWeaver.HttpServer.IsoDataModule](../../../SysWeaver.HttpServer.IsoDataModule/README.md) (flags by country, language and phone prefix, looked up as `icons/flags/<code>.svg`)
- **Related:** [`../opt/flags/`](../opt/flags/README.md) is an unused optimised copy of the same files
