# ServerManager

[⬆ srcWeb](../README.md)

> The browser pages of the server manager: host overview and metrics, managed services with their versions, and key files.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.ServerManager](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

`ServerManagerService` adds three of these pages to the menu: *Server* (`server.html`), *Managed services* (`services.html`) and *Key files* (`keys.html`, Admin only). The other pages are opened from links. The `TableData` URLs on `SmServiceBrief`, `SmDriveInfo` and `SmProcessMetrics` open `serviceInfo.html` and `server_metrics.html`, and `serviceInfo.js` opens `versionInfo.html`. Each page is an HTML file plus a script with a `*Main()` entry point, and most also have a stylesheet.

## Contents

| Item | Description |
|---|---|
| [`server.html`](server.html) / [`server.js`](server.js) / [`server.css`](server.css) | Server overview with CPU, memory and drive charts (`serverMain()`) |
| [`server_metrics.html`](server_metrics.html) | Detail charts for one metric (drive, process CPU or memory), using `serverMetricsMain()` from `server.js` |
| [`services.html`](services.html) / [`services.js`](services.js) / [`services.css`](services.css) | List of managed services (`servicesMain()`) |
| [`serviceInfo.html`](serviceInfo.html) / [`serviceInfo.js`](serviceInfo.js) / [`serviceInfo.css`](serviceInfo.css) | Details of one managed service: versions, control, configs and logs, file uploads (`serviceInfoMain()`) |
| [`versionInfo.html`](versionInfo.html) / [`versionInfo.js`](versionInfo.js) / [`versionInfo.css`](versionInfo.css) | Details of one uploaded version of a service (`versionInfoMain()`) |
| [`keys.html`](keys.html) / [`keys.js`](keys.js) / [`keys.css`](keys.css) | Key file management with upload (`keysMain()`) |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.ServerManager](../../README.md)
- **Uses:** shared web assets of [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md) (`common/`, `app/application.js`), `explore/table.js` from [SysWeaver.HttpServer.ExploreModule](../../../SysWeaver.HttpServer.ExploreModule/README.md), `edit/Edit.js` from [SysWeaver.MicroService.Edit](../../../SysWeaver.MicroService.Edit/README.md), `common/fileUploader.js` from [SysWeaver.MicroService.FileUploader](../../../SysWeaver.MicroService.FileUploader/README.md)

## Behavior (web analysis)

### Loading and initialization

Every page has the same shape: an HTML shell with the standard head (meta tags, favicon/manifest from `../`, `../common/theme.css`), `defer` scripts, and `<body onload="xxxMain()">`. Some pages contain static custom-element placeholders that the script fills in. Nothing is templated with `${...}`. The pages are normally shown embedded in the app shell (`WebMenuEmbedded`), or opened from links inside other ServerManager pages and explore tables.

| Page | Scripts (in order) | Styles | Entry |
|---|---|---|---|
| `server.html` | common.js, app/application.js, server.js | theme, application.css, server.css | `serverMain()` |
| `server_metrics.html` | common.js, server.js | theme, server.css | `serverMetricsMain()` |
| `services.html` | common.js, application.js, edit/Edit.js, explore/table.js, services.js | theme, application, table, Edit/EditDesktop/EditMobile, services.css | `servicesMain()` |
| `serviceInfo.html` | common.js, md5.js, UZIP.js, fileUploader.js, application.js, serviceInfo.js | theme, application, serviceInfo.css | `serviceInfoMain()` |
| `versionInfo.html` | common.js, application.js, versionInfo.js | theme, application, versionInfo.css | `versionInfoMain()` |
| `keys.html` | common.js, md5.js, UZIP.js, fileUploader.js, application.js, explore/table.js, keys.js | theme, application, table, keys.css | `keysMain()` |

Entry points into the pages:
- `ServerManagerService` (`[WebApiUrl("../ServerManager")]`) registers three menu items: `[WebMenuEmbedded]` *Server* (`ServerManager/server.html`), *Managed services* (`ServerManager/services.html`) and *Key files* (`ServerManager/keys.html`, `Roles.Admin`).
- Table cells link to the other pages. `SmServiceBrief` has `[TableDataUrl("{0}", "*../ServerManager/serviceInfo.html?p={0}")]`. `SmDriveInfo` and `SmProcessMetrics` link to `server_metrics.html?q1=..&q2=..&q3=..&title=..`. A leading `*` is a link-target prefix meaning `_self` (common.js `getUrlTarget`; `+` means `_blank`, `^` `_top`, `-` `_parent`).
- Most pages call `PageLoaded()` (common.js) once the first content is in place, which tells the hosting shell or loader that the page has loaded.

### Files

**`server.js`** (shared by `server.html` and `server_metrics.html`)
- `addChart(to, fetchUrl, openOnClick, title, extra)` (global): creates `<server-item>` with an iframe from `createIFrame()` (`tabIndex -1`; the iframe has `pointer-events:none` in CSS), whose `src` is `../chart/chart.html?q=../ServerManager/<fetchUrl>&transparent=true&m=false&noLabels=true&center=true<extra>`. A transparent `<server-itemclick>` overlay takes clicks (`badClick`, `keyboardClick`). On click it opens `openOnClick` in place, or derives a metrics page URL: `server_metrics.html?q1=<X>Chart&q2=<X>HistoryShortChart&q3=<X>HistoryChart&title=...`, built by string-replacing `Chart` with `HistoryChart` and then `ShortChart`. This depends on the C# API names following the pattern `GetXxxChart`, `GetXxxHistoryChart` and `GetXxxHistoryShortChart`.
- `serverMain()`: builds `<server-stats>`, `<server-charts>` and `<server-commands>`. `addStats(icon, text, title, link, isCommand)` creates `<server-stat>` or `<server-command>` with a `ColorIcon("../icons/<icon>.svg")` and a `<server-text>`. `link` is either a URL (opened in place with `Open(url, "_self")`) or an async function (the element is disabled while it runs). It first calls `GET ../ServerManager/GetServerInfo`, using half the measured round trip to correct the server clock. The stats are machine name, OS (icon `os_<OsBase>`), CPU count (link to the CPU metrics page), memory (link to the memory metrics page), a live server clock (`updateServerTime` every 100 ms from a local offset; clicking copies the timestamp) and the process count (link to `../explore/table.html?q=../ServerManager/ProcessInfoTable`). The commands are **Reboot** (a `Confirm` dialog, then `GET ../ServerManager/RebootComputer`) and **Deleted** (opens `../ServerManager/Bak/explore`). The charts are `GetServicesChart` (click opens `services.html`), `GetCpuChart`, `GetMemChart` and `GetDriveChart?<i>` for `info.DriveCount` drives. `updateStats()` polls `GET ../ServerManager/GetServerStats` every 5 s to resync the clock and refresh the process count.
- `serverMetricsMain()`: reads `q1`, `q2`, `q3` and `title` (sets `document.title`), adds `body.Dual` and stacks three `addChart` items. `q1` is the current value (centred), `q2` the short history and `q3` the long history, the last two with `aspect=false&noLabels=true`. Clicking a chart opens that chart in full on the standalone `../chart/chart.html` (with its menu).

**`server.css`**: uses custom properties `--pad`, `--w` and `--h` to make square chart tiles (up to 400px, two per row) in a flex-wrapped `server-charts`. In `body.Dual` the first chart is short (20% of height) and the other two share the remaining height. It also styles the stat and command pills (`.Click`, `.Disabled`, hover and focus outlines).

**`services.js` / `services.css`**
- `servicesMain()`: `GET ../Api/auth/GetUser`. If the user object has `Tokens`, it adds a 64px `ColorIcon` plus-button to `<ss-add>` (adding the class `Show`). The button calls `PopUpEdit("SysWeaver.MicroService.ManagedService", null, "Add", ...)` (an edit dialog built from a server-side type; `PopUpEdit` is in application.js and uses Edit.js) and then `sendRequest("AddService", res)` (resolved relative to the page, so `ServerManager/AddService`). It shows `StartWorking` / `StopWorking` on the icon. It then renders `Table.addTable("../ServerManager/ServicesTable", <ss-table>)`. The table rows link to `serviceInfo.html`.
- The role loop computes an `ok` flag (debug/admin) that is not used to decide whether the button is shown. Whether the user may add a service is enforced on the server.
- `services.css`: `ss-add` is hidden until it has the class `.Show`.

**`serviceInfo.js` / `serviceInfo.css`** (the largest page)
- `BeginElementChildUpdate(target)` (global; an identical copy exists in versionInfo.js): a small reconciler for child lists. `Add(e)` keeps the existing child at that index if its `outerHTML` is equal, otherwise replaces or appends it, and returns the node that is now in place. `Complete()` removes the remaining children. It is used for the polled lists so they update without flicker.
- `serviceInfoMain()`: reads `p` (service name, required) and `v` (view-only; adds `body.ViewOnly`, which removes `pointer-events`). When `window.IsTop` is set it adds `TopWindow`, which shows the `<si-name>` header bar. It fills the static placeholders: `si-state[0..4]` (status, PID, CPU, memory, total CPU time), and two `si-graph` iframes that point at `../chart/chart.html?...&q=../ServerManager/GetServiceCpuChart?"<svc>"` and `GetServiceMemChart?"<svc>"` (the API argument is a JSON string literal in the query). Clicking a CPU or memory state or graph opens `server_metrics.html` with the `GetServiceCpu*` or `GetServiceMem*` charts.
- Service control buttons (`Button` from common.js; the icons are the CSS classes `si-icon-*`): Restart, Pause, Resume (sends `Continue`), Stop, Start, Disable (sends `Uninstall`) and Kill (after a `Confirm`). Each calls `DoVerb`, which disables all buttons and runs `sendRequest(<Verb>, service)`. The response is a fresh `SmServiceDetail` that replaces `data`. `updateButtons(status)` enables each button according to the status (`Running`, `Paused`, `Stopped`, `NotInstalled`, `Unknown`), `data.IsSm` (the server manager's own service cannot be paused, stopped, ...) and `data.ProcId`.
- Debug buttons: **Explore** opens `../FolderSync/ManagedFolders/<svc>/explore`. **View log** opens `../edit/text.html?scrollToEnd=true&r=../FolderSync/ManagedFolders/<svc>/<data.Log.Name>&d=../ServerManager/DeleteLogFile`. **Deleted** opens `../ServerManager/ServiceBak/<svc>/explore`.
- Config file lists: `updateFileList(el, files, header, title, folderSuffix, isMaster)` renders a grid of `si-file-state` (a star marks files that are not backups), `si-file-icon` (background `../icons/ext/<ext>.svg`), `si-file-name`, `si-file-size` and `si-file-time`. Clicking a name opens `../edit/text.html?r=<folderSuffix><svc>/<file>&u=../ServerManager/UpdateConfig&d=../ServerManager/DeleteConfigFile`. The icon opens a `PopUpMenu` (also on right-click) with these items: View (`../logFile/logfile.html?api=...`), Activate (backup files only; `ActivateConfig`), Use as current (master only; `UseMasterConfig`), Copy file name, Copy local path, Copy local folder, Download (`downloadFile`) and Delete (`Confirm`, then `DeleteConfig`). The config requests take the body `{ServiceName, Config, IsMaster}` (`SmConfigRequest`). Current configs are served from `../FolderSync/ManagedFolders/`, master configs from `../ServerManager/Data/`.
- `isBackup(fn)` / `isBackupNew(fn)` recognise backup file names character by character, with maps of where `-` and `_` separators sit. There are two formats: `Bak_yyyy-MM-dd_HH_mm_ss_yyyy-MM-dd_HH_mm_ss[_n].*` (new) and `name.yyyy-MM-dd_HH_mm_ss[_n].ext` or `name.LastGood.ext` (old). The code comment says this must match the C# `ServiceHost.NewIsConfigBackupName`.
- Uploads: on the first update, `fileUploaderSetup(<si-uploads>[0], "Current_<svc>", null, UploadCompleted, null, true)` and the same for `Master_<svc>`. The server provides these as `BackupFileRepo` instances. `UploadCompleted` reports the first failure, the first "already uploaded" or the first success, and on success calls `updateNow()`.
- Versions list (`si-versions` grid): one row per `data.Versions` entry with an active star, the upload time (click opens `versionInfo.html?p=<svc>,<Uploaded>,<Name>`), user, machine and comment (each copy-on-click) and the last-used time.
- Polling: a `for(;;)` loop runs `sendRequest("GetDetail", service)`, then `update(first)`, then `delayWithAbort(5000, abortWait)`. `updateNow()` calls `abortWait.raise()` to cut the wait short. `stateMap` maps the service status to the CSS classes `Ok`, `Warning` and `Error` on `si-state`.
- `serviceInfo.css`: a centred column `--MaxW: 100ch` with a fixed `si-bg` shadow panel. It styles the state pills (`Ok`, `Warning`, `Error`, `Hide`), the two-up graph tiles (full width in portrait), the upload buttons (`si-uploads`, shown when they have `.Show`, with the icon changing on `Dragging`, `Selecting` and `Uploading`), the file and version CSS grids and the `si-icon-*` mask icons.

**`versionInfo.js` / `versionInfo.css`**
- `versionInfoMain()`: reads `p` = `"<service>,<uploadedIso>,<folderName>"` (a comma-separated key that is sent back to the server as is). The title is built from it with a formatted timestamp. It polls `sendRequest("GetVersion", version)` every 5 s with `delayWithAbort`. The first time, it creates action `Button`s: Activate (`VersionActivate`), Touch (`VersionTouch`; shows `SmCompFileStats`), Verify (`VersionVerify`; stats), Expand (`VersionExpand`), Compress (`VersionCompress`) and Delete (`Confirm`, then `VersionDelete`, then `History.back()`). `doWork` serialises the actions: it disables all buttons, runs the action, then fetches the version again and updates the key, because the folder name changes when a version is activated. Enablement depends on `IsActive`, `Comp` and `IsCompressedService`. The page renders key/value pairs into the first `si-values` grid (uploaded, active, folder, user, machine, comment, last used, compressed, files, size, path) and the chunk statistics into the second (files, chunks, unique chunks, reuse %, compressed and expanded size, ratio, broken files and missing chunks).
- `versionInfo.css`: the same `si-bg`/`si-name`/`TopWindow` layout as serviceInfo, plus the `si-values` grid and the `si-actions` flex row.

**`keys.js` / `keys.css`**
- `keysMain()`: `fileUploaderSetup(<kf-upload>, "Keys", noop, UploadCompleted, null, true)` (the repo `Keys` is a `BackupFileRepo` over the key folder), then `Table.addTable("../ServerManager/KeysTable", <kf-table>)`. `UploadCompleted` has the same logic as in serviceInfo. On success it calls `updateNow()`, which refreshes the key table through the `UpdateNow` function returned by `Table.addTable`.
- `keys.css`: a 64px upload icon button with the `Dragging`, `Selecting` and `Uploading` states.

### Server interaction

All endpoints belong to `ServerManagerService` (URL root `ServerManager/`) unless noted. Calls without a path (`"GetDetail"`, `"AddService"`, ...) resolve relative to the page URL, which only works because the pages are served from the same `ServerManager/` folder as the API.

- Raw chart JSON (Chart.js config, consumed through `chart.html`): `GetServicesChart`, `GetCpuChart`, `GetCpuHistoryChart`, `GetCpuHistoryShortChart`, `GetMemChart` and its history variants, `GetDriveChart?<i>` and its history variants, `GetProcess{Cpu,Mem}[History[Short]]Chart?<pid>`, and `GetService{Cpu,Mem}[History[Short]]Chart?"<svc>"`. GET calls with a single argument pass it as JSON after `?`.
- Info: `GetServerInfo` (`SmServerInfo`: Machine, Os, OsBase, ProcessorCount, Memory, Time, TzName, TzDayName, ProcessCount, DriveCount) and `GetServerStats` (Time, ProcessCount).
- Commands: `RebootComputer`. The service verbs `Restart`, `Pause`, `Continue`, `Stop`, `Start`, `Uninstall` and `Kill` take a service name and return `SmServiceDetail`. `GetDetail` returns `SmServiceDetail` (Status, ProcId, CpuUsage, MemUsage, TotalProcessorTime, IsSm, Log, Folder, CurrentFolder, Configs, MasterConfigs, Versions). `AddService` takes `ManagedService`.
- Versions: `GetVersion`, `VersionActivate`, `VersionTouch`, `VersionVerify`, `VersionExpand`, `VersionCompress` and `VersionDelete`, each taking the version key string.
- Configs: `ActivateConfig`, `UseMasterConfig` and `DeleteConfig` (`SmConfigRequest`). `UpdateConfig`, `DeleteConfigFile` and `DeleteLogFile` are passed as URLs to the edit page (`edit/text.html` `u=` and `d=` parameters), which calls them.
- Tables (explore table protocol): `ServicesTable`, `KeysTable`, `ProcessInfoTable`.
- Many control endpoints carry `[WebApiAuth(Roles.AdminOps)]`. The pages do no client-side role gating apart from `services.js`'s check that `Tokens` exists and the `v` view-only flag.
- Other modules: `../Api/auth/GetUser` (auth), `../chart/chart.html` (ChartJs), `upload/CheckStatus` and `upload/Upload` through fileUploader.js (repos `Keys`, `Current_<svc>`, `Master_<svc>`), `../FolderSync/ManagedFolders/...` (FolderSync file serving and `/explore`), `../ServerManager/Bak/explore`, `../ServerManager/ServiceBak/<svc>/explore` and `../ServerManager/Data/` (static folders that ServerManager registers), `../edit/text.html` (Edit), `../logFile/logfile.html` (LogFile), `../explore/table.html`.

### State and persistence

- No localStorage, sessionStorage or cookies. State lives in the main-function closures: `data` (last detail or version), `buttonsEnabled` / `blockButtonStates`, `abortWait` (`AbortHandler`) and the server clock offset.
- Polling: serviceInfo and versionInfo poll every 5 s in an endless async loop (abortable delay). server.js polls stats every 5 s and redraws the clock every 100 ms. Each embedded chart iframe polls on its own `RefreshRate`.
- Navigation is in place (`Open(url, "_self")`), and context travels in URL parameters (`p`, `v`, `q1`/`q2`/`q3`/`title`).
- Expandos: upload elements get the uploader expandos; `si-file-icon` gets `.Config` (the file record); service buttons rely on `element.Button` (set by common.js `Button`) to disable them all.

### Dependencies

- `common/common.js`: `getUrlParams`, `getRequest`, `sendRequest`, `Open`, `badClick`, `keyboardClick`, `rightClick`, `ColorIcon`, `Button`, `createIFrame`, `AddLoading`, `PageLoaded`, `Fail`, `Info`, `_T` / `_TF`, `ValueFormat` (`formatByteSize`, `formatTimeSpan`, `toString`, `getTimeStampTitle`, `convertUTCDateToLocalDate`, `copyOnClick`, `copyToClipboardInfo`, `stringFormat`), `AbortHandler`, `delayWithAbort`, `delay`, `downloadFile`, `window.IsTop`.
- `app/application.js`: `Confirm`, `PopUpMenu`, `WebMenu`, `WebMenuItem`, `PopUpEdit` (with `edit/Edit.js` loaded on services.html).
- `explore/table.js`: `Table.addTable(url, element)` (services, keys).
- `common/fileUploader.js` (+ `md5.js`, `UZIP.js`): `fileUploaderSetup`, `UploadStatus`, `fileUploaderStatusText`.
- `chart/chart.html` (ChartJs service), only through iframes and URLs.
- Shared icons in `../icons/` (including `icons/ext/*.svg` and `os_*.svg`).
- No other first-party web code includes these scripts. Other code reaches the pages through the menu and table URLs.

### Structural observations

- Global functions in classic scripts with one `xxxMain` per page. `server.js` serves two pages (`serverMain` and `serverMetricsMain`) and exposes `addChart` globally. That name is unrelated to `CanvasChart.addChart`; here it means "iframe a chart page".
- `BeginElementChildUpdate` is copied in full between `serviceInfo.js` and `versionInfo.js`. The `UploadCompleted` handler is copied between `serviceInfo.js` and `keys.js`. The `si-bg`, `si-name` and `TopWindow` CSS is copied between `serviceInfo.css` and `versionInfo.css`. The page-head boilerplate is repeated in every HTML file.
- The pages rely on static placeholder elements found by tag name and index (`getElementsByTagName("si-state")[2]`, `si-configs[1]`, `si-values[1]`), so the HTML structure and the script are tightly coupled.
- Implicit contracts with C#: the chart API naming pattern (`*Chart`, `*HistoryChart`, `*HistoryShortChart`, which `addChart` builds by string replacement), service status strings, backup file name formats (mirrored from `ServiceHost`), repo names (`Keys`, `Current_<svc>`, `Master_<svc>`), the version key format `svc,uploaded,folder`, and DTO field names.
- Relative API calls without a path tie the page location to the API URL root (`ServerManager/`).
- Embedding a chart means an iframe for each tile, each with its own Chart.js runtime and polling loop. Communication is only one way, through URL parameters; the `ChartClick` postMessage is not used here, and click overlays sit on top of the iframes instead.
- Service control and file operations are fire-and-refresh: each action is followed by an immediate re-poll (`updateNow` / `abortWait.raise()`) rather than a local state update.

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function (closures over its state); "global" marks top-level functions of a classic script (they become `window` properties). All scripts are loaded with a static `<script defer>` tag from the page HTML and started from `<body onload="...">`.

### `keys.js`

Script of `keys.html` (Key files page). Defines one global, `keysMain`, which wires a file-upload drop target for the `Keys` repo and renders the key file table. No other globals and no immediately executed code.

#### Functions

| Function | Signature | Description |
|---|---|---|
| `keysMain` | async `keysMain()` → `Promise<void>` | Entry point (`<body onload>`). Calls `fileUploaderSetup(<kf-upload>[0], "Keys", f => {}, UploadCompleted, null, true)` (fileUploader.js; the third argument is a no-op callback), then awaits `Table.addTable("../ServerManager/KeysTable", <kf-table>[0])` (explore/table.js), keeps the returned `{ UpdateNow }` in `table`, and calls `PageLoaded()`. |
| `updateNow` (inner of `keysMain`) | `updateNow()` | Calls `table.UpdateNow()` (once the table exists) to refresh the key list immediately. |
| `UploadCompleted` (inner of `keysMain`) | `UploadCompleted(e, res, files)` | Upload-finished callback passed to `fileUploaderSetup`. `e` = upload element, `res` = upload result (`Error` string or `Status` array of `UploadStatus` values, one per file), `files` = the `File` list. If `res.Error` is set shows `Fail(err)`. Otherwise, in three passes: the first status that is neither `UploadStatus.AlreadyUploaded` nor `UploadStatus.None` shows `Fail("<fileUploaderStatusText(status)>, when uploading \"<name>\"")`; else the first `AlreadyUploaded` shows `Info("File \"<name>\" was already uploaded")`; else the first `None` (success) shows `Info("Uploaded \"<name>\"")` and calls `updateNow()` (refreshes the key table). All texts go through `_T`. |

### `services.js`

Script of `services.html` (Managed services page). Defines one global, `servicesMain`. No immediately executed code.

#### Functions

| Function | Signature | Description |
|---|---|---|
| `servicesMain` | async `servicesMain()` → `Promise<void>` | Entry point. Awaits `getRequest("../Api/auth/GetUser")`. If the user has a `Tokens` array, loops over it computing a local `ok` flag (compares the array itself against `"debug"` / `"admin"`; the flag is not used afterwards), then creates a 64×64 `ColorIcon("../icons/plus.svg", "IconColorThemeMain", ...)` whose click handler calls `StartWorking()`, awaits `PopUpEdit("SysWeaver.MicroService.ManagedService", null, "Add", "../icons/plus.svg", ...)` (an edit dialog for a server type; application.js + edit/Edit.js), and if a value is returned sends `sendRequest("AddService", res)` (page-relative, i.e. `ServerManager/AddService`), reporting `Info("Create new managed service")` or `Fail(...)`; `StopWorking()` in `finally`. The icon element is appended to `<ss-add>[0]`, which gets class `Show`. Finally awaits `Table.addTable("../ServerManager/ServicesTable", <ss-table>[0])` and calls `PageLoaded()`. |

### `server.js`

Shared script of `server.html` (entry `serverMain`) and `server_metrics.html` (entry `serverMetricsMain`). Defines three globals: `addChart`, `serverMain`, `serverMetricsMain`. No immediately executed code. Charts are embedded as iframes of `../chart/chart.html` (ChartJs service) that fetch chart JSON from `../ServerManager/<api>`.

#### Functions

| Function | Signature | Description |
|---|---|---|
| `addChart` | `addChart(to, fetchUrl, openOnClick, title, extra)` | Appends a `<server-item>` to element `to`, containing an iframe from `createIFrame()` (`tabIndex = -1`) with `src = "../chart/chart.html?q=../ServerManager/" + fetchUrl + "&transparent=true&m=false&noLabels=true&center=true" + extra` (`extra` is ignored unless it is a string), plus a `<server-itemclick>` overlay. The overlay's click handler (ignores `badClick`) opens `openOnClick` with `Open(url, "_self")` if given; otherwise it derives `h = fetchUrl.replace("Chart", "HistoryChart")` and opens `server_metrics.html?q1=<fetchUrl>&q2=<h with "Chart"→"ShortChart">&q3=<h>&title=<title>`. `keyboardClick(overlay)` makes it focusable/keyboard-activatable. No return value. |
| `serverMain` | async `serverMain()` → `Promise<void>` | Entry point of `server.html`. Appends `<server-stats>`, `<server-charts>` and `<server-commands>` to `body`. Measures the round trip of `getRequest("../ServerManager/GetServerInfo")` and keeps `serverTimeStart` (server UTC time + half ping, converted with `ValueFormat.convertUTCDateToLocalDate`) and `atTime` (local receive time). Adds stats via `addStats`: machine name (`computer`), OS (`os_<OsBase>`), CPU count (link to the CPU metrics page), memory (`ValueFormat.formatByteSize`, link to the memory metrics page), a clock (click copies `"yyyy-MM-dd HH:mm:ss"` of the computed server time with `ValueFormat.copyToClipboardInfo`), and process count (link `../explore/table.html?q=../ServerManager/ProcessInfoTable`). Adds commands *Reboot* (`Confirm(...)`, then `Info`, `getRequest("../ServerManager/RebootComputer")`, `delay(10000)`) and *Deleted* (`Open("../ServerManager/Bak/explore", "_self")`). Adds charts `GetServicesChart` (click opens `services.html`), `GetCpuChart`, `GetMemChart` and `GetDriveChart?<i>` for `i < info.DriveCount`. Calls `PageLoaded()` and schedules `updateStats` after 5 s. |
| `addStats` (inner of `serverMain`) | `addStats(icon, text, title, link, isCommand)` → `[element, textElement]` | Creates `<server-command>` (if `isCommand`) or `<server-stat>` with `title`, a `ColorIcon("../icons/<icon>.svg")` (32 px `IconColorThemeAcc2` for commands, 24 px `IconColorThemeMain` for stats) and a `<server-text>` containing `text`. If `link` is set, adds class `Click`, `keyboardClick`, and an async click handler: a string `link` is opened with `Open(link, "_self")`; a function `link` is awaited once at a time (re-entry guarded), with class `Click` swapped for `Disabled` while running and errors shown via `Fail(e.message)`. Appends to `<server-commands>` or `<server-stats>`. Returns the element and its text element. |
| `updateServerTime` (inner of `serverMain`) | `updateServerTime()` | Computes the current server time as `serverTimeStart + (now - atTime)` and writes `HH:mm:ss` into the clock's `<server-text>` and a title with date, time zone (`info.TzDayName`, plus `info.TzName` in parentheses if different) and a hint, only when changed. Exceptions are swallowed. Reschedules itself every 100 ms with `setTimeout`. |
| `updateStats` (inner of `serverMain`) | async `updateStats()` | Fetches `getRequest("../ServerManager/GetServerStats")`, re-syncs `atTime` / `serverTimeStart` with half-ping correction, and updates the process count text if changed. Exceptions are swallowed. Reschedules itself every 5000 ms. |
| `serverMetricsMain` | async `serverMetricsMain()` → `Promise<void>` | Entry point of `server_metrics.html`. Shows a loader (`AddLoading()`), reads URL params `q1`, `q2`, `q3`, `title` (`getUrlParams`); sets `document.title` to `title` if present; adds class `Dual` to `body`; appends a `<server-charts>` and calls `addChart` three times: `q1` (click opens `../chart/chart.html?q=../ServerManager/<q1>&center=true`), and `q2` / `q3` with `extra = "&aspect=false"` (click opens the chart page with `&aspect=false&noLabels=true`). Closes the loader. |

### `serviceInfo.js`

Script of `serviceInfo.html` (one managed service). Defines two globals: `BeginElementChildUpdate` (a copy of the one in `versionInfo.js`) and `serviceInfoMain`. All other logic is inner functions of `serviceInfoMain`, which ends in an endless polling loop. Relies on the static placeholders in `serviceInfo.html` (`si-name`, five `si-state`, `si-servicebuttons`, two `si-graph` with iframes, `si-debugbuttons`, two `si-configs`, two `si-uploads`, `si-versions`).

#### Functions

| Function | Signature | Description |
|---|---|---|
| `BeginElementChildUpdate` | `BeginElementChildUpdate(targetElement)` → `{ Target, Add, Complete }` | Starts an in-place reconciliation of `targetElement`'s children. Captures the current child count and an index. `Add(e)` / `Complete()` are the inner functions `add` / `complete` below; `Target` is the element. Used so that polled lists only touch changed nodes. |
| `add` (inner of `BeginElementChildUpdate`) | `add(e)` → `Element` | If past the original children, appends `e` and returns it. Otherwise compares the child at the current index with `e` by `outerHTML`: if equal keeps the existing child and returns it (so expandos/handlers should be set on the returned node), else replaces it with `e` and returns `e`. Advances the index. |
| `complete` (inner of `BeginElementChildUpdate`) | `complete()` | Removes all original children at or after the current index (from the end backwards). |
| `serviceInfoMain` | async `serviceInfoMain()` → never resolves | Entry point. Reads URL params `p` (service name; `Fail` and return if missing) and `v` (view-only → `body.ViewOnly`); adds `body.TopWindow` when `window.IsTop`. Sets `document.title = "Service - <svc>"` and the `<si-name>` text. Unless view-only, makes `si-state[2]` / `si-state[3]` and both `si-graph` elements clickable (`keyboardClick`) to open `server_metrics.html` with `GetServiceCpu[History[Short]]Chart?"<svc>"` or `GetServiceMem...` charts. Sets the graph iframes' `src` to `../chart/chart.html?transparent=true&m=false&aspect=false&noLabels=true&center=true&q=../ServerManager/GetServiceCpuChart?"<svc>"` and `...GetServiceMemChart?"<svc>"` (`tabIndex -1`). Creates an `AbortHandler` (`abortWait`). Unless view-only, builds the service control buttons (Restart, Pause, Resume→`Continue`, Stop, Start, Disable→`Uninstall`, Kill with `Confirm`; `Button` from common.js with `si-icon-*` classes, each calling `DoVerb`) in `<si-servicebuttons>` and debug buttons in `<si-debugbuttons>`: Explore (`../FolderSync/ManagedFolders/<svc>/explore`), View log (`../edit/text.html?scrollToEnd=true&r=../FolderSync/ManagedFolders/<svc>/<data.Log.Name>&d=../ServerManager/DeleteLogFile`), Deleted (`../ServerManager/ServiceBak/<svc>/explore`, icon `../icons/trash.svg`); and assigns the real `updateButtons`. Builds `stateMap` and the backup-name maps, then loops forever: `data = await sendRequest("GetDetail", svc)`, `update(first)`, `await delayWithAbort(5000, abortWait)`; errors go to `Fail(e)`. |
| `updateButtons` (inner variable of `serviceInfoMain`) | `updateButtons(status)` | Initially a no-op; when not view-only it is replaced by a closure that, unless `blockButtonStates` is set, enables: Restart if `Running`; Pause if not `data.IsSm` and `Running`; Resume if not SM and `Paused`; Stop if not SM and Running/Paused; Start if not SM and `Stopped`/`NotInstalled`/`Unknown`; Disable if not SM and Stopped/Running/Paused; Kill if not SM and `data.ProcId > 0`. Always enables View log iff `data.Log` is set. |
| `updateNow` (inner of `serviceInfoMain`) | `updateNow()` | Calls `abortWait.raise()`, which ends the current 5 s `delayWithAbort` so the next `GetDetail` poll runs immediately. |
| `DoVerb` (inner of `serviceInfoMain`, non-view-only block) | async `DoVerb(button, v, okText)` | Runs service verb `v` (`"Restart"`, `"Pause"`, `"Continue"`, `"Stop"`, `"Start"`, `"Uninstall"`, `"Kill"`). Sets `blockButtonStates`, calls `button.StartWorking()`, disables every child of `<si-servicebuttons>` through its `.Button` expando, awaits `sendRequest(v, service)`. On a falsy result: `Fail("Failed to <v>")` and `abortWait.raise()`. On success: `data = newData`, clears `blockButtonStates`, `updateNow()`, `Info(ValueFormat.stringFormat(okText, service))` (`okText` contains `{0}`). On exception: `Fail`, clear block, `abortWait.raise()`. Always `button.StopWorking()`. |
| `isBackupNew` (inner of `serviceInfoMain`) | `isBackupNew(fn)` → `boolean` | True if `fn` matches the new backup name format `Bak_yyyy-MM-dd_HH_mm_ss_yyyy-MM-dd_HH_mm_ss[_<digits>].<rest>`: starts with `Bak_`, the part before the first `.` is at least 43 chars, positions 4–42 are digits except the separators in `bakMapNew` (`-` at 8, 11, 28, 31; `_` at 14, 17, 20, 23, 34, 37, 40), and anything after position 43 is `_` followed by digits. Mirrors C# `ServiceHost.NewIsConfigBackupName`. |
| `isBackup` (inner of `serviceInfoMain`) | `isBackup(fn)` → `boolean` | True if `isBackupNew(fn)`, or if the second-to-last dot segment (`name.<seg>.ext`) is `LastGood` or matches `yyyy-MM-dd_HH_mm_ss[_<digits>]` (19 chars checked against `bakMap`: `-` at 4, 7; `_` at 10, 13, 16; the rest digits). |
| `updateFileList` (inner of `serviceInfoMain`) | `updateFileList(el, files, headerText, headerTitle, folderSuffix = "../FolderSync/ManagedFolders/", isMaster = false)` | Reconciles grid `el` (a `<si-configs>`) via `BeginElementChildUpdate`: a `<si-file-header>` (`headerText`, title `headerTitle`), then for each `files` entry (`{Name, Size, LastModified}`) five cells: `<si-file-state>` (class `Show` = not a backup per `isBackup`), `<si-file-icon>` (background `../icons/ext/<ext>.svg`; expando `.Config` = the file record set on the node returned by `Add`), `<si-file-name>` (click opens `../edit/text.html?r=<folderSuffix><svc>/<name>&u=../ServerManager/UpdateConfig&d=../ServerManager/DeleteConfigFile`), `<si-file-size>` (`formatByteSize`, copy-on-click) and `<si-file-time>` (`getTimeStampTitle`, copy-on-click). Unless view-only, the icon gets click/keyboard/right-click handlers that open a `PopUpMenu` (popup element gets class `Rel`) with a `WebMenu` named `SmConfigFiles`: View (`../logFile/logfile.html?api=<folderSuffix><svc>/<name>`), Activate (only if `isBackup`; `sendRequest("ActivateConfig", {ServiceName, Config, IsMaster})`), Use as current (only if `isMaster`; `UseMasterConfig`), Copy file name, Copy local path (`<data.Folder or data.CurrentFolder>\<name>`), Copy local folder, Download (`downloadFile(name, <folderSuffix><svc>/<name>)`), Delete (`Confirm`, then `DeleteConfig`). Successful server actions call `updateNow()`. Ends with `Complete()`. |
| `update` (inner of `serviceInfoMain`) | `update(first)` | Renders the latest `data` (`SmServiceDetail`). On `first`: `PageLoaded()`, sets titles of Explore / View log buttons and both `si-uploads` with folder paths. Writes `data.Status` into `si-state[0]` and replaces its classes with `stateMap.get(status)` (`Ok` / `Warning` / `Error`). Adds/removes `Hide` on `si-state[1..4]` when `ProcId === 0`; writes `Id:`, `Cpu: <n> %`, `Mem: <bytes>`, `Tot: <timespan>`. If `data.Configs` exists: `updateFileList(configs, ...)`, `configs.Show`, and on first (not view-only) `fileUploaderSetup(uploads, "Current_<svc>", null, UploadCompleted, null, true)` + `keyboardClick` + `Show`; else removes `Show`. Same for `data.MasterConfigs` with folder `../ServerManager/Data/`, `isMaster = true` and repo `Master_<svc>`. Reconciles `<si-versions>`: a `<si-version-header>` and per version `<si-version-active>` (class `Active` if `IsActive`), `<si-version-uploaded>` (click opens `versionInfo.html?p=<svc>,<Uploaded>,<Name>`), `<si-version-user>`, `<si-version-machine>`, `<si-version-comment>` (copy-on-click), `<si-version-last>`. Finally `updateButtons(status)`. |
| `UploadCompleted` (inner of `update`) | `UploadCompleted(e, res, files)` | Same logic as `keysMain`'s `UploadCompleted` (error → `Fail`; first failing status → `Fail`; first `AlreadyUploaded` → `Info`; first `None` → `Info` + `updateNow()`). Passed to both `fileUploaderSetup` calls. |

#### Inner state of `serviceInfoMain`

| Name | Kind | Description |
|---|---|---|
| `service`, `viewOnly`, `serviceArg` | const | Service name from `p`; view-only flag from `v`; `?"<svc>"` query suffix for chart APIs. |
| `states`, `graphs`, `configs`, `uploads`, `masterConfigs`, `masterUploads`, `versions` | const | Static placeholder elements looked up by tag name / index. |
| `abortWait` | const `AbortHandler` | Aborts the polling delay (`updateNow`). |
| `exploreButton`, `logButton`, `deletedButton`, `blockButtonStates` | let | Debug buttons (null in view-only) and the flag that suppresses `updateButtons` while a verb runs. |
| `stateMap` | const `Map` | Service status → CSS class: `Running`→`Ok`; `StartPending`, `ContinuePending`→`Warning`; `Unknown`, `NotInstalled`, `Stopped`, `StopPending`, `Paused`, `PausePending`→`Error`. |
| `bakMap`, `bakMapNew` | const `Map` | Character index → expected separator for `isBackup` / `isBackupNew`. |
| `data` | let | Last `SmServiceDetail` from `GetDetail` or a verb. |

### `versionInfo.js`

Script of `versionInfo.html` (one uploaded version of a service). Defines two globals: `BeginElementChildUpdate` (identical copy of the one in `serviceInfo.js`) and `versionInfoMain`. Everything else is inner to `versionInfoMain`, which ends in an endless polling loop. Uses placeholders `si-name`, two `si-values` (values and action output) and `si-actions`.

#### Functions

| Function | Signature | Description |
|---|---|---|
| `BeginElementChildUpdate` | `BeginElementChildUpdate(targetElement)` → `{ Target, Add, Complete }` | Same as in `serviceInfo.js` (see there), including the inner `add(e)` (keep/replace/append by `outerHTML`, returns the node in place) and `complete()` (remove leftover children). |
| `add` (inner of `BeginElementChildUpdate`) | `add(e)` → `Element` | See `serviceInfo.js`. |
| `complete` (inner of `BeginElementChildUpdate`) | `complete()` | See `serviceInfo.js`. |
| `versionInfoMain` | async `versionInfoMain()` → never resolves | Entry point. Reads URL param `p` = version key `"<service>,<uploaded>,<folderName>"` (`Fail` and return if missing), splits it into `versionParts`; adds `body.TopWindow` when `window.IsTop`. Calls `setTitle()`, creates `abortWait` (`AbortHandler`) and loops forever: `data = await sendRequest("GetVersion", version)`, `update(first)`, `await delayWithAbort(5000, abortWait)`; errors go to `Fail(e)`. |
| `setTitle` (inner of `versionInfoMain`) | `setTitle()` | Joins `versionParts` with `" - "`, with the uploaded timestamp replaced by its `ValueFormat.getTimeStampTitle(...)[1]` short form, and writes it to `document.title` and `<si-name>`. |
| `updateButtonState` (inner of `versionInfoMain`) | `updateButtonState()` | Enables buttons only when `buttonsEnabled` and the version is not active: Activate, Touch, Delete always; Verify and Expand if `data.Comp`; Compress if not `Comp` and `data.IsCompressedService`. |
| `doWork` (inner of `versionInfoMain`) | async `doWork(button, action)` | Serialises actions: returns if `buttonsEnabled` is false; clears the output `si-values[1]`, disables all buttons, `button.StartWorking()`, awaits `action()` (exception → `Fail(e.Message)`). In `finally`: re-fetches `sendRequest("GetVersion", version)`, updates `versionParts[2]` / `version` from `data.Name` (the folder name changes on activation), `setTitle()`, `update()`, re-enables and `StopWorking()`. |
| `update` (inner of `versionInfoMain`) | `update(first)` | On `first`: refreshes `versionParts` from `data.ServiceName` / `Uploaded` / `Name`, `setTitle()`, creates the action `Button`s in `<si-actions>` (Activate → `VersionActivate`, with a tooltip listing the steps depending on `Comp` / `IsRunning` / `IsCompressedService`; Touch → `VersionTouch` + `showStats`; Verify → `VersionVerify` + `showStats`; Expand → `VersionExpand`; Compress → `VersionCompress`; Delete → `Confirm`, `VersionDelete`, then `History.back()`), each wrapped in `doWork`, and calls `PageLoaded()`. Always: `updateButtonState()` and re-renders `si-values[0]` with Uploaded, Active, Folder, User, Machine, Comment, Last used, Compressed, Files, Size, Path. |
| `showStats` (inner of `update`) | `showStats(r)` | Renders chunk statistics `r` (`SmCompFileStats`) into the output `si-values[1]`: Files, Chunks, Unique chunks, Chunk reuse % (if `ChunkCount > 0`), Compressed size (`ChunkCompSize + FileSize`), Expanded size and Compression ratio (if `ChunkExpSize > 0`), Broken files and Missing chunks (if `TotalMissing > 0`). Reassigns the shared `updater` of the enclosing `update` call. |
| `AddTime` (inner of `update`) | `AddTime(key, value, valueTitle, keyTitle)` | Adds a `<si-value-name>` (`key`, title `keyTitle`) and a `<si-value-value>` with the short timestamp of `value`; title = `valueTitle` + full timestamp; copy-on-click of the raw value. |
| `AddString` (inner of `update`) | `AddString(key, value, valueTitle, keyTitle, copyValue)` | Adds name/value cells; the value copies `copyValue` (or `value`) on click. |
| `AddYesNo` (inner of `update`) | `AddYesNo(key, value, valueTitle, keyTitle)` | `AddString` with `"Yes"` / `"No"`. |
| `AddCount` (inner of `update`) | `AddCount(key, value, valueTitle, keyTitle)` | `AddString` with `ValueFormat.toString(value, 0)`; copies the raw number. |
| `AddPercentage` (inner of `update`) | `AddPercentage(key, value, valueTitle, keyTitle)` | `AddString` with `ValueFormat.toString(value, 2) + " %"`. |
| `AddByteSize` (inner of `update`) | `AddByteSize(key, value, valueTitle, keyTitle)` | `AddString` with `ValueFormat.formatByteSize(value)`. |

#### Inner state of `versionInfoMain`

| Name | Kind | Description |
|---|---|---|
| `version`, `versionParts` | let / const | Current version key string sent to the server, and its comma-split parts (updated when the folder name changes). |
| `name`, `values`, `actions`, `output` | const | `<si-name>`, `si-values[0]`, `<si-actions>`, `si-values[1]`. |
| `buttonsEnabled` | let | False while `doWork` runs. |
| `activateButton`, `touchButton`, `validateButton`, `expandButton`, `compressButton`, `deleteButton` | let | Action buttons, created on the first `update`. |
| `abortWait`, `data` | const / let | Polling abort handle; last version DTO from `GetVersion`. |

## CSS reference

All five stylesheets are plain static CSS (not templated), loaded by a `<link>` in their page after `../common/theme.css` and (on every page except `server_metrics.html`) `../app/application.css`; see the page table above. They rely on theme variables from `theme.css`: `--ThemeMain`, `--ThemeMainRGB`, `--ThemeMain25`, `--ThemeAcc1`, `--ThemeAcc1RGB`, `--ThemeAcc2`, `--ThemeAcc225`, `--ThemeAcc250`, `--ThemeBackground`, `--ThemeOk`, `--ThemeWarning`, `--ThemeError`, `--ThemeRad`, `--ThemeRadInp`, `--ThemeFadeDuration`, `--ThemeFontMono`. The class `KeyClick` is added by common.js `keyboardClick`; `Dragging`, `Selecting`, `Uploading` are toggled by fileUploader.js; `sysweaver-button` is the element created by common.js `Button`. None of these files define `@keyframes` or `[data-theme]` variants; theme switching comes entirely from the theme variables.

### `keys.css`

Styles `keys.html`: page reset and the 64 px upload drop target `<kf-upload>` (populated by `fileUploaderSetup` in `keys.js`). The table itself is styled by `explore/table.css`.

#### Page base

| Selector(s) | Description |
|---|---|
| `:root` | Defines `--MaxW: 100ch` (not used elsewhere in this file). |
| `*` | `box-sizing: border-box`. |
| `body` | No padding/margin, full width. |

#### Upload button

| Selector(s) | Description |
|---|---|
| `kf-upload` | 64×64 px centred block, rounded, pointer cursor, transparent border/outline with theme-fade transitions. |
| `kf-upload.Show` | `display: block` (same as default here). |
| `kf-upload:focus` | `--ThemeAcc1` outline. |
| `kf-upload:after`, `kf-upload:hover:after` | Mask icon `../icons/upload.svg` filled with `--ThemeMain`, opacity 0.8 (1 on hover). |
| `kf-upload.Dragging` | Dashed `--ThemeAcc1` border while a file is dragged over (fileUploader.js). |
| `kf-upload.Selecting:after` | Icon `../icons/disc.svg`, opacity 0.5 while the file picker is open. |
| `kf-upload.Uploading:after` | Icon `../icons/Loading.svg`, opacity 0.5 during upload. |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--MaxW` | `100ch` on `:root` | Declared only. |

### `services.css`

Styles `services.html`: page reset and the add-service button container. Table and edit dialog styles come from `explore/table.css` and `edit/Edit*.css`.

| Selector(s) | Description |
|---|---|
| `:root` | Defines `--MaxW: 100ch` (unused here). |
| `*` | `box-sizing: border-box`. |
| `body` | No padding/margin, full width. |
| `ss-add` | Full width, centred text, `margin: auto`; declares `display: hide`, which is not a valid value and is dropped, so the element keeps its initial display. |
| `ss-add.Show` | `display: block`; class added by `servicesMain` when the plus icon is inserted. |
| `ss-table` | Not styled here (table container filled by `Table.addTable`). |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--MaxW` | `100ch` on `:root` | Declared only. |

### `server.css`

Styles `server.html` and `server_metrics.html`: square chart tiles with click overlays, the stat pills and the command pills, plus the stacked three-chart layout of `body.Dual`. All elements are created by `server.js`.

#### Page base

| Selector(s) | Description |
|---|---|
| `:root` | Empty rule. |
| `*` | `box-sizing: border-box`. |
| `html` | Sets `--Margin: 0`. |
| `body` | Defines `--pad`, `--mw`, `--w`, `--h` (see below), `padding: var(--pad)`, no margin. |

#### Charts

| Selector(s) | Description |
|---|---|
| `server-charts` | Wrapping flex row, `space-evenly`, centred. Created by `serverMain` / `serverMetricsMain`. |
| `server-item` | Relative `--w` × `--h` inline-flex tile, rounded (`--ThemeRad`). Created by `addChart`. |
| `server-item iframe` | Absolutely positioned chart iframe, `--w` × `--h`, no border, `pointer-events: none` so clicks reach the overlay. |
| `server-item server-itemclick`, `server-itemclick:hover`, `server-itemclick:focus` | Full-size transparent click overlay (pointer cursor, rounded); hover shows a `--ThemeAcc1RGB` glow, focus a `--ThemeAcc2` outline. |

#### Metrics page (`body.Dual`)

| Selector(s) | Description |
|---|---|
| `body.Dual server-charts` | `display: block`; defines `--pad2`, `--mh`, `--mw`, `--mmh`, `--h`, `--h2` from viewport size (`svh`/`svw`). Class `Dual` is added by `serverMetricsMain`. |
| `body.Dual server-item:first-child`, `... iframe` | First chart: full width (`--mw`) and 20 % height (`--h`), no background. |
| `body.Dual server-item`, `... iframe` | Other charts: full width, height `--h2` (half of the remaining height), `--ThemeBackground`, block, top margin `--pad2`. |

#### Stats and commands

| Selector(s) | Description |
|---|---|
| `server-stats` | Wrapping flex row with a faint bottom border. |
| `server-stat`, `server-stat.Click:hover`, `server-stat:focus` | Rounded pill with `--ThemeMain25` border; `Click` (set by `addStats` when a link exists) gets a `--ThemeMain` border and pointer on hover; focus outline `--ThemeAcc1`. |
| `server-text` | Label next to the icon: 24 px line height, left padding. |
| `server-commands` | Wrapping flex row with a faint top border, `--ThemeAcc2` text. |
| `server-command server-text` | 32 px line height, 150 % font size. |
| `server-command`, `server-command:hover`, `server-command:focus` | Larger pill with `--ThemeAcc225` border, pointer; hover `--ThemeAcc2` border; focus `--ThemeAcc1` outline. |
| `server-command.Disabled` | Opacity 0.25, default cursor, no outline; set by `addStats` while an async command runs. |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--Margin` | `0` on `html` | Overrides a theme/application margin variable. |
| `--pad` | `8px` on `body` | Body padding, hover glow size. |
| `--mw` | `calc(50svw - var(--pad))`; in `Dual`: `calc(100svw - var(--pad2))` | Max tile width (two per row) / full width in Dual. |
| `--w` | `min(var(--mw), 400px)` | Tile width. |
| `--h` | `calc(var(--w) + 2em)`; in `Dual`: `min(var(--mmh), var(--mw))` | Tile height / first chart height in Dual. |
| `--pad2` | `calc(var(--pad) * 2)` (Dual) | Chart spacing. |
| `--mh` | `calc(100svh - var(--pad2) * 4)` (Dual) | Available height. |
| `--mmh` | `calc(var(--mh) * 0.2)` (Dual) | 20 % of available height. |
| `--h2` | `calc((var(--mh) - var(--h)) / 2)` (Dual) | Height of the 2nd and 3rd chart. |

### `serviceInfo.css`

Styles `serviceInfo.html`: a centred column (`--MaxW`) over a fixed shadow panel, the state pills, the control/debug button rows, the two chart tiles, the upload buttons, the config-file grids, the versions grid and the `si-icon-*` button icons. Static placeholders come from the HTML; grid cells are created by `serviceInfo.js`.

#### Page base

| Selector(s) | Description |
|---|---|
| `:root` | Defines `--MaxW: 100ch` (column width). |
| `*` | `box-sizing: border-box`. |
| `body` | No margin, full width, 20em bottom padding. |
| `body.ViewOnly` | `pointer-events: none`, no bottom padding (class from URL param `v`). |
| `si-bg` | Fixed, horizontally centred panel `--MaxW + 2em` wide with a `--ThemeMain25` shadow, behind content (`z-index: -1`). |
| `si-name`, `body.TopWindow si-name` | Bold, centred `--ThemeAcc2` header bar; hidden unless `body.TopWindow` (added when `window.IsTop`). |

#### State pills

| Selector(s) | Description |
|---|---|
| `si-stats` | Centred wrapping flex row (max `--MaxW`). |
| `si-state` | Bold rounded pill, `--ThemeMain` background, `--ThemeBackground` text. |
| `si-state.Ok`, `si-state.Warning`, `si-state.Error` | Background `--ThemeOk` / `--ThemeWarning` / `--ThemeError`; set on `si-state[0]` from `stateMap` in `update`. |
| `si-state.Hide` | `display: none`; set on `si-state[1..4]` when `ProcId === 0`. |
| `si-state.KeyClick`, `si-state.KeyClick:hover`, `si-state.KeyClick:focus` | Clickable state (CPU/memory; `keyboardClick` adds `KeyClick`): pointer, `--ThemeBackground` border on hover, `--ThemeAcc1` outline on focus. |

#### Buttons

| Selector(s) | Description |
|---|---|
| `si-servicebuttons`, `si-debugbuttons` | Centred wrapping flex rows (max `--MaxW`) holding `Button` elements. |
| `si-servicebuttons sysweaver-button` | 8 px margins (right/top/bottom) between control buttons. |
| `.si-icon-restart`, `.si-icon-pause`, `.si-icon-resume`, `.si-icon-stop`, `.si-icon-start`, `.si-icon-disable`, `.si-icon-explore`, `.si-icon-log`, `.si-icon-kill` | Mask images for the `Button` icon classes: `../icons/reload.svg`, `preview_pause.svg`, `preview_play.svg`, `preview_stop.svg`, `preview_play.svg`, `close.svg`, `explore.svg`, `notes.svg`, `skull.svg` respectively. |

#### Graphs

| Selector(s) | Description |
|---|---|
| `si-graphs` | Block container (max `--MaxW`). |
| `si-graph`, `si-graph:focus`, `si-graph:hover` | Half-width tile (`calc(50% - 1.5em)`, aspect 38/40), rounded, pointer; focus `--ThemeAcc1` outline, hover `--ThemeAcc250` border. |
| `si-graph iframe` | Fills the tile, no border, `pointer-events: none` (clicks go to the `si-graph`). |
| `si-text` | 24 px centred block; not created by the ServerManager HTML/JS. |

#### Uploads

| Selector(s) | Description |
|---|---|
| `si-uploads`, `si-uploads.Show` | 64×64 px upload drop target, hidden until `Show` (added in `update` when configs exist). |
| `si-uploads:focus` | `--ThemeAcc1` outline. |
| `si-uploads:after`, `si-uploads:hover:after` | Mask icon `../icons/upload.svg` in `--ThemeMain`, opacity 0.8 / 1 on hover. |
| `si-uploads.Dragging`, `si-uploads.Selecting:after`, `si-uploads.Uploading:after` | Dashed border while dragging; `disc.svg` while selecting; `Loading.svg` while uploading (fileUploader.js states). |

#### Config file grid

| Selector(s) | Description |
|---|---|
| `si-configs`, `si-configs.Show` | 5-column grid (`28px 28px auto min-content min-content`), bordered, horizontally scrollable; hidden until `Show`. |
| `si-file-header` | Header row spanning all 5 columns, uppercase, `--ThemeMain` background. |
| `si-file-state`, `si-file-state.Show` | Column 1: `fav_on.svg` star mask in `--ThemeAcc1`, shown only with `Show` (file is not a backup). |
| `si-file-icon`, `si-file-icon:hover`, `si-file-icon:focus` | Column 2: 28×24 file-type icon (background image set inline), pointer, lifts 2 px on hover, focus outline. Opens the file menu. |
| `si-file-name`, `si-file-name:focus`, `si-file-name:hover` | Column 3: link-like name in `--ThemeAcc2`, inverted colours on hover, focus outline. |
| `si-file-size` | Column 4: right-aligned monospace size in `--ThemeAcc1`. |
| `si-file-time` | Column 5: right-aligned monospace timestamp. |

#### Versions grid

| Selector(s) | Description |
|---|---|
| `si-versions` | 6-column grid (`28px auto auto auto max-content auto`), bordered, horizontally scrollable. |
| `si-version-header` | Header spanning 6 columns, uppercase, `--ThemeMain` background. |
| `si-version-active`, `si-version-active.Active` | Column 1: star mask (`fav_on.svg`, `--ThemeAcc1`) only when `Active` (version `IsActive`). |
| `si-version-uploaded`, `si-version-uploaded:focus`, `si-version-uploaded:hover` | Column 2: clickable monospace timestamp (opens `versionInfo.html`), inverted on hover. |
| `si-version-user`, `si-version-machine`, `si-version-comment`, `si-version-last` | Columns 3–6: user (`--ThemeAcc1`), machine, comment (`--ThemeAcc1`), right-aligned monospace last-used time. |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--MaxW` | `100ch` on `:root` | Width of the content column and the `si-bg` panel. |

#### Media queries

| Query | Effect |
|---|---|
| `screen and (max-width: 600px)` | `si-name` square corners; `si-graphs` no side padding; `si-configs` and `si-versions` full-bleed (no side margins, radius or side borders). |
| `(max-aspect-ratio: 2/3)` (portrait) | `si-graph` full width, no side margins or radius. |

### `versionInfo.css`

Styles `versionInfo.html`: the same base, `si-bg` panel and `si-name` / `TopWindow` header as `serviceInfo.css` (copied), plus the key/value grids and the action button row.

| Selector(s) | Description |
|---|---|
| `:root` | Defines `--MaxW: 100ch`. |
| `*` | `box-sizing: border-box`. |
| `body` | No padding/margin, full width. |
| `si-bg` | Fixed centred shadow panel `--MaxW + 2em` wide, behind content. |
| `si-name`, `body.TopWindow si-name` | `--ThemeAcc2` title bar, hidden unless `body.TopWindow` (set by `versionInfoMain` when `window.IsTop`). |
| `si-values` | Grid (`auto auto 100%`) of key/value rows, max `--MaxW`, centred. Two instances: version values and action output. |
| `si-value-name` | Column 1: key in `--ThemeMain`, no wrap. Created by `AddString` / `AddTime`. |
| `si-value-value` | Column 2: value in `--ThemeAcc1`, no wrap. |
| `si-actions` | Centred wrapping flex row (max `--MaxW`) for the action buttons. |
| `si-actions sysweaver-button` | 8 px margins between action buttons. |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--MaxW` | `100ch` on `:root` | Width of `si-bg`, `si-values`, `si-actions`. |

#### Media queries

| Query | Effect |
|---|---|
| `screen and (max-width: 600px)` | `si-name` square corners; `si-values` no side margins, radius or side borders. |
