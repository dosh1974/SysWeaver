# srcData

[⬆ SysWeaver.MicroService.UserStorage](../README.md)

> HTML templates for the pages that are served when someone opens a stored link.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.UserStorage](../README.md) |
| **Kind** | Embedded data (HTML templates) |

## Purpose

The `EmbedLocal` target in [`SysWeaver.MicroService.UserStorage.csproj`](../SysWeaver.MicroService.UserStorage.csproj) runs `Compress.exe -Methods br .\srcData\*+ .\data` before each build. The generated `data/**/*.br` files (not in git) are embedded as resources. [`UserStorageService`](../UserStorageService.cs) loads them as `<namespace>.data.embed.html` and `<namespace>.data.img.html` and parses them as text templates with `${...}` placeholders.

When a stored link is requested, the service fills in `Base Url`, `Url Name` (the target URL, or the extension viewer for it), `Title Text` and `Extre Themes`. It then returns the result as an HTML page. Image targets use `img.html`. Everything else uses `embed.html`. Both pages are new format pages (`sw/core.js`, `sw/ui.js`, `sw/shell.js` for `Sw.Shell.ToMenuItems`, `common/theme.css`, `sw/ui.css`, `<body class="sw-root" onload="usMain()">`) and, when shown at top level, add a small settings button (`Sw.Button` + `Sw.Menu`) with full screen and the server's Theme menu. The page code is inline (srcData templates are not served as files) and runs from `<body onload>`, after the deferred bundles.

## Contents

| Item | Description |
|---|---|
| [`embed.html`](embed.html) | Shows the linked URL in a full page iframe and copies the iframe document title to the page title |
| [`img.html`](img.html) | Shows a linked image scaled to fit the page |

## Relationships

- **Part of:** [SysWeaver.MicroService.UserStorage](../README.md)
- **Uses:** shared web assets of [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md) (`sw/core.js`, `sw/ui.js`, `sw/shell.js`, `common/theme.css`, `sw/ui.css`)
