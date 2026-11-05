# textEffects

[⬆ mediaView](../README.md)

> Entry page that renders stylised text, either one text or a collage of texts, optionally with an effect.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../../../README.md) |
| **Kind** | Embedded web assets (page) |

## Purpose

This folder holds no shaders of its own. [`index.html`](index.html) reads the text style (font, fill/stroke, gradients, shadow, margins) and the effect from the URL. With `Text`, it renders a single text using an effect from [`../imageEffects/`](../imageEffects/README.md) (default `DistortedTv`). With `Texts`, it renders a separated list of texts as a collage using an effect from [`../collageEffects/`](../collageEffects/README.md) (default `Stripes`). The result is shown in an iframe of `../MediaPreview.html` as media type 5 (Text). [`../TextDemo.html`](../TextDemo.html) builds its preview URLs against this page (`textEffects/?...`).

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | Builds text/effect parameters from the URL and shows the rendered text |

## Relationships

- **Part of:** [mediaView](../README.md) of [SysWeaver.MicroService.Media](../../../README.md)
- **Uses:** [`../imageEffects/`](../imageEffects/README.md), [`../collageEffects/`](../collageEffects/README.md), [`../effectPage.js`](../effectPage.js), [`../parseTools.js`](../parseTools.js), [`../MediaPreview.html`](../MediaPreview.html)
- **Used by:** [`../TextDemo.html`](../TextDemo.html)

## Behavior

`index.html` is a lean page (no SysWeaver core): it loads `../parseTools.js`, `../../common/anim.js` and the shared page logic [`../effectPage.js`](../effectPage.js) and declares `<body data-effect-page="text">`. `EffectPage.Main` (see the mediaView README) then:

1. **Mode.** `Texts` in the url selects the collage mode, else the single mode (`Text`).
2. **Defaults** (`EffectPage.TextDefaults`): single mode bold 300px Tahoma, `EffectSizeFromImage: true`, `AdaptiveSize: false`, `Text: "SysWeaver"`; collage mode bold 80px Tahoma, `EffectSizeFromImage: false`, `AdaptiveSize: true`, `Texts: "Hello,World,SysWeaver"`, `CollageSeparator: ","`, `CollageColumns: 2`; both with the fill / stroke colors, shadow, margins, `StrokeFirst`, `FontUrl`, `FontUrlName`, `DebugImage`, `Duration: 10`. Effect params: `EffectPage.EffectDefaults`.
3. **Effect.** `Effect` defaults to `DistortedTv` (single) or `Stripes` (collage); a bare name becomes `imageEffects/<name>.glsl` or `collageEffects/<name>.glsl` (relative to `mediaView/`, where the rendering iframe lives).
4. **Parameters.** The side file `../<effect>.json` (missing files are ignored), then the url; keys go to the text params, else to the effect params, else become shader variables. Keys are case sensitive; colors may be written `$rgb[a]` / `$rrggbb[aa]`; `null` sets null.
5. **Render.** An iframe of `../MediaPreview.html?transparent=true&link=<url encoded text>&type=5&props=<url encoded json>` (+ `&ShaderDebug=true`), with `ParseTools.MessageProxy` relaying the Media* commands into the player and its `PageLoaded` out; when the preview signals loaded the page also posts `LoaderRemoved` to the top window (the app shell reveals it at once).

Url parameters: `Text`, `Texts`, `Effect`, `ShaderDebug`; text style `Font`, `FontUrl`, `FontUrlName`, `LetterSpacing`, `Fill`, `FillGradient` (JSON), `Stroke`, `StrokeGradient` (JSON), `StrokeWidth`, `StrokeCap`, `ShadowFill`, `ShadowStroke`, `ShadowX`, `ShadowY`, `ShadowBlur`, `MarginLeft/Top/Right/Bottom`, `StrokeFirst`, `AttachTo`, `DebugImage`; image params `Duration`, `Crop`, `Transparent`, `AdaptiveSize`, `EffectSizeFromImage`; collage `CollageSeparator`, `CollageColumns`; effect params (Width, Height, Speed, ...); any other key is a shader `//var:` override.

## CSS reference (`index.html` inline style)

| Selector(s) | Description |
|---|---|
| `:root` | `color-scheme: normal !important` (the transparent page is not tinted by a dark scheme). |
| `html, body` | Transparent, no margin / padding, full size, no scroll. |
| `body > *`, `iframe` | Not interactive; the iframe is borderless and full size. |
