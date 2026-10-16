# mediaView

[⬆ srcWeb](../README.md)

> The media viewer pages: the extension viewer, the preview page, the map view, the effect library and demo pages, and the GLSL effect folders.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

[`MediaView.html`](MediaView.html) is registered by `MediaService` as the extension viewer for image, video, audio and `.glsl` files (`mediaView/MediaView.html?link={0}`). [`MediaPreview.html`](MediaPreview.html) shows a single media item with no controls. The effect pages use it inside an iframe, and [SysWeaver.MicroService.Thumbnail.Web](../../../SysWeaver.MicroService.Thumbnail.Web/README.md) renders such pages to make thumbnails. The library pages (`*EffectsLib.html`) and the demos are reached from the developer-only *Debug/Media* menu entries declared on `MediaService`.

The service API is published under this folder (`[WebApiUrl("../mediaView/Api/")]`). `GetImageEffects`, `GetBackgroundEffects`, `GetCollageEffects` and `GetCounterEffects` list the `.glsl` files that the server has published from the matching effect folder, so to add an effect you only need to drop a new shader file into that folder.

## Contents

| Item | Description |
|---|---|
| [`backgroundEffects/`](backgroundEffects/README.md) | Full screen GLSL background effects (no input image) |
| [`collageEffects/`](collageEffects/README.md) | GLSL effects that animate a tiled collage image |
| [`counterEffects/`](counterEffects/README.md) | GLSL effects for animated number counters |
| [`imageEffects/`](imageEffects/README.md) | GLSL post effects applied to an image or text (CRT, VHS, rain, ...) |
| [`map/`](map/README.md) | SVG map pins used by `MapView.html` |
| [`textEffects/`](textEffects/README.md) | Entry page that renders stylised text with an image or collage effect |
| [`MediaView.html`](MediaView.html) | Media viewer/editor page (`mediaViewMain()` in `media.js`) |
| [`MediaPreview.html`](MediaPreview.html) | Bare preview of a single media item (`mediaPreviewMain()`), used in iframes and for thumbnails |
| [`media.js`](media.js) | Viewer logic: player creation, popup playback, property editing, view and preview entry points |
| [`media.css`](media.css) | Styles for the viewer pages |
| [`parseTools.js`](parseTools.js) | `ParseTools`: turns URL parameters and optional `.json` side files into player/effect parameters, decodes `$rgb` colours, proxies messages from iframes |
| [`textEffect.js`](textEffect.js) | `TextEffect` / `CounterEffectParams` and `textEffectMain()`, used by the counter effect page |
| [`textEffect.css`](textEffect.css) | Styles for the text/counter effect page |
| [`BackgroundEffectsLib.html`](BackgroundEffectsLib.html) | Gallery of all background effects (menu *Debug/Media/Background effects*) |
| [`ImageEffectsLib.html`](ImageEffectsLib.html) | Gallery of all image effects, shown on `../logo.png` |
| [`CollageEffectsLib.html`](CollageEffectsLib.html) | Gallery of all collage effects, shown on the demo collage |
| [`DemoCollage.svg`](DemoCollage.svg) | 3×3 demo collage image for the collage gallery |
| [`DemoCollage.json`](DemoCollage.json) | Tile layout (tile counts, sizes, border) that goes with `DemoCollage.svg` |
| [`TextDemo.html`](TextDemo.html) | Demo of stylised text rendered to an image, with effects |
| [`MapView.html`](MapView.html) | Map page using Google Maps (with a key) or OpenStreetMap, with optional pin |
| [`MapDemo.html`](MapDemo.html) | Demo of the map view (menu *Debug/Media/Google map demo*) |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.Media](../../README.md)
- **Uses:** the player scripts in [`mediaPlayer/`](../mediaPlayer/README.md), shared assets of [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md)
- **Used by:** [SysWeaver.MicroService.Thumbnail.Web](../../../SysWeaver.MicroService.Thumbnail.Web/README.md)
