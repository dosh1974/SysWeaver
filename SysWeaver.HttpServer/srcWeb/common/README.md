# common

[⬆ srcWeb](../README.md)

> The shared client-side library of all SysWeaver web pages, served under `common/`: core scripts, themes and background patterns, the tab control, animation helpers and a few vendored libraries.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Almost every page in the framework loads `common/common.js` and `common/theme.css`. `theme.css` is served as a template (`${Variable}` substitution, e.g. for the configured colours) and defines the themes selected through the `data-theme` attribute (e.g. `dark`, `hacker`, `application`), several of which use the background images in this folder. The remaining scripts are loaded on demand by the pages that need them.

## Contents

| Item | Description |
|---|---|
| [`common.js`](common.js) | Core library (about 7800 lines): requests to the web API (`sendRequest`, `getRequest`), dynamic script and style loading (`includeJs`, `includeCss`), translation helpers, `SessionManager`, cross-window messaging (`InterOp`), buttons, value formatting, colour icons, full screen, progress bars, image loading and caching |
| [`theme.css`](theme.css) | Theme variables and theme variants (template) |
| [`common.css`](common.css) | Theme variable defaults and base styles; no reference to this file by name was found in the repositories |
| [`tab.js`](tab.js), [`tab.css`](tab.css) | Tab bar control (`Tab`, a `SysWeaver-TabBar` element, optionally remembering the selected tab in local storage) |
| [`anim.js`](anim.js) | Animation and interpolation helpers (`MathTools`, cubic Hermite interpolation, `SmoothValue`, spring, projectile and bounce motions), used by the media viewer pages |
| [`marked.js`](marked.js) | Vendored *marked* v15.0.3 Markdown parser (MIT, minified) |
| [`UZIP.js`](UZIP.js) | Vendored UZIP JavaScript zip / deflate library, used with `md5.js` by upload pages |
| [`md5.js`](md5.js) | Vendored YaMD5 MD5 hasher |
| [`racing-stripes.svg`](racing-stripes.svg), [`moroccan.svg`](moroccan.svg), [`glamorous.svg`](glamorous.svg), [`graph-paper.svg`](graph-paper.svg), [`Hacker.png`](Hacker.png) | Background patterns used by the themes in `theme.css` (`Hacker.png` is a 32×32 tile for the hacker theme) |
| [`hexagons.svg`](hexagons.svg), [`floating-cogs.svg`](floating-cogs.svg), [`plus.svg`](plus.svg) | Further patterns / images; no reference to them by name was found in the repositories |

## Relationships

- **Part of:** [SysWeaver.HttpServer](../../README.md)
- **Used by:** [`app/`](../app/README.md), the explorer pages of [SysWeaver.HttpServer.ExploreModule](../../../SysWeaver.HttpServer.ExploreModule/srcWeb/explore/README.md) and the web pages of most other projects, e.g. [SysWeaver.Chat](../../../SysWeaver.Chat/README.md) (`UZIP.js`, `md5.js`), [SysWeaver.MicroService.Media](../../../SysWeaver.MicroService.Media/README.md) (`anim.js`), [SysWeaver.MicroService.FileUploader](../../../SysWeaver.MicroService.FileUploader/README.md) and [SysWeaver.MicroService.ServerManager](../../../SysWeaver.MicroService.ServerManager/README.md)
