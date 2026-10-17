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

## Behavior (web analysis)

### Loading and initialization

1. `chart.html` has an empty transparent `<body class="ThemeBaseBg ThemeMainFg" onload="chartMain()">`. Scripts are all `defer`: `../common/common.js`, `external/chart.js` (Chart.js UMD, global `Chart`), `external/hammerjs.js`, `external/chartjs_plugin_datalabels.js` (global `ChartDataLabels`), then `chart.js`. Styles: `../common/theme.css`, `chart.css`.
2. `chartMain()` (bottom of `chart.js`) shows a loader (`AddLoading()`), hides body overflow, reads the URL parameters through `getUrlParams()` (common.js; **keys are lower-cased**, so `noLabels` and `nolabels` are the same), builds a `CanvasChartOptions` from them and calls `CanvasChart.addChart(q, null, p, removeLoader, options)`. Without `q` it calls `Fail("No query parameter specified!")` and stops.
3. Loaded only when needed: the first click on the menu icon calls `CanvasChart.getChartExportMenu()`, which loads `external/canvas2svg.js` (global `C2S`), `../app/application.js` and `../app/application.css` (with `includeJs` / `includeCss`, relative to `CanvasChart.CS` = `document.currentScript.src`) and fetches `../Api/GetChartExporters`, all at the same time. The result is cached in the static `CanvasChart.MenuExportItems`.
4. Nothing here is a `${Variable}` template. All configuration comes from the URL and the fetched JSON.

### Files

**`chart.html`**: the bootstrap shell described above. There is no markup; `addChart` builds all the DOM.

**`chart.js`** (first-party, about 2.5k lines; it has the same name as the vendored `external/chart.js`, which is Chart.js itself):

- `class Random`: a seeded PRNG (sfc32 style). `GetRandomSeed()` returns 4 random int32s. `SeedFromString(text)` gives 4 int32s from a string hash (not secure). `Create(seed)` takes a string, a `[a,b,c,d]` array or an int and returns a function that produces values in `[0,1)`. It is used by the random and label-hash colour modes.
- `class CanvasChartOptions`: a bag of options with these fields: `DisableMenu`, `OnServerData(config)` (called on the raw server JSON before it is stored), `OnFixedData(config)` (called after `ApplyChanges`, before render), `OnDefaults(Chart.defaults)` (called on each theme refresh), `OnClick(label, value, datasetIndex, dataIndex)`, `Transparent`, `WaitFirst` (await the first fetch), `ValueLabel`, `DisableOrderMenu`, `DisableColorMenu`. `MinValue`, `MaxValue` and `MaxCount` are not declared but are read when present (`chartMain` sets them).
- `class CanvasChart`:
  - Statics: `CS` (script URL), `MenuExportItems`, `FixedColors` (12 colour names for the gradient menus), `GradientFns` (a Map from `down | up | left | right | cone` to a canvas gradient factory), `tweakXmlText(text, fn)` (parses XML, calls `fn`, serializes the result back), `getChartExportMenu()`.
  - `static async addChart(url, toElement = document.body, tableParams, onFirstLoad, chartOptions)`: the main entry point. It creates `<SysWeaver-Chart>` (class `Transparent` if requested) with a `<canvas>` child inside `toElement`, and returns that element. Everything else is a closure inside this function:
    - Per-chart view state: `forceChartType`, `forcedIndexAxis`, `forcedColor` with `forcedColorSeed`, `forceColorLabels`, `forceColorValues`, `forcedOrder` (0 = original, ±1 = by label, ±(n+2) = by dataset n; a negative value means descending), `chartScale`, `orgDataStr` (the JSON string of the last server config, from which every rebuild starts) and `orgDataType` (`type` plus `h`/`v`).
    - Colour helpers: `rgbToHsl`, `hslToRgb`, `GetRgbaColorValues(css)` (paints into a cached 2D canvas and reads the pixel back), `GenerateColorAround(css, count, repeat, spread)` (builds a hue fan around a base colour), and `resolveColor(v)`, which turns strings such as `"down(0;red;1;blue)"` into scriptable gradient functions through `gradientCreator`. Gradients are cached per canvas size in `gradCache`. Arrays become per-`dataIndex` scriptables.
    - `FilterData(data)`: removes every x index where any dataset value falls outside `MinValue`/`MaxValue`. It updates labels, data and per-point colour arrays, including `options.elements.*`. `Limit(data)` truncates to `MaxCount` in the same way.
    - `ApplyChanges(data)`: the transform from server config to render config. It forces `responsive: true` and `maintainAspectRatio: false`, and reads `Precision`, `ValuePrefix` and `ValueSuffix`. It chooses the datalabel mode from `ValueLabel` (0/default formatted value, 1 label plus value, 2 label only, 3 first line of the label, 4 none, 5 raw value, 6 bare number). It applies the forced type and axis, sets tick precision, and for pie/doughnut/polarArea removes the x/y scales, puts the legend on the left and generates per-label colours. When there is a single dataset it hides the legend. It keeps the `r` scale only for polarArea, rotates datalabels -80° on vertical axis charts, reorders data, then either applies a forced colour scheme (`RandomValueGradient`, `RandomSeriesGradient`, `RandomMagnitudeGradient`, `LabelText`, `FixedValue{0-11}`, `FixedSeries{0-11}`, `FixedMag{0-11}`) or resolves the gradient strings in the original colours. Finally it applies coloured labels/values, `Limit`, and an `options.onClick` that forwards to `chartOptions.OnClick`.
    - `RebuildChart(allowAnimation)`: parses `orgDataStr` again, runs `ApplyChanges`, destroys the Chart instance, **replaces the `<canvas>` element** and creates a new `Chart`. When animation is not allowed it temporarily sets `animation.duration = 0`.
    - `DoUpdate()`: the polling loop. It fetches with `sendRequest(url, tableParams)` when `tableParams` is set, otherwise `getRequest(url)`, and runs `OnServerData`. It stores `orgDataStr`, runs `ApplyChanges` and `OnFixedData`, and sets `document.title` from `options.plugins.title.text[0]`. The first time it creates the `Chart` and calls `onFirstLoad`. After that it **diffs** the new data into the live chart: datasets are matched by `label` and x values by label value; it inserts or removes items, copies values and per-point colours, then calls `main.update()`. That keeps the animation smooth and preserves user changes. It reschedules itself with `setTimeout` every `RefreshRate` ms (from the data; default 15000, minimum 500). On error it calls `Fail`, waits 3 s and keeps polling.
    - Export: `GetChartAsImage(data, w, h, transparent, format)` renders off-screen to a hidden canvas (`"Png"`, `toBase64Image`) or to a `C2S` mock context (`"Svg"`; the `<svg>` header is rewritten to a `viewBox` and the result base64-encoded into a data URI) and waits for `animation.onComplete`. `SaveImage(...)` downloads that image through a temporary `<a download>`, named after the chart title. `ExportChart(close, name, desc, format, icon)` POSTs to `../Api/ExportChart` (see below).
    - Menu (unless `DisableMenu`): a `ColorIcon("IconChartMenu", ...)` 32x32 button in the bottom-right corner of `<SysWeaver-Chart>` opens `PopUpMenu` (application.js) with a `WebMenu` named `Chart` and these sub-menus: **Type** (bar/line, each vertical and horizontal, pie, doughnut, polar area, filtered by `data.ValidTypes` plus the original type), **Order** (original, labels, then each dataset, toggling ascending/descending), **Color** (coloured labels/values toggles, plus original, label hash, the three random gradients and three 12-entry fixed gradient sub-menus), and **Export** (the server-provided exporter items, then client-side SVG 16:9/A4 and PNG HD/1440p/4K/A4 low/high DPI, and an "Export font size" sub-menu that sets `chartScale` 1–2.5). Flags use `checked = 2` and `readonly = 1`, the `WebMenuItem` flag bits.
    - Keyboard: a `keydown` handler is assigned to `toElement.onkeydown` (the body on the standalone page). `o`, `l`, `g`, `s` and `m` select colour modes, `t` and `v` toggle coloured labels/values, and Space cycles the sort order with an `Info` toast.
    - Chart.js global setup, which runs for **every** `addChart` call: it registers two inline plugins, `customCanvasBackgroundColor` (fills the theme background unless `chart.Transparent`, and applies `canvas.Scale` padding and scaling for font-scaled exports) and `dropShadow` (reads `options.plugins.shadow {dx, dy, rad, color}`), then `ChartDataLabels`. It replaces `Chart.defaults` datalabels settings and tick callbacks with `ValueFormatter` (prefix + `ValueFormat.toString(v, precision)` + suffix); category ticks show only the first line of a label. `UpdateColor()` copies the theme CSS variables (`--ThemeBackground`, `--ThemeFont`, `--ThemeMainRGB`, `--ThemeMain`, `--ThemeBackgroundRGB`, `--ThemeAcc1`) into `Chart.defaults`, calls `OnDefaults` and rebuilds the chart. It runs once and again on each `InterOp` message `Theme.Changed`.
  - Static label strategies `addDataLabels`, `onlyDataLabels`, `noLabels`, `firstLineDataLabels`, `formattedValueLabels`, `bareValueLabels` and `valueLabels` each change `config.options.plugins.datalabels`.
- `setValue(obj, "a.*b.c", v)`: sets a value at a dotted path. A segment starting with `*` means "only if it already exists", otherwise the segment is created.
- `chartMain()`: the page entry point. It maps URL parameters to options (see Server interaction) and posts `{Type:"ChartClick", Label, Value, DatasetIndex, DataIndex}` to `window.parent` on click.

**`chart.css`**: lays out `SysWeaver-Chart` at 100% width and height, with a 50% (or 90% without `data-theme`) theme background. It defines `.Transparent`, `.ChartCenter canvas` (horizontal centring) and the menu icon position. `html.Embedded body` adds a 16px top padding (`Embedded` is set by `app/application.js` on embedded documents) and `body.FillScreen` uses `100svh` (set by common.js). The rest are mask-image icon classes: chart-type icons from `icons/`, shared icons from `../icons/` (menu, export, file types, order, share). `.IconColorGradient0..11` are CSS `linear-gradient(in hsl, ...)` swatches that match the `FixedValue`, `FixedSeries` and `FixedMag` hues (i/12 to (i+2)/12). `.IconShareLink0..2` and `.IconFileXlsx` belong to exporter menu items that the server supplies.

### Server interaction

- **Chart data contract** (from the `q` URL, either GET or a POST of `p`): a Chart.js config (`type`, `data{labels, datasets[]}`, `options`) extended with the SysWeaver fields of `ChartJsConfig` (C#): `RefreshRate` (ms), `ValidTypes[]` (`bar`, `barv`, `barh`, `line`, `linev`, `lineh`, `pie`, `doughnut`, `polararea`), `Precision` (default -2; a negative value means "up to N decimals"), `Title` (sets `document.title`), `ValuePrefix`, `ValueSuffix`, `ValueLabel` (0–6), `DisableOrderMenu`, `DisableColorMenu`. Colours may be gradient strings `dir(stop;color;stop;color...)`. `options.plugins.shadow` is read by the drop-shadow plugin.
- Typical `q` sources: `../Api/TableChart?{json}` (explore tables), `../ServerManager/Get*Chart[?arg]` (raw JSON APIs), stored user files (`?q=../<file>` from `UserStorageChartExporter`), chat/MCP attachment URLs (`../chat/...` or absolute URLs created by `ChartJsService.BuildAdvancedChart`), and menu items of type 6 (`WebMenuChart`, which `app/application.js` turns into `chart/chart.html?q=../<data>`).
- `GET ../Api/GetChartExporters` returns `WebMenuItem[]`: `Name` is the exporter id, `Title` the description, `IconClass`, and `Data` = `ChartExportInputTypes` as a string (`0` Data, `1` Png, `2` Svg). Anonymous sessions get only the exporters that do not require a user.
- `POST ../Api/ExportChart` with `{Data: ChartJsConfig|null, DataStr: dataUri|null, ExportAs: name, Options: {Filename: title}}`. For input types 1/2 the page renders an A4 high-DPI image and sends it as `DataStr`, with `Data` null. The response is a `MemoryFile`. If it has `Mime` and `Data` (base64), the page downloads it. If it has only `Name` (a link, for example from `UserStorageChartExporter`), the page makes it absolute, copies it to the clipboard and `Open`s it.
- URL parameters read by `chartMain`: `q` (required), `p` (JSON POST body), `m=false` / `mo=false` / `mc=false` (disable the whole menu, the order menu, the colour menu), `type`, `l=true` / `novalues=true` / `nolabels=true` / `valuelabel=N`, `minvalue`, `maxvalue`, `maxcount`, `bordersize`, `bordercol`, `textcol`, `backgroundcol` (a 3- or 6-character hex value without `#` is accepted), `font`, `font-size`, `font-style`, `font-weight`, `scale-font*`, `line-height`, `label-padding`, `label-radius`, `legend=false`, `aspect`, `transparent=true`, `center=true`.

### State and persistence

- There is no localStorage or sessionStorage. All view state (type, order, colours, scale) lives in closures inside `addChart` and is lost on reload.
- Caches: `CanvasChart.MenuExportItems` (exporter menu, per page), `gradCache` (per chart, keyed by canvas size), `GetRgbaColorValues.Context`.
- Messaging: it listens to `InterOp` `Theme.Changed` (BroadcastChannel `SysWeaver` or a window fallback, from common.js). It posts `ChartClick` to `window.parent` through `postMessage` with no target origin argument. Chart.js instances get the expando properties `Transparent` and `Data`; canvases get `Scale`.

### Dependencies

- From `common/common.js`: `getUrlParams`, `getRequest`, `sendRequest`, `includeJs`, `includeCss`, `AddLoading`, `Fail`, `Info`, `delay`, `ColorIcon`, `ValueFormat` (`toString`, `copyToClipboardInfo`), `Base64EncodeString`, `GetAbsolutePath`, `Open`, `InterOp`. From `common/theme.css`: the theme CSS variables.
- From `app/application.js` (loaded lazily): `PopUpMenu`, `PopUpWorking`, `WebMenu`, `WebMenuItem`.
- Vendored: Chart.js 4.5 (`Chart`), chartjs-plugin-datalabels (`ChartDataLabels`), canvas2svg (`C2S`, lazy). Hammer.js is loaded but not referenced by this code.
- Consumers: always through the page URL in an iframe or a link, never by including `chart.js` directly. They are `explore/table.js` (`../chart/chart.html?q=../Api/TableChart?...`), the `app/application.js` menu type 6, ServerManager `server.js` and `serviceInfo.js` (iframes with `transparent=true&m=false&noLabels=true&center=true`), the C# `ChartJsService` and `UserStorageChartExporter`, and the user-storage link handler (`HandleLink` recognises the `chart/chart.html?q=` prefix).

### Structural observations

- The page is a single global script with two classes, the `Random` helper and two global functions (`setValue`, `chartMain`). It is not a module. Almost all logic is one roughly 2,000-line closure inside `CanvasChart.addChart`. Menu building, colour algorithms, export and polling share its local variables.
- `addChart` changes global `Chart.defaults` and calls `Chart.register` on every call, so several charts on one page would share and overwrite defaults (for example `ValueFormatter` precision). In practice every chart is its own iframe or page.
- The array-pruning code is duplicated: `FilterData` and `Limit` contain the same `fixArray` and `fixCommon` helpers. The 12 gradient swatches exist twice: computed in JS and hard-coded in CSS.
- The type ids and the `ValidTypes` vocabulary (`barv`, `barh`, ...), the `ValueLabel` numbers 0–6 and the exporter `Data` codes are implicit contracts shared with C# (`ChartJsConfig`, `ChartExportInputTypes`). They have no shared definition.
- Naming mismatches: `ApplyChanges` case 6 calls `CanvasChart.bareValueLabel` while the static is called `bareValueLabels`. `ExportChart` passes `false` or `true` as the `format` argument of `GetChartAsImage`, which switches on the strings `"Png"` and `"Svg"` (`SaveImage` does map `true` to `"Svg"`).
- The live-update diff matches datasets by `label` and points by label text, so labels act as identity keys.
- Export depends on `application.js` being loadable from `../app/`, so the page assumes it is served from `<root>/chart/`.
- Keyboard shortcuts are attached with `toElement.onkeydown =`, which overwrites any existing handler.
