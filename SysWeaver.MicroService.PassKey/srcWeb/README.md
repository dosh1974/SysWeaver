# srcWeb

[⬆ SysWeaver.MicroService.PassKey](../README.md)

> Source of the embedded passkey web pages and icons.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.PassKey](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

This folder holds the browser side of the passkey service. It has no files of its own, only the two sub folders below.

## How it is built

The `EmbedLocal` target in [`SysWeaver.MicroService.PassKey.csproj`](../SysWeaver.MicroService.PassKey.csproj) runs before each build:

1. `SvgOpt.exe -Compare .\WebCompare -RemoveFill -RemoveSize` optimises `icons/*.svg` into the temporary `icons/.opt/` folder (not in git). The optimiser writes a before/after comparison page using [`WebCompare.html`](../WebCompare.html).
2. `Compress.exe -Methods br` compresses the optimised icons into `web/icons` and everything in `auth/` into `web/auth`.
3. Every `web/**/*.br` file is embedded as a resource and served as `icons/...` and `auth/...` relative to the site root.

See [`_tools`](../../_tools/README.md) for the build tools.

## Contents

| Item | Description |
|---|---|
| [`auth/`](auth/README.md) | Passkey pages (sign in, add, add on other device, manage) with their script and styles |
| [`icons/`](icons/README.md) | Passkey icons used through CSS mask images |

## Relationships

- **Part of:** [SysWeaver.MicroService.PassKey](../README.md)
- **Uses:** `auth/Auth.js` and `auth/AuthUi.js` from [SysWeaver.MicroService.Auth](../../SysWeaver.MicroService.Auth/README.md), shared web assets of [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md)
