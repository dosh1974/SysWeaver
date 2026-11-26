# ServerManager

[⬆ srcWeb](../README.md)

> The browser pages of the server manager: host overview and metrics, managed services with their versions, key files, and the card templates of its tables.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.ServerManager](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

`ServerManagerService` adds three of these pages to the menu: *Server* (`server.html`, Admin / Ops), *Managed services* (`services.html`) and *Key files* (`keys.html`, Admin only). The other pages are opened from links: the `TableData` urls of `SmServiceBrief` (`*../ServerManager/serviceInfo.html?p={0}`), `SmDriveInfo` / `SmProcessMetrics` (`*../ServerManager/server_metrics.html?q1=..&q2=..&q3=..&title=..`), and `serviceInfo.html` opens `versionInfo.html`. `cards.html` is not a page: it holds the card templates of the server manager tables.

All pages use the new front end format (Sw core + ui, no `common/common.js` / `app/application.js`) and are built from the shared ui components (`Sw.PageLayout`, `Sw.Facts`, `Sw.Kpi`, `Sw.Card`, `Sw.Grid`, `Sw.StateView`, see "Page anatomy" in the [ui README](../../../SysWeaver.HttpServer/srcWeb/ui/README.md)), so the page css files only hold a few sizes.

## Contents

| Item | Description |
|---|---|
| [`server.html`](server.html) / [`server.js`](server.js) / [`server.css`](server.css) | Server dashboard: facts, KPI tiles, Deleted files / Reboot (`serverMain()`); `server.css` also sizes the chart cards of `serviceInfo.html` and `server_metrics.html` |
| [`server_metrics.html`](server_metrics.html) | Three charts of one metric (`serverMetricsMain()` in `server.js`), url parameters `q1`, `q2`, `q3`, `title` |
| [`services.html`](services.html) / [`services.js`](services.js) / [`services.css`](services.css) | Managed services table and the "Add service" dialog (`servicesMain()`) |
| [`serviceInfo.html`](serviceInfo.html) / [`serviceInfo.js`](serviceInfo.js) / [`serviceInfo.css`](serviceInfo.css) | One managed service: status, control verbs, executable, charts, files, config files with uploads, versions (`serviceInfoMain()`), url parameters `p`, `v` |
| [`versionInfo.html`](versionInfo.html) / [`versionInfo.js`](versionInfo.js) / [`versionInfo.css`](versionInfo.css) | One uploaded version: values and actions (`versionInfoMain()`), url parameter `p` |
| [`keys.html`](keys.html) / [`keys.js`](keys.js) / [`keys.css`](keys.css) | Key files: "Upload key files" button (repo `Keys`) and table (`keysMain()`) |
| [`smShared.js`](smShared.js) / [`smShared.css`](smShared.css) | Helpers shared by the pages (`SmShared`: chart tiles, metric urls, list reconciler, upload buttons, fill tables) |
| [`cards.html`](cards.html) / [`cards.css`](cards.css) | Card templates of the tables (see [Card templates](#card-templates)) |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.ServerManager](../../README.md)
- **Uses:** `sw/core.js`, `sw/ui.js`, `sw/ui.css`, `common/theme.css` of [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md); `explore/table.js` (`Sw.Table`) of [SysWeaver.HttpServer.ExploreModule](../../../SysWeaver.HttpServer.ExploreModule/README.md); `edit/Edit.js` (`Sw.Edit`, `Sw.Edit.Options`) of [SysWeaver.MicroService.Edit](../../../SysWeaver.MicroService.Edit/README.md); `common/fileUploader.js` (`fileUploaderSetup` only) of [SysWeaver.MicroService.FileUploader](../../../SysWeaver.MicroService.FileUploader/README.md); `chart/chart.html` of [SysWeaver.MicroService.ChartJs](../../../SysWeaver.MicroService.ChartJs/README.md) (iframes, one way via url parameters).

## Behavior

### Page protocol

Each page loads `../sw/core.js`, `../sw/ui.js`, (libraries), `smShared.js`, the page script, and calls its global `*Main()` from `<body onload>`. The entry runs inside `Sw.Page.Main(fn, { Loading: true })`: the loader shows until the first data is rendered, then the app shell reveals the frame. All in-shell navigation uses `Sw.Page.Navigate(url, "_self")`. Server apis are page relative (`GetDetail` = `ServerManager/GetDetail`).

### Pages

- **server.html**: page header "Server" with the actions "Deleted files" (opens `../ServerManager/Bak/explore`, the backups of deleted and replaced files) and "Reboot..." (danger outline). Facts strip: Host, Operating system, Processors and Memory (open the metric page), Processes (opens `../explore/table.html?q=../ServerManager/ProcessInfoTable`), Server time (a live clock, `SmServerClock`, synchronized using half the round trip; a click copies it). KPI tiles with meters: running services (opens `services.html`), CPU, memory and one per drive (open the metric page). The tiles are fed by the chart apis `GetServicesChart`, `GetCpuChart`, `GetMemChart` (every 2 s) and `GetDriveChart?<i>` (every 10 s), at the `RefreshRate` of the reply (`smPollChart`); `GetServerStats` every 5 s updates the process count and the clock. `SmShared.UseServerTimeZone(info)` makes the history charts show their UTC time labels in the server's time zone. Reboot: the shared danger confirmation (`Sw.Cells.ConfirmDanger`: "Reboot HOST?", the number of running processes, "Reboot HOST"), then `RebootComputer?"<token>"` with the `RebootToken` of `GetServerInfo` (the page's own reply while it is younger than 10 minutes, else a new one): the server refuses a reboot without a valid token, so a plain link can't reboot.
- **server_metrics.html**: `q1` (current value), `q2` (short history), `q3` (long history) chart apis (relative to ServerManager/, may carry `?<arg>`), `title` = document title (untrusted url text: `Sw.Page.SetLinkTitle`). Three chart cards (now, recent, history); a chart missing from the link is left out, only a link without any of the three shows the "Missing chart parameters" error. A click opens the chart full page in `../chart/chart.html`.
- **services.html**: a fill page with the live table `../ServerManager/ServicesTable` (`SmShared.FillTable`; the row link and the Remove action are defined by C#). "Add service" (primary action, shown for every signed in user: `GET ../Api/auth/GetUser` with tokens; the api requires Admin and refuses others) opens a `Sw.Dialog` with a `Sw.Edit` object editor of `SysWeaver.MicroService.ManagedService` (type info from `edit/GetTypeInfo`), then `AddService`; the live table shows the new service with its next refresh.
- **serviceInfo.html?p=svc[&v=1]**: page header with the service name, the status badge (`SmStateKinds`: ok / warning / error) and the seven control verbs Restart, Pause, Resume (`Continue`), Stop, Start, Disable (`Uninstall`), Kill (always shown, the ones that don't apply to the current state are disabled with the reason; `IsSm` services only allow Restart; Kill is a danger outline with a confirmation). Facts: Process ID, Executable (a click opens `ExeMenu`: "Automatic" or one of the executables, `SelectExe`), Total CPU time. CPU / memory KPI tiles and the `GetServiceCpuChart` / `GetServiceMemChart` chart cards (open the metric page). A Files card: Explore (`../FolderSync/ManagedFolders/<svc>/explore`), View log (`../edit/text.html?scrollToEnd=true&r=..&d=../ServerManager/DeleteLogFile`), Deleted files (`../ServerManager/ServiceBak/<svc>/explore`). Active / Master configuration cards: row lists with an upload button in the card head (repos `Current_<svc>` and `Master_<svc>`), a "Backup" badge (`smIsConfigBackup`, same rules as C# `ServiceHost.NewIsConfigBackupName`), the name opens `../edit/text.html?r=..&u=../ServerManager/UpdateConfig&d=../ServerManager/DeleteConfigFile`, and a row menu: View, Activate (backups), Use as current (master), Copy file name / local path / local folder, Download, Delete (the shared danger confirmation). A Versions card ("Active" badge; the upload time opens `versionInfo.html?p=<svc>,<Uploaded>,<Name>`). `v` = view only: no buttons, menus, uploads or links, `body.ViewOnly`. `GetDetail` every 5 s; actions poll immediately; the lists are reconciled without flicker (`SmShared.ChildUpdater`).
- **versionInfo.html?p=key**: the title from the url key is url text (`Sw.Page.SetLinkTitle`) until `GetVersion` answers. Page header with the version, an "Active" badge and the actions Activate, Touch, Verify, Expand, Compress, Delete (danger outline, confirmed, then `history.back()`); enabled only for an inactive version (Verify / Expand when compressed, Compress when not compressed and `IsCompressedService`), one action at a time. The values are a facts strip (a value copies on click); a Result card shows the Touch / Verify statistics. The key is refreshed after an action (activation renames the folder). `GetVersion` every 5 s.
- **keys.html**: a fill page with the action "Upload key files" (`SmShared.UploadButton`: click or drop, repo `Keys`) and the live table `../ServerManager/KeysTable`, refreshed after a successful upload.

### Card templates

The row types of the server manager tables point at the templates in `cards.html` with `[TableDataCard]` (format: "Card templates" in the [explore README](../../../SysWeaver.HttpServer.ExploreModule/srcWeb/explore/README.md#card-templates-card)): `drive` and `drive-mobile` (`SmDriveInfo`, `DriveInfoTable`, cards by default), `service` (`SmServiceBrief`, `ServicesTable`, table by default), `process` (`SmProcessMetrics`, `ProcessInfoTable`, table by default), `key` (`SmKeyFile`, `KeysTable`, cards by default) and `storage` (`SmChunkFolderStats`, `StorageStatsTable`, cards by default). `cards.css` sets the drive card width and the style of the used value.

### Input and layout

Every clickable thing is a `button` (keyboard, touch, focus ring); chart tiles are an iframe (out of the tab order, `aria-hidden`) under a transparent button; the embedded chart shows its normal loader, which never steals the focus from this page. Menus use `Sw.Menu`. Colors come from the Sw tokens, so every theme works.

### Not allowed (403)

A refused request (403) shows the shared "not allowed" state of `Sw.Access` instead of a blank page (ex: server.html for users without Admin / Ops). The upload buttons (`SmShared.UploadTarget(el, repo, onUploaded, guard)`) are guarded by `Sw.Access.Guard`; without a `guard` (keys.html, serviceInfo.html) a button is disabled after the first refused request of the page.

## JavaScript reference

### smShared.js (global `SmShared`)

`ReportUpload`, `UploadTarget`, `UploadButton` and `FillTable` are aliases of the shared components `Sw.ReportUpload`, `Sw.UploadTarget`, `Sw.UploadButton` and `Sw.FillTable` (the local versions only run on an older framework without them); the aliases keep the classes `sm-dropbtn` / `sm-fill`.

| Member | Description |
|---|---|
| `ChartUrl(api, extra)` | `../chart/chart.html?q=<encoded ../ServerManager/api>&timeoffset=<tz><extra>` (time labels in the zone set by `UseServerTimeZone`, default the viewer's local time) |
| `UseServerTimeZone(info)` | Charts created afterwards show their UTC time labels in the server's time zone, computed from a `GetServerInfo` reply; returns the offset (minutes, or "local" when the info is unusable) |
| `MetricsUrl(api, title)` | `server_metrics.html?q1=..&q2=..&q3=..&title=..` derived from a `Get<X>Chart[?arg]` api |
| `ChartTile({Api, Extra, Title, OnClick, Class})` | div.sm-chart: chart iframe (+ a click button when `OnClick`) |
| `ChildUpdater(target)` | `{Add(el, init), Complete()}`: keeps equal children (by outerHTML), `init(node)` only for inserted nodes |
| `ReportUpload(res, files, onUploaded)` | One toast for an upload result |
| `UploadTarget(el, repo, onUploaded, guard)` | `fileUploaderSetup(el, repo, ...)` with ReportUpload and `Sw.Access.Guard`; disables the button without the upload library |
| `UploadButton({Text, Title, Repo, OnUploaded, Guard, Small})` | A `Sw.Button` made an upload target (class `sm-dropbtn`) |
| `FillTable(page, url)` | A live `Sw.Table` in page mode inside a fill page (`.sm-fill`); returns the table |
| `KpiSub(kpi, text)` | The context line of a KPI tile, after a "High" / "Critical" badge when the meter is above its warning / danger level |
| `TimeCell(el, iso, prefix)` | Short time text, full time tool tip, copy on click |
| `Lines(...lines)` | Join translated lines with line breaks (translated literals may not contain line breaks) |
| `DangerTitle(verb, target, fallback)` | Title of a destructive confirmation naming the target (`Sw.Dialog.DangerTitle`, else the fallback) |
| `BaseName(path)` | The last part of a file or folder path |

### server.js

`serverMain()`, `serverMetricsMain()` (entries), `smReboot(host, processes, info, infoAt)` (the Reboot command), `smPollChart(api, onData)` (polls a chart api the way the chart page does), `smPercent(v)`, `smUsedFree(chart)`, class `SmServerClock` (`Sync(time, sentAt)`, `Now()`, `Shown()`, `Draw()`, `Start()`).

### services.js

`servicesMain()`, `smEditNewService()` (the add dialog, resolves to the object or null).

### serviceInfo.js

`serviceInfoMain()`, `smIsConfigBackup(name)`, `SmStateKinds` (status: ok / warning / error), class `SmServiceInfo`: `MetricsUrl(kind)`, `OpenMetrics("Cpu" | "Mem")`, `Title()`, `Build()`, `BuildServiceButtons()`, `BuildDebugButtons()`, `DoVerb(verb, okText)`, `UpdateButtons()`, `ExeMenu(anchor)`, `ConfigMenu(anchor, file, isMaster)`, `UpdateFiles(...)`, `AddUpload(...)`, `Update(first)`, `UpdateVersions(list)`, `Poll()`, `Loop()`, `WakeUp()`.

### versionInfo.js

`versionInfoMain()`, class `SmVersionInfo`: `Key`, `Build()`, `ClearOutput()`, `SetTitle()`, `BuildActions()`, `UpdateButtons()`, `DoWork(action)`, `Stats(api)`, `Delete()`, `ShowStats(stats)`, `Render()`, `Poll()`, `Loop()`, static `FillValues(target, rows)`.

### keys.js

`keysMain()`.

## CSS reference

| File | Classes |
|---|---|
| `smShared.css` | `.sm-chart`, `.sm-chart-click` (chart tiles), `.sm-upload`, `.sm-upload-row`, `.sm-upload-labeled` + `.sm-upload-text` (local upload buttons; `Dragging`, `Selecting`, `Uploading` from fileUploader.js), `.sm-dropbtn` (upload `Sw.Button`), `.sm-fill` (fill page table) |
| `server.css` | `.sm-linecard` (chart cards, also on serviceInfo.html), `.sm-metric`, `.sm-metric-now`, `.sm-metric-hist` (server_metrics.html) |
| `services.css` | `.ss-edit` (the "Add new service" dialog) |
| `serviceInfo.css` | `body.ViewOnly`, the rows `li.si-row` of the file and version lists: `.si-file-icon`, `.si-link`, `.si-file-name`, `.si-file-size`, `.si-file-time`, `.si-file-menu`, `.si-version-uploaded`, `.si-version-text`, `.si-version-comment`, `.si-version-last` |
| `versionInfo.css` | `.vi-page` (long values wrap) |
| `keys.css` | Nothing page specific (header comment only) |
| `cards.css` | `--sw-card-min` of the drive cards, `.sm-card-used` |
