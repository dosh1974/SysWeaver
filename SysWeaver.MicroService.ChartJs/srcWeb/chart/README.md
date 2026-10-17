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
- The live-update diff matches datasets by `label` and points by label text, so labels act as identity keys.
- Export depends on `application.js` being loadable from `../app/`, so the page assumes it is served from `<root>/chart/`.
- Keyboard shortcuts are attached with `toElement.onkeydown =`, which overwrites any existing handler.

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function (closures inside `CanvasChart.addChart` share its local state); `data` / `config` means a Chart.js configuration object extended with the SysWeaver `ChartJsConfig` fields. Vendored files in `external/` are not documented.

### `chart.js`

First-party chart viewer logic (not to be confused with the vendored Chart.js in `external/chart.js`). Loaded by `chart.html` as a deferred classic script after `common/common.js`, `external/chart.js` (`Chart`), `external/hammerjs.js` and `external/chartjs_plugin_datalabels.js` (`ChartDataLabels`); `chart.html` calls `chartMain()` from `<body onload>`. Defines the global classes `Random`, `CanvasChartOptions`, `CanvasChart` and the global functions `setValue` and `chartMain`. The only code executed at load is the evaluation of the static fields of `CanvasChart` (`CS` reads `document.currentScript.src`).

#### Functions

| Function | Signature | Description |
|---|---|---|
| `setValue` | `(obj: object, path: string, v: any)` | Assigns `v` at a dot-separated `path` below `obj` (e.g. `"options.scales.*x.ticks.font.size"`). Intermediate segments are created as `{}` when missing, except segments prefixed with `*`, which must already exist (otherwise the function returns without assigning). The last segment is always assigned. |
| `chartMain` | async `()` | Entry point of `chart.html`. Shows a loader (`AddLoading()`, removed in `finally` and also passed as `onFirstLoad`), sets `document.body.style.overflow = "hidden"`, reads URL parameters with `getUrlParams()` (lower-cased keys). Without `q` calls `Fail("No query parameter specified!")` and returns. Parses `p` as JSON (POST body), builds a `CanvasChartOptions` (`m`/`mo`/`mc` = `"false"` → `DisableMenu`/`DisableOrderMenu`/`DisableColorMenu`; `l=true` → `ValueLabel` 1, `novalues=true` → 2, `nolabels=true` → 4, `valuelabel=N` overrides; `minvalue`, `maxvalue`, `maxcount` → `MinValue`, `MaxValue`, `MaxCount`; `transparent=true` → `Transparent`) and installs the callbacks `OnServerData`, `OnFixedData`, `OnDefaults`, `OnClick` (see inner functions). Awaits `CanvasChart.addChart(q, null, p, removeLoader, options)` and adds class `ChartCenter` to the returned element when `center=true`. |

#### Inner functions of `chartMain`

| Function | Signature | Description |
|---|---|---|
| `parseCol` | `(text: string) → string` | Colour parameter normaliser: a 3- or 6-character value not starting with `r` gets a `#` prefix (hex without `#`); anything else is returned unchanged. |
| `OnServerData` (assigned to `o.OnServerData`) | `(config)` | Overrides `config.type` with the `type` URL parameter when present and sets `document.title` from `config.Title`. |
| `OnFixedData` (assigned to `o.OnFixedData`) | `(config)` | Applies display URL parameters after `ApplyChanges`: `bordersize` / `bordercol` on dataset 0 (`borderWidth`, `borderColor` via `parseCol`); `scale-font-size` (or `font-size`), `scale-font` (or `font`), `scale-font-style` (or `font-style`), `scale-font-weight` (or `font-weight`, parsed as float) into `ticks.font.*` of the existing `x`, `x2`, `y`, `r` scales via `setValue` with `*` segments; `aspect` → `options.maintainAspectRatio` (`"true"` or false). |
| `OnDefaults` (assigned to `o.OnDefaults`) | `(def: Chart.defaults)` | Applies URL parameters to the global Chart.js defaults on every theme refresh: `legend=false` hides the legend; `textcol`, `backgroundcol`, `bordercol` (via `parseCol`) set `def.color`/`backgroundColor`/`borderColor` and the matching datalabels colours; `font`, `font-style`, `font-weight` set both `def.font` and datalabels font; `font-size` sets both sizes and scales datalabels `borderRadius` (8) and `padding` (4) by size/11; `label-padding`, `label-radius` override those; `line-height` sets `def.font.lineHeight`. |
| `OnClick` (assigned to `o.OnClick`) | `(label, value, datasetIndex, dataIndex)` | Logs the label and posts `{Type: "ChartClick", Label, Value, DatasetIndex, DataIndex}` to `window.parent` with `postMessage` (no target origin; exceptions swallowed). Installed whenever `window.parent` is truthy (always; at top level it is the window itself). |

#### class `Random`

Static-only seeded pseudo-random number generator (sfc32-style 4×32-bit state), used for reproducible colour schemes. Never instantiated.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `GetRandomSeed` | static method | `() → number[4]` | Returns four random int32 values from `Math.random()`, usable as a seed. |
| `SeedFromString` | static method | `(text: string) → number[4]` | Derives four int32 values from a string with four shift/add rolling hashes (not cryptographic). Same text always gives the same seed. |
| `Create` | static method | `(seed: string \| number[4] \| number) → () => number` | Returns a generator function producing floats in `[0, 1)`. A string is hashed with `SeedFromString`; an array supplies the 4 state words; any other value is used as the first word (`seed \| 0`) with fixed constants 19, 74, 11 for the others. |

#### class `CanvasChartOptions`

Plain option bag passed to `CanvasChart.addChart`; created with `new CanvasChartOptions()` (by `chartMain`, or defaulted inside `addChart`).

| Member | Kind | Signature | Description |
|---|---|---|---|
| `DisableMenu` | field | `bool = false` | When true, no menu icon and no keyboard handler are created. |
| `OnServerData` | field | `fn(config) \| null` | Awaited on each freshly fetched server config before it is stored as the original (`orgDataStr`). |
| `OnFixedData` | field | `fn(config) \| null` | Awaited on the config after `ApplyChanges`, before it is rendered / diffed (only in `DoUpdate`). |
| `OnDefaults` | field | `fn(Chart.defaults) \| null` | Called by `UpdateColor` after the theme has been copied into `Chart.defaults`. |
| `OnClick` | field | `fn(label, value, datasetIndex, dataIndex) \| null` | Called from the chart `options.onClick` with the first clicked element. |
| `Transparent` | field | `bool = false` | Adds class `Transparent` to the `<SysWeaver-Chart>` element. |
| `WaitFirst` | field | `bool = true` | When true `addChart` awaits the first `DoUpdate`; otherwise it returns immediately. |
| `ValueLabel` | field | `number \| null` | Data label mode 0–6 overriding the config's `ValueLabel` (see `ApplyChanges`). |
| `DisableOrderMenu`, `DisableColorMenu` | field | `bool = false` | Hide the Order menu / the data colouring part of the Color menu and disable the corresponding keyboard shortcuts. |
| `MinValue`, `MaxValue`, `MaxCount` | field (undeclared, optional) | `number` | Read by `FilterData` / `Limit` when set (set by `chartMain` from URL parameters). |

#### class `CanvasChart`

Static-only class holding shared state and the `addChart` factory; never instantiated. Each `addChart` call creates one live, self-refreshing chart whose state lives in the closure.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `getChartExportMenu` | static async method | `() → Promise<WebMenuItem[]>` | Returns the cached `MenuExportItems`, or loads in parallel `external/canvas2svg.js`, `../app/application.js` (`includeJs`), `../app/application.css` (`includeCss`), all relative to `CS`, plus `getRequest("../Api/GetChartExporters")` (relative to the page), then caches and returns the exporter items. |
| `CS` | static field | `string` | `document.currentScript.src` captured at load; base for the lazy includes. |
| `MenuExportItems` | static field | `WebMenuItem[] \| null` | Cache of the server exporter list. |
| `tweakXmlText` | static method | `(text: string, onXmlDoc: fn(XMLDocument)) → string` | Parses `text` as `text/xml`, lets `onXmlDoc` mutate the document and returns the re-serialized XML. Used to rewrite the SVG root element on SVG export. |
| `FixedColors` | static field | `string[12]` | Display names of the 12 hue steps (30° apart) used in the fixed gradient menu items (`"Red"` … `"Pink"`; index 4 and 5 are both `"Green"`). |
| `GradientFns` | static field | `Map<string, fn(ctx, x0, y0, x1, y1) → CanvasGradient>` | Gradient factories for colour strings: `down` (top→bottom), `up` (bottom→top), `left` (x0→x1), `right` (x1→x0) linear gradients and `cone` (conic gradient starting at −90° around the centre). |
| `addChart` | static async method | `(url: string, toElement?: HTMLElement = document.body, tableParams?: object, onFirstLoad?: fn(), chartOptions?: CanvasChartOptions) → Promise<HTMLElement>` | Creates `<SysWeaver-Chart>` (class `Transparent` if requested) with a `<canvas>` child appended to `toElement`, sets up the menu, keyboard handler, Chart.js plugins/defaults and theme listener, then starts the polling loop `DoUpdate` (fetching `url`, POSTing `tableParams` when given). Returns the `<SysWeaver-Chart>` element (after the first fetch when `WaitFirst`). `onFirstLoad` is called once after the first successful render or the first failure. See inner functions below. |
| `addDataLabels` | static method | `(config)` | Label mode 1: ensures `options.plugins.datalabels`, sets `display = true` and a `formatter` returning `label + "\n" + ValuePrefix + ValueFormat.toString(value, Precision ?? 0) + ValueSuffix`. |
| `onlyDataLabels` | static method | `(config)` | Label mode 2: ensures datalabels, `display = true`, `formatter` returns the x label only. |
| `noLabels` | static method | `(config)` | Label mode 4: ensures datalabels and sets `display = false`. |
| `firstLineDataLabels` | static method | `(config)` | Label mode 3: ensures datalabels, `display = true`, `formatter` returns the first line of the x label (`""` when missing). |
| `formattedValueLabels` | static method | `(config)` | Default label mode (0): if `options.plugins.datalabels` exists sets `display = true` and a `formatter` returning prefix + `ValueFormat.toString(value, Precision ?? 0)` + suffix; otherwise does nothing (the global `ValueFormatter` default applies). |
| `bareValueLabels` | static method | `(config)` | Label mode 6 strategy: if datalabels exists, `display = true` and `formatter` returns `ValueFormat.toString(value, Precision ?? 0)` without prefix/suffix. |
| `valueLabels` | static method | `(config)` | Label mode 5: if datalabels exists, `display = true` and `formatter = null` (raw value rendering by the datalabels plugin). |

#### Inner functions of `CanvasChart.addChart`

Closure state used by these functions: `chart` (the `<SysWeaver-Chart>` element), `canvas` (current `<canvas>`, replaced on rebuild), `main` (the live `Chart` instance), `forceChartType`, `forcedIndexAxis`, `forcedColor`, `forcedColorSeed`, `forceColorLabels`, `forceColorValues`, `forcedOrder` (0 original, ±1 labels, ±(n+2) dataset n, negative = descending), `orgDataStr` (JSON of the last server config), `orgDataType` (`type` + `h`/`v`), `chartScale` (export font scale), `precision`/`valuePrefix`/`valueSuffix` (formatter state), `refreshRate` (ms, default 15000), `themeBackgroundColor`, `gradCache`, the constant maps `noAxisTypes` (`pie`, `doughnut`, `polarArea`) and `keepRAxis` (`polarArea`), the colour mode names `colorRandomValueGradient`, `colorRandomSeriesGradient`, `colorRandomMagnitudeGradient`, `colorLabelText`, the WebMenuItem flag constants `checked` (2), `readonly` (1), `checkedReadonly`, and A4 export sizes (277×190 mm printable area: `A4_wl`×`A4_hl` = 1635×1122 px at 150 DPI, `A4_wh`×`A4_hh` = 3271×2244 px at 300 DPI).

| Function | Signature | Description |
|---|---|---|
| `ValueFormatter` | `(value: any) → string` | Shared formatter for datalabels, ticks and tooltips. Objects are replaced by their `label` or `r` property; numbers become `valuePrefix + ValueFormat.toString(value, precision) + valueSuffix`, other values `valuePrefix + value + valueSuffix`. |
| `rgbToHsl` | `(r, g, b: 0..255) → [h, s, l]` | Converts RGB to HSL with all components in `[0, 1]`. |
| `hslToRgb` | `(h, s, l) → [r, g, b]` | Converts HSL to RGB floats 0..255. Hue is wrapped (`h < 0` → `h + 10`, then `h % 1`). |
| `hue2rgb` (inner of `hslToRgb`) | `(p, q, t) → number` | Standard HSL helper computing one channel. |
| `GetRgbaColorValues` | `(cssColor: string) → Uint8ClampedArray[4]` | Resolves any CSS colour to `[r, g, b, a]` (0..255) by filling a 4×4 rect on a cached off-screen 2D context (`GetRgbaColorValues.Context`) and reading pixel (1,1). |
| `GenerateColorAround` | `(cssColor, count, repeatCount = 13, spread = 120) → string[]` | Generates `count` colours with the saturation/lightness/alpha of `cssColor` and hues fanned from −`spread`° to +`spread`° around it, repeating every `repeatCount` items. A negative `repeatCount` means `ceil(count / -repeatCount)` (min 5); it is capped at `count`. Returns `rgb(...)` strings (or `rgba(...)` when the base is translucent). |
| `gradientCreator` | `(chartContext, gradData: string, cf: GradientFns fn, i: number) → CanvasGradient \| string` | Scriptable-colour backend. Returns `"#000"` until `chart.chartArea` exists. Uses the full canvas (0,0,width,height) as gradient box; `gradCache` is cleared when the canvas size changes and caches per `gradData` string. Parses `gradData` after the `(` at index `i` as `stop;color;stop;color...`; on a real `CanvasRenderingContext2D` creates the gradient with `cf` and adds the stops, otherwise (e.g. the canvas2svg context) returns the first colour. |
| `resolveColor` | `(v: any) → any` | Converts gradient colour strings `name(stops)` whose `name` is in `CanvasChart.GradientFns` into scriptable functions `ctx => gradientCreator(...)`. Arrays: empty → `"red"`, one element → resolved element, otherwise resolved in place and, if any entry became a function, wrapped into a scriptable that picks entry `ctx.dataIndex`. Other values are returned unchanged. |
| `FilterData` | `(data)` | When `chartOptions.MinValue` and/or `MaxValue` is set (0 counts), collects every x index where any dataset value is outside the range and removes those indices (descending) from `labels`, each dataset's `data`, `backgroundColor`, `borderColor`, `fill.above`/`fill.below`, and the same colour arrays of `options.elements.point/line/bar/arc`. Arrays shorter than the longest dataset are left alone. |
| `fixArray` (inner of `FilterData`) | `(arr)` | Splices the collected indices out of `arr` if it exists and is at least as long as the longest dataset. |
| `fixCommon` (inner of `FilterData`) | `(ds)` | Applies `fixArray` to `backgroundColor`, `borderColor` and `fill.above`/`fill.below` of a dataset or element options object. |
| `Limit` | `(data)` | When `chartOptions.MaxCount` is set and the longest dataset exceeds it, truncates labels, data and the same colour arrays as `FilterData` to `MaxCount` entries. |
| `fixArray` (inner of `Limit`) | `(arr)` | `arr.splice(maxCount, valCount - maxCount)` on arrays at least as long as the longest dataset. |
| `fixCommon` (inner of `Limit`) | `(ds)` | Same as in `FilterData`, using `Limit`'s `fixArray`. |
| `ApplyChanges` | `(data)` | Mutates a server config into the render config: `FilterData`; `responsive = true`, `maintainAspectRatio = false`; reads `Precision` (default −2), `ValuePrefix`, `ValueSuffix`; selects the label strategy from `chartOptions.ValueLabel` (or `data.ValueLabel`): 1 `addDataLabels`, 2 `onlyDataLabels`, 3 `firstLineDataLabels`, 4 `noLabels`, 5 `valueLabels`, 6 `bareValueLabels`, else `formattedValueLabels`. For a forced `line` type defaults `borderColor` to `backgroundColor` and `borderWidth` ≤ 0 to 2. Applies `forceChartType`/`forcedIndexAxis` and sets `ticks.precision` = \|precision\| on the `indexAxis` scale (or `y`). For `pie`/`doughnut`/`polarArea` removes `x`/`y` scales, puts the legend left (not reversed) and, with more than one label, generates per-label colours with `GenerateColorAround` where the colour arrays don't match the label count (dataset 0 around its single colour or a default green; later datasets copy dataset 0); for other types a single dataset hides the legend. Removes the `r` scale except for `polarArea` (where a default tick `backdropColor` from `--ThemeBackgroundRGB` is set). Sets datalabels `rotation` −80° on vertical axis charts. Reorders labels and dataset data per `forcedOrder` (inner `reOrder`). Applies `forcedColor` schemes (random value/series/magnitude gradients seeded by `forcedColorSeed`, `LabelText` hashing each label with `Random.Create(label)`, `FixedValue<i>`, `FixedSeries<i>`, `FixedMag<i>` with hues i/12…(i+2)/12, via `MakeRgb`/`MakeRgba`), otherwise resolves gradient strings with inner `resolve`. If dataset 0 has both colours: `forceColorValues` sets datalabels `color` to its border colours; `forceColorLabels` colours legend entries via a `generateLabels` callback and polar `pointLabels.color` (no-axis types) or the index axis `ticks.color`. Then `Limit`, and sets `options.onClick` to forward the first clicked element (label, value, datasetIndex, index) to `chartOptions.OnClick`. |
| `reOrder` (inner of `ApplyChanges`) | `(a: any[])` | Permutes array `a` in place according to the sorted index order `newOrder` (labels by `localeCompare`, or values of dataset `otype − 2`, reversed for descending). Colour arrays are not permuted. |
| `MakeRgb` (inner of `ApplyChanges`) | `(rgb: number[3]) → string` | Returns `rgb(r,g,b)` with integer-truncated channels. |
| `MakeRgba` (inner of `ApplyChanges`) | `(rgb: number[3], o: number) → string` | Returns `rgba(r,g,b,o)`. |
| `resolve` (inner of `ApplyChanges`) | `(ds)` | Runs `resolveColor` on `borderColor`, `backgroundColor` and `fill.above`/`fill.below` of a dataset or `options.elements.*` object. |
| `generateLabels` (assigned to `options.plugins.legend.labels.generateLabels`) | `(chart) → legend items` | Set by `ApplyChanges` in coloured-labels mode on no-axis charts: one legend item per label with `fontColor`/`strokeStyle` = border colour i and `fillStyle` = background colour i. |
| `color` (assigned to `scales.r.pointLabels.color`) | `(p) → colour` | Returns border colour `p.index` for polar point labels (coloured-labels mode). |
| `onClick` (assigned to `options.onClick`) | `(e, els)` | Chart.js click handler set by `ApplyChanges`; see above. |
| `RebuildChart` | `(allowAnimation?: bool)` | No-op until `main` exists. Re-parses `orgDataStr`, runs `ApplyChanges`; unless `allowAnimation`, forces `options.animation.duration = 0` (creating `options`/`animation` as needed). Destroys `main`, replaces the `<canvas>` with a new element, creates a new `Chart` (`Transparent = true`, `Data = data`), calls `update()`, and after 10 ms runs `restore` (a local function-valued variable that puts back the original animation duration / removes the added objects). |
| `GetTitle` | `(data) → string` | Returns `options.plugins.title.text[0]` or `"Chart"`. |
| `GetChartAsImage` | async `(data, width, height, transparent: bool, format: "Png" \| "Svg") → Promise<string>` | Renders `data` off-screen and resolves a data URI. `"Png"`: hidden `<canvas>` of the given size appended to `document.body` with expando `Scale = chartScale` (removed in `finally`), result `toBase64Image()`. `"Svg"`: a `C2S` (canvas2svg) mock context; the serialized SVG root gets `width`/`height` replaced by `viewBox="0 0 w h"` (via `tweakXmlText`) and is base64-encoded (`Base64EncodeString`) into a `data:image/svg+xml` URI. Sets `responsive = false`, `options.animation.duration = 0` (requires `options.animation` to exist), sets the chart's `Transparent` expando and waits for `options.animation.onComplete` (an assigned callback that captures the image and clears itself). Any other `format` throws `Error("Invalid format ...")`. |
| `SaveImage` | async `(closeFn, width, height, transparent, format = "Png")` | Menu action for client-side export. `format === true` means `"Svg"`. Calls `closeFn`, builds a fresh config from `orgDataStr` + `ApplyChanges`, shows `PopUpWorking("Exporting", "WxH", "IconFile" + format)`, renders with `GetChartAsImage` and downloads the result through a temporary hidden `<a download="<title>">`; errors → `Fail`. |
| `ExportChart` | async `(closeFn, name, desc, format, icon)` | Menu action for a server exporter. Calls `closeFn`, rebuilds the config, shows `PopUpWorking("Exporting", desc, icon)`. For `parseInt(format)` 1 or 2 renders the chart with `GetChartAsImage(data, A4_wh, A4_hh, false, "Png" | "Svg")` (1 = Png, 2 = Svg, matching `ChartExportInputTypes`) and sends it as `DataStr` with `Data` null. POSTs `{Data, DataStr, ExportAs: name, Options: {Filename: title}}` to `../Api/ExportChart` with `sendRequest`. A reply with `Mime` and `Data` is downloaded via a temporary `<a download=Name>`; a reply with only `Name` becomes `GetAbsolutePath("../" + Name)`, is copied with `ValueFormat.copyToClipboardInfo` and opened with `Open`. Empty replies or exceptions → `Fail`. |
| `SetChartType` | `(closeFn, newType: string, isHorizontal?: bool)` | Sets `forceChartType = newType` and `forcedIndexAxis` (`"y"` when horizontal, else `"x"`), closes the menu and calls `RebuildChart()`. |
| `SetChartColor` | `(closeFn?, newColor: string \| null)` | Picks a new random `forcedColorSeed`, sets `forcedColor` (`null` = original colours), closes the menu if given and calls `RebuildChart()`. |
| `SetChartScale` | `(closeFn?, newScale: number)` | Sets `chartScale` (font scale used only for PNG exports via `canvas.Scale`); no rebuild. |
| `SetOrder` | `(closeFn?, newOrder: number)` | Sets `forcedOrder` and calls `RebuildChart(true)` (animated). |
| menu icon click handler | async `(ev)` | Created via `new ColorIcon("IconChartMenu", "IconColorThemeAcc2", 32, 32, "Click to show options", handler, null, null, true)` and appended to `chart` (unless `DisableMenu`). `Fail("No data!")` without data; awaits `getChartExportMenu`, rebuilds the config and calls `PopUpMenu(menuIcon.Element, builder, true, true)`. The builder adds class `Chart` to the popup element (`backEl.parentElement`) and returns a `WebMenu` named `Chart` with sub-menus **Type** (`IconChartType`; items `IconChartTypeBar`, `IconChartTypeBarHorizontal`, `IconChartTypeLine`, `IconChartTypeLineHorizontal`, `IconChartTypePie`, `IconChartTypeDoughnut`, `IconChartTypePolarArea`, filtered by `data.ValidTypes` + `orgDataType` and shown only if more than one type is valid; an empty `ValidTypes` array hits an assignment to the `const validTypes` and throws), **Order** (`IconOrder`; Original, Labels, one item per dataset; icons `IconOrderOriginal[Sel]`, `IconOrderAscending[Sel]`, `IconOrderDescending[Sel]`), **Color** (`IconColors`; `IconColoredLabels`, `IconColoredValues` toggles, then unless the colour menu is disabled a separator `Id "-"`, `IconColorOriginal`, `IconColorLabelText`, `IconColorRandomValueGradient`, `IconColorRandomSeriesGradient`, `IconColorRandomMagnitudeGradient`, and three sub-menus of 12 items with `IconColorGradient0`…`11`), **Export** (`IconChartExport`; one item per server exporter whose `Data` is replaced by `() => ExportChart(close, Name, Title, Data, IconClass)` (`sm.Data`), an "Export font size" sub-menu `IconChartSize` with `IconChartSize100/125/150/200/250`, and SVG 16:9 / A4 and PNG HD / 1440p / 4K / A4 low / A4 high DPI items calling `SaveImage`). |
| `onkeydown` (assigned to `toElement.onkeydown`) | async `(ev: KeyboardEvent)` | Keyboard shortcuts (unless `DisableMenu`): `o`/`l`/`g`/`s`/`m` → `SetChartColor` original / label hash / random value / random series / random magnitude (unless the colour menu is disabled); `t`/`v` toggle coloured labels/values and rebuild; Space (unless the order menu is disabled) cycles `forcedOrder` 0 → 1 → −1 → 2 → −2 … → ±(datasets+1) → 0, shows an `Info` toast (1500 ms) and calls `SetOrder`. Logs every key. |
| `renderBackgroundPlugin` (`customCanvasBackgroundColor`) | Chart.js plugin object | `beforeLayout`: when `canvas.Scale` ≠ 1 sets `layout.padding` so that the scaled drawing fills the canvas, else padding 4. `beforeDraw`: `ctx.save()`, `ctx.scale(Scale)`; unless `chart.Transparent` fills the canvas behind the chart (`destination-over`) with `options.color` or `themeBackgroundColor`. `afterDraw`: `ctx.restore()`. |
| `dropShadowPlugin` (`dropShadow`) | Chart.js plugin object | Active when `options.plugins.shadow = {dx, dy, rad, color}`. `beforeLayout` raises `layout.padding` (number or per side) to at least `max(\|dx\|, \|dy\|) + rad`; `beforeDatasetDraw` saves the context and sets `shadowColor` (default `rgba(0,0,0,0.5)`), offsets and blur; `afterDatasetDraw` restores. Both plugins and `ChartDataLabels` are registered with `Chart.register` on every `addChart` call, which also replaces `Chart.defaults.plugins.datalabels` (`display "auto"`, `align "start"`, `anchor "end"`, bold 11px, radius 8, padding 4, `formatter: ValueFormatter`). |
| `callback` (assigned to `Chart.defaults.scales.category.ticks.callback`) | `function(value, index, ticks) → string` | Category tick labels show only the first line (`this.getLabelForValue(value)` up to `\n`, trimmed). The `scale`, `linear`, `logarithmic`, `radialLinear`, `time` and `timeseries` tick callbacks are set to `ValueFormatter`. |
| `label` (assigned to `Chart.defaults.plugins.tooltip.callbacks.label`) | `(context) → string` | Tooltip text `"<dataset label>: " + ValueFormatter(context.raw)`. |
| `UpdateColor` | `()` | Copies theme CSS variables of `document.body` into `Chart.defaults`: `themeBackgroundColor` = `--ThemeBackground`, `font.family` = `--ThemeFont`, font sizes 14 (datalabels 11), `color` = `--ThemeMain`, `borderColor` = `rgba(--ThemeMainRGB, 0.07)`, arc border = background colour with width 1, datalabels background `rgba(--ThemeBackgroundRGB, 0.65)` and colour `--ThemeAcc1`; calls `chartOptions.OnDefaults(def)` and `RebuildChart()`. Run once during `addChart` and 10 ms after each `InterOp` message of type `Theme.Changed` (`InterOp.AddListener`). |
| `HaveArrayOffLen` | `(a, l) → bool` | True when `a` is truthy and `a.length === l`; used by the live diff to decide whether per-point colour arrays must be spliced along with the data. |
| `DoUpdate` | async `()` | Polling loop. Fetches `sendRequest(url, tableParams)` (POST) or `getRequest(url)` (GET); awaits `OnServerData`; ensures `options`, `options.plugins`, `options.layout`; stores `orgDataStr` and `orgDataType`; runs `ApplyChanges`; awaits `OnFixedData`; updates `document.title` from the chart title. First time: `main = new Chart(canvas, data)` (`Transparent = true`) and calls `onFirstLoad` once. Later: updates the title and diffs into `main.data` — datasets matched by `label` (removed, or inserted as placeholders then filled with the new dataset), x positions matched by label text (labels, data and same-length colour arrays spliced), values and array colours copied per index — then `main.update()`. Updates `refreshRate` from `data.RefreshRate` (min 500 ms) and sets `main.Data = data`. On exception: `Fail("Failed to fetch data: ...")`, calls `onFirstLoad` if still pending, waits 3 s. Always reschedules itself with `setTimeout(refreshRate)`. |

## CSS reference

### `chart.css`

Styles for the standalone chart page `chart.html` (linked after `../common/theme.css`, whose theme variables such as `--ThemeBackgroundRGB` it uses) and for the chart popup menu built by `chart.js` with `app/application.js`/`.css` (loaded lazily). Not templated. Mask-icon classes rely on the generic icon rendering of `ColorIcon` (common.js), which paints the element background through `mask-image`; classes that set `background-image` instead show multi-coloured file-type images.

#### Page and chart container

| Selector(s) | Description |
|---|---|
| `body` | No padding/margin, forced 100% width and height, block display. |
| `body.FillScreen` | Height `100svh`. Class `FillScreen` is added to `<html>` and `<body>` by common.js when the URL has a `fillscreen` parameter. |
| `html` | Sets `--Margin: 0` (overrides the theme page margin). |
| `html.Embedded body` | 16px top padding and height `calc(100% - 16px)`; `Embedded` is set on embedded documents by `app/application.js`. |
| `SysWeaver-Chart` | Custom element created by `CanvasChart.addChart`: block, `position: relative`, 100%×100%, background `rgba(var(--ThemeBackgroundRGB), 0.5)`. |
| `html:not([data-theme]) SysWeaver-Chart` | Without a `data-theme` attribute on `<html>` the background is 90% opaque. |
| `SysWeaver-Chart.ChartCenter canvas` | Centres the canvas horizontally (`margin: 0 auto`); `ChartCenter` is added by `chartMain` for `center=true`. |
| `SysWeaver-Chart.Transparent` | Removes the background (`none transparent !important`); class added by `addChart` when `CanvasChartOptions.Transparent`. |
| `SysWeaver-Chart canvas` | Canvas fills the element (100%×100%, border-box, no margin/padding). |
| `SysWeaver-Chart icon-32x32.IconButton` | The 32×32 `ColorIcon` menu button (element `icon-32x32`, default button class `IconButton`), positioned absolutely 0.5em from the bottom-right corner at 10% opacity. |
| `SysWeaver-PopupMenu.Chart SysWeaver-PopupMenuBack` | In the chart popup menu (class `Chart` added to the `SysWeaver-PopupMenu` element by the menu builder in `addChart`), anchors the `SysWeaver-PopupMenuBack` element (created by `PopUpMenu` in application.js) at `bottom: 0`. |

#### Menu and type icons

| Selector(s) | Description |
|---|---|
| `.IconChartMenu`, `.IconChartExport` | Mask icons `../icons/menu.svg` (menu button) and `../icons/newtab.svg` (Export sub-menu). |
| `.IconChartType`, `.IconChartTypeBar`, `.IconChartTypeBarHorizontal`, `.IconChartTypeLine`, `.IconChartTypeLineHorizontal`, `.IconChartTypePie`, `.IconChartTypeDoughnut`, `.IconChartTypePolarArea` | Mask icons from `icons/`: `chart_mixed.svg` (Type sub-menu), `chart_bar.svg`, `chart_bar_horizontal.svg`, `chart_line.svg`, `chart_line_horizontal.svg`, `chart_pie.svg`, `chart_doughnut.svg`, `chart_polar.svg` (Type items). |
| `.IconChartSize` | Mask `../icons/fullscreen_exit.svg` for the "Export font size" sub-menu. |
| `.IconChartSize50`, `.IconChartSize75`, `.IconChartSize100`, `.IconChartSize125`, `.IconChartSize150`, `.IconChartSize200`, `.IconChartSize250` | Mask `icons/chart_pie.svg` with growing `mask-size` (12, 14, 16, 20, 24 px, 28×38 px; 250 uses the default size) for the font scale items. `IconChartSize50` and `IconChartSize75` are only referenced in commented-out JS. |

#### File type and exporter icons

| Selector(s) | Description |
|---|---|
| `.IconFilePng`, `.IconFileSvg`, `.IconFilePdf`, `.IconFileXlsx` | Full-colour `background-image` icons (`../icons/ext/png.svg`, `svg.svg`, `pdf.svg`, `xlsx.svg`) with a transparent background colour. PNG/SVG are used by the client-side export items and `PopUpWorking`; PDF/XLSX are `IconClass` values of server exporters (`ChartJsPdfExporter`, `ChartJsExcelExporter`). |
| `.IconShareLink0`, `.IconShareLink1`, `.IconShareLink2` | Mask icons `../icons/disc.svg`, `../icons/user_check.svg`, `../icons/share.svg` for the `UserStorageChartExporter` items (`"IconShareLink" + scopeIndex`). |

#### Color menu icons

| Selector(s) | Description |
|---|---|
| `.IconColors` | Mask `../icons/color_palette.svg` (Color sub-menu and the three gradient sub-menus). |
| `.IconColoredLabels`, `.IconColoredValues` | Masks `../icons/text.svg` and `../icons/number.svg` for the coloured labels / values toggles. |
| `.IconColorOriginal` | Mask `../icons/home.svg` ("Original" colours). |
| `.IconColorRandomValueGradient`, `.IconColorRandomSeriesGradient`, `.IconColorRandomMagnitudeGradient` | Mask `../icons/gradient.svg` for the random gradient modes (class built as `"IconColor" + mode name`). |
| `.IconColorLabelText` | Mask `../icons/text.svg` for the label hash mode. |
| `.IconColorGradient0`, `.IconColorGradient1`, `.IconColorGradient2`, `.IconColorGradient3`, `.IconColorGradient4`, `.IconColorGradient5`, `.IconColorGradient6`, `.IconColorGradient7`, `.IconColorGradient8`, `.IconColorGradient9`, `.IconColorGradient10`, `.IconColorGradient11` | 28×28 rounded swatches (8px radius, 2px margin, black outline) with `background-image: linear-gradient(in hsl, hsl(i·30, 80%, 70%), hsl(i·30+60, 80%, 70%))`, matching the hue ranges of the `FixedValue<i>` / `FixedSeries<i>` / `FixedMag<i>` colour modes. |

#### Order menu icons

| Selector(s) | Description |
|---|---|
| `.IconOrder` | Mask `../icons/table_log.svg` (Order sub-menu). |
| `.IconOrderOriginalSel`, `.IconOrderAscendingSel`, `.IconOrderDescendingSel` | Selected-state masks `../icons/home.svg`, `../icons/m_sort_asc.svg`, `../icons/m_sort_desc.svg`. |
| `.IconOrderOriginal`, `.IconOrderAscending`, `.IconOrderDescending` | Same masks at `opacity: 0.1 !important` for unselected items (the Order menu builder appends `Sel` for the selected one). |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--Margin` | Set to `0` on `html` here (theme.css default 24px) | Page margin used by the theme.css body padding; zeroed so the chart fills the frame. |
| `--ThemeBackgroundRGB` | `common/theme.css` | `SysWeaver-Chart` background (`rgba(..., 0.5)` / `0.9`). |

#### Animations (@keyframes) and media queries / theme variants

No `@keyframes` and no media queries. The only theme-dependent rule is `html:not([data-theme]) SysWeaver-Chart` (more opaque background when no `data-theme` attribute is set on `<html>`).
