# counterEffects

[⬆ mediaView](../README.md)

> GLSL effects for animated number counters and the page that hosts them.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../../../README.md) |
| **Kind** | Embedded web assets (GLSL shader + page) |

## Purpose

A counter shader gets the rendered digits as a texture (`tex`) together with per-digit rectangles and widths (`uniform vec4 digits[64]`, `uniform float digitWidths[64]`), and animates the digits as the value changes. The `MediaService.GetCounterEffects` API lists the available shaders. [`../textEffect.js`](../textEffect.js) loads `../counterEffects/<name>.glsl`, and `CounterEffectParams` holds the formatting options (digits, decimals, separators, prefix/suffix, flip speed).

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | Transparent page that runs `textEffectMain()` from [`../textEffect.js`](../textEffect.js) |
| [`ScrollUp.glsl`](ScrollUp.glsl) | Digits scroll upwards and fade at the top and bottom edges |

## Limitations and considerations

- `index.html` loads `canvasTools.js` only through `media_player.js` (in [`../../mediaPlayer/`](../../mediaPlayer/README.md)); `TextEffect.LoadPlayers()` loads the image and effect player scripts before the counter is created.

## Relationships

- **Part of:** [mediaView](../README.md) of [SysWeaver.MicroService.Media](../../../README.md)
- **Uses:** [`../textEffect.js`](../textEffect.js), [`../textEffect.css`](../textEffect.css), [`mediaPlayer/`](../../mediaPlayer/README.md)
