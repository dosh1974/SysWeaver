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
- `keysMain()`: `fileUploaderSetup(<kf-upload>, "Keys", noop, UploadCompleted, null, true)` (the repo `Keys` is a `BackupFileRepo` over the key folder), then `Table.addTable("../ServerManager/KeysTable", <kf-table>)`. `UploadCompleted` has the same logic as in serviceInfo. On success it calls `updateNow()`, which is not declared in keys.js; in serviceInfo.js that name is local to `serviceInfoMain`.
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
