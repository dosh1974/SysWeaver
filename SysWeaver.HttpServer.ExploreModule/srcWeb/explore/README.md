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
- Values computed but not used further: the `keys` list in `tableUpdateHeader` (tests `TableDataProps.key`, which is not defined), the `rowsPerPage` argument, the member path in the api `EditChange` handler, and the `AbortHandler` instance in `addTable`.
