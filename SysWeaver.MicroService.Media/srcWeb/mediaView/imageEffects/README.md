# imageEffects

[⬆ mediaView](../README.md)

> GLSL post effects applied to an image, such as old TV, VHS, glitch, rain, a waving flag and a silver photo frame.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../../../README.md) |
| **Kind** | Embedded web assets (GLSL shaders) |

## Purpose

Image effect shaders sample an input texture (`tex`) and transform it over `time`. The `MediaService.GetImageEffects` API lists them. The text effect page in [`../textEffects/`](../textEffects/README.md) uses them for single texts. An effect can have a `.json` side file with the same name that sets default player properties ([`../effectPage.js`](../effectPage.js) loads it before the url parameters; none of the effects in this folder has one at the moment).

[`index.html`](index.html) reads `Effect` (default `DistortedTv`) and `Image` (default `../../logo.png`) from the URL and renders them directly with `MediaPlayerEffect` and `MediaPlayerImageTexture` (page logic: the shared [`../effectPage.js`](../effectPage.js), `data-effect-page="image"`). [`../ImageEffectsLib.html`](../ImageEffectsLib.html) is the gallery.

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | Renders an image with a chosen image effect |
| [`3D-flag.glsl`](3D-flag.glsl) | The image printed on a cloth flag waving in the wind |
| [`BadVHS.glsl`](BadVHS.glsl) | Heavily degraded VHS look |
| [`Bokeh.glsl`](Bokeh.glsl) | Bokeh style effect with optional noise and vignetting |
| [`Crt.glsl`](Crt.glsl) | CRT monitor look |
| [`DistortedTv.glsl`](DistortedTv.glsl) | Distorted television (the default) |
| [`Glitch.glsl`](Glitch.glsl) | Digital glitch |
| [`None.glsl`](None.glsl) | Pass-through, no effect |
| [`OldMovie.glsl`](OldMovie.glsl) | Old film look |
| [`Rain.glsl`](Rain.glsl) | Rain running over the image |
| [`Silver_Keepsake.glsl`](Silver_Keepsake.glsl) | The image in a silver photo frame standing on a glossy floor that fades into transparent fog |
| [`SliceGlitch.glsl`](SliceGlitch.glsl) | Glitch with displaced slices |
| [`VHS.glsl`](VHS.glsl) | VHS tape look |

## Relationships

- **Part of:** [mediaView](../README.md) of [SysWeaver.MicroService.Media](../../../README.md)
- **Uses:** [`mediaPlayer/`](../../mediaPlayer/README.md), [`../parseTools.js`](../parseTools.js)
- **Used by:** [`../textEffects/`](../textEffects/README.md), [`../ImageEffectsLib.html`](../ImageEffectsLib.html)
