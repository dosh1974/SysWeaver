# srcWeb

[⬆ SysWeaver.HttpServer](../README.md)

> Source of the built-in web application shell: the start page, the application scripts and styles, the shared JavaScript library and themes, and template help.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

The build step `Compress.exe -Methods br .\srcWeb\*+ .\web` compresses everything here (recursively, Brotli) into the generated `web/` folder, then copies `web\index.html.br` to `web\debug.html.br`. All `web/**/*.br` (and `*.png`) files are embedded and served at their relative path from the server root, e.g. `common/common.js` or `app/Home.html`. Other projects add their own pages next to these and build on the scripts in `common/` and `app/`.

Some of the files are served as templates with `${Variable}` substitution (by default `index.html`, `debug.html`, `app/manifest.json`, `app/Home.html` and `common/theme.css`); see [`help/Templates.txt`](help/Templates.txt).

## Contents

| Item | Description |
|---|---|
| [`app/`](app/README.md) | The application shell: main menu, pop-ups, home and welcome pages, web app manifest, tag gallery |
| [`common/`](common/README.md) | Shared JavaScript library, themes, tab control, animation helpers and vendored scripts (marked, UZIP, MD5) |
| [`help/`](help/README.md) | Documentation of the template syntax |
| [`index.html`](index.html) | The start page (template): title from `${#EnvInfo.AppDisplayName}`, loads `common/common.js`, `app/application.js`, `common/theme.css`, `app/application.css` and the optional application specific `app/app.js` / `app/app.css`; also served as `debug.html` |

## Relationships

- **Part of:** [SysWeaver.HttpServer](../README.md)
- **Used by:** the web pages of nearly every project with a `srcWeb/` folder, e.g. [SysWeaver.HttpServer.ExploreModule](../../SysWeaver.HttpServer.ExploreModule/srcWeb/explore/README.md), which reference `../common/…` and `../app/…`
- **Uses:** the icons served by [SysWeaver.HttpServer.IconModule](../../SysWeaver.HttpServer.IconModule/srcWeb/icons/README.md) (`icons/*.svg`)
