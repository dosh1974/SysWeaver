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
- **Uses:** [`../imageEffects/`](../imageEffects/README.md), [`../collageEffects/`](../collageEffects/README.md), [`../parseTools.js`](../parseTools.js), [`../MediaPreview.html`](../MediaPreview.html)
- **Used by:** [`../TextDemo.html`](../TextDemo.html)

## Behavior (web analysis)

### Loading and initialization

`index.html` loads `../parseTools.js` and `../../common/anim.js` with `defer`, and boots from `<body onload="main()">` (an inline function). It does not load `common.js` or any player script. Rendering happens in a child iframe of `../MediaPreview.html`, which loads `common.js`, `media.js` and the media player. This page only translates its URL into a `MediaPreview.html` URL. It is not a server template.

### Files

**`index.html`**, inline `main()`:

1. **Mode.** `isCollage = URL has "Texts"`.
2. **Defaults.** Builds three plain objects:
   - `props`: a literal mirroring `MediaPlayerParamsText`, i.e. image params plus a `TextStyle`.
     - Single mode: `EffectSizeFromImage: true`, `AdaptiveSize: false`, `bold 300px Tahoma`, `Text: "SysWeaver"`.
     - Collage mode: `EffectSizeFromImage: false`, `AdaptiveSize: true`, `bold 80px Tahoma`, `Texts: "Hello,World,SysWeaver"`, `CollageSeparator: ','`, `CollageColumns: 2`.
     - Both modes include the fill/stroke colors and gradients, shadow, margins, `StrokeFirst`, `DebugImage` and `Duration: 10`.
   - `ep`: a literal mirroring `MediaPlayerParamsEffect` (`Width` 1920, `Height` 1080, `Speed`, `DpiAdjust`, ..., `CustomAnimZ`).
   - `fxParams`: an empty object for effect-specific shader variables.
3. **Effect resolution.** `Effect` defaults to `DistortedTv` (single) or `Stripes` (collage). Unless it already ends in `.glsl`, it becomes `imageEffects/<name>.glsl` or `collageEffects/<name>.glsl`, relative to `mediaView/` because the iframe page lives there.
4. **Side file.** `ParseTools.FileToObject("../" + effect without extension + ".json", props, ...)`. The `../` prefix is skipped for absolute `://` URLs. For example, `Effect=3D-flag` reads `../imageEffects/3D-flag.json`.
5. **URL parameters.** `ParseTools.ParamsToObject(searchParams, props, ...)`. Keys go through the same three-level fallback as the side file: the member of `props`, else the member of `ep`, else `fxParams[key] = ParseTools.DecodeObject(value)`.
6. **Fix-up.**
   - Sets `props.Effect`.
   - Takes the text from `props.Texts` or `props.Text` (default `"SysWeaver"`) and deletes both keys from `props`.
   - Nests the params: `props.EffectParams = ep`, `ep.FxProps = fxParams`.
   - Copies `Transparent` and `AdaptiveSize` from `props` to `ep`. `ep.AdaptiveSize` is forced to false when `EffectSizeFromImage` is set.
7. **Render.**
   - Builds the global `url = '../MediaPreview.html?transparent=true&link=' + text + '&type=5&props=' + encodeURIComponent(JSON.stringify(props))`. `type=5` is `MediaTypes.Text`.
   - Adds `&ShaderDebug=true` if requested and logs the URL.
   - Creates an `<iframe>` (full size, `pointer-events: none`), installs `ParseTools.MessageProxy(iframe)` and sets `src`.

**DOM and CSS**: only the iframe, styled inline. `html`/`body` are transparent with no margin and `overflow: hidden`, and `color-scheme: normal` is forced.

### Server interaction

- **Requests**: no API calls. The page fetches only the optional effect side file `mediaView/<kind>Effects/<Effect>.json`; a missing file is ignored silently.
- **URL parameters**. Keys are case-sensitive and must match member names exactly.
  - Basic:
    - `Text`: selects single mode and gives the text.
    - `Texts`: selects collage mode. Separated by `CollageSeparator` (default `,`).
    - `Effect`: an image effect name in single mode or a collage effect name in collage mode, or any `.glsl` URL.
    - `ShaderDebug=true`: forwarded to the iframe.
  - Style and image params (matched against the `props` literal): `Font`, `FontUrl`, `FontUrlName`, `LetterSpacing`, `Fill`, `FillGradient` (JSON), `Stroke`, `StrokeGradient` (JSON), `StrokeWidth`, `StrokeCap`, `ShadowFill`, `ShadowStroke`, `ShadowX`, `ShadowY`, `ShadowBlur`, `MarginLeft/Top/Right/Bottom`, `StrokeFirst`, `AttachTo`, `DebugImage`, `Duration`, `Crop`, `Transparent`, `AdaptiveSize`, `EffectSizeFromImage`. Collage mode adds `CollageSeparator` and `CollageColumns`.
  - Effect params: see the vocabulary in [../README.md](../README.md).
  - Any other key becomes a shader `//var:` override.
  - Colors may be written as `$rgb[a]`/`$rrggbb[aa]`, which becomes `#...` for string members. The literal `null` sets null.
- **Producer**: `../TextDemo.html` generates these URLs with `ParseTools.ObjectToParams(style)`.
- **Templated values**: none.

### State and persistence

- **Storage**: none.
- **Messaging**: `ParseTools.MessageProxy` replaces `window.onmessage`. Messages from the iframe go to `window.parent`, and messages from anywhere else go into the iframe. A host can therefore send `MediaPlay`/`MediaPause`/`MediaSeek`/... commands to the player inside `MediaPreview.html` and receive its `PageLoaded` notification.

### Dependencies

- **Uses**:
  - `../parseTools.js` (`ParseTools`).
  - `../MediaPreview.html` → `media.js` → `mediaPlayer/media_player.js`; the text player (`media_player_text.js`) renders the text to a canvas and applies the effect.
  - Effect shaders in `../imageEffects/` and `../collageEffects/`.
  - `anim.js` is loaded, but this page does not use it directly.
- **Consumers**: `../TextDemo.html` (icons per style/effect). As a plain URL it can also be embedded by any host page or rendered for thumbnails.

### Structural observations

- The page is a thin URL-to-URL adapter. All rendering is delegated to `MediaPreview.html` with media type 5, so it adds one iframe level and needs a message proxy.
- The `props`/`ep` default literals repeat the C# `MediaPlayerParamsText`/`MediaPlayerParamsEffect` shapes by hand. They are near-duplicates of the literals in `../imageEffects/index.html`, `../collageEffects/index.html` and `../backgroundEffects/index.html`. `ParseTools` only maps keys that exist in these literals, so the literals double as the accepted-parameter whitelist.
- The three-level fallback (props → effect params → shader vars) is expressed as nested `onNotFound` lambdas, repeated for the side file and for the URL.
- `url` is assigned without a declaration (implicit global). The `text` value is inserted into the `link` parameter without URL encoding, while `props` is encoded.
- The header comment lists only a subset of the effects (Rain, OldMovie, DistortedTv, BadVHS, VHS). The real set is whatever `.glsl` files exist in the target folder.

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function.

### `index.html` (inline script)

The only code in this folder. Loaded scripts: `../parseTools.js` (class `ParseTools`, documented in [../README.md](../README.md)) and `../../common/anim.js`; boot `<body onload="main()">`. Defines the global `main` and, at run time, the implicit global `url`.

#### Functions

| Function | Signature | Description |
|---|---|---|
| `main` | async `()` | Reads the URL params. The presence of `Texts` selects collage mode. Starts from a `MediaPlayerParamsText`-shaped `props` literal: in collage mode bold 80px Tahoma, `AdaptiveSize: true`, `EffectSizeFromImage: false`, `Texts`, `CollageSeparator` `,` and `CollageColumns` 2; otherwise bold 300px Tahoma, `EffectSizeFromImage: true` and `Text: "SysWeaver"`. Also uses a `MediaPlayerParamsEffect`-shaped `ep` literal (1920×1080, ...) and an empty `fxParams`. `Effect` defaults to `Stripes` (collage) or `DistortedTv`; a bare name is mapped to `collageEffects/<name>.glsl` or `imageEffects/<name>.glsl`. Applies the side file `../<effect>.json` (`ParseTools.FileToObject`), then the URL params (`ParseTools.ParamsToObject`), with nested `onNotFound` callbacks: keys unknown in `props` go to `ep` (`ParseTools.SetToObject`), keys unknown in `ep` go to `fxParams` (`ParseTools.DecodeObject`). Then sets `props.Effect`, takes the text from `Texts`/`Text` (default `SysWeaver`) and deletes both keys, sets `props.EffectParams = ep`, `ep.FxProps = fxParams`, copies `Transparent`/`AdaptiveSize` into `ep` (`AdaptiveSize` false when `EffectSizeFromImage`). Builds `url = '../MediaPreview.html?transparent=true&link=<text>&type=5&props=<encoded JSON>'` (plus `&ShaderDebug=true` when `ShaderDebug=true`), logs it, appends an `<iframe>` with that `src` and installs `ParseTools.MessageProxy(iframe)` to forward `postMessage` traffic between the parent and the iframe. |

The `onNotFound` arrow callbacks passed to `ParseTools` are described in `main`; they have no own entries.

## CSS reference

### `index.html` (inline style)

Makes the page a transparent full-size host for the `MediaPreview.html` iframe. Not templated; uses no theme variables.

| Selector(s) | Description |
|---|---|
| `:root` | `color-scheme: normal !important`, so the transparent page is not tinted by a dark color scheme. |
| `html, body` | Transparent background (`!important`), no margin/padding, 100% width/height, no scroll. |
| `iframe` | The `MediaPreview.html` iframe created by `main`: borderless, full size, `pointer-events: none`. |

No custom properties, animations or media queries.
