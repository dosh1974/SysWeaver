# external

[⬆ chart](../README.md)

> Third-party browser libraries used by the chart page, stored as distributed (minified where the upstream build is minified).

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.ChartJs](../../../README.md) |
| **Kind** | Vendored library |

## Purpose

The chart page renders entirely in the browser; these scripts are served from `chart/external/` so no CDN is needed. They are Brotli compressed and embedded like the rest of [`srcWeb/`](../../README.md).

## Contents

| Item | Library | Version | Licence | Description |
|---|---|---|---|---|
| [`chart.js`](chart.js) | [Chart.js](https://www.chartjs.org) | 4.5.0 | MIT | The charting library (UMD build, about 200 KB) |
| [`chartjs_plugin_datalabels.js`](chartjs_plugin_datalabels.js) | [chartjs-plugin-datalabels](https://chartjs-plugin-datalabels.netlify.app) | 2.2.0 | MIT | Labels drawn on the data points |
| [`hammerjs.js`](hammerjs.js) | [Hammer.js](http://hammerjs.github.io) | 2.0.7 | MIT | Touch gesture recognition |
| [`canvas2svg.js`](canvas2svg.js) | Canvas 2 Svg (Gliffy Inc., Kerry Liu) | 1.0.19 | MIT | Mock canvas context that produces SVG; loaded on demand by `chart.js` for the export menu |

Version and licence are taken from the header comment of each file.

## Limitations and considerations

- `chart.html` loads `chart.js`, `hammerjs.js` and `chartjs_plugin_datalabels.js` on every page load; nothing in the repository's own chart script references Hammer.js directly.
- Updating Chart.js may require matching changes in the server-side configuration model (`ChartJsConfig` and related classes).

## Relationships

- **Part of:** [chart](../README.md), [SysWeaver.MicroService.ChartJs](../../../README.md)
