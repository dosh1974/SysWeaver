# srcWeb

[⬆ SysWeaver.HttpServer.IconModule](../README.md)

> Source of the shared icon library, embedded and served under `icons/`.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.IconModule](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

All web content of this project lives in [`icons/`](icons/README.md). The build first deletes and regenerates `srcWeb\icons\.opt\` with SvgOpt (git-ignored), then compresses `.opt` into `web/icons` and the extension icons from `icons\ext` directly into `web/icons/ext`. The `web/**/*.br` files are embedded and served at their relative path, e.g. `icons/search.svg`.

## Contents

| Item | Description |
|---|---|
| [`icons/`](icons/README.md) | The general-purpose UI icons plus the colour, external and file extension icon sets |

## Relationships

- **Part of:** [SysWeaver.HttpServer.IconModule](../README.md)
- **Related:** [`srcData/`](../srcData/README.md) holds the icons the module loads itself (templates, folder icons, flags)
