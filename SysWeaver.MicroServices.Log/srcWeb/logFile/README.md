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

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function.

### `logfile.js`

Page script for `logfile.html`, loaded with a static `<script defer>` after `../common/common.js` and `../app/application.js`. It defines a single global function, `logFileMain`, which the page calls from `<body onload="logFileMain()">`. Nothing runs at load time apart from that function declaration.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `logFileMain` | function (global) | The page entry point (see below). No other globals are defined. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `logFileMain` | async `logFileMain()` → `Promise<void>` | Reads the optional URL param `api` (`getUrlParams().get("api")`). If `sendRequest("../Api/HaveService", "SysWeaver.MicroService.TypeService")` is truthy, it redirects with `location.replace` to `../edit/text.html?r=<api>&scrollToEnd=true`, or, without `api`, to `../edit/text.html?r=../Api/logFile/LogFile.txt&d=../Api/logFile/DeleteLogFile&scrollToEnd=true`, and returns. Otherwise it appends a `<pre>` to `document.body` and starts the inner `updateText()`. It then fetches `../Api/auth/GetUser`. The user counts as admin only when `api` is absent and `user.Succeeded` is set and `user.Tokens` contains `"admin"` or `"debug"`. It appends a `Button.CreateRow()` row (a `SysWeaver-CenterBlock` element) holding a **Download** `Button` (`IconDownload`). Its click handler calls `StartWorking()`, takes the file name from the last path segment of `api` or from `getRequest("../Api/logFile/DownloadName")`, calls `downloadFile(name, url)`, reports errors with `Fail`, and calls `StopWorking()`. For admins it also adds a **Delete** `Button` (`IconDelete`). That handler asks with `Confirm("Delete log", …, "Yes, delete!", "No, keep it!", "IconDelete")`, then calls `getRequest("../Api/logFile/DeleteLogFile")`. On a truthy result it shows `Info("Log file deleted from disc!")` and calls `updateText()` again; otherwise it calls `Fail`. Finally the function awaits the first text load and calls `PageLoaded()`. Any exception calls `PageLoaded()` and then `Fail(e)`. |

#### Inner functions of `logFileMain`

| Function | Signature | Description |
|---|---|---|
| `updateText` | inner async `updateText()` → `Promise<void>` | Sets the `<pre>`'s `innerText` to the text from `getRequest(api ?? "../Api/logFile/LogFile.txt", false, false, null, async e => e.text())`, then scrolls the window to `document.body.scrollHeight` (the end of the log). |

## CSS reference

### `logfile.css`

Styles `logfile.html`, the fallback log viewer used when the Edit project's text viewer is not available. It is loaded by a static `<link>` after `../common/theme.css` and `../app/application.css`, and depends on the theme variables `--ThemeBackground`, `--ThemeFont`, `--ThemeMain` and `--ThemeFontMono`. It is not templated. (`logfile.html` also has an inline `<style>` that makes `html, body` transparent, but this file's `body` rule sets the theme background again.)

#### Page layout

| Selector(s) | Description |
|---|---|
| `body` | No margin, theme background (`--ThemeBackground`), theme font (`--ThemeFont`), centered text, `user-select: none`. |
| `SysWeaver-CenterBlock` | The button row, created by `Button.CreateRow()` in common.js and appended by `logFileMain`. It is fixed at the top-left, full width minus 2em, with 0.25em/1em padding, a drop shadow (`rgba(0,0,0,0.4) 0 4px 4px`) and the theme background, so it stays visible over the scrolling text. |
| `pre` | The log text element created by `logFileMain`. It uses the `--ThemeMain` color and the `--ThemeFontMono` font at 100% size, left-aligned and selectable (`user-select: text`), with 128px top and bottom padding to keep the text clear of the fixed bar. |

#### Icons

| Selector(s) | Description |
|---|---|
| `.IconDownload`, `.IconDelete` | Mask icons for the Download and Delete `Button`s created in `logFileMain`: `../icons/download.svg` and `../icons/trash.svg`. The icon color and shape come from the shared button/icon CSS in application.css and common.js. |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--ThemeBackground` | theme.css | `body` and `SysWeaver-CenterBlock` background |
| `--ThemeFont` | theme.css | `body` font |
| `--ThemeMain` | theme.css | `pre` text color |
| `--ThemeFontMono` | theme.css | `pre` font |

#### Animations, media queries and theme variants

There are none in this file.
