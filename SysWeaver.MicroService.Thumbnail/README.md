# SysWeaver.MicroService.Thumbnail

[⬆ SysWeaver overview](../README.md)

> Instant thumbnails for any file served from disc: append a size key such as `?Thumb64x64` to the file's URL.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Micro service (file transformer + module) |

## Purpose

Galleries and file browsers need previews. This service transforms requests for files served by the file module into thumbnails — images and video frames — and falls back to file-type icons for everything else.

## How it fits into SysWeaver

```mermaid
flowchart LR
  Req["GET /photos/a.jpg?Thumb64x64"] --> FM["FileHttpServerModule"]
  FM --> TS["ThumbnailService"]
  TS --> Img["image thumbnail"]
  TS --> Vid["video frame via FFmpeg"]
  TS --> Icon["file type icon fallback"]
```

## Key features

- Configurable size prefix and allowed resolutions (only listed sizes are produced and cached).
- Media information caching.

## Limitations and considerations

- Requires the icon module and a file module; only files served by the file module are covered.
- Unsupported formats get icons rather than previews.

## Using it

```json
{ "Type": "SysWeaver.MicroService.ThumbnailService, SysWeaver.MicroService.Thumbnail" }
```
List it after the icon service and a file module.

## Relationships

- **Project:** [`SysWeaver.MicroService.Thumbnail.csproj`](SysWeaver.MicroService.Thumbnail.csproj)
- **Builds on:** [SysWeaver.HttpServer.IconModule](../SysWeaver.HttpServer.IconModule/README.md), [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.Media](../SysWeaver.Media/README.md), [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
