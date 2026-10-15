# srcWeb

[⬆ SysWeaver.MicroService.FileUploader](../README.md)

> Source of the embedded web assets of the file uploader: the reusable browser upload script and a debug upload page.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.FileUploader](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Everything in here is served by the HTTP server under the same relative path, so `srcWeb/common/fileUploader.js` becomes `common/fileUploader.js`, next to the shared scripts of [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md).

## How it is built

The `EmbedLocal` target of [`SysWeaver.MicroService.FileUploader.csproj`](../SysWeaver.MicroService.FileUploader.csproj) runs `Compress.exe -Methods br .\srcWeb\*+ .\web` (see [`_tools`](../../_tools/README.md)), which minifies and Brotli compresses the whole folder recursively into the generated `web/` folder; all `web/**/*.br` files are embedded as resources.

## Contents

| Item | Description |
|---|---|
| [`common/`](common/README.md) | `fileUploader.js`: the browser side of uploads, used by other projects' pages |
| [`upload/`](upload/README.md) | `Example.html`: a debug page for manual uploads to any repository |

## Relationships

- **Part of:** [SysWeaver.MicroService.FileUploader](../README.md)
- **Uses:** `common/common.js`, `common/md5.js`, `common/UZIP.js` and `common/theme.css` from [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md)
