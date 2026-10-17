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

## Behavior (web analysis)

### Loading and initialization

- This project contributes the file to the shared `common/` URL folder. At runtime it sits next to `common/common.js`, `md5.js` and `UZIP.js` from SysWeaver.HttpServer, even though it is built and embedded by SysWeaver.MicroService.FileUploader. When that service is not hosted, `common/fileUploader.js` does not exist.
- It is a plain classic script with no side effects at load. It only declares globals; nothing runs until a consumer calls `fileUploaderSetup`, `fileUploaderInit`, `fileUploader` or `selectFiles`.
- How pages include it: statically with `<script defer src="../common/fileUploader.js">` (Example.html, ServerManager `keys.html` and `serviceInfo.html`), or lazily with `includeJs(current, "../common/fileUploader.js")` (`app/home.js`) or `includeJs(null, "fileUploader.js", true)` (`chat.js`, where the missing file is tolerated and checked with `typeof fileUploader`).
- It is not a template; there is no `${...}` substitution.

### Files

**`fileUploader.js`**

- `UploadStatus`: a const object that mirrors the C# `FileUploadStatus` enum: `MultipleFilesNotAllowed -12`, `UnknownRepo -11`, `InvalidFileName -10`, `OperationInProgress -9`, `InvalidFile -8`, `DiscQuotaExceeded -7`, `NotAuthorized -6`, `InvalidParams -5`, `RefuseExtension -4`, `RefuseSize -3`, `Refuse -2`, `UploadFailed -1`, `None 0` (= uploaded OK), `AlreadyUploaded 1`, `Upload 2` (= may be uploaded).
- `fileUploaderStatusText(code)`: returns the translated human-readable text through `_TF` / `_T`.
- `async fileUploader(repo, files, apiBase = "../upload/")`: the core protocol (see Server interaction). It returns `{Status: number[], Urls: (string|null)[], Redirected: url|null}` or `{Error}`. The arrays are indexed like `files`.
- `async fileUploaderElement(element, files)`: the element-level wrapper. It sets `element.IsUploading`, the CSS class `Uploading` and a temporary `title`. It reads `data-repo`, calls the `data-onstart` global function (looked up by name with `GetFunction`) and/or `element.OnUploadStart`, runs `fileUploader(repo, files, data-apibase)`, then calls `data-onresults` and/or `element.OnUploadResults(element, res, files, repo)`. All state is restored in `finally`.
- Event handlers: `fileUploaderDrop` (collects `dataTransfer.items` of kind `file`, or `dataTransfer.files`, and keeps only one file unless `data-multiple` is `true` or `1`), `fileUploaderDrag` (`preventDefault` on dragover), `fileUploaderDragEnter` / `fileUploaderDragLeave` (toggle the `Dragging` class), and `fileUploaderClick` (ignores the event when `badClick(ev)` is true; creates a hidden `<input type=file [multiple]>`, sets the `Selecting` class while the dialog is open, and uploads on `change`).
- `async selectFiles(allowMultiple)`: a standalone file picker. It awaits `change` or `cancel` through `waitEvent2` and returns a `FileList` or `null`. Used by chat.
- `fileUploaderSetup(el, repo, onstart, onresults, apiBase, allowMultiple)`: attaches the five listeners and saves the configuration as `data-*` attributes plus the expandos `OnUploadStart` and `OnUploadResults`.
- `fileUploaderInit(tagName = "sysweaver-drop")`: calls `fileUploaderSetup` on every element with that tag. The configuration comes entirely from `data-repo`, `data-onstart`, `data-onresults`, `data-multiple` and `data-apibase`. No in-repo page calls it.
- CSS contract: the script creates no DOM of its own except the temporary file input. It only toggles the classes `Dragging`, `Selecting` and `Uploading`, and consumers style those (Example.html `div`, `kf-upload` in keys.css, `si-uploads` in serviceInfo.css).

### Server interaction

Handled by `FileUploaderService` (`[WebApiUrl("../upload")]`, so it is served at `upload/...`):

1. **Hash.** For each file, the whole content is read into memory (`File.arrayBuffer()`) and hashed with **MD5** through `YaMD5.hashByteArray` (from `common/md5.js`; the result is a lower-case hex string). The page builds `FileUploadInfo` objects `{Name, Length, Hash, LastModified (ms since epoch)}`.
2. **Status check.** A single `sendRequest(apiBase + "CheckStatus", {Repo, Files: [...]}, true)` (JSON POST, the `true` is the `reload` flag) returns `FileUploadResult[]` `{Result, Url, Chunks}`, one per file. The server answers `UnknownRepo` or `NotAuthorized` for every file when the repo is missing or the session lacks the repo's auth. Otherwise the repo decides each file: `AlreadyUploaded` with `Url` (deduplication on hash), `Upload`, or a refusal code.
3. **Upload.** Only files whose status is `2` (`Upload`) are sent. Each one is a `fetch` POST to `apiBase + "Upload?repo=..&name=..&length=..&hash=..&time=.."`, with the values concatenated without URL encoding, and body `application/octet-stream`. When `UZIP` exists and `isCompressibleExt(getFileExtension(name))` is true, the body is `UZIP.deflateRaw(level 8)` with `Content-Encoding: deflate`, but only if that makes it smaller. All uploads run in parallel (`Promise.all`). The server routes the request through `FileUploaderService.Handler` (POST with a `repo` query parameter, to the per-repo `UploadHandler`) and answers JSON `{Result, Url}`. A non-200 response or a redirect (for example to a login page) sets the status to `-1`, and the redirect URL is reported in `Redirected`.
4. **Not used by the JS client:** the server supports optional content-defined chunking (`FileUploadInfo.Chunks` and `FileUploadResult.Chunks`, and a chunk-list upload stream in `StaticFileRepo`). This script never sends `Chunks`, so uploads are always whole files. The JSON property names (`Repo`, `Files`, `Name`, `Length`, `Hash`, `LastModified`, `Result`, `Url`) are an implicit contract with the C# DTOs.
- The `Url` values are relative to the server root. Consumers prefix them with `../`.

### State and persistence

- No storage or caching. Per-element state is the expando `IsUploading` (which blocks re-entry for both click and drop) plus the CSS classes. File buffers are dropped (`data[i] = null`) once queued.
- Logging goes to `console.log` for each file checked and uploaded.

### Dependencies

- `common/common.js`: `sendRequest`, `_T`, `_TF`, `GetFunction`, `badClick`, `waitEvent2`, `getFileExtension`, `isCompressibleExt`.
- `common/md5.js`: `YaMD5`, required.
- `common/UZIP.js`: `UZIP`. The code is written as optional (`!!UZIP`), but because it reads the bare global, the name must be declared; every in-repo static includer loads `UZIP.js`.
- Consumers: `upload/Example.html`, `app/home.js` (the user image, repo `UserImage`), `chat/chat.js` (attachments; uses `fileUploader` and `selectFiles` directly), ServerManager `keys.js` (repo `Keys`) and `serviceInfo.js` (repos `Current_<service>` and `Master_<service>`).

### Structural observations

- Everything is global functions and a global `const UploadStatus`. It is not a module and has no namespace.
- The status codes are written out in two places, `UploadStatus` and the `switch` in `fileUploaderStatusText`, and both must match the C# enum by hand.
- The two `Request` constructions in `doOne` are nearly identical and differ only in body and header. The `data-multiple` parsing is repeated in `fileUploaderDrop` and `fileUploaderClick`, and `selectFiles` repeats the hidden-input logic.
- Callbacks can be configured two ways at once: global function names in `data-on*` attributes (resolved with `GetFunction`) and the expando functions `OnUploadStart` / `OnUploadResults`. Both are called if both are set.
- Consumers all copy the same result-handling pattern (the `UploadCompleted` functions in keys.js and serviceInfo.js and the handlers in home.js and Example.html). The library offers no shared helper for it.
- Every file is read fully into memory before the status check, so the client has no streaming or chunking.

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function. All symbols are classic-script globals.

### `fileUploader.js`

Client side of the file upload protocol of `FileUploaderService`. Loaded as a classic script (`<script defer src="../common/fileUploader.js">` or `includeJs`) after `common.js`, `md5.js` and (optionally) `UZIP.js`. Defines the global `UploadStatus` constant and the global functions below; nothing executes at load time.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `UploadStatus` | const object | Named status codes returned by the server (mirror of the C# `FileUploadStatus` enum): `MultipleFilesNotAllowed` -12, `UnknownRepo` -11, `InvalidFileName` -10, `OperationInProgress` -9, `InvalidFile` -8, `DiscQuotaExceeded` -7, `NotAuthorized` -6, `InvalidParams` -5, `RefuseExtension` -4, `RefuseSize` -3, `Refuse` -2, `UploadFailed` -1, `None` 0 (uploaded OK), `AlreadyUploaded` 1, `Upload` 2 (may be uploaded). |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `fileUploaderStatusText` | `(code: number) → string` | Returns a translated (`_TF`) human-readable text for a status code -12..2; for any other value returns `_T("Unknown upload status [{0}]", code)`. |
| `fileUploader` | async `(repo: string, files: File[]/FileList, apiBase?: string = "../upload/") → Promise<{Status: number[], Urls: (string\|null)[], Redirected: string\|null} \| {Error}>` | Reads every file into an `ArrayBuffer`, MD5-hashes it with `YaMD5.hashByteArray` and builds `{Name, Length, Hash, LastModified}` infos. Posts them once via `sendRequest(apiBase + "CheckStatus", {Repo, Files}, true)`; a falsy reply returns `{Error: Error("Couldn't check status!")}`. Seeds `Status`/`Urls` from the reply (`Result`, `Url`), then uploads (in parallel, `Promise.all`) only the files whose status is `2`, via inner `doOne`. Logs each check/upload to `console.log`. Any exception yields `{Error: e}`. Arrays are indexed like `files`. |
| `fileUploaderElement` | async `(element: HTMLElement, files: File[]/FileList) → Promise<void>` | Element-level upload wrapper (used internally by drop/click). Sets `element.IsUploading = true`, adds class `Uploading` and a temporary translated `title`. Reads `data-repo`; calls the global function named by `data-onstart` (via `GetFunction`) and awaits `element.OnUploadStart(element, files, repo)` if set; runs `fileUploader(repo, files, data-apibase)`; then calls the `data-onresults` function and awaits `element.OnUploadResults(element, res, files, repo)`. Restores title/class/`IsUploading` in `finally`. |
| `fileUploaderDrop` | async `(ev: DragEvent)` | `drop` handler: `preventDefault`, removes `Dragging` (via `fileUploaderDragLeave`), ignores the drop while `IsUploading`. Collects files from `dataTransfer.items` (kind `"file"`) or `dataTransfer.files` (via inner `onFile`), truncates to one file unless `data-multiple` is `"true"`/`"1"`, then calls `fileUploaderElement(currentTarget, files)`. |
| `fileUploaderDrag` | async `(ev: DragEvent)` | `dragover` handler; only calls `preventDefault()` so the element accepts drops. |
| `fileUploaderDragEnter` | async `(ev: DragEvent)` | `dragenter` handler; adds CSS class `Dragging` to `ev.currentTarget`. |
| `fileUploaderDragLeave` | async `(ev: DragEvent)` | `dragleave` handler; removes CSS class `Dragging` from `ev.currentTarget`. |
| `selectFiles` | async `(allowMultiple: bool) → Promise<FileList\|null>` | Creates a detached `<input type=file>` (with `multiple` when requested), clicks it (must be called from a user gesture), awaits `change` or `cancel` via `waitEvent2`, and returns the selected `FileList` or `null` if none. |
| `fileUploaderClick` | async `(ev: MouseEvent)` | `click` handler: returns if `badClick(ev)` or the element `IsUploading`. Creates a detached `<input type=file>` (multiple per `data-multiple`), adds class `Selecting` while the dialog is open (removed on `change`/`cancel`), and on `change` with ≥1 file calls `fileUploaderElement(element, files)`. |
| `fileUploaderSetup` | `(el: HTMLElement, repo?: string, onstart?: fn(el, files, repo), onresults?: fn(el, res, files, repo), apiBase?: string, allowMultiple?: bool)` | Makes an element an upload target: adds `click`, `drop`, `dragover`, `dragenter`, `dragleave` listeners (the handlers above); stores `apiBase` → `data-apibase`, `repo` → `data-repo`, `allowMultiple` → `data-multiple="true"`, and the callbacks as expandos `el.OnUploadStart` / `el.OnUploadResults`. Falsy arguments leave existing attributes untouched. |
| `fileUploaderInit` | `(tagName?: string = "sysweaver-drop")` | Calls `fileUploaderSetup(el)` (no arguments, configuration from `data-*` attributes) for every element returned by `document.getElementsByTagName(tagName)`, iterating backwards. |

#### Inner functions of `fileUploader`

| Function | Signature | Description |
|---|---|---|
| `doOne` | async `(info: {Name, Length, Hash, LastModified}, buffer: ArrayBuffer, index: number) → Promise<void>` | Uploads one file: builds a `Request` POST (`mode: "cors"`, `application/octet-stream`) to `apiBase + "Upload?repo=&name=&length=&hash=&time="` (values not URL-encoded). If `UZIP` exists and `isCompressibleExt(getFileExtension(Name))`, the body is `UZIP.deflateRaw(level 8)` with `Content-Encoding: deflate` when that is smaller than the original. Writes `stats[index]`/`urls[index]` from the JSON reply `{Result, Url}`; non-200, redirect (also sets the closure `redirect` URL) or exception set status `-1`. Logs to console. |

#### Inner functions of `fileUploaderDrop`

| Function | Signature | Description |
|---|---|---|
| `onFile` | `(file: File, index: number)` | Pushes `file` onto the local `files` array (`index` is unused). |
