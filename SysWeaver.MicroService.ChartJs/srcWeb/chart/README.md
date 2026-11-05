# chart

[⬆ srcWeb](../README.md)

> The chart viewer page: loads a chart configuration from a URL, renders it with Chart.js, keeps it live (polling with animated merging) and offers a menu (type, order, colors, export), keyboard shortcuts and touch long press.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.ChartJs](../../README.md) |
| **Kind** | Embedded web assets (new format page on the Sw core / ui libraries) |

## Purpose

Served under `chart/`. `chart.html?q=<url>` fetches a Chart.js config extended with the C# `ChartJsConfig` fields from `<url>` (relative to `chart/`, or an absolute url of this site: a url of another site, `//host` or a non http(s) scheme is refused in-page with "This chart can not be shown" and nothing is fetched (`Sw.Url.SafeSameOrigin`); for example `../Api/TableChart?{...}`, `../ServerManager/GetCpuChart`, a stored chart file or a chat attachment) and renders it. The page is embedded by the app shell (menu type 6, `WebMenuChart`), the explore tables ("Show as chart"), [SysWeaver.MicroService.ServerManager](../../../SysWeaver.MicroService.ServerManager/README.md) tiles and metric pages, chat attachments and links built by [`ChartJsService`](../../ChartJsService.cs) (`HandleLink` parses `chart/chart.html?` + `q`) and [`UserStorageChartExporter`](../../UserStorageChartExporter.cs).

Processing (SVG optimisation, Brotli compression, embedding) is described in [`srcWeb/`](../README.md). Only this folder plus `external/` and `icons/` are embedded (no new sub folders).

## Contents

| Item | Description |
|---|---|
| [`external/`](external/README.md) | Vendored libraries: Chart.js (`Chart`), chartjs-plugin-datalabels (`ChartDataLabels`), canvas2svg (`C2S`, loaded on demand for svg exports), Hammer.js (kept, no longer loaded: it was never used) |
| [`icons/`](icons/README.md) | Chart type icons used as css masks in the menu |
| [`chart.html`](chart.html) | The page: `../sw/core.js`, `../sw/ui.js`, the vendored scripts, `chart.js` (`data-main` = run the page entry), `../common/theme.css`, `../sw/ui.css`, `chart.css` |
| [`chart.js`](chart.js) | The viewer (`Sw.Chart`, legacy `CanvasChart` / `CanvasChartOptions`, `chartMain`) |
| [`chart.css`](chart.css) | Page / container layout, the menu button and the chart menu icon classes |

## Relationships

- **Part of:** [srcWeb](../README.md), [SysWeaver.MicroService.ChartJs](../../README.md)
- **Talks to:** the `q` url (GET, or POST of `p`), `Api/GetChartExporters`, `Api/ExportChart` ([`ChartJsService`](../../ChartJsService.cs))
- **Uses:** `sw/core.js` (Sw.Net, Sw.Url, Sw.Page, Sw.Theme, Sw.Format, Sw.Util), `sw/ui.js` (Sw.Menu, Sw.Dialog.Working, Sw.Fail / Info toasts, Sw.LongPress, Sw.Hint, Sw.Clipboard, Sw.IconClass), `common/theme.css`, `sw/ui.css` (icon classes such as IconChartMenu, IconOrder*, IconFile*, IconShareLink*) from [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md)

## Behavior

### Loading
1. `chart.js` runs `Sw.Page.Main(chartMain)` when its script tag has `data-main`. `chartMain` shows the loader (`Sw.Page.Loading()`, removed after the first fetch, success or failure; this also signals `PageLoaded` / `LoaderRemoved` to the shell), hides the body overflow and reads the url parameters (keys are case insensitive).
2. Without `q`: an in-page explanation (`div.ChartMissing`: info icon, "No chart selected", how to open a chart and an example url), no toast and no console message.
3. `Sw.Chart.Add(q, body, p, removeLoader, options)` creates `<SysWeaver-Chart>` with a `<canvas>` and (unless `m=false`) the menu button, then polls.

### Url parameters
| Parameter | Effect |
|---|---|
| `q` | Data url (required) |
| `p` | Json POST argument (`Sw.Net.Send(q, p)` instead of a GET) |
| `m=false` | No menu button, no long press menu, no keyboard shortcuts |
| `mo=false` | No Order menu, no Space shortcut |
| `mc=false` | No data coloring part of the Color menu, no c / o / l / g / s / m shortcuts |
| `type` | Overrides the server type |
| `l=true`, `novalues=true`, `nolabels=true`, `valuelabel=0..6` | Data label mode 1, 2, 4, or explicit (see below) |
| `minvalue`, `maxvalue` | Remove x positions where any dataset value is outside the range |
| `maxcount` | Keep the first N x positions |
| `order` | Initial order (the Order menu state): 0 original, 1 / -1 by label, n + 2 / -(n + 2) by the values of dataset n (negative = descending). explore "Show as chart" passes `-2` (largest first) for unsorted tables |
| `solid=true` | Solid theme colors on axis charts with the original colors: one color per dataset from the theme palette `--sw-chart-1..7` (dataset 0 in `--ThemeAcc1`; 85 % fill, opaque border; `applySolidColors`). Used by explore "Show as chart" |
| `timeoffset=<minutes>` / `timeoffset=local` | UTC time labels ("HH:mm", "HH:mm:ss", "MM-dd HH:mm"; every label must match the same pattern) are shown in another time zone: minutes added (ex: the server offset, ServerManager uses it so the history axes match the "server time" clock) or the viewer's local time zone (`shiftTimeLabels`). Other labels are unchanged |
| `axistitles=false` | No automatic axis titles. By default (when the menu is on) axis charts without server axis titles get them (`applyAxisTitles`): the value axis the series names and unit ("Cpu use (%)"), the category axis "Time" (time labels) or "Name" (code names); the method graph (`Api/PerformanceChart`) "Monitored function" / "Number of calls at the same time" (below 500 px height the vertical axis gets the short "Calls at once" / "Monitored function", the long title was cut) |
| `vhist=<url>`, `vhistn=<n>` | Value history (same site url of a history chart): a two value gauge (pie / doughnut, ex: cpu "Use" / "Idle") shows the mean of the last n (default 3) values of the history chart instead of its single sample, also in its title line (`ApplyValueHistory`). ServerManager uses it for the cpu gauges |
| `log=true` | Logarithmic value axis on axis charts (the menu toggle "Log scale"; the value axis title gets "(log scale)", or the title "Log scale"; starts at the power of ten at or below the smallest value (1 for counts), bars are at least 4 px long so values at the start keep a bar; only 1, 2, 5 x 10^n ticks get a text). |
| `bordersize`, `bordercol` | Dataset 0 border width / color (`bordercol` also the default border colors) |
| `textcol`, `backgroundcol` | Default text / background colors (also the data labels); hex without `#` accepted |
| `font`, `font-size`, `font-style`, `font-weight`, `line-height` | Default fonts (data label radius / padding scale with the size) |
| `scale-font`, `scale-font-size`, `scale-font-style`, `scale-font-weight` | Tick fonts of the existing x, x2, y, r scales (default: the `font*` values) |
| `label-padding`, `label-radius` | Data label padding / border radius |
| `legend=false` | No legend |
| `title=false` | No title inside the chart (the embedding card shows it; ServerManager line chart cards) |
| `aspect` | `true` keeps the aspect ratio, anything else not |
| `transparent=true` | No container background (`SysWeaver-Chart.Transparent`) |
| `center=true` | Canvas centered (`ChartCenter`) |
| `fillscreen`, `settheme`, `usetheme`, `mobile`, ... | Core flags (handled by `Sw.Init` / `Sw.Layout`) |

### Server data (ChartJsConfig)
`RefreshRate` (poll interval, default 15000 ms, minimum 500), `ValidTypes` (Type menu filter; empty = no Type menu; shown with at least two valid types; the original type always counts), `Precision` (default -2, negative = up to N decimals; also the index axis tick precision), `ValuePrefix` / `ValueSuffix`, `ValueLabel` (default label mode), `DisableOrderMenu`, `DisableColorMenu`, `Title` (document title), `options.plugins.shadow {dx, dy, rad, color}` (drop shadow plugin). `responsive` is forced on and `maintainAspectRatio` off.

### Rendering
- Label modes: 0 formatted value, 1 label + value, 2 label, 3 first label line, 4 none, 5 raw value, 6 number without prefix / suffix. Category ticks show the first label line; numeric ticks, tooltips and labels use the formatter (`Sw.Format.Number` + prefix / suffix).
- Pie, doughnut, polar area: no x / y scales, legend left (bottom when the container is portrait, updated on resize), per label colors generated around dataset 0's color when the color arrays don't match; the `r` scale only for polar area. One dataset on axis charts hides the legend; vertical axis charts rotate the data labels -80 degrees. Segment fills of pie types get an alpha of at least 0.45 (`MinArcAlpha`; forced color schemes are not changed). Gauge like data (`arcTrack`): a faded segment (all rgba alphas below 0.45) next to strong ones, the servers' "rest" segment (idle cpu, free memory), is drawn in a flat neutral track color (`--ThemeMainRGB` at 12 %), so a 0 % gauge is an empty grey ring instead of a faded red / yellow gradient. Segments with the value 0 get no data label (`hideZeroArcLabels`).
- Group legend (`applyGroupLegend`, axis charts without a forced color scheme or `solid`): when at least 80 % of the labels are "Group / Name" and the bars of different groups have different colors (ex: `Api/PerformanceChart`, where the server picks the hue by system and the shade by name, so "HttpServer" bars are red), the legend lists every group with its bar color after the series, in palette slot order with theme colors (`SwGroupSlots` from `applyThemePalette`: the slot of the group name, see the theme colors below; first appearance otherwise), only the groups of the shown bars (`MaxCount`), groups that share a color in one entry ("ServiceManager · ExchangeRate") (the series are then drawn in neutral grey, filled or outlined like their bars; group items do nothing on click), and the tick labels drop the group. `legend=false` still hides the legend.
- Many categories (`applyDensity`, axis charts): the per point value labels are only drawn while each category gets at least 16 px along the index axis (re-evaluated on every draw; tooltips always work), and a crowded category axis (< 40 px per category) gets vertical x tick labels on the picked categories only (`pickLabeledCategories`: as many labels as fit (line height + 8 px each), the categories with the largest values first, their neighbours unlabeled; scale like labels (times, dates, numbers) every n-th): a label always sits under its own bar, and the tallest bars are labeled (Chart.js' autoSkip labeled every n-th category, so a tall bar could stand above its neighbour's label). Labels are shortened to 30 % of the chart height (14 to 40 characters) by `shortenLabel`: a leading group ("API / ...") is dropped first, then a path keeps its last segment ("…/GetMessages [POST]"), else the end is cut ("…"); the bar tooltip has the full label. Code name labels (`isCodeLabels`: api paths, member names like "API / Api/application/GetMessages [POST]") are shown as readable words (`readableLabels`: "Get messages (POST)", "Handle api http entry"; equal names get their parent path segment, "chat: get messages (POST)"); the tooltip keeps the full name. When the server sets `autoSkip` or a tick callback, Chart.js' autoSkip (8 px padding) is used as before. Server settings for rotation, padding and `display: false` are kept. No data is dropped.
- Theme colors (the default color mode, `applyThemePalette`; not with a forced color mode, "Original", `solid` or `DisableColorMenu`): every chromatic server color is replaced by a color of the theme palette (`--sw-chart-1..7`, ui/tokens.css: copper and steel from the theme accents plus validated extra hues; fallback `FallbackPalette`), keeping its alpha. The fill picks the slots in the server order (before ordering, so a color follows its entity across polls): colors within 8 degrees of hue share a slot; per label colors of "Group / Name" labels share one slot per group, **chosen by the group name** (`stableSlot`: an FNV-1a hash of "sw949:" + name modulo the palette size), so a module has the same color in every app and every load, whatever other modules the app loads (the salt gives the 7 systems every app has 7 different slots with the default palette: HttpServer slot 1, IconHttpServerModule 2, ServiceManager 3, ExploreHttpServerModule 4, FileHttpServerModule 5, API 6, CachedTransformer 7); a gradient string becomes one palette color getting lighter towards its end (the ServerManager donuts); grays stay. Past the palette the hues repeat lighter, then darker. A server drop shadow without a color gets a soft one (`rgba(29,42,51,0.16)`, dark themes `rgba(0,0,0,0.35)`).
- **An app theme** (`appTheme`: `usetheme` / `settheme` with a style sheet, `name@css`, ex: an app with its own UI that frames `chart/chart.html?...&usetheme=hypnos@../styles/themes.css`): the chart starts in "Original" (the server's colors) and without the Design v2 look (`applyDesign`): rounded bars, borders, the drop shadow and the grid are drawn as the server sent them, as the original chart. The color menu still offers the theme colors.
- Theme color slots, predictable for a brand override: **series n takes slot n in legend order** (the n-th distinct hue in the server's order: datasets in order, per label colors in label order; "Group / Name" labels take the slot of their group name, see above, and the group legend lists the groups in slot order). Series 8 and up reuse slots 1.. lighter, then darker. Defaults (`ui/tokens.css`, zero specificity, so `app/app.css` `:root { --sw-chart-n: ... }` replaces a slot):

  | Slot | Light themes (light, light scheme) | Dark themes (dark, hacker, application, pro, dark scheme) |
  |---|---|---|
  | `--sw-chart-1` | Accent 1 (`--sw-accent1`; Light: copper `#A85724`) | Accent 1 (Dark: copper `#E5823F`) |
  | `--sw-chart-2` | blue `#2A78D6` | blue `#3987E5` |
  | `--sw-chart-3` | green `#138A60` | green `#199E70` |
  | `--sw-chart-4` | violet `#6250D6` | violet `#9085E9` |
  | `--sw-chart-5` | magenta `#C4507E` | ochre `#C98500` |
  | `--sw-chart-6` | ochre `#A87400` | Accent 2 (`--sw-accent2`; Dark: steel `#9FB6C8`) |
  | `--sw-chart-7` | Accent 2 (`--sw-accent2`; Light: steel `#4E6B80`) | magenta `#D55181` |

  Replacing one slot keeps the others, so check the whole set: a new slot 3 in ochre sits next to the default ochre slot (light 6, dark 5). The gallery's Brand card (`ui/gallery.html`, "Chart slots") shows the current 7 values of the theme.
- Gradient color strings `down|up|left|right|cone(stop;color;...)` become canvas gradients (cached per chart and canvas size; svg exports use the first color).
- Theme: colors from `--ThemeBackground`, `--ThemeFont`, `--ThemeMain(RGB)`, `--ThemeBackgroundRGB`, `--ThemeAcc1` copied into `Chart.defaults`; re-read on `Sw.Theme.OnChange` (also when another window posts `Theme.Changed`), url overrides re-applied, chart rebuilt. Self-healing (OWN4-Chart-1): `themeSignature()` (theme attributes + computed theme tokens and `--sw-chart-1..12`) is compared with the one the chart was drawn with on resize (once per frame), `load`, `pageshow`, when the page becomes visible, on `Sw.Theme.BrandReady` and 0.25 / 1 / 3 s after the start; a difference (or another `--sw-scale` step) re-reads the colors and rebuilds, so the colors are right from the first paint and stable while resizing; an unchanged signature never redraws.
- Text sizes (`setFontScale`): ticks and legend 14 px, titles 16 px semi bold, value labels 12 px, tool tips 14 px, times the large screen step `--sw-scale` (1, 1.2 at 2560 px, 1.45 at 3440 px; a chart in a small frame, ex: the server page tiles, uses the largest step of its same site parent windows; re-applied when a resize changes the step). Image exports always use the base sizes (times their own "Export font size").
- Turned sideways (`IndexAxis` differs from the server config: Type "horizontal" or the phone method graph): the server x / y scale settings (ex: stacked) move with their axis.
- Design v2 bars and heads (`applyDesign`): bars have no outline (`borderWidth` 0; the fill tells the series), not stacked bars a 4 px rounded data end; the chart title and the legend of axis charts are left aligned (`align: "start"`, unless the server sets an alignment); the method graph explanation is at most 90ch wide.
- Method graph (`Api/PerformanceChart`, chart page with menu): a one line explanation above the chart (`body.ChartWithNote`, `p.ChartNote`: what the light / dark bars mean), busiest first (order -3 = "Max concurrency" descending, unless the url sets `order`); horizontal bars on every screen (readable names, never rotated labels; every bar named, `ChartOptions.LabelAll`); on phones (window narrower than 600 px, or lower than 500 px: phones in landscape) the 10 busiest with a compact legend (on low screens beside the chart) and every bar named (`ChartOptions.LabelAll`: shortened names, straight value ticks, axis title "Function (tap a bar)" on low touch screens); other screens show the 25 busiest (both unless `maxcount`) and the explanation says "Showing the n busiest." (`span.ChartNoteCount`) with a "Show all" / "Show the n busiest" button (`button.ChartNoteAll`, rebuilds the chart); on low screens the explanation is one line with a "More" / "Less" button (`button.ChartNoteMore`, `body.ChartNoteOpen`). The value axis is stable: linear unless the url sets `log=true` or the viewer turned on "Log scale" in the menu (remembered per viewer in localStorage `SysWeaver.Chart.MethodLog`); there is no automatic switch, so the same bars keep their length between loads. A log axis says so in the explanation (`span.ChartNoteLog`: "The value axis uses a log scale ...") and in the axis title ("Number of calls at the same time (log scale)"). `ChartOptions.AutoLogScale` still exists for other pages (the method graph no longer uses it). The category axis title says "hover over a bar for details" with a mouse and "tap a bar for details" on touch screens (a tap shows the tool tip). The "Max concurrency" bars, sent with a 10 % fill, are filled at 35 % (55 % on dark schemes, where 35 % read as a muddy grey; `fillPerformanceBars`), so no bar looks hollow; the opaque "In progress" part stays the dark one.

### Live updates
Every `RefreshRate` ms (also while the page is hidden, like the original chart) the config is fetched again, the view state is applied and the new data is merged into the live chart: datasets matched by label, x positions by label text (labels, data and same length color arrays spliced), values copied, `update()` animates. Type / color changes rebuild from the last server config without animation, order changes animate. Fetch errors show a toast, wait 3 s and keep polling.

### Menu, keyboard, touch
- Menu button top right (10% opacity until hovered / focused; 44 px on touch screens), right click or long press anywhere on the chart, or the context menu key / Shift+F10: a `Sw.Menu` (cascading on desktop, drill down on touch / narrow screens, fully keyboard operable).
- Type (bar / line vertical and horizontal, pie, doughnut, polar area), Order (original, labels, each dataset; picking the selected one flips ascending / descending; colors follow their values), Color (colored labels / values toggles; theme colors (default), original (the server colors), label hash, random value / series / magnitude gradients, 3 x 12 fixed gradients), Export (server exporters, export font size 100-250% for png, svg 16:9 / A4, png HD / 1440p / 4K / A4 150 dpi / A4 300 dpi; every format has a one line description under its name, ex: "Sharp at any size, for documents and slides").
- Keys (not in inputs, not while a menu is open, no modifiers): `c` (theme colors) `o` (original) `l` `g` `s` `m` color modes, `t` / `v` colored labels / values, Space cycles the order (0, 1, -1, 2, -2, ...) with an info toast. Tab reaches the menu button (Enter / Space open the menu).
- A click on a data point posts `{Type: "ChartClick", Label, Value, DatasetIndex, DataIndex}` to `window.parent` (same origin).

### Exports
Client side: off-screen render (device pixel ratio 1, exact size) to png (`toBase64Image`) or svg (canvas2svg, viewBox rewritten), downloaded as `<title>.png` / `<title>.svg`. Server side: `POST Api/ExportChart {Data, DataStr, ExportAs, Options: {Filename}}` (input types 1 / 2 send an A4 300 dpi png / svg as `DataStr`); a file reply (`Mime` + `Data`) is downloaded, a reply with only `Name` is a link (copied to the clipboard and opened in a new tab). A working dialog is shown meanwhile.

### State
View state lives in memory only (no storage keys).

## JavaScript reference (`chart.js`)

One IIFE; globals: `Sw.Chart`, `CanvasChart`, `CanvasChartOptions`, `chartMain`.

| Member | Description |
|---|---|
| `Sw.Chart.Add(url, parent, params, onFirstLoad, options)` | Add a live chart; resolves to the `SysWeaver-Chart` element (`.SwChart` = the `ChartView`), after the first fetch when `options.WaitFirst` |
| `Sw.Chart.Options` | Options class: `DisableMenu`, `DisableOrderMenu`, `DisableColorMenu`, `Transparent`, `WaitFirst` (true), `ValueLabel`, `MinValue`, `MaxValue`, `MaxCount`, `LabelAll` (name every bar of a horizontal chart with at most 30 bars), `Order` (initial order), `IndexAxis` (initial "y" = horizontal), `LogScale` (url log), `Solid` (solid theme colors), `TimeOffset` (url timeoffset: minutes or "local"), `AxisTitles` (automatic axis titles), `ValueHistory` / `ValueHistoryCount` (url vhist / vhistn), callbacks `OnServerData(config)`, `OnFixedData(config)`, `OnDefaults(Chart.defaults)`, `OnClick(label, value, datasetIndex, dataIndex)` |
| `Sw.Chart.View` | `ChartView` class: state (`Type`, `IndexAxis`, `Color`, `ColorSeed`, `ColorLabels`, `ColorValues`, `Order`, `Scale`, `OrgStr`, `Main`, `RefreshRate`, `Fmt`), `Apply(data)`, `Fresh()`, `Rebuild(animate)`, `Describe()` (canvas text alternative: the title, plus label / value of each part for pie / doughnut / polar charts, "Cpu use: Use 7.99 %, Idle 92.01 %"), `Theme()`, `Poll()`, `OnData(data)`, `SetType`, `SetColor`, `ToggleColorLabels`, `ToggleColorValues`, `SetOrder`, `CycleOrder`, `ShowMenu(anchor)`, `MenuItems` / `TypeItems` / `OrderItems` / `ColorItems` / `ExportItems`, `OnKey(ev)`, `SaveImage(w, h, format)`, `Export(exporter)` |
| `Sw.Chart.GetExporters()` | Cached `Api/GetChartExporters` list |
| `Sw.Chart.RenderImage(data, w, h, transparent, format, scale, fmt)` | Off-screen render ("Png" / "Svg") to a data uri |
| `Sw.Chart.ApplyLabelMode(config, mode)` | Apply a data label mode 0-6 |
| `Sw.Chart.GradientFns`, `FixedColors`, `A4`, `Main` | Gradient factories, fixed gradient hue names, A4 export sizes, the page entry |
| `CanvasChart` (legacy) | `addChart`, `getChartExportMenu`, `tweakXmlText`, `MenuExportItems`, `CS`, `FixedColors`, `GradientFns`, `addDataLabels`, `onlyDataLabels`, `firstLineDataLabels`, `noLabels`, `valueLabels`, `bareValueLabels`, `formattedValueLabels` |
| `CanvasChartOptions` (legacy) | = `Sw.Chart.Options` |
| `chartMain()` | The page entry (url parameters above) |

Internal helpers: `Rng` (seeded sfc32), `rgbToHsl`, `hslToRgb`, `cssToRgba`, `generateColorAround`, `themePalette`, `parseCssColor`, `applyThemePalette`, `gradient`, `resolveColor(s)`, `filterData`, `limitData`, `orderData`, `applyColorScheme`, `mergeInto`, `formatValue`, `LabelModes`, the Chart.js plugins `customCanvasBackgroundColor` (export background + font scale) and `dropShadow`, `setupChartJs`, `applyThemeDefaults`, `renderImage`, `parseCol`, `setPath`.

## CSS reference (`chart.css`)

| Selector | Purpose |
|---|---|
| `html`, `body`, `body.FillScreen`, `html.Embedded body` | Full size page, no margin; 16 px top padding inside the shell (`--Margin: 0px` needs the unit: theme.css computes `calc(100% - var(--Margin))` for `html.Embedded`, a unitless 0 made the page taller than the shell frame and cut off the bottom axis labels) |
| `.ChartMissing`, `.ChartMissingIcon`, `.ChartMissingTitle`, `.ChartMissingText` | The "No chart selected" explanation (chart.html without `q`) |
| `SysWeaver-Chart` (+ `.Transparent`, `.ChartCenter canvas`, `html:not([data-theme])`) | Container: 50% theme background (90% in the light theme), none when transparent |
| `.ChartMenuButton` | Menu button (top right, a framed button in the accent 1 text color on the panel background, visible in every theme; 85% opacity until hover / focus / open; 44 px in `.SwMobile` / coarse pointers) |
| `.IconChartType*`, `.IconChartSize*`, `.IconColors`, `.IconColored*`, `.IconColorOriginal`, `.IconColorRandom*Gradient`, `.IconColorLabelText` | Chart menu icon masks (not in ui/icons.css) |
| `.IconOrderOriginal/Ascending/Descending` | Unselected order icons dimmed |
| `.IconColorGradient0-11` | Fixed gradient swatches (hue i*30 to i*30+60) |

## Not allowed (403)

- A refused data request (403, `Sw.Access.IsDenied`) keeps polling every 3 s like the original chart (an answer reloads the page) and shows the shared "not allowed" state (`ChartView.ShowDenied` -> `Sw.Page.ShowNotAllowed`): the whole page for chart.html, else the chart element. No "Failed to fetch data" toasts every 3 s.
