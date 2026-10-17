# common

[⬆ srcWeb](../README.md)

> The shared client-side library of all SysWeaver web pages, served under `common/`: core scripts, themes and background patterns, the tab control, animation helpers and a few vendored libraries.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Almost every page in the framework loads `common/common.js` and `common/theme.css`. `theme.css` is served as a template (`${Variable}` substitution, e.g. for the configured colours) and defines the themes selected through the `data-theme` attribute (e.g. `dark`, `hacker`, `application`), several of which use the background images in this folder. The remaining scripts are loaded on demand by the pages that need them.

## Contents

| Item | Description |
|---|---|
| [`common.js`](common.js) | Core library (about 7800 lines): requests to the web API (`sendRequest`, `getRequest`), dynamic script and style loading (`includeJs`, `includeCss`), translation helpers, `SessionManager`, cross-window messaging (`InterOp`), buttons, value formatting, colour icons, full screen, progress bars, image loading and caching |
| [`theme.css`](theme.css) | Theme variables and theme variants (template) |
| [`common.css`](common.css) | Theme variable defaults and base styles; no reference to this file by name was found in the repositories |
| [`tab.js`](tab.js), [`tab.css`](tab.css) | Tab bar control (`Tab`, a `SysWeaver-TabBar` element, optionally remembering the selected tab in local storage) |
| [`anim.js`](anim.js) | Animation and interpolation helpers (`MathTools`, cubic Hermite interpolation, `SmoothValue`, spring, projectile and bounce motions), used by the media viewer pages |
| [`marked.js`](marked.js) | Vendored *marked* v15.0.3 Markdown parser (MIT, minified) |
| [`UZIP.js`](UZIP.js) | Vendored UZIP JavaScript zip / deflate library, used with `md5.js` by upload pages |
| [`md5.js`](md5.js) | Vendored YaMD5 MD5 hasher |
| [`racing-stripes.svg`](racing-stripes.svg), [`moroccan.svg`](moroccan.svg), [`glamorous.svg`](glamorous.svg), [`graph-paper.svg`](graph-paper.svg), [`Hacker.png`](Hacker.png) | Background patterns used by the themes in `theme.css` (`Hacker.png` is a 32×32 tile for the hacker theme) |
| [`hexagons.svg`](hexagons.svg), [`floating-cogs.svg`](floating-cogs.svg), [`plus.svg`](plus.svg) | Further patterns / images; no reference to them by name was found in the repositories |

## Relationships

- **Part of:** [SysWeaver.HttpServer](../../README.md)
- **Used by:** [`app/`](../app/README.md), the explorer pages of [SysWeaver.HttpServer.ExploreModule](../../../SysWeaver.HttpServer.ExploreModule/srcWeb/explore/README.md) and the web pages of most other projects, e.g. [SysWeaver.Chat](../../../SysWeaver.Chat/README.md) (`UZIP.js`, `md5.js`), [SysWeaver.MicroService.Media](../../../SysWeaver.MicroService.Media/README.md) (`anim.js`), [SysWeaver.MicroService.FileUploader](../../../SysWeaver.MicroService.FileUploader/README.md) and [SysWeaver.MicroService.ServerManager](../../../SysWeaver.MicroService.ServerManager/README.md)

## Behavior (web analysis)

> Behavioural description of the first-party code in this folder (`common.js`, `theme.css`, `common.css`, `tab.js`, `tab.css`, `anim.js`) as a basis for refactoring. Vendored files: [`marked.js`](marked.js) is *marked* v15.0.3 (UMD, sets global `marked`); [`UZIP.js`](UZIP.js) is UZIP (global `UZIP`); [`md5.js`](md5.js) is YaMD5 (global `YaMD5`). Line numbers refer to [`common.js`](common.js) at the time of writing.

### Loading and initialization

Pages load the library as a classic, deferred script plus the theme stylesheet, typically:

```html
<script defer src="../common/common.js"></script>
<script defer src="page.js"></script>
<link rel="stylesheet" href="../common/theme.css">
<body onload="pageMain()">
```

53 pages reference `common/common.js` and 54 reference `common/theme.css`; a couple use `${@Base Url}common/common.js` through templating. Page scripts are deferred *after* `common.js`, so they can use its globals when they are evaluated. Most pages start their own code from `<body onload=...>`.

What runs while `common.js` is evaluated, in source order:

1. **Class static initialisers** run when each class declaration is evaluated:
   - `SessionManager.Current = document.currentScript.src` and `SessionManager.Url = <Current>/../../Api/application/GetMessages`. The URL is resolved relative to the script URL, so it points to `/Api/application/GetMessages` at the server root or prefix.
   - `InterOp.Id` = `Date.now() + Math.random()`, also stored in `window.SysWeaverId`. `InterOp.B` = an `InterOpBroadcast("SysWeaver")` (a `BroadcastChannel`). If that throws, it is an `InterOpWindows` fallback that *replaces `window.open`* to track opened same-origin windows.
   - `ValueFormat.Current = document.currentScript?.src ?? "."` is the base for `includeJs`/`includeCss` when `current` is null. A few detached template DOM nodes are also created (`ValueFormat.TextNode`, `LinkNode`, `ImgNode`, `TagSpanNode`, `TempToggle`, `TempAction`, ...).
   - `Button.DefaultStyle = new ButtonStyle()`, `Animator.Functions = new Map()`, `Cache.InternalCache = new Map()`, `ColorIcon.GenCss = new Map()`.
   - These initialisers rely on `document.currentScript` being set. That requires `common.js` to be loaded as a classic `<script>`: not as a module, and not injected via `includeJs`.
2. **The last two lines** (7781-7782) run `SessionManager.Create()` and then `SysWeaverInit()`. `SessionManager.Create()` starts buffering all InterOp messages into `SessionManager.StartUp`. `SysWeaverInit()` is async and not awaited.

`SysWeaverInit()` (guarded by `window.HaveSysWeaverInit`):

| Step | Behaviour |
|---|---|
| URL params | `getUrlParams()` lower-cases the keys and caches the result in `window.sysweaverParams`. `css=<url>` → `includeCss(null, css)` (not awaited). `settheme=<name>` → stored in `localStorage["SysWeaver.Theme"]`. `usetheme=<name>` → `window.UseTheme`. |
| Theme | `applyTheme()` (not awaited, see [Theming and styling](#theming-and-styling)). |
| Smooth src loading | Runs `fixElementWithSrc` on every `img`/`iframe` that has `data-sw-fade`, `data-sw-fadein` or `data-sw-useevent`. It also installs a document-wide `MutationObserver` (subtree, childList) that applies `fixElementWithSrc` to added nodes and `cleanElementWithSrc` to removed nodes. |
| Polyfill | `String.prototype.replaceAll` if missing. |
| Mobile | `window.Mobile` comes from the first available of: URL `mobile`, `localStorage["SysWeaver.Mobile"]` (JSON), the parent window's `Mobile` (same-origin iframe), `isMobile()`. |
| Color scheme | URL `colorscheme=<v>` adds `<meta name="color-scheme" content=v>`. |
| `ignoreuserchanges=true` | `SysWeaverIgnoreUserChanges()` → `window.IgnoreUserChanged = true`, which suppresses the automatic reloads listed under [Server interaction](#server-interaction). |
| Layout | `onSizeChange()` runs once and then on every `ResizeObserver` callback of `<html>`. It sets `window.Portrait/Narrow`, taken from the parent window when this is a same-origin child whose parent has `Mobile`. It sets `window.SelfPortrait/SelfNarrow` from the window's own size (narrow = width ≤ 600). It toggles classes `SwPortrait/SwLandscape`, `SwNarrow/SwWide`, `SwSelfPortrait/SwSelfLandscape` and `SwSelfNarrow/SwSelfWide` on both `<body>` and `<html>`. On changes it dispatches `CustomEvent`s on `<body>` (`detail` = the boolean): `SwOrientation` + `SwPortrait`/`SwLandscape`, `SwLayout` + `SwNarrow`/`SwWide`, `SwSelfOrientation` + `SwSelfPortrait`/`SwSelfLandscape`, `SwSelfLayout` + `SwSelfNarrow`/`SwSelfWide`. |
| `window.load` | Adds classes to both `<html>` and `<body>`. `SwMobile` or `SwDesktop` always. `Transparent` when `transparent=true`, or when the page is a child frame and `transparent` is not `false`. `FillScreen` when `fillscreen` is present. |
| Reload detection | If the navigation type is `reload`, `ClearHistoryState()`. |
| Interaction | The first `mousedown`/`keydown`/`touchstart` on `document` sets `window.DidInteract = true`. It then calls the optional `window.OnInteraction(ev)` and dispatches a plain `Event("UserInteracted")` on `document.body`. |
| Scroll save | Registers `saveScroll` on `document` for an event named `"beforeunload "` (with a trailing space). `saveScroll` stores `ScrollX/ScrollY` in history state. |
| Top detection | `window.IsTop` is true for the top window, or for a frame whose top is cross-origin. |
| Message map | Builds a `type → handler` map and attaches it with `InterOp.AddListener` (see [Server interaction](#server-interaction) and [State and persistence](#state-and-persistence)). |
| Session | `SessionManager.Init()` (not awaited): leader election and the server long-poll. |

**Dynamic includes**: `includeJs(current, file, noThrow)` and `includeCss(current, file, noThrow)` work the same way:

- `file` is resolved with `new URL(current + "/../" + file)`, i.e. against the folder of `current`. `null` means the folder of `common.js`.
- Loads are de-duplicated through `window.IncMap`, keyed by absolute URL. Concurrent callers poll every 10 ms until the first load finishes.
- The file is loaded by appending a `<script async>` or `<link rel=stylesheet>` to `<head>`.
- The result resolves to `{ element, error, errorEv, loaded }`. On error the promise rejects unless `noThrow` is set.

Inside this folder they are used for `marked.js` (loaded lazily by `ValueFormat.MarkDownToHTML`) and for a theme's `@css` suffix.

### Files

#### [`common.js`](common.js)

A single classic script (~7800 lines) of global functions, global `const`s and global classes. Grouped by area:

##### Translation markers

| Function | Behaviour |
|---|---|
| `_T(format, ...args)` | Marks a string literal for translation and formats it with `ValueFormat.stringFormatArgs`. Placeholders: `{0}`; `{_0}` lower-cases the argument, `{^0}` upper-cases it, `{$0}` lower-cases it unless it is at position 0. A function argument is called as `fn(number, fmt, index)`. A trailing literal that the format does not use is the translation context. |
| `_TH(format, ...args)` | Same as `_T` at runtime; intended for HTML-safe output after server translation. |
| `_TF(text, context)` / `_TFH(text, context)` | Identity at runtime; marks literals with no parameter substitution. |

The literals are rewritten on the server when the `.js` file is served, by `JsTranslation` in [SysWeaver.FileTranslation](../../../SysWeaver.FileTranslation/README.md). Calls must therefore use string literals.

##### Theme

| Function | Behaviour |
|---|---|
| `getTheme()` | Returns the first available of: `window.UseTheme`, `localStorage["SysWeaver.Theme"]`, `prefers-color-scheme` (`light`/`dark`), `"dark"`. |
| `async applyTheme()` | Applies the current theme to `<html>`. It skips the work if `documentElement.Theme` already equals the theme. Details under [Theming and styling](#theming-and-styling). |
| `async setTheme(name)` | Clears `window.UseTheme`, stores `SysWeaver.Theme`, applies the theme and posts `Theme.Changed`. Returns whether the theme changed. Called by the server-generated *Theme* menu (`[WebMenuJs(... "if(setTheme('dark'));false")]` in `HttpServerBase.cs`). |
| `async resetTheme()` | Removes `SysWeaver.Theme`, applies the theme and posts `Theme.Changed` (the menu's "(Automatic)" entry). |
| `async toggleTheme()` | Switches between `light` and `dark`. |
| `appendTheme(url)` | Appends `useTheme=<window.UseTheme>` to `url` when a forced theme is active. Used by `Open` and the Markdown link renderer. |
| `hackerThemeAnimate(time)` | Per-frame hook for the `hacker` theme; sets `--ThemePos` on `<html>`. |

##### Server time

`GetServerTime()` returns a `Date` and `GetServerTick()` returns milliseconds. Both compute `Date.now() + window.SysWeaverServerTimeOffset`; the offset is maintained by `SessionManager`.

##### `SessionManager` (static class): cross-tab leader election and server push messages

| Member | Behaviour |
|---|---|
| `Create()` | Resets `StartUp = []` and registers `InternalOnMsg`. That handler buffers every InterOp message until a master starts; the master then replays them into its own handler. |
| `async Init()` | Runs once per window (guarded by `window.HaveSessionManager`). **Same-origin child frames** only: on `beforeunload`/`pagehide` they post `SessionManager.RemoveEvents` for their owned events, listen for `SessionManager.TimeOffset`, and post `SessionManager.GetTimeOffset`. **Top windows** run the election and, when elected, the long-poll loop (see [Server interaction](#server-interaction)). |
| `AddServerEvent(name)` / `RemoveServerEvent(name)` | Lower-case the name and ref-count it in `ServerEvents` and `OwnedEvents`. On the first add they post `SessionManager.AddEvents` `{Events:[name]}`; on the last remove they post `SessionManager.RemoveEvents`. Return true in those two cases. |
| `AddMessageHandler(type, fn, ignoreServer)` | Registers `async fn(msg)` for an InterOp message type (lower-cased) in `window.SessionManagerFns`. One shared InterOp listener dispatches by lower-cased `Type`. Unless `ignoreServer` is set, it also calls `AddServerEvent(type)`. Handlers must be removed explicitly. |
| `RemoveMessageHandler(type, fn, ignoreServer)` | Inverse of `AddMessageHandler`. |
| `AddRef/DecRef(map, name, isOwned)`, `GetMap()`, `InternalOnMsg` | Internals. |
| `ServerEvents` (Map name→count), `OwnedEvents`, `IsMaster`, `Url`, `Current`, `StartUp` | State. |

##### `InterOp`: cross-window messaging

| Member | Behaviour |
|---|---|
| `InterOp.Id` | Random per-window id. |
| `InterOp.Post(type, data, isFromServer)` | Sets `Type`, `From` (`InterOp.Id`, or `"Server"`) and `MagicXyz: "SysWeaver"` on `data` (creating it if needed), then posts it. With `BroadcastChannel` the message reaches every same-origin window and frame. It also reaches the local listeners of the posting window, through an explicit loop. |
| `InterOp.AddListener(fn)` / `RemoveListener(fn)` | The listener receives a `MessageEvent`-like `{data}`. |
| `InterOp.GetMessage(ev)` | Returns `ev.data` if it carries `MagicXyz === "SysWeaver"` and a string `Type`, else `{Type:null, From:null}`. |
| `InterOpBroadcast(name)` | Wrapper around `BroadcastChannel`; keeps its own listener list so it can deliver to itself. |
| `InterOpWindows()` | Fallback bus. Collects the `window.parent` chain and the windows opened through the patched `window.open`, posts to each with `postMessage`, and listens on the `window` `message` event. |

##### Raw `postMessage` helpers and navigation

| Function | Behaviour |
|---|---|
| `PostAll(name, data, origin)` | `postMessage({Type:name,...})` to `window.opener ?? window.top`. |
| `PostTop(name, data, origin)` | Posts to `window.top`. |
| `PostParent(name, data, origin)` | Posts to `window.parent`, or to `window.opener` when the page is not framed. |
| `PageLoaded()` | `PostParent("PageLoaded")` + `PostTop("LoaderRemoved")`. This is the contract with the `app/` shell and with the `data-sw-useevent` iframes of `fixElementWithSrc`. |
| `Open(url, target)` | For `_self` with a same-origin or relative URL: posts `IframeNavigating {Url: absolute}` to top, then after 100 ms calls `window.open(appendTheme(url), "_self")`. Otherwise calls `window.open(appendTheme(url), target ?? "_blank")`. |
| `async EditOrOpenUrl(read, target, save, remove)` / `EditUrl(...)` / `ViewUrl(read, target)` | Ask the server for an editor or viewer URL: `/Api/GetExtensionEditor` with absolute `{Read, Save, Delete}` URLs, or `/Api/GetExtensionViewer` with the read URL. If one is returned, it is opened (`_blank` if absolute, else `"/" + url` in `_self`); otherwise `Open(read, target)`. |
| `onLink(ev, url, target, useEdit, useView)` | Click handler used by generated links. A modifier-click copies the URL; otherwise it calls `ViewUrl`, `EditUrl` or `Open`. |
| `fixLinks()` | Replaces every `<a>` in the document with a `SysWeaver-Link` created by `ValueFormat.createLink`; image links keep their `<img>`. |
| `ReloadAll(flushCache, clearHash)` | Optionally clears the hash with `history.replaceState(null,null,' ')`, then calls `location.reload(...)`. |
| `IsSameOrigin(url)`, `IsAbsolutePath(url)` (starts with `http://`/`https://`), `GetAbsolutePath(url, current)` | URL helpers. |
| `downloadText(name, text)`, `downloadFile(name, url)` | Trigger downloads via a temporary `<a download>`. |

##### Requests and caching

`sendRequest`, `getRequest`, `postData`, `Cache`, `FlushCache`, `FlushCacheSync` and `SetImageSource` are described under [Server interaction](#server-interaction) and [Smooth src loading](#smooth-src-loading-images--iframes).

##### Environment / platform

| Function | Behaviour |
|---|---|
| `getUrlParams()` | Cached `URLSearchParams` with lower-cased keys. |
| `getNumberParam(params, name)`, `getIntParam(params, name)` | Parse a param, or return `null`. |
| `isMobile()` | `navigator.userAgentData.mobile`, or the classic UA regex. |
| `getPlatform()` | Lower-cased platform string from `userAgentData.platform`, `navigator.platform` or UA heuristics. |
| `getMobileParams()` | `[isMobile, useSingleColumn, isPortrait, scale]`. |
| `async getDevicePosition(lowPrecision, timeout, maximumAge)` | Promise wrapper of `navigator.geolocation.getCurrentPosition`; throws on error. |
| `async EnterNoSleep()` / `async ExitNoSleep()` / `IsSleepPrevented()` | Ref-counted wake lock that plays a looping, muted inline video (state in `document.NoSleep`). |
| `FullScreen.IsFull()`, `async Enter()`, `async Exit()`, `async Toggle()` | Fullscreen API with vendor fallbacks. `IsFull` also treats `display-mode: standalone` and `display-mode: fullscreen` as full screen. |
| `async CaptureVideoFrame(video, mime, quality)` | Draws the current frame of a video's `srcObject` to a canvas and returns the encoded image as base64. |

##### Encoding, hashing, binary

| Function | Behaviour |
|---|---|
| `CryptRandomInt32()`, `CryptRandomGuid()` | Use `crypto`, with a `Math.random` fallback. |
| `legacySha256(buffer)` | Pure-JS SHA-256, used when `crypto.subtle` is unavailable. |
| `hashData(u8)` / `hashString(s)` | SHA-256; `hashString` returns base64. |
| `hashDataSha1` / `hashStringSha1` | SHA-1 equivalents. |
| `Base64EncodeString` / `Base64DecodeString` | Base64 of UTF-8 text. |
| `MakeUriSafeBase64`, `Uint8ArrayToBase64(bytes, uriSafe)`, `async bufferToBase64(buffer, keepUri)` (via `FileReader`), `base64ToArray`, `bufferToHex` | Conversions. |
| `concatArrays`, `ConcatUint8Arrays`, `async ReadAllData(stream)` | Byte array helpers. |
| `async DeflateCompress(buffer)` | `CompressionStream("deflate-raw")`. |
| `ToTypedJson(data, type)` | JSON with `$type` as the first property when a type is given or already present. |
| `RemoveNulls(o)` | Deletes null properties recursively. |

`MakeJsObject` and `MakeCsObject` are commented out.

##### DOM, focus and input helpers

| Function | Behaviour |
|---|---|
| `isHidden(el)`, `traverseChildren(el, okFn, traverseHidden)`, `traverseForward(...)`, `traverseBackward(...)` | DOM-order walks that skip `display:none` elements unless asked. |
| `isTabable(el)`, `focusNextTab(el)`, `focusPrevTab(el)`, `tabToNextOnEnter(input, onNext)` | Focus navigation; `tabToNextOnEnter` sets `input.onkeyup`. |
| `keyboardClick(el)` / `removeKeyboardClick(el)` | Sets `tabIndex=0`, adds class `KeyClick`, and maps Space/Enter in `onkeyup` to `el.click()`. |
| `rightClick(el)` / `removeRightClick(el)` | Maps `oncontextmenu` to `el.click()`. |
| `isPureClick(ev)`; `badClick(ev, keepProp)` | `badClick` calls `preventDefault`/`stopPropagation` (unless `keepProp`) and returns true when a modifier key is held. It is the common "ignore modified clicks" guard used by almost every click handler. |
| `makeHtmlAttributeSafe(s, preserveCR)`, `makeHtmlSafe(text)` | Escaping; `makeHtmlSafe` goes through a temporary element's `innerText`. |
| `isValidEmail(s)`, `isValidPhone(s)`, `isLetter(c)` | Simple validators; `isLetter` uses `eval` for non-ASCII characters. |
| `getNumberAttribute`, `getIntegerAttribute` | Attribute parsing with a default. |
| `insertAtCursor(field, value, updateSelection)` | Replaces the selection of an input or textarea. |
| `GetFunction(name, context)` | Resolves dotted names from `window`. |
| `IsAttached(el)` / `isInDocument(el)` | Walk the parents up to `document.body`. |
| `GetAbsoluteRect(el)` | Document-relative rect, measured against a hidden absolutely positioned `div`. |
| `TrackElement(mouseEventOrElement, target, margin)` | Keeps a fixed-position element next to an element or click point. It follows resizes (`ResizeObserver`) and scrolling, can clamp to the viewport, and stops when the target is detached. |
| `IsAtMaxScrollV(el)`, `StickToBottom(el, stick, onBottom, onFree, onTop, instant)` | Auto-scroll to the bottom, with a `Stick` class, a `ResizeObserver` and a `MutationObserver`. |
| `BlockTab(el)` / `UnblockTab(el)` | Focus-trap stack in `window.TabBlockers`, enforced by a capture `focus` listener on `<body>`. Unblocking restores the previous focus. `SYSWEAVER-TABBLOCK` elements act as sentinels. |
| `SetTemporaryInnerText(el, text, duration, newText)` | Shows a text for a while, then restores the original. |
| `GetRgbColor(keyword)` | Computed `rgb()` value of any CSS colour. |
| `RankScoreMatch(searchFor, ...texts)` | Search ranking; earlier texts and earlier positions score higher. |
| `getFileExtension`, `isImageExt/isAudioExt/isVideoExt/isProgrammingExt/isCompressibleExt`, `isImageFile`, `isVideoFile` | Backed by the frozen maps `imageExtensions`, `videoExtensions`, `audioExtensions`, `programmingExtensions`, `compressExtensions`. |

##### Async utilities

| Function | Behaviour |
|---|---|
| `waitEvent(el, name, fn, timeout)` / `waitEvent2(el, n1, n2, fn, timeout)` | Promise of the first matching event, or `null` on timeout. |
| `waitFor(async end => ...)` | Waits until the callback calls `end`. |
| `setMediaSrcAndWait(media, url, onResult)` | Sets a media source and waits for `canplaythrough` or `error`. |
| `AbortHandler` | `raise`, `addListener`, `removeListener` on a private element. |
| `delay(ms)`, `delayOneFrame()` | Timer and animation-frame promises. |
| `delayWithAbort(ms, abortHandler)` | Resolves `true` if aborted. |

##### Smooth `src` loading (images / iframes)

| Function | Behaviour |
|---|---|
| `fixElementWithSrc(el, showError, fadeIn, fadeDelay, fadeChannel, fadeParent)` | Runs once per element (marker `el.FadeIn`). While the `src` loads, the element (or its `fadeParent`-th ancestor) is hidden with `visibility:hidden; opacity:0`. It then fades in over `fadeIn` ms (default from `data-sw-fadein`, else 150) after a delay of `data-sw-fadedelay` ms, using a `requestAnimationFrame` smootherstep. A `MutationObserver` on `src` restarts the cycle. Iframes with `data-sw-useevent` wait for a window `message` with `Type === "PageLoaded"` instead of `load`. `data-sw-fadechannel="name,delayMs"` staggers elements of the same channel (`window.FadeChannels`). Images get `draggable=false`. |
| `cleanElementWithSrc(el)` | Removes the listener and observer state. |
| `createElementWithSrc(tag, showError, fadeIn)`, `createImg(showError, fadeIn)`, `createIFrame(fadeIn)` | Create an element and call `fixElementWithSrc` on it. |
| `SetImageSource(img, src, fallback, flushCache, onLoaded, reloadOnMessage)` | Sets `img.src` while hiding it via opacity, with a one-shot fallback URL and an optional cache flush first. With `reloadOnMessage` it registers an InterOp listener and calls `SessionManager.AddServerEvent(reloadOnMessage)`. When that message arrives it flushes the cache and reloads the image; it unregisters once the image is detached. |
| `PendingImages` | `Start(img, src)` counts loads and failures; `async WaitAll()` polls every 10 ms. |

##### UI building blocks

| Class / function | DOM built | Behaviour |
|---|---|---|
| `ColorIcon(imageClass, colorClass, w, h, title, onClick, buttonClass, disabledClass, captureContextMenu)` | `<icon-WxH>` | Monochrome icon drawn with `background-color` through a CSS mask. The first use of a size inserts rules into a `<style id="ColorIcon">`: `icon-WxH` (size `calc(var(--ThemeIconSize)*Wpx)`), the `.IconButton` hover/active lift, `.IconDisabled`, and the colour classes `IconColorThemeBackground/Main/Acc1/Acc2`. `imageClass` is either a CSS class that defines `mask-image`, or a URL (contains `.`). A plain URL becomes `mask-image`; a URL with a trailing `*` becomes a full-colour `background-image`. Clickable icons get `keyboardClick` and ignore clicks that are modified, disabled or arrive while working. Methods: `SetTitle`, `SetEnabled`, `ChangeImage`, `ChangeColor`, `AddColor(name, css)`, static `AddIconColor`, `StartWorking(icon="IconWorking", afterMs=250)` / `StopWorking()`. |
| `ButtonStyle` | – | Defaults: element `Sysweaver-Button`, `MinPressed` 20 ms, 24×24 icon, `IconColorThemeBackground`. |
| `Button(style, text, title, imageClass, enabled, onclick)` | `<Sysweaver-Button><Sysweaver-ButtonText/>[icon]</…>` | Adds class `Pressed` while the async `onclick(button)` runs; clicks are disabled meanwhile, and the callback may return an exit function. Also handles the `Disabled` class and Enter-key activation. Methods: `Enable/Disable/SetEnabled`, `ChangeText`, `ChangeTitle`, `ChangeImage`, `ChangeImageColor`, `StartWorking/StopWorking`. `Button.CreateRow()` → `<SysWeaver-CenterBlock>`. |
| `Message(text, duration, className, noCopyOnClick)`, `Fail(...)`, `Info(...)` | `<SysWeaver-Fail class="MsgError\|MsgInfo">` on `<body>` | A single toast that is reused (its text is replaced). Shown for 5 s by default; `true` means 24 h. A click removes it and copies the text unless `noCopyOnClick`. |
| `AddLoading(page, text, opaque, useIcon)` | `<SysWeaver-Loading>[<SysWeaver-LoadingText>][icon-64x64]` | Overlay that blocks mouse, keys and tab focus (`BlockTab`). Returns `remove(noEvents)`. Unless `noEvents` is set, removal calls `PageLoaded()`, posts InterOp `WindowLoaded`, and restores the saved scroll position (`ScrollX/ScrollY` history state). The scroll restore is retried each frame until the layout settles or the user interacts. With URL param `noloader`, `AddLoading` returns `PageLoaded` directly. |
| `ProgressBar` | `<SysWeaver-ProgressBar>` with `TextInside`, `TextOutside`, `Fill` | `SetValue(current, max, text, title)`, `SetPercentage(value, text, title)`; toggles `Active` on the inside or outside text at 50 %. |
| `Animator` (static) | – | Single `requestAnimationFrame` loop. `Add(fn)`: the function receives the ms elapsed since it was added, and returning true removes it. `Remove(fn)`. |
| `FullScreen`, `PendingImages`, `AbortHandler`, `Cache` | – | Described in the sections above and below. |

##### `ValueFormat` (static class): value formatting and table-cell rendering

Utility members (widely used directly):

| Member | Behaviour |
|---|---|
| `stringFormat(fmt, ...args)`, `stringFormatArgs(fmt, args)`, `FormatMaps` | `{n}` substitution with the `_ ^ $` case modifiers. |
| `toString(value, decimals, thousandSep=' ', decimalSep='.')` | Fixed decimals and thousands grouping; negative `decimals` shows decimals only when needed. |
| `toAmount(currencyInfo, amount, options, decSep, thouSep)` | Currency formatting with `CurrencyFormatOptions` (`IsoPrefix`, `IsoSuffix`, `Symbol`, `ForceRounding`, `ApplyThousandsSeparator`, `AutomaticRounding`, `Default`). `currencyInfo` comes from `iso_data/currencies.js` (`IsoCurrency.Get`). |
| `formatSuffix(v, suffix, emptySuffix, k)`, `formatByteSize(v)`, `formatTimeSpanMs(ms, blankIfNeg)`, `formatTimeSpan(".NET TimeSpan string", zeroAsThis)` | Human-readable sizes and durations, with translated units. |
| `countSuffix(count, singular, plural)`, `removeCamelCase(str, space)`, `keyValueSplit(text, split)`, `joinNonEmpty(delim, ...args)`, `AddNonNullLine(to, header, value)` | Text helpers. |
| `getTimeStampTitle(date, onlyDate, prefix, tab, fmt)` | Returns `[tooltip, "yyyy-mm-dd hh:mm:ss"]`; `fmt` is `"utc"` or `"local"`. Related: `convertLocalDateToUTCDate`, `convertUTCDateToLocalDate`. |
| `setElementText(el, text)` | Preserves leading and trailing spaces with `&nbsp;`. |
| `updateText(el, text, title, flashClass)`, `updateHtml(el, html, title, flashClass)` | Update only on change, optionally adding a flash class for about 1 s. Return whether the content changed. |
| `readFromClipboard()`, `copyToClipboard(v)`, `copyToClipboardInfo(v, generic)`, `copyOnClick(el, value, addKeyboard, addTitle)` | Clipboard helpers. `copyToClipboardInfo` shows `Info`/`Fail`. `copyOnClick` adds class `MouseClick` and returns the tooltip text. |
| `createLink(url, text, target, title, isImage, useAttr, useEdit, useView)` | Creates `<SysWeaver-Link tabindex=0>`. With `useAttr` it writes an inline `onclick="onLink(event,'url','target')"` attribute, which survives `outerHTML` copies. `getUrlTarget(url)` maps prefixes `+`→`_blank`, `*`→`_self`, `^`→`_top`, `-`→`_parent`. |
| `jsonToHtml(json)` | Syntax highlighting with spans `key/string/number/boolean/null`, styled by `.SysWeaverCode`. |
| `async MarkDownToHTML(text)`, `async SetMarkDown(el, text)` | Lazily calls `includeJs(null, "marked.js")` and installs a `marked` renderer, then returns `marked.parse(text)`. The renderer opens links and images in a new tab with tooltips and `appendTheme`. Raw HTML is kept only for an allow-list of inline tags and `<img>`. |
| `isAnythingSelected(sel)`, `haveElementSelection(el)` | Used to skip click actions while text is selected. |

**Cell rendering**: `ValueFormat.update(el, type, value, flash, format, nextValue, onRefresh)` renders a value into a table cell.

- It picks a formatter by `format` or by the .NET type name. `format` is a `;`-separated string whose first token names the formatter; the remaining tokens are positional options.
- It maintains a `ValueFormat<Name>` class on the element and calls `fn(el, value, formats[], nextValue, flash, type, onRefresh)`.
- `updateFormat(el, name)` resets the element's state when the formatter changes: classes `Right Center Null False Monospaced Text Toggle`, `onclick`, and timers in `el.Updater`.

| Type (`typeFormatter`) | Formatter |
|---|---|
| `System.SByte/Int16/Int32/Int64/Byte/UInt16/UInt32/UInt64/Single/Double/Decimal` | `updateNumber`: right aligned, with `Null`/`False` classes for 0 and negative values. |
| `System.DateTime` | `updateDateTime`: a live clock when the value is empty; `"0001-01-01T00:00:00"` is shown as `-`. |
| `System.TimeSpan` | `updateTimeSpan` |
| `System.Boolean` | `updateBoolean` (☑/☐) |
| `System.Type` | `updateType`: link to `../edit/type.html?q=<type>`. |
| other | `updateDefault` |

| Format token (`formatFormatter`) | Formatter |
|---|---|
| `Number`, `Url`, `Img`, `Tags`, `PerRowFormat`, `ByteSize`, `ByteSpeed`, `Duration` (TimeSpan), `Uptime` (`updateDateTimeLive`, a self-refreshing elapsed time), `WebColor` | Display-only formatters. |
| `Toggle` | Renders `<SysWeaver-toggle>`. A click issues `getRequest(<formats[5] formatted>, true)`, then calls `onRefresh()`. |
| `Actions` | Renders one `<SysWeaver-action>` per option (`text\|title\|url\|icon`). A url prefixed `&` opens with `Open(_self)`, `@` with `Open(_blank)`; any other url is called with `getRequest(url, true)`. Afterwards `onRefresh()` is called. |
| `Json`, `Text`, `MD` | Show a capped first line. A click opens `PopUp(...)` (defined in [`app/application.js`](../app/application.js)) with `<SysWeaver-Code>`, `<SysWeaver-LongText>` or `<SysWeaver-MdText>`, plus an optional copy icon. |

The format strings are produced on the server (table and explorer metadata). They are consumed mainly by the explorer's `table.js` through `ValueFormat.update`.

##### State helpers

`GetHistoryState(key, default)`, `SetHistoryState(key, value)`, `UpdateHistoryState(fn)`, `ClearHistoryState()`, `ClearOtherHistoryState(url)` and `InternalGetHistoryState()` are described under [State and persistence](#state-and-persistence). `Cache` is described under [Server interaction](#server-interaction).

##### Init

`SysWeaverInit()` and `SysWeaverIgnoreUserChanges()` are described under [Loading and initialization](#loading-and-initialization).

#### [`theme.css`](theme.css)

Served as a template, registered with `AddTemplateMatch("common/theme.css")` in `HttpServerBase.cs`. It imports the *Nunito* font from Google Fonts and defines:

- **`:root` defaults** (the *light* theme) for all theme variables, plus placeholders for the derived `*R/*G/*B/*RGB/*25/*50/*75` variables that `applyTheme` overwrites.
- **Theme blocks** selected by `[data-theme=...]`. Each non-light theme sets `color-scheme: dark` on `:root` and `textarea`, and has its own body background (pattern SVG + radial gradients).

  | Theme | Notes |
  |---|---|
  | `dark` | Dark colours. |
  | `hacker` | Also `--ThemePos`, a monospace font, and a fixed `body::before` overlay using `Hacker.png`. |
  | `application` | Colours from `${Color.Background}`, `${Color.Color}`, `${Color.Acc1}`, `${Color.Acc2}`. |
  | `pro` | Nunito, 14 px, `--ThemeIconSize: 0.75`, asymmetric radii. |

- **Base element styles**:
  - `html` (`--Margin: 24px`) and `body` (theme background, colour and font, `graph-paper.svg` pattern, `user-select:none`).
  - Page modes `html.FillScreen`, `html.Embedded` (padding, transparent body) and `html.Transparent`.
  - Links (`a`, `a.Image`), `.ThemeImgClick` and `em`.
  - Colour utility classes: `.ThemeBackgroundBg/Fg`, `.ThemeMainBg/Fg`, `.ThemeAccBg/Fg`, `.ThemeAcc2Bg/Fg`.
- **Styles for the elements created by `common.js`**:
  - Links, pages and toasts: `SysWeaver-Link`, `SysWeaver-Page` (`.Wide`), `SysWeaver-Fail` (`.MsgError`, `.MsgInfo`).
  - Buttons: `SysWeaver-CenterBlock`, `SysWeaver-Button` (`.Pressed`, `.Disabled`, `.ThemeMain`, `.ThemeAcc1`, `.SysWeaverButtonCenter`).
  - Loading and progress: `SysWeaver-Loading` (`.Opaque`), `SysWeaver-LoadingText` (`.Center`), `SysWeaver-ProgressBar*`.
  - Text: `SysWeaver-Text` (`.KeyClick`).
  - The server block: `SysWeaver-Server{Block,Image,Title,Text,Waiting,Time}`.
  - Pop-up content: `SysWeaver-Code` / `.SysWeaverCode` (JSON highlighting), `SysWeaver-LongText` (`.Mono`), `SysWeaver-MdText`.
- **Icon mask classes** used by `common.js` itself: `.IconWorking` (animated spinner), `.IconPaused`, `.IconPower`, `.IconWarning` and `.IconReload` (inline SVG data URIs). Also `.IconLogo` (`../icon.svg`) and `.IconCurrentUser` / `.IconCurrentUserFill` (`../auth/UserImages/Current/small`).

#### [`common.css`](common.css)

An older, non-templated subset of `theme.css`. Its `:root` uses the *dark* colours as defaults, with the same variable set. It has the same `SysWeaver-*` element styles and icon classes, but no themes, no fonts and no base `html`/`body`/`a` rules. There are minor differences, such as a `SysWeaver-Button` radius of `15px` and no `--ThemeIconSize` scaling. No page references it.

#### [`tab.js`](tab.js) / [`tab.css`](tab.css)

`class Tab` (global):

| Member | Behaviour |
|---|---|
| `constructor(saveKey)` | Creates `<SysWeaver-TabBar>` (`this.Element`). With `saveKey`, the selected index is restored from and saved to `localStorage["SysWeaver.Tabs." + saveKey]`. |
| `AddTab(name, onActivate, title, onDeactivate, element, titleSelected)` | Appends a `<SysWeaver-Tab>` with text `name` and tooltip `title`/`titleSelected`. A non-modified click selects it. As tabs are added, the first tab (or the restored index) is selected. Returns the tab element. |
| `Select(index)` | Moves class `tabactive` between the tab elements and their associated content `element`. `onActivate` may be a function `(tabEl, index)` or an element that receives `tabactive`. `onDeactivate(tabEl, index)` is called on the previous tab. The index is saved when selected explicitly. |
| `RemoveTab(index)` | Adjusts the selection and removes the tab. |

`tab.css` styles `SysWeaver-TabBar`, `SysWeaver-Tab` (`.tabactive`) and `SysWeaver-TabContent`, which is hidden unless `.tabactive` and has a `.CODE` variant. The tab control is used by `edit/type.html` and `explore/api.html`.

#### [`anim.js`](anim.js)

Standalone (no dependency on `common.js`); all declarations are global:

| Name | Behaviour |
|---|---|
| `Smoothness` (frozen enum) | `Normal`, `Smooth`, `Smoother`, `Step`. |
| `MathTools` | `Smooth(t)` (smoothstep), `Smoother(t)` (smootherstep), `Step(t)`, `EaseSmooth(t, d)`, `EaseSmooth2(t, in, out)`, `EaseSmoother(t, d)`, `EaseSmoother2(t, in, out)`, `GetSmoothDoubleFunction(smoothness)`. |
| `CubicHermite` | `CalcBasis(t)`, `CalcDerivativeBasis(t)`. |
| `CubicHermiteInterpolator(from, to, init, exit)` | `ValueAt(t)`, `DerivativAt(t)`, `Target`; copy-constructible. |
| `SmoothValue(timeMs, value, duration=1000, init, from)` | A value that glides to new targets along a C1-continuous Hermite curve: `Update(timeMs, value, duration, exitVelocity)`, `ValueAt(timeMs)`, `DerivativeAt(timeMs)`, `SetTo(timeMs, value, init)`, `Target`. Time defaults to `performance.now()`. |
| `SmoothBool(timeMs, startState, duration, smoothness)` | A boolean as a smoothed 0..1 value (`ValueAt`, `Update`, `SetTo`, `Target`), using the constants `FalseValue`/`TrueValue`. |
| `SpringAnimation(frequency, initialLength, decay, snap)` | Damped cosine `Displacement(timeMs)`/`ValueAt`; static `Zoom` preset. |
| `ProjectileMotion` | `FlightTime`, `MaxHeight`, `Displacement`, `Velocity`, `Solve(maxHeight, flightTime)`. |
| `BounceMotion` | Period-based bounce maths: `Period`, `RestAfterPeriod`, `RestTime`, `PeriodVelocity`, `PeriodDuration`, `PeriodEndTime`, `Displacement`, `Displacement2`, `Velocity`, `Velocity2`. |
| `BounceAnimation(v0, g, restitution, restVelocity, normalize, startAtPeak)` | `ValueAt(timeMs)`. |

Used by the media viewer and effect pages of [SysWeaver.MicroService.Media](../../../SysWeaver.MicroService.Media/README.md) (`SmoothBool`, `Smoothness`, `SpringAnimation`, `BounceAnimation`).

### Server interaction

**Request helpers**

`async sendRequest(url, data, reload, errHandler, returnBlob, setupRequestFn, usePost)` serialises `data` with `ToTypedJson` and chooses between GET and POST:

1. GET is only considered when `usePost === false` and the JSON is under 16 KiB.
2. If `encodeURIComponent(json)` is under 128 characters, the request is `GET url?<encoded json>`.
3. Otherwise two candidates are built: `_d<base64url(deflate-raw(utf8))>` and `_u<base64url(utf8)>`. The compressed form is only used if it is at least 1/8 smaller. Whichever is shortest becomes the query, and the GET is used if it is under 4096 characters.
4. Otherwise the request is a **POST** with `Content-Type: application/json`. If compression was computed, the compressed body is sent with `Content-Encoding: deflate`.

Further details:

- A `//DEBUG_BEGIN … //DEBUG_END` block defaults `usePost` to `true` when it is omitted. Callers therefore get plain JSON POSTs unless they pass `false`.
- All requests use `mode: "cors"` and `cache: reload ? "reload" : "default"`; `setupRequestFn(request)` may adjust the request.

| Function | Behaviour |
|---|---|
| `async getRequest(url \| Request, reload, returnBlob, setupRequestFn, returnFn, maxRetryCount)` | GET with the same response handling; `returnFn(response)` can read the response itself. |
| `postData(url, data)` | Fire-and-forget via `navigator.sendBeacon`. |
| `Cache.Get(name, key, lifeTimeMs, fetchFn)`, `Cache.GetRequest(url, lifeTime)`, `Cache.SendRequest(url, data, lifeTime)` | In-memory memoisation keyed by `name + "|" + JSON.stringify(key)`, pruned every 5 s while non-empty. |
| `FlushCache(url)` (awaitable) / `FlushCacheSync(url, whenDone)` | Fire an `XMLHttpRequest` GET with `Cache-Control: no-cache…`, `Pragma` and `Expires` headers and a 1 ms timeout, so the browser revalidates its cached copy. |

Response handling in both helpers:

- **Status other than 200**: `sendRequest` first calls `errHandler(status)`; returning true retries. Otherwise both throw `Error("<status> - <statusText>[\n<body for 500>]")`.
- **Redirect** (e.g. to a login page): the page navigates with `Open(res.url, "_self")` and the helper returns `null`. A second redirect throws.
- **Body that is not JSON**: yields `undefined`.

Callers use `try/catch` + `Fail(...)` for user-visible errors.

**Auth/session**: no tokens or headers are added; requests rely on the server's session cookie. User changes arrive as push messages (`user.login`, `user.logout`) and trigger reloads (see the table below). `navigator.credentials.preventSilentAccess()` is called on logout.

**Endpoints called from this folder**

| Endpoint | Caller | Notes |
|---|---|---|
| `Api/application/GetMessages` (relative to `common.js`) | `SessionManager` master | Long poll, see below. |
| `serverTime` (relative to `common.js`, server root) | `SessionManager` master | Forced endpoint that returns Unix ms as text. The first call passes `?<IANA time zone>;<navigator.language>`, which the server stores as `HttpSession.ClientTimeZone`. |
| `/Api/GetExtensionEditor`, `/Api/GetExtensionViewer` | `EditUrl`, `ViewUrl`, `EditOrOpenUrl` | `ServiceManager` endpoints that map a file URL to a registered editor or viewer page URL. |
| `../edit/type.html?q=<type>` | `ValueFormat.updateType` | Link into the Edit micro service. |
| arbitrary URLs | `ValueFormat` `Toggle` / `Actions` formatters | `getRequest(url, true)` with URLs built from server-provided format strings. |

`GetMessages` is served by `[WebApi("application/{0}")] HttpServerBase.GetMessages(MessageStreamRequest)`, which calls `HttpSession.GetMessages`:

- The request body is `{Cc, MessageTypes[]}`.
- `Cc: 0` returns immediately with `server.connect`. A `Cc` newer than the server's returns `server.reconnect`, meaning the server restarted.
- Otherwise the call waits up to 90 s for messages of the requested types plus the forced types: `user.logout`, `user.login`, `server.shutdown`, `server.pause`, `server.continue`, `server.restart`, `reload`, `refresh`, `filereload`.
- The response is `{Cc, Prefix, Messages[]}`, or `null` on timeout.

**Long-poll loop** (master only):

- Each iteration waits `delay(1)` and POSTs. On success it sets `cc = response.Cc`, stamps each message with `Prefix`, and re-broadcasts it with `InterOp.Post(msg.Type, msg, true)` (`From: "Server"`).
- After 3 consecutive failures it posts InterOp `server.error` and resets `cc = 0`, so the next success yields `server.connect`. After recovery it posts `server.restored`.
- Each failure is followed by a 5 s wait.
- When the set of subscribed types changes, the in-flight fetch is aborted (`AbortController`) and re-issued with the new `MessageTypes`.

**Server time sync** (master): the first sync runs immediately, then the interval starts at 5 s and doubles up to about 5 min. New offsets are blended in over several 500 ms steps, and each value is broadcast as `SessionManager.TimeOffset {O}`.

**Reactions to push messages** (registered in `SysWeaverInit`):

| Type | Windows | Behaviour |
|---|---|---|
| `server.connect` | top | Compares `Value` (the server instance id) with `sessionStorage["SysWeaver.ServerInstance"]`. If different, the page reloads; if the same, the server block overlay is removed. |
| `server.reconnect` | top | Same, but also reloads when no instance was stored. |
| `server.pause`, `server.restart`, `server.shutdown`, `server.error` | top | Show a full-screen `<SysWeaver-ServerBlock>` and swallow all input on `document`. The block has an icon (`IconPaused` / `IconReload` / `IconPower` / `IconWarning`), title, text, spinner and a live "elapsed" timer. |
| `server.continue`, `server.restore` | top | Remove the block. (The poll loop posts `server.restored`.) |
| `reload` | top | After 100 ms, `ReloadAll(true, true)` unless `window.IgnoreUserChanged`. |
| `refresh` | top | After 100 ms, `location.reload(true)` unless ignored. |
| `user.login` | top | Clears the stored server instance, then reloads unless ignored. |
| `user.logout` | top | Calls `preventSilentAccess()`, then reloads unless ignored. |
| `filereload` | all | `ReloadFiles`. The master flushes the browser cache for `Prefix + Value[i]` and re-posts the message with `DidReload: true`. Every window then re-fetches the matching URLs and re-sets matching `href`/`src`/`data` attributes and inline `background-image`/`mask-image` styles. |
| `Theme.Changed` | all | If it came from another window: clear `UseTheme` and call `applyTheme()`. |
| `color.scheme`, `theme.set`, `theme.use` | child frames only, and only when started with `colorscheme` / `settheme` / `usetheme` | Update the color-scheme meta, the stored theme or the forced theme. |

**Templated values**: only `theme.css` is a template (`${Color.Background}`, `${Color.Color}`, `${Color.Acc1}`, `${Color.Acc2}`, defined in `HttpServerBase_Vars.cs`). `common.js` is not templated, but it passes through the JS translation transformer (`_T`/`_TF`… literals).

### State and persistence

| Kind | Key / name | Written by | Purpose |
|---|---|---|---|
| `localStorage` | `SysWeaver.Theme` | `setTheme`, `?settheme`, `theme.set` | Persisted theme name (may contain `name@css-url`). |
| `localStorage` | `SysWeaver.Mobile` | (read only) | JSON boolean override for `window.Mobile`. |
| `localStorage` | `SysWeaver.Tabs.<saveKey>` | `Tab` | Selected tab index. |
| `sessionStorage` | `SysWeaver.ServerInstance` | `server.connect` / `server.reconnect` / `user.login` handlers | Detects server restarts per tab. |
| `sessionStorage` | `<location.href>` | `SetHistoryState` / `UpdateHistoryState` in frames | JSON history state for non-top windows. |
| `history.state` | object | the same helpers in top windows | Page state (`ScrollX`, `ScrollY`, page-defined keys) via `history.replaceState`; cleared on a reload navigation. |
| Web Locks | `SysWeaver.SessionManager` | `SessionManager` | Serialises master election. |
| BroadcastChannel | `SysWeaver` | `InterOp` | Cross-window bus. |

In-memory caches: `window.IncMap` (includes), `Cache.InternalCache`, `ColorIcon.GenCss` with the `<style id="ColorIcon">` sheet, `window.FadeChannels`, `window.SessionManagerFns`, `window.HistoryState`, `window.sysweaverParams`.

**InterOp (BroadcastChannel) messages**: all carry `{Type, From, MagicXyz:"SysWeaver", ...}`.

| Type | Payload | Meaning |
|---|---|---|
| `SessionManager.Ping` / `SessionManager.Pong` | `{To}` on pong | Master discovery (100 ms timeout). |
| `SessionManager.MasterClosed` | `{Cc, ServerEvents:[[name,count]...]}` | The master is unloading; a child takes over with the same `Cc` and subscriptions. |
| `SessionManager.AddEvents` / `SessionManager.RemoveEvents` | `{Events:[names]}` | Subscription ref counts, kept by the master. |
| `SessionManager.GetTimeOffset` / `SessionManager.TimeOffset` | `{O, To?}` | Distribution of the server clock offset. |
| `Theme.Changed` | – | The theme changed in some window. |
| `WindowLoaded` | – | Posted when an `AddLoading` overlay is removed. |
| `server.error`, `server.restored` | – | Generated by the master's poll loop. |
| any server push type (lower-case, e.g. `server.connect`, `user.login`, `filereload`, chat types) | server message fields + `Prefix` | Re-broadcast server messages (`From: "Server"`). |

**Election protocol**:

1. Every top window calls `StartMaster(0)` inside the lock. It pings; if no pong arrives within 100 ms it becomes master (`SessionManager.IsMaster = true`), replays the buffered `StartUp` messages and starts polling.
2. Otherwise it becomes a child: it asks for the time offset and retries the election every second.
3. The master answers pings, `AddEvents`/`RemoveEvents` and `GetTimeOffset`. On `beforeunload`/`pagehide` it posts `MasterClosed`.
4. Same-origin iframes never become master.

**`window.postMessage` messages**: no `MagicXyz`; exchanged between a frame and its parent, top or opener.

| Type | Sent by | Receiver |
|---|---|---|
| `PageLoaded` | `PageLoaded()` → parent/opener | `fixElementWithSrc` iframes with `data-sw-useevent`; the media player also posts it. |
| `LoaderRemoved` | `PageLoaded()` → top | The `app/application.js` shell (`mainMenu.LoaderRemoved`). |
| `IframeNavigating {Url}` | `Open(url, "_self")` → top | The `app/application.js` shell. |

**URL parameters read** (keys are lower-cased by `getUrlParams`): `settheme`, `usetheme`, `css`, `mobile`, `colorscheme`, `ignoreuserchanges`, `transparent`, `fillscreen`, `noloader`. `appendTheme` adds `useTheme` to outgoing URLs.

**Window globals written** (beyond functions and classes): `UseTheme`, `IsDarkMode`, `SysWeaverId`, `SysWeaverServerTimeOffset`, `HaveSysWeaverInit`, `HaveSessionManager`, `Mobile`, `Portrait`, `Narrow`, `SelfPortrait`, `SelfNarrow`, `SwWidth`, `SwHeight`, `IsTop`, `DidInteract`, `ScrollCounter`, `IgnoreUserChanged`, `TabBlockers`, `TabBlockFn`, `DivGetRgbColor`, `_open` (fallback bus). Hooks read from `window`: `window.OnInteraction`, `window[<theme>ThemeAnimate]`.

### Theming and styling

**Selection**: `getTheme()` checks, in order:

1. `window.UseTheme` (URL `usetheme`, message `theme.use`).
2. `localStorage["SysWeaver.Theme"]` (URL `settheme`, `setTheme()`, message `theme.set`).
3. The OS `prefers-color-scheme`.
4. `"dark"`.

Built-in names are `light`, `dark`, `hacker`, `application` and `pro`; the menu entries come from `[WebMenuJs]` attributes on `HttpServerBase`. A theme name of the form `name@path.css` loads an extra stylesheet (relative to `common/`) before applying `name`.

**Application** (`applyTheme`):

1. `light` removes `data-theme` from `<html>`; any other name sets `data-theme="<name>"`.
2. It reads the computed `--ThemeBackground`, `--ThemeMain`, `--ThemeAcc1` and `--ThemeAcc2`, which must be `#rgb` or `#rrggbb` hex values.
3. For each, it writes inline variables on `<html>`:
   - `--X R/G/B` (numbers).
   - `--XRGB` (`"r,g,b"`, used as `rgba(var(--XRGB), a)`).
   - `--X25/--X50/--X75` (rgba strings).
4. It sets `document.documentElement.Theme` and `IsDarkMode` (from the body's computed `color-scheme`), and swaps the `Animator` hook `window[theme + "ThemeAnimate"]`.

**Propagation**: `setTheme`/`resetTheme` post `Theme.Changed`, and all same-origin windows and frames re-apply the theme. Child frames started with `usetheme` keep their forced theme until a change message clears it. `appendTheme` and the Markdown renderer carry `useTheme` into navigations.

**CSS variables**:

- Colours: `--ThemeBackground`, `--ThemeMain`, `--ThemeAcc1`, `--ThemeAcc2`, `--ThemeOk`, `--ThemeError`, `--ThemeWarning`, plus the JS-derived variants above.
- Fonts: `--ThemeFont`, `--ThemeFontMono`, `--ThemeFontSize`.
- Sizes and shapes: `--ThemeIconSize` (scales every `icon-WxH` and `Img` cell), `--ThemeRad`, `--ThemeRadInp`, `--ThemeRadInp2`, `--Margin` (on `html`).
- Other: `--ThemeFadeDuration`, `--ThemePos` (hacker).
- `SysWeaver-ProgressBarText*` refer to `--ThemeMono`, which is not defined.

**Layout classes** set by JS on `<html>` and `<body>`: `SwMobile`/`SwDesktop`, `SwPortrait`/`SwLandscape`, `SwNarrow`/`SwWide`, `SwSelf*`, `Transparent`, `FillScreen`. `Embedded` is styled here but not set by this folder.

**Element and class conventions**:

- Custom tag names (`SysWeaver-*`, `icon-WxH`) are plain unknown elements styled by tag selector, not registered custom elements.
- State classes: `KeyClick`, `MouseClick`, `Pressed`, `Disabled`, `IconButton`, `IconDisabled`, `IconColorTheme*`, `Stick`, `Capped`, `Right`, `Center`, `Monospaced`, `Null`, `False`, `Text`, `Toggle`, `ValueFormat<Format>`, `ValueFormatDt…`, `Active`, `tabactive`, `Opaque`.
- Elements hidden by `fixElementWithSrc` use inline `visibility`/`opacity`, so those properties cannot be styled on such elements.

**Data attributes**: `data-theme` (on `<html>`), `data-sw-fade`, `data-sw-fadein`, `data-sw-fadedelay`, `data-sw-fadechannel`, `data-sw-fadeparent`, `data-sw-useevent`.

### Dependencies

**Needs from elsewhere**

- **Server**: the session cookie, `Api/application/GetMessages`, `serverTime`, the ServiceManager `GetExtensionEditor`/`GetExtensionViewer` APIs, the JS translation transformer and the `theme.css` template variables. The CSS icon classes use `../icon.svg` and `../auth/UserImages/Current/small`.
- **`PopUp`**: `PopUp(build, blockUntilClosed, dontAllowClose, onClose, haveCloseButton)` from [`app/application.js`](../app/application.js), for the `Json`/`Text`/`MD` cell pop-ups.
- **Icon CSS classes**: classes not defined here (`IconCopy`, `IconTheme*`, …) come from consumer stylesheets such as `app/application.css`, `explore/table.css` and `chat/chat.css`.
- **`iso_data/currencies.js`**: supplies the inputs for `ValueFormat.toAmount`.
- **External**: Google Fonts (*Nunito*) from `theme.css`.
- **Shared `common/` folder**: other projects also publish into the served `common/` folder. For example, [`SysWeaver.MicroService.FileUploader/srcWeb/common/fileUploader.js`](../../../SysWeaver.MicroService.FileUploader/srcWeb/common/fileUploader.js) is loaded as `common/fileUploader.js` / `includeJs(null, "fileUploader.js")`.

**Consumed by**: usage counts across other `srcWeb` folders (occurrences / files, excluding this folder and vendored code):

| API | Occurrences / files |
|---|---|
| `_TF` / `_T` / `_TH` | 960 / 24, 148 / 26, 18 / 1 |
| `Fail` / `Info` / `Message` | 212 / 30, 38 / 17, 99 / 7 |
| `ValueFormat.*` | 161 / 33 |
| `sendRequest` / `getRequest` | 110 / 23, 34 / 18 |
| `ColorIcon` / `Button` | 59 / 16, 54 / 12 |
| `badClick` / `keyboardClick` / `isPureClick` | 51 / 17, 23 / 8, 10 / 5 |
| `includeJs` / `includeCss` | 49 / 10, 3 / 3 |
| `Open` | 48 / 17 |
| `delay` | 44 / 17 |
| `getUrlParams` | 20 / 17 |
| `AddLoading` / `PageLoaded` | 19 / 19, 15 / 12 |
| `hashString`, `bufferToBase64`, `createImg`, `createIFrame`, `GetAbsolutePath`, `makeHtmlSafe`, `SetTemporaryInnerText` | 3–22 each |
| `InterOp.Post/AddListener/GetMessage`, `SessionManager.AddServerEvent` | Used by `chat.js`, `chart.js` (listens for `Theme.Changed`) and `application.js`. |
| `Tab` | `edit/type.html`, `explore/api.html` |
| `anim.js` classes | Media viewer and effect pages. |

The most used `ValueFormat` members are `copyToClipboardInfo`, `copyOnClick`, `AddNonNullLine`, `createLink`, `stringFormat`, `toString`, `getTimeStampTitle`, `formatByteSize`, `countSuffix`, `update`, `updateDateTime(Live)`, `MarkDownToHTML` and `jsonToHtml`.

Many exported helpers have no callers outside this folder, for example `Cache`, `ProgressBar`, `PostTop/PostParent/PostAll`, `GetServerTime`, `EnterNoSleep`, `CaptureVideoFrame`, `CurrencyFormatOptions`, and the history-state helpers except `ClearOtherHistoryState`. `setTheme` is only called from the server-generated menu.

### Structural observations

- **Module style**: classic scripts only, with no ES modules, bundler or namespaces. `common.js` defines about 200 global functions, classes and consts, and `anim.js` and `tab.js` add more globals. Load order is implicit (the `defer` order in each HTML page), and initialisation side effects run when the script is evaluated (`SessionManager.Create(); SysWeaverInit();`).
- **`document.currentScript` contract**: the base URLs for the API (`SessionManager.Url`), `serverTime` and `includeJs/includeCss(null, …)` are derived from where `common.js` was loaded. This lets the site run under a URL prefix, but it also means `common.js` cannot be injected dynamically or turned into a module without replacing this mechanism. `EditUrl`/`ViewUrl` instead use root-absolute `/Api/...` URLs.
- **Global state on `window`/`document` and DOM expandos**: many flags and registries live on `window` (listed above). DOM elements carry ad-hoc properties:
  - `FadeIn`, `FadeStyle`, `StopListen`, `SrcObs`, `AnimReq`, `Format`, `Updater`, `MsgHandler`, `Button`, `TempText`, `TempId`, `NextLast`.
  - `document.body.FailElement/FailTimer`, `document.NoSleep`, `documentElement.Theme/IsDarkMode/ThemeAnim`.
- **Two messaging layers**:
  - `InterOp`: BroadcastChannel, same-origin, all tabs, magic-tagged messages.
  - Raw `postMessage` helpers: frame ↔ parent/top, untagged `{Type}` objects.
  - The `InterOpWindows` fallback also uses `postMessage`, so both kinds can arrive on the same `message` event.
  - Message types are compared case-sensitively in `SysWeaverInit` (`"Theme.Changed"`, `"filereload"`, `"server.*"`), while `SessionManager.AddMessageHandler` lower-cases them and the server lower-cases pushed types.
- **Implicit contracts with other folders**: `PopUp` (app), `LoaderRemoved`/`IframeNavigating`/`PageLoaded` (app shell), icon CSS class names, the `table.js` format strings for `ValueFormat.update`, the server `[WebMenuJs]` strings that call `setTheme`/`resetTheme`, and `SYSWEAVER-TABBLOCK` sentinel elements.
- **Duplicated patterns**:
  - `includeJs`/`includeCss` (same body).
  - `PostAll`/`PostTop`/`PostParent`.
  - `FlushCache`/`FlushCacheSync`.
  - The response handling of `sendRequest`/`getRequest`.
  - `updateJson`/`updateMarkDown`/`updateLongText` (capping + pop-up).
  - `isInDocument`/`IsAttached` and `concatArrays`/`ConcatUint8Arrays`.
  - Several base64 helpers.
  - The four class-pair toggles in `onSizeChange`.
  - `theme.css` vs `common.css`, and two `SysWeaver-Loading` rule blocks inside `theme.css`.
- **Naming**:
  - Top-level functions mix PascalCase (`Fail`, `Open`, `AddLoading`, `GetHistoryState`, classes) and camelCase (`sendRequest`, `includeJs`, `badClick`, `getUrlParams`).
  - `ValueFormat` mixes `updateX`/`toString` with `MarkDownToHTML`/`AddNonNullLine`.
  - Element tag casing varies: `SysWeaver-Button` in CSS, `Sysweaver-Button` in `ButtonStyle` (HTML tag names are case-insensitive).
- **Translation coupling**: all user-visible strings are `_T`/`_TF` literals with a context argument. A refactoring must keep these as literal-argument calls for the server-side translator.
- **Async conventions**: `async` is used throughout, with fire-and-forget calls during init (`applyTheme()`, `SessionManager.Init()`, `includeCss`). Polling with `setTimeout` (10 ms) is used for include de-duplication and `PendingImages.WaitAll`. Live values use per-element timers (`el.Updater`).
- **Size and cohesion**:
  - `ValueFormat` (about 1640 lines) mixes generic formatting utilities with table-cell rendering.
  - `SessionManager.Init` (about 460 lines) contains election, polling, time sync and teardown as nested closures.
  - `SysWeaverInit` (about 820 lines) contains layout, theme, server-block UI and file-reload logic.
