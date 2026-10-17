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

## Behavior (web analysis)

This section describes what the code in this folder does at runtime. The small `index.html` pages of the effect sub folders are covered here too, because they share the conventions below. Only `textEffects/` has its own behavior section, in [textEffects/README.md](textEffects/README.md).

### Loading and initialization

All pages use classic (non-module) scripts loaded with `defer` and are booted from `<body onload=...>`. The exception is `MapView.html`, which calls `main()` from an inline script at the end of `<body>`. No file in this folder is a `${...}` server template.

| Page | Boot function | Scripts loaded (in order) | Stylesheets |
|---|---|---|---|
| `MediaView.html` | `mediaViewMain()` (media.js) | `../common/common.js`, `../common/anim.js`, `../app/application.js`, `media.js` (which pulls in `../mediaPlayer/media_player.js` via `includeJs`) | `../common/theme.css`, `../app/application.css`, `media.css` |
| `MediaPreview.html` | `mediaPreviewMain()` (media.js) | `../common/anim.js`, `../common/common.js`, `media.js` (+ `media_player.js` via `includeJs`) | `../common/theme.css`, `media.css`; an inline style forces `color-scheme: normal` and a transparent background |
| `BackgroundEffectsLib.html`, `ImageEffectsLib.html`, `CollageEffectsLib.html` | inline `main()` | `../common/common.js`, `../mediaPlayer/media_player_effect.js`, `media_effect_program.js`, `media_player_tools.js` (no `media_player.js`) | `../common/theme.css` + inline card styles |
| `TextDemo.html` | inline `main()` | `../common/common.js`, `parseTools.js`, `../mediaPlayer/canvasTools.js` | `../common/theme.css` + inline |
| `MapDemo.html` | inline `main()` | `../common/common.js` | `../common/theme.css` + inline |
| `MapView.html` | inline `main()`, called immediately | none (stand-alone; defines its own `_T`/`_TF` stubs) | inline only |
| `backgroundEffects/index.html` | inline `main()` | `../parseTools.js`, `../../common/anim.js`, `../../mediaPlayer/media_player_tools.js`, `media_effect_program.js`, `media_player_effect.js` (no `common.js`) | inline |
| `imageEffects/index.html` | inline `main()` | same set as `backgroundEffects/index.html` | inline |
| `collageEffects/index.html` | inline `main()` | `../parseTools.js`, `../../common/anim.js` only; renders through an iframe of `../MediaPreview.html` | inline |
| `counterEffects/index.html` | `textEffectMain()` (textEffect.js) | `../../common/common.js`, `../../common/anim.js`, `../canvasTools.js`, `../textEffect.js` (+ `media_player.js` via `includeJs`) | `../../common/theme.css`, `../textEffect.css`; preloads a Nunito font from fonts.gstatic.com |
| `textEffects/index.html` | inline `main()` | `../parseTools.js`, `../../common/anim.js`; renders through an iframe of `../MediaPreview.html` | inline |

`media.js` and `textEffect.js` both start with `includeJs(document.currentScript.src, "../mediaPlayer/media_player.js")` at parse time. `MediaPlayer` and `MediaTypes` therefore load asynchronously, and the code expects them to be ready when `onload` fires. On each `MediaPlayer.Create`, `media_player.js` lazily includes the player script for that media type, unless `window.MediaLoadAll` is set.

### Files

#### media.js

Global functions (no class, no module):

- **`mediaCreatePlayer(target, type, data, props, noControls, before, noStats, fit, aspectX, aspectY, alignX, alignY)`** → `[player, mediaControls, mediaStats]`.
  - Builds `<Media-Preview>`, which holds `<Media-AspectContainer>` (the clip) and a temporary `<Media-Cache>`. When it replaces an existing player, the element gets class `Replacing` and is inserted before `before`; otherwise it is appended to `target`.
  - Calls `await MediaPlayer.Create(type, data, props)`, appends `player.Element` to the clip, runs `await player.Cache()`, then adds `<Media-Overlay>`.
  - Defaults: `fit=true`, aspect 16:9, `alignX=0.5`, `alignY=0`. An aspect `<= 0` means "auto": the aspect comes from the media, or from the preview box when `AdaptiveSize` is set.
  - The inner `UpdateSize()` computes the clip box. With `AdaptiveSize` it sizes the player element to the box. Otherwise it sets `width/height` to `props.Width/Height` and applies a CSS `scale(..) translate(..)` transform that honors `player.ClipX/Y/Width/Height` and `player.UseClip`.
  - When the crop is not applied (`UseClip` false), the overlay gets class `Show` and semi-transparent red borders that show the cropped margins.
  - With `fit`, it positions `Media-Stats` (or `Media-Controls`) right under the media.
  - `UpdateSize()` re-runs from a `ResizeObserver` on `<Media-Preview>` and from `player.OnClipChanged`.
  - **Stats** (unless `noStats`): a `<Media-Stats>` element holding `<Media-Stat>` entries (`<Media-StatName>` + `<Media-StatValue>`).
    - Each entry is created by an inner `createStats(name, title, className)`, which returns a setter `setText(text, value)`. Calling it with no text hides the stat via class `Hide`.
    - Clicking a stat copies `name: text (value)` to the clipboard (`ValueFormat.copyToClipboard`).
    - Which stats appear depends on what the player supports:

      | Stat | Shown when | Class |
      |---|---|---|
      | Time, Duration | always | — |
      | GPU time | player has `AvgDrawTimeMs` and `Query` | — |
      | Build | player has `CompilationTime` (`-3` = error, other negatives = compiling) | — |
      | Volume | `HaveAudio` | — |
      | Width, Height | `HaveVisual` | — |
      | Trimmed time, Trimmed duration | `props.StartAt` exists | `Adjust` |
      | Cropped width/height/left/top | `props.Crop` exists | `Clip` |
      | Render width, Render height | `props.AdaptiveSize` exists | `Actual` |

    - Values update on every `requestAnimationFrame` until the stats element leaves the document.
  - **Controls** (unless `noControls`): a `<Media-Controls>` element with common.js `Button`s, rendered as `<Media-Button>` through `ButtonStyle.ButtonElement`.
    - If `player.CanPlay`: Pause, Play, Stop, Loop and Once. When `HaveAudio`, also Mute, Un mute and VOL 100/75/50/25 %.
    - Always: a "Source" button (`EditOrOpenUrl(player.Source)`).
    - `setButtonState()` toggles the mutually exclusive buttons.
    - The controls element is assigned to `player.MessageElement` and listens for `mediaEnded`.
  - Adds three members to the player: `player.SetButtonState`, `player.UpdateSize` and `player.TopElement` (the `<Media-Preview>`).
- **`mediaPlayWithPopup(player)`**: applies the saved loop, volume and mute preferences (see State) and starts playback. Browsers may block un-muting without a user gesture (checked with `MediaPlayerTools.CanUnMute()`). In that case, unless the user chose mute, it shows a full-screen `<Media-NoAudio>` overlay ("Must click once to get audio!") that un-mutes and plays on click.
- **`mediaEditProps(target, playerRes, type, data, props, canEdit, saveFn, messagePrefix)`**: property editor shown below the player.
  - It does nothing unless `props` has more than one key.
  - Forces `props.Crop.VisualizeMargins = false` and fetches type info with `sendRequest('../edit/GetTypeInfo', props["$type"])`.
  - Effect players expose `ScriptTypeInfo`/`ScriptMember` (the shader's `//var:` constants). When present, it registers that type and adds an `EffectProps` member.
  - Creates an `Edit` from the Edit service inside `<Media-Edit>`, with `EditOptions.ReadOnly = !canEdit` and an `UndoStack`.
  - When editable, adds "Save changes" (only if `saveFn` is given) and "Apply changes" buttons in a `<SysWeaver-CenterBlock>`.
  - The inner `reload()` rebuilds the player:
    1. Calls `mediaCreatePlayer(..., before = old TopElement)`.
    2. Swaps the controls and stats elements.
    3. Restores volume, mute, loop, play state and position.
    4. Removes the old element and calls `oldP.OnDispose()`.
  - On the Edit `EditChange` event, these members are applied live, without a reload:
    - `Volume`: `ResetVolume`.
    - `Speed`.
    - `AdaptiveSize`: `UpdateSize`.
    - `Crop`: `UseClip` + `ApplyClip` + `UpdateSize`.
    - `EffectProps`: `LazyCompile`, and mirrors `props.FxProps = JSON.stringify(EffectProps)`.
  - Any other change enables "Apply changes" when the props JSON (ignoring `Volume`) differs from the applied state.
  - Each live change is also broadcast with `InterOp.Post(messagePrefix + <Name>, ...)` (see State).
- **`mediaEditMain()`**: boot function for an embeddable "assignment media editor" page. No page in this repository calls it; an external host page uses it.
  - URL params: `get` (API that reads the media info), `aid` (JSON assignment id) and optional `update` (API name). If `update` is missing it is derived from `get`: `MediaGetAssignedInfo` becomes `MediaSet…Data`, otherwise `Get` becomes `Update`.
  - Calls `sendRequest(getApi, aid)` and expects `{Data, Type, Link, Edit}`.
  - Creates the player and two buttons:
    - Collapse/Expand: hides the player and pauses it, or shows it and resumes.
    - New window: opens `MediaView.html?type=&link=&props=&ctrl=<messagePrefix>`.
  - Calls `mediaEditProps` with a save function that posts `{AssignmentId|...aid, Data}` to `'../quizz/' + updateApi`.
  - `canEdit` is `(info.Edit & QuizzRights.UpdatePrivateData) !== 0`.
  - `messagePrefix` is `YaMD5.hashAsciiStr(getApi + "_" + aid + "_" + type + "_" + data)`.
  - Answers `<prefix>GetProps` messages with `<prefix>SetProps`, and posts an initial `<prefix>Reload`.
- **`SsHack`**: a stand-in for the `ScreenShotHost` object that only logs to the console (`isSupported`, `setSize`, `doIt`). Enabled by the `hack` URL parameter on `MediaPreview.html`.
- **`mediaGetTypeFromUrl(url)`**: maps a file extension to `MediaTypes.Image`, `Video` or `Audio` (using common.js `isImageExt/isVideoExt/isAudioExt`), or to `Effect` for `.glsl`. Returns `-1` otherwise.
- **`mediaViewMain()`** (`MediaView.html`):
  - URL params: `link`/`d`, `type`/`t` (derived from the extension if missing), `props` (JSON), `no_stats`, `no_controls`, `ctrl`.
  - With `ctrl`, it first asks the opener for the current props: it posts `<ctrl>GetProps` and waits up to 100 ms for `<ctrl>SetProps`.
  - Builds `<Media-View>`, a CSS grid with rows for preview, stats and controls.
  - Calls `mediaCreatePlayer(mview, ..., fit=false, aspect 0:0 (auto), align 0.5/0.5)`.
  - Clicking the media opens `MediaPreview.html?full&back&pos=<pos>&type&link&props` in the same tab.
  - With `ctrl`, it listens for `<ctrl>*` messages and copies matching fields into `props`, then acts on the name:
    - `Reload`: rebuilds the player.
    - `SetVolume`: `ResetVolume`.
    - `UpdateSize`: `UpdateSize`.
    - `Crop`: re-applies the clip.
    - `Compile`: `LazyCompile`.
  - Finishes with `mediaPlayWithPopup` and `PageLoaded()`.
- **`mediaPreviewMain()`** (`MediaPreview.html`): a bare player that fills the body (class `MediaPreview`).
  - Looks for a screenshot host, in this order:
    1. `window.ScreenShotHost` (with CefSharp, after `CefSharp.BindObjectAsync()`).
    2. WebView2 `chrome.webview.hostObjects.ScreenShotHost`.
    3. `SsHack`, when `hack` is set.
  - With a host, the body gets class `NoFade` (checkerboard background, no fade transitions) and the player uses fill mode.
  - URL params:
    - `fill`: fill instead of fit.
    - `full`: enter `FullScreen`, and re-enter on the first click or key.
    - `back`: a click calls `history.go(-1)`.
    - `pos`: seek position.
    - `link`/`d`, `type`/`t`, `props`.
  - Playback:
    1. Creates the player directly with `MediaPlayer.Create` and calls `Cache(true)`.
    2. Sizes it with `MediaPlayerTools.UpdateSize(target, player, fill)` under a `ResizeObserver`.
    3. Un-mutes if allowed, otherwise on the first interaction.
    4. Plays and calls `Show()`.
    5. Wires `MediaPlayerTools.SetupMediaMessages(player)`, which handles window `message` commands such as `MediaPlay`, `MediaPause` and `MediaSeek`.
  - Screenshot protocol:
    1. Resizes the host window to the media's clipped size, or to its aspect ratio when `AdaptiveSize` is set: calls `shost.setSize(w, h)`, then waits for `resize`.
    2. After playback starts: if the player has `ResetCounters()`, calls it and waits 600 ms to measure.
    3. Pauses and seeks back to `pos`.
    4. Calls `shost.doIt(duration, fps, error, compilationTime)`, where fps = `1000 * MeasureCount / TotalTime`.
    5. On error, calls `doIt(0, 0, message, 0)` instead.

#### media.css

Styles for the custom elements built by media.js:

| Element or selector | Purpose and modifier classes |
|---|---|
| `Media-Full` | Editor page; `.Hide` collapses preview, stats and controls |
| `Media-NoAudio` | Full-screen "click for audio" overlay |
| `Media-Preview` | 16:9 box sized from the viewport; `.Replacing` moves it off-screen while a new player loads; `.NoVisual` collapses it for audio |
| `Media-AspectContainer` | `.MediaPreviewFit` / `.MediaPreviewFill`; children are absolutely transformed from the top-left; `.MediaHidden` children are hidden |
| `Media-Overlay` | `.Show` draws a red outline for crop visualization |
| `Media-Stats` / `Media-Stat` | `.Adjust` green, `.Clip` blue, `.Actual` magenta, `.Hide` |
| `Media-Controls` / `Media-Button` | `.Pressed`, `.Disabled` |
| `Media-Edit` | Container for the property editor |
| `Media-View` | 3-row grid used by `MediaView.html` |
| `body.MediaPreview` | Bare preview page; `.NoFade` checkerboard; `> .MediaHidden` |
| Icon mask classes | `MediaIconPlay/Pause/Stop/Mute/UnMute/VolLoud/VolLow/Expand/Loop/Once`, `IconSave`, `IconApply`, `IconMediaNewWindow`, `IconMediaCollapse`, `IconMediaExpand`, pointing at `../icons/*.svg` |

It relies on theme variables from `common/theme.css`: `--ThemeMain*`, `--ThemeAcc1`, `--ThemeBackground*`, `--ThemeRad` and `--ThemeFontMono`.

#### parseTools.js

`class ParseTools` has only static members. It is the generic "URL/JSON → typed params object" mapper used by the effect pages and `TextDemo.html`:

- **`DecodeObject(val)`**:
  - `$rgb`, `$rgba`, `$rrggbb` and `$rrggbbaa` become `{Red, Green, Blue, Alpha}` (0..1 floats).
  - Strings starting with `{` or `[` are parsed with `JSON.parse`.
  - Anything else is returned as the string.
- **`SetToObject(obj, key, val, onNotFound, logPrefix)`**: assigns `val` according to the type of the *existing* member `obj[key]`, and logs each assignment to the console:
  - `null` value: sets null.
  - Missing member: calls `onNotFound(key, val)`, or warns if no callback is given.
  - Object member: `DecodeObject`.
  - String member: the value, with a leading `$` turned into `#`, so colors can be passed in URLs without `#`.
  - Boolean member: `"true"` or `"1"` is true.
  - Number member: `parseFloat`; ignored if NaN.
- **`ParamsToObject(searchParams, dest, onNotFound, logPrefix)`**: applies every URL parameter with `SetToObject`; the literal `"null"` becomes null. Parameter names are case-sensitive and must match member names (PascalCase).
- **`ObjectToObject(src, dest, ...)`**: the same for the members of a plain object; values are converted to strings first.
- **`ObjectToParams(src)`**: the reverse, object → query string. Objects are JSON-encoded and falsy numbers and booleans are normalized.
- **`FileToObject(srcUrl, dest, ...)`**: fetches a `.json` side file with `fetch` and applies it with `ObjectToObject`. A missing file is ignored silently.
- **`MessageProxy(iframe)`**: sets `window.onmessage` to forward messages from the child iframe to `window.parent`, and messages from anywhere else into the iframe. This lets an outer host control a player nested two frames deep.

The pages chain three targets with nested `onNotFound` callbacks. A key is tried on the media params (`props`) first, then on the effect params (`ep`). If it is still unknown, it becomes an effect-specific shader variable in `fxParams`, decoded with `DecodeObject`.

#### textEffect.js / textEffect.css

- **`class CounterEffectParams`**: `Fade`, `ExtraSpacing`, `MaxDigits`, `FlipSpeed`, `FlipFraction`, `DecimalDigits`, `ThousandsSeparator`, `DecimalSeparator`, `Suffix`, `Prefix`, `DigitSpacing`.
- **`TextEffect.CreateTextEffect(effectName = "Rain", text, textStyle, effectParams)`**:
  1. If `textStyle.FontUrl` is set, loads it as a `FontFace`.
  2. Renders the text to an image URL with `CanvasTools.CreateTextImageUrl`.
  3. Creates an Image player with `Effect = "imageEffects/<name>.glsl"` and transparent effect params.
- **`TextEffect.CreateCounterEffect(effectName = "ScrollUp", textStyle, counterParams, effectParams)`**:
  1. Renders a digit atlas with `CanvasTools.CreateNumberImageUrl`: digits 0-9, blank, separators, prefix and suffix, stacked vertically in 11 rows.
  2. Computes the part layout from right to left: suffix, decimals, decimal separator, integer digits with thousands separators, prefix, and an end marker.
  3. Creates an Image player using `../counterEffects/<name>.glsl` with a fixed `Width/Height`, and sets the shader vars `MaxParts`, `UvScale`, `UvAdd` and `Fade` through `FxProps`.
  4. Hooks `player.OnRender(gl, p)`. Each frame it reads the value from `player.GetValue(time)` and computes digit positions and roll fractions. `GetValue` defaults to the identity and may return `[value, flipSpeed]`.
  5. Uploads the results to the shader uniforms `digits` (vec4 array: x position, atlas row/11, UV scale, UV offset) and `digitWidths`.
  - Part type codes: 0 digit, 16 blank, 17 thousands separator, 18 prefix, 19 suffix, 20 decimal separator, -1 end.
  - Fires `player.OnNewSize(newWidth, oldWidth)` when the visible width changes.
- **`textEffectMain()`**: a hard-coded demo used by `counterEffects/index.html`. It shows a gold `TextStyle` "SEK …:-" counter animated by `GetValue = x => [(990 + x^1.6) * 100, 500]`, with a `CanvasTools.CreateTextGrid` of greetings appended below.
- **`textEffect.css`**: makes body children block elements at full width with `object-fit: contain`.

#### Effect gallery pages (`BackgroundEffectsLib.html`, `ImageEffectsLib.html`, `CollageEffectsLib.html`)

The three pages are copies of the same code. They differ only in `GetApi`, `Folder`, the effect kind and the texture.

1. `getRequest("Api/Get<Kind>Effects")` returns the list of effect names.
2. For each name, `MediaPlayerEffect.GetImage(kind, Folder + "/" + name + ".glsl", mp, createTexture)` renders a still at 960×540 and returns `[imageDataUrl, compilationMs, avgDrawMs]`.
3. The page builds an `<Effect-Card>` with `<Effect-Title>`, `<Effect-Info>` (the timings, via `_T`) and an `<img>`.

Clicking the image opens `Folder/?Effect=<name>` in the same tab. Image effects add `&Image=../logo.png` and collage effects add `&Image=DemoCollage.svg`. `ValueFormat.copyOnClick` copies the same URL.

Textures:
- Image effects use `../logo.png` (`EffectSizeFromImage: true`).
- Collage effects use `DemoCollage.svg` and set `mp.FxProps` from `DemoCollage.json`.

`CollageEffectsLib.html` contains the most complete comment documenting the effect URL parameter vocabulary (see below). The inline `canvas.MediaHidden` rule moves the off-screen render canvases out of view.

#### Effect folder pages (`backgroundEffects/`, `imageEffects/`, `collageEffects/`, `counterEffects/` `index.html`)

- **`backgroundEffects/index.html`** (default `Effect=SmoothBands`):
  1. Builds a literal shaped like `MediaPlayerParamsEffect`.
  2. Loads `../<effect>.json`, then the URL params, into it; unknown keys go to `fxParams`.
  3. Sets `props.FxProps` and creates `new MediaPlayerEffect(effect, props)` directly.
  4. Sizes the player with `MediaPlayerTools.UpdateSize(document.body, player)` and plays it.
  5. Wires `SetupMediaMessages` and posts `PageLoaded` to the parent or opener (`MediaPlayerTools.PostPageLoaded`).
  - A commented-out alternative renders through `MediaPreview.html` with type 4.
- **`imageEffects/index.html`** (defaults `Effect=DistortedTv`, `Image=../../logo.png`):
  - Uses three targets: the image params (`Crop`, `Duration`, `EffectSizeFromImage`, `Transparent`, `AdaptiveSize`), the effect params, and the shader vars.
  - Loads values in this order: `<effect>.json` (same folder, e.g. `3D-flag.json`), then `<image>.json`, then the URL.
  - Creates a `MediaPlayerEffect` directly in the page, with a `MediaPlayerImageTexture` for the image.
- **`collageEffects/index.html`** (defaults `Effect=Stripes`, `Image=DemoCollage.svg`):
  - Uses the same three-stage loading; side files are resolved with a `../` prefix.
  - Renders through an iframe of `../MediaPreview.html?transparent=true&link=<image>&type=0&props=<json>` (+ `ShaderDebug=true` when set), with `ParseTools.MessageProxy`.
- **`counterEffects/index.html`**: a shell page for `textEffectMain()`. It ignores URL parameters.

#### Map pages

**`MapView.html`** is a stand-alone page that shows a map in an iframe, with an optional pin and link overlay.

- **Map provider**:
  - Google Maps Embed API: `https://www.google.com/maps/embed/v1/view?key&center&zoom&language&maptype`.
  - OpenStreetMap embed: `https://www.openstreetmap.org/export/embed.html?bbox=...`. The bbox is approximated from `zoom` and the window size.
  - Without a `key` parameter, the page fetches `Api/GetGoogleMapsKey`. If no key is returned, it falls back to OpenStreetMap.
- **Dark variants** are CSS filters on the iframe. `body.Dark` applies invert + hue-rotate; `body.OpenDark` applies a stronger filter.
- **Pin**: an `<img>` centered on the viewport.
  - Offset by `--pinX`/`--pinY` and sized by `--pinHeight`. These CSS variables are set on `<html>`.
  - A table maps pin names to (file, anchor X, anchor Y, height), pointing at `map/*.svg`.
- **DPI**: `--dpi`/`--idpi` scale the iframe via `zoom`.
- **Overlay**: a transparent element covers the map. It is an `<a target=_blank>` when there is a link, or a `<Map-Title>` when there is only a title.
- **Interaction**: without `interactive=true`, the iframe gets `pointer-events: none` (`body.NoInteraction`).
- **Fade-in**: `body.Show` is added 100 ms after the iframe `load` event.
- **Screenshot host**: detected the same way as in `MediaPreview.html` (without the `hack` option). The page adds `NoFade` and calls `shost.doIt(0, 0, null)` 500 ms after load.
- **Errors** are written as the body text.

**`MapDemo.html`** is a gallery of `MapView.html` iframes.
- Each map sits in a `<Map-Card>`, grouped by `<Map-Section>`: map types, pin styles, pin sizes, zoom, DPI, links, languages and misc.
- The key comes from the `key` param or from `Api/GetGoogleMapsKey`. The `center` param defaults to London.
- Each card has a full-screen `ColorIcon` that opens the URL in the same tab, and copies the URL on click.

#### TextDemo.html

A gallery of `TextStyle` presets: fill/stroke, shadow, letter spacing, gradients, gold and neon.

- Each preset is rendered client-side with `CanvasTools.CreateTextImageUrl(text, style)` into an `<img>` inside a `<Map-Card>`. It reuses the map card CSS element names.
- Each card title has these icons:
  - Copy the style as JSON.
  - 5 image-effect icons (BadVHS, Rain, SliceGlitch, OldMovie, 3D-flag) that open `textEffects/?<style params>&Text=<text>&Effect=<fx>`.
  - 4 collage-effect icons (3d-tiles, Stripes, Scroll, Wheel) that open `textEffects/?<half-scale style>&Texts=<texts>&Effect=<fx>`.
- The style is serialized with `ParseTools.ObjectToParams`.
- URL params: `Text` (default `Abc123`) and `Texts` (comma separated; defaults to a list of greetings).

### Server interaction

**Endpoints.** MediaService is published with `[WebApiUrl("../mediaView/Api/")]`, so pages in this folder call it with relative `Api/...` URLs.

| Endpoint | Used by | Notes |
|---|---|---|
| `Api/GetBackgroundEffects`, `Api/GetImageEffects`, `Api/GetCollageEffects`, `Api/GetCounterEffects` | `*EffectsLib.html` (`getRequest`) | Return the sorted effect names (file name without `.glsl`) of the endpoints published under `mediaView/<kind>Effects`. Client cache 30 s, server request cache 28 s. `GetCounterEffects` has no web consumer here. |
| `Api/GetGoogleMapsKey` | `MapView.html`, `MapDemo.html` (raw `fetch`) | `Roles.Dev` auth, statically cached; returns `null` if no key is configured. |
| `../edit/GetTypeInfo` | `mediaEditProps` (`sendRequest`) | Edit service type info for `props.$type`. |
| `<get>` / `../quizz/<update>` | `mediaEditMain` | API names supplied by the host (QuizzWeaver-style assignment media). |

**C# registration**:
- `MediaService` registers `mediaView/MediaView.html?link={0}` as the extension viewer for png, gif, jpg, jpeg, avif, webp, tiff, tif, svg, jfif, webm, mp4, ogg, mov, mp3, wav, aac and glsl. The page derives `type` from the extension.
- Dev-only menu entries (`WebMenuEmbedded`, `Roles.Dev`) under *Debug/Media* open BackgroundEffectsLib, ImageEffectsLib, CollageEffectsLib, TextDemo and MapDemo.

**URL parameter vocabulary**

- **`MediaView.html`**: keys are read through common.js `getUrlParams()`, which lowercases them, so they are case-insensitive.
  - `link` | `d`: media URL or data. Required.
  - `type` | `t`: MediaTypes int (0 Image, 1 Video, 2 Audio, 3 YouTube, 4 Effect, 5 Text). Derived from the extension if missing.
  - `props`: JSON of the type-specific params.
  - `no_stats`, `no_controls`: presence flags.
  - `ctrl`: InterOp message prefix that links the page to an opener editor.
- **`MediaPreview.html`** (case-insensitive):
  - `link` | `d`, `type` | `t`, `props`: as for `MediaView.html`.
  - `pos`: seconds. The page seeks there before playing and seeks back there after the screenshot.
  - `fill`: fill instead of fit.
  - `full`: fullscreen.
  - `back`: a click goes back in history.
  - `hack`: use `SsHack`.
  - Callers also pass `transparent=true`, which media.js does not read.
  - `ShaderDebug=true` is read from the page URL by `media_effect_program.js`.
- **`mediaEditMain` host page**: `get`, `aid` (JSON), `update`.
- **Effect pages** (`backgroundEffects/`, `imageEffects/`, `collageEffects/`, `textEffects/`). Keys are case-sensitive PascalCase.
  - `Effect`: an effect name or a `.glsl` URL.
  - `Image`: image and collage pages only.
  - `ShaderDebug`: iframe-based pages only.
  - Any member of the image params: `Crop`, `Duration`, `EffectSizeFromImage`, `Transparent`, `AdaptiveSize`.
  - Any member of the effect params: `Width`, `Height`, `Speed`, `LimitSize`, `DpiAdjust`, `DpiScale`, `Static`, `AdaptiveSize`, `WrapU`, `WrapY`, `WebGL2`, `MipMap`, `AntiAlias`, `ScrollId`, `MouseId`, `MouseEnable`, `ImageSize`, `ShowVars`, `ForceMode`, `DarkModeSwitch`, `CustomX/Y/Z`, `CustomAnimX/Y/Z`.
  - Any other key becomes a shader variable (`FxProps`). Examples are the atlas keys `TileCountX`, `TileCountY`, `TileCount`, `TileWidth`, `TileHeight`, `PageWidth`, `PageHeight`, `ImageWidth`, `ImageHeight`, `UniqueCount`, `Border`, and the per-effect settings documented in the comment in `CollageEffectsLib.html`.
  - Value formats: the literal `null`, `$rgb[a]`/`$rrggbb[aa]` colors, and JSON for objects.
  - A key that exists in both sets (e.g. `AdaptiveSize`) lands in the image params first; the "fix parameters" step then copies it to the effect params.
- **`MapView.html`**. Keys are case-sensitive.
  - `maptype`:
    - `roadmap` (default), `satellite` and other values are passed through to Google.
    - `dark`: Google roadmap with a CSS dark filter.
    - `open`, `openstreet`, `openstreetmap`: OpenStreetMap.
    - `opendark`, `darkstreet`, `openstreetdark`, `openstreetmapdark`: OpenStreetMap with a CSS dark filter.
  - `key`: Google Maps API key.
  - `center`: `lat,lng`. Required. Non-numeric characters are stripped; a trailing `S`/`W` negates the value.
  - `zoom`: default 16.
  - `language` | `lang`: default `en`.
  - `dpi`: percent, default 100.
  - `interactive`: `true` allows interaction with the map.
  - `link`: `true`/`view`, `dir`, or any URL. Empty, `false` or `null` means no link.
  - `title`: tooltip text.
  - `pin`: `true`, `star`, `green-pennant`, `blue-medieval`, `game-flag`, `green-static-flag`, `red-/green-/blue-flag`, `red-/green-/blue-needle`, `red-/green-/blue-marker`, or any image URL.
  - `pinX`, `pinY`: pin anchor, as CSS lengths or percentages.
  - `pinHeight`: pin height in px.
- **`MapDemo.html`**: `key`, `center`.
- **`TextDemo.html`**: `Text`, `Texts`.

**`.json` side files**:
- For an effect `X.glsl`, the page reads `X.json` next to it. Example: `imageEffects/3D-flag.json` sets `EffectSizeFromImage: false` and `AdaptiveSize: true`.
- For an image `Y.png`, the page reads `Y.json` next to it. Example: `DemoCollage.json` holds the tile atlas layout.
- Side files are fetched with `ParseTools.FileToObject`; a 404 is ignored silently. They are applied before the URL parameters, so the URL wins.
- `CollageEffectsLib.html` loads `DemoCollage.json` directly as `FxProps`.

**Templated values:** none in this folder.

**External services:** Google Maps Embed API, OpenStreetMap embed, google.com/maps links, and fonts.gstatic.com (font preload on the counter page).

### State and persistence

- **`localStorage`** (media.js). The control buttons write these keys and `mediaPlayWithPopup` reads them:
  - `QuizzWeaver.MediaMute`: `"true"`/`"false"`.
  - `QuizzWeaver.MediaLoop`: `"true"`/`"false"`.
  - `QuizzWeaver.MediaVolume`: `"1"`, `"0.75"`, `"0.5"` or `"0.25"`; default 0.25.
- **Window globals**:
  - `window.sysweaverParams`: common.js cache of the lowercased URL params.
  - `window.ScreenShotHost`: set by `mediaPreviewMain` once a host is detected.
  - `window.IncMap`: `includeJs` cache.
  - `window.ShaderDebug`: read by the effect program.
- **InterOp messaging (editor ↔ detached viewer)**: `mediaEditMain` and a `MediaView.html?ctrl=<prefix>` window share an MD5 prefix and exchange `InterOp.Post` messages. The transport is common.js BroadcastChannel/window messaging, and messages are tagged with `MagicXyz: "SysWeaver"`.

  | Message | Direction | Payload and effect |
  |---|---|---|
  | `<prefix>GetProps` | viewer → editor | Asks for the current props |
  | `<prefix>SetProps` | editor → viewer | Full props, or `{Speed}` on a live edit |
  | `<prefix>Reload` | editor → viewer | Full props; the viewer rebuilds the player |
  | `<prefix>SetVolume` | editor → viewer | `{Volume}` |
  | `<prefix>UpdateSize` | editor → viewer | `{AdaptiveSize}` |
  | `<prefix>Crop` | editor → viewer | `{Crop}` |
  | `<prefix>Compile` | editor → viewer | `{EffectProps}` |

  The viewer copies every message field that already exists in its `props`.
- **Window `postMessage` (player control)**:
  - `MediaPreview.html` and the effect index pages listen through `MediaPlayerTools.SetupMediaMessages` for `{Type: ...}` messages: `MediaPlay`, `MediaPause`, `MediaStop`, `MediaOnce`, `MediaLoop`, `MediaMute`, `MediaUnMute`, `MediaSetVolume`, `MediaSeek`, `MediaDebugLoseContext`, `MediaCustomX/Y/Z`.
  - Pages post `PageLoaded` to the parent, through `PageLoaded()` in common.js or `MediaPlayerTools.PostPageLoaded()`.
  - Pages that wrap an iframe (`collageEffects/`, `textEffects/`) relay messages in both directions with `ParseTools.MessageProxy`.
- **Screenshot host contract**: SysWeaver.MicroService.Thumbnail.Web injects a `ScreenShotHost` JS object (see `ThumbnailWebService_Internal.cs`) with `isSupported()`, `setSize(width, height)` and `doIt(duration, fps, error, loadTime)`.
- **Server-side state**: nothing is written to the server except through the save API of `mediaEditMain`.

### Dependencies

- **common.js** (HttpServer):
  - Functions: `includeJs`, `getUrlParams`, `waitEvent`, `sendRequest`, `getRequest`, `Fail`, `Info`, `PageLoaded`, `AddLoading`, `Open`, `EditOrOpenUrl`, `badClick`, `keyboardClick`, `isInDocument`, `getFileExtension`, `isImageExt/isVideoExt/isAudioExt`, `_T`/`_TF`.
  - Classes: `Button`, `ButtonStyle`, `ColorIcon`, `ValueFormat` (`copyToClipboard`, `copyOnClick`, `copyToClipboardInfo`), `FullScreen`, `InterOp`.
  - Also used: theme variables from `theme.css` and icons from `../icons/`. Most pages load `anim.js`, which the players use.
- **app/application.js/.css**: loaded by `MediaView.html` only.
- **Edit service, `YaMD5` and `QuizzRights`**: only `mediaEditProps`/`mediaEditMain` use them.
  - Edit service: `edit/Edit.js`, `EditOptions`, `UndoStack`, `../edit/GetTypeInfo`.
  - `YaMD5` comes from `common/md5.js`.
  - `QuizzRights` is not defined anywhere in this repository.
  - No page in this folder loads any of them, so the host page must provide them.
- **mediaPlayer/**:
  - Factory and helpers: `MediaPlayer.Create`, `MediaTypes`, `MediaPlayerTools` (`UpdateSize`, `CanUnMute`, `delay`, `SetupMediaMessages`, `PostPageLoaded`).
  - Player instance API:
    - Playback: `Element`, `Params`, `Cache`, `Play/Pause/Stop/Loop/Once/Mute/UnMute/SetVolume/ResetVolume/Seek/GetPos/Show`.
    - State: `Width/Height/Duration/Volume/Muted/Paused/Looping/CanPlay/HaveAudio/HaveVisual/Source`.
    - Clipping: `UseClip/ClipX/ClipY/ClipWidth/ClipHeight/ApplyClip/OnClipChanged`, `AspectRatio`.
    - Callbacks: `OnEnded/OnLoop/OnDispose`.
    - Effect players only: `CompilationTime/AvgDrawTimeMs/Query/TotalTime/MeasureCount/ResetCounters/LazyCompile/ScriptTypeInfo/ScriptMember/Program/GL/OnRender`.
  - Classes: `MediaPlayerEffect` (+ static `GetImage`), `MediaPlayerImageTexture`, `MediaPlayerParamsEffect`, `MediaPlayerParamsImage`, `CanvasTools`, `TextStyle`, `TextLinearGradient`.
- **Consumers**:
  - SysWeaver.MicroService.Thumbnail.Web builds `mediaView/MediaPreview.html?type&link&pos&props` and `mediaView/MapView.html?key&center&zoom&language&maptype&dpi&pin&pinX&pinY...`, and renders them in a headless browser with an injected `ScreenShotHost`.
  - SysWeaver.MicroService.MediaThumbnail builds `mediaView/MediaPreview.html?pos&type&link`.
  - The server's extension viewer mechanism opens `MediaView.html?link=` for media files.
  - The dev menu opens the gallery and demo pages.
  - External host pages (QuizzWeaver) call `mediaEditMain`.

**GLSL effect file conventions** (all `*Effects/` folders). The effects are plain WebGL1 fragment shaders.

- **Standard uniforms** filled by the effect player:
  - Always available: `time` (float) and `resolution` (vec2).
  - As needed: `tex` (sampler2D input image or atlas; image, collage and counter effects), `mouse` (vec2), `scroll` (vec4), `date` (vec4).
  - Counter effects also use `digits[64]` (vec4) and `digitWidths[64]` (float), set from `OnRender`.
- **ShaderToy-style shaders** (with `mainImage` and no `main`) get a generated `main()` and the uniforms `iResolution/iTime/iTimeDelta/iFrameRate/iFrame/iMouse/iDate`.
- **Generated headers**: a precision header and the derivative/LOD extensions are added when missing.
- **Tunable parameters** are top-level `const` declarations followed by a `//var:` comment with optional relaxed JSON metadata: `name`, `desc`, `min`, `max`, `step`, and `type` (such as `colhdr`).
  - Example: `const int AMOUNT = 5; //var: { "min": 1, "max": 50, "name": "Amount", "desc": "..." }`.
  - These constants become editable members (`ScriptTypeInfo`). They can be overridden through `FxProps` (URL keys or side `.json` files).
- **Header comment**: ported shaders keep a header comment with the original source URL (shadertoy or glslsandbox).
- **Naming and listing**: effect names are the file names. The server lists them from the published endpoints.

### Structural observations

- **Module style**: global functions and classes in classic scripts, with no ES modules and no namespaces. `media.js` exposes about 8 globals; `textEffect.js` and `parseTools.js` define one main class each. Pages are wired by an `onload` attribute that names a global function.
- **Two parameter-parsing regimes**:
  - `media.js` uses common.js `getUrlParams()`: lowercased keys, explicit names, and `props` as one JSON blob.
  - The effect and text pages use raw `URLSearchParams` + `ParseTools`: case-sensitive PascalCase keys mapped onto default objects by member type, with unknown keys falling through to shader vars.
  - Thumbnail.Web and the galleries depend on both formats.
- **Duplicated code**:
  - The three `*EffectsLib.html` pages are near-identical (inline CSS and `main()`).
  - The four effect folder pages repeat the same `MediaPlayerParamsEffect` literal and the same three-stage load/fix logic.
  - The player-rebuild function `reload()` exists twice, in `mediaEditProps` and in `mediaViewMain`.
  - The `UpdateSize` logic in `mediaCreatePlayer` parallels `MediaPlayerTools.UpdateSize`.
  - Screenshot host detection exists in both `mediaPreviewMain` and `MapView.html`.
  - `TextDemo.html` reuses the `Map-Card`/`Map-Title` element names and CSS from `MapDemo.html`.
  - The default param literals in the effect pages mirror C# types (`MediaPlayerParamsEffect`, `MediaPlayerParamsImage`, `MediaPlayerParamsText`) by hand.
- **Two render paths for effects**:
  - `backgroundEffects/` and `imageEffects/` create `MediaPlayerEffect` in the page.
  - `collageEffects/` and `textEffects/` serialize the params into a `MediaPreview.html` iframe URL (`type=0` / `type=5`) and proxy messages.
  - Relative effect and image URLs therefore resolve against different base folders depending on the path. `imageEffects/` resolves `Effect` and side files in its own folder. The iframe pages prefix `collageEffects/` or `imageEffects/` relative to `mediaView/` and use `../` for side files.
- **Implicit contracts**:
  - The player object is extended ad hoc by `mediaCreatePlayer` (`SetButtonState`, `UpdateSize`, `TopElement`, `MessageElement`) and by `CreateCounterEffect` (`GetValue`, `OnNewSize`, `OnRender`).
  - The editor/viewer InterOp protocol is purely string-based, with a hashed prefix.
  - The `ScreenShotHost` interface is shared with C#.
  - The pin names in `MapView.html` and the `GoogleMapTypeNames` in Thumbnail.Web must stay in sync with this page.
  - A C# comment says `MediaExtensions` "should be synched with text.js".
- **Product coupling**: the localStorage keys (`QuizzWeaver.*`), `QuizzRights`, the `../quizz/` save endpoint and the `MediaGetAssignedInfo`/`MediaSet…Data` naming in `mediaEditMain` tie this generic framework folder to the QuizzWeaver application.
- **References that the hosting pages do not satisfy**:
  - `mediaEditMain` needs Edit, YaMD5 and QuizzRights from the host page.
  - `counterEffects/index.html` references `../canvasTools.js`, which resolves to `mediaView/` while `canvasTools.js` lives in `mediaPlayer/`. (`media_player.js` also includes it.)
  - `MapView.html` references `map/GreenStaticFlag.svg`, which is not in `map/`.
  - `TextEffect.CreateTextEffect` and `CreateCounterEffect` call the async `MediaPlayer.Create` without `await`.
  - `MediaView.html` loads `app/application.js`; `MediaPreview.html` does not.
- **Global leaks**: `collageEffects/index.html` and `textEffects/index.html` assign `url` without declaring it, which creates an implicit global.
- **Dead or commented code**:
  - A commented-out iframe path in `backgroundEffects/` and `imageEffects/`.
  - A commented-out click handler in `mediaViewMain`.
  - The `SsHack` debug class.
  - Demo values in `textEffectMain`.
