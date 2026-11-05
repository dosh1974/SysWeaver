# logFile

[⬆ srcWeb](../README.md)

> The log file viewer page shown under *Debug / Log file*.

| | |
|---|---|
| **Project** | [SysWeaver.MicroServices.Log](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

`FileLogService` adds `logFile/logfile.html` to the menu (tokens `debug`, `ops` or `admin`). ServerManager `serviceInfo.html` opens it with `?api=<text url>` to view a config file.

## Contents

| Item | Description |
|---|---|
| [`logfile.html`](logfile.html) | The page (new format: `sw/core.js`, `sw/ui.js`), `<body onload="logFileMain()">` |
| [`logfile.js`](logfile.js) | `logFileMain()`, `logFileViewer(api)`, constants `LogFileUrl`, `LogFileDeleteUrl`, `LogFileTokenUrl` |
| [`logfile.css`](logfile.css) | `.lf-card` (the text card filling the page), `.lf-text` (selectable monospace text) |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroServices.Log](../../README.md)
- **Uses:** Sw core / ui of SysWeaver.HttpServer; `edit/text.html` of SysWeaver.MicroService.Edit when hosted.
- **Server API:** `Api/HaveService`, `Api/logFile/LogFile.txt`, `Api/logFile/DownloadName`, `Api/logFile/GetDeleteLogFileToken`, `Api/logFile/DeleteLogFile`, `Api/auth/GetUser`.

## Behavior

1. If `Api/HaveService "SysWeaver.MicroService.TypeService"` is true the page is a redirect: `Sw.Page.Replace` (location.replace, no history entry, no "loaded" signal, so the app shell keeps the menu url and Back skips this page) to `../edit/text.html?r=<api>&scrollToEnd=true`, or `../edit/text.html?r=../Api/logFile/LogFile.txt&d=../Api/logFile/DeleteLogFile&scrollToEnd=true` (the original url, no request before it, titled after the file; the text viewer recognizes the log by its lines (`LooksLikeLog`) and shows it in its log mode `sw_log` with the level colors).
2. Otherwise the fallback viewer (Design v2 fill page: compact page header "Log file" (the menu name inside the shell) or "Text file" with `api`, the text in a card that fills the height): the text of `api` (or the log file) in a monospace block, scrolled to the end, and the header actions: Download (name = last segment of `api`, or `Api/logFile/DownloadName`), Reload, and Delete (only for the log file and users who may call `DeleteLogFile`; with the one time token of `Api/logFile/GetDeleteLogFileToken` the shared `Sw.Cells.ConfirmDanger` shows the file, its size and last write, the server's warning and a type to confirm field ("delete"), then posts `{Token}` to `DeleteLogFile`; an older server without the token api: the plain danger confirm and `DeleteLogFile`; info toast, reload).

## JavaScript reference

| Name | Description |
|---|---|
| `logFileMain()` | Entry: redirect or fallback viewer (manages its own loading phase) |
| `logFileViewer(api)` | Builds the fallback viewer |
| `LogFileUrl`, `LogFileDeleteUrl` | Api urls |

## CSS reference

Design v2 fill page (`main.sw-page-fill` from `Sw.PageLayout`, compact page header with the actions, ui/page.css); `.lf-card` (the card that takes the rest of the height), `.lf-text` (pre-wrap monospace, selectable, focusable for keyboard scrolling, scrolls inside the card). No shadows.
