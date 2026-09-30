# SysWeaver.MicroService.MediaThumbnail

[⬆ SysWeaver overview](../README.md)

> Thumbnails for media items rendered by a web client (headless browser), for media that cannot be thumbnailed server-side.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Micro service |

## How it fits into SysWeaver

Requires a web thumbnail service ([SysWeaver.MicroService.Thumbnail.Web](../SysWeaver.MicroService.Thumbnail.Web/README.md)), which renders media pages in a headless browser and captures images.

## Limitations and considerations

- Browser rendering is comparatively slow and resource hungry; results should be cached.

## Relationships

- **Project:** [`SysWeaver.MicroService.MediaThumbnail.csproj`](SysWeaver.MicroService.MediaThumbnail.csproj)
- **Builds on:** [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.Media](../SysWeaver.Media/README.md), [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
