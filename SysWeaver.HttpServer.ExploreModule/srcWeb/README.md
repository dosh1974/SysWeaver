# srcWeb

[⬆ SysWeaver.HttpServer.ExploreModule](../README.md)

> Source of the explorer web pages embedded in the module and served under `explore/`.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.ExploreModule](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

The build step `Compress.exe -Methods br .\srcWeb\*+ .\web` compresses everything in this folder (Brotli) into the generated `web/` folder, whose `*.br` files are embedded as resources. The embedded files are served at their relative path, so `explore/table.html` here becomes the URL `explore/table.html` on the server.

## Contents

| Item | Description |
|---|---|
| [`explore/`](explore/README.md) | The explorer pages: end point list scripts, API page, table viewer, icon gallery and logo designer |

## Relationships

- **Part of:** [SysWeaver.HttpServer.ExploreModule](../README.md)
- **Related:** [`srcData/`](../srcData/README.md) holds the end point list page template that loads `explore/explore.js`
