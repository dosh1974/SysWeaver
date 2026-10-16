# explore

[⬆ srcWeb](../README.md)

> The explorer pages, scripts and styles, served under `explore/`.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.ExploreModule](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Each page is a small HTML file with its own script and style sheet, built on the shared web application scripts (`../common/common.js`, `../app/application.js`, `../common/theme.css`) from [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/srcWeb/README.md). The pages call the module's APIs under `Api/debug/explore/` (and `Api/debug/GetApiInfo`) and are linked from the *Debug* menu entries declared on [`ExploreHttpServerModule`](../../ExploreHttpServerModule.cs) (`explore`, `explore/icons.html`, `explore/logo.html`).

## Contents

| Item | Description |
|---|---|
| [`explore.js`](explore.js), [`explore.css`](explore.css) | Script and style of the generated end point list (the `explore` URL), loaded by the template in [`srcData/index.html`](../../srcData/index.html) |
| [`api.html`](api.html), [`api.js`](api.js), [`api.css`](api.css) | API page; fetches the description of an API method from `Api/debug/GetApiInfo` (also loads `tab.js`, the `edit/` scripts and `iso_data/languages.js`) |
| [`table.html`](table.html), [`table.js`](table.js), [`table.css`](table.css) | Generic interactive table viewer for any table-data API, given by the `q` (API url) and `p` (parameter) query arguments |
| [`icons.html`](icons.html), [`icons.js`](icons.js), [`icons.css`](icons.css) | Icon gallery, lists the icons returned by `Api/debug/explore/GetIcons` |
| [`logo.html`](logo.html), [`logo.js`](logo.js), [`logo.css`](logo.css) | Logo seed designer; starts from `Api/debug/explore/GetAppSeed` and previews seeds with `logo_debug.svg?<seed>` |
| [`Link.svg`](Link.svg) | Link icon (no reference to it was found in the scripts or styles) |

## Relationships

- **Part of:** [SysWeaver.HttpServer.ExploreModule](../../README.md)
- **Uses:** [SysWeaver.HttpServer `srcWeb/`](../../../SysWeaver.HttpServer/srcWeb/README.md) (common scripts, application shell, theme), the icons from [SysWeaver.HttpServer.IconModule](../../../SysWeaver.HttpServer.IconModule/srcWeb/icons/README.md)
- **Used by:** the `DataRefTable` / `GetTableDataRefUrl` links of [`ExploreHttpServerModule.cs`](../../ExploreHttpServerModule.cs), which point to `table.html`
