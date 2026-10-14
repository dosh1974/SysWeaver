# SysWeaver.MicroService.MediaThumbnail

[⬆ SysWeaver overview](../README.md)

> Thumbnails for SVG images and GLSL effect files rendered by a headless browser, for media that cannot be thumbnailed server-side.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Micro service |

## Purpose

Some media can only be rendered faithfully by a browser — notably GLSL shader effects. This service plugs a browser-based renderer into `MediaInfo` of [SysWeaver.Media](../SysWeaver.Media/README.md), so file listings and the thumbnail service get icons for these files too.

## How it fits into SysWeaver

```mermaid
flowchart LR
  MI["MediaInfo<br/>.svg / .glsl"] --> Svc["MediaThumbnailService"]
  Svc --> Web["IThumbnailWebService<br/>Thumbnail.Web"]
  Web --> Page["mediaView/MediaPreview.html<br/>MicroService.Media"]
```

The service registers media info creators for the `svg` and `glsl` extensions. Local files are mapped to their URL through the file server module, and the preview page of [SysWeaver.MicroService.Media](../SysWeaver.MicroService.Media/README.md) is captured by the web thumbnail service ([SysWeaver.MicroService.Thumbnail.Web](../SysWeaver.MicroService.Thumbnail.Web/README.md)).

## Key components

- [`MediaThumbnailService`](MediaThumbnailService.cs) — the service and its parameters (`MediaThumbnailParams`: `MaxConcurrency`, `MediaUrlRoot`)

## Using it

```json
{ "Type": "SysWeaver.MicroService.MediaThumbnailService, SysWeaver.MicroService.MediaThumbnail" }
```

Requires an `IThumbnailWebService`, a `FileHttpServerModule` and an HTTP server in the same manifest, and the `MediaService` (for the preview page).

## Limitations and considerations

- Browser rendering is comparatively slow and resource hungry; concurrency is limited by `MaxConcurrency` and results are cached by `MediaInfo`
- Only files served by the file server module (or URLs) can be rendered

## Relationships

- **Project:** [`SysWeaver.MicroService.MediaThumbnail.csproj`](SysWeaver.MicroService.MediaThumbnail.csproj)
- **Builds on:** [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.Media](../SysWeaver.Media/README.md), [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
