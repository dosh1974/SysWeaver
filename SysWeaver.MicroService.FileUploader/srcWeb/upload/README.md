# upload

[⬆ srcWeb](../README.md)

> A debug page for uploading files manually to any file repository.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.FileUploader](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Served as `upload/Example.html`, the same url folder as the upload api (`upload/Upload`, `upload/CheckStatus`). [`FileUploaderService`](../../FileUploaderService.cs) adds it to the menu as *Debug / FileUploader / Upload file* (roles `debug`, `dev`, embedded in the shell). It is also the example of how to use [`common/fileUploader.js`](../common/README.md) (`Sw.Upload`).

## Contents

| Item | Description |
|---|---|
| [`Example.html`](Example.html) | The page: new format includes (`../sw/core.js`, `../sw/ui.js`, `../common/fileUploader.js`, `example.js`; `../common/theme.css`, `../sw/ui.css`, `example.css`), a *Save to repository* input, the drop zone `#drop` (with the words "Drop files here or select to choose", `.DropText`), the result link `#link` and the example figure `.UploadUserImage`: the current user image `#userimage` with the caption "Example: your user picture, stored in the UserImage repository" |
| [`example.js`](example.js) | `exampleMain()` (body onload) and `exampleShowResult(res, files)` |
| [`example.css`](example.css) | Page styles on the Sw tokens: input, drop zone states, link, image |

## Relationships

- **Part of:** [srcWeb](../README.md), [SysWeaver.MicroService.FileUploader](../../README.md)
- **Uses:** [`common/fileUploader.js`](../common/fileUploader.js) (`Sw.Upload`), the Sw core / ui, `../auth/UserImages/Current/large`, icons `upload.svg`, `disc.svg`, `plus.svg`, `loading.svg`

## Behavior

- `exampleMain()` shows the loader (`Sw.Page.Loading`), reads the url parameters `repo` (default `UserImage`) and `multiple` (present = multiple files allowed), fills the *Repository* input (changing it retargets later uploads through `data-repo`) and calls `Sw.Upload.Target(#drop, {Repo, Multiple, Title, OnStart, OnResults})`; it preloads `md5.js` (`Sw.Upload.Ensure`). On touch only devices (`pointer: coarse` and no fine pointer: nothing can be dropped) the drop zone words become "Tap here to choose files"; elsewhere they are device neutral ("Drop files here or select to choose"), as are the tool tips ("Choose file(s) to upload, or drop file(s) here to upload them", "View or download the uploaded file").
- The drop zone is a neutral dashed area (line color, the upload icon above the words; hover / focus: primary accent) and is clickable, keyboard operable (Tab, Enter / Space; role button, focus ring) and a drop target; states: `Selecting` (picker open: dimmed, disc icon), `Dragging` (accent 1, plus icon), `Uploading` (dimmed, loading icon, input blocked).
- `OnStart`: info toast "Uploading "<first file>"". `exampleShowResult`: `res.Error` -> fail toast (link hidden); the first file's url is shown as a link (`../<url>`, new tab, hidden without url); `AlreadyUploaded` -> info; `None` -> info and a cache flushing reload of the user image (`Sw.SetImageSource(img, src, {FlushCache: true})`); other codes -> fail with `Sw.Upload.StatusText`.
- Static texts carry `data-translation_context` for the server side translation. No storage.

## JavaScript reference (`example.js`)

| Name | Description |
|---|---|
| `exampleMain()` | Entry point (see Behavior). |
| `exampleShowResult(res, files)` | Shows the result of an upload (see Behavior). |

## CSS reference (`example.css`)

| Selector | Description |
|---|---|
| `.UploadExample` | Centered column, max 90 characters wide, 16 px side gutter (at least the safe area). |
| `.UploadRepo`, `.UploadRepo input` | Label and input (touch sized, focus ring). |
| `#drop`, `#drop::after`, `.DropText`, `.Selecting`, `.Dragging`, `.Uploading` | Drop zone (neutral, dashed), its masked state icon and its words. |
| `#link`, `#link.Show` | Result link (hidden until a url exists). |
| `.UploadUserImage`, `.UploadUserImage figcaption`, `#userimage` | The example figure: the current user image sized like a profile picture (200 px times `--sw-scale`, at most 60vw x 30vh) with its muted caption. |
