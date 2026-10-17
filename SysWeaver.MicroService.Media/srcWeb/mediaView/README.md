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

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function; "global" means a classic-script top-level declaration (shared `window` scope, no modules).

### `media.js`

Media viewer/preview/editor logic. Loaded with `<script defer>` by `MediaView.html` (boot `mediaViewMain`) and `MediaPreview.html` (boot `mediaPreviewMain`). `mediaEditMain` is meant for an external host page; nothing in this repository calls it. At parse time it runs `includeJs(document.currentScript.src, "../mediaPlayer/media_player.js")`, so `MediaPlayer`, `MediaTypes` and `MediaPlayerTools` come from the mediaPlayer folder. It depends on common.js (`Fail`, `Info`, `Button`, `ButtonStyle`, `badClick`, `keyboardClick`, `ValueFormat`, `isInDocument`, `getUrlParams`, `sendRequest`, `waitEvent`, `Open`, `EditOrOpenUrl`, `InterOp`, `FullScreen`, `PageLoaded`, `getFileExtension`, `isImageExt`/`isVideoExt`/`isAudioExt`, `YaMD5`) and, for the editor, on the Edit service (`Edit`, `EditOptions`, `UndoStack`) and `QuizzRights`. All globals are plain functions plus the class `SsHack`.

#### Globals

| Name | Kind | Description |
|---|---|---|
| (top-level call) | immediately executed | `includeJs(document.currentScript.src, "../mediaPlayer/media_player.js")` loads the media player framework relative to this script. |
| `props` | implicit global | `mediaEditMain` assigns `props = info.Data` without a declaration, so it becomes `window.props`. |
| `objState` | implicit global | Assigned (`objState = JSON.stringify(props)`) by the save callback passed from `mediaEditMain`; the `let objState` inside `mediaEditProps` is a different, local variable. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `mediaCreatePlayer` | async `(target, type, data, props, noControls, before, noStats, fit = true, aspectX = 16, aspectY = 9, alignX = 0.5, alignY = 0)` → `[player, mediaControls, mediaStats]` | Creates `<Media-Preview>` (with `<Media-AspectContainer>` and a temporary `<Media-Cache>`), inserted before `before` (with class `Replacing`) or appended to `target`. `type`/`data`/`props` go to `MediaPlayer.Create`; on failure it calls `Fail` and returns `undefined`. Adds `NoVisual` when the player has no visual, caches the media, appends `<Media-Overlay>`, sets `MediaPreviewFit`/`MediaPreviewFill` on the clip and lays it out with inner `UpdateSize` (re-run by a `ResizeObserver` and `player.OnClipChanged`). `aspectX`/`aspectY <= 0` means automatic aspect. Unless `noStats`, appends a `<Media-Stats>` block to `target`; unless `noControls`, appends `<Media-Controls>` with buttons. Sets `player.SetButtonState`, `player.UpdateSize`, `player.TopElement` (the `<Media-Preview>`) and, with controls, `player.MessageElement`. Returns the player and the controls/stats elements (or `null`). |
| `mediaPlayWithPopup` | async `(player)` | Applies the stored preferences: `localStorage["QuizzWeaver.MediaLoop"]` (`"true"` → `Loop()`, else `Once()`), `QuizzWeaver.MediaVolume` (default `0.25`) and `QuizzWeaver.MediaMute`. Plays directly for media without audio, or when `MediaPlayerTools.CanUnMute()` is true or the user prefers mute. Otherwise appends a full-screen `<Media-NoAudio>` ("Must click once to get audio!") whose click un-mutes, plays and removes it. Always ends with `player.SetButtonState()`. |
| `mediaEditProps` | async `(target, playerRes, type, data, props, canEdit, saveFn, messagePrefix)` | Appends `<Media-Edit>` to `target` and, when `props` has more than one key, an Edit-service editor for it. `playerRes` is the array from `mediaCreatePlayer`; `type`/`data` are reused to rebuild the player; `canEdit` toggles read-only; `saveFn(reload)` is an async callback returning truthy on success (enables the "Save changes" button); `messagePrefix` (optional) prefixes `InterOp.Post` messages (`Reload`, `SetVolume`, `SetProps`, `UpdateSize`, `Crop`, `Compile`) for a remote `MediaView.html`. Fetches type info with `sendRequest('../edit/GetTypeInfo', props["$type"], false)`; for effect players it registers `player.ScriptTypeInfo` and adds `player.ScriptMember`. Listens for `EditChange` and applies Volume, Speed, AdaptiveSize, Crop and EffectProps live; other changes enable "Apply changes". |
| `mediaEditMain` | async `()` | Boot function of the assignment media editor. Appends `<SysWeaver-Page class="Media">` and `<Media-Full>` to the body; reads URL params `get`, `aid` (JSON) and `update` (derived from `get` when absent); loads `{Data, Type, Link, Edit}` with `sendRequest(getApi, aid)`; creates the player, a Collapse/Expand button (inner `toggleCollape`) and a "New window" button that opens `MediaView.html?...&ctrl=<messagePrefix>`; calls `mediaEditProps` with a save callback that posts to `'../quizz/' + updateApi`; plays with `mediaPlayWithPopup`; answers `<prefix>GetProps` messages with `<prefix>SetProps` and posts an initial `<prefix>Reload`. Sets `player.OnLoop`/`player.OnEnded` to console-logging wrappers. Calls `PageLoaded()` on success. |
| `mediaGetTypeFromUrl` | `(url)` → number | Maps the URL's file extension to `MediaTypes.Image`, `MediaTypes.Video`, `MediaTypes.Audio`, `MediaTypes.Effect` (for `.glsl`) or `-1` when unknown. |
| `mediaViewMain` | async `()` | Boot function of `MediaView.html`. URL params: `link`/`d` (required), `type`/`t` (else guessed with `mediaGetTypeFromUrl`), `props` (JSON), `no_stats`, `no_controls`, `ctrl` (message prefix). With `ctrl`, first asks the opener for props (`<prefix>GetProps`, inner `getPropsFn`, waits up to 100 ms for the `MediaGotProps` event). Creates `<Media-View>` and a player with `fit = false`, auto aspect and centered alignment; clicking the media (inner `showFull`) navigates to `MediaPreview.html?full&back&pos=...`. With `ctrl`, listens for `InterOp` messages, copies matching keys into `props` and handles `Reload` (inner `reload`), `SetVolume`, `UpdateSize`, `Crop` and `Compile`. Plays with `mediaPlayWithPopup`, then `PageLoaded()`. |
| `mediaPreviewMain` | async `()` | Boot function of `MediaPreview.html` (bare full-window player, also used for screenshots). Adds class `MediaPreview` to the body; binds CefSharp when present; uses a `ScreenShotHost` (from `window`, WebView2 host objects via inner `GetHo`, or `SsHack` when `?hack`) and then adds `NoFade`. URL params: `link`/`d`, `type`/`t`, `pos`, `props`, `fill`, `full` (enter full screen, re-entered on first key/click), `back` (click goes `history.go(-1)`). Creates the player, appends its element directly to the body, caches it, resizes it with `MediaPlayerTools.UpdateSize` from a `ResizeObserver` (inner `updateSize`), seeks, un-mutes when allowed (else on first interaction), plays, shows and calls `MediaPlayerTools.SetupMediaMessages`. With a screenshot host it resizes the window to the media size via `shost.setSize`, measures FPS and reports `shost.doIt(duration, fps, error, compilationTime)`. |

#### Inner functions of `mediaCreatePlayer`

| Function | Signature | Description |
|---|---|---|
| `UpdateSize` | `(ev)` | Lays out the clip and player element. When called with an event and the player element has left the document, removes itself as a `resize` listener. Recomputes auto aspect (from the preview box with `AdaptiveSize`, otherwise from the clipped media size). With `AdaptiveSize` the player element gets the box size in px; otherwise it gets `props.Width`/`props.Height` plus a `scale(..%) translate(..)` transform and the clip is positioned with `alignX`/`alignY`. When `player.UseClip` is false, the overlay gets `Show` and red semi-transparent borders marking the cropped margins. With `fit`, moves `Media-Stats` (or `Media-Controls`) just under the media. Copies the transform and size to `<Media-Overlay>`. |
| `player.OnClipChanged` | `()` | Assigned callback that calls `UpdateSize()`. |
| `setButtonState` | `()` | Function variable; a no-op until controls exist, then enables/disables Mute/Un mute, Pause/Play, Loop/Once and the four volume buttons from the player state. Exposed as `player.SetButtonState` and returned by every button callback so `Button` refreshes the state. |
| `createStats` | `(name, title, className)` → `setText` | Appends `<Media-Stat>` (`<Media-StatName>` + `<Media-StatValue>`) to `<Media-Stats>`, with tooltip `title` and optional class (`Adjust`, `Clip`, `Actual`). Click (via `keyboardClick` also keyboard) copies `name: text (value)` to the clipboard with `ValueFormat.copyToClipboard`. |
| `setText` | `(newText, newValue)` | Inner of `createStats`: hides the stat (class `Hide`) when `newText` is falsy, otherwise shows it and updates the text; stores `newValue` (or the text) in `val.Value`. |
| `updateStats` | `(atime)` | Reads time, duration, volume, GPU time, compile time, trimmed time/duration, crop and render size from the player and calls the setters; each read is wrapped in `try/catch`. Reschedules itself with `requestAnimationFrame`; stops once `<Media-Stats>` is no longer in the document. |

Buttons created in the controls block (each a common.js `Button` with `ButtonStyle.ButtonElement = "Media-Button"`): Pause, Play, Stop, Loop, Once, Mute, Un mute, VOL 100/75/50/25 % (each writes the matching `QuizzWeaver.Media*` `localStorage` key) and Source (`EditOrOpenUrl(player.Source)`). The `<Media-Controls>` element listens for `mediaEnded` and calls `setButtonState`.

#### Inner functions of `mediaEditProps`

| Function | Signature | Description |
|---|---|---|
| `GetApplyState` | `()` → string | `JSON.stringify(props)` with `Volume` temporarily removed; compared to decide whether "Apply changes" is enabled. |
| `reload` | async `()` | Disables Apply, rebuilds the player with `mediaCreatePlayer(target, type, data, props, !mediaControls, oldTopElement, !mediaStats)`, swaps the new controls/stats elements in, restores `OnEnded`, volume, mute, loop, play state and position, removes the old `<Media-Preview>`, removes `Replacing` from the new one, calls `oldP.OnDispose()`, refreshes buttons and resets the apply state. Also passed to `saveFn`. |

#### Inner functions of `mediaEditMain`

| Function | Signature | Description |
|---|---|---|
| `toggleCollape` | `()` | Toggles class `Hide` on `<Media-Full>`; when hiding, remembers whether it was playing and pauses; when showing, resumes. Updates the collapse button text, title and icon (`IconMediaCollapse`/`IconMediaExpand`). |
| `player.OnLoop`, `player.OnEnded` | `()` | Assigned callbacks: log "Loop"/"End" to the console; `OnEnded` then calls the original handler. |

#### Inner functions of `mediaViewMain`

| Function | Signature | Description |
|---|---|---|
| `getPropsFn` | `(ev)` | `InterOp` listener used only during start-up: on `<prefix>SetProps` copies the message (minus `Type`, `From`, `MagicXyz`) to `newProps` and dispatches `MediaGotProps` on the body. |
| `showFull` | `()` | Function variable; opens `MediaPreview.html?full&back&pos=<pos>&type=&link=&props=` in the same tab. Set as the click handler of the player's parent element. |
| `reload` | async `()` | Same player rebuild as in `mediaEditProps` (keeps volume/mute/loop/play/position), re-attaches `showFull` and updates `props` to the new `player.Params`. Triggered by a `<prefix>Reload` message. |
| `player.OnLoop`, `player.OnEnded` | `()` | Console-logging callbacks as in `mediaEditMain`. |
| `target.onclick` | — | Only inside a commented-out block (old top-right-corner click to open `MediaPreview.html`); not active. |

#### Inner functions of `mediaPreviewMain`

| Function | Signature | Description |
|---|---|---|
| `GetHo` | `()` → object\|null | Returns `chrome.webview.hostObjects.ScreenShotHost` or `null` (also on exception). |
| `onInteraction` (full screen) | async `()` | With `full`: on first `keyup`/`click` on the body removes itself, re-enters full screen and re-enables `back` navigation after 100 ms. |
| `onInteraction` (audio) | `()` | When un-muting is blocked: on first `keyup`/`click` removes itself, un-mutes the player and re-enables `back` navigation after 100 ms. |
| `updateSize` | `()` → `[w, h]` | Calls `MediaPlayerTools.UpdateSize(document.body, player, fill)`; the returned clipped size is used to resize the screenshot window. |

#### class `SsHack`

Debug stand-in for a screenshot host object, used by `mediaPreviewMain` when the URL has `hack` and no real `ScreenShotHost` exists. Every method only logs its call to the console.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `isSupported` | method | `()` | Logs `isSupported()`. |
| `setSize` | method | `(width, height)` | Logs the requested window size. |
| `doIt` | method | `(duration, fps, error, loadDuration)` | Logs the final report values. |

### `parseTools.js`

Static helper class used by the effect sub-folder pages (`backgroundEffects/`, `imageEffects/`, `collageEffects/`, `textEffects/` `index.html` via `../parseTools.js`) and by `TextDemo.html`. It copies URL parameters, JSON side files or objects onto typed default objects, converting each string to the type of the existing member. Defines one global: the class `ParseTools`.

#### class `ParseTools`

Never instantiated; all members are static.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `DecodeObject` | static method | `(val)` → object\|string | Decodes a string: `$RGB`, `$RGBA`, `$RRGGBB`, `$RRGGBBAA` hex become `{Red, Green, Blue, Alpha}` with 0–1 components; strings starting with `{` or `[` are `JSON.parse`d; anything else is returned unchanged. |
| `MessageProxy` | static method | `(iframe)` | Sets `window.onmessage` to forward messages between the parent window and `iframe.contentWindow`: messages from the iframe go to `window.parent`, others go to the iframe (same `origin` argument). Ignores messages from the window itself and does nothing while the iframe has no window. |
| `SetToObject` | static method | `(obj, key, val, onNotFound, logPrefix = "")` | Sets `obj[key]` from string `val` based on the existing member type: `null` sets `null`; `object` uses `DecodeObject`; `string` maps a leading `$` to `#`; `boolean` is true for `"true"`/`"1"`; `number` uses `parseFloat` (ignored if NaN). When the member does not exist, calls `onNotFound(key, val)` or logs a warning. Logs every assignment to the console with `logPrefix`. |
| `ParamsToObject` | static method | `(searchParams, dest, onNotFound, logPrefix)` | Runs `SetToObject` for every entry of a `URLSearchParams`; the string `"null"` becomes `null`. |
| `ObjectToObject` | static method | `(srcObject, dest, onNotFound, logPrefix)` | Runs `SetToObject` for every non-function member of `srcObject`, converting values to strings first. |
| `ObjectToParams` | static method | `(srcObject)` → string | Serializes an object to a URL query string: skips `undefined` and functions, JSON-encodes objects, writes falsy numbers as `0` and falsy booleans as `false`, and keeps `null` as `"null"`. |
| `FileToObject` | static async method | `(srcUrl, dest, onNotFound, logPrefix)` | Fetches `srcUrl` (GET, `cors`, default cache); on HTTP 200 parses JSON and applies it with `ObjectToObject`. Network/parse errors and other statuses are silently ignored. |

### `textEffect.js`

Text and counter effect helpers. Loaded by `counterEffects/index.html` (with `../canvasTools.js`, `common.js`, `anim.js`), which boots `textEffectMain`. At parse time it runs `includeJs(document.currentScript.src, "../mediaPlayer/media_player.js")`. Uses `TextStyle`, `CanvasTools`, `MediaPlayerParamsEffect`, `MediaPlayerParamsImage`, `MediaPlayer`, `MediaTypes` from the mediaPlayer folder. Defines globals `CounterEffectParams`, `TextEffect` and `textEffectMain`.

#### Globals

| Name | Kind | Description |
|---|---|---|
| (top-level call) | immediately executed | `includeJs(..., "../mediaPlayer/media_player.js")`. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `textEffectMain` | async `()` | Demo boot function of `counterEffects/index.html`. Builds a hard-coded gold `TextStyle` (bold 160px Tahoma, gradients, shadow) and a `CounterEffectParams` (prefix `"SEK "`, suffix `":-"`, `,` thousands separator, 8 digits), creates `TextEffect.CreateCounterEffect("ScrollUp", ...)`, appends its element to the body, sets `fx.GetValue = x => [(990 + x^1.6) * 100, 500]`, shows and plays it, then appends `CanvasTools.CreateTextGrid` of 16 greetings. A `document.body.onmousemove` handler stores `offsetX` in a local `value` that is only used by commented-out `GetValue` variants. On error writes the message into the body; otherwise calls `PageLoaded()`. |

#### class `CounterEffectParams`

Plain options object for `TextEffect.CreateCounterEffect`; created with `new` and modified by the caller.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Fade` | field | `16.0` | Fade height in pixels at the top/bottom of the digits (minimum 1). |
| `ExtraSpacing` | field | `10.0` | Extra vertical spacing between the digit glyphs in the image strip. |
| `MaxDigits` | field | `10` | Number of integer digits (clamped 1–32). |
| `FlipSpeed` | field | `1` | Speed factor for the digit flip animation. |
| `FlipFraction` | field | `0.1` | Fraction (0–1) of each unit step spent flipping. |
| `DecimalDigits` | field | `2` | Number of decimal digits. |
| `ThousandsSeparator`, `DecimalSeparator` | field | `" "`, `"."` | Separator strings drawn into the glyph image. |
| `Prefix`, `Suffix` | field | `""` | Text drawn before / after the number. |
| `DigitSpacing` | field | `0` | Extra horizontal pixels between digits (can be negative). |

#### class `TextEffect`

Factory class with static methods only; returns media players that render text through a GLSL effect.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `CreateTextEffect` | static async method | `(effectName = "Rain", text, textStyle = new TextStyle(), effectParams = new MediaPlayerParamsEffect())` → player | Loads `textStyle.FontUrl` as a `FontFace` when set, renders `text` to an image URL with `CanvasTools.CreateTextImageUrl`, maps a bare effect name to `imageEffects/<name>.glsl`, sets `effectParams.Transparent = true`, creates an image player (`MediaPlayer.Create(MediaTypes.Image, url, MediaPlayerParamsImage{Effect, EffectParams})`) and calls `player.Cache()`. Returns the player (caller attaches `.Element`, then `Show()`/`Play()`). |
| `CreateCounterEffect` | static async method | `(effectName = "ScrollUp", textStyle, counterParams = new CounterEffectParams(), effectParams)` → player | Builds an animated number counter. Loads the font, sets text margins, renders a glyph strip with `CanvasTools.CreateNumberImageUrl` (digits, separators, prefix, suffix), and builds the part list (inner `DefPart`). Maps a bare name to `../counterEffects/<name>.glsl`; sets the effect width/height, `AdaptiveSize = false` and `FxProps` `MaxParts`, `UvScale`, `UvAdd`, `Fade`. Creates and caches the image player, then installs `player.OnNewSize`, `player.GetValue` and `player.OnRender` (below). Returns the player. |

#### Inner functions of `TextEffect.CreateCounterEffect`

| Function | Signature | Description |
|---|---|---|
| `DefPart` | `(type, partWidth)` | Appends a part (type `0` digit, `17` thousands separator, `18` prefix, `19` suffix, `20` decimal separator, `-1` end) with its width to `partType`/`partWidths` and adds it (plus `DigitSpacing` for digits) to the total `width`. |
| `player.OnNewSize` | `(newWidth, oldWidth)` | Assigned callback, called when the visible counter width changes; default is a no-op that callers may replace. |
| `player.GetValue` | `(time)` → number \| `[value, flipSpeed]` | Assigned callback returning the value to display for a playback time; default returns `time`. An array return also overrides the flip speed. |
| `player.OnRender` | `(gl, p)` | Assigned per-frame callback: reads the value via `GetValue(p.GetPos())`, walks the parts from the right, computes each part's x position, glyph row (`/11`), UV scale and flip fraction (cosine easing, scroll-in of the leading digit and prefix), stops after the last significant digit, calls `OnNewSize` when the width changes, logs the part list when it changes, and uploads the `digitWidths` (`uniform1fv`) and `digits` (`uniform4fv`) uniforms. |
| `fx.GetValue`, `document.body.onmousemove` | — | Assigned in `textEffectMain` (see above). |

### `BackgroundEffectsLib.html` (inline script)

Gallery of all background effects. Loads `../common/common.js` and the mediaPlayer `media_player_effect.js`, `media_effect_program.js`, `media_player_tools.js`; boot `<body onload="main()">`.

| Function | Signature | Description |
|---|---|---|
| `main` | async `()` | Shows a loading indicator (`AddLoading`), appends `<SysWeaver-Page>`, loads the effect names from `Api/GetBackgroundEffects`, and for each one renders a still with `MediaPlayerEffect.GetImage("Background", "backgroundEffects/<name>.glsl", mp)` (960×540, not adaptive, not static). Appends an `<Effect-Card>` with `<Effect-Title>`, `<Effect-Info>` (compilation time and average draw time, translated with `_T`) and the `<img>`. Clicking the image opens `backgroundEffects/?Effect=<name>`; `ValueFormat.copyOnClick` copies that URL. Effects without an image are skipped. Errors go to `Fail`. |

### `CollageEffectsLib.html` (inline script)

Gallery of the collage effects, rendered with `DemoCollage.svg` as texture atlas. Same scripts as `BackgroundEffectsLib.html`. A header comment documents the URL parameters of the collage effects.

| Function | Signature | Description |
|---|---|---|
| `main` | async `()` | As in `BackgroundEffectsLib.html`, but uses `Api/GetCollageEffects`, folder `collageEffects`, loads `mp.FxProps` from `DemoCollage.json`, renders with `MediaPlayerEffect.GetImage("Collage", ..., mp, CreateTexture)` and links to `collageEffects/?Effect=<name>&Image=DemoCollage.svg`. |
| `CreateTexture` | inner `(gl)` → `MediaPlayerImageTexture` | Texture factory for `DemoCollage.svg` with `EffectSizeFromImage: false`. |

### `ImageEffectsLib.html` (inline script)

Gallery of the image effects applied to `../logo.png`. Same scripts as `BackgroundEffectsLib.html`.

| Function | Signature | Description |
|---|---|---|
| `main` | async `()` | As in `BackgroundEffectsLib.html`, but uses `Api/GetImageEffects`, folder `imageEffects`, renders with `MediaPlayerEffect.GetImage("Image", ..., mp, CreateTexture)` and links to `imageEffects/?Effect=<name>&Image=../logo.png`. |
| `CreateTexture` | inner `(gl)` → `MediaPlayerImageTexture` | Texture factory for `../logo.png` with `EffectSizeFromImage: true`. |

### `MapDemo.html` (inline script)

Demo grid of `MapView.html` variants. Loads only `../common/common.js`.

| Function | Signature | Description |
|---|---|---|
| `main` | async `()` | Appends `<SysWeaver-Page>`. Uses URL param `key` or fetches `Api/GetGoogleMapsKey` (JSON string; empty when unavailable), and `center` (default London). Adds sections and cards for map types, pin styles, pin sizes, zoom, DPI scale, links, languages and misc options. |
| `Section` | inner `(title)` | Appends a `<Map-Section>` heading with `title`. |
| `Add` | inner `(title, type, pin, link, extra)` | Builds `MapView.html?key=&center=` plus optional `&maptype=`, `&pin=`, `&link=` and the raw `extra` query string; appends a `<Map-Card>` with `<Map-Title>`, an `<iframe>` (`tabIndex -1`) showing that URL and a full-screen `ColorIcon` (`../icons/fullscreen_enter.svg`) that opens it in the same tab; `ValueFormat.copyOnClick` copies the URL. |

### `MapView.html` (inline script)

Stand-alone embeddable map (no common.js). The script at the end of `<body>` calls `main()` immediately.

| Function | Signature | Description |
|---|---|---|
| `_T` | `(x, a, c)` → string | Local stub of the translation function: replaces `{0}` in `x` with `a`; `c` (translator comment) is ignored. |
| `_TF` | `(x, c)` → string | Local stub: returns `x`. |
| `main` | async `()` | Reads `maptype` (default `roadmap`; `open`/`openstreet`/`openstreetmap` → OpenStreetMap, `opendark`/`darkstreet`/`openstreetdark`/`openstreetmapdark` → dark OpenStreetMap). For Google maps it uses `key` or `Api/GetGoogleMapsKey`, falling back to OpenStreetMap with a console warning. Adds body classes `OpenDark`, `Dark` (Google `dark` → `roadmap` + CSS invert), `Link`, `NoInteraction` (unless `interactive=true`), `NoFade` (screenshot host) and `Show` (after load). Parses `center` (required, `lat,lon`, `S`/`W` suffix negates), `dpi` (100), `zoom` (16), `language`/`lang` (`en`), `link` (`true`/`view`/`dir` map to Google Maps URLs, any other value is used as URL), `title`, `pin` (predefined names map to `map/*.svg` with anchor and height; any other value is used as image URL), `pinX`, `pinY`, `pinHeight`. Writes `--dpi`, `--idpi`, `--pinX`, `--pinY`, `--pinHeight` on `<html>`. Creates the map `<iframe>` (OpenStreetMap embed with a bbox computed from zoom and window size, or Google Maps Embed `v1/view`), an optional pin `<img>`, and either an `<a target=_blank>` overlay for links or a `<Map-Title>` overlay for a non-interactive title. On iframe load waits 100 ms and adds `Show`; with a screenshot host waits 500 ms more and calls `shost.doIt(0, 0, null)`. Errors are written into the body. |
| `fix` | inner `(x)` → number | Keeps digits, `.` and `-` of a coordinate string and parses it; throws `Invalid coordinate` when empty or NaN. |
| `last` | inner `(x)` → string | Lower-cased last character of the trimmed string (hemisphere letter). |
| `GetHo` | inner `()` → object\|null | Returns `chrome.webview.hostObjects.ScreenShotHost` or `null`. |
| `delay` | inner `(msToWait)` → Promise | Resolves after `msToWait` ms. |

### `TextDemo.html` (inline script)

Demo of `TextStyle` rendering with links to the text effect pages. Loads `../common/common.js`, `parseTools.js`, `../mediaPlayer/canvasTools.js`.

| Function | Signature | Description |
|---|---|---|
| `main` | async `()` | Appends `<SysWeaver-Page>`; reads URL params `Text` (default `Abc123`) and `Texts` (comma separated greetings). Defines 5 image effects (`BadVHS`, `Rain`, `SliceGlitch`, `OldMovie`, `3D-flag`) and 4 collage effects (`3d-tiles`, `Stripes`, `Scroll`, `Wheel`) with icon names, then calls `Add` for eight sample styles (fill and stroke, drop shadow, two letter spacings, horizontal and diagonal gradient, gold, neon). |
| `Add` | inner async `(title, style)` | Sets all margins of `style` to 32; appends a `<Map-Card>` with `<Map-Title>` and an `<img>`. The title gets a copy icon (copies the style JSON with `ValueFormat.copyToClipboardInfo`), one icon per image effect (opens `textEffects/?<style params>&Text=<text>&Effect=<name>`) and one per collage effect (opens `textEffects/?<half-size style params>&Texts=<texts>&Effect=<name>`; the style is scaled with `cscale = 0.5`). Params are built with `ParseTools.ObjectToParams`. The image is `CanvasTools.CreateTextImageUrl(text, style)`; clicking it opens the `None` effect, and `ValueFormat.copyOnClick` copies that URL. |

### `backgroundEffects/index.html` (inline script)

Full-window renderer for one background effect. Loads `../parseTools.js`, `../../common/anim.js` and the mediaPlayer `media_player_tools.js`, `media_effect_program.js`, `media_player_effect.js`; boot `<body onload="main()">`.

| Function | Signature | Description |
|---|---|---|
| `main` | async `()` | Starts from a `MediaPlayerParamsEffect`-shaped `props` literal (1920×1080, `AdaptiveSize: true`, ...). Effect from URL param `Effect` (default `SmoothBands`, `.glsl` appended). Applies the side file `../<effect>.json` with `ParseTools.FileToObject` and then the URL params with `ParseTools.ParamsToObject`; unknown keys go to `fxParams` (decoded with `DecodeObject`), which becomes `props.FxProps`. Creates `new MediaPlayerEffect(effect, props)`, appends its element, caches it, sizes it with `MediaPlayerTools.UpdateSize(document.body, player)` from a `ResizeObserver` (inner `updateSize`), plays, shows, calls `MediaPlayerTools.SetupMediaMessages` and `MediaPlayerTools.PostPageLoaded()`. A commented-out block holds the older iframe-to-`MediaPreview.html` variant. |
| `updateSize` | inner `()` | Calls `MediaPlayerTools.UpdateSize(document.body, player)`. |

### `imageEffects/index.html` (inline script)

Full-window renderer for one image effect applied to an image. Same scripts as `backgroundEffects/index.html`.

| Function | Signature | Description |
|---|---|---|
| `main` | async `()` | Uses a `MediaPlayerParamsImage`-shaped `props` literal (`EffectSizeFromImage: true`, `Transparent: true`) and a `MediaPlayerParamsEffect`-shaped `ep` literal. `Image` defaults to `../../logo.png`, `Effect` to `DistortedTv` (`.glsl` appended). Applies `<effect>.json`, `<image>.json` and the URL params in three levels: `props` → `ep` (via nested `ParseTools.SetToObject`) → `fxParams`. Sets `props.Effect`, `props.EffectParams = ep`, `ep.FxProps`, `ep.Transparent`, `ep.AdaptiveSize` (false when `EffectSizeFromImage`). Creates `new MediaPlayerEffect(effect, ep, gl => new MediaPlayerImageTexture(gl, props, image))` and runs it as in `backgroundEffects/index.html`. A commented-out block holds the older iframe variant. |
| `updateSize` | inner `()` | Calls `MediaPlayerTools.UpdateSize(document.body, player)`. |

### `collageEffects/index.html` (inline script)

URL adapter for collage effects: builds a `MediaPreview.html` URL and shows it in an iframe. Loads `../parseTools.js`, `../../common/anim.js`.

| Function | Signature | Description |
|---|---|---|
| `main` | async `()` | Same three-level parameter merge as `imageEffects/index.html` (`props` with `AdaptiveSize: true`; `Image` default `DemoCollage.svg`; `Effect` default `Stripes`, mapped to `collageEffects/<name>.glsl`; side files `../<effect>.json` and `../<image>.json`). Builds `url = '../MediaPreview.html?transparent=true&link=<image>&type=0&props=<encoded JSON>'` (plus `&ShaderDebug=true` when requested), assigned to an implicit global `url`, logs it, creates an `<iframe>` with that `src`, installs `ParseTools.MessageProxy(iframe)` and appends it to the body. |

### `counterEffects/index.html`

No inline script; it loads `../textEffect.js` and boots `textEffectMain` (see `textEffect.js`).

## CSS reference

### `media.css`

Styles the media player pages `MediaView.html` and `MediaPreview.html` (and any host page of `mediaEditMain`). Loaded with `<link rel="stylesheet">` after `../common/theme.css` (and `../app/application.css` in `MediaView.html`). Depends on theme variables `--ThemeBackground`, `--ThemeBackground75`, `--ThemeMain`, `--ThemeMain50`, `--ThemeAcc1`, `--ThemeRad`, `--ThemeFontMono`. Not templated. Icon masks reference `../icons/*.svg`.

#### Page and layout

| Selector(s) | Description |
|---|---|
| `body` | No margin/padding, no text selection, centered text, vertical scroll only. |
| `SysWeaver-Page.Media` | Fixed background page created by `mediaEditMain` (class `Media`). |
| `Media-Full`, `Media-Full > *`, `Media-Full > Media-Preview` | Editor container from `mediaEditMain`, above the background; children limited to `52em` and centered, except the preview. |
| `Media-Full.Hide Media-Preview`, `Media-Full.Hide Media-Stats`, `Media-Full.Hide Media-Controls` | Collapsed editor (class `Hide` toggled by `toggleCollape`) hides player, stats and controls. |
| `Media-NoAudio` | Full-screen blurred click-to-unmute overlay from `mediaPlayWithPopup`, text centered vertically via `line-height: 100vh`. |
| `Media-Edit` | Property editor container from `mediaEditProps`, full width, left aligned. |
| `Media-View`, `Media-View Media-PreView`, `Media-View Media-Stats`, `Media-View Media-Controls` | `mediaViewMain` layout: absolute box with a 3-row grid (preview, stats, controls). `Media-PreView` matches the `<Media-Preview>` element because HTML tag selectors are case-insensitive. |

#### Preview and aspect container

| Selector(s) | Description |
|---|---|
| `Media-Preview` | Player box; width from `--maxWidth`, 16:9 height, `1em auto` margin. |
| `Media-Preview.Replacing` | New player being built during `reload`, moved off-screen (`top: -10000px`). |
| `Media-Preview.NoVisual` | Audio-only player: zero size. |
| `Media-AspectContainer`, `Media-AspectContainer > *` | Clip box positioned by `UpdateSize`; children are non-interactive, top-left transform origin. |
| `Media-AspectContainer.MediaPreviewFit > *` | Fit mode: children get the theme background (a second rule with a commented-out drop shadow is empty). |
| `Media-AspectContainer.MediaPreviewFill` | Fill mode: clips overflow. |
| `Media-AspectContainer > *.MediaHidden` | Player element hidden (`MediaHidden` set by `media_player_tools.js` until shown). |
| `Media-AspectContainer > Media-Overlay`, `Media-AspectContainer.MediaPreviewFit > Media-Overlay`, `Media-AspectContainer > Media-Overlay.Show` | Crop overlay: invisible by default; `Show` (set by `UpdateSize` when crop is visualized) makes it visible with a red outline. |

#### Stats

| Selector(s) | Description |
|---|---|
| `Media-Stats`, `Media-Stats > *:first-child:last-child` | Wrapping flex row of stats; a single stat is centered. |
| `Media-Stat`, `Media-Stat:hover`, `Media-Stat:focus` | Clickable stat chip (mono font, theme border, focus outline in `--ThemeAcc1`). |
| `Media-Stat.Adjust`, `Media-Stat.Adjust:hover` | Green trimmed-time stats. |
| `Media-Stat.Clip`, `Media-Stat.Clip:hover` | Blue crop stats. |
| `Media-Stat.Actual`, `Media-Stat.Actual:hover` | Magenta render-size stats. |
| `Media-Stat.Hide` | Hidden stat (set by `setText` with no text). |
| `Media-StatName`, `Media-StatValue` | Label and right-aligned value (min `5em`). |

#### Controls

| Selector(s) | Description |
|---|---|
| `Media-Controls`, `Media-Controls > *:first-child:last-child` | Wrapping flex row of buttons; a single button is centered. |
| `Media-Button`, `Media-Button:hover` | Button element used through `ButtonStyle.ButtonElement`: accent background, rounded, shadow, lift on hover. |
| `Media-Button.Pressed`, `Media-Button.Disabled`, `Media-Button.Disabled:hover` | States set by common.js `Button` (`Pressed` while clicked, `Disabled` when not enabled). |
| `Media-Button Sysweaver-ButtonText`, `Media-Button Icon-24x24` | Button text (left) and 24×24 icon (right) created by common.js `Button`. |

#### Preview page (`MediaPreview.html`)

| Selector(s) | Description |
|---|---|
| `body.MediaPreview`, `body.MediaPreview > *` | Class added by `mediaPreviewMain`: full-window body without scroll; the player element is non-interactive and fades with opacity. |
| `body.MediaPreview.NoFade`, `body.MediaPreview.NoFade > *` | Screenshot mode (`NoFade`): checkerboard background built from `--c0`/`--c1`, no transitions. |
| `body.MediaPreview > *.MediaHidden` | Hidden player element before `Show()`. |

#### Icons

| Selector(s) | Description |
|---|---|
| `.MediaIconPlay`, `.MediaIconPause`, `.MediaIconStop`, `.MediaIconMute`, `.MediaIconUnMute`, `.MediaIconVolLoud`, `.MediaIconVolLow`, `.MediaIconExpand`, `.MediaIconLoop`, `.MediaIconOnce` | Mask icons of the player buttons from `../icons/preview_play.svg`, `preview_pause.svg`, `preview_stop.svg`, `preview_mute.svg`, `preview_unmute.svg`, `preview_vol_loud.svg`, `preview_vol_low.svg`, `preview_expand.svg`, `preview_loop.svg`, `preview_once.svg`; used by `mediaCreatePlayer`. |
| `.IconSave`, `.IconApply` | Editor buttons "Save changes" (`../icons/disc.svg`) and "Apply changes" (`../icons/reload.svg`) in `mediaEditProps`. |
| `.IconMediaNewWindow`, `.IconMediaCollapse`, `.IconMediaExpand` | `mediaEditMain` buttons: `../icons/newtab.svg`, `../icons/expand.svg`, `../icons/collapse.svg`. |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--maxWidth` | `Media-Preview`: `min(100vw - 5em, (80vh - 72px - 5em) * 16 / 9)` | Width of the preview box; height is `--maxWidth * 9 / 16`. |
| `--c0`, `--c1` | `body.MediaPreview.NoFade`: `#bbb`, `#eee` | The two checkerboard colors. |
| `--hs` | `round(up, min(100vw, 100vh) / 32, 1px)` | Half checker cell size (background positions). |
| `--s` | `--hs * 2` | Checker cell size (`background-size`). |

#### Animations, media queries, theme variants

None (only `transition`s on `Media-Stat`, `Media-Button` and `body.MediaPreview > *`).

### `textEffect.css`

Loaded by `counterEffects/index.html` after `../../common/theme.css`. Not templated.

| Selector(s) | Description |
|---|---|
| `body` | `line-height: 100%`, vertical middle alignment. |
| `body > *` | Every body child (the effect canvas and the text grid) is a full-width block with automatic height (`!important`), `object-fit: contain`. |

No custom properties, animations or media queries.

### Inline styles of the gallery pages (`BackgroundEffectsLib.html`, `CollageEffectsLib.html`, `ImageEffectsLib.html`)

The three pages share an identical `<style>` block. Theme variables used: `--ThemeMainRGB`, `--ThemeMain`, `--ThemeMain50`, `--ThemeRad`, `--ThemeFadeDuration`, `--ThemeAcc1`, `--ThemeAcc2`, `--ThemeAcc275`.

| Selector(s) | Description |
|---|---|
| `*`, `body` | Border-box sizing; no horizontal scroll. |
| `Effect-Card`, `Effect-Card:hover`, `Effect-Card:focus`, `Effect-Card:focus:hover` | Card per effect created by inline `main()`, with hover/focus outline. |
| `Effect-Title`, `Effect-Info` | Card title (accent, bold) and timing info (selectable). |
| `Effect-Card img` | Full-width preview image. |
| `canvas.MediaHidden` | The off-screen WebGL canvas used by `MediaPlayerEffect.GetImage` while rendering (class `MediaHidden` from `media_player_tools.js`), moved out of view with near-zero opacity. |

### Inline styles of `MapDemo.html`

| Selector(s) | Description |
|---|---|
| `*` | Border-box sizing. |
| `SysWeaver-Page` | Max width `85.1em` (three cards per row). |
| `Map-Section`, `Map-Section:first-child` | Section header from `Section()`: inverted theme colors, uppercase; the first one has a smaller top margin. |
| `Map-Card`, `Map-Card:hover`, `Map-Card:focus`, `Map-Card:focus:hover` | Inline-block `26em` card from `Add()`, hover/focus outline. |
| `Map-Card icon-28x28` | Full-screen `ColorIcon` placed in the top-right corner. |
| `Map-Title` | Card title. |
| `Map-Card iframe` | `20em` high borderless `MapView.html` iframe. |

### Inline styles of `MapView.html`

| Selector(s) | Description |
|---|---|
| `html` | Transparent, no scroll; declares the custom property defaults below and scales its size by `--dpi`. |
| `body` | Transparent, no scroll, red 200% text (used for error messages). |
| `body iframe` | Map iframe centered with `zoom: var(--dpi)`, size scaled by `--idpi` (with extra margins to hide map chrome), fades in. |
| `body.Dark iframe`, `body.OpenDark iframe` | Dark map variants via `invert(1) hue-rotate(...)` filters (classes set by `main`). |
| `body.NoInteraction iframe` | Disables pointer events when not `interactive`. |
| `body.Show iframe`, `body.Show img` | Visible after the iframe loaded. |
| `body.NoFade iframe`, `body.NoFade img` | Screenshot mode: immediately visible, no transition. |
| `body img` | Pin image at the window center, height `--pinHeight`, translated by `--pinX`/`--pinY`. |
| `body a`, `body Map-Title` | Full-window overlays for a link (`<a>`) or a title tooltip (`<Map-Title>`). |

Custom properties (set by `main` on `document.documentElement.style`):

| Variable | Default / source | Used for |
|---|---|---|
| `--dpi` | `1`; `dpi / 100` | `zoom` of the iframe and `html` size. |
| `--idpi` | `1`; `100 / dpi` | Iframe size compensation. |
| `--pinX`, `--pinY` | `-50%`, `-100%`; negated pin anchor | Pin `translate`. |
| `--pinHeight` | `96px`; `pinHeight` px | Pin image height. |

### Inline styles of `TextDemo.html`

| Selector(s) | Description |
|---|---|
| `*`, `SysWeaver-Page` | Border-box sizing; page max width `85.1em`. |
| `Map-Card`, `Map-Card:hover`, `Map-Card:focus`, `Map-Card:focus:hover` | Block card per text style from `Add()` (reuses the map demo card names). |
| `Map-Card icon-28x28` | Effect/copy icons floated right in the title. |
| `Map-Title` | Card title. |
| `Map-Card img` | Rendered text image, scaled down to fit. |

### Inline styles of `MediaPreview.html` and `MediaView.html`

| Selector(s) | Description |
|---|---|
| `:root` | (`MediaPreview.html`) `color-scheme: normal !important`. |
| `html, body` | Transparent background (`!important` in `MediaPreview.html`). |

### Inline styles of the effect folder pages (`backgroundEffects/`, `imageEffects/`, `collageEffects/`, `counterEffects/` `index.html`)

| Selector(s) | Description |
|---|---|
| `:root` | `color-scheme: normal !important` (all four). |
| `html, body` | Transparent background (`!important`); full size, no margin, no scroll (all but `counterEffects/`). |
| `body > *` | Non-interactive children (`backgroundEffects/`, `imageEffects/`). |
| `iframe` | Borderless full-size non-interactive iframe (`collageEffects/`, `imageEffects/`; commented out in `backgroundEffects/`). |
