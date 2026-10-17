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
- `MediaTypes`: static integer constants `Image=0, Video=1, Audio=2, YouTube=3, Effect=4, Text=5`, a frozen `Names` array, and `GetTypeName(mediaType)`, which returns `"SysWeaver.MicroService.Media.Media" + Names[...]` (it refers to `Names` without the class qualifier).
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
  - JS params declare `WrapY` while the render reads `WrapV`, and C# uses `WrapV`.
- Effect parameter editing rewrites the GLSL source and recompiles (no uniforms). The editable schema is hand-built in the server's type-info JSON format (`Flags` bit values, `System.Single` type names), which ties this code to the generic editor in another project.
- Cross-window reach: dark-mode detection and `GetElementFromId` read `window.top` / parent frames directly, which assumes same-origin framing. Control is through untyped `postMessage(..., "*")` with string `Type` keys, and there is no origin check.
- Expando properties are used as data channels: `canvas.This`, `gl.PCompile`, number-atlas widths on the canvas, `style.ComputedMargins`, grid stats written into `EffectParams.FxProps`.
- The "with effect" composition inverts the player: an Image/Text with `Effect` becomes a `MediaPlayerEffect` that owns a texture provider. So `player.Params` is the effect params, not the image/text params.
