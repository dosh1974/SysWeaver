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

## Behavior (web analysis)

### Loading and initialization

- **`index.html`** (a template; only `${#EnvInfo.AppDisplayName}` is substituted into `<title>`) is an empty shell document:
  - Mobile/PWA meta tags and a viewport with zoom disabled.
  - `defer` scripts in order: `common/common.js`, `app/application.js`, `app/app.js`.
  - Stylesheets in order: `common/theme.css` (also a template), `app/application.css`, `app/app.css`.
  - Icons `favicon.ico`, `icon.svg`, `apple-touch-icon.png` and `app/manifest.json`, all served by `HttpServerBase` optional end points or templates.
  - `<body onload="appMain()">`.
- Because all scripts are `defer`, `common.js` has run its top-level setup (theme selection, `window.Mobile`/`window.Narrow`, the `SwMobile`/`SwNarrow` body classes, the `?useTheme=` parameter) before `onload` calls `appMain()`. The full sequence is described in [`app/README.md`](app/README.md#behavior-web-analysis):
  1. loading overlay;
  2. `App.Create()` builds header, menu and content, and loads the `Default`, `User` and `Theme` menus plus `auth/GetUser`;
  3. the optional `window.AppMain()` from `app/app.js`;
  4. the overlay is removed;
  5. the initial content opens: the Base64 URL from `location.hash`, or the first main-menu item (Welcome or Home).
- **`debug.html`** has no source file. The build copies `web/index.html.br` to `web/debug.html.br` (see the csproj), and the server registers `debug.html` as a template like `index.html`. The served content is identical; any debug-specific behaviour comes from the server (e.g. `icon_debug.svg` / `logo_debug.svg` end points or session state), not from the markup.
- **Application customisation:** `app/app.js` and `app/app.css` are always requested. `HttpServerBase` answers them with empty content (`HandleEmpty` optional end points) unless a module serves real files at those paths. An `app.js` usually defines `async function AppMain()`.

### Server interaction

- Template variables: `EnvInfo.AppDisplayName` (title). Templates matched by default: `index.html`, `debug.html`, `app/manifest.json`, `app/Home.html`, `common/theme.css` (plus `Templates` from the server parameters).
- All dynamic data (menus, user) is requested by `application.js`; see [`app/README.md`](app/README.md).

### State and persistence

- The root page owns the browser history and URL: the hash holds `Base64(url of the embedded page)`, and `history.state` holds the URL. Embedded pages talk to it through `window.top.postMessage` (`IframeNavigating`, `LoaderRemoved`). The `localStorage` keys used are `SysWeaver.Pinned`, `SysWeaver.MenuCollapsed.*` and `SysWeaver.PopUpRecent.*` (app), and `SysWeaver.Theme` (common.js).

### Dependencies

- Needs `common/` (library, theme), `app/` (shell), the icons from IconModule, and the server end points `Api/application/GetMenu` (SysWeaver.MicroService.HttpServer `WebMenuService`) and `Api/auth/GetUser` (SysWeaver.MicroService.Auth). Without `WebMenuService` the menu request fails.
- Every page that registers a menu entry is hosted inside this page's `<app-content>` iframe.

### Structural observations

- `index.html` contains no markup besides `<head>`; the whole UI is built in JS. Load order (common, then application, then app) and global function names (`appMain`, `AppMain`) form the contract between the files.
- `index.html` and `debug.html` are identical copies produced by the build, not two sources.
- Sub-pages under `app/` repeat the same head block (meta tags, favicon links, manifest, `theme.css`) instead of sharing it; this pattern is repeated by pages in other projects.
