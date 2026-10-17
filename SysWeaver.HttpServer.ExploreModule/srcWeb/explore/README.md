# explore

[⬆ srcWeb](../README.md)

> The explorer pages, scripts and styles, served under `explore/`.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.ExploreModule](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Each page is a small HTML file with its own script and style sheet, built on the shared web application scripts (`../common/common.js`, `../app/application.js`, `../common/theme.css`) from [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/srcWeb/README.md). The pages call the module's APIs under `Api/debug/explore/` (and `Api/debug/GetApiInfo`) and are linked from the *Debug* menu entries declared on [`ExploreHttpServerModule`](../../ExploreHttpServerModule.cs) (`explore`, `explore/icons.html`, `explore/logo.html`).

## Contents

| Item | Description |
|---|---|
| [`explore.js`](explore.js), [`explore.css`](explore.css) | Script and style of the generated end point list (the `explore` URL), loaded by the template in [`srcData/index.html`](../../srcData/index.html) |
| [`api.html`](api.html), [`api.js`](api.js), [`api.css`](api.css) | API page; fetches the description of an API method from `Api/debug/GetApiInfo` (also loads `tab.js`, the `edit/` scripts and `iso_data/languages.js`) |
| [`table.html`](table.html), [`table.js`](table.js), [`table.css`](table.css) | Generic interactive table viewer for any table-data API, given by the `q` (API url) and `p` (parameter) query arguments |
| [`icons.html`](icons.html), [`icons.js`](icons.js), [`icons.css`](icons.css) | Icon gallery, lists the icons returned by `Api/debug/explore/GetIcons` |
| [`logo.html`](logo.html), [`logo.js`](logo.js), [`logo.css`](logo.css) | Logo seed designer; starts from `Api/debug/explore/GetAppSeed` and previews seeds with `logo_debug.svg?<seed>` |
| [`Link.svg`](Link.svg) | Link icon (no reference to it was found in the scripts or styles) |

## Relationships

- **Part of:** [SysWeaver.HttpServer.ExploreModule](../../README.md)
- **Uses:** [SysWeaver.HttpServer `srcWeb/`](../../../SysWeaver.HttpServer/srcWeb/README.md) (common scripts, application shell, theme), the icons from [SysWeaver.HttpServer.IconModule](../../../SysWeaver.HttpServer.IconModule/srcWeb/icons/README.md)
- **Used by:** the `DataRefTable` / `GetTableDataRefUrl` links of [`ExploreHttpServerModule.cs`](../../ExploreHttpServerModule.cs), which point to `table.html`

## Behavior (web analysis)

> Descriptive analysis of what the web code does, written as input for a later refactoring. It describes behavior, not defects.

### Loading and initialization

All pages are classic (non-module) scripts loaded with `defer`, and every page starts from a global function wired through `<body onload="...">`:

| Page | Entry point | Scripts loaded (in order) | Notes |
|---|---|---|---|
| `<any folder>/explore` (generated) | inline `exploreMain()` -> `Build($(DATA))` | `$(COMMONJSFILE)` (`common/common.js`), `$(JSFILE)` (`explore/explore.js`) | Not a static page: `ExploreHttpServerModule` serves [`srcData/index.html`](../../srcData/index.html) as a `TextTemplate` with `$(NAME)` placeholders (note: `$(...)`, not the `${...}` syntax of the HttpServer templates). `$(DATA)` is the JSON-serialized `ExploreData` object inlined directly into the script. `application.js` is not loaded. |
| `explore/api.html` | `apiMain()` | `common/common.js`, `common/tab.js`, `app/application.js`, `edit/Edit.js`, `iso_data/languages.js`, `api.js` | `Edit.js` (from SysWeaver.MicroService.Edit) dynamically includes `edit/type.js` etc.; `api.js` uses the globals `AddTypeName` / `AddTextElement` defined in `edit/type.js`. |
| `explore/table.html` | `tableMain()` | `common/common.js`, `app/application.js`, `table.js` | Body gets `ThemeBaseBg ThemeMainFg`. |
| `explore/icons.html` | `iconsMain()` | `common/common.js`, `icons.js` | |
| `explore/logo.html` | `logoMain()` | `common/common.js`, `logo.js` | |

Every `*Main()` follows the same skeleton: `const remove = AddLoading(); try { ... } catch (e) { Fail(e, true); } finally { remove(); }` (`tableMain` has no catch; it also passes `removeLoader` to `Table.addTable` as `onFirstLoad`).

### Files

#### `explore.js` / `explore.css` (end point list)

Global functions, no classes. Renders the end points below one URL folder (one row per `ExploreItem`).

- `Build(data)` - entry: `AddLoading()`, `GetSettings()`, clears `document.body`, `BuildNavigator`, `BuildTable`.
- `BuildNavigator(data, set)` - appends `div.ExploreNavigator.ThemeBaseFg` with one `ValueFormat.createLink` per path segment of `data.LocalUrl` (the first is the translated "ROOT"). Parent segments link to `"../".repeat(depth) + "explore" + <current query string>` (`_self`); the last segment links to `./` + query in `_blank`. Links get class `Path` (styled as `SysWeaver-Link.Path` pills with a trailing `/`).
- `BuildTable(data, set)` - sorts `data.Items` in place by `set.Order`, builds `table.Explore` (header row `ThemeAccBg ThemeBaseFg`, `th` cells via `InsertTableCell`), then one row per item, and replaces the first `<table>` in `body` (or appends). Column ids (values of `set.Cols`): 0 icon/type, 1 name, 2 size, 3 extension, 4 auth, 5 time, 6 client cache, 7 server (request) cache, 8 compression preference, 9 pre-compressed. Sort keys (`set.Order`) are the column id + 1 (1-based so that a negative value means descending).
- Per item, behavior depends on `item.Type` (`HttpServerEndpointTypes`: 0 Unknown, 1 Folder, 2 File, 3 Api, 4 FileUpload):
  - Folder: icon `FolderIcon` (or `VirtualFolderIcon` if `Location` does not start with `[Folder]`), link `name + FolderSuffix + query` (navigates into `<name>/explore`), size `<DIR>`.
  - File: icon `ExtIconBase + ext + ".svg"` (`bin` if no extension), link to the file (new window when embedded), size in bytes; the link is created in "view" mode via the `ValueFormat.createLink` flags.
  - Api: icon `ApiIcon`, link `IconBase + "../explore/api.html?q=" + LocalUrl + name` (opens the API page), size `[API]`.
  - Unknown / FileUpload: icon `ApiIcon`, size `[???]` / `[UP]`.
  - `newWindow` is forced false when the page is top level (`window.location === window.parent.location`), i.e. new windows are only used when embedded in the app shell iframe.
- Cell helpers: `IntToString` (space thousands separator), `SetByteSize` (text + kb/Mb/Gb tooltip; returns true for values shown dimmed), `SetDateString` -> `ValueFormat.updateDateTime`, `SetCacheDuration` (`-` when <= 0, `∞` when > 1 500 000 000 s), `GetFileExtension(name, type)`.
- Auth column: `icons/g_protected.svg` + one span per comma-separated token, or a dimmed `g_warning.svg` for public resources. Compression column: one span per `method:quality` entry.
- Alternating accent classes (`ThemeMain`, `ThemeAccFg`, `ThemeAccBg`/`ThemeMainBg`) are toggled per column through a local `accent` flag.
- `AddSortHeader(data, set, el, index, name, title, extra)` - sort icon (`IconBase + m_sort_asc.svg|m_sort_desc.svg`, dimmed unless it is the primary sort) plus optional title span; click -> `DoSort`, which moves the key to the front of `set.Order`, rebuilds the table and persists the settings.
- CSS: `.Explore` table, `.ExploreNavigator`, per-column `th.Explore*H` / `td.Explore*` classes, `.Dimmed`.

#### `api.html` / `api.js` / `api.css` (API inspector and tester)

`apiMain()` builds a `SysWeaver-Page.PadBottom` containing:

1. Header section (`SysWeaver-ApiSection`): `SysWeaver-ApiTag` "GET" or "POST" (POST when the API has an argument), `SysWeaver-ApiUrl` with `data.Uri` (tooltip and copy-on-click with the absolute URL, computed by stripping the last two path segments - `explore/api.html` - from `location.href`), and, when `data.Auth != null`, a lock `ColorIcon("IconSecured")` plus one tag per auth token.
2. Optional description section (`SysWeaver-ApiDesc`).
3. Optional cache/compression section: tags `CLIENT {n}s`, `SERVER {n}s` and one tag per `CompPreference` entry (`method:quality`).
4. Signature section (`.ApiSignature`, monospace): return type via `AddTypeName` (or a google-search link `[mime]` for raw-mime APIs, or `void`), the method name (last URL segment), `(`, argument type + `ArgName`, `);`.
5. When the API has an argument: a `Tab("Api.Args")` with two tabs - "Parameters" (an `Edit` form for `data.Arg`) and "JSON" (rendered on tab select via `ValueFormat.jsonToHtml(JSON.stringify(input.GetObject()))`, click copies).
   - Initial object: `Edit.CreateDefault(data.Arg, null, true)`, overlaid with the last sent value from `localStorage["SysWeaver.Api." + url]` (primitive: replaces; object: `Object.assign` without `$type`), then `Edit.CleanUp`.
   - Any `EditChange` event removes previous results (the handler also walks the `detail.Next` chain to compute a member path, which is not used further).
6. `SysWeaver-ApiSendButton` with a `Button` ("Send API call", icon `IconSendIt`). On click it persists the argument (without `$type`) to localStorage, calls `sendRequest(fullUri, obj, true, null, isRaw)` or `getRequest(fullUri, true, isRaw)`, then builds a `Tab("Api.Results")`:
   - success + JSON: "RESULT" tab (read-only `Edit` of `data.Return` via `EditOptions.ReadOnly = true`) and "JSON" tab (highlighted JSON, click copies);
   - raw mime: "PREVIEW" tab with an `img` (image/*) or `iframe` showing a blob object URL, click opens it in a new tab;
   - no return value: "VOID"; error: "ERROR" tab with class `Error`.
   Results are removed by deleting every sibling after the button.

CSS relied on: custom elements `SysWeaver-ApiSection`, `SysWeaver-ApiTag`, `SysWeaver-ApiUrl`, `SysWeaver-ApiDesc`, `SysWeaver-ApiSendButton`, `SysWeaver-TabContent.CODE` (iframe/img sizing), mask icons `.IconSecured` (`icons/lock.svg`) and `.IconSendIt` (`icons/table_api.svg`); plus `common/tab.css` and `edit/Edit*.css`.

#### `table.html` / `table.js` / `table.css` (generic data table - the reusable component)

`table.js` defines the globals `insertTH(trow)`, classes `TableDataProps`, `TableDataFilterOps`, `TableDataFilter`, `TableDataState`, `Table` (static members only), and `tableMain()`.

- `TableDataProps` - bit flags mirroring C# `TableDataColumnProps` (`CanSort 1, SortedDesc 2, Hide 4, Filter 8, TextFilter 16, OrderFilter 32, CanChart 64, IsKey 128, IsPrimaryKey1..7` encoded in bits 256/512/1024), plus computed `AnyFilters`, `AnyKey`.
- `TableDataFilterOps` - numeric ops mirroring C# `TableDataFilterOps` (0 Equals ... 12 OutsideRange, `Count = 13`), with parallel arrays `Names`, `Descriptions` (translated) and `Props` (which column prop flag enables each op), and `DefaultOrders` (`Contains`, `Equals`, `GreaterThan`) used to pick a default op per column.
- `TableDataFilter` - `{ Invert, CaseSensitive, Op, Value, ColName, ColumnIndex }`, sent as the C# `TableDataFilter`.
- `TableDataState` - persisted UI state `{ FilterRows, Filters (array of filter sets, each an array of TableDataFilter per visible column), Expanded (0 hidden, 1 simple, 2 advanced), RequestParams, AutoRowCount, TimeMode (null or "utc") }`; mirrors C# `TableDataState`.
- **`Table.addTable(url, toElement, rowsPerPage, tableParams, onFirstLoad, tableName, stateStr, onFirstData, filter)`** - the main reusable entry point:
  - State: key `"SysWeaver.Table." + await hashString(url)`; the state comes from `stateStr` (when given) or localStorage, is JSON-parsed and completed with defaults; `filter` (JSON, one filter or an array) replaces `state.Filters` with a single filter set. `requestParams.Param = tableParams`. `rowsPerPage` is defaulted but not otherwise used (the page size is `RequestParams.MaxRowCount`, adjusted by auto sizing).
  - Default request: `{ Cc: 0, Row: 0, MaxRowCount: 25, LookAheadCount: 1, Order: null }`.
  - DOM: `table.DataTable` (expando properties `Info {Name, Url}`, `Cols`, `HomeIcon`, `PrevIcon`, `NextIcon`, `ExportIcon`, `ClearSearch`) followed by a `datatable-footer` with `ColorIcon`s Home/Prev/Next/Export, a search `<input>` (sets `RequestParams.SearchText`) and a clear-search icon.
  - Update loop (`Update` / `UpdateNow`): collects the non-empty filters of all filter sets into one flat `requestParams.Filters` list, sets `requestParams.Cc = cc` (change counter from the last response) and calls `sendRequest(url, requestParams, didChange)` (POST). On the first response: when no rows came back and `Row > 0`, it resets to row 0 and requests again; when no name was given it uses `data.Title`. Then it enables the nav icons (`Next` when `data.RowCount > Row + Rows.length`), calls `tableRedraw`, stores `cc = data.Cc` and reschedules itself with `setTimeout(Update, data.RefreshRate)` (<= 0 becomes 5000 ms, minimum 100 ms). Errors or null data retry after 3000 ms. `onChangeFn` = save + `UpdateNow()` (cancels the timer and fetches immediately); an `isUpdating` flag serializes updates by polling `delay(10)`.
  - Auto row count: when `AutoRowCount` is set and on the first page, `onResizeFn` measures the average row height and the space above the footer and adjusts `MaxRowCount` (5..100, multiple of 5); also hooked to `window.resize`.
  - Export: the export icon loads `app/application.js` / `.css` via `includeJs` / `includeCss` (relative to `Table.CS = document.currentScript.src`) and `getRequest("../Api/GetTableExporters")` (cached in `Table.MenuExportItems`), then shows a `PopUpMenu` with sub-menus "Current view" (mode 0: `sendRequest("../Api/ExportTableData", {Data: latestData (with Cols), ExportAs, Options: {Filename}})`), "Filtered" (mode 1) and "All" (mode 2, clears `Filters` / `SearchIndex` / `SearchText`), the latter two via `sendRequest("../Api/ExportTableApi", {Api: url, Req, ExportAs, Options})`. The returned `MemoryFile` is downloaded from a `data:` URL when `Mime` and `Data` are present; when only `Name` is set, the absolute URL of `../<Name>` is copied to the clipboard and opened.
- `Table.tableRedraw(table, data, state, onChangeFn, saveFn, didChange)` - when `data.Cols` is present it (re)builds the header row (the first `th` is a filter icon cycling `state.Expanded`, then one `th` per non-hidden column with a sort `ColorIcon` + title span) and `FilterRows` x 3 filter rows; otherwise it reuses `table.Cols` (the server can omit `Cols` on later responses). Data rows are reused in place: column 0 shows the 1-based row number; each cell caches `value + "|" + format` in an expando `Value` and only calls `ValueFormat.update(cell, type, value, flash, fmt, next, onChangeFn)` when it changed. Rows are either generic (`row.Values[colIndex]`) or typed objects (`row[colName]`), detected from the first row. `System.DateTime` columns without `Format` use `state.TimeMode`. The `Flash` class is passed for changed cells of the same data row during periodic refreshes (not after user changes).
- `Table.tableUpdateHeader(cols, table, state, onChangeFn, saveFn)` - sets the sort icon (`IconAscending` / `IconDescending`), classes `CurrentSort` / `Sort` / `NoSort`, tooltips, and a click / context-menu `PopUpMenu` (`WebMenu` named `TableHeader`) with: Sort ascending / descending / Clear sort, Copy title / name / description / type, "Show as chart" (when `Table.canShowChart()` and the column has `CanChart`), which navigates to `../chart/chart.html?q=../Api/TableChart?{TableApi, Keys (primary-key column names ordered by IsPrimaryKeyN), Values: [col], Options (request params with MaxRowCount 1000), Title}`, and for un-formatted `System.DateTime` columns a local/UTC toggle.
- Sorting helpers `haveSort`, `isCurrentSort`, `isReverseSort`, `removeSort`, `updateSort(requestParams, colName, defaultDesc)`: `Order` is an array of column names, a `-` prefix means descending, the clicked column moves to the front, at most 3 entries. Every sort change resets `Row` to 0.
- `Table.addFilterRow(table, state, rowIndex, columnCount, onChangeFn, saveFn, cols)` - inserts 3 rows (`tr.FilterRow`, hidden with `HideRow` according to `Expanded`): (1) an op `<select>` per column, options disabled unless the column props enable that op; the first cell is Reset (first set: resets all filters, removes extra sets, clears the search) or Remove-set; (2) a value `<input>` per column (fires on keyup/change), first cell Clear; (3) two `table-check` toggles per column - `!` (Invert) and `A ≠ a` (CaseSensitive, TextFilter columns only), first cell `+` adds a filter set (max 3). Data rows start at index `1 + FilterRows * 3`.
- `Table.tableUpdateFilterIcon(table, state)` - shows/hides the filter rows according to `Expanded` (1 shows only the value row of the first set).
- `Table.canShowChart()` - cached `getRequest("../Api/TableChartAvailable")` (false on error).
- `tableMain()` - reads the URL params (see below) and calls `Table.addTable(q, null, r, p, removeLoader, n, s, data => document.title = data.Title (unless n), f)`.
- `table.css` - besides the table chrome (`.DataTable`, `th.Sort/NoSort/CurrentSort`, `tr.FilterRow`, `tr.HideRow`, `table-check`, `datatable-footer`, and `html.SwSelfWide` / `html.SwSelfNarrow` layout variants set by the app shell), it holds the cell styles emitted by `ValueFormat.update` in `common.js` (`td.ValueFormatImg`, `ValueFormatJson`, `ValueFormatText`, `ValueFormatMD`, `ValueFormatDt` / `ValueFormatDtutc` / `ValueFormatDtlocal`, `ValueFormatWebColor`, `Flash`, `Null`, `False`, `Toggle`, `SysWeaver-toggle`, `SysWeaver-action`) and mask icon classes (`IconAscending`, `IconPlus`, `IconExport`, `IconFileCsv` ..., `IconCopy`, `IconClock`, ...) pointing to `../icons/*.svg`. The `body` / `html` rules (`overflow-y: hidden`, centered text) assume the table is the whole page.

#### `icons.html` / `icons.js` / `icons.css`

`iconsMain()` fetches `GetIcons` (GET, or POST `{Param: path}` when the `path` URL param is set) and renders a `SysWeaver-IconList` of `SysWeaver-IconItem`s (an `img` loaded through `PendingImages` on a checkerboard background, plus a `SysWeaver-IconName`). Clicking copies the relative URL (`ValueFormat.copyOnClick`); the tooltip shows size and `Location`. Item size is computed in CSS from the viewport.

#### `logo.html` / `logo.js` / `logo.css`

`logoMain()` gets the current seed (`GetAppSeed`), then `Create()` renders 10 `SysWeaver-IconItem`s: item 0 is the selected seed, items 1-9 random 31-bit seeds; each `img` is `../logo_debug.svg?<seed>` with a `SysWeaver-IconSeed` caption. Clicking copies the seed and swaps it into the first slot. A `Button` "Randomize" (`IconDice` -> `icons/reload.svg`) regenerates. The CSS is a near copy of `icons.css` with a highlighted first item.

#### `Link.svg`

Static 18x18 link glyph (`currentColor`); not referenced.

### Server interaction

| Caller | Endpoint | Method / payload | Owner |
|---|---|---|---|
| generated `explore` page | `<folder>/explore` (`ExploreHttpServerModule.Handler` claims any URL ending in `explore`) | HTML template, `$(DATA)` = `ExploreData` | ExploreModule |
| `api.js` | `../Api/debug/GetApiInfo` | POST, body = the API url string | `TypeService.GetApiInfo` in SysWeaver.MicroService.Edit |
| `api.js` | the tested API (`fullUri`) | POST with the edited object, or GET when there is no argument; Blob response when `Mime` is set | any |
| `table.js` | the table API given by `q` | POST `TableDataRequest` on every refresh | any `TableData X(TableDataRequest)` API |
| `table.js` | `../Api/GetTableExporters`, `../Api/ExportTableData`, `../Api/ExportTableApi` | GET / POST | `ExploreHttpServerService` in SysWeaver.MicroService.HttpServer |
| `table.js` | `../Api/TableChartAvailable`; navigation to `../chart/chart.html?q=../Api/TableChart?...` | GET | `ChartJsService` in SysWeaver.MicroService.ChartJs |
| `icons.js` | `../Api/debug/explore/GetIcons` | GET, or POST `{Param: path}` (a `TableDataRequest`) -> `IconInfo[]` (`Url`, `Name`, `Size`, `Location`) | ExploreModule (`Roles.Dev`) |
| `logo.js` | `../Api/debug/explore/GetAppSeed` -> int; images `../logo_debug.svg?<seed>` | GET | ExploreModule (`Roles.Debug`) |

Metadata contracts consumed:

- **`ExploreData`** (inlined JSON): `FolderSuffix` (`/explore`), `FolderIcon`, `VirtualFolderIcon`, `ApiIcon`, `IconBase`, `ExtIconBase`, `MimeIconBase` (relative to the current folder), `BaseUrl` (absolute URL of the folder), `LocalUrl` (server-relative folder), `Items[]` of `ExploreItem` (`Name`, `Method`, `Type`, `ClientCacheDuration`, `RequestCacheDuration`, `PerSession`, `CompPreference` such as "br:Best, deflate:Best", `PreCompressed`, `Auth` as comma-joined tokens / "" / null, `Location`, `Size`, `LastModified`, `Mime`). Template variables: `TITLE`, `COMMONJSFILE`, `JSFILE`, `CSSFILE`, `STYLECSSFILE`, `FOLDERICON`, `DATA`.
- **`ApiInfo`** (from `GetApiInfo`): `Uri`, `Auth` (null = public, "" = any authenticated user, else comma-separated tokens), `Mime` (raw response mime), `Desc`, `ClientCacheDuration`, `RequestCacheDuration`, `PerSession`, `CompPreference`, `Arg` / `Return` (`TypeDesc`, consumed by `Edit` and `AddTypeName(...TypeName...)`; `Return.Summary`), `ArgName`, `ArgSummary`.
- **`TableDataRequest`** sent: `Cc`, `Row`, `MaxRowCount`, `LookAheadCount`, `Order[]`, `Filters[]` (TableDataFilter), `Param`, `SearchText` (`SearchIndex` is only cleared, on export).
- **`TableData`** received: `Cols[]` (`Name`, `Title`, `Desc`, `Type` = C# full type name, `Format`, `Props`), `Rows[]` (`{Values: [...]}` or typed objects), `RowCount`, `Title`, `Cc`, `RefreshRate` (ms).
- Column `Format` strings (e.g. from `[TableDataUrl(text, "*table.html?q=...", title)]` on C# row types) are interpreted by `ValueFormat.update` in `common.js`; relative URLs in them resolve against `explore/`, where `table.html` lives.

URL parameters:

- `api.html`: `q` = API url relative to the site root (e.g. `Api/debug/explore/GetIcons`).
- `table.html`: `q` = table API url (relative to `explore/`, normally `../Api/...`), `p` = string `Param`, `jp` = JavaScript source run as an async function body whose return value becomes `Param` (overrides `p`), `n` = title / table name, `r` = rows per page (passed on but unused), `s` = initial state JSON (used instead of the stored state, e.g. `{"RequestParams":{"Order":["-Id"]}}`), `f` = filter JSON (one `TableDataFilter` or an array).
- `icons.html`: `path` = folder to list (server default `icons/`).
- generated `explore` page: the incoming query string is kept on all navigation links.

### State and persistence

| Key / place | Content | Writer |
|---|---|---|
| `localStorage["ExploreSettings"]` | `{Order: [sort keys], Cols: [10 column ids]}` (defaults `[1,2]` and `[0,1,2,5,3,6,7,4,8,9]`; no UI changes `Cols`) | `explore.js` |
| `localStorage["SysWeaver.Api." + url]` | last argument object sent to that API (without `$type`) | `api.js` |
| `localStorage["SysWeaver.Table." + hashString(url)]` | full `TableDataState` (filters, sort, search, page, row count, expanded, time mode), saved on every change | `table.js` |
| `Tab("Api.Args")`, `Tab("Api.Results")` | tab ids (selection handling in `common/tab.js`) | `api.js` |
| Static class fields | `Table.MenuExportItems`, `Table._CanShowChart`, `Table.CS` | `table.js` |
| DOM expandos | `table.Info/Cols/ClearSearch/*Icon`, `cell.Icon/Text/Value/Expanded`, `trow.DataRow`, `table-check.Update` | `table.js` |

No cross-window messaging is used here; embedding in the app shell is detected only through `window.parent.location` in `explore.js`, and handled by the `html.SwSelfWide/SwSelfNarrow` classes in `table.css`.

### Dependencies

- From `common/common.js`: `AddLoading`, `Fail`, `getUrlParams`, `getRequest`, `sendRequest`, `hashString`, `delay`, `AbortHandler`, `includeJs`, `includeCss`, `createImg`, `createIFrame`, `PendingImages`, `ColorIcon`, `Button`, `ValueFormat` (`createLink`, `copyOnClick`, `copyToClipboardInfo`, `update`, `updateText`, `updateDateTime`, `jsonToHtml`, `joinNonEmpty`, `removeCamelCase`), `badClick`, `isPureClick`, `keyboardClick`, `Open`, `GetAbsolutePath`, `_T`, `_TF`; `common/theme.css` variables (`--ThemeMain`, `--ThemeAcc1/2`, `--ThemeRad`, `--ThemeIconSize`, ...).
- From `app/application.js`: `PopUpMenu`, `PopUpWorking`, `WebMenu`, `WebMenuItem` (table header and export menus).
- From `common/tab.js`: `Tab` (API page).
- From SysWeaver.MicroService.Edit (`edit/`): `Edit`, `EditOptions`, `AddTypeName`, `AddTextElement` (API page); `iso_data/languages.js`.
- Icons from SysWeaver.HttpServer.IconModule (`icons/*.svg`, extension icons).

Consumers:

- `table.js` + `table.css` are included directly by SysWeaver.MicroService.ServerManager: `ServerManager/keys.html` (`Table.addTable("../ServerManager/KeysTable", <kf-table>)`) and `ServerManager/services.html` (`Table.addTable("../ServerManager/ServicesTable", <ss-table>)`).
- `explore/table.html?q=...` is the generic table viewer target of: the app shell menu (`application.js`, menu item type 1 - `[WebMenuTable]` APIs - embeds `explore/table.html?q=../<api>`), `ServerManager/server.js`, C# `[TableDataUrl]` formats (`ApiHttpServerModule`, `ExploreHttpServerModule` data refs, MySqlAudit `DbAuditApiCall` / `DbAuditApiClient`), `GetTableDataRefUrl`, `UserDataTableDataExporter` / `ExploreHttpServerService.HandleLink` (parses `explore/table.html?q=` links server side) and `AiServiceBase_DefaultTools` (MessageTable links).
- `explore/api.html?q=...` is linked from the explore listing, `ApiInfoBase` (`[TableDataUrl(null, "*../explore/api.html?q={0}")]`) and MySqlAudit `DbAuditApiCall`.
- `explore`, `explore/icons.html` and `explore/logo.html` are Debug menu entries (`[WebMenuEmbedded]` on `ExploreHttpServerModule`).

### Structural observations

- Module style: classic global scripts. Everything (`Table`, `TableData*`, `insertTH`, `Build`, `BuildTable`, `IntToString`, `GetSettings`, ...) is global; `explore.js` uses generic names (`Build`, `BuildTable`, `GetDate`, `DoSort`) that only work because the generated page loads nothing else besides `common.js`.
- `table.js` is both a library and a page: it holds the reusable `Table` component and `tableMain()`. It reads `document.currentScript.src` when the class is defined (`Table.CS`), so it must be loaded through a classic `<script>` tag. It needs `app/application.js` at runtime (header menu); the export path additionally includes it dynamically.
- Duplicated contracts with C#: the `TableDataProps` / `TableDataFilterOps` numbers and the `TableDataFilter` / `TableDataState` shapes are hand-maintained mirrors of `SysWeaver.Common/Data/*` (`TableDataColumnProps`, `TableDataFilterOps`, `TableDataFilter`, `TableDataState`). The explore list uses two parallel integer schemes (column ids and sort keys = column id + 1) that are implicit.
- `Table` is an all-static class: the per-table state lives in closures inside `addTable` and in DOM expandos, not in an instance. Header/filter functions take long argument lists (`cols, table, state, onChangeFn, saveFn`). There is no teardown API (the resize listener and refresh timer live as long as the page).
- Implicit path contracts: table-relative URLs (`../Api/...`, `../chart/...`, `TableDataUrl` formats) assume the host page is one folder below the root (true for `explore/` and `ServerManager/`). `api.js` derives the site URL by stripping two segments from its own location. `explore.js` builds API links as `IconBase + "../explore/api.html"` (assumes the icon folder is a sibling of `explore/`).
- `table.css` doubles as the stylesheet for `ValueFormat` cell rendering from `common.js`, so pages that render `ValueFormat` tables include `explore/table.css`.
- Repeated patterns: the `AddLoading` / `try` / `Fail` / `finally` skeleton in every entry point; near-identical `icons.css` and `logo.css`; the same head boilerplate (metas, favicons, manifest, theme.css) in every HTML file. The generated explore page uses its own template (`srcData/index.html`) with another placeholder syntax and no app shell.
- Cross-project runtime dependencies: the API page needs SysWeaver.MicroService.Edit (both `GetApiInfo` and the `edit/` scripts); table export needs SysWeaver.MicroService.HttpServer; charts need SysWeaver.MicroService.ChartJs (probed via `TableChartAvailable`; exporters are not probed).
- Values computed but not used further: the `keys` list in `tableUpdateHeader` (columns flagged `TableDataProps.IsKey`), the `rowsPerPage` argument, the member path in the api `EditChange` handler, and the `AbortHandler` instance in `addTable`.

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function; globals from `common/common.js` (`_T`, `_TF`, `ValueFormat`, `ColorIcon`, `Button`, `getRequest`, `sendRequest`, ...) and `app/application.js` (`PopUpMenu`, `WebMenu`, `WebMenuItem`, `PopUpWorking`) are referenced but not documented here. `_T` / `_TF` calls are translation lookups and are not listed as symbols.

### `explore.js`

Script of the generated end point listing (`<folder>/explore`). Loaded as `$(JSFILE)` by the template [`srcData/index.html`](../../srcData/index.html) after `$(COMMONJSFILE)` (`common/common.js`); the template's inline `exploreMain()` calls `Build($(DATA))` with the inlined `ExploreData` JSON. Defines only global functions, no classes and no top-level state.

#### Globals

| Name | Kind | Description |
|---|---|---|
| (none) | - | Only function declarations; nothing runs at load time. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `IntToString` | `IntToString(value)` -> string | Converts `value` to a string and inserts a space every three digits from the right (thousands separator). |
| `GetDate` | `GetDate(dd)` -> Date | Returns `new Date(dd)`; used for sorting and the time column. |
| `SetByteSize` | `SetByteSize(el, size)` -> bool | Writes a size into cell `el`. A string `size` (e.g. `<DIR>`, `[API]`) is written as is and returns true; `null` writes `-` with an "unknown size" tooltip and returns true; a number is written via `IntToString`, and when >= 512 bytes the tooltip lists rounded kb / Mb / Gb values; returns false. A true result makes the caller add `Dimmed`. |
| `SetDateString` | `SetDateString(el, dd)` | Delegates to `ValueFormat.updateDateTime(el, dd)`. |
| `SetCacheDuration` | `SetCacheDuration(el, dd)` | Writes a cache duration (seconds) into `el`: `<= 0` -> `-` with class `Dimmed` and "Not cached" tooltip; `> 1500000000` -> `∞` ("until the server is restarted"); otherwise `"{n} s"` with a "Cached for n seconds" tooltip. |
| `GetSettings` | `GetSettings()` -> object | Reads `localStorage["ExploreSettings"]` (JSON; invalid or missing -> `{}`) and defaults `Order` to `[1, 2]` and `Cols` (when missing or not 10 long) to `[0,1,2,5,3,6,7,4,8,9]`. |
| `UpdateSettings` | `UpdateSettings(s)` | Writes `s` as JSON to `localStorage["ExploreSettings"]`. |
| `GetFileExtension` | `GetFileExtension(n, t)` -> string | Lower-case extension of name `n` (text after the last `.`; `""` if none). When a type `t` is given and it is not 2 (File) or 3 (Api), returns `""`. |
| `InsertTableCell` | `InsertTableCell(row)` -> HTMLTableCellElement | Inserts a cell into `row` and replaces it with a new `<th>`, which is returned. |
| `DoSort` | `DoSort(index, set, data)` | Removes `index` and `-index` from `set.Order`, puts `index` first (negative = descending), then calls `BuildTable(data, set)` and `UpdateSettings(set)`. |
| `AddSortHeader` | `AddSortHeader(data, set, el, index, name, title, extra)` | Fills header cell `el` with a `div` holding a sort icon `img` (`data.IconBase + "m_sort_desc.svg"` or `"m_sort_asc.svg"`, class `Dimmed` unless this column is the primary sort) and, when `title` is given, a `span` with the title. `index` is the column id (internally +1 to form the sort key); `name` is used in tooltips ("Click to sort by {name} ..." and current priority); `extra` is prefixed to the tooltip. Clicking icon or span calls `DoSort` with the flipped direction when the column is the current ascending sort (or not current but descending). |
| `BuildNavigator` | `BuildNavigator(data, set)` | Appends `div.ExploreNavigator.ThemeBaseFg` to `document.body` with one `ValueFormat.createLink` (class `Path`) per segment of `data.LocalUrl` (first labeled "ROOT"). Parent segments link to `"../".repeat(depth) + <FolderSuffix without leading "/"> + <query>` in `_self`; the last segment links to `"./" + <query>` in `_blank`. Tooltips show the absolute folder URL derived from `data.BaseUrl`. `set` is unused. |
| `BuildTable` | `BuildTable(data, set)` | Sorts `data.Items` in place by the keys in `set.Order` (1 type, 2 name (case-insensitive), 3 size, 4 extension, 5 auth, 6 last modified, 7 client cache, 8 request cache, 9 comp preference, 10 pre-compressed; negative = descending; empty auth/comp/precompressed values sort first), builds `table.Explore` with a header row (`ThemeAccBg ThemeBaseFg`, `th` cells with classes `ExploreIconH` ... `ExplorePreCompressedH` via `AddSortHeader`) and one row per item for the column ids in `set.Cols`. Per-type link/icon/size rules: Folder -> `FolderIcon`/`VirtualFolderIcon`, link `name + FolderSuffix + query`, `<DIR>`; File -> `ExtIconBase + ext + ".svg"`, view-mode link, byte size; Api -> `ApiIcon`, link `IconBase + "../explore/api.html?q=" + LocalUrl + name`, `[API]`; Unknown -> `[???]`; FileUpload -> `[UP]`. New windows (`_blank`) are only used when embedded (`window.location !== window.parent.location`). Auth cells get `g_protected.svg` plus one span per token, or a dimmed `g_warning.svg` for public items; compression cells get one span per `method:quality`. A local `accent` flag alternates `ThemeMain` / `ThemeAccFg` / `ThemeAccBg`-`ThemeMainBg` per column. Replaces the first `<table>` in `body`, or appends. |
| `Build` | `Build(data)` | Entry point: shows `AddLoading()`, loads settings with `GetSettings()`, clears `document.body`, calls `BuildNavigator` and `BuildTable`, then removes the loader. |

#### Inner functions of `AddSortHeader`

| Function | Signature | Description |
|---|---|---|
| `click` | `click(ev)` | Click handler on the sort icon and title span; calls `DoSort(flip ? -index : index, set, data)`. |

### `api.js`

Script of `api.html` (API inspector and tester). Loaded with `defer` after `common.js`, `tab.js`, `application.js`, `edit/Edit.js` and `iso_data/languages.js`; `<body onload="apiMain()">`. Uses `AddTypeName` / `AddTextElement` from `edit/type.js` and `Edit` / `EditOptions` from `edit/Edit.js`.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `didAdd` | implicit global | Assigned `true` (without declaration) in the compression-tag loop of `apiMain`; creates a global property, never read. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `apiMain` | async `apiMain()` -> Promise&lt;void&gt; | Reads URL param `q` (API url; fails with "No query parameter specified!" if missing), sets `document.title = "API: " + q`, and POSTs `q` to `../Api/debug/GetApiInfo`. Builds a `SysWeaver-Page.PadBottom` with: a header `SysWeaver-ApiSection` (`SysWeaver-ApiTag` GET/POST, `SysWeaver-ApiUrl` with copy-on-click full URL computed by stripping the last two path segments of `location.href`, a lock `ColorIcon("IconSecured")` and one tag per auth token when `Auth != null`); an optional `SysWeaver-ApiDesc` section; an optional cache/compression section (`CLIENT {n}s`, `SERVER {n}s`, one `SysWeaver-ApiTag.right` per `CompPreference` entry); a `.ApiSignature` section (return type via `AddTypeName`, a google search link for raw `Mime` APIs, or `void`; method name; argument type + `ArgName`). When the API has an argument it adds `Tab("Api.Args")` with a "Parameters" `Edit` form (initial object from `Edit.CreateDefault`, overlaid with `localStorage["SysWeaver.Api." + q]`, then `Edit.CleanUp`) and a "JSON" tab (rendered on select with `ValueFormat.jsonToHtml`, click copies). An `EditChange` listener calls `RemoveResults` (it also walks `detail.Next` to build a member path that is not used). Finally a `SysWeaver-ApiSendButton` with a `Button` ("Send API call", `IconSendIt`) that saves the argument (without `$type`) to localStorage, calls `sendRequest(fullUri, obj, true, null, isRaw)` or `getRequest(fullUri, true, isRaw)` and shows `Tab("Api.Results")` with RESULT (read-only `Edit`) + JSON tabs, PREVIEW (`img` / `iframe` of a blob URL, click opens in new tab), VOID or ERROR (class `Error`; `NoResult` class for void). Errors -> `Fail(e, true)`; loader removed in `finally`. |

#### Inner functions of `apiMain`

| Function | Signature | Description |
|---|---|---|
| `RemoveResults` | `RemoveResults()` | If a results `Tab` exists, removes every element after the send button and the results tab element, and clears `results`. |

### `table.js`

Reusable data table component plus the page script of `table.html`. Loaded as a classic `<script>` (it reads `document.currentScript.src` at class definition) by `table.html` (`<body onload="tableMain()">`) and directly by other pages (e.g. ServerManager `keys.html`, `services.html`) that call `Table.addTable`. Requires `common/common.js`; the header menu and export need `app/application.js` (`PopUpMenu`, `WebMenu`, `WebMenuItem`, `PopUpWorking`), which the export path includes on demand. Defines the globals `insertTH`, `TableDataProps`, `TableDataFilterOps`, `TableDataFilter`, `TableDataState`, `Table` and `tableMain`.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `TableDataProps`, `TableDataFilterOps`, `TableDataFilter`, `TableDataState`, `Table` | class | See the class sections below. Static field initializers run at load (translated `Names` / `Descriptions`, `Table.CS`). |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `insertTH` | `insertTH(trow)` -> HTMLTableCellElement | Appends a cell to row `trow`, replaces it with `<th></th>` via `outerHTML` and returns the new last cell. |
| `tableMain` | async `tableMain()` -> Promise&lt;void&gt; | Entry point of `table.html`. Reads URL params `q` (table API url; required, otherwise `Fail`), `n` (title; sets `document.title`), `p` (string `Param`), `jp` (JavaScript source run as an async function body; its result replaces `p`), `r`, `s`, `f`, then awaits `Table.addTable(q, null, r, p, removeLoader, n, s, onFirstData, f)` where `onFirstData` sets `document.title` to `data.Title` when no `n` was given. The loader is removed in `finally` (and by `onFirstLoad`). |

#### class `TableDataProps`

Bit flags of a column's `Props`, mirroring C# `TableDataColumnProps`. Never instantiated; static fields only.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `CanSort` | static field | `1` | Column can be sorted. |
| `SortedDesc` | static field | `2` | Default sort direction is descending. |
| `Hide` | static field | `4` | Column is hidden (no header/filter/data cell). |
| `Filter` | static field | `8` | Equality / any-of filters allowed. |
| `TextFilter` | static field | `16` | Text filters (contains / starts / ends) and case sensitivity allowed. |
| `OrderFilter` | static field | `32` | Ordering filters (less / greater / range) allowed. |
| `CanChart` | static field | `64` | Column can be shown as a chart value. |
| `IsKey` | static field | `128` | Column is a key. |
| `IsPrimaryKey1` ... `IsPrimaryKey7` | static field | `256`, `512`, `768`, `1024`, `1280`, `1536`, `1792` | Primary key position 1..7 encoded in bits 256/512/1024 (`(Props & IsPrimaryKey7) >> 8` = position). Members: `IsPrimaryKey1`, `IsPrimaryKey2`, `IsPrimaryKey3`, `IsPrimaryKey4`, `IsPrimaryKey5`, `IsPrimaryKey6`, `IsPrimaryKey7`. |
| `AnyFilters` | static field | `Filter \| TextFilter \| OrderFilter` | Mask: any filter enabled. |
| `AnyKey` | static field | `IsPrimaryKey7 \| IsKey` | Mask: any key flag. |

#### class `TableDataFilterOps`

Filter operation ids mirroring C# `TableDataFilterOps`, plus per-op UI metadata. Static only.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Equals`, `NotEqual`, `LessThan`, `GreaterThan`, `LessEqual`, `GreaterEqual`, `Contains`, `StartsWith`, `EndsWith`, `AnyOf`, `NoneOf`, `InRange`, `OutsideRange` | static field | `0` ... `12` | Operation ids in that order. |
| `Count` | static field | `13` | Number of operations. |
| `DefaultOrders` | static field | `[Contains, Equals, GreaterThan]` | Preference order used by `addFilterRow` to choose the default op when the stored op is unset or disabled for a column. |
| `Names` | static field | string[13] | Translated short names (option text and value input placeholder). |
| `Props` | static field | number[13] | The `TableDataProps` flag that enables each op (Filter for Equals/NotEqual/AnyOf/NoneOf, OrderFilter for the comparisons and ranges, TextFilter for Contains/StartsWith/EndsWith). |
| `Descriptions` | static field | string[13] | Translated option tooltips. |

#### class `TableDataFilter`

One filter for one visible column; serialized as C# `TableDataFilter` in `TableDataRequest.Filters`. Created by `addFilterRow` (`new TableDataFilter(col.Name)`) or restored from stored state.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `new TableDataFilter(colName)` | Sets `ColName`. Note: the `ColName = ""` field initializer is declared after the constructor body but class fields initialize before the constructor body runs, so `ColName` ends as `colName`. |
| `Invert`, `CaseSensitive` | field | bool, default `false` | Keep non-matching rows; case-sensitive text match. |
| `Op` | field | number, default `-1` | `TableDataFilterOps` id (-1 = not chosen yet). |
| `Value` | field | string, default `""` | Filter value; empty filters are not sent. |
| `ColName`, `ColumnIndex` | field | string / number | Column name and index into `data.Cols` (including hidden columns). |

#### class `TableDataState`

Persisted per-table UI state (mirrors C# `TableDataState`), stored as JSON in localStorage. Created by `Table.tableDefaultState()`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `FilterRows` | field | number, default `1` | Number of filter sets (1..3). |
| `Filters` | field | `TableDataFilter[][]`, default `[]` | One array per filter set, one filter per visible column. |
| `Expanded` | field | number, default `1` | Filter UI mode: 0 hidden, 1 simple (first value row only), 2 advanced (all rows). |
| `RequestParams` | field | object | `TableDataRequest` built by `Table.tableDefaultRequest()` (evaluated at construction). Later also carries `Param`, `Filters`, `SearchText`. |
| `AutoRowCount` | field | bool, default `true` | Adjust `MaxRowCount` to the available height. |
| `TimeMode` | field | `null` or `"utc"` | Default format for unformatted `System.DateTime` columns. |

#### class `Table`

All-static component class; a table is created with `Table.addTable(...)`, whose per-table state lives in closures and DOM expandos on the `<table>` (`Info`, `Cols`, `HomeIcon`, `PrevIcon`, `NextIcon`, `ExportIcon`, `ClearSearch`). There is no teardown.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `tableUpdateFilterIcon` | static method | `tableUpdateFilterIcon(table, state)` | Applies `state.Expanded` to the header filter icon (`cell.Icon` of `rows[0].cells[0]`) and the filter rows 1..`FilterRows*3`: 0 -> `IconExpand`, all filter rows get `HideRow`; 1 -> `IconMore`, only row 2 (value row of the first set) visible; 2 -> `IconCollapse`, all visible. Skips when `cell.Expanded` already equals the mode. |
| `haveSort` | static method | `haveSort(requestParams)` -> bool | True when `requestParams.Order` is a non-empty array. |
| `isCurrentSort` | static method | `isCurrentSort(requestParams, colName)` -> bool | True when `Order[0]` is `colName` or `"-" + colName`. |
| `isReverseSort` | static method | `isReverseSort(requestParams)` -> bool | True when `Order[0]` starts with `-` (descending). |
| `removeSort` | static method | `removeSort(requestParams, colName)` | Removes `colName` or `-colName` from `Order`; sets `Order = null` when it becomes (or is) empty. |
| `updateSort` | static method | `updateSort(requestParams, colName, defaultDesc)` | If the column is already first, toggles its direction; otherwise inserts it first (descending when `defaultDesc`), removes other occurrences and truncates `Order` to 3 entries. Creates `Order` when missing/empty. |
| `tableUpdateHeader` | static method | `tableUpdateHeader(cols, table, state, onChangeFn, saveFn)` | For every non-hidden column header cell (from index 1): sets classes `CurrentSort` / `Sort` / `NoSort`, the sort icon (`IconAscending` / `IconDescending`, class `IconButton`; hidden for non-sortable columns), the title text, tooltips, the icon click handler (`onClickFn`, re-sorts via `updateSort`, resets `Row`, re-runs itself and `onChangeFn`) and cell `onclick` / `oncontextmenu` opening the column menu (`contextMenu`). Collects primary key column names (ordered by `IsPrimaryKeyN`) for the chart link; also builds a `keys` list of `TableDataProps.IsKey` columns (unused). |
| `addFilterRow` | static method | `addFilterRow(table, state, rowIndex, columnCount, onChangeFn, saveFn, cols)` | Inserts one filter set (3 `tr.FilterRow` rows, `HideRow` by `Expanded`) at `rowIndex`, creating/updating `state.Filters[set]` entries for the visible columns. Row 1: action icon (`IconReset` on the first set: resets ops/flags/values, deletes extra sets, clears the search; `IconClose` on other sets: removes the set) and one op `<select>` per column (options disabled unless `Props` allow them; whole select disabled if none; default from `DefaultOrders`). Row 2: `IconClear` (clears all values of the set) and one text `<input>` per column (applies on keyup/change; disabled without `AnyFilters`). Row 3: `IconPlus` (adds a set after this one, max 3) and two `table-check` toggles per column, `!` (Invert, needs `AnyFilters`) and `A ≠ a` (CaseSensitive, needs `TextFilter`), class `Checked` / `Disabled`, each with an expando `Update`. Value changes reset `Row` and call `onChangeFn`; changes without a value only call `saveFn`. |
| `tableRedraw` | static method | `tableRedraw(table, data, state, onChangeFn, saveFn, didChange)` | When `data.Cols` is present: stores it in `table.Cols`, clears the table, builds the header (`insertTH`; first cell an `IconPlus` `ColorIcon` cycling `state.Expanded` 0-1-2 and saving; then a `ColorIcon` + `span` per visible column stored as `cell.Icon` / `cell.Text`), adds `FilterRows` filter sets, then `tableUpdateFilterIcon` and `tableUpdateHeader`. Otherwise reuses `table.Cols`. Then fills/reuses data rows from index `1 + FilterRows*3`: cell 0 = 1-based row number, other cells updated by `ValueFormat.update(cell, type, value, flash, fmt, next, onChangeFn)` only when `value + "|" + fmt` (cached in `cell.Value`) changed or columns were rebuilt. Unformatted `System.DateTime` columns use `state.TimeMode`. `flash` is `"Flash"` for changed cells of the same data row when `didChange` is false. Surplus rows are deleted. |
| `tableDefaultRequest` | static method | `tableDefaultRequest()` -> object | Returns `{ Cc: 0, Row: 0, MaxRowCount: 25, LookAheadCount: 1, Order: null }`. |
| `tableDefaultState` | static method | `tableDefaultState()` -> `TableDataState` | Returns `new TableDataState()`. |
| `getTableExportMenu` | static async method | `getTableExportMenu()` -> Promise&lt;menu item[]&gt; | Returns the cached `Table.MenuExportItems`, or loads `../app/application.js` / `.css` via `includeJs` / `includeCss` relative to `Table.CS` together with `getRequest("../Api/GetTableExporters")`, caches and returns the exporter list. |
| `CS` | static field | string | `document.currentScript.src` at load time; base for the dynamic includes. |
| `MenuExportItems` | static field | array or `null` | Cached exporter menu items. |
| `_CanShowChart` | static field (assigned) | bool | Cache of `canShowChart()`; created on first call. |
| `addTable` | static async method | `addTable(url, toElement, rowsPerPage, tableParams, onFirstLoad, tableName, stateStr, onFirstData, filter)` -> Promise&lt;{UpdateNow}&gt; | Creates a live table for the table API `url` inside `toElement` (default `document.body`). `rowsPerPage` defaults to 25 but is not used further; `tableParams` becomes `RequestParams.Param`; `onFirstLoad()` is called once after the first response or error; `tableName` (default: camel-case-split last URL segment, replaced by `data.Title` on first data) names the table/export; `stateStr` is a JSON state used instead of `localStorage["SysWeaver.Table." + hashString(url)]`; `onFirstData(data)` is called once with the first data; `filter` is JSON (one filter or array) that replaces `state.Filters`. Repairs missing state parts (saving when fixed), creates `table.DataTable` and a `datatable-footer` with Home/Prev/Next/Export `ColorIcon`s, a search `<input>` and a clear icon, registers a `window` resize listener, and starts the update loop (`Update()`, not awaited). The returned Promise resolves after setup to `{ UpdateNow }`, which callers can use to force an immediate refresh (used by ServerManager `keys.js` after an upload). See the inner functions below. |
| `canShowChart` | static async method | `canShowChart()` -> Promise&lt;bool&gt; | Returns the cached `Table._CanShowChart`, or `getRequest("../Api/TableChartAvailable")` (false on error), caching the result. |

#### Inner functions of `Table.tableUpdateHeader`

| Function | Signature | Description |
|---|---|---|
| `onClickFn` | `onClickFn(ev)` | Per-column sort icon handler (two variants: current sort / other sort). Ignores `badClick`, calls `Table.updateSort` (default direction from `SortedDesc`), logs the order, sets `Row = 0`, re-runs `tableUpdateHeader` and calls `onChangeFn()`. |
| `contextMenu` | async `contextMenu(ev)` | Awaits `Table.canShowChart()` and opens a `PopUpMenu` at the column icon with a `WebMenu` named `TableHeader` (also adds class `TableHeader` to the popup): Sort ascending / Sort descending (checked when active; clicking an active one removes it), Clear sort (read-only when unsorted), Copy title / name (if different) / description (if any) / column type, "Show as chart" (`IconChart`; when charts are available and the column has `CanChart`: navigates `_self` to `../chart/chart.html?q=../Api/TableChart?{TableApi, Keys, Values, Options, Title}` with `MaxRowCount` 1000 and without `Cc`/`LookAheadCount`/`Row`), and for unformatted `System.DateTime` columns "Show local times" / "Show UTC times" (sets `state.TimeMode`, saves, refreshes). |
| `Copy` | async `Copy(text)` | Inner of the menu builder: `ValueFormat.copyToClipboardInfo(text)` then closes the menu. |

#### Inner functions of `Table.addFilterRow`

| Function | Signature | Description |
|---|---|---|
| `getFilterIndex` | `getFilterIndex(indexOfRow)` -> number | Filter set index for a table row index: `((indexOfRow - 1) / 3) \| 0`. |
| `getFirstRowIndex` | `getFirstRowIndex(element)` -> number | Walks up to the `TR` of `element` and returns the row index of the first row of its filter set (`((rowIndex - 1) / 3 \| 0) * 3 + 1`). Used because sets can shift when others are removed. |
| `deleteFilters` | `deleteFilters(rIndex)` -> bool | Removes the filter set starting at row `rIndex`: decrements `state.FilterRows`, splices `state.Filters`, deletes the 3 rows. Returns whether the set had a non-empty value (checked up to the first non-empty one). |
| `setInvert` | `setInvert()` | Renders the `!` toggle from `filter.Invert` (text, tooltip, class `Checked`); stored as `invert.Update`. |
| `setCaseSens` | `setCaseSens()` | Renders the `A ≠ a` toggle from `filter.CaseSensitive`; stored as `caseSens.Update`. |

#### Inner functions of `Table.tableRedraw`

| Function | Signature | Description |
|---|---|---|
| `getRowData` | `getRowData(rowData, colIndex, colName)` -> value | Arrow function selected once per redraw: `rowData.Values[colIndex]` for generic rows (first row has a `Values` array, or no rows) or `rowData[colName]` for typed rows. |

#### Inner functions of `Table.addTable`

| Function | Signature | Description |
|---|---|---|
| `save` | `save()` | Writes `state` as JSON to `localStorage[key]` (logs "Saved state"; logs on failure). |
| `onChangeFn` | `onChangeFn()` | Saves, sets `didChange = true` and calls `UpdateNow()`. Passed to header/filter/redraw code and `ValueFormat.update`. |
| `onResizeFn` | `onResizeFn(ev)` | Auto row count: from the average data row height and the space between the table top and the footer (minus 40px, min 128px) computes a new `MaxRowCount` (5..100, rounded down to a multiple of 5); calls `onChangeFn()` when it differs. Called by the `window` resize listener, `saveFn` and after each redraw (via `setTimeout`) when `AutoRowCount` and `Row == 0`. |
| `saveFn` | `saveFn()` | `save()` plus `onResizeFn()` when auto row count applies; used for changes that do not need a refetch. |
| `exportTable` | async `exportTable(mode, id, name, closeFn, text, icon)` | Closes the menu, shows `PopUpWorking("Exporting", ...)`, then mode 0: `sendRequest("../Api/ExportTableData", {Data: latestData (with Cols = table.Cols), ExportAs: id, Options: {Filename: tableName}})`; mode 1/2: `sendRequest("../Api/ExportTableApi", {Api: url, Req: copy of requestParams with Cc/Row 0 (mode 2 also clears Filters, SearchIndex, SearchText), ExportAs, Options})`. A returned file with `Mime` + `Data` is downloaded via a hidden `a[download]` with a base64 `data:` URL; with only `Name`, the absolute URL of `../<Name>` is copied and opened; otherwise `Fail`. |
| `add` | `add(name, title, mode)` | Inner of the export menu builder: pushes a sub menu `name` / `title` to the `WebMenu` named `Table` with one child per exporter item (from `getTableExportMenu`) whose `Data` calls `exportTable(mode, item.Id, item.Name, close, title, item.IconClass)`. Called for "Current view" (0), "Filtered" (1), "All" (2). |
| `clearSearch` | `clearSearch()` -> bool | Exposed as `table.ClearSearch`. When the search input has text, clears it, disables the clear icon, sets `RequestParams.SearchText = ""` and returns true; else false. |
| `UpdateNow` | async `UpdateNow()` | Waits while an update is running (`delay(10)` polling), cancels the pending timer and calls `Update()`. |
| `Update` | async `Update()` | One refresh cycle: waits for a running update, sets `requestParams.Cc = cc` and `Filters` (all non-empty filters of all sets, or null), POSTs `sendRequest(url, requestParams, didChange)`. Null data -> `Fail` ("No data found" / "No update found") and retry in 3000 ms. On first data: if no rows and `Row > 0`, resets `Row` to 0, saves and refetches; resolves the table name. Then stores `latestData`, enables Home/Prev (`Row > 0`) and Next (`RowCount > Row + Rows.length`), calls `Table.tableRedraw`, schedules `onResizeFn`, clears `didChange`, stores `cc = data.Cc`, calls `onFirstLoad` / `onFirstData` once, and reschedules itself after `RefreshRate` ms (<= 0 -> 5000, min 100). Exceptions -> `Fail("Failed to fetch data: ...")`, `onFirstLoad` once, retry in 3000 ms. |

Anonymous handlers set in `addTable`: Home (Row 0), Prev (Row - MaxRowCount, min 0), Next (Row + MaxRowCount), Export (opens the export `PopUpMenu`), clear-search icon, and `searchEI.onchange` (trims the text, enables the clear icon, sets `SearchText`, resets `Row` when changed, calls `onChangeFn`).

### `icons.js`

Script of `icons.html` (icon gallery). Loaded with `defer` after `common/common.js`; `<body onload="iconsMain()">`.

#### Globals

| Name | Kind | Description |
|---|---|---|
| (none) | - | Only `iconsMain`. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `iconsMain` | async `iconsMain()` -> Promise&lt;void&gt; | Gets `IconInfo[]` from `../Api/debug/explore/GetIcons` (GET, or POST `{Param: path}` when the `path` URL param is set), and appends a `SysWeaver-IconList` to `body` with one `SysWeaver-IconItem` per icon: a non-draggable `img` loaded via `PendingImages.Start(img, "../" + Url)` and a `SysWeaver-IconName` with `Name`. Clicking copies `Url` (`ValueFormat.copyOnClick`); the tooltip shows `Url`, size in bytes and `Location`. Awaits all images; errors -> `Fail(e, true)`; loader removed in `finally`. |

### `logo.js`

Script of `logo.html` (logo seed designer). Loaded with `defer` after `common/common.js`; `<body onload="logoMain()">`.

#### Globals

| Name | Kind | Description |
|---|---|---|
| (none) | - | Only `logoMain`. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `logoMain` | async `logoMain()` -> Promise&lt;void&gt; | Gets the current seed from `../Api/debug/explore/GetAppSeed` (as string `selected`), appends a `Button.CreateRow()` row with a "Randomize" `Button` (`IconDice`, calls `Create`) and a `SysWeaver-IconList` to `body`, calls `Create()` and awaits all images. Errors -> `Fail(e, true)`; loader removed in `finally`. |

#### Inner functions of `logoMain`

| Function | Signature | Description |
|---|---|---|
| `Create` | `Create()` | Clears the list and adds 10 `SysWeaver-IconItem`s: item 0 uses `selected`, items 1-9 a random 31-bit seed; each has an `img` loaded from `../logo_debug.svg?<seed>` (via `PendingImages`) and a `SysWeaver-IconSeed` caption. `item.onclick` (ignores `badClick`) sets `selected` to the item's seed, copies it with `ValueFormat.copyToClipboardInfo`, and for items other than the first swaps image and caption with the first item. |

## CSS reference

All five style sheets rely on the theme custom properties of `common/theme.css` (`--ThemeMain`, `--ThemeMainRGB`, `--ThemeMain25`, `--ThemeMain50`, `--ThemeAcc1`, `--ThemeAcc1RGB`, `--ThemeAcc150`, `--ThemeAcc2`, `--ThemeAcc2RGB`, `--ThemeAcc225`, `--ThemeBackground`, `--ThemeRad`, `--ThemeRadInp`, `--ThemeRadInp2`, `--ThemeFont`, `--ThemeFontMono`, `--ThemeIconSize`, `--ThemeFadeDuration`). None is templated; mask-icon classes (`mask-image`) are colored by the `ColorIcon` / `IconColor*` rules of `common.css`, and icon URLs resolve against `explore/` (`../icons/...`). Elements `icon-16x16`, `icon-24x24`, `icon-26x26`, `icon-main` are the wrappers created by `ColorIcon` in `common.js`.

### `api.css`

Styles `api.html` (loaded by that page next to `common/tab.css` and the `edit/` CSS). Styles the custom elements created by `api.js`.

#### API page sections

| Selector(s) | Description |
|---|---|
| `SysWeaver-ApiSection` | Block section box (full width, padding, 1.5em bottom margin, `--ThemeBackground`, faint `--ThemeMainRGB` border, `--ThemeRad`, horizontal scroll). |
| `SysWeaver-ApiSection:first-child`, `SysWeaver-ApiSection:first-child icon-24x24` | Header section as a centered flex row; the lock icon (`ColorIcon` 24x24) gets a 1em left margin. |
| `SysWeaver-ApiSection SysWeaver-Text`, `SysWeaver-ApiSection a` | Inline-block text elements (from `AddTextElement` in `edit/type.js`) and links. |
| `SysWeaver-ApiSection icon-24x24` | Empty rule. |
| `SysWeaver-ApiSection.ApiSignature`, `SysWeaver-ApiSection.ApiSignature a`, `SysWeaver-ApiSection.ApiSignature SysWeaver-Text`, `SysWeaver-ApiSection.ApiSignature SysWeaver-Text.Signature`, `SysWeaver-ApiSection.ApiSignature SysWeaver-Text.Control` | Signature section: monospace 80% text; links normal weight; text in `--ThemeAcc1` at 80%; `.Signature` (method / argument names) in `--ThemeAcc2` with a 4px left margin; `.Control` (`(` and `);`) in `--ThemeMain`. Classes passed to `AddTextElement` by `api.js`. |
| `SysWeaver-Page.PadBottom` | Half-viewport (`50svh`) bottom padding on the page container. |
| `SysWeaver-ApiDesc` | Description text in `--ThemeAcc2`, selectable. |
| `SysWeaver-ApiUrl` | API url in `--ThemeAcc1`, selectable. |
| `SysWeaver-ApiTag` | Pill tag (method, auth tokens, cache, compression): `--ThemeMain` background, `--ThemeBackground` text, bold 80%. |
| `SysWeaver-ApiSendButton` | Centered block wrapper of the send `Button`. |

#### Tab content

| Selector(s) | Description |
|---|---|
| `SysWeaver-TabContent` | Tab panels (from `common/tab.js`) get `--ThemeBackground`. |
| `SysWeaver-TabContent.CODE`, `SysWeaver-TabContent.CODE > iframe`, `SysWeaver-TabContent.CODE > img` | Code/preview panels scroll horizontally; preview `iframe` (min 50vh, vertically resizable) and `img` (scale-down, top-centered) fill the width with pointer cursor. |

#### Icons

| Selector(s) | Description |
|---|---|
| `.IconSecured`, `.IconSendIt` | Mask icons `../icons/lock.svg` (auth lock in the header) and `../icons/table_api.svg` (send button). |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| (none defined) | theme.css | See the shared list above. |

#### Animations (@keyframes) and media queries / breakpoints / theme variants

None.

### `explore.css`

Styles the generated end point listing; linked as `$(CSSFILE)` by `srcData/index.html` (with `$(STYLECSSFILE)` theme). Classes are set by `explore.js`.

#### Page and navigator

| Selector(s) | Description |
|---|---|
| `.Dimmed` | 40% opacity (dimmed sort icons, sizes, public-resource icon, uncached durations). |
| `a`, `a:visited` | Links inherit the text color. |
| `body` | No margin. |
| `.ExploreNavigator` | Centered table-display bar, 125% bold (`BuildNavigator`). |
| `SysWeaver-Link.Path`, `SysWeaver-Link.Path::after` | Path segment pills (links from `ValueFormat.createLink` with class `Path`): `--ThemeAcc2` background, `--ThemeBackground` text, `--ThemeRad`; a faded `/` after each. |

#### Listing table

| Selector(s) | Description |
|---|---|
| `.Explore`, `.Explore img`, `.Explore td`, `.Explore td:first-child`, `.Explore th`, `.Explore th:first-child`, `.Explore th:last-child`, `.Explore th div`, `.Explore th img`, `.Explore th span`, `.Explore tr:nth-child(2n)` | The `table.Explore` built by `BuildTable`: collapsed border, centered; images sized `--s` (`--ThemeIconSize` x 32px); cells no-wrap with left separators; header cells uppercase, rounded outer top corners; header sort `div` inline-flex with 16px clickable icon and clickable title span; even rows darkened. |
| `th.ExploreIconH`, `th.ExploreNameH`, `th.ExploreSizeH`, `th.ExploreExtH`, `th.ExploreAuthH`, `th.ExploreTimeH`, `th.ExploreClientCacheH`, `th.ExploreRequestCacheH`, `th.ExploreCompPreferenceH`, `th.ExplorePreCompressedH` | Header cells per column (4px side padding; icon centered, size right-aligned). |
| `td.ExploreSize`, `td.ExploreName`, `td.ExploreExt`, `td.ExploreTime`, `td.ExploreClientCache`, `td.ExploreRequestCache` | Data cells: 4px side padding; size and cache durations right-aligned; name max 512px (clipped); extension max 64px. (`td.ExploreIcon` and `td.ExplorePreCompressed` are set by JS but have no rule; a commented-out `td.ExploreIcon SysWeaver-Link` rule exists.) |
| `td.ExploreAuth`, `td.ExploreAuth div`, `td.ExploreAuth img`, `td.ExploreAuth span` | Auth cell: flex row of the protection icon and small uppercase bold token pills. |
| `td.ExploreCompPreference`, `td.ExploreCompPreference div`, `td.ExploreCompPreference span` | Compression cell: flex row of small uppercase bold pills (`--ThemeRadInp2`). |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--s` | `calc(var(--ThemeIconSize) * 32px)` on `.Explore img` | Icon width/height. |

#### Animations (@keyframes) and media queries / breakpoints / theme variants

None.

### `icons.css`

Styles `icons.html` (gallery built by `icons.js`).

| Selector(s) | Description |
|---|---|
| `body` | `user-select: none`. |
| `SysWeaver-CenterBlock` | 1em bottom padding (element from common code; not created by `icons.js`). |
| `SysWeaver-IconList` | Centered inline-block list, bold 125%. |
| `SysWeaver-IconItem`, `SysWeaver-IconItem:hover` | Square tile sized from the viewport (`--s`, height `--h`), `--ThemeMain50` border, `--ThemeRad`, pointer, accent glow on hover. |
| `SysWeaver-IconItem img` | Icon image `--s` x `--s` on a checkerboard background built from `--D`, `--B`, `--C`, `--R`. |
| `SysWeaver-IconItem SysWeaver-IconName` | Centered 1.25em caption below the image. |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--s` | `calc(min(100svw, 100svh - 64px - 1em) / 2 - 3em)` on `SysWeaver-IconItem` | Tile and image size (two per row/column of the viewport). |
| `--h` | `calc(var(--s) + 1.5em)` | Tile height (image + caption). |
| `--D`, `--B` | `#666`, `#aaa` | Dark / light checkerboard colors. |
| `--C`, `--R` | `10px`, `20px` | Checker offset and tile size. |

#### Animations (@keyframes) and media queries / breakpoints / theme variants

None (sizes adapt through `svw` / `svh` units).

### `logo.css`

Styles `logo.html` (seed tiles built by `logo.js`). Near copy of `icons.css`.

| Selector(s) | Description |
|---|---|
| `body`, `SysWeaver-CenterBlock`, `SysWeaver-IconList` | Same as `icons.css`. |
| `SysWeaver-IconItem:first-child` | 2px `--ThemeAcc2` border marking the selected seed (first tile). Declared before the generic `SysWeaver-IconItem` rule but wins by specificity. |
| `SysWeaver-IconItem`, `SysWeaver-IconItem:hover`, `SysWeaver-IconItem img` | Same tiles and checkerboard image as `icons.css`. |
| `SysWeaver-IconItem SysWeaver-IconSeed` | Centered 1.25em seed caption. |
| `.IconDice` | Mask icon `../icons/reload.svg` for the Randomize button. |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--s`, `--h`, `--D`, `--B`, `--C`, `--R` | Same values as `icons.css` | Tile size, tile height, checkerboard colors, offset and size. |

#### Animations (@keyframes) and media queries / breakpoints / theme variants

None.

### `table.css`

Styles the data table of `table.js` and the cell output of `ValueFormat.update` / `ValueFormat.updateFormat` in `common/common.js` (which adds `ValueFormat` + format name classes and `Right`, `Left`, `Null`, `False`, `Text`, `Toggle`, `Monospaced`, `Capped`, `KeyClick`, `MouseClick`). Loaded by `table.html` and by other pages that embed `Table.addTable` or render `ValueFormat` tables (e.g. ServerManager pages). The `a`, `body` and `html` rules assume the table fills the page.

#### Page

| Selector(s) | Description |
|---|---|
| `a`, `a:visited` | Links without decoration/padding, inheriting color. |
| `body` | Centered text, height `calc(100% - 200px) !important`. |
| `html` | No vertical scroll, always-on horizontal scroll, stable scrollbar gutter, themed scrollbar colors. |
| `html.SwSelfWide`, `html.SwSelfWide .DataTable` | Layout variant (classes set by the app shell code in `common.js`): rounded, clipped table with `margin-right: var(--Margin)`. Uses CSS nesting. |
| `html.SwSelfNarrow`, `html.SwSelfNarrow .DataTable` | Narrow variant: `html` gets `margin-left`, `margin-right`, `margin-bottom` 0, `min-width` 100%, `min-height: calc(100% - var(--Margin))`; table `min-width` 100%. Uses CSS nesting. |

#### Data table

| Selector(s) | Description |
|---|---|
| `.DataTable` | The `table.DataTable` from `addTable`: inline-block, collapsed, shadowed, `--ThemeBackground`; defines `--HeaderCol`, `--HeaderColHover`, `--RowPadding`. |
| `.DataTable tr`, `.DataTable tr:hover`, `.DataTable tr:first-child`, `.DataTable tr:first-child:hover` | Row hover tint (`--ThemeAcc2RGB`); header row `--HeaderCol` / `--HeaderColHover`. |
| `.DataTable tr:first-child td,th`, `.DataTable tr:last-child td, th`, `.DataTable td:first-child`, `.DataTable th:first-child`, `.DataTable td:last-child`, `.DataTable th:last-child` | Remove the outer borders (the bare `th` parts of the two selector lists apply to every `th` on the page); first data column centered. |
| `.DataTable th`, `.DataTable th:first-child`, `.DataTable th:first-child > *`, `.DataTable th div`, `.DataTable th span` | Header cells: no-wrap, `--ThemeAcc150` border; first (filter toggle) cell centered with pointer; header `div` inline-flex; titles bold uppercase 75% in `--ThemeAcc1`. |
| `.DataTable th.Sort`, `.DataTable th.Sort:hover`, `.DataTable th.Sort icon-main`, `.DataTable th.NoSort icon-16x16`, `.DataTable th.CurrentSort`, `.DataTable th.CurrentSort icon-main` | Sort state classes from `tableUpdateHeader`: sortable non-current headers at 50% opacity (90% on hover); icon spacing; zero-width icon for `NoSort`. |
| `.DataTable td`, `.DataTable td.KeyClick:focus`, `.DataTable td span`, `.DataTable td a`, `.DataTable td a:hover`, `.DataTable td:nth-child(1)` | Data cells: ellipsis, `--ThemeAcc225` borders, `--ThemeAcc1` text, box-shadow transition; inset focus ring for keyboard-clickable cells (`KeyClick` from `keyboardClick`); spans as `--ThemeAcc1` pills; links in `--ThemeAcc2` with hover fill; row-number column small and faded. |
| `.DataTable td.Right`, `.DataTable td.Monospaced`, `.DataTable td.False`, `.DataTable td.Null`, `.DataTable td.Text`, `.DataTable td.Toggle` | `ValueFormat` modifiers: right-aligned numbers; monospace; false booleans in `--ThemeAcc2`; null at 50% opacity; selectable `--ThemeMain` text; centered toggles without padding. |
| `.DataTable td.Flash` | Runs `DataTableFlash` for 1s (class passed as `flash` by `tableRedraw` to `ValueFormat.update`). |

#### Filter rows

| Selector(s) | Description |
|---|---|
| `.DataTable tr.HideRow` | Hidden filter rows (`tableUpdateFilterIcon`, `addFilterRow`). |
| `.DataTable tr.FilterRow`, `.DataTable tr.FilterRow:hover`, `.DataTable tr.FilterRow:nth-child(2n):hover` | Filter rows tinted `--HeaderCol` / `--HeaderColHover`. |
| `.DataTable tr.FilterRow td`, `.DataTable tr.FilterRow td:first-child`, `.DataTable tr.FilterRow td:first-child > *` | Bordered no-wrap cells; first (action icon) cell centered with pointer. |
| `.DataTable tr.FilterRow td select`, `... select:focus`, `.DataTable tr.FilterRow td option`, `... option:hover` | Op selects filling the cell, themed, `--ThemeAcc2` focus outline; option hover in `--ThemeAcc1`. |
| `.DataTable tr.FilterRow td input`, `... input::placeholder`, `... input:focus`, `... input:read-only` | Value inputs filling the cell; italic faded placeholder (op name); focus outline; read-only at 70%. |
| `.DataTable tr.FilterRow td table-check`, `table-check.Disabled`, `table-check:hover`, `table-check.Disabled:hover`, `table-check:active`, `table-check.Disabled:active`, `table-check:focus`, `table-check.Disabled:focus`, `table-check.Checked`, `table-check:nth-child(2)` | The `!` / `A ≠ a` toggles from `addFilterRow`: small pills with hover lift, press, focus outline; `Disabled` stays at 70% without effects; `Checked` inverts to `--ThemeMain` background; the second toggle gets a 4px left margin. |

#### ValueFormat cells

| Selector(s) | Description |
|---|---|
| `.DataTable td.ValueFormatImg`, `.DataTable td.ValueFormatImg.Left`, `.DataTable td.ValueFormatImg.Right`, `.DataTable td.ValueFormatImg img.ImgLink` | Image cells: centered without vertical padding, optional left/right alignment, pointer on linked images (`ImgLink`). |
| `.DataTable td.ValueFormatDefault.MouseClick`, `.DataTable td.ValueFormatDefault.MouseClick:hover` | Clickable default cells (`MouseClick`) with hover color. |
| `.DataTable td.ValueFormatWebColor`, `.DataTable td.ValueFormatWebColor.False` | Web color swatches: bold monospace centered black text, white when `False` (dark background). |
| `.DataTable td.ValueFormatJson`, `.DataTable td.ValueFormatText`, `.DataTable td.ValueFormatMD` (each with `:hover` and `.Capped:after`) | JSON, text and markdown cells: selectable `--ThemeMain` text, pointer, hover `--ThemeAcc1`; `Capped` (truncated) appends ` ...` in `--ThemeAcc2`. |
| `.DataTable td.ValueFormatDt`, `.DataTable td.ValueFormatDt:after`, `.DataTable td.ValueFormatDt.ValueFormatDtutc:after`, `.DataTable td.ValueFormatDt.ValueFormatDtlocal:after` | Date/time cells with a 24px left indent and a mask glyph (`--s` = 20px x `--ThemeIconSize`): hidden by default, `../icons/table_country.svg` at 50% for UTC (`ValueFormatDtutc`), `../icons/home.svg` at 50% for local (`ValueFormatDtlocal`). |
| `SysWeaver-toggle`, `SysWeaver-toggle:focus`, `SysWeaver-toggle:hover`, `SysWeaver-toggle:active`, `SysWeaver-toggle.Null` | Toggle pills created by `ValueFormat` (`TempToggle`): `--ThemeAcc2`, 50% opacity, lift on hover/focus, 20% and default cursor when `Null`. |
| `SysWeaver-action`, `SysWeaver-action img`, `SysWeaver-action:focus`, `SysWeaver-action:hover`, `SysWeaver-action:active`, `SysWeaver-action.Null`, `SysWeaver-action.Disabled`, `SysWeaver-action > *`, `SysWeaver-actionText` | Action buttons created by `ValueFormat` (`TempAction` / `TempSpan`): 22px high inline-flex `--ThemeAcc2` pills with max 22px image, lift effects, 20% when `Null` / `Disabled`; text span with 6px left margin. |

#### Footer and menus

| Selector(s) | Description |
|---|---|
| `datatable-footer`, `datatable-footer icon-26x26`, `datatable-footer icon-26x26:last-child`, `datatable-footer *.Disabled`, `datatable-footer input`, `datatable-footer input::placeholder` | Fixed 36px bottom bar from `addTable` (gradient, shadow, centered flex): 26px nav icons with 1em margins (last one, the clear-search icon, 0.25em left), disabled items at 50%; the search input is 20ch wide with italic `--ThemeMain25` placeholder. |
| `SysWeaver-PopupMenu.Table SysWeaver-PopupMenuBack` | The export popup (class `Table` added by `addTable`) anchors its back element at `bottom: 0`. |

#### Icons

| Selector(s) | Description |
|---|---|
| `.IconAscending`, `.IconDescending` | Mask icons `../icons/m_sort_asc.svg`, `../icons/m_sort_desc.svg` (header sort icons and menu items). |
| `.IconPlus`, `.IconMinus`, `.IconClose`, `.IconMore`, `.IconExpand`, `.IconCollapse`, `.IconClear`, `.IconReset` | Mask icons `plus.svg`, `minus.svg`, `close.svg` (declared twice), `more.svg`, `expand.svg`, `collapse.svg`, `clear.svg`, `reset.svg` — filter toggle/rows and search clear (`IconMinus` is not used by `table.js`). |
| `.IconClearSort` | Mask `../icons/m_menu.svg` ("Clear sort" menu item). |
| `.IconHome`, `.IconPrev`, `.IconNext`, `.IconExport`, `.IconReload` | Mask icons `home.svg`, `prev.svg`, `next.svg`, `newtab.svg` (export), `reload.svg` (used elsewhere, e.g. `PerfMonitor.cs` / `common.js`). |
| `.IconCopy`, `.IconClock` | Mask icons `copy.svg` (copy menu items), `clock.svg` (local/UTC menu item). |
| `.IconFileCsv`, `.IconFileHtml`, `.IconFileMD`, `.IconFileJson`, `.IconFileXlsx`, `.IconFilePdf` | Full-color background images `../icons/ext/{csv,html,md,json,xlsx,pdf}.svg` (transparent background) for exporter menu items (`IconClass` from the C# exporters: Csv/Html/MarkDown table exporters, JSON, Excel/PDF). |
| `.IconShareLink0`, `.IconShareLink1`, `.IconShareLink2` | Mask icons `disc.svg`, `user_check.svg`, `share.svg` for share-link exporters (`"IconShareLink" + scopeIndex` in `UserDataTableDataExporter` / `UserStorageChartExporter`). |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--HeaderCol` | `rgba(var(--ThemeAcc1RGB), 0.1)` on `.DataTable` | Header and filter row background. |
| `--HeaderColHover` | `rgba(var(--ThemeAcc1RGB), 0.25)` | Header and filter row hover background. |
| `--RowPadding` | `round(calc(var(--ThemeIconSize) * 4px), 1px)` | Defined but not used in this file. |
| `--s` | `calc(20px * var(--ThemeIconSize))` on `td.ValueFormatDt:after` | Date/time glyph size. |
| `--Margin` | not defined here (host/app shell) | Margins of the `SwSelfWide` / `SwSelfNarrow` variants. |

#### Animations (@keyframes) and media queries / breakpoints / theme variants

| Item | Description |
|---|---|
| `@keyframes DataTableFlash` | Fades a cell background from `--ThemeAcc225` to transparent; used by `td.Flash` (1s). |
| `html.SwSelfWide` / `html.SwSelfNarrow` | Layout variants (see Page); no media queries or `[data-theme]` rules. |
