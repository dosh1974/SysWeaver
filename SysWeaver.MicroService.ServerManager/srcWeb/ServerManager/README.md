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
