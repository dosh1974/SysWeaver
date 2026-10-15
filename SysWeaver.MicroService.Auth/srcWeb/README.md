# srcWeb

[⬆ SysWeaver.MicroService.Auth](../README.md)

> Source of the embedded web assets of the auth services: the login and logout pages, their scripts and styles, and icons.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Auth](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Everything in here is served by the HTTP server under the same relative path, so `srcWeb/auth/Login.html` becomes `auth/Login.html`. The HTTP server redirects unauthenticated requests to this login page.

## How it is built

The `EmbedLocal` target of [`SysWeaver.MicroService.Auth.csproj`](../SysWeaver.MicroService.Auth.csproj) runs before each build (tools in [`_tools`](../../_tools/README.md)):

1. `SvgOpt.exe -Compare .\WebCompare -RemoveSize` optimises `auth/icons/*.svg` into a temporary `auth/icons/.opt/` folder (deleted first).
2. `Compress.exe -Methods br` writes Brotli compressed copies of the optimised icons to `web/auth/icons` and of the `auth/*` files to `web/auth`.
3. All `web/**/*.br` files are embedded as resources.

## Contents

| Item | Description |
|---|---|
| [`auth/`](auth/README.md) | Login / logout pages, `Auth.js` (requests) and `AuthUi.js` (page building), styles and icons |

## Relationships

- **Part of:** [SysWeaver.MicroService.Auth](../README.md)
- **Uses:** `common/common.js` and `common/theme.css` from [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md)
