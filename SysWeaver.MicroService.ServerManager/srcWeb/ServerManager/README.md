# ServerManager
[⬆ srcWeb](../README.md)

> The browser pages of the server manager: host overview and metrics, managed services with their versions, and key files.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.ServerManager](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

`ServerManagerService` adds three of these pages to the menu: *Server* (`server.html`), *Managed services* (`services.html`) and *Key files* (`keys.html`, Admin only). The other pages are opened from links: the `TableData` urls of `SmServiceBrief` (`*../ServerManager/serviceInfo.html?p={0}`), `SmDriveInfo` / `SmProcessMetrics` (`*../ServerManager/server_metrics.html?q1=..&q2=..&q3=..&title=..`), and `serviceInfo.html` opens `versionInfo.html`.

All pages use the new front end format (Sw core + ui, no `common/common.js` / `app/application.js`).

## Contents

| Item | Description |
|---|---|
| [`server.html`](server.html) / [`server.js`](server.js) / [`server.css`](server.css) | Server overview: stats, live server clock, charts, short history, Reboot / Deleted files commands (`serverMain()`) |
| [`server_metrics.html`](server_metrics.html) | Three charts of one metric (`serverMetricsMain()` in `server.js`), url parameters `q1`, `q2`, `q3`, `title` |
| [`services.html`](services.html) / [`services.js`](services.js) / [`services.css`](services.css) | Managed services table and the "Add service" dialog (`servicesMain()`) |
| [`serviceInfo.html`](serviceInfo.html) / [`serviceInfo.js`](serviceInfo.js) / [`serviceInfo.css`](serviceInfo.css) | One managed service: state, charts, control / debug buttons, config files with uploads, versions (`serviceInfoMain()`), url parameters `p`, `v` |
| [`versionInfo.html`](versionInfo.html) / [`versionInfo.js`](versionInfo.js) / [`versionInfo.css`](versionInfo.css) | One uploaded version: values and actions (`versionInfoMain()`), url parameter `p` |
| [`keys.html`](keys.html) / [`keys.js`](keys.js) / [`keys.css`](keys.css) | Key files: "Upload key files" button (icon and word, repo `Keys`) and table (`keysMain()`) |
| [`smShared.js`](smShared.js) / [`smShared.css`](smShared.css) | Helpers shared by the pages (`SmShared`: chart tiles, metric urls, list reconciler, upload targets) |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.ServerManager](../../README.md)
- **Uses:** `sw/core.js`, `sw/ui.js`, `sw/ui.css`, `common/theme.css` of [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md); `explore/table.js` (`Table.addTable`) of [SysWeaver.HttpServer.ExploreModule](../../../SysWeaver.HttpServer.ExploreModule/README.md); `edit/Edit.js` (`Edit`, `EditOptions`) of [SysWeaver.MicroService.Edit](../../../SysWeaver.MicroService.Edit/README.md); `common/fileUploader.js` (`fileUploaderSetup` only) of [SysWeaver.MicroService.FileUploader](../../../SysWeaver.MicroService.FileUploader/README.md); `chart/chart.html` of SysWeaver.MicroService.ChartJs (iframes, one way via url parameters).

## Behavior

### Page protocol

Each page loads `../sw/core.js`, `../sw/ui.js`, (libraries), `smShared.js`, the page script, and calls its global `*Main()` from `<body onload>`. The entry runs inside `Sw.Page.Main(fn, { Loading: true })`: the loader shows until the first data is rendered, then `Sw.Page.Loaded()` tells the app shell to reveal the frame. Polling pages (serviceInfo, versionInfo) start their 5 s loop after that. All in-shell navigation uses `Sw.Page.Navigate(url, "_self")` (IframeNavigating protocol, shell history). Server apis are page relative (`GetDetail` = `ServerManager/GetDetail`).

### Pages

- **server.html**: `GetServerInfo` (half the round trip corrects the server clock), stat pills (machine, OS `os_<OsBase>` icon, CPU count ("24 CPUs") and memory ("63.35 GB memory", 2 decimals like the server's "Memory use ... of 63.35 GB" donut caption) open the metric page, live clock ("01:04:33 server time") with a date / time zone tool tip, click copies `yyyy-MM-dd HH:mm:ss`, process count ("363 processes") opens `../explore/table.html?q=../ServerManager/ProcessInfoTable`; the muted words after the values are `.sm-stat-label`), chart tiles `GetServicesChart` (opens services.html; while the server manages no services a compact "No managed services" placeholder button is shown instead of an empty "0 / 0" chart, re-checked every 10 s), `GetCpuChart`, `GetMemChart`, `GetDriveChart?<i>` per drive (open `server_metrics.html`); the tiles share the width (`--sm-count`, one row on wide screens, two per row on phones). On screens of at least 900x900 px the short history charts `GetCpuHistoryShortChart` / `GetMemHistoryShortChart` fill the rest of the window (click opens the metric page). Commands: "Deleted files" (folder icon, tool tip explains it browses the backups of deleted / replaced files: `../ServerManager/Bak/explore`) at the start, and set apart at the far end a dashed danger group "Server power" with Reboot (danger button; the shared danger confirmation names the server: "Reboot HOME-SERVER?", the running process count, "Cancel" (focused) / "Reboot HOME-SERVER"; then a fresh `GetServerInfo` for its `RebootToken` and `RebootComputer?"<token>"`: the server refuses a reboot without a valid token, so a plain link can't reboot). `GetServerStats` every 5 s re-syncs the clock and the process count. The server sends the history chart time labels in UTC; `SmShared.UseServerTimeZone(info)` makes every chart show them in the server's time zone, so the chart axes and the "server time" clock agree.
- **server_metrics.html**: `q1` (current value), `q2` (short history), `q3` (long history) chart apis (relative to ServerManager/, may carry `?<arg>`; links from C# are not url encoded, links from these pages are; both decode the same), `title` = document title (untrusted url text: `Sw.Page.SetLinkTitle`, plain, at most 80 characters, shown as plain text in the shell, no caption). The page sends only the chart requests of the original page (no `GetServerInfo`); the time labels are in the viewer's local time. Three stacked charts (the first 20 % of the height); a chart missing from the link is left out (as the original page, which showed the others), only a link without any of the three shows the "Missing chart parameters" error; a click opens the chart full page in `../chart/chart.html` (`&center=true` / `&aspect=false&noLabels=true`).
- **services.html**: `Table.addTable("../ServerManager/ServicesTable", <ss-table>)` (row link / Remove action defined by C#). "Add service" (for every signed in user, as the original page; the api requires Roles.Admin and refuses others) opens a `Sw.Dialog` with an `Edit` object editor of `SysWeaver.MicroService.ManagedService` (type info from `edit/GetTypeInfo`), then `AddService`; the live table shows the new service with its next refresh.
- **serviceInfo.html?p=svc[&v=1]**: title "Service - <p>" is url text (`Sw.Page.SetLinkTitle`, plain, limited text in the shell, no caption) until `GetDetail` answers, then a normal title; state pills (status coloured ok / warning / error, Id / Cpu / Mem / Tot hidden without a process; Cpu / Mem open the metric page), cpu / memory chart tiles, control buttons Restart, Pause, Resume (`Continue`), Stop, Start, Disable (`Uninstall`), Kill (the shared danger confirmation: "Kill the process of <svc>?", "Cancel" / "Kill <svc>") enabled per state (`IsSm` services only allow Restart), debug buttons Explore (`../FolderSync/ManagedFolders/<svc>/explore`), View log (`../edit/text.html?scrollToEnd=true&r=..&d=../ServerManager/DeleteLogFile`), Deleted (`../ServerManager/ServiceBak/<svc>/explore`). Config grids (active / master): star = not a backup (`smIsConfigBackup`, same rules as C# `ServiceHost.NewIsConfigBackupName`), file type icon, name (opens `../edit/text.html?r=..&u=../ServerManager/UpdateConfig&d=../ServerManager/DeleteConfigFile`), size / time (copy on click), a menu button (also long press / right click / context menu key on the row): View (`../logFile/logfile.html?api=..`), Activate (backups), Use as current (master), Copy file name / local path / local folder, Download, Delete (the shared danger confirmation naming the file, "Cancel" / "Delete"). Upload buttons for repos `Current_<svc>` and `Master_<svc>` (click, Enter / Space, drop; multiple files). Versions grid: active star, uploaded (opens `versionInfo.html?p=<svc>,<Uploaded>,<Name>`), user, machine, comment, last used (copy on click). `v` = view only: no buttons, menus, uploads or links, `body.ViewOnly`. The title bar shows only in a top window (`body.TopWindow`). `GetDetail` every 5 s; actions poll immediately; the grids are reconciled without flicker.
- **versionInfo.html?p=key**: title from the url key is url text (`SetTitle(true)`: `Sw.Page.SetLinkTitle`) until `GetVersion` answers, then a normal title; values (Uploaded, Active, Folder, User, Machine, Comment, Last used, Compressed, Files, Size, Path; copy on click), actions Activate (tool tip lists the steps), Touch / Verify (show chunk statistics), Expand, Compress, Delete (the shared danger confirmation naming the folder, "Cancel" / "Delete version", then `history.back()`); enabled only for an inactive version (Verify / Expand when compressed, Compress when not compressed and `IsCompressedService`), one action at a time; the key is refreshed after an action (activation renames the folder). `GetVersion` every 5 s (paused during actions).
- **keys.html**: "Upload key files" button (`.sm-upload-labeled`: icon and word in a dashed outline; click or drop, repo `Keys`) and `Table.addTable("../ServerManager/KeysTable", <kf-table>)`, refreshed after a successful upload. Upload results: error / first failing file: fail toast; already uploaded / uploaded: info toast.

### Design v2 page anatomy
Every page is built from the shared ui components (ui/page.js, ui/README.md "Page anatomy"): `Sw.PageLayout` (page container + `Sw.PageHeader`; inside the shell the h1 is the menu name), `Sw.Facts`, `Sw.Kpi`, `Sw.Card`, `Sw.ListCard`, `Sw.Grid`, `Sw.StateView`; no page specific pills, donuts, fieldsets, boxed panels, shadows or gradients.
- **server.html**: header (Deleted files, Reboot... danger outline), facts strip, KPI meter tiles (running services, CPU, memory, drives) fed by the chart apis of the original donuts at their rate (`smPollChart`: `GetServicesChart`, `GetCpuChart`, `GetMemChart` every 2 s, `GetDriveChart?<i>` every 10 s, the `RefreshRate` of the reply), `GetServerStats` every 5 s: the same requests as the original page. The running services tile opens services.html, the others the metric page. Reboot uses the `RebootToken` of the page's `GetServerInfo` (a new one only when the page is older than 10 minutes).
- **services.html**, **keys.html**: fill pages (`SmShared.FillTable`: the live table takes the free height, its refresh line moves under the page description); actions in the header ("Add service", primary, for every signed in user (`GET ../Api/auth/GetUser` with tokens), as the original page; the server refuses it without Admin; "Upload key files", `SmShared.UploadButton`, also a drop target).
- **serviceInfo.html**: header with the status badge and the seven control verbs (always shown, the ones that don't apply to the current state disabled with the reason, as the original; Kill... is a danger outline with a confirmation), facts (Process ID, Executable ("(auto)" when detected; a click opens the selection menu), Total CPU time), CPU / memory KPI tiles (no `GetServerInfo`: the memory tile shows the working set), the `GetServiceCpuChart` / `GetServiceMemChart` chart tiles of the original page (a click opens the metric page), a Files card (folders, log; Explore / View log / Deleted files), Active / Master configuration cards (row lists with an Upload button in the card head, a row menu, a "Backup" badge) and a Versions card ("Active" badge; the upload time opens versionInfo.html).
- **versionInfo.html**: header (version title, "Active" badge, Activate / Touch / Verify / Expand / Compress / Delete), the version values as a facts strip (a value copies on click), a Result card with the Touch / Verify statistics.
- **server_metrics.html**: header with the title from the link (`Sw.Page.SetLinkTitle`), three chart cards (now, recent, history; the histories as lines).

### Input and layout

Every clickable thing is a `button` (keyboard, touch, focus ring); chart tiles are an iframe (out of the tab order, `aria-hidden`) under a transparent button; the embedded chart shows its normal loader, which never steals the focus from this page (the Sw overlays only take the focus in a frame that has it). Menus use `Sw.Menu` (44 px items on touch). Desktop, phone portrait (one chart column on serviceInfo, wrapping pills) and landscape (chart tiles limited by the viewport height). Colors come from the Sw tokens, so every theme works.

## JavaScript reference

### smShared.js (global `SmShared`)
Design v2 helpers: `KpiSub(kpi, text)` (the meter level as a "High" / "Critical" badge), `UploadButton({Text, Title, Repo, OnUploaded, Guard, Small})` (Sw.Button + `UploadTarget`, class `sm-dropbtn`), `FillTable(page, url)` (a `Sw.Table` in page mode inside a fill page, `.sm-fill`).

| Member | Description |
|---|---|
| `ChartUrl(api, extra)` | `../chart/chart.html?q=<encoded ../ServerManager/api>&timeoffset=<tz><extra>` (time labels in the zone set by `UseServerTimeZone`, default the viewer's local time). The chart page polls only that api (the server smooths the cpu value itself), so the requests are those of the original chart tiles |
| `UseServerTimeZone(info)` | Charts created afterwards show their UTC time labels (history charts) in the server's time zone, computed from a `GetServerInfo` reply (`Time` - `Utc`, whole quarter hours); returns the offset (minutes, or "local" when the info is unusable) |
| `MetricsUrl(api, title)` | `server_metrics.html?q1=..&q2=..&q3=..&title=..` derived from a `Get<X>Chart[?arg]` api |
| `ChartTile({Api, Extra, Title, OnClick, Class})` | div.sm-chart: chart iframe (tabindex -1, titled with the chart page title, its canvas names the values) (+ a click button when `OnClick`, named "Show details of <chart title>") |
| `ChildUpdater(target)` | `{Add(el, init), Complete()}`: keeps equal children (by outerHTML), `init(node)` only for inserted nodes |
| `ReportUpload(res, files, onUploaded)` | One toast for an upload result |
| `UploadTarget(el, repo, onUploaded)` | `fileUploaderSetup(el, repo, ...)` with ReportUpload; disables the button without the upload library |
| `TimeCell(el, iso, prefix)` | Short time text, full time tool tip, copy on click |
| `Lines(...lines)` | Join translated lines with line breaks (translated literals may not contain line breaks) |

### server.js

`serverMain()`, `serverMetricsMain()` (entries), `smStat(icon, text, title, action, label)` (stat pill; a button when it has an action; `label` = muted unit / meaning after the value), `smReboot(host, processes)` (Reboot command: the shared danger confirmation `Sw.Cells.ConfirmDanger`, "Reboot HOST?" naming the server and the running process count, buttons "Cancel" (focused) and "Reboot HOST"), class `SmServerClock` (`Sync(time, sentAt)`, `Now()`, `Draw()`, `Start()`).

### services.js

`servicesMain()`, `smEditNewService()` (the add dialog, resolves to the object or null), `SmLines(...)`.

### serviceInfo.js

`serviceInfoMain()`, `smIsConfigBackup(name)`, `SmStateKinds` (status: ok / warning / error), class `SmServiceInfo`: `Build()`, `BuildServiceButtons()`, `BuildDebugButtons()`, `DoVerb(verb, okText)`, `UpdateButtons()`, `ExeMenu(anchor)` (the executable selection: "Automatic" or one of `SmServiceDetail.Executables`, `SelectExe`), `ConfigMenu(anchor, file, isMaster)`, `UpdateFiles(grid, files, header, tip, isMaster)`, `AddUpload(row, repo, title)`, `Update(first)`, `UpdateVersions(list)`, `Poll()`, `Loop()`, `WakeUp()`, `OpenMetrics("Cpu" | "Mem")`.

### versionInfo.js

`versionInfoMain()`, class `SmVersionInfo`: `Key`, `SetTitle()`, `BuildActions()`, `UpdateButtons()`, `DoWork(action)`, `Stats(api)`, `Delete()`, `ShowStats(stats)`, `Render()`, `Poll()`, `Loop()`, static `FillValues(dl, rows)`.

### keys.js

`keysMain()`.

## CSS reference

| File | Classes |
|---|---|
| `smShared.css` | `.sm-chart`, `.sm-chart-click` (chart tiles; `.sm-hot`: the hover frame, only after a real mouse move over the tile, so a resting pointer leaves no frame after a menu / theme change), `.sm-upload` (+ `Dragging`, `Selecting`, `Uploading` from fileUploader.js), `.sm-upload-icon`, `.sm-upload-row`, `.sm-upload-labeled` + `.sm-upload-text` (upload button with a visible word) |
| `server.css` | `.sm-server`, `.sm-stats`, `.sm-stat` (`button.sm-stat` clickable, `[aria-busy]`), `.sm-stat-icon`, `.sm-stat-text`, `.sm-stat-label`, `.sm-charts` (tile size `--w`, `--h`), `.sm-commands` (flex: commands left, `.sm-danger` group with `.sm-danger-label` right), `.sm-metrics` (body.Dual, stacked charts `--h`, `--h2`) |
| `services.css` | `ss-add` (`.Show`), `ss-table`, `.ss-edit` |
| `serviceInfo.css` | `--MaxW`, `.si-page`, `.si-name` (body.TopWindow), body.ViewOnly, `.si-stats`, `.si-state` (`data-kind` ok / warning / error), `.si-buttons`, `.si-graphs`, `.si-graph`, `.si-grid`, `.si-configs`, `.si-versions`, `.si-row` (display contents), `.si-grid-header`, `.si-file-state` / `.si-version-active` (`.si-show` star), `.si-file-type`, `.si-file-icon`, `.si-link`, `.si-file-name`, `.si-file-size`, `.si-file-time`, `.si-file-menu`, `.si-version-*`, `.si-upload-row` |
| `versionInfo.css` | `--MaxW`, `.vi-page`, `.vi-name` (body.TopWindow), `.vi-values` (dt / dd), `.vi-output`, `.vi-actions` |
| `keys.css` | `kf-table` |

## Not allowed (403)

- server.html: users without Admin / Ops (ex: dev, whose menu has Server) get the shared "not allowed" state ("This page requires the role Admin or Ops", Home link) through `Sw.Page.Main` instead of a blank page.
- keys.html and the service upload buttons: `SmShared.UploadTarget(el, repo, onUploaded, guard)` guards the upload target (`Sw.Access.Guard`); keys.js passes `{ Url: "../ServerManager/KeysTable" }`, so Upload key files is disabled with "Requires Admin" for other roles, and the key table shows the "not allowed" state (no Refresh / Download).
