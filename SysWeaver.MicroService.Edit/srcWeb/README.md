# srcWeb

[⬆ SysWeaver.MicroService.Edit](../README.md)

> Source of the embedded web assets of the edit services: the object editor scripts, the type information page, the text / code editor and the vendored Ace and Prettier libraries.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Edit](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Everything in here is served by the HTTP server under the same relative path, so `srcWeb/edit/text.html` becomes `edit/text.html`.

## How it is built

The `EmbedLocal` target of [`SysWeaver.MicroService.Edit.csproj`](../SysWeaver.MicroService.Edit.csproj) runs before each build (tools in [`_tools`](../../_tools/README.md)):

1. `Compress.exe -Methods br .\srcWeb\edit\* .\web\edit` minifies and Brotli compresses the editor's own files.
2. `Compress.exe -OptimizeWebFiles false -Methods br .\srcWeb\edit\external\*+ .\web\edit\external` compresses the vendored libraries recursively without minifying them again.
3. All `web/**/*.br` files are embedded as resources.

## Contents

| Item | Description |
|---|---|
| [`edit/`](edit/README.md) | Object editor (`Edit.js` and the `EditType*` editors), type information page, text / code editor, vendored libraries |

## Relationships

- **Part of:** [SysWeaver.MicroService.Edit](../README.md)
- **Uses:** `common/` and `app/` scripts and styles from [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md)
