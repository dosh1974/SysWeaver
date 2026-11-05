# srcWeb

[⬆ SysWeaver.HttpServer](../README.md)

> Source of the built-in web application shell: the start page, the application scripts and styles, the shared JavaScript library and themes, and template help.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

The build step `Compress.exe -Methods br .\srcWeb\*+ .\web` compresses everything here (recursively, Brotli) into the generated `web/` folder, then copies `web\index.html.br` to `web\debug.html.br`. All `web/**/*.br` (and `*.png`) files are embedded and served at their relative path from the server root, e.g. `common/common.js` or `app/Home.html`. Other projects add their own pages next to these and build on the bundles in `sw/` (new format) or the legacy bundles in `common/` and `app/` (old format).

Start here: [the web client overview](../../_docs/Web.md), [your first page in 5 minutes](../../_docs/WebQuickStart.md), the component gallery `ui/gallery.html`, and the [SwWeb](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwWeb/README.md) tool (bundle, check, migrate).

**Porting an existing app** (old `common.js` / `application.js` pages, plain html / js / css pages without the framework, or new format pages from before Design v2): follow [`PORTING.md`](PORTING.md): a checklist first, then one section per topic (stable ids A0-A13, B1-B20; A0 = a page without the framework) with before / after and what `SwWeb migrate` does automatically (`--level design` for the Design v2 part) and what `SwWeb check --design` flags. The short version: `SwWeb migrate <srcWeb> --dry-run --level design --report port.md` is the complete worksheet (exactly what `check --strict --design` reports after the migration), then `migrate`, `migrate --level design` (one run does it all), and `check --strict --design` until 0 findings (necessary, not sufficient: look at the page in the five themes and on a phone). A new page: `common/theme.css` before `sw/ui.css` (without it the page stays light in the dark themes; check: `theme-include`), `<body class="sw-root">`, the viewport meta tag, texts made by code through `_T` (check: `tr-untranslated`), the code in a deferred page script, the includes of the libraries it uses (`../explore/table.js` + `table.css` for `Sw.Table`), and the page anatomy (`Sw.PageLayout`, ui/README "Page anatomy").

Build rules for everything in a `srcWeb` folder: one dot per file name (`a.b.js` is served as `a/b.js`), no `-` or `.` in folder names, classic scripts (no ES modules), nothing loaded from the internet, the translation functions `_T` / `_TF` / `_TH` / `_TFH` only as real calls with literal arguments, and bundles regenerated with `SwWeb bundle` after a change in `core/`, `ui/` or `shell/` (`SwWeb check` reports the rest; the full list and which projects embed sub-folders: [Web.md](../../_docs/Web.md#bundles-and-build-rules)).

Some of the files are served as templates with `${Variable}` substitution (by default `index.html`, `debug.html`, `app/manifest.json`, `app/Home.html` and `common/theme.css`); see [`help/Templates.txt`](help/Templates.txt).

## Contents

| Item | Description |
|---|---|
| [`core/`](core/README.md) | The UI-less client core `Sw` (requests, server push messages, InterOp, theme engine, layout, page protocol, storage, formatting, translation fallbacks); sources of the bundle `sw/core.js` |
| [`ui/`](ui/README.md) | The UI component library (`Sw.Button`, `Sw.Dialog`, `Sw.Menu`, `Sw.Tabs`, value cells, toasts, icons, design tokens); sources of `sw/ui.js` / `sw/ui.css`, the legacy `ui/compat_common.*` layer, and the component gallery [`ui/gallery.html`](ui/gallery.html) |
| [`sw/`](sw/bundles.json) | GENERATED bundles (`core.js`, `ui.js`, `ui.css`, `shell.js`, `shell.css`, the migration helpers `compat.js` / `compat.css`) and the bundle manifest `bundles.json` (built by `SwWeb bundle`; edit the sources, not the bundles) |
| [`app/`](app/README.md) | Home and welcome pages, web app manifest, tag gallery, and the GENERATED legacy bundles `application.js` / `application.css` (old pop-up and menu globals for old format pages) |
| [`shell/`](shell/README.md) | The application shell (`Sw.Shell`: header, main menu, content frame, history; bundles `sw/shell.js`, `sw/shell.css`) and the legacy compatibility layer of the old application.js |
| [`common/`](common/README.md) | The templated theme `theme.css`, the GENERATED legacy bundle `common.js` (core + ui + old globals, for old format pages), tab control, animation helpers, backgrounds and vendored scripts (marked, UZIP, MD5) |
| [`help/`](help/README.md) | Documentation of the template syntax |
| [`brand/`](brand/README.md) | The brand fonts Barlow and Saira Semi Condensed as `fonts.css` (local data urls, imported by `common/theme.css`) with their TTF sources and OFL licences; no logo or icon files |
| (not here) `favicon.ico`, `icon.svg`, `apple-touch-icon.png`, `icon-180.png`, `icon-192.png`, `icon-512.png`, `logo.svg`, `logo.png` | Not shipped in this folder: the server generates them per app (`HttpServerBase_Favicon.cs`) from the app name / `AppSeed` in the application colours; an app replaces them with its own files of the same names |
| [`PORTING.md`](PORTING.md) | The porting guide for app authors: old UI -> Sw core / ui / shell, and Design v2 (includes, dialogs, libraries, times, access checks; tokens, elevation, page anatomy, components, tables, editors, shell, themes, branding, app colours, framework pages), with what SwWeb migrates and checks |
| [`index.html`](index.html) | The start page (template): title from `${#EnvInfo.AppDisplayName}`, loads `sw/core.js`, `sw/ui.js`, `sw/shell.js`, `common/theme.css`, `sw/ui.css`, `sw/shell.css` and the optional application specific `app/app.js` / `app/app.css`; also served as `debug.html` |

## Relationships

- **Part of:** [SysWeaver.HttpServer](../README.md)
- **Used by:** the web pages of nearly every project with a `srcWeb/` folder, e.g. [SysWeaver.HttpServer.ExploreModule](../../SysWeaver.HttpServer.ExploreModule/srcWeb/explore/README.md), which reference `../common/…` and `../app/…`
- **Uses:** the icons served by [SysWeaver.HttpServer.IconModule](../../SysWeaver.HttpServer.IconModule/srcWeb/icons/README.md) (`icons/*.svg`)

## Behavior (web analysis)

### Loading and initialization

- **`index.html`** (a template; only `${#EnvInfo.AppDisplayName}` is substituted into `<title>`) is an empty shell document:
  - Mobile/PWA meta tags and the viewport `width=device-width, initial-scale=1, viewport-fit=cover`. Pinch zoom is allowed on every page (WCAG 1.4.4 resize text; iOS ignores `user-scalable=no` anyway); controls with their own drag gestures (sliders, split handles, the menu drawer) set `touch-action` themselves, so zoom never steals their gestures. Do not add `user-scalable=no` or `maximum-scale` to new pages.
  - `defer` scripts in order: `sw/core.js`, `sw/ui.js`, `sw/shell.js`, `app/app.js`.
  - Stylesheets in order: `common/theme.css` (also a template), `sw/ui.css`, `sw/shell.css`, `app/app.css`.
  - Icons `favicon.ico`, `icon.svg`, `apple-touch-icon.png` and `app/manifest.json`, all served by `HttpServerBase` optional end points or templates. `icon.svg` is generated if no module serves it, and the bitmaps are rendered from it (see [App icon, logo and banner](../README.md#app-icon-logo-and-banner)).
  - `<body onload="appMain()">` (`appMain` is a permanent global defined by `sw/shell.js`).
- Because all scripts are `defer`, the core has run its setup (theme, the `Sw*` layout classes, url flags such as `?useTheme=`) before `onload` calls `appMain()`. The full sequence is described in [`shell/README.md`](shell/README.md#behavior):
  1. nested shell guard (inside another shell's frame the top shell is reloaded instead), loading overlay;
  2. `Sw.Shell.App.Create()` builds header, menu and content, and loads the `User` and `Theme` menus, `auth/GetUser` and the `Default` menu;
  3. the optional `window.AppMain()` from `app/app.js`;
  4. the overlay is removed;
  5. the initial content opens: the Base64 URL from `location.hash`, or the first main-menu item (Welcome or Home).
- Old format pages keep loading `common/common.js` + `app/application.js` (GENERATED legacy bundles that define the old globals on top of Sw).
- **Migrated pages** (new format includes, but code that still uses legacy names; written by `SwWeb migrate`) load the GENERATED compatibility bundle `sw/compat.js` after `sw/core.js` + `sw/ui.js` (+ `sw/shell.js`), and link `sw/compat.css` after `sw/ui.css` (+ `sw/shell.css`):
  ```html
  <script defer src="../sw/core.js"></script>
  <script defer src="../sw/ui.js"></script>
  <script defer src="../sw/compat.js"></script>   <!-- legacy names: remove once the page only uses Sw.* (SwWeb check --strict) -->
  <script defer src="page.js"></script>
  <link rel="stylesheet" href="../common/theme.css">
  <link rel="stylesheet" href="../sw/ui.css">
  <link rel="stylesheet" href="../sw/compat.css">
  <link rel="stylesheet" href="page.css">
  ```
  `sw/compat.js` = `shell/menu.js` + `shell/content.js` + `shell/shell.js` (each loads once, so `sw/shell.js` may be loaded too) + `ui/compat_common.js` + `shell/compat_application.js`: every global of the old common.js and application.js, without loading the core / ui again. Like common.js it switches `Sw.Cells.Classes` to the old cell class names (`Right`, `Text` ...), so the page's old style sheets keep matching. `sw/compat.css` = `ui/compat_common.css` + `shell/shell.css` + `shell/compat_application.css` (the compat part of common.js's styles plus the old application.css); `sw/compat.js` inserts it after the last `sw/*.css` link when the page does not link it. Remove both includes once `SwWeb check --strict` reports no legacy names for the page.
- **`debug.html`** has no source file. The build copies `web/index.html.br` to `web/debug.html.br` (see the csproj), and the server registers `debug.html` as a template like `index.html`. The served content is identical; any debug-specific behaviour comes from the server (e.g. `icon_debug.svg` / `logo_debug.svg` end points or session state), not from the markup.
- **Application customisation:** `app/app.js` and `app/app.css` are always requested. `HttpServerBase` answers them with empty content (`HandleEmpty` optional end points) unless a module serves real files at those paths. An `app.js` usually defines `async function AppMain()`.

### Server interaction

- Template variables: `EnvInfo.AppDisplayName` (title). Templates matched by default: `index.html`, `debug.html`, `app/manifest.json`, `app/Home.html`, `common/theme.css` (plus `Templates` from the server parameters).
- All dynamic data (menus, user) is requested by the shell (`sw/shell.js`); see [`shell/README.md`](shell/README.md).

### State and persistence

- The root page owns the browser history and URL: the hash holds `Base64(url of the embedded page)`, and `history.state` holds the URL. Embedded pages talk to it through `window.top.postMessage` (`IframeNavigating`, `LoaderRemoved`). The `localStorage` keys used are `SysWeaver.Pinned`, `SysWeaver.MenuCollapsed.*` and `SysWeaver.PopUpRecent.*` (shell / ui), and `SysWeaver.Theme` (core).

### Dependencies

- Needs `sw/` (core, ui and shell bundles), `common/theme.css`, `app/` (start pages), the icons from IconModule, and the server end points `Api/application/GetMenu` (SysWeaver.MicroService.HttpServer `WebMenuService`) and `Api/auth/GetUser` (SysWeaver.MicroService.Auth). Without `WebMenuService` the menu request fails.
- Every page that registers a menu entry is hosted inside this page's `<app-content>` iframe.

### Structural observations

- `index.html` contains no markup besides `<head>`; the whole UI is built in JS. Load order (`sw/core.js`, then `sw/ui.js`, then `sw/shell.js`, then `app/app.js`) and the global function names (`appMain`, `AppMain`) form the contract between the files. Old format pages use the order `common/common.js`, then `app/application.js`.
- `index.html` and `debug.html` are identical copies produced by the build, not two sources.
- Sub-pages under `app/` repeat the same head block (meta tags, favicon links, manifest, `theme.css`) instead of sharing it; this pattern is repeated by pages in other projects.
