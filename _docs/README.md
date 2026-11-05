# _docs

[⬆ SysWeaver overview](../README.md)

> Additional documents that are not tied to a project folder: the web client architecture overview, the web quick start guide and the reverse proxy schematic.

| | |
|---|---|
| **Repository** | [SysWeaver](../README.md) |
| **Kind** | Documents |

## Purpose

Holds cross-project documentation. [`Web.md`](Web.md) describes the web client as a whole (the `Sw` library: core, ui, shell) and links to the detailed folder READMEs; [`WebQuickStart.md`](WebQuickStart.md) walks a developer through a first page. The other document is a Word drawing of the reverse proxy set-up of [SysWeaver.ReverseProxy](../SysWeaver.ReverseProxy/README.md). It was committed together with `SysWeaver.ReverseProxy/ReverseProxy.svg`, the schematic shown in that project's README, and appears to be its editable source. Nothing in the build uses this folder.

## Contents

| Item | Description |
|---|---|
| [`Web.md`](Web.md) | Architecture overview of the web client: the `Sw` layers (UI-less core, UI library, app shell) and their bundles, build and serving rules, page format and life cycle, the shell and server menus, server contract highlights, push messages, messaging, theming, feature libraries, the translation rule, the legacy compatibility layer, storage keys |
| [`UX_Guide.md`](UX_Guide.md) | The UX / UI rules for every page and component: input (pure clicks, right-click menus, cursor and hover, keyboard / touch / mouse parity), pop-ups, keyboard focus, feedback for async work, motion, layout stability, tool tips, localization (`_T` / `_TF` / `_TH` / `_TFH`, HTML translation and `data-translation_context`, no CSS translation), data safety (confirmations, unsaved edits, drafts), forms, tables, accessibility; with the framework support status and the page review checklist |
| [`WebQuickStart.md`](WebQuickStart.md) | Your first page in 5 minutes: embedding a `srcWeb` folder, a core-only page (api call, push messages), a page with its own UI on the messaging only bundle `sw/session.js` (+ the opt-in `sw/theme.js`), a UI page with a form and a live table, the `[WebMenuEmbedded]` attribute, testing, migrating old pages with SwWeb, the rules that bite |
| [`WebMessaging.md`](WebMessaging.md) | How a page talks to the server and the other windows: the session (GetMessages, master election, handover), the server events and their default reactions, subscriptions, server time, InterOp and the frame protocol, the theme hand-shake (`?setTheme`, `?useTheme`), the automatic html / body classes, the legacy names, the differences from the original; the messaging only bundle `sw/session.js` and the opt-in `sw/theme.js` |
| [`WebBranding.md`](WebBranding.md) | Branding a deployed app: name, logo and icons, the Application theme colors (from `AppSeed`), own themes, page styles, without forking the framework |
| [`ReverseProxy.docx`](ReverseProxy.docx) | Schematic of the reverse proxy: HTTP clients reach a **reverse proxy server** (machine with public access, static IP and domain name; client services reached by sub path, the default, or by domain name) over HTTPS, which forwards to one or more **reverse proxy client services** (machines without public access) whose end points are either internal (the same SysWeaver service process) or any HTTP server reachable from that machine. |

## Related

- [Sw UI library](../SysWeaver.HttpServer/srcWeb/ui/README.md), [core API](../SysWeaver.HttpServer/srcWeb/core/README.md), [shell](../SysWeaver.HttpServer/srcWeb/shell/README.md)
- [Component gallery](../SysWeaver.HttpServer/srcWeb/ui/gallery.html) (served at `/ui/gallery.html`)
- [SwWeb tool](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwWeb/README.md) (bundle, check, migrate)

## Relationships

- **Part of:** [SysWeaver](../README.md)
- **Documents:** [SysWeaver.ReverseProxy](../SysWeaver.ReverseProxy/README.md); the hosts in SysWeaver.Services are [ReverseProxyServer](https://github.com/dosh1974/SysWeaver.Services/blob/main/ReverseProxyServer/README.md) and [ReverseProxyClient](https://github.com/dosh1974/SysWeaver.Services/blob/main/ReverseProxyClient/README.md)
