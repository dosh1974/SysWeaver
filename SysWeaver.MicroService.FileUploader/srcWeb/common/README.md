# common

[⬆ srcWeb](../README.md)

> The reusable browser upload script, served as `common/fileUploader.js` alongside the shared scripts of the HTTP server.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.FileUploader](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Pages that accept files include this script (after `common/common.js`, `common/md5.js` and optionally `common/UZIP.js` from [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md)). For each file it computes an MD5 hash, asks `upload/CheckStatus` whether the file is accepted, already uploaded or refused, and then posts only the files that need uploading to `upload/Upload?repo=...`, deflate compressed (`Content-Encoding: deflate`) when `UZIP` is available.

## Contents

| Item | Description |
|---|---|
| [`fileUploader.js`](fileUploader.js) | `UploadStatus` codes and `fileUploaderStatusText`, `fileUploader(repo, files, apiBase)` (status check and upload), `selectFiles` (file picker), drag-and-drop handlers, `fileUploaderSetup(element, repo, ...)` and `fileUploaderInit(tagName)` (wires up all `sysweaver-drop` elements via `data-repo`, `data-multiple`, `data-apibase`, ... attributes and sets the `Dragging` / `Selecting` / `Uploading` CSS classes) |

## Relationships

- **Part of:** [srcWeb](../README.md), [SysWeaver.MicroService.FileUploader](../../README.md)
- **Talks to:** [`FileUploaderService`](../../FileUploaderService.cs) (`upload/CheckStatus`, `upload/Upload`)
- **Used by:** [`upload/Example.html`](../upload/README.md), [SysWeaver.Chat](../../../SysWeaver.Chat/README.md) (`chat.js`), [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md) (`app/home.js`), [SysWeaver.MicroService.ServerManager](../../../SysWeaver.MicroService.ServerManager/README.md) (`keys`, `serviceInfo` pages)
