# srcWeb

[⬆ SysWeaver.MicroService.Media](../README.md)

> Source of the embedded media web application: the player scripts and the viewer pages with their GLSL effect libraries.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Everything the browser needs to show media lives here. The folder has no files of its own, only the two sub folders below.

## How it is built

The `EmbedLocal` target in [`SysWeaver.MicroService.Media.csproj`](../SysWeaver.MicroService.Media.csproj) runs `Compress.exe -Methods br .\srcWeb\*+ .\web` before each build. This writes Brotli compressed copies of all files to the generated `web/` folder (not in git), and every `web/**/*.br` file is embedded as a resource. The files are then served relative to the site root, e.g. `mediaView/MediaView.html` or `mediaPlayer/media_player.js`. The pages also use shared scripts and styles (`common/common.js`, `common/anim.js`, `common/theme.css`, ...) that are provided by [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md), not by this project.

## Contents

| Item | Description |
|---|---|
| [`mediaPlayer/`](mediaPlayer/README.md) | JavaScript media player: image, video, audio, YouTube, text and WebGL effect players |
| [`mediaView/`](mediaView/README.md) | Media view/preview pages, map view, demos and the GLSL effect libraries |

## Relationships

- **Part of:** [SysWeaver.MicroService.Media](../README.md)
- **Uses:** shared web assets of [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md); the C# media types in [`Media/`](../Media/README.md) mirror the player parameters
