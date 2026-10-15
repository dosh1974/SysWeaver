# srcData

[⬆ SysWeaver.HttpServer.ExploreModule](../README.md)

> The HTML template of the generated "explore" end point listing pages.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.ExploreModule](../README.md) |
| **Kind** | Embedded data |

## Purpose

Unlike [`srcWeb/`](../srcWeb/README.md), this folder is not served as is. The build step `Compress.exe -Methods br .\srcData\*+ .\data` compresses it into `data/`, which is embedded as resources. [`ExploreHttpServerModule`](../ExploreHttpServerModule.cs) reads `index.html` from the resources at start up and uses it as a `TextTemplate` with `$(...)` variables to render the end point list whenever `explore` is appended to a folder URL.

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | Page template; the module fills in `$(TITLE)` (the explored path), `$(FOLDERICON)`, `$(COMMONJSFILE)`, `$(STYLECSSFILE)`, `$(JSFILE)`, `$(CSSFILE)` (`explore/explore.js` / `.css`) and `$(DATA)` (the end point list as JSON, passed to `Build()`) |

## Relationships

- **Part of:** [SysWeaver.HttpServer.ExploreModule](../README.md)
- **Used by:** [`ExploreHttpServerModule.cs`](../ExploreHttpServerModule.cs)
- **Uses:** [`srcWeb/explore/`](../srcWeb/explore/README.md) (`explore.js`, `explore.css`), `common/common.js` and `common/theme.css` from [SysWeaver.HttpServer](../../SysWeaver.HttpServer/srcWeb/common/README.md)
