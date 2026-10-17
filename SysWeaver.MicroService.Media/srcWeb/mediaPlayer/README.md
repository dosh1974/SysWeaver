# mediaPlayer

[⬆ srcWeb](../README.md)

> The JavaScript media player: one player class per media type, plus the WebGL effect program and canvas text tools they share.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../../README.md) |
| **Kind** | Embedded web assets (JavaScript) |

## Purpose

[`media_player.js`](media_player.js) is the entry point. It defines the `MediaTypes` constants (`Image`, `Video`, `Audio`, `YouTube`, `Effect`, `Text`) and the `MediaPlayer` class. `MediaPlayer.Init()` loads `media_player_tools.js` and `canvasTools.js` with `includeJs`, and loads all the other player scripts as well when `window.MediaLoadAll` is set. `MediaPlayer.Create(type, data, mediaParams)` then creates the right player. Each `MediaPlayerParams*` class mirrors one of the C# types in [`Media/`](../../Media/README.md).

Effects are GLSL fragment shaders (see the effect folders under [`mediaView/`](../mediaView/README.md)). [`media_effect_program.js`](media_effect_program.js) compiles them and reads editable parameters from `const` lines that end in a `//var:{...}` comment.

The scripts are served as `mediaPlayer/*.js`. The pages in `mediaView/` include them either directly or through `media_player.js`.

## Contents

| Item | Description |
|---|---|
| [`media_player.js`](media_player.js) | `MediaTypes` constants and the `MediaPlayer` factory/loader |
| [`media_player_tools.js`](media_player_tools.js) | `MediaPlayerTools`: shared helpers for clipping, sizing/alignment, blob caching, unmute detection, page-loaded and parent-window messages, errors |
| [`media_effect_program.js`](media_effect_program.js) | `EffectProgram` / `EffectProgramData`: WebGL shader compilation, linking, and parsing of `//var:` parameter annotations |
| [`media_player_effect.js`](media_player_effect.js) | `MediaPlayerEffect` (renders a GLSL effect, optionally over an image texture) and `MediaPlayerImageTexture` |
| [`media_player_image.js`](media_player_image.js) | `MediaPlayerImage` and its parameters |
| [`media_player_video.js`](media_player_video.js) | `MediaPlayerVideo` and its parameters |
| [`media_player_audio.js`](media_player_audio.js) | `MediaPlayerAudio` and its parameters |
| [`media_player_youtube.js`](media_player_youtube.js) | `MediaPlayerYoutube`: YouTube clip playback |
| [`media_player_text.js`](media_player_text.js) | `MediaPlayerText` / `MediaPlayerTextTexture`: stylised text (single or collage), optionally with an effect |
| [`canvasTools.js`](canvasTools.js) | `TextStyle`, `TextLinearGradient`, `TextGradientStop` and `CanvasTools`: draws styled text to a canvas |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.Media](../../README.md)
- **Used by:** the pages in [`mediaView/`](../mediaView/README.md) and its effect sub folders
- **Mirrors:** the C# media types in [`Media/`](../../Media/README.md)

## Behavior (web analysis)

### Loading and initialization

- All files are classic (non-module) scripts that declare global classes; there is no bundler, no `export`, no namespace object.
- Two ways of loading:
  1. **Via the entry point** `media_player.js` (used by `mediaView/media.js` and `mediaView/textEffect.js`, which do `includeJs(document.currentScript.src, "../mediaPlayer/media_player.js")`). On load the script runs `MediaPlayer.Init()` immediately at top level. It records `document.currentScript.src` in `MediaPlayer.CurrentUrl` and includes `media_player_tools.js` + `canvasTools.js` relative to it (common's `includeJs`). If `window.MediaLoadAll` is truthy (it is not set anywhere in the repo) it also includes every player script and runs `MediaPlayerTools.CanUnMute()`.
  2. **Direct `<script defer>` tags** for just the needed files: the effect pages under `mediaView/backgroundEffects|imageEffects/index.html` and the `*EffectsLib.html` thumbnail pages load `media_player_tools.js`, `media_effect_program.js`, `media_player_effect.js` (plus `common/anim.js` for smoothing). `TextDemo.html` loads only `canvasTools.js`.
- **Lazy loading:** `MediaPlayer.Create(type, data, mediaParams)` is `async`; unless `MediaLoadAll` is set it `await includeJs(...)`s the scripts the chosen player needs (for example `media_player_effect.js` + `media_effect_program.js` for an effect) before constructing it. `MediaPlayer.CreateSync` does the same switch with no loading and throws `"Must use CreateAsync when lazy loading..."` if the class is not loaded yet.
- Load-order dependency: `media_player_text.js` declares `class MediaPlayerParamsText extends TextStyle`, so `canvasTools.js` has to be loaded first. `Init()` loads it eagerly for this reason.
- A player is usable only after `await player.Cache(keepHidden)`. Constructors only build the element and defaults, and `Cache` does all the network and GPU work.

### Files

#### `media_player.js`
- `MediaTypes`: static integer constants `Image=0, Video=1, Audio=2, YouTube=3, Effect=4, Text=5`, a frozen `Names` array, and `GetTypeName(mediaType)`, which returns `"SysWeaver.MicroService.Media.Media" + MediaTypes.Names[...]`.
- `MediaPlayer.Init()`, `MediaPlayer.CurrentUrl`: see above.
- `MediaPlayer.Create(type, data, mediaParams)` / `CreateSync(...)`: the factory. Mapping:
  - `Image`: `MediaPlayerImage(data, params)`, or, if `params.Effect` is set, `MediaPlayerEffect(params.Effect, params.EffectParams, gl => new MediaPlayerImageTexture(gl, params, data))`.
  - `Video`: `MediaPlayerVideo`. `Audio`: `MediaPlayerAudio`. `YouTube`: `MediaPlayerYoutube(code, params)`.
  - `Effect`: `MediaPlayerEffect(url, params)`.
  - `Text`: `MediaPlayerText(text, params)`, or, if `params.Effect` is set, `MediaPlayerEffect(params.Effect, params.EffectParams, gl => new MediaPlayerTextTexture(gl, params, text))`.
  - Any other type: `null`.
  - The "X with effect" variants make the **effect** the player and wrap the original media as a texture provider. The outer params (Crop, EffectSizeFromImage, ...) go to the texture, and `EffectParams` becomes the effect's `MediaPlayerParamsEffect`.

#### `media_player_tools.js` — `MediaPlayerTools` (all static)
- `InitBase(i, element, params)`: the "base class by mixin". It sets common state on the instance: `Cached=false, Element, Width/Height=0, Volume=0.25, Muted=true, Paused=true, Looping=true, HaveAudio/HaveVisual/Duration` defaults, `UseClip=true`. It adds CSS class `MediaHidden` to the element. It installs default async methods **only if the instance does not already have them** (`OnDispose`→`Stop`, `SetVolume`, `Mute/UnMute`, `Play/Pause/Stop`, `Seek`, `GetPos`, `Loop/Once`, `Show/Hide`, `ResetVolume`, `GetTrueVolume`, `ApplyClip`, `OnLoop`). Class methods on the prototype are found by `if (!i.X)`, so they win. `OnEnded` is always overwritten. `OnLoop`/`OnEnded` dispatch `CustomEvent("mediaLoop" | "mediaEnded", {detail: player})` on `player.MessageElement` if the consumer set one.
  - `Show()` removes `MediaHidden` and restores the inline style saved by `Hide()`. `Hide()` adds `MediaHidden`, saves the `style` attribute and removes it.
  - `GetTrueVolume()` = `Muted ? 0 : Volume * params.Volume * 0.01` (if params has a `Volume`), otherwise `Volume`.
- `IsDarkMode()`: reads `color-scheme` from the computed style of `window.top.document.body`.
- `AddDarkModeChangeEvent(fn)` / `RemoveDarkModeChangeEvent(fn)`: a shared registry (`DarkModeEventsMap`). The first registration creates two `MutationObserver`s on the top window's `<html>` and `<body>` (`style`, `class` attributes). On a change it re-evaluates `IsDarkMode()` and calls every callback with the new boolean. The observers are disconnected when the last callback is removed.
- `ComputeClip(i, params)`: computes `ClipX/ClipY/ClipWidth/ClipHeight/ClipR/ClipB` from `params.Crop` (`MarginTop/Right/Bottom/Left` in pixels, or `MarginTopP...` percentages when `MarginAsPercentage`). It returns false when there is no crop.
- `ApplyClip(i, params)`: applies the clip as CSS `clip-path: inset(...)` on the element, and calls `i.OnClipChanged()` when the clip changes.
- `UpdateSize(target, player, fill, alignX=0.5, alignY=0.5)`: lays the player element out inside `target`. With `Params.AdaptiveSize` the element gets the target's pixel size. Otherwise the element keeps its native `Width×Height` and is fitted (or filled when `fill`) with `transform: scale() translate()` (origin left top), taking the clip rectangle into account. It returns `[clipWidth, clipHeight]`.
- `CanUnMute()`: probes autoplay-with-sound once (the result is cached in `HaveTested/Result`). It needs `MediaPlayerAudio`. It plays an embedded tiny base64 MP3 unmuted. On success it dispatches `AudioEnabled` on `document.body`. On failure it installs one-shot `keydown/mousedown/click` listeners that set `Result=true` and dispatch `AudioEnabled`.
- `SetupMediaMessages(player)`: the **remote-control contract** (see Player model). It adds a `window` `message` listener and returns an unsubscribe function.
- `PostPageLoaded()`: `postMessage({Type:"PageLoaded"}, "*")` to `window.parent` (or `window.opener` when not framed).
- `SetError(text)`: reports shader errors to a native host. It uses CefSharp (`CefSharp.BindObjectAsync()` then `window.ErrorText.SetText`) or WebView2 (`chrome.webview.hostObjects.ErrorText.SetText`).
- Utilities: `CreateBlob(url)` (fetch to object URL), `OnCacheComplete(i, keepHidden)` (sets `Cached`, removes `MediaHidden` unless `keepHidden`), `IsAttached(el)`, `MustBeAttached(el)` (temporarily appends off-screen with `position:fixed; left:-10000px; opacity:0` and returns a restore function), `delay(ms)`, and `waitEvent2(el, ev1, ev2, fn, timeout)`. The last two duplicate the global common.js functions of the same name.

#### `media_player_image.js` — `MediaPlayerParamsImage`, `MediaPlayerImage`
- Params: `Crop, Duration=10, Effect, EffectParams, EffectSizeFromImage=true, Transparent, AdaptiveSize` (defaults to true for `.svg` URLs).
- Element: `<img>` (`object-fit: contain` when adaptive). `Cache` sets `src` and waits for `load`/`error` (global `waitEvent2`), reads `naturalWidth/Height`, sets `AspectRatio` when adaptive, then `ApplyClip`.
- Playback is a **virtual timeline**. `Play` starts a `setTimeout` for the remaining `Duration`. At the end it either loops (resets the position, re-arms the timer and calls `OnLoop`) or pauses and calls `OnEnded`. `GetPos` derives the position from `performance.now() - StartedAt`. `Pause/Stop/Seek/Once` manipulate `CurrentPosition` and the timer. A `Duration <= 0` means "infinite/still".

#### `media_player_video.js` / `media_player_audio.js`
- Params: video `StartAt, EndAt, Volume=100, Stream (0 = both, 1 = video only, 2 = audio only), Transparent`; audio `StartAt, EndAt, Volume=100`.
- Element: `<video muted>` / `<audio muted>`. A `<source src>` child is appended in the static `LoadVideo(e, url)` / `LoadAudio(e, url)`, which wait for `loadeddata`/`error` and then poll until `readyState >= 3`.
- The `StartAt/EndAt` window is enforced in `ontimeupdate`. When `currentTime` reaches `endTime` the player either seeks back to `StartAt` (loop) or pauses at the end and fires `OnEnded`. A "LoopWatch" flag detects native `loop` wrapping back to 0 when `StartAt > 0` and re-seeks to `StartAt` (firing `OnLoop`). `onended` covers the natural end.
- Volume is applied as `Volume * Params.Volume * 0.01` to `element.volume`. Mute toggles `element.muted`. `Loop/Once` sets `element.loop`. `GetPos` is `element.currentTime`. With `Stream==2` the video element gets 0 size and opacity 0.
- The two files are near line-for-line copies (Stop still contains a commented-out "replace element" path).

#### `media_player_youtube.js` — `MediaPlayerParamsYouTube`, `MediaPlayerYoutube`
- Params: `Width=1920, Height=1920, StartAt, EndAt, Volume=100, Stream`, and a default `Crop` of 21.875% top and bottom (it crops the square iframe to 16:9).
- Element: an iframe from common's `createIFrame()`, with `sandbox="allow-same-origin allow-scripts allow-presentation"`, `allow="autoplay"` and a fixed pixel size. Each instance gets an incrementing `InstanceId` (static `GlobalInstanceId`).
- `Cache` attaches the iframe off-screen if needed (`MustBeAttached`), loads `https://www.youtube-nocookie.com/embed/<code>?...enablejsapi=1&controls=0&mute=1&loop=1&playlist=<code>...`, then performs the **YouTube IFrame postMessage protocol by hand** (no YouTube JS API library). It repeatedly posts `{"event":"listening","id":<InstanceId>,"channel":"widget"}` until messages come back. It listens on `window` `message`, parses JSON, filters on `id == InstanceId`, and re-dispatches each as a DOM `CustomEvent("YT" + event)` on the iframe (`YTonReady`, `YTinfoDelivery`, `YTonError`, ...). It retries the connection up to 5 times with exponential backoff (50 ms up to 5 s).
- Commands: `SendCommand(func, argsJson)` posts `{"event":"command","func":...,"args":...}`. `WaitCommand` sends one and waits up to 500 ms for the next `YTinfoDelivery`.
- Position tracking and the start/end/loop window are driven by `infoDelivery.currentTime`. To show a paused frame at an exact time it uses `PauseGreaterThan`: it seeks to slightly before the time, plays muted, and pauses + seeks once the time passes. This is used for the initial cue (`YTInitCompleted`), `Stop` and paused `Seek`.
- Volume: `setVolume [Volume*Params.Volume]` + `unMute`, or `mute`.

#### `media_player_text.js` — `MediaPlayerParamsText`, `MediaPlayerText`, `MediaPlayerTextTexture`
- `MediaPlayerParamsText extends TextStyle` and adds `CollageSeparator, CollageColumns=2, Crop, Duration=10, Effect, EffectParams, EffectSizeFromImage, Transparent, AdaptiveSize, DebugImage`.
- `data` is the text itself, not a URL. When `CollageSeparator` is set it is split into several texts and rendered as a grid.
- `Cache` optionally loads a web font (`FontUrl`/`FontUrlName` through `FontFace` + `document.fonts.add`). It renders the text with `CanvasTools.CreateTextImageUrl` or `CreateTextGridUrl` (a WebP blob URL) into an `<img>`. Collage grid stats are written into `EffectParams.FxProps` (`TileCountX/Y, TileWidth/Height, PageWidth/Height, ImageWidth/Height, Border, UniqueCount`), which lets a collage shader receive the grid layout as `//var:` constants.
- Its timeline methods are identical copies of `MediaPlayerImage`'s.
- `MediaPlayerTextTexture(gl, params, text)`: the texture-provider version. It renders to a canvas and uploads it as a GL texture. Otherwise it is identical to `MediaPlayerImageTexture` (see below).

#### `canvasTools.js` — `TextGradientStop`, `TextLinearGradient`, `TextStyle`, `CanvasTools`
- `TextStyle` holds the defaults for font, letter spacing, fill/stroke color or gradient, stroke width/cap, fill/stroke shadow, margins, `StrokeFirst`, and `AttachTo` (debug: append the canvas).
- `CanvasTools` (static): `CreateLinearGradient(x1,y1,x2,y2, p0,c0, p1,c1, ...)` (uses varargs), `BuildCanvasGradient`, `ComputeMargins` (grows the margins by shadow and stroke extents and caches them on `style.ComputedMargins`), `ApplyMargins`, `MeasureText`, `RenderText` (stroke and fill passes with separate shadows).
- Image producers: `CreateTextImage` (canvas) / `...Blob` / `...Url`, `CreateTextGrid(texts, style, columns, stats)` / `...Blob` / `...Url`, and `CreateNumberImage(style, extraSpacing, thousandSep, decimalSep, prefix, suffix)` / `...Blob` / `...Url`. The number image is a glyph atlas for counter effects: 11 rows of digits (1..9, 0, 1) in one column, and separator/prefix/suffix in a second column. Width metadata is stored as expando properties on the canvas (`NumberWidth`, `OtherWidth`, `ThousandsSeparatorWidth`, ...). `CreateCanvasImageBlob/Url` convert a canvas. The default mime is `image/webp`.
- There is no DOM attachment and no CSS. These are pure 2D-canvas utilities, also used directly by `mediaView/textEffect.js` and `TextDemo.html`.

#### `media_effect_program.js` — `EffectProgram`, `EffectProgramData`
- `EffectProgram(gl, vs, fs, program)`: holds the shaders and the program, plus `CompilationTime`. `Dispose()` deletes them.
- `EffectProgramData(gl, src, fileName)`: parses the GLSL source once.
  - Sets up async compilation through `KHR_parallel_shader_compile` (the extension is cached as `gl.PCompile`), polling every 5 ms with an abort callback. Without the extension it checks synchronously.
  - Debug flag: `window.ShaderDebug`, initialized from the `?ShaderDebug=true` query parameter. When set it logs the prefix/suffix, the final sources and the translated sources (`WEBGL_debug_shaders`).
  - Line scan: trims every line and detects `precision`, `main`, `mainImage`, derivative functions and extension, and `texture2DLod` and extension.
  - **Parameter annotations**: a line `const <type> <name> = <value>; // var: {json5-ish}` becomes an editable parameter. Supported types are `float, int, bool, vec2, vec3, vec4` (`TypeMap`). Unquoted keys in the comment JSON are quoted before `JSON.parse`. The descriptor keys are `name` (display name), `type` (edit type, for example `col` / `colhdr`, which makes vec3/vec4 objects `{Red,Green,Blue[,Alpha]}` instead of `{x,y,z[,w]}`), `min`, `max`, `step` and `desc`.
  - For each annotated line it records a default value in `scriptProps` and a closure that rewrites that source line with a new literal (`ScriptPropsApply(values)`). It also builds a member descriptor in the **SysWeaver type-info schema used by the generic editor** (`Name, TypeName` such as `System.Single` / `glsl.vec3[_col]`, `Min, Max, Default, Flags` (256 for a scalar, 512 for an object, `|8` when min and max are both present), `EditParams, Summary, ...`). These are exposed as `ScriptTypeInfo` (a synthetic type `EffectProps.<type_name_...>`), `ScriptMember` (member `EffectProps`) and `ScriptDefText` (default JSON).
  - Builds `Prefix`/`Suffix`: adds `#extension` lines that are missing, `#define texture2DLod texture2D`, and `precision highp float` if absent. **ShaderToy adaptation**: if there is `mainImage` but no `main`, it declares the `iResolution, iTime, iTimeDelta, iFrameRate, iFrame, iMouse, iDate` uniforms and appends `void main(){ mainImage(gl_FragColor, gl_FragCoord.xy); }`.
- `Compile(vars, defaultVS, abortCheckFn)`: applies `vars` to the source lines, then builds the full source. If the source contains both `#ifdef VERTEX` and `#ifdef FRAGMENT`, the same source is compiled twice with `#define VERTEX` / `#define FRAGMENT` prepended. Otherwise it uses `defaultVS` and the fragment source. It compiles, links and returns an `EffectProgram`, `null` on error (the error is logged and sent to `MediaPlayerTools.SetError`), or the string `"Aborted"`.
- `ValidateVars(jsonString)`: merges the supplied values over the defaults, drops unknown keys, and returns JSON (or `null` if the effect has no parameters).

#### `media_player_effect.js` — `MediaPlayerParamsEffect`, `MediaPlayerEffect`, `MediaPlayerImageTexture`
- Params: `Width=1920, Height=1080, Speed=1, Offset, LimitSize, DpiAdjust=false, DpiScale=1, Static, Transparent, AdaptiveSize=true, WrapU, WrapY, Texture, WebGL2, MipMap, AntiAlias, ScrollId, MouseId, MouseEnable, ImageSize, ShowVars, FxProps, ForceMode ("dark"/"light"), DarkModeSwitch=200 (ms), CustomX/Y/Z, CustomAnimX/Y/Z`.
- Constructor `(url, params, createTextureFn)`: creates a `<canvas>` (with expando `canvas.This = player`) at the CSS size `Width×Height` and calls `InitBase`. Dark mode is fixed by `ForceMode`. Otherwise it is tracked through `MediaPlayerTools.AddDarkModeChangeEvent`, optionally smoothed with common/anim.js `SmoothBool` (`Smoothness.Smooth`, `DarkModeSwitch` ms) when `Smoothness` exists and the effect is not `Static`. `CustomX/Y/Z` become `{Get, Set}` animators from `GetAnimFn(spec, value)`, where `spec` is null (a plain value), a function, or a CSV string `"SmoothBool,delay,smoothness" | "SmoothValue,delay" | "Spring,freq,len,decay,snap" | "Bounce,v0,g,restitution,restV,normalize,startAtPeak"` (classes from `common/anim.js`).
- `Cache(keepHidden)`: `fetch(url)` of the `.glsl` (it keeps the `etag` in `Etag`), then `Create()`.
- `Create()`: `InitGL()` gets a `webgl`/`webgl2` context (alpha = `Transparent`, no depth/stencil, retries without antialias), enables the derivative and LOD extensions, creates a fullscreen-triangle vertex buffer `[-1,1, 3,1, -1,-3]`, calls `createTextureFn(gl)`, and sets up an `EXT_disjoint_timer_query` if available. It then builds `EffectProgramData`, calls `texture.Cache(haveLod)`, which may resize the effect to the image size (`EffectSizeFromImage`; it also sets `AdaptiveSize=false`), validates `FxProps` into `Params.EffectProps`, exposes `ScriptTypeInfo/ScriptMember`, compiles, sets the clear color (transparent, or opaque green `0,0.5,0,1`), renders once if attached, installs a `ResizeObserver` (it stores `ClientWidth/Height` and re-renders when idle), resolves `ScrollElement` and the mouse element by id. `GetElementFromId` searches up to 10 parent frames and falls back to `top.document.body`. Mouse tracking is `mousemove` → `MouseX/Y` (client coordinates).
- `Compile(abortCheckFn)`: compiles with the default VS `attribute vec2 position; gl_Position = vec4(position,0,1)`, then caches uniform setter closures (see uniforms below) and binds the texture to unit 0. `LazyCompile()` debounces recompiles (50 ms, restarting while edits keep arriving), for live parameter editing.
- **Render loop**: `Play()` validates the context, records `ResumeTime`, sets `IsAnimating` and runs `requestAnimationFrame(renderFrame)` until `Paused` (or after one frame when `Static`). `static render(t, animTime)`:
  - time = `(animTime - RenderTime)/1000 + ResumeTime`.
  - Backing-store size: `ClientWidth/Height * devicePixelRatio * DpiScale [* global adaptive DpiScale]` when `AdaptiveSize`, otherwise `Width/Height * DpiScale`. `LimitSize` clamps it to the configured max/min.
  - It skips the draw when the canvas fails `checkVisibility()`.
  - It sets the uniforms, calls the optional `t.OnRender(gl, player)` hook (used by counter effects to upload extra uniforms), and draws 3 vertices.
  - Performance measurement: a GPU timer query if available (thresholds 12/2 ms), otherwise frame-to-frame time (35/17 ms). `AccTime` keeps the minimum over 32 frames in `AvgDrawTimeMs`. With `DpiAdjust` the global `MediaPlayerEffect.DpiScale` is halved or doubled (0.125..1) and persisted.
- `Validate()`: if the WebGL context is lost it releases GL, replaces the canvas with a clone and runs `Create()` again (on Play, Stop, and paused Seek). `DebugLoseContext()` forces a context loss for testing.
- `Stop` resets to t=0 and renders one frame. A paused `Seek` renders one frame at the new time. `OnDispose` pauses, waits for the loop to stop, unregisters dark mode, removes the element, frees GL and removes the mouse listener.
- `static GetImage(cacheName, url, params, createTextureFn)`: a thumbnail generator used by the `*EffectsLib.html` galleries. It renders the effect off-screen at t=5 s, waits for a perf sample, and turns the canvas into a JPEG data URI. It returns `[dataUri, compilationTimeMs, avgDrawTimeMs]` (or `[null, error]`). The result is cached in localStorage and revalidated by re-fetching the `.glsl` and comparing etags.
- `MediaPlayerImageTexture(gl, params, url)`: the texture provider for an image. `Cache(haveLod)` loads an `Image`, uploads it flipped-Y with clamp/linear filtering, creates mipmaps when `haveLod || EffectParams.MipMap` (WebGL1 needs power-of-two sizes), and returns `[w,h]` if `EffectSizeFromImage`, otherwise `null`. `ApplyClip(params)` turns the `Crop` into a UV scale/offset. `Apply(texUvLoc, texSizeLoc)` binds the texture and sets the `texUv`/`texSize` uniforms. `Dispose()` deletes the texture. Texture providers are duck-typed: `Cache(haveLod)`, `Apply(uv, size)`, `ApplyClip(p)`, `Dispose()`, `Width/Height`.

### Player model

- **No inheritance.** Every player is a standalone class that gets its shared behavior from `MediaPlayerTools.InitBase` (mixin-style defaults plus shared fields). Each player has a sibling `MediaPlayerParams*` class whose field initializers are the defaults; the constructor does `Object.assign(new Params(), params)` and stores the result in `player.Params`.
- **Common player interface** (duck-typed, used by `mediaView/media.js` and the effect pages):
  - State fields: `Element`, `Params`, `Width/Height` (native size), `Clip*`, `UseClip`, `AspectRatio`, `HaveVisual`, `HaveAudio`, `CanPlay`, `Cached`, `Paused`, `Muted`, `Looping`, `Volume` (0..1), `Duration`, `Url/Source`.
  - Async methods: `Cache(keepHidden)` → bool, `Play`, `Pause`, `Stop`, `Seek(sec)`, `Loop`, `Once`, `Mute`, `UnMute`, `SetVolume(0..1)`, `ResetVolume`, `OnDispose`.
  - Sync methods: `GetPos()`, `Show()`, `Hide()`, `ApplyClip(params)`, `GetTrueVolume()`.
  - Optional hooks: `MessageElement` (receives `mediaLoop`/`mediaEnded` DOM events), `OnClipChanged`, and for effects `OnRender(gl, player)`.
  - Effect-only: `ScriptTypeInfo/ScriptMember` (the editable schema), `LazyCompile`, `CompilationTime` (-1 = compiling, -2 = aborted, -3 = failed), `AvgDrawTimeMs/TotalTime/MeasureCount/ResetCounters`, `SetCustomX/Y/Z`, `DebugLoseContext`.
- **Lifecycle**: `MediaPlayer.Create` → append `player.Element` to the DOM → `await player.Cache(keepHidden)` → `MediaPlayerTools.UpdateSize(...)` on resize → `Play()` / `Show()` → optionally `SetupMediaMessages(player)` → `OnDispose()`.
- **Remote-control message contract** (`SetupMediaMessages`, `window.postMessage` with `{Type, ...}`): `MediaPlay`, `MediaPause`, `MediaStop`, `MediaOnce`, `MediaLoop`, `MediaMute`, `MediaUnMute`, `MediaSetVolume {Volume|Value}`, `MediaSeek {Time|Position|Value}`, and when supported `MediaDebugLoseContext` and `MediaCustomX/Y/Z {Value, Anim?, Start?}`. Outgoing: `{Type:"PageLoaded"}` to the parent/opener. This is how a hosting page (another iframe/window, or a native host) drives a player page such as `mediaView/backgroundEffects/index.html`.
- **Shader uniform conventions** (each one is optional and looked up by name):
  | Uniform | Value |
  |---|---|
  | `time` or `iTime` (float) | seconds × `Speed` + `Offset` |
  | `iTimeDelta` | located, but no per-frame value is set |
  | `resolution` (vec2) / `iResolution` (vec3, z = 0) | backing-store pixels |
  | `mouse` (vec2) / `iMouse` (vec4, zw = 0) | mouse client position × dpi |
  | `scroll` (vec4) | scrollLeft/Top × dpi, plus scroll ratios, of `ScrollId`'s element |
  | `iFrame` (int) | frame index |
  | `iDate` (vec4) | year, month, day, seconds of day (fixed `2001-01-01` when `Speed <= 0`) |
  | `customDark` (vec4) | CustomX, CustomY, CustomZ, darkness 0..1 (smoothed) |
  | `tex` (sampler2D, unit 0), `texUv` (vec4: uv scale.xy, offset.xy for crop/flip), `texSize` (vec2) | the image or text texture |
  | `position` attribute | fullscreen triangle |
  - Effects are written GLSL ES 1.0 / glslsandbox style (`uniform float time; uniform vec2 resolution;`), or ShaderToy style (`mainImage`, adapted automatically).
  - Effect parameters are compile-time `const`s annotated with `// var:{...}`. Changing a parameter means **recompiling** the shader (source rewrite), not setting a uniform.
  - Per-effect default values can come from a sibling `.json` (loaded by the mediaView pages, not by this folder).

### Server interaction

- The player scripts call **no SysWeaver API endpoints**. All network access is plain resource loading:
  - `fetch(effectUrl)` with `mode:"cors"` for `.glsl` sources (relative paths such as `imageEffects/X.glsl` under `mediaView/`, or absolute external URLs). The `etag` header is used for the thumbnail cache.
  - Media URLs are loaded through `<img>`, `<video>/<audio> <source>` and `Image()`.
  - YouTube goes to `youtube-nocookie.com/embed/...`, talking to the iframe over postMessage.
  - Font URLs go through `FontFace`.
- The effect file lists come from the Media service, but are fetched by the mediaView pages (not here): `MediaService` has `[WebApiUrl("../mediaView/Api/")]` with `GetBackgroundEffects`, `GetImageEffects`, `GetCollageEffects`, `GetCounterEffects`, each returning `.glsl` base names enumerated from the server's `mediaView/<folder>` endpoints, plus `GetGoogleMapsKey` (Dev role).
- There are no `${...}` template variables in this folder.
- Query parameter read directly: `ShaderDebug=true` (in `EffectProgramData`).
- Native host hooks: CefSharp `ErrorText` binding, WebView2 `chrome.webview.hostObjects.ErrorText`.

### State and persistence

- `localStorage["SysWeaver.Media.EffectDpiScale"]`: the global adaptive DPI scale for effects (read at class definition into `MediaPlayerEffect.DpiScale`, written by `AdjustDpi`).
- `localStorage["SysWeaver.EffectInfo.<cacheName>.<hashString(url)>"]` = `{R:[dataUri, compileMs, drawMs], T: etag}`: the effect thumbnail cache (`GetImage`).
- In-memory statics: `MediaPlayer.CurrentUrl`, `MediaPlayerTools.HaveTested/Result` (unmute probe), `MediaPlayerTools.DarkModeEventsMap/DarkModeClear`, `MediaPlayerYoutube.GlobalInstanceId`, `window.ShaderDebug`, `window.MediaLoadAll` (input flag), `gl.PCompile` (expando on the GL context).
- Messaging:
  - `window` `message` (`Media*` commands in, `PageLoaded` out).
  - DOM `CustomEvent`s: `mediaLoop`/`mediaEnded` on `MessageElement`, `AudioEnabled` on `document.body`, `YT*` on the YouTube iframe.
  - `MutationObserver` on the **top** window's `<html>`/`<body>` for dark mode.
- Blob URLs created for text images are never revoked by these scripts.

### Dependencies

- **From `common/common.js`** (global functions): `includeJs` (media_player.js), `waitEvent2` and `delay` (image, video, audio, youtube, text; the effect code uses the `MediaPlayerTools.*` copies instead), `createIFrame` (YouTube), `hashString` and `bufferToBase64` (`MediaPlayerEffect.GetImage`).
- **From `common/anim.js`** (optional, checked with `typeof Smoothness`): `SmoothBool`, `SmoothValue`, `SpringAnimation`, `BounceAnimation`, `Smoothness`.
- **CSS**: no stylesheet in this folder. The consumer must define `.MediaHidden` (in `mediaView/media.css` and the `*EffectsLib.html` pages).
- **Browser APIs**: WebGL1/2, `KHR_parallel_shader_compile`, `EXT_disjoint_timer_query`, `OES_standard_derivatives`, `EXT_shader_texture_lod`, `WEBGL_debug_shaders`, `WEBGL_lose_context`, `ResizeObserver`, `Element.checkVisibility`, `FontFace`, `canvas.letterSpacing`.
- **Consumers**:
  - `mediaView/media.js` (`mediaCreatePlayer` for the preview UI with controls and the parameter editor through `Edit.AddType(player.ScriptTypeInfo)`, and a full-page player used by `MediaPreview.html`/`MediaView.html` that also reports duration, fps and compile time to a screenshot host `shost.doIt`).
  - `mediaView/textEffect.js` (text/counter effects, `OnRender` hook, `CreateNumberImageUrl`).
  - The `mediaView/{background,image}Effects/index.html` stand-alone effect pages (construct `MediaPlayerEffect` directly, then `UpdateSize`, `SetupMediaMessages` and `PostPageLoaded`).
  - `textEffects/` and `collageEffects/index.html` (they build a `MediaPreview.html?type=5|0&props=...` iframe URL).
  - `*EffectsLib.html` (thumbnails through `GetImage`).
  - `TextDemo.html` (`CanvasTools`).
- **C# mirror**: `SysWeaver.MicroService.Media/Media/*.cs` (`MediaImage`, `MediaVideo`, `MediaAudio`, `MediaYouTube`, `MediaEffect`, `MediaCrop`, `MediaTypes`). Field names match the JS params, and the values are serialized as JSON into `props`.

### Structural observations

- Global-class script style with load-order coupling (`TextStyle` before `MediaPlayerParamsText`; tools before players). Lazy loading lives in an `async` factory, with a sync variant that guards with `typeof X === "function"`.
- "Interface" through mixin: `InitBase` installs default methods only when absent, so whether behavior comes from the class or the default depends on whether the class defines the method. Several class methods are sync (`Pause`/`Stop` of image/text) while the defaults are async.
- Heavy duplication:
  - `MediaPlayerImage` and `MediaPlayerText` share an identical timer-based timeline.
  - `MediaPlayerVideo` and `MediaPlayerAudio` are near-identical.
  - `MediaPlayerImageTexture` and `MediaPlayerTextTexture` share identical upload, mipmap, clip and apply code.
  - `SetCustomX/Y/Z` are copies of each other.
  - `CreateTextGrid` and `CreateNumberImage` share measuring code.
  - The `*Blob`/`*Url` wrappers repeat each other.
  - `MediaPlayerTools.delay/waitEvent2` duplicate common.js.
  - Commented-out legacy code (old `ApplyClip`, blob video cache, element replacement in `Stop`) is still present.
- Parameter mirroring with the C# types is by name only:
  - JS `MediaTypes` includes `Text=5`, which the C# enum lacks; C# has `MediaVimeo`, which JS has no player for.
  - `MediaEffect.DpiAdjust` defaults to true in C# and false in JS.
  - JS params declare `WrapY` while C# uses `WrapV`; the render accepts either name.
- Effect parameter editing rewrites the GLSL source and recompiles (no uniforms). The editable schema is hand-built in the server's type-info JSON format (`Flags` bit values, `System.Single` type names), which ties this code to the generic editor in another project.
- Cross-window reach: dark-mode detection and `GetElementFromId` read `window.top` / parent frames directly, which assumes same-origin framing. Control is through untyped `postMessage(..., "*")` with string `Type` keys, and there is no origin check.
- Expando properties are used as data channels: `canvas.This`, `gl.PCompile`, number-atlas widths on the canvas, `style.ComputedMargins`, grid stats written into `EffectParams.FxProps`.
- The "with effect" composition inverts the player: an Image/Text with `Effect` becomes a `MediaPlayerEffect` that owns a texture provider. So `player.Params` is the effect params, not the image/text params.

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function; "default" in an `InitBase` row means the member is installed on the player instance only when the class does not already define it. Every file declares plain global classes (no modules, no `export`).

### `media_player.js`

Entry point of the folder, loaded with a static `<script>` tag by consuming pages (for example `mediaView/media.js` hosts, `MediaPreview.html`, `MediaView.html`, the effect `index.html` pages). It defines the global classes `MediaTypes` and `MediaPlayer`, and runs `MediaPlayer.Init()` immediately at load. `Init` loads the shared helper scripts with common.js `includeJs`, relative to this script's own URL (`document.currentScript.src`).

#### Globals
| Name | Kind | Description |
|---|---|---|
| `MediaTypes` | class (static constants) | Media type ids mirroring the C# `MediaTypes` enum (plus `Text`). |
| `MediaPlayer` | class (static factory) | Script loader and player factory. |
| `MediaPlayer.Init()` | immediately executed code | Called at the end of the file; not awaited. |
| `window.MediaLoadAll` | input flag (read) | When truthy, `Init` loads every player script eagerly and `Create`/`CreateSync` skip lazy loading. Set by the host page before loading this script. |

#### class `MediaTypes`
Static constant holder; never instantiated.
| Member | Kind | Signature | Description |
|---|---|---|---|
| `Image` | static field | `= 0` | Image file (`MediaPlayerImage`, or `MediaPlayerEffect` + `MediaPlayerImageTexture` when `Effect` is set). |
| `Video` | static field | `= 1` | Video file (`MediaPlayerVideo`). |
| `Audio` | static field | `= 2` | Audio file (`MediaPlayerAudio`). |
| `YouTube` | static field | `= 3` | YouTube clip; data is the video id (`MediaPlayerYoutube`). |
| `Effect` | static field | `= 4` | GLSL pixel shader effect (GlslSandbox / ShaderToy style); data is the `.glsl` URL (`MediaPlayerEffect`). |
| `Text` | static field | `= 5` | Stylized text, optionally with an effect; data is the text itself (`MediaPlayerText`, or `MediaPlayerEffect` + `MediaPlayerTextTexture`). |
| `Names` | static field | frozen `["Image","Video","Audio","YouTube","Effect","Text"]` | Display names indexed by type id. |
| `GetTypeName` | static method | `GetTypeName(mediaType)` → string | Returns `"SysWeaver.MicroService.Media.Media" + MediaTypes.Names[mediaType]` (the C# params type name). |

#### class `MediaPlayer`
Static factory; never instantiated.
| Member | Kind | Signature | Description |
|---|---|---|---|
| `Init` | static async method | `Init()` → Promise | Stores `document.currentScript.src` in `CurrentUrl`, loads `media_player_tools.js` and `canvasTools.js` in parallel with `includeJs`. If `window.MediaLoadAll`, also loads `media_effect_program.js` and all `media_player_*.js` files, then awaits `MediaPlayerTools.CanUnMute()`. |
| `CurrentUrl` | static field | string | URL of `media_player.js`; base for the relative `includeJs` calls. |
| `Create` | static async method | `Create(type, data, mediaParams)` → Promise<player \| null> | Lazily loads (unless `MediaLoadAll`) the script(s) for `type` and returns a new player: Image/Text with `mediaParams.Effect` → `new MediaPlayerEffect(mediaParams.Effect, mediaParams.EffectParams, gl => new MediaPlayer{Image\|Text}Texture(gl, mediaParams, data))`; otherwise `MediaPlayerImage/Video/Audio/Youtube/Effect/Text(data, mediaParams)`. Unknown type → `null`. |
| `CreateSync` | static method | `CreateSync(type, data, mediaParams)` → player \| null | Same dispatch as `Create` without loading anything. When `MediaLoadAll` is not set it checks with `typeof X === "function"` that the needed classes exist and throws `Error("Must use CreateAsync when lazy loading (or perform manual init)!")` otherwise. |

### `media_player_tools.js`

Shared helpers for every player, loaded by `MediaPlayer.Init()` (always). Defines the global class `MediaPlayerTools` (static members only). `InitBase` is the "base class" substitute that every player constructor calls.

#### Globals
| Name | Kind | Description |
|---|---|---|
| `MediaPlayerTools` | class (static) | Player mixin, clipping, sizing, dark-mode tracking, remote-control messages, misc. async helpers. |

#### class `MediaPlayerTools`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `InitBase` | static method | `InitBase(i, e, params)` | Initializes player `i` with element `e`: sets `Cached=false`, `Element=e`, `Width=Height=0`, `CanPlay` (false if unset), `Volume=0.25`, `Muted=true`, `Paused=true`, `Looping=true`, `HaveAudio`/`HaveVisual`/`Duration` (false/false/0 if unset), `UseClip` (true if undefined, else coerced to bool); adds class `MediaHidden` to `e`. Installs the default members listed below when absent. `OnEnded` is always (re)assigned. |
| `i.OnDispose` | default (async) | `() ` | Awaits `i.Stop()`. |
| `i.SetVolume` | default (async) | `(v)` | Sets `i.Volume = v`. |
| `i.UnMute` / `i.Mute` | default (async) | `()` | Set `i.Muted` false / true. |
| `i.Play` / `i.Pause` / `i.Stop` | default (async) | `()` | Set `i.Paused` false / true / true. |
| `i.Seek` | default (async) | `(time)` | No-op. |
| `i.GetPos` | default | `()` → 0 | Always 0. |
| `i.Loop` / `i.Once` | default (async) | `()` | Set `i.Looping` true / false. |
| `i.Show` | default | `()` | Restores the saved inline style (`i.OldStyle`) if any and removes `MediaHidden`. |
| `i.Hide` | default | `()` | Adds `MediaHidden`, saves the `style` attribute in `i.OldStyle` and removes it. |
| `i.ResetVolume` | default (async) | `()` | Forces `SetVolume` to re-apply the current volume (sets `Volume=-1` first so the equality short-circuit in players is bypassed). |
| `i.GetTrueVolume` | default | `()` → number | 0 when muted; else `Volume * params.Volume * 0.01` when `params.Volume` exists, otherwise `Volume`. |
| `i.ApplyClip` | default | `(ap)` | Calls `MediaPlayerTools.ApplyClip(i, ap)`. |
| `i.OnLoop` | default | `()` | Dispatches `CustomEvent("mediaLoop", {detail: i})` on `i.MessageElement` if set. |
| `i.OnEnded` | always assigned | `()` | Dispatches `CustomEvent("mediaEnded", {detail: i})` on `i.MessageElement` if set. |
| `IsDarkMode` | static method | `IsDarkMode()` → bool | True when the computed `color-scheme` of `window.top.document.body` is `"dark"`. |
| `AddDarkModeChangeEvent` | static method | `AddDarkModeChangeEvent(fn)` → bool | Registers `fn(isDark)` in `DarkModeEventsMap` (false if already registered). On the first registration it creates two `MutationObserver`s on `window.top`'s `<html>` and `<body>` (`style`/`class` attributes) and stores a disconnect function in `DarkModeClear`. |
| `onChange` | inner (of `AddDarkModeChangeEvent`) | `()` | Re-evaluates `IsDarkMode()`; when it changed, calls every registered callback with the new value (exceptions swallowed). |
| `DarkModeClear` | static field (function, transient) | `()` | Set while observers exist; disconnects both observers and deletes itself. |
| `RemoveDarkModeChangeEvent` | static method | `RemoveDarkModeChangeEvent(fn)` → bool | Unregisters `fn` (false if unknown); when the map becomes empty calls `DarkModeClear`. |
| `DarkModeEventsMap` | static field | `new Map()` | Registered dark-mode callbacks (`fn → true`). |
| `ComputeClip` | static method | `ComputeClip(i, params)` → bool | Computes `i.ClipX/ClipY/ClipWidth/ClipHeight` (and `ClipR/ClipB` when clipped) from `(params ?? i.Params).Crop` against `i.Width/Height`. Crop uses `MarginTop/Right/Bottom/Left` pixels, or `Margin*P` percentages when `MarginAsPercentage`. Returns false (full-size clip) when there is no crop or all margins are ≤ 0. |
| `ApplyClip` | static method | `ApplyClip(i, params)` | Runs `ComputeClip` and sets `i.Element.style.clipPath` to `inset(top right bottom left px)` (or clears it); calls `i.OnClipChanged()` when the value changed. A commented-out older implementation (with inner `NoClip` helpers and a `Stream === 2` exception) remains in the body and is inactive. |
| `CreateBlob` | static async method | `CreateBlob(url)` → Promise<string \| null> | Fetches `url` and returns an object URL for the blob, `null` on error. Not used inside this folder. |
| `OnCacheComplete` | static async method | `OnCacheComplete(i, keepHidden)` → Promise<true> | Removes `MediaHidden` unless `keepHidden`, sets `i.Cached = true`. Final step of every player's `Cache`. |
| `CanUnMute` | static async method | `CanUnMute()` → Promise<bool> | One-time probe (result cached in `HaveTested`/`Result`): plays an embedded silent MP3 data URI unmuted at volume 0.01 in a detached `<audio>` (via `MediaPlayerAudio.LoadAudio`; returns false if that class is not loaded). On success dispatches `AudioEnabled` on `document.body`. If `play()` is rejected (autoplay policy) it installs a one-shot listener `l` on `keydown`/`mousedown`/`click` of `document.body` that sets `Result = true` and dispatches `AudioEnabled`. |
| `l` | inner (of `CanUnMute`) | async `()` | User-gesture listener described above; removes itself from all three events. |
| `HaveTested`, `Result` | static field (set dynamically) | bool | Cache of the `CanUnMute` probe. |
| `IsAttached` | static method | `IsAttached(el)` → bool | True if `el` is `document.body` or a descendant of it. |
| `PostPageLoaded` | static method | `PostPageLoaded()` | Posts `{Type:"PageLoaded"}` (target `"*"`) to `window.parent`, or to `window.opener` when not framed; errors ignored. |
| `MustBeAttached` | static method | `MustBeAttached(e)` → restore function | If `e` is not attached, appends it to `document.body` at `position:fixed; opacity:0; left:-10000px` and returns a function that removes it and restores those three style values; otherwise returns a no-op. |
| `delay` | static method | `delay(msToWait)` → Promise | `setTimeout`-based sleep (copy of common.js `delay`). |
| `waitEvent2` | static method | `waitEvent2(element, eventName1, eventName2, fn, timeout)` → Promise<Event \| null> | Resolves with the first of two events on `element` (listeners removed afterwards); calls `fn(element)` after subscribing; resolves `null` after `timeout` ms when `timeout > 0`. Copy of common.js `waitEvent2`. |
| `end` | inner (of `waitEvent2`) | `(ev)` | Clears the timeout, removes both listeners, resolves. |
| `SetupMediaMessages` | static method | `SetupMediaMessages(obj)` → unsubscribe function | Adds a `window` `message` listener mapping `data.Type` to player calls: `MediaPlay`, `MediaPause`, `MediaStop`, `MediaOnce`, `MediaLoop`, `MediaMute`, `MediaUnMute`, `MediaSetVolume` (`Volume ?? Value ?? 0`), `MediaSeek` (`Time ?? Position ?? Value ?? 0`), plus `MediaDebugLoseContext`, `MediaCustomX/Y/Z` (whole message passed) when `obj` has those methods. No origin check. |
| `h` | inner (of `SetupMediaMessages`) | `(ev)` | The message handler: looks up `ev.data.Type` in the map and calls it with `ev.data`. |
| `UpdateSize` | static method | `UpdateSize(target, player, fill, alignX = 0.5, alignY = 0.5)` → `[w, h]` \| undefined | Fits `player.Element` into `target`'s bounding rect. With `Params.AdaptiveSize` the element is sized to the rect (no transform). Otherwise it is sized to `player.Width×Height` and given `transform: scale(s) translate(dx,dy)` (`transform-origin: left top`), where `s` is the min (fit) or max (`fill`) scale of the (clipped, if `UseClip`) size and `align*` position the remainder. Returns the clipped size, `[0,0]` when it is empty, `undefined` without a player. |
| `SetError` | static async method | `SetError(text)` | Reports an error to a native host: binds CefSharp objects if `window.CefSharp` exists, then calls `SetText(text)` on `window.ErrorText` or `chrome.webview.hostObjects.ErrorText` (WebView2). Errors ignored. |

### `media_player_image.js`

Static image player. Loaded by `MediaPlayer.Init` (with `MediaLoadAll`) or lazily by `MediaPlayer.Create`. Defines globals `MediaPlayerParamsImage` and `MediaPlayerImage`. Uses common.js `waitEvent2`.

#### Globals
| Name | Kind | Description |
|---|---|---|
| `MediaPlayerParamsImage` | class | Params with defaults (mirrors C# `MediaImage`). |
| `MediaPlayerImage` | class | `<img>` player with a timer-based virtual timeline. |

#### class `MediaPlayerParamsImage`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `Crop` | field | `= null` | Crop object (`MarginTop/Right/Bottom/Left`, `MarginAsPercentage`, `Margin*P`), see `MediaPlayerTools.ComputeClip`. |
| `Duration` | field | `= 10` | Display duration in seconds; ≤ 0 means infinite/still. |
| `Effect` | field | `= null` | URL of a `.glsl` effect; when set, `MediaPlayer.Create` builds a `MediaPlayerEffect` instead. |
| `EffectParams` | field | `= null` | `MediaPlayerParamsEffect` values for that effect. |
| `EffectSizeFromImage` | field | `= true` | Effect canvas takes the image size (used by `MediaPlayerImageTexture.Cache`). |
| `Transparent` | field | `= false` | Not read by `MediaPlayerImage` itself. |
| `AdaptiveSize` | field | `= false` | Element fills its container with `object-fit: contain`; the constructor pre-sets it to `url.endsWith(".svg")` before applying `params`. |

#### class `MediaPlayerImage`
Created by `MediaPlayer.Create(MediaTypes.Image, url, params)` when no effect is given. Lifecycle: constructor → `Cache` → `Play`/`Pause`/... → default `OnDispose`.
| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `(url, params)` | Merges `params` into a new `MediaPlayerParamsImage`, creates `<img>` (with `objectFit = "contain"` if adaptive), sets `HaveVisual`, `CanPlay`, calls `InitBase`, sets `Url = Source = url`, `CurrentPosition = 0`, `Duration = Params.Duration`. |
| `Cache` | async method | `Cache(keepHidden)` → Promise<bool> | Sets `src` and waits for `load`/`error`; on success reads `naturalWidth/Height` into `Width/Height`, sets `AspectRatio` when adaptive, calls `ApplyClip()` then `OnCacheComplete`. |
| `Play` | async method | `Play()` | No-op if playing. Starts the timer: `Duration ≤ 0` → position 0 and (if not looping) immediately paused + `OnEnded`. Otherwise sets `StartedAt` and a `setTimeout` for the remaining time; on expiry it loops (position 0, re-arm, `OnLoop`) or pauses at `Duration` and calls `OnEnded`. |
| `setTimer` | inner (of `Play`) | async `()` | The timer arm/re-arm routine described above (stored in `TimeFn`). |
| `Pause` | method (sync) | `Pause()` | Clears the timer, stores `GetPos()` in `CurrentPosition`, sets `Paused`. |
| `Stop` | method (sync) | `Stop()` | Clears the timer, pauses, `CurrentPosition = 0` (no-op when already paused at 0). |
| `GetPos` | method | `GetPos()` → seconds | `CurrentPosition` when paused; 0 when `Duration ≤ 0`; else elapsed since `StartedAt`, capped at `Duration`. |
| `Once` | async method | `Once()` | Sets `Looping = false`; when playing with `Duration ≤ 0` it pauses and calls `OnEnded`. |
| `Seek` | async method | `Seek(time)` → Promise | Ignores negative times (returns `null`); clamps to `Duration`; if playing, stops the timer, sets the position and calls `Play()` again; if paused, only sets `CurrentPosition`. |
| `Params`, `Url`, `Source`, `CurrentPosition`, `StartedAt`, `TimeFn`, `AspectRatio` | fields | | Params object, source URL, paused/last position (s), `performance.now()` base, active timeout id, width/height ratio (adaptive only). Other state fields come from `InitBase`. |

### `media_player_video.js`

HTML5 video player. Loaded by `MediaPlayer.Init` (`MediaLoadAll`) or lazily by `MediaPlayer.Create`. Defines globals `MediaPlayerParamsVideo` and `MediaPlayerVideo`. Uses common.js `waitEvent2` and `delay`.

#### Globals
| Name | Kind | Description |
|---|---|---|
| `MediaPlayerParamsVideo` | class | Params with defaults (mirrors C# `MediaVideo`). |
| `MediaPlayerVideo` | class | `<video>` player with a start/end window. |

#### class `MediaPlayerParamsVideo`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `StartAt` | field | `= 0` | Start of the play window (s). |
| `EndAt` | field | `= 0` | End of the window (s); 0, ≤ `StartAt` or > duration means "to the end". |
| `Volume` | field | `= 100` | Volume percentage multiplied with the player volume (0..1). |
| `Stream` | field | `= 0` | 0 = video + audio, 1 = video only (`HaveAudio=false`), 2 = audio only (`HaveVisual=false`, element sized 0 and transparent). |
| `Transparent` | field | `= false` | Not read by `MediaPlayerVideo`. |

#### class `MediaPlayerVideo`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `(url, params)` | Merges params, creates a muted `<video>`, sets `HaveVisual`/`HaveAudio` from `Stream`, calls `InitBase`, sets `loop = Looping`, hides the element for audio-only, sets `CanPlay`, `Url = Source = url`, `AtStart = true`. |
| `Cache` | async method | `Cache(keepHidden)` → Promise<bool> | `LoadVideo`; reads `videoWidth/Height` (or 0 for audio-only), `ApplyClip()`, `Duration`, seeks to `StartAt`, computes `endTime` and the loop-watch midpoint, installs `ontimeupdate`/`onended`, then `OnCacheComplete`. |
| `e.ontimeupdate` | handler (set in `Cache`) | async `()` | Updates `Duration`. Before `endTime`: detects a native-loop wrap below the midpoint (when `LoopWatch` is set) and seeks back to `StartAt` + `OnLoop`; otherwise arms `LoopWatch` once past the midpoint (only when `StartAt > 0`, looping and playing). At/after `endTime`: seeks to `StartAt` if looping, else pauses at `endTime`, sets `AtEnd` and calls `OnEnded`. |
| `e.onended` | handler (set in `Cache`) | async `(ev)` | Natural end while playing: pause, `AtEnd = true`, `OnEnded`. |
| `LoadVideo` | static async method | `LoadVideo(e, c)` → Promise<bool> | Appends `<source src=c>` to `e`, waits for `loadeddata`/`error`, then polls every 100 ms until `readyState ≥ 3`. False on error. A commented-out blob-cache implementation (`VideoCache`) precedes it. |
| `Mute` | async method | `Mute()` | Sets `Muted` and `element.muted = true`. |
| `UnMute` | async method | `UnMute()` | Clears `Muted`; if `HaveAudio`, sets `element.volume = Volume * Params.Volume * 0.01` and unmutes the element. |
| `SetVolume` | async method | `SetVolume(vol)` | Clamps to 0..1, stores `Volume`; applies to the element when it has audio and is not muted. |
| `Play` | async method | `Play()` | From `AtEnd` seeks to `StartAt` first; sets `loop`, awaits `element.play()`; on success `Paused=false`, `AtStart=false`, updates `Duration`. Rejections are swallowed (state stays paused). |
| `Stop` | async method | `Stop()` → Promise<true \| undefined> | Unless paused at start: pauses, seeks to `StartAt`, `Paused=AtStart=true`. Contains a commented-out "replace the element" variant. |
| `Pause` | async method | `Pause()` | Pauses the element. |
| `Seek` | async method | `Seek(time)` | Clamps `time` to `[StartAt, endTime]`, updates `AtEnd`/`AtStart`, sets `currentTime`. |
| `GetPos` | method | `GetPos()` → seconds | `element.currentTime`. |
| `Loop` / `Once` | async method | `Loop()` / `Once()` | Set `Looping` and `element.loop` true / false. |
| `Params`, `Url`, `Source`, `AtStart`, `AtEnd`, `LoopWatch` | fields | | Params, source URL, at-window-start / at-window-end flags, native-loop wrap detector. |

### `media_player_audio.js`

HTML5 audio player; a near line-for-line copy of `media_player_video.js` without visuals. Loaded by `MediaPlayer.Init` (`MediaLoadAll`), lazily by `MediaPlayer.Create`, and required by `MediaPlayerTools.CanUnMute`. Defines globals `MediaPlayerParamsAudio` and `MediaPlayerAudio`.

#### Globals
| Name | Kind | Description |
|---|---|---|
| `MediaPlayerParamsAudio` | class | Params with defaults (mirrors C# `MediaAudio`). |
| `MediaPlayerAudio` | class | `<audio>` player with a start/end window. |

#### class `MediaPlayerParamsAudio`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `StartAt` | field | `= 0` | Start of the play window (s). |
| `EndAt` | field | `= 0` | End of the window (s); same rules as video. |
| `Volume` | field | `= 100` | Volume percentage. |

#### class `MediaPlayerAudio`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `(url, params)` | Merges params, creates a muted `<audio>`, `HaveVisual=false`, `HaveAudio=true`, `InitBase`, `loop = Looping`, `CanPlay`, `Url = Source = url`, `AtStart = true`. |
| `Cache` | async method | `Cache(keepHidden)` → Promise<bool> | `LoadAudio`; `Width=Height=0` and 0-size element; `Duration`; seek to `StartAt`; installs `ontimeupdate`/`onended` exactly like the video player (no `ApplyClip`). |
| `e.ontimeupdate` | handler (set in `Cache`) | async `()` | Same window/loop logic as `MediaPlayerVideo` (also clears `LoopWatch` when ending). |
| `e.onended` | handler (set in `Cache`) | async `(ev)` | Natural end while playing: pause, `AtEnd`, `OnEnded`. |
| `LoadAudio` | static async method | `LoadAudio(e, c)` → Promise<bool> | Appends `<source src=c>`, waits `loadeddata`/`error`, polls until `readyState ≥ 3`. |
| `Mute` / `UnMute` | async method | `()` | As video (`UnMute` always applies volume, no `HaveAudio` check). |
| `SetVolume` | async method | `SetVolume(vol)` | Clamp 0..1, store, apply `vol * Params.Volume * 0.01` when unmuted. |
| `Play` / `Stop` / `Pause` / `Seek` / `GetPos` / `Loop` / `Once` | (async) methods | as video | Identical behavior to the `MediaPlayerVideo` members of the same name (`Stop` also keeps a commented-out element-replacement variant). |
| `Params`, `Url`, `Source`, `AtStart`, `AtEnd`, `LoopWatch` | fields | | As video. |

### `media_player_youtube.js`

YouTube player that drives a `youtube-nocookie.com` embed iframe through the raw IFrame postMessage protocol (no YouTube JS library). Loaded by `MediaPlayer.Init` (`MediaLoadAll`) or lazily by `MediaPlayer.Create`. Defines globals `MediaPlayerParamsYouTube` and `MediaPlayerYoutube`. Uses common.js `createIFrame`, `waitEvent2`, `delay`.

#### Globals
| Name | Kind | Description |
|---|---|---|
| `MediaPlayerParamsYouTube` | class | Params with defaults (mirrors C# `MediaYouTube`). |
| `MediaPlayerYoutube` | class | iframe-based YouTube player. |

#### class `MediaPlayerParamsYouTube`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `Width` | field | `= 1920` | iframe pixel width (falls back to 1280 if falsy). |
| `Height` | field | `= 1920` | iframe pixel height (falls back to `Width * 9/16` if falsy). |
| `StartAt` | field | `= 0` | Window start (s). |
| `EndAt` | field | `= 0` | Window end (s); 0 / ≤ `StartAt` / > duration means the clip end. |
| `Volume` | field | `= 100` | Volume percentage; YouTube `setVolume` receives `Volume(0..1) * Params.Volume`. |
| `Stream` | field | `= 0` | 0 = both, 1 = video only, 2 = audio only (0×0 iframe, opacity 0). |
| `Crop` | field | `= {MarginAsPercentage: true, MarginTopP: 21.875, MarginRightP: 0, MarginBottomP: 21.875, MarginLeftP: 0}` | Default crop of the square iframe to 16:9. |

#### class `MediaPlayerYoutube`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `(code, params)` | Merges params, creates the iframe (`createIFrame()`), sets `HaveVisual`/`HaveAudio` from `Stream`, `InitBase`, `scrolling="no"`, `sandbox="allow-same-origin allow-scripts allow-presentation"`, `allow="autoplay"`, pixel size; sets `Width/Height`, `CanPlay`, `done=false`, a new `InstanceId`, `Source = "https://www.youtube.com/watch?v=" + code`, `CurrentTime = StartAt`, `PauseGreaterThan = -1`, `Duration = 0`, then `ApplyClip()`. |
| `GlobalInstanceId` | static field | `= 0` | Counter used to give each instance a unique postMessage `id`. |
| `Cache` | async method | `Cache(keepHidden)` → Promise<bool> | Temporarily attaches the iframe (`MustBeAttached`), defines `WaitCommand`, registers `messageHandler` on `window`, wraps `OnDispose` to remove it, then calls `connect()` up to 6 times (delay 50 ms doubling to max 5 s). After connecting: mutes, seeks to just before `StartAt` with `PauseGreaterThan = StartAt` and waits for `YTInitCompleted` (or `YTonError` → false). Restores the attachment state in `finally`. |
| `t.WaitCommand` | field (async function, set in `Cache`) | `WaitCommand(cmd, args)` → Promise<bool> | Sends a command and waits up to 500 ms for `YTinfoDelivery` (true) or `YTonError`/timeout (false, logs a warning). |
| `messageHandler` | inner (of `Cache`) | async `(ev)` | Parses `ev.data` JSON and ignores other ids. Marks the connection, flags `onError`. For `infoDelivery` with `currentTime`: grows `Duration`, updates `CurrentTime`, executes a pending `PauseGreaterThan` (pause, seek back, unmute if needed, fire `YTInitCompleted` once), handles the native-loop wrap (seek to `StartAt` + `OnLoop`), and at the window end either pauses at the end (`AtEnd`, `OnEnded`) or loops (`seekTo StartAt` + `OnLoop`, or waits for the native wrap when there is no `EndAt`). Always re-dispatches the message as `CustomEvent("YT" + event, {detail})` on the iframe. |
| `connect` | inner (of `Cache`) | async `()` → bool | Mutes, resets flags, loads the embed URL (`autoplay=0&mute=1&controls=0&enablejsapi=1&loop=1&playlist=<code>&playsinline=1&...`), waits for `load`, then polls with `conFn` until `YTonReady` (true) or `YTonError` (false). |
| `conFn` | inner (of `connect`) | `()` | Posts `{"event":"listening","id":<InstanceId>,"channel":"widget"}` to the iframe every 100 ms until a message arrives. |
| `this.OnDispose` | field (set in `Cache`) | `()` | Removes `messageHandler` from `window`, then calls the previous `OnDispose` (the `InitBase` default, i.e. `Stop`). |
| `SendCommand` | method | `SendCommand(command, args)` | Posts `{"event":"command","func":command[,"args":args]}` (args is a JSON text) to the iframe's `contentWindow`. |
| `Mute` | async method | `Mute()` | Sends `mute`. |
| `UnMute` | async method | `UnMute()` | If it has audio and `Volume * Params.Volume > 0`, sends `setVolume [v]` and `unMute`. |
| `SetVolume` | async method | `SetVolume(vol)` | Clamp 0..1, store; when audible and unmuted sends `mute` (v ≤ 0) or `setVolume` + `unMute`, de-duplicated through `PlayerVolume`. |
| `Play` | async method | `Play()` | Seeks to `StartAt` if `AtEnd`, then `WaitCommand("playVideo")`. |
| `Stop` | async method | `Stop()` | Unless paused at/before start: pauses logically, sets `PauseGreaterThan = StartAt`, seeks just before it, mutes if needed and plays so the handler can pause on the exact frame. |
| `Pause` | async method | `Pause()` | `WaitCommand("pauseVideo")`. |
| `Seek` | async method | `Seek(time)` | Ignores negatives/unchanged; while paused uses the `PauseGreaterThan` seek-play-pause trick, otherwise a plain `seekTo`. |
| `GetPos` | method | `GetPos()` → seconds | `CurrentTime` (last reported by `infoDelivery`). |
| `Params`, `Code`, `InstanceId`, `CurrentTime`, `PauseGreaterThan`, `PlayerVolume`, `AtStart`, `AtEnd`, `done` | fields | | Params, video id, postMessage id, last known time, pending "pause once past this time" (−1 = none), last volume sent, window flags, unused flag. |

### `media_player_text.js`

Text player and text texture provider. Loaded by `MediaPlayer.Init` (`MediaLoadAll`) or lazily by `MediaPlayer.Create`; must load after `canvasTools.js` because `MediaPlayerParamsText extends TextStyle`. Defines globals `MediaPlayerParamsText`, `MediaPlayerText`, `MediaPlayerTextTexture`.

#### Globals
| Name | Kind | Description |
|---|---|---|
| `MediaPlayerParamsText` | class | Text style + player params. |
| `MediaPlayerText` | class | `<img>` player showing rendered text. |
| `MediaPlayerTextTexture` | class | WebGL texture provider for `MediaPlayerEffect`. |

#### class `MediaPlayerParamsText` extends `TextStyle`
Inherits all `TextStyle` fields (see `canvasTools.js`) and adds:
| Member | Kind | Signature | Description |
|---|---|---|---|
| `CollageSeparator` | field | `= null` | When set, the text is split on it and rendered as a grid (`CanvasTools.CreateTextGrid*`). |
| `CollageColumns` | field | `= 2` | Grid column count. |
| `Crop` | field | `= null` | Crop object, see `ComputeClip`. |
| `Duration` | field | `= 10` | Display duration (s); ≤ 0 = infinite. |
| `Effect` | field | `= null` | Effect `.glsl` URL; makes `MediaPlayer.Create` build a `MediaPlayerEffect`. |
| `EffectParams` | field | `= null` | Effect params; the collage path creates it and writes grid stats into `EffectParams.FxProps`. |
| `EffectSizeFromImage` | field | `= true` | Effect canvas takes the text image size. |
| `Transparent` | field | `= false` | Not read by `MediaPlayerText`. |
| `AdaptiveSize` | field | `= false` | `object-fit: contain` and `AspectRatio`. |
| `DebugImage` | field | `= false` | Logs the generated image URL (texture provider also keeps an `<img>` in `Image`). |

#### class `MediaPlayerText`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `(url, params)` | `url` is the text. Same as `MediaPlayerImage`'s constructor with `MediaPlayerParamsText` (no SVG check). |
| `Cache` | async method | `Cache(keepHidden)` → Promise<bool> | Loads `FontUrl` as `FontFace(FontUrlName)` into `document.fonts` if set; renders the text with `CanvasTools.CreateTextGridUrl(texts, p, CollageColumns, FxProps)` or `CreateTextImageUrl(text, p)` (WebP blob URL), loads it into the `<img>`, reads `naturalWidth/Height`, `AspectRatio`, `ApplyClip()`, `OnCacheComplete`. |
| `Play`, `Pause`, `Stop`, `GetPos`, `Once`, `Seek` | (async) methods | as image | Identical copies of the `MediaPlayerImage` timeline members. |
| `setTimer` | inner (of `Play`) | async `()` | Same as in `MediaPlayerImage.Play`. |

#### class `MediaPlayerTextTexture`
Created by the `createTextureFn` passed to `MediaPlayerEffect` from `MediaPlayer.Create` (Text + `Effect`).
| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `(gl, params, url)` | Stores `Params`, `Url` (the text), `GL`, and a new `Texture`. |
| `Cache` | async method | `Cache(haveLod)` → Promise<[w,h] \| null> | Loads the font, renders a canvas with `CanvasTools.CreateTextGrid` / `CreateTextImage` (grid stats into `EffectParams.FxProps`), optionally (`DebugImage`) converts it to an `<img>` in `Image`, sets `Width/Height`, uploads it flipped-Y with clamp/linear filtering, generates mipmaps when `haveLod` or `EffectParams.MipMap` (WebGL1 requires power-of-two sizes; otherwise warns and skips). Returns the size when `EffectSizeFromImage`, else `null`. |
| `powerOf2` | inner (of `Cache`) | `(v)` → bool | Power-of-two test. |
| `Apply` | method | `Apply(uvScaleAndOffset, texSize)` | Binds the texture; sets `texUv` to the clip UV transform (`UvA..UvD`) or `(1,-1,0,1)`, and `texSize` to the clip or full size. |
| `ApplyClip` | method | `ApplyClip(p)` | `ComputeClip` on this provider; when clipped sets `HaveClip` and `UvA = cw/ow`, `UvB = -ch/oh`, `UvC = ClipX/ow`, `UvD = (oh-ClipB)/oh`. |
| `Dispose` | method | `Dispose()` | Deletes the GL texture. |

### `canvasTools.js`

Pure 2D-canvas text rendering utilities (no DOM attachment except the debug `AttachTo`). Loaded by `MediaPlayer.Init` (always), and directly by `mediaView/textEffect.js` users and `TextDemo.html`. Defines globals `TextGradientStop`, `TextLinearGradient`, `TextStyle`, `CanvasTools`.

#### Globals
| Name | Kind | Description |
|---|---|---|
| `TextGradientStop` | class | One gradient stop. |
| `TextLinearGradient` | class | Relative linear gradient definition. |
| `TextStyle` | class | Text rendering style (base of `MediaPlayerParamsText`). |
| `CanvasTools` | class (static) | Measuring, rendering and image producers. |

#### class `TextGradientStop`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `P` | field | `= 0` | Position 0..1. |
| `Color` | field | `= "#000"` | CSS color. |

#### class `TextLinearGradient`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `X1`, `Y1` | field | `= 0`, `= 0` | Relative start (0..1 of the text box). |
| `X2`, `Y2` | field | `= 0`, `= 1` | Relative end (default: top to bottom). |
| `Stops` | field | `= []` | `TextGradientStop[]`. |

#### class `TextStyle`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `Font` | field | `= 'bold 128px Verdana'` | CSS font. |
| `FontUrl` | field | `undefined` | Optional font file URL (loaded by the text players). |
| `FontUrlName` | field | `undefined` | Face name for `FontUrl`; should match the family in `Font`. |
| `LetterSpacing` | field | `= 0` | Extra px between glyphs (`ctx.letterSpacing`). |
| `Fill` | field | `= "#fff"` | Fill color; `'none'` disables fill. |
| `FillGradient` | field | `= null` | `TextLinearGradient` used instead of `Fill`. |
| `Stroke` | field | `= "#006"` | Stroke color. |
| `StrokeGradient` | field | `= null` | Gradient used instead of `Stroke`. |
| `StrokeWidth` | field | `= 2` | Stroke width; 0 disables stroke. |
| `StrokeCap` | field | `= "round"` | `butt` / `round` / `square`. |
| `ShadowFill` | field | `= "#000c"` | Shadow color of the fill pass. |
| `ShadowStroke` | field | `= null` | Shadow color of the stroke pass. |
| `ShadowX`, `ShadowY` | field | `= 3`, `= 5` | Shadow offset. |
| `ShadowBlur` | field | `= 12` | Shadow blur radius. |
| `MarginLeft`, `MarginTop`, `MarginRight`, `MarginBottom` | field | `= 1` each | Extra edge margins (px). |
| `StrokeFirst` | field | `= true` | Draw stroke before fill. |
| `AttachTo` | field | `= null` | Debug: element the generated canvas is appended to. |
| `ComputedMargins` | field (expando, set by `ComputeMargins`) | `[t,r,b,l]` | Margin cache. |

#### class `CanvasTools`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `CreateLinearGradient` | static method | `CreateLinearGradient(x1, y1, x2, y2, p0, c0, p1, c1, ...)` → `TextLinearGradient` | Builds a gradient; stops are read from `arguments` as (position, color) pairs after the 4th argument. |
| `BuildCanvasGradient` | static method | `BuildCanvasGradient(cc, x1, y1, x2, y2, gradient)` → `CanvasGradient \| null` | Maps the relative gradient onto the rectangle and adds the stops; `null` without a gradient. |
| `ComputeMargins` | static method | `ComputeMargins(style, useCache)` → `[top, right, bottom, left]` | Adds shadow extent (`(ShadowBlur [+StrokeWidth])/2` adjusted by the shadow offset) when a shadow applies, else half the stroke width, to the style margins. Caches in `style.ComputedMargins` (reused only with `useCache`). |
| `ApplyMargins` | static method | `ApplyMargins(m, margins)` → `[w, h, drawX, drawY, mt, mr, mb, ml]` | Integer canvas size from `TextMetrics` bounding box + margins, distributing the rounding slack evenly; returns the draw origin and adjusted margins. |
| `MeasureText` | static method | `MeasureText(cc, text, style)` → array | Sets font/letter spacing, measures, applies `ComputeMargins` + `ApplyMargins`. |
| `RenderText` | static method | `RenderText(cc, text, style, posX, posY, gradX1, gradY1, gradX2, gradY2)` | Draws stroke and fill passes (order by `StrokeFirst`), each with its own shadow and color/gradient (gradient mapped to the `grad*` rectangle). |
| `fill` | inner (of `RenderText`) | `()` | Fill pass (skipped without `Fill`/`FillGradient`). |
| `stroke` | inner (of `RenderText`) | `()` | Stroke pass (skipped without stroke color/gradient or width). |
| `CreateTextImage` | static method | `CreateTextImage(text, style = new TextStyle())` → `HTMLCanvasElement` | Canvas sized to the measured text with the text rendered. |
| `CreateTextImageBlob` | static method | `CreateTextImageBlob(text, style, mime = null, quality = 1)` → Promise<Blob> | `toBlob` of `CreateTextImage` (default `image/webp`). |
| `CreateTextImageUrl` | static async method | same params → Promise<string> | Object URL of the blob (never revoked here). |
| `CreateTextGrid` | static method | `CreateTextGrid(texts, style, columnCount = 2, stats = null)` → canvas | Forces all style margins ≥ 1, measures all texts, makes equal tiles (max width/ascent/descent + margins), renders each text centered in its tile row-major. Fills `stats` with `TileCountX/Y`, `TileCount`, `TileWidth/Height`, `PageWidth/Height`, `ImageWidth/Height` (tile − 2), `Border` (1), `UniqueCount`. Logs the size. |
| `GetWidth` | inner (of `CreateTextGrid` and of `CreateNumberImage`) | `(m)` → number | Ink width (`right - left` bounding box) or at least `m.width`; half of `m.width` when the ink width is ≤ 0. |
| `CreateTextGridBlob` | static method | `(texts, style, columnCount = 2, stats = null, mime = null, quality = 1)` → Promise<Blob> | Blob of `CreateTextGrid`. |
| `CreateTextGridUrl` | static async method | same params → Promise<string> | Object URL of that blob. |
| `CreateNumberImage` | static method | `CreateNumberImage(style, extraSpacing = 0, thousandSeparator = " ", decimalSeparator = ".", prefix = "", suffix = "")` → canvas | Glyph atlas for counters: column 1 (`NumberWidth` wide) has 11 rows with digits `1..9, 0, 1`; column 2 (`OtherWidth`) has the thousands separator in row 1, prefix row 2, suffix row 3, decimal separator row 4. Expando properties on the canvas: `NumberWidth`, `OtherWidth`, `TotalWidth`, `ThousandsSeparatorWidth`, `DecimalSeparatorWidth`, `PrefixWidth`, `SuffixWidth`. `thousandSeparator` defaults to a space only when `undefined` (`null` → empty). `extraSpacing` is normalized but not used in the layout. |
| `measureOther` | inner (of `CreateNumberImage`) | `(text)` → TextMetrics | Measures a non-digit string and grows the shared max width/ascent/descent. |
| `text` | inner (of `CreateNumberImage`) | `(www, m, i, text)` | Renders a non-digit string into row `i` of the second column. |
| `CreateNumberImageBlob` | static method | `(style, extraSpacing, thousandSeparator, decimalSeparator, prefix, suffix, mime = null, quality = 1)` → Promise<[Blob, canvas]> | Blob plus the canvas (for its width expandos). |
| `CreateNumberImageUrl` | static async method | same params → Promise<[url, Blob, canvas]> | Adds an object URL. Used by `mediaView/textEffect.js`. |
| `CreateCanvasImageBlob` | static method | `(canvas, mime = null, quality = 1)` → Promise<Blob> | Generic canvas → blob (default WebP). |
| `CreateCanvasImageUrl` | static async method | `(canvas, mime = null, quality = 1)` → Promise<[url, Blob]> | Generic canvas → object URL. |

### `media_effect_program.js`

GLSL source parsing and program compilation for `MediaPlayerEffect`. Loaded by `MediaPlayer.Init` (`MediaLoadAll`) or lazily by `MediaPlayer.Create` together with `media_player_effect.js`. Defines globals `EffectProgram`, `EffectProgramData`; sets `window.ShaderDebug` on first use.

#### Globals
| Name | Kind | Description |
|---|---|---|
| `EffectProgram` | class | A linked program + its shaders. |
| `EffectProgramData` | class | Parsed effect source, parameter schema and compiler. |
| `window.ShaderDebug` | window global | Initialized by the first `EffectProgramData` constructor from the `?ShaderDebug=true` query parameter (unless already defined); enables shader source logging and periodic draw-time logs. |

#### class `EffectProgram`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `(gl, vs, fs, program)` | Stores `GL`, `VS`, `FS`, `Program`. |
| `Dispose` | method | `Dispose()` | Deletes the program and both shaders. |
| `GL`, `VS`, `FS`, `Program` | field | | Context, vertex shader, fragment shader, program. |
| `CompilationTime` | field | ms | Set by `EffectProgramData.Compile` on success. |

#### class `EffectProgramData`
Created once per loaded effect by `MediaPlayerEffect.Create`; `Compile` can be called repeatedly with new parameter values.
| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `(gl, src, fileName)` | Sets `Url = fileName`; caches `KHR_parallel_shader_compile` as `gl.PCompile` and defines `waitCompilation`/`waitLinking`; initializes `window.ShaderDebug`; trims every line into `Lines` and scans for `precision`, `main`, `mainImage`, derivative functions/extension and `texture2DLod`/extension; parses `const <type> <name> = <value>; // var:{...}` annotations (unquoted keys are quoted before `JSON.parse`) into defaults, editor members and line-rewrite closures; builds `ScriptDefText`, `ScriptPropsApply`, `ScriptTypeInfo`, `ScriptMember` (all null/no-op without annotations); builds `Prefix`/`Suffix` (missing `#extension` lines, `#define texture2DLod texture2D`, `precision highp float`, ShaderToy uniforms + `main()` wrapper when only `mainImage` exists). Logs details when `ShaderDebug`. Annotation parse errors and unknown types are logged and skipped. |
| `t.waitCompilation` | field (function) | `(shader, abortCheckFn)` → Promise<bool> \| bool | With the parallel-compile extension: polls every 5 ms until complete (returns true early if `abortCheckFn()`), then `COMPILE_STATUS`. Without: synchronous `COMPILE_STATUS`. |
| `t.waitLinking` | field (function) | `(prog, abortCheckFn)` → Promise<bool> \| bool | Same for `LINK_STATUS`. |
| `isSpace` | inner (of constructor) | `(line, pos, falseOutSize)` → bool | Whitespace test at `pos`; out of range returns `!falseOutSize`. |
| `isIdentifier` | inner | `(line, pos)` → bool | Letter, digit, `_` or `@` at `pos`. |
| `isLetter` | inner | `(c)` → bool | Case-distinguishing letters, or non-ASCII characters accepted as a JS function name (tested via `eval`). |
| `haveIdentifier` | inner | `(line, name)` → bool | `name` occurs as a whole identifier before any `//` comment. |
| `haveSpaceIdentifier` | inner | `(line, name)` → bool | Same, but must be preceded by whitespace / line start (used for keywords and function names). |
| `t.ScriptPropsApply` | field (function) | `(values)` | Rewrites each annotated source line to `const <type> <name>=<literal>;` for every key present in `values` (no-op function when there are no annotations). |
| `Url`, `Lines`, `GL`, `Prefix`, `Suffix`, `HaveLod` | field | | Source name, trimmed (and rewritten) source lines, context, injected header/footer, whether `texture2DLod` is used. |
| `ScriptDefText` | field | JSON string \| null | Default parameter values `{name: value}`. |
| `ScriptTypeInfo` | field | object \| null | Synthetic type-info for the generic editor: `Asm: "EffectProps"`, `TypeName: "EffectProps.<type_name_...>"`, `Members` (one per annotation: `Name`, `DisplayName` (`desc.name`), `TypeName` (`System.Single/Boolean/Int32`, `glsl.vec2/3/4`, `_col`/`_colhdr` suffix for an edit type), `Min`/`Max` strings, `Flags` (256 scalar / 512 object, `\|8` when both min and max), `EditParams` (step or edit type), `Default` JSON, `Summary` (`desc.desc`)), `Flags: 512`, `Default: ScriptDefText`. |
| `ScriptMember` | field | object \| null | Member descriptor named `EffectProps` of that type (used by the parameter editor). |
| `Compile` | async method | `Compile(vars, defaultVS, abortCheckFn = () => false)` → Promise<EffectProgram \| null \| "Aborted"> | Applies `vars` via `ScriptPropsApply`, builds `Prefix + Lines + Suffix`. If it contains both `#ifdef VERTEX` and `#ifdef FRAGMENT` compiles it twice with `#define VERTEX` / `#define FRAGMENT`, otherwise uses `defaultVS`. Compiles, waits, logs translated sources when `ShaderDebug` (`WEBGL_debug_shaders`), links. Compile errors are logged and sent to `MediaPlayerTools.SetError`, returning `null`; link errors return `null`; abort returns `"Aborted"`. Sets `CompilationTime`. |
| `ValidateVars` | method | `ValidateVars(propString)` → JSON string \| null | `null` without parameters; the defaults when `propString` is empty; otherwise defaults overlaid with the parsed values, dropping unknown keys. |
| `TypeMap` | static field | `Map<string, {parseDef, member, toString}>` | Built by an IIFE for `float`, `bool`, `int`, `vec2`, `vec3`, `vec4`: `parseDef(text, editType)` parses the default literal (vec3/vec4 return `{Red,Green,Blue[,Alpha]}` for edit types `col`/`colhdr`, else `{x,y,z[,w]}`; missing components repeat the last one), `member` is the editor member template, `toString(value, editType)` produces the GLSL literal (missing vector components default to 1.0). |
| `toFloat` | inner (of the `TypeMap` IIFE) | `(v)` → string | Number text with `.0` appended when it has no decimal point. |

### `media_player_effect.js`

WebGL effect player and image texture provider. Loaded by `MediaPlayer.Init` (`MediaLoadAll`) or lazily by `MediaPlayer.Create` (with `media_effect_program.js`); also used directly by the `mediaView/*Effects` pages. Defines globals `MediaPlayerParamsEffect`, `MediaPlayerEffect`, `MediaPlayerImageTexture`. At class definition time it reads `localStorage["SysWeaver.Media.EffectDpiScale"]`. Optional dependency on common/anim.js (`SmoothBool`, `SmoothValue`, `SpringAnimation`, `BounceAnimation`, `Smoothness`); uses common.js `hashString` and `bufferToBase64` in `GetImage`.

#### Globals
| Name | Kind | Description |
|---|---|---|
| `MediaPlayerParamsEffect` | class | Params with defaults (mirrors C# `MediaEffect`). |
| `MediaPlayerEffect` | class | `<canvas>` WebGL shader player. |
| `MediaPlayerImageTexture` | class | WebGL texture provider for an image URL. |
| `MediaPlayerEffect.DpiScale` | static field, executed at load | Global adaptive DPI factor read from localStorage (default 1). |

#### class `MediaPlayerParamsEffect`
| Member | Kind | Signature | Description |
|---|---|---|---|
| `Width` | field | `= 1920` | CSS width (px) when not adaptive, and the max size for `LimitSize` (1280 if falsy). |
| `Height` | field | `= 1080` | CSS height (falls back to `Width * 9/16` if falsy). |
| `Speed` | field | `= 1` | Time multiplier for `time`/`iTime`; ≤ 0 also freezes `iDate` at `ZeroDate`. |
| `LimitSize` | field | `= false` | Clamp the backing store to the `Width/Height` extent (long side ≤ max, short side ≤ min). |
| `DpiAdjust` | field | `= false` | Enable automatic global DPI scaling from measured draw time (not for `Static`). |
| `DpiScale` | field | `= 1` | Fixed backing-store scale factor. |
| `Static` | field | `= false` | Render a single frame (re-render on resize / dark-mode change) instead of animating. |
| `Transparent` | field | `= false` | Context `alpha`; clear color transparent instead of opaque green. |
| `AdaptiveSize` | field | `= true` | Backing store follows the element's client size × devicePixelRatio; set to false when a texture dictates the size. |
| `WrapU` | field | `= false` | `REPEAT` wrap on S for the texture (applied in `render` when `ApplyTextue`). |
| `WrapY` | field | `= false` | `REPEAT` wrap on T for the texture (applied in `render` when `ApplyTextue`); `render` honors both `WrapY` and the C# name `WrapV`. |
| `Texture` | field | `= null` | Declared; not read in this folder. |
| `WebGL2` | field | `= false` | Request a `webgl2` context instead of `webgl`. |
| `MipMap` | field | `= false` | Read by the texture providers (via `EffectParams.MipMap`) to generate mipmaps. |
| `AntiAlias` | field | `= false` | Context `antialias` (retried without on failure). |
| `ScrollId` | field | `= null` | Element id whose scroll position feeds the `scroll` uniform (resolved with `GetElementFromId`; default: top body). |
| `MouseId` | field | `= null` | Element id whose `mousemove` feeds `mouse`/`iMouse`. |
| `MouseEnable` | field | `= false` | Enable mouse tracking on the top body when `MouseId` is not given. |
| `ImageSize` | field | `= false` | Declared; not read in this folder. |
| `ShowVars` | field | `= false` | Log the default effect parameters (pretty JSON) on `Create`. |
| `FxProps` | field | `= null` | Effect parameter values (object or JSON string), validated into `EffectProps`. |
| `ForceMode` | field | `= null` | `"dark"` / `"light"` to fix dark mode instead of tracking the top page. |
| `DarkModeSwitch` | field | `= 200` | Dark-mode transition time in ms (`SmoothBool`); 0 disables smoothing. |
| `CustomX`, `CustomY`, `CustomZ` | field | `= 0` each | Initial values of the `customDark.xyz` uniform channels. |
| `CustomAnimX`, `CustomAnimY`, `CustomAnimZ` | field | `= null` each | Animator spec for each channel, see `GetAnimFn`. |
| `Offset` | field | `= 0` | Added to the time uniform. |
| `EffectProps` | field (set by `Create`) | object | Validated parameter values passed to `EffectProgramData.Compile`; deleted when the effect has no parameters. |

#### class `MediaPlayerEffect`
Created by `MediaPlayer.Create` (Effect, or Image/Text with `Effect`), by `GetImage`, or directly by effect pages. Lifecycle: constructor → `Cache` (fetch + `Create`) → `Play` render loop → `OnDispose`.
| Member | Kind | Signature | Description |
|---|---|---|---|
| `DpiScale` | static field | number | Global adaptive DPI scale (0.125..1), persisted by `AdjustDpi`. |
| `GetImage` | static async method | `GetImage(cacheName, url, mp, ct)` → Promise<[dataUri, compileMs, drawMs] \| [null, errorMessage]> | Thumbnail generator. Key `"SysWeaver.EffectInfo." + cacheName + "." + hashString(url)`; if cached and a re-fetch of `url` returns the same `etag`, returns the cached result. Otherwise creates `new MediaPlayerEffect(url, mp, ct)` off-screen (`top:-10000px`), caches it, seeks to 5 s, plays until a GPU-timer sample exists (when available), stops, encodes the canvas as JPEG, disposes, converts to a data URI (`bufferToBase64`), stores `{R, T: etag}` in localStorage. |
| `GetElementFromId` | static method | `GetElementFromId(id)` → Element | Looks `id` up in this window and up to 9 parent windows (stops at `top`); falls back to `top.document.body` (also when `id` is empty or access throws). |
| `InitGL` | method | `InitGL()` | Gets the `webgl`/`webgl2` context (`antialias`, `alpha = Transparent`, no depth/stencil, no preserveDrawingBuffer; retries without antialias; throws if unavailable), enables `OES_standard_derivatives` and `EXT_shader_texture_lod`, creates the fullscreen-triangle buffer `Pos`, creates `Texture` via `CreateTextureFn(gl)`, and sets up `Query` from `EXT_disjoint_timer_query`. |
| `constructor` | constructor | `(url, params, createTextureFn)` | Merges params, creates `<canvas>` with expando `This`, sets CSS size, `InitBase`, `Width/Height`, `CompilationTime = -1`, `ResetCounters()`, `ApplyClip()`. Dark mode: fixed by `ForceMode`, else `IsDarkMode` from the top page with optional `SmoothDark` (`SmoothBool`, when anim.js is loaded, not `Static`, `DarkModeSwitch > 0`) and registers `DarkModeChanged`. Creates `CustomX/Y/Z` animators via `GetAnimFn`. |
| `t.DarkModeChanged` | field (function, set in constructor) | `(isDark)` | Updates `IsDarkMode`, re-renders immediately when `Static`, and feeds `SmoothDark`. |
| `SetCustomX` / `SetCustomY` / `SetCustomZ` | method | `SetCustomX(m)` with `m = {Value, Anim?, Start?}` | If `Anim` is given, replaces the channel animator with `GetAnimFn(Anim, Start ?? Value)` (Start used only when numeric); then calls `Set(Value)`. Targets of the `MediaCustomX/Y/Z` messages. |
| `GetAnimFn` | static method | `GetAnimFn(f, v = 0)` → `{Get, Set}` | Builds a channel animator: no `f` → plain value; function → `Get = f`, `Set` no-op; CSV string `"SmoothBool[,delayMs=200[,smoothness=Smoothness.Normal]]"` (value ≠ 0 as bool), `"SmoothValue[,delayMs=200]"`, `"Spring[,frequency=7,initialLength=100,decay=90,snap=0.01]"`, `"Bounce[,initialVelocity=5,gravity=10,restitution=0.8,restVelocity=0.1,normalize=true,startAtPeak=false]"` (Spring/Bounce: `Get` = curve at time since last `Set` × magnitude, `Set` = new magnitude and restart); unknown name → plain value. |
| `Get`, `Set` | inner (of `GetAnimFn`) | `Get()` → number, `Set(nv)` | The returned closure pair for each animator kind described above. |
| `ApplyClip` | method | `ApplyClip(p)` | Forwards to `Texture.ApplyClip(p)` (if a texture exists), sets `ApplyTextue = true` so the next `render` re-binds the texture uniforms, and triggers `GetRender()()`. Overrides the `InitBase` default (no CSS clip-path for effects). |
| `CustomX`, `CustomY`, `CustomZ` | field | `{Get, Set}` | Channel animators (class field initializers are `0`, replaced in the constructor). |
| `ReleaseGL` | method | `ReleaseGL()` | Unobserves the `ResizeObserver`, disposes texture, program and position buffer, clears `GL`. |
| `OnDispose` | async method | `OnDispose()` | Pauses, waits until the frame loop stops, unregisters `DarkModeChanged`, removes the element, `ReleaseGL()`, removes the mouse listener. |
| `ResetCounters` | method | `ResetCounters()` | `TotalTime = 10000000`, `MeasureCount = 0`, `AvgDrawTimeMs = 0`, `PrintCounter = 0`. |
| `LazyCompile` | method | `LazyCompile()` | Debounced recompile for live parameter edits: after 50 ms runs `Compile(() => CompileAgain)` in a loop until no new request arrived meanwhile (`Compiling`/`CompileAgain` flags). |
| `GetRender` | method | `GetRender()` → function | When not animating or `Static`: a function that clears `Rendered` and renders one frame if attached; otherwise a no-op. |
| `DebugLoseContext` | method | `DebugLoseContext()` | Forces context loss via `WEBGL_lose_context` (testing). |
| `Compile` | async method | `Compile(abortCheckFn)` → Promise<true \| false \| "Aborted"> | `CompilationTime = -1`; compiles `ProgramData` with `Params.EffectProps` and the default vertex shader `attribute vec2 position; ... gl_Position=vec4(position,0,1)`. Failure → `-3`/false; abort → `-2`/`"Aborted"`. On success uses the program, resolves `position` and builds the uniform setters below, binds the texture (`tex`, `texUv`, `texSize`, unit 0), sets the vertex attribute, disposes the old program, stores `Program` and `CompilationTime`, and re-renders when called with an abort function. |
| `UniformTime`, `UniformTimeDelta`, `UniformFrameIndex`, `UniformDate` | field (function, set in `Compile`) | `(time)`, `(dt)`, `(i)`, `(date)` | Set `time`/`iTime`, `iTimeDelta`, `iFrame`, `iDate` (year, month 1-12, day, seconds of day); no-ops when the uniform is absent. |
| `t.UniformResolution` | field (function) | `(w, h)` | Sets `resolution` (vec2) or, if present, `iResolution` (vec3, z = 0). |
| `t.UniformMouse` | field (function) | `(x, y)` | Sets `mouse` (vec2) and/or `iMouse` (vec4, zw = 0). |
| `t.UniformScroll` | field (function) | `(x, y, rx, ry)` | Sets `scroll` (vec4) when present. |
| `t.UniformCustomDark` | field (function) | `(x, y, z, isDarkMode)` | Sets `customDark` (vec4) when present. |
| `Create` | async method | `Create()` → Promise<false \| undefined> | `InitGL`, parses `Src` into `ProgramData`, logs defaults if `ShowVars`, caches the texture (resizing the canvas/params to the texture size and setting `AdaptiveSize = false` when it returns a size), validates `FxProps` into `EffectProps` and exposes `ScriptMember`/`ScriptTypeInfo`, compiles (false on failure), disables depth/blend, sets the clear color, `Cached = true`, renders once if attached, installs a `ResizeObserver` (updates `ClientWidth/Height` and re-renders), resolves `ScrollElement`, and with `MouseId`/`MouseEnable` installs `OnMouseMove` and `RemoveMouseListener`. |
| `OnMouseMove` | inner (of `Create`) | `(ev)` | Stores `ev.clientX/Y` in `MouseX/MouseY`. |
| `t.RemoveMouseListener` | field (function, set in `Create`) | `()` | Removes the `mousemove` listener. |
| `Cache` | async method | `Cache(keepHidden)` → Promise<bool> | Fetches the `.glsl` (`mode: "cors"`); non-200 → warn + false. Stores `Etag` and `Src`, runs `Create()`, then `OnCacheComplete`. |
| `AdjustDpi` | static method | `AdjustDpi(timeElapsed, max, min)` | Halves `DpiScale` (min 0.125) when `timeElapsed > max`, doubles it (max 1) when `< min`; logs and persists to localStorage on change. |
| `AccTime` | method | `AccTime(timeElapsed, max, min)` | Keeps the minimum frame time; every 32 measurements (every call when `Static`) publishes it in `AvgDrawTimeMs`, calls `AdjustDpi` with `DpiAdjust` (from the 2nd publication on), logs every 8th publication when `ShaderDebug`, and resets the window. |
| `ScrollElement` | field | `= null` | Element read for the `scroll` uniform. |
| `MouseX`, `MouseY` | field | `= 0.5` each | Last mouse client position. |
| `OnRender` | field | `= null` | Optional hook `(gl, player)` called before each draw (used by `mediaView/textEffect.js` to upload extra uniforms). |
| `ZeroDate` | static field | `new Date("2001-01-01T11:37:42Z")` | `iDate` value when `Speed ≤ 0`. |
| `render` | static method | `render(t, animTime)` | Draws one frame of player `t` (no-op without `Program`). With `animTime` (from rAF) computes time from `RenderTime`/`ResumeTime` and uses the GPU timer. Computes the backing-store size (adaptive client size × dpr × `DpiScale` × global scale, or `Width/Height × DpiScale`; `LimitSize` clamp), resizes the canvas, skips repeated `Static` frames and invisible canvases (`checkVisibility`), sets viewport and all uniforms (`scroll` ratios are computed as `scrollLeft / Math.max(1.0, scrollWidth)`), re-applies the texture when `ApplyTextue` (plus `WrapU`/`WrapV`-or-`WrapY` repeat), calls `OnRender`, draws, and feeds `AccTime` (GPU query thresholds 12/2 ms, else CPU frame delta 35/17 ms). |
| `draw` | inner (of `render`) | `()` | `gl.drawArrays(TRIANGLES, 0, 3)`. |
| `Validate` | async method | `Validate()` | If there is no context or it is lost: `ReleaseGL`, replaces the canvas with `cloneNode()` in the DOM, and re-runs `Create()`. |
| `Play` | async method | `Play()` | `Validate`, unpause, `ResumeTime = CurrentPosition`, reset `RenderTime` and counters; unless an already-rendered `Static` effect, sets `IsAnimating` and starts `renderFrame`. |
| `renderFrame` | inner (of `Play`) | `(animTime)` | Renders and re-schedules itself with `requestAnimationFrame` until paused (or the static frame is done), then clears `IsAnimating`. |
| `Pause` | async method | `Pause()` | Sets `Paused` (the loop stops on its next frame). |
| `Stop` | async method | `Stop()` | Pauses, waits for the loop to end, `CurrentPosition = 0`, `Validate`, renders one frame. |
| `GetPos` | method | `GetPos()` → seconds | `CurrentPosition` (effect time before `Speed`/`Offset`). |
| `Seek` | async method | `Seek(time)` | Negative → `null`. Paused: sets the position, `Validate`, renders one frame. Playing: sets `ResumeTime = time - CurrentPosition` and `CurrentPosition = time`. |
| `Params`, `Url`, `Source`, `Src`, `Etag`, `CreateTextureFn`, `Texture`, `GL`, `Pos`, `Query`, `ProgramData`, `Program` | fields | | Params; `.glsl` URL; fetched source; ETag; texture factory; texture provider; context; vertex buffer; `{E: ext, Q: query, IsQ}` timer query; parsed source; current `EffectProgram`. |
| `CompilationTime`, `AvgDrawTimeMs`, `TotalTime`, `MeasureCount`, `PrintCounter` | fields | | Compile ms (−1 compiling, −2 aborted, −3 failed); performance counters. |
| `CurrentPosition`, `ResumeTime`, `RenderTime`, `PrevTime`, `FrameIndex`, `Rendered`, `IsAnimating`, `Compiling`, `CompileAgain`, `ApplyTextue` | fields | | Timeline and render-loop state (`FrameIndex` starts at 0 in the constructor and is incremented in `render`). |
| `IsDarkMode`, `SmoothDark`, `ClientWidth`, `ClientHeight`, `ResizeObserver`, `ScriptTypeInfo`, `ScriptMember` | fields | | Dark state and smoother, observed element size, editor schema from `ProgramData`. |

#### class `MediaPlayerImageTexture`
Texture provider created by the `createTextureFn` passed from `MediaPlayer.Create` (Image + `Effect`). Duck-typed interface shared with `MediaPlayerTextTexture`: `Cache(haveLod)`, `Apply(uv, size)`, `ApplyClip(p)`, `Dispose()`, `Width/Height`.
| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `(gl, params, url)` | Stores `Params` (the image params), `Url`, `GL`, new `Texture`. |
| `Cache` | async method | `Cache(haveLod)` → Promise<[w,h] \| null \| false> | Loads an `Image` (`MediaPlayerTools.waitEvent2`; false on error), stores `Image`, `Width/Height`, uploads flipped-Y with clamp/linear, mipmaps as in `MediaPlayerTextTexture`, returns the size when `EffectSizeFromImage`. |
| `powerOf2` | inner (of `Cache`) | `(v)` → bool | Power-of-two test. |
| `Apply` | method | `Apply(uvScaleAndOffset, texSize)` | Same as `MediaPlayerTextTexture.Apply`. |
| `ApplyClip` | method | `ApplyClip(p)` | Same as `MediaPlayerTextTexture.ApplyClip`. |
| `Dispose` | method | `Dispose()` | Deletes the texture. |
