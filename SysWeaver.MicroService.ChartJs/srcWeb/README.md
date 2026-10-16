# srcWeb

[⬆ SysWeaver.MicroService.ChartJs](../README.md)

> Source of the embedded web assets of the chart service: the chart page and script, chart type icons and the bundled Chart.js libraries.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.ChartJs](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Everything in here is served by the HTTP server under the same relative path, so `srcWeb/chart/chart.html` becomes `chart/chart.html`.

## How it is built

The `EmbedLocal` target of [`SysWeaver.MicroService.ChartJs.csproj`](../SysWeaver.MicroService.ChartJs.csproj) runs before each build (tools in [`_tools`](../../_tools/README.md)):

1. `SvgOpt.exe -Compare .\WebCompare -RemoveFill -RemoveSize` optimises `chart/icons/*.svg` into a temporary `chart/icons/.opt/` folder (deleted first); fills are removed because the icons are used as CSS masks.
2. `Compress.exe -Methods br` writes Brotli compressed copies of the optimised icons to `web/chart/icons`, of the `chart/*` files to `web/chart`, and of `chart/external/` (recursively) to `web/chart/external`.
3. All `web/**/*.br` files are embedded as resources.

## Contents

| Item | Description |
|---|---|
| [`chart/`](chart/README.md) | The chart page (`chart.html`), its script and styles, icons and the vendored libraries |

## Relationships

- **Part of:** [SysWeaver.MicroService.ChartJs](../README.md)
- **Uses:** `common/` and `app/` scripts and styles from [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md)
