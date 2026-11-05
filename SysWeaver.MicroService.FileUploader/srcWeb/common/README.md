# common

[⬆ srcWeb](../README.md)

> The reusable browser upload library, served as `common/fileUploader.js` alongside the shared scripts of the HTTP server.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.FileUploader](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Client of the upload protocol of [`FileUploaderService`](../../FileUploaderService.cs): for each file it computes an MD5 hash, asks `upload/CheckStatus` whether the file is accepted, already uploaded or refused, then posts only the files that need uploading to `upload/Upload?repo=...`, raw-deflate compressed when that helps. It also turns any element into an upload target (click / keyboard picker and drag and drop).

New API: `Sw.Upload` (needs the Sw core; the visual helpers use the Sw ui library when present). The old global functions are kept as thin wrappers for existing callers.

## Contents

| Item | Description |
|---|---|
| [`fileUploader.js`](fileUploader.js) | `Sw.Upload` (`Status`, `StatusText`, `Ensure`, `Files`, `Pick`, `Target`, `ForElement`, `Handlers`, `DefaultApiBase`) and the legacy globals `UploadStatus`, `fileUploaderStatusText`, `fileUploader`, `fileUploaderElement`, `fileUploaderDrop`, `fileUploaderDrag`, `fileUploaderDragEnter`, `fileUploaderDragLeave`, `selectFiles`, `fileUploaderClick`, `fileUploaderSetup`, `fileUploaderInit` |

## Relationships

- **Part of:** [srcWeb](../README.md), [SysWeaver.MicroService.FileUploader](../../README.md)
- **Talks to:** [`FileUploaderService`](../../FileUploaderService.cs) (`upload/CheckStatus`, `upload/Upload`)
- **Uses:** the Sw core (`Sw.Net`, `Sw.Util`, `Sw.Format`), optionally the Sw ui (`Sw.KeyClick`, `Sw.Hint`), `common/md5.js` (YaMD5, loaded on demand), optionally `common/UZIP.js` (only when the browser has no CompressionStream)
- **Used by:** [`upload/Example.html`](../upload/README.md), [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md) `app/home.js` (`Sw.Upload`), [SysWeaver.Chat](../../../SysWeaver.Chat/README.md) `chat.js` (`fileUploader`, `selectFiles`), [SysWeaver.MicroService.ServerManager](../../../SysWeaver.MicroService.ServerManager/README.md) `keys` / `serviceInfo` pages (`fileUploaderSetup`)

## Behavior

### Loading
Classic script without side effects at load. Load it after the core (`sw/core.js`, or the legacy `common/common.js` which contains the core): statically (`<script defer src="../common/fileUploader.js">`) or with `Sw.Net.IncludeJs(null, "fileUploader.js")`. `md5.js` no longer has to be loaded by the page (`Sw.Upload.Ensure()` / the first upload load it); pages that still load `md5.js` / `UZIP.js` statically keep working.

### Upload (`Sw.Upload.Files(repo, files, {ApiBase, OnProgress})`)
1. Every file is read fully and hashed with `YaMD5.hashByteArray` -> `FileUploadInfo {Name, Length, Hash, LastModified}`.
2. One `Sw.Net.Send(apiBase + "CheckStatus", {Repo, Files}, {Post: true, Reload: true})` returns `FileUploadResult[] {Result, Url}` (a falsy answer gives `{Error}`).
3. Files with status `Upload (2)` are POSTed in parallel to `apiBase + "Upload?repo=&name=&length=&hash=&time="` (values URL-encoded) as `application/octet-stream`; compressible extensions (`Sw.Format.IsCompressibleExt`) are raw-deflated (`Sw.Util.Deflate`, or `UZIP.deflateRaw` level 8 without CompressionStream) and sent with `Content-Encoding: deflate` when smaller.
4. A non-200 answer or an exception gives status -1; a redirected upload (ex: to the sign in page) gives -1 and `Redirected`.
5. Result `{Status[], Urls[], Redirected}` indexed like the files (urls are server root relative: prefix `../`), or `{Error}`.
`OnProgress({Phase, Done, Count})` reports the phases `hash`, `check`, `upload`, `done` (no per byte progress; the server's chunking is not used).

### Upload targets (`Sw.Upload.Target(el, opts)`)
Options `Repo`, `Multiple`, `ApiBase`, `Accept`, `Title`, `OnStart(el, files, repo)`, `OnResults(el, result, files, repo)`, `NoKeyboard`; stored as `data-repo`, `data-multiple="true"`, `data-apibase`, `data-accept` and the expandos `OnUploadStart` / `OnUploadResults` (so `data-*` configured elements work too, plus the global callbacks named by `data-onstart` / `data-onresults`).
- Click (no modifier keys) opens the file picker (`multiple` / `accept` as configured); class `Selecting` while it is open.
- Non-button elements become keyboard operable: `tabindex=0`, `role=button`, Enter / Space open the picker (`Sw.KeyClick`); the title also shows on long press and focus (`Sw.Hint`).
- Dragging files over the element adds `Dragging` (nested enter / leave counted, only for file drags); dropping uploads the files (only the first unless multiple).
- While uploading: class `Uploading`, `aria-busy="true"`, a temporary title, `IsUploading` blocks new clicks and drops.
- Returns `{Remove()}`; calling it again for the same element only updates the options.

### Legacy globals
`fileUploaderSetup(el, repo, onstart, onresults, apiBase, allowMultiple)` = `Sw.Upload.Target`; `fileUploaderInit(tag = "sysweaver-drop")` sets up all such elements; `fileUploader(repo, files, apiBase)` = `Sw.Upload.Files`; `selectFiles(multiple)` = `Sw.Upload.Pick`; `fileUploaderElement`, `fileUploaderClick`, `fileUploaderDrop`, `fileUploaderDrag`, `fileUploaderDragEnter`, `fileUploaderDragLeave` = the handlers; `UploadStatus` and `fileUploaderStatusText(code)` = `Sw.Upload.Status` / `StatusText` (unknown codes: "Unknown upload status [n]").

## JavaScript reference

| Name | Kind | Description |
|---|---|---|
| `UploadStatus` | frozen object (global) | C# `FileUploadStatus`: `MultipleFilesNotAllowed -12`, `UnknownRepo -11`, `InvalidFileName -10`, `OperationInProgress -9`, `InvalidFile -8`, `DiscQuotaExceeded -7`, `NotAuthorized -6`, `InvalidParams -5`, `RefuseExtension -4`, `RefuseSize -3`, `Refuse -2`, `UploadFailed -1`, `None 0`, `AlreadyUploaded 1`, `Upload 2`. |
| `Sw.Upload.StatusText(code)` | function | Translated status text. |
| `Sw.Upload.Ensure()` | async function | Loads `md5.js` when `YaMD5` is missing (throws if it can't). |
| `Sw.Upload.Files(repo, files, opts)` | async function | See Upload. |
| `Sw.Upload.Pick({Multiple, Accept})` | async function | File picker; `FileList` or null when cancelled (call from a user gesture). |
| `Sw.Upload.Target(el, opts)` | function | See Upload targets. |
| `Sw.Upload.ForElement(el, files)` | async function | Upload for a configured element (busy state and callbacks). |
| `Sw.Upload.Handlers` | frozen object | `Click`, `Drop`, `DragOver`, `DragEnter`, `DragLeave` event handlers. |
| `Sw.Upload.DefaultApiBase` | string | `"../upload/"`. |
| private: `isMultiple`, `deflate`, `isCompressible`, `uploadOne`, `dropFiles`, `hasFiles` | functions | Helpers of the above. |
| legacy globals | functions | See Legacy globals. |

## Not allowed (403)

- `Sw.Upload.Target(el, { Guard })`: `Guard` `{Url, Auth}` (or true) disables the target for users without the role (`Sw.Access.Guard`). A disabled target (`disabled` / `aria-disabled`) ignores clicks and drops. After a refused upload (403, or every file `NotAuthorized`) the target is disabled with the role needed as tool tip.
