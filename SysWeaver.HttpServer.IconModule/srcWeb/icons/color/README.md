# color

[⬆ icons](../README.md)

> Coloured icons that keep their fill, served at the `icons/` root.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.IconModule](../../../README.md) |
| **Kind** | Icons |

## Purpose

The build optimises `icons\color\*.svg` (recursively) with `SvgOpt.exe -RemoveSize` only, so unlike the parent folder the colours are kept. The output is written to `icons\.opt\` itself, not to a `color` sub folder, so these icons are served as `icons/<name>.svg`. The `g_` prefix marks them as graphical (coloured) icons.

## Contents

| Item | Description |
|---|---|
| [`g_protected.svg`](g_protected.svg) | Coloured "protected" icon, served at `icons/g_protected.svg` |
| [`g_warning.svg`](g_warning.svg) | Coloured warning triangle with drop shadow, served at `icons/g_warning.svg` |

## Relationships

- **Part of:** [SysWeaver.HttpServer.IconModule](../../../README.md)
- **Related:** [`icons/`](../README.md), the monochrome icon set served at the same URL level
