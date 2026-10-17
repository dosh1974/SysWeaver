# logFile

[⬆ srcWeb](../README.md)

> The log file viewer page shown under *Debug / Log file*.

| | |
|---|---|
| **Project** | [SysWeaver.MicroServices.Log](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

`FileLogService` adds `logFile/logfile.html` to the menu (tokens `debug`, `ops` or `admin`). When the `TypeService` of [SysWeaver.MicroService.Edit](../../../SysWeaver.MicroService.Edit/README.md) is registered, the page redirects to the generic text viewer `edit/text.html` with the log file and delete APIs. Otherwise it shows the log text (`../Api/logFile/LogFile.txt`) itself, scrolled to the end, with a *Download* button and, for admin/debug users, a *Delete* button. An optional `api` URL parameter shows another text resource instead of the log file.

## Contents

| Item | Description |
|---|---|
| [`logfile.html`](logfile.html) | The page (`logFileMain()`) |
| [`logfile.js`](logfile.js) | Viewer logic: redirect to the text editor, or show, download and delete the log |
| [`logfile.css`](logfile.css) | Styles for the fallback viewer |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroServices.Log](../../README.md)
- **Uses:** shared web assets of [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md), optionally `edit/text.html` from [SysWeaver.MicroService.Edit](../../../SysWeaver.MicroService.Edit/README.md)

## Behavior (web analysis)

### Loading and initialization

`logfile.html` loads `../common/common.js`, `../app/application.js` and `logfile.js` (all `defer`) plus theme, app and local CSS, and calls `logFileMain()` on body load. The page is registered by `[WebMenuEmbedded(null, "Debug/LogFile", "Log file", "logFile/logfile.html", ..., "IconTableLog", 0, "debug,ops,admin")]` on `FileLogService` (`[WebApiUrl("logFile")]`), so it is shown embedded in the app shell.

### Files

- **`logfile.js`**: one global async function, `logFileMain()`.
  1. It reads the optional URL param `api` (another text resource to show).
  2. It calls `sendRequest("../Api/HaveService", "SysWeaver.MicroService.TypeService")`. If that returns true, it does `location.replace` to the generic text viewer: `../edit/text.html?r=<api>&scrollToEnd=true`, or `?r=../Api/logFile/LogFile.txt&d=../Api/logFile/DeleteLogFile&scrollToEnd=true` (`r` = read URL, `d` = delete URL). It then returns.
  3. Otherwise it renders its own fallback viewer:
     - a `<pre>` filled with `getRequest(url ?? "../Api/logFile/LogFile.txt", ..., e => e.text())` and then scrolled to the end (`updateText()`);
     - in parallel, `../Api/auth/GetUser`. The delete button is shown only when there is no `api` param and the user has the `admin` or `debug` token;
     - a `Button.CreateRow()` with **Download** (file name from `../Api/logFile/DownloadName`, or the last URL segment; `downloadFile(name, url)`) and **Delete** (`Confirm` dialog → `../Api/logFile/DeleteLogFile`, then `Info` and a reload of the text). Buttons use `StartWorking`/`StopWorking`.
  4. `PageLoaded()` is called after the text has loaded, or on error; errors go to `Fail`.
- **`logfile.html`**: shell page, empty body.
- **`logfile.css`**: `body` theme background and font; a fixed top `SysWeaver-CenterBlock` (the button row); `pre` in the monospace theme font with 128px top/bottom padding (room for the fixed bar); `.IconDownload` and `.IconDelete` masks from `../icons/`.

### Server interaction

- `GET ../Api/logFile/LogFile.txt`: raw text, `Roles.AdminOps`, client cache 5 s, request cache 4 s.
- `../Api/logFile/DownloadName`: string, static cache.
- `../Api/logFile/DeleteLogFile`: bool, `Roles.Admin`, audited.
- `../Api/HaveService`: an HttpServer-level check for a registered service type.
- `../Api/auth/GetUser`: `{Succeeded, Tokens[]}`.

Polling, push and templating are not used.

### State and persistence

None. It uses the URL param `api` only.

### Dependencies

- common.js: `getUrlParams`, `sendRequest`, `getRequest`, `Button`, `downloadFile`, `Confirm`, `Info`, `Fail`, `PageLoaded`.
- The app shell CSS and JS.
- Optionally the Edit project's `edit/text.html`, whose URL contract (`r`, `d`, `scrollToEnd`) this page relies on.

### Structural observations

- The page is mostly a redirector: the full feature set lives in the Edit project's text viewer, and the local viewer is a minimal fallback that duplicates download and delete.
- The availability check is done at runtime by service type name (`"SysWeaver.MicroService.TypeService"`), a string contract with the server.
- The client-side admin/debug token check only hides the button; the server enforces `Roles.Admin`.
