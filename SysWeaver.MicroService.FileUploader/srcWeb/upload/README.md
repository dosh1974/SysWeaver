# upload

[⬆ srcWeb](../README.md)

> A debug page for uploading files manually to any file repository.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.FileUploader](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Served as `upload/Example.html`, the same URL folder as the upload API (`upload/Upload`, `upload/CheckStatus`). [`FileUploaderService`](../../FileUploaderService.cs) adds it to the web menu as *Debug / FileUploader / Upload file* for sessions with the `debug` or `dev` role. It also serves as an example of how to use [`common/fileUploader.js`](../common/README.md).

## Contents

| Item | Description |
|---|---|
| [`Example.html`](Example.html) | Upload example page: loads `common/common.js`, `common/md5.js`, `common/UZIP.js` and `common/fileUploader.js`, has a repository name input (from the `repo` URL parameter, default `UserImage`) and a drop / click area set up with `fileUploaderSetup`, and shows the result of each upload |

## Relationships

- **Part of:** [srcWeb](../README.md), [SysWeaver.MicroService.FileUploader](../../README.md)
- **Uses:** [`common/fileUploader.js`](../common/fileUploader.js)

## Behavior (web analysis)

### Loading and initialization

- `Example.html` loads `../common/common.js`, `../common/md5.js`, `../common/UZIP.js` and `../common/fileUploader.js` (all `defer`), plus `../common/theme.css`. All page styles are in an inline `<style>` block.
- The entry point is the inline function `exampleMain()`, run on load through `<body onload="exampleMain()">`.
- Static markup: a "Repository" label `<span>`, `<input id="repo">`, the drop zone `<div id="drop">`, a result link `<a id="link" target="_blank">` and `<img id="userimage" src="../auth/UserImages/Current/large">`. `data-translation_context` attributes give the translation layer context.
- The `<a>`, `<input>`, `<span>` and `<div>` elements are styled through element selectors, so the page assumes it contains only one of each relevant element.

### Files

**`Example.html`** (`exampleMain()`):
- Shows a loader with `AddLoading(document.body)`. It reads the `repo` URL parameter (default `UserImage`) and the `multiple` parameter (any value means `data-multiple="true"`).
- Sets `data-repo` on `#drop` and copies the repo into `#repo`. The input's `onchange` rewrites `data-repo`, so the target repository can be changed at runtime.
- Calls `fileUploaderSetup(drop, null, onstart, onresults)`. `onstart` shows `Info("Uploading \"<first file>\"")`. `onresults` checks `res.Error` (then `Fail`) and otherwise looks only at the **first** file: when `Urls[0]` is set, `#link` shows it with `href="../" + url` and the `Show` class. For `AlreadyUploaded` and `None` it shows an `Info` toast, and on `None` it also reloads `#userimage` with `SetImageSource(img, img.src, null, true)` (cache flush). Any other status calls `Fail(fileUploaderStatusText(code))`.
- CSS: the drop `div` has a dashed `--ThemeAcc275` border and a masked `:after` icon. The icon changes with the state classes set by `fileUploader.js`: `upload.svg` by default, `disc.svg` for `.Selecting`, `plus.svg` for `.Dragging` and `loading.svg` for `.Uploading`, and `.Selecting` and `.Uploading` dim the element. The link is hidden until it has the `.Show` class. Theme variables used: `--ThemeFont`, `--ThemeFontSize`, `--ThemeMain25`, `--ThemeAcc1`, `--ThemeAcc2`, `--ThemeAcc225`, `--ThemeAcc250`, `--ThemeAcc275`, `--ThemeRad`, `--ThemeRadInp`, `--ThemeFadeDuration`.

### Server interaction

- It does no direct calls. Everything goes through `fileUploader.js` with the default `apiBase` `../upload/`, which gives `upload/CheckStatus` (JSON) and `upload/Upload?repo=...` (binary POST). See [`common/`](../common/README.md) for the protocol.
- It reads `../auth/UserImages/Current/large` (an image from the auth module) to show the effect of the default `UserImage` repo.
- Registration: `FileUploaderService` has `[WebMenuEmbedded(null, "Debug/FileUploader", "Upload file", "upload/Example.html", ..., "IconUpload", -8, "debug,dev")]`, so the page is embedded in the app shell menu for the `debug` and `dev` roles.

### State and persistence

- No storage. The repo choice lives only in the `data-repo` attribute; state comes from the URL parameters `repo` and `multiple`.

### Dependencies

- `common.js`: `getUrlParams`, `AddLoading`, `Info`, `Fail`, `_T`, `SetImageSource`. `fileUploader.js`: `fileUploaderSetup`, `UploadStatus`, `fileUploaderStatusText`. `md5.js` and `UZIP.js` are used indirectly.
- No other page links to it; the only way in is the menu entry.

### Structural observations

- It is an example page: the logic is an inline script with no separate JS or CSS file. Its result-handling switch is the same pattern repeated in `app/home.js`, ServerManager `keys.js` and `serviceInfo.js`.
- It handles only the first file even when `multiple` is set (an inline comment says so).
- The page lives in `upload/` because the service's API URL root is `upload/`. The page and the API share one URL folder, while the library it demonstrates lives in the shared `common/`.

## JavaScript reference

Conventions used in this reference: "inner" marks functions local to an enclosing function. The folder has no `.js` or `.css` files; only the inline `<script>` of `Example.html` is documented (its inline `<style>` is summarized under Behavior above).

### `Example.html` (inline script)

A single inline `<script>` at the end of `<body>` that defines one global function. It depends on the deferred scripts `common/common.js` and `common/fileUploader.js`. It runs on load through `<body onload="exampleMain()">`.

#### Functions

| Function | Signature | Description |
|---|---|---|
| `exampleMain` | `()` | Page entry point. Shows a loader (`AddLoading(document.body)`, closed at the end), reads URL parameters via `getUrlParams()`: `repo` (default `"UserImage"`) is written to `#drop`'s `data-repo` and to the `#repo` input, whose `onchange` rewrites `data-repo`; any `multiple` value sets `data-multiple="true"`. Calls `fileUploaderSetup(#drop, null, onstart, onresults)` with two anonymous callbacks: onstart shows `Info("Uploading \"<first file>\"")`; onresults calls `Fail(res.Error)` (and hides `#link`) on error, otherwise uses only the first file: sets `#link` text/`href="../" + Urls[0]` and toggles class `Show`, then for `UploadStatus.AlreadyUploaded` shows an `Info`, for `UploadStatus.None` shows an `Info` and reloads `#userimage` with `SetImageSource(img, img.src, null, true)`, and for any other code calls `Fail` with `fileUploaderStatusText(code)`. Exceptions during setup are reported with `Fail(e)`. |
