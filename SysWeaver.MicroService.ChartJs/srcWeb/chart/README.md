# chart

[⬆ srcWeb](../README.md)

> The chart viewer page: loads a chart configuration from a URL and renders it with Chart.js, with a menu for changing the chart type and exporting.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.ChartJs](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Served under `chart/`. `chart.html?q=<url>` fetches the chart data from `<url>` (for example `../Api/TableChart?...`, a stored chart file or a chat attachment) and renders it; further query parameters such as `p`, `m=false`, `transparent`, `noLabels` and `center` adjust the display. The page is opened or embedded in an iframe by the table views of the [API explorer](../../../SysWeaver.HttpServer.ExploreModule/README.md), by [SysWeaver.MicroService.ServerManager](../../../SysWeaver.MicroService.ServerManager/README.md) and by links that [`ChartJsService`](../../ChartJsService.cs) and [`UserStorageChartExporter`](../../UserStorageChartExporter.cs) create.

Processing (SVG optimisation, Brotli compression, embedding) is described in [`srcWeb/`](../README.md).

## Contents

| Item | Description |
|---|---|
| [`external/`](external/README.md) | Vendored libraries: Chart.js, chartjs-plugin-datalabels, Hammer.js, canvas2svg |
| [`icons/`](icons/README.md) | Chart type icons used as CSS masks in the menu |
| [`chart.html`](chart.html) | The page; loads `common/common.js`, the external scripts and `chart.js`, then calls `chartMain()` |
| [`chart.js`](chart.js) | `chartMain` (reads the URL parameters), `CanvasChart` (loading, rendering, chart type switching, export menu backed by `Api/GetChartExporters`), `CanvasChartOptions` and a seeded `Random` for colours |
| [`chart.css`](chart.css) | Page and menu styles, including the icon masks |

## Relationships

- **Part of:** [srcWeb](../README.md), [SysWeaver.MicroService.ChartJs](../../README.md)
- **Talks to:** [`ChartJsService`](../../ChartJsService.cs) web APIs
- **Uses:** `common/common.js`, `common/theme.css`, `app/application.js` / `.css` (export menu) from [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md)
