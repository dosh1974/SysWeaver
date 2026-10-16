# backgroundEffects

[⬆ mediaView](../README.md)

> Library of full screen GLSL background effects. These are procedural shaders that need no input image.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../../../README.md) |
| **Kind** | Embedded web assets (GLSL shaders) |

## Purpose

Each `.glsl` file is a WebGL fragment shader that animates over `time` and `resolution`. The `MediaService.GetBackgroundEffects` API lists the effects by file name without `.glsl`, so a new shader dropped in here shows up automatically. Tunable values are `const` declarations with a `//var:{...}` comment, which the effect player in [`../../mediaPlayer/media_effect_program.js`](../../mediaPlayer/media_effect_program.js) exposes as editable parameters. Some shaders keep comments that credit their original authors.

[`index.html`](index.html) renders one effect. It reads `Effect` (default `SmoothBands`) and the player/effect properties from the URL, optionally merges a `.json` side file with the same name as the effect, and shows the result in an iframe of `../MediaPreview.html` (media type 4, Effect). [`../BackgroundEffectsLib.html`](../BackgroundEffectsLib.html) shows all of them as a gallery.

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | Renders a single background effect selected by URL parameters |
| `*.glsl` (44 files) | Background effect shaders, e.g. [`Fire.glsl`](Fire.glsl), [`Matrix.glsl`](Matrix.glsl), [`Fireworks.glsl`](Fireworks.glsl), [`Clouds.glsl`](Clouds.glsl), [`Caustics.glsl`](Caustics.glsl), [`MetaBalls.glsl`](MetaBalls.glsl), [`HexWave.glsl`](HexWave.glsl), [`SmoothBands.glsl`](SmoothBands.glsl), [`SoftBokeh.glsl`](SoftBokeh.glsl), [`TwinklingTunnel.glsl`](TwinklingTunnel.glsl) |

Themes covered: clouds and sky (`2D-clouds`, `CloudFlight`, `Clouds`), bubbles and blobs (`Blobs`, `Bubbles`, `Bubbles2`, `ColorBubbles`, `LargeBubbles`, `MetaBalls`, `ChromaticBlob`), materials (`CarbonFabric`, `ChromeTiles`, `LiquidMetal`, `MetalPlates`, `MetallicVoronotRoses`, `Silk`), lights and waves (`Caustics`, `SlowCaustics`, `DancingLights`, `GlowingLines`, `LightRays`, `SoftWaves`, `SmoothBands`, `Flow`, `Pulse`), patterns (`Grid`, `HexPulse`, `HexWave`, `Lines`, `RainbowCells`, `SimpleTiles`, `SphereMandala`, `Spin`) and themed scenes (`Fire`, `Fireworks`, `Grass`, `Mario`, `Matrix`, `Meters`, `VerticalMeters`, `RaceBanner`, `ChaseRGB`, `TwinklingTunnel`, `SoftBokeh`).

## Relationships

- **Part of:** [mediaView](../README.md) of [SysWeaver.MicroService.Media](../../../README.md)
- **Uses:** [`mediaPlayer/`](../../mediaPlayer/README.md) (`media_player_effect.js`, `media_effect_program.js`, `media_player_tools.js`), [`../parseTools.js`](../parseTools.js)
