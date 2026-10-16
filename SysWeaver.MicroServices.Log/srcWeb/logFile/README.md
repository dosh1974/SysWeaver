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
