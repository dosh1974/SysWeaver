# collageEffects

[⬆ mediaView](../README.md)

> GLSL effects that animate a collage, an image made of a grid of tiles.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../../../README.md) |
| **Kind** | Embedded web assets (GLSL shaders) |

## Purpose

Collage shaders sample the collage image (`tex`) and animate its tiles using `time`, `resolution`, `mouse` and `scroll` uniforms. The `MediaService.GetCollageEffects` API lists them. The text effect page in [`../textEffects/`](../textEffects/README.md) also uses them when it renders several texts as a collage.

[`index.html`](index.html) reads `Effect` (default `Stripes`) and `Image` from the URL, merges optional `.json` side files for the effect and the image (a collage image comes with a tile layout like [`../DemoCollage.json`](../DemoCollage.json)), and shows the result in an iframe of `../MediaPreview.html` (media type 0, Image). [`../CollageEffectsLib.html`](../CollageEffectsLib.html) is the gallery.

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | Renders a collage image with a chosen collage effect |
| [`3d-tiles.glsl`](3d-tiles.glsl) | Tiles spread over several planes, zooming in 3D |
| [`Scroll.glsl`](Scroll.glsl) | Scrolling tiles |
| [`Stripes.glsl`](Stripes.glsl) | Tiles presented in stripes (the default) |
| [`Wheel.glsl`](Wheel.glsl) | Scrolling tiles with shadows and vignetting |

## Relationships

- **Part of:** [mediaView](../README.md) of [SysWeaver.MicroService.Media](../../../README.md)
- **Used by:** [`../textEffects/`](../textEffects/README.md), [`../CollageEffectsLib.html`](../CollageEffectsLib.html)
