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
| Scroll save | Registers `saveScroll` on `window` for `"beforeunload"`. `saveScroll` stores `ScrollX/ScrollY` in history state. |
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
- After 3 consecutive failures it posts InterOp `server.error` and resets `cc = 0`, so the next success yields `server.connect`. After recovery it posts `server.restore`, which removes the blocking overlay.
- Each failure is followed by a 5 s wait.
- When the set of subscribed types changes, the in-flight fetch is aborted (`AbortController`) and re-issued with the new `MessageTypes`.

**Server time sync** (master): the first sync runs immediately, then the interval starts at 5 s and doubles up to about 5 min. New offsets are blended in over several 500 ms steps, and each value is broadcast as `SessionManager.TimeOffset {O}`.

**Reactions to push messages** (registered in `SysWeaverInit`):

| Type | Windows | Behaviour |
|---|---|---|
| `server.connect` | top | Compares `Value` (the server instance id) with `sessionStorage["SysWeaver.ServerInstance"]`. If different, the page reloads; if the same, the server block overlay is removed. |
| `server.reconnect` | top | Same, but also reloads when no instance was stored. |
| `server.pause`, `server.restart`, `server.shutdown`, `server.error` | top | Show a full-screen `<SysWeaver-ServerBlock>` and swallow all input on `document`. The block has an icon (`IconPaused` / `IconReload` / `IconPower` / `IconWarning`), title, text, spinner and a live "elapsed" timer. |
| `server.continue`, `server.restore` | top | Remove the block. (`server.restore` is posted by the poll loop after recovery.) |
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
| `server.error`, `server.restore` | – | Generated by the master's poll loop. |
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

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function; "L123" is a line number in the file at the time of writing; parameters shown with `?` are optional.

### `common.js`

The core client library of the framework: one classic (non-module) script of global functions, global `const`s and global classes, loaded by almost every page with a static `<script defer src="../common/common.js">` before the page's own scripts. It must be loaded as a classic script because several static initialisers read `document.currentScript.src` (`SessionManager.Current`, `ValueFormat.Current`) to derive the API, `serverTime` and include base URLs. It provides translation markers (`_T`, `_TF`, ...), theming, server time and the cross-tab `SessionManager` (leader election plus the server long-poll), cross-window messaging (`InterOp`), encoding/hashing helpers, DOM/focus helpers, smooth `src` loading for images and iframes, dynamic includes (`includeJs`, `includeCss`), async utilities, navigation and `postMessage` helpers, cache flushing, `ColorIcon` / `Button` UI blocks and toasts (`Fail`, `Info`, `Message`). The second half of the file (from `class ValueFormat`, L3934) adds `ValueFormat`, `FullScreen`, requests (`sendRequest`, `getRequest`), `ProgressBar`, `Animator`, `PendingImages`, `Cache`, history-state helpers and `SysWeaverInit`. The last two lines run `SessionManager.Create(); SysWeaverInit();` when the script is evaluated. This section covers L1–3933.

#### Globals (L1–3933)

| Name | Kind | Description |
|---|---|---|
| `CurrencyFormatOptions` | const (frozen object) | Bit flags for currency formatting (consumed by `ValueFormat.toAmount`): `None: 0`, `IsoPrefix: 1` ("USD 100.00"), `IsoSuffix: 2` ("100.00 USD"), `Symbol: 3` ("$100.00", "100.00 kr"), `ForceRounding: 4` (round to integer), `ApplyThousandsSeparator: 8`, `AutomaticRounding: 16` (drop zero sub-units, "$100.00" → "$100"), `Default: 3 \| 8`. |
| `window.UseTheme` | window global | Forced theme name (set from URL `usetheme` by `SysWeaverInit`). Read first by `getTheme`, cleared by `setTheme`, appended to URLs by `appendTheme`. |
| `window.IsDarkMode` | window global | Set by `applyTheme`: true when the computed `color-scheme` of `<body>` is `dark`. Also mirrored as `document.documentElement.IsDarkMode`. |
| `document.documentElement.Theme`, `.ThemeAnim` | element expandos | `Theme` = last applied theme string (including any `@css` suffix), used by `applyTheme` to skip re-applying; `ThemeAnim` = the per-frame theme animation function registered with `Animator`. |
| `window.SysWeaverServerTimeOffset` | window global | Milliseconds to add to the local clock to get the estimated server time. Written by the `SessionManager` master's time sync (and by `SessionManager.TimeOffset` messages in other windows); read by `GetServerTime`/`GetServerTick`. |
| `window.HaveSessionManager` | window global | Guard so `SessionManager.Init` runs once per window. |
| `window.SysWeaverId` | window global | The window's `InterOp.Id` (set by the `InterOp.Id` initialiser and again in `SessionManager.Init`). |
| `window.SessionManagerFns` | window global (Map) | Lower-cased message type → array of handlers; created lazily by `SessionManager.GetMap`. |
| `window._open` | window global | The original `window.open`, saved by `InterOpWindows` when it replaces `window.open` (only when `BroadcastChannel` is unavailable). |
| `window.sysweaverParams` | window global | Cached `URLSearchParams` with lower-cased keys, created by `getUrlParams`. |
| `window.FadeChannels` | window global (Map) | Fade channel name → channel state (`{Name, Count, StartTime, Delay, FirstElement, Index, Delayed, ItemCount}`), created by `fixElementWithSrc`, entries removed by `removeFadeChannel`; the global is deleted when empty. |
| `window.IncMap` | window global (Map) | Absolute URL → include state (`{element, error, errorEv, Pending, loaded}`) shared by `includeJs` and `includeCss` for de-duplication. |
| `window.DivGetRgbColor` | window global | Hidden `<div>` reused by `GetRgbColor`. |
| `document.body.FailElement`, `.FailTimer` | element expandos | The single toast element and its removal timer, managed by `Message`. |
| `localStorage["SysWeaver.Theme"]` | storage key | Persisted theme choice; read by `getTheme`, written by `setTheme`, removed by `resetTheme`. |

#### Functions: translation markers

All four are recognised by the server-side JS translation transformer, which rewrites the string literals when the `.js` file is served; the first argument must therefore be a string literal.

| Function | Signature | Description |
|---|---|---|
| `_T` | `_T(format, ...args)` → string | Marks `format` for translation and returns `ValueFormat.stringFormatArgs(format, args)`. Placeholders `{0}`, `{1}`, ...; prefix `_` lower-cases the argument, `^` upper-cases it, `$` lower-cases it unless the placeholder is at position 0. A trailing string literal not used by the format is the translation context (the server may remove the call when only a context argument is present). |
| `_TH` | `_TH(format, ...args)` → string | Same runtime behaviour as `_T`; signals that the translated text is to be made HTML-safe by the server. |
| `_TF` | `_TF(text, translationContext?)` → string | Identity at runtime (returns `text`); marks a literal with no parameter substitution. The server translates and removes the call. |
| `_TFH` | `_TFH(text, translationContext?)` → string | Identity at runtime; like `_TF`, with HTML-safe translated output (arguments are not escaped). |

#### Functions: theme

| Function | Signature | Description |
|---|---|---|
| `hackerThemeAnimate` | `hackerThemeAnimate(time)` | Per-frame hook for the `hacker` theme (found by `applyTheme` as `window[theme + "ThemeAnimate"]`). Sets CSS variable `--ThemePos` on `<html>` to `-round((time*0.01) % 16) * 4` px. Not to be called manually. |
| `getTheme` | `getTheme()` → string | Returns `window.UseTheme` if set; else `localStorage["SysWeaver.Theme"]` if non-empty; else `"light"` / `"dark"` from `prefers-color-scheme`; default `"dark"`. |
| `applyTheme` | async `applyTheme()` | Applies `getTheme()` to `<html>`; no-op if `documentElement.Theme` already equals it. A theme of the form `name@css` first awaits `includeCss(null, css)`. `light` removes `data-theme`, any other name sets `data-theme=name`. Computes `R`, `G`, `B`, `RGB`, `25`, `50`, `75` derived variables (e.g. `--ThemeMainR`, `--ThemeMain50` = `rgba(r,g,b,0.50)`) for `--ThemeBackground`, `--ThemeMain`, `--ThemeAcc1`, `--ThemeAcc2` via the inner `fix`. Sets `Theme`, `IsDarkMode` (from `<body>` computed `color-scheme`), and swaps the `Animator` theme animation (`window[name + "ThemeAnimate"]`). Logs "Theme: Applying theme". |
| `fix` (inner of `applyTheme`) | `fix(name)` | Reads the computed value of CSS variable `name` as `#rgb` or `#rrggbb` and writes `name+"R"`, `"G"`, `"B"`, `"RGB"` (`"r,g,b"`), `"25"`, `"50"`, `"75"` (rgba strings) as inline properties on `<html>`. Returns without writing if the variable is empty. |
| `resetTheme` | async `resetTheme()` → boolean | Removes `localStorage["SysWeaver.Theme"]`, awaits `applyTheme`, posts InterOp `Theme.Changed`. Returns true if `getTheme()` changed. |
| `toggleTheme` | async `toggleTheme()` | `setTheme("dark")` if the current theme is `light`, otherwise `setTheme("light")`. |
| `setTheme` | async `setTheme(themeName)` → boolean | Clears `window.UseTheme`, stores `themeName` in `localStorage["SysWeaver.Theme"]`, awaits `applyTheme`, posts InterOp `Theme.Changed`. Returns true if the theme changed. |
| `appendTheme` | `appendTheme(url)` → string | If `window.UseTheme` is set, appends `?useTheme=<t>` or `&useTheme=<t>` to `url`; otherwise returns `url` unchanged. |

#### Functions: server time

| Function | Signature | Description |
|---|---|---|
| `GetServerTime` | `GetServerTime()` → Date | `new Date(Date.now() + window.SysWeaverServerTimeOffset)` (offset ignored if not a number). |
| `GetServerTick` | `GetServerTick()` → number | Same as `GetServerTime` but returns Unix milliseconds. |

#### class `SessionManager`

Static-only class that elects one top-level window per origin as "master" (via `navigator.locks` and a ping/pong over `InterOp`), runs the server message long-poll and server-time sync in that master, and redistributes server messages to all windows through `InterOp`. `Create()` is called at the end of the file; `Init()` is called by `SysWeaverInit`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Create` | static method | `Create()` | Sets `StartUp = []` and adds `InternalOnMsg` as an InterOp listener, buffering every message until a master consumes them. Also called again when a `MasterClosed` message is received. |
| `InternalOnMsg` | static method | `InternalOnMsg(msg)` | Pushes `msg` (the raw message event) to `StartUp`. |
| `StartUp` | static field | array \| null | Buffered InterOp messages; set to `null` (and `InternalOnMsg` removed) once a master starts or a master election attempt finishes. |
| `Current` | static field | string | `document.currentScript.src` (the URL of `common.js`). |
| `AddServerEvent` | static method | `AddServerEvent(name)` → boolean | Lower-cases `name`, ref-counts it in `ServerEvents` and `OwnedEvents`. On the first reference posts InterOp `SessionManager.AddEvents` `{Events:[name]}` and returns true; otherwise false. |
| `RemoveServerEvent` | static method | `RemoveServerEvent(name)` → boolean | Lower-cases `name`, decrements it in `ServerEvents` and `OwnedEvents`. When the count reaches 0 posts `SessionManager.RemoveEvents` `{Events:[name]}` and returns true; otherwise false. |
| `AddRef` | static method | `AddRef(map, name, isOwned)` → boolean | Increments `map[name]`; if `isOwned` also increments `OwnedEvents[name]`. Returns true when the count became 1. |
| `DecRef` | static method | `DecRef(map, name, isOwned)` → boolean | If `isOwned` decrements `OwnedEvents[name]` first. Decrements `map[name]`, deleting the key at 0 (or if missing). Returns true only when the count went from 1 to 0. |
| `ServerEvents` | static field | `Map<string, number>` | All server event names wanted by any window (ref-counted); maintained by the master from `AddEvents`/`RemoveEvents` messages and sent as `MessageTypes` in the long-poll. |
| `OwnedEvents` | static field | `Map<string, number>` | Events registered by this window; released on `beforeunload`/`pagehide`. |
| `Url` | static field | string | Absolute `<Current>/../../Api/application/GetMessages`, the long-poll endpoint. |
| `IsMaster` | static field | boolean | True while this window is the master. |
| `Init` | static async method | `Init()` | Runs once per window (`window.HaveSessionManager`), sets `window.SysWeaverId`. Same-origin child frames only register `beforeunload`/`pagehide` cleanup (inner `WindowClose`), a `SessionManager.TimeOffset` handler and post `SessionManager.GetTimeOffset`. Top windows (or frames with a cross-origin top) install the master/child message maps, call `StartMaster(0)` and, if not elected, `StartChild()`, and register their own `WindowClose`. See inner functions below. Message types used (prefix `SessionManager.`): `Ping`, `Pong`, `MasterClosed`, `AddEvents`, `RemoveEvents`, `TimeOffset`, `GetTimeOffset` (`GetEvents` is defined but unused). |
| `GetMap` | static method | `GetMap()` → Map | Returns `window.SessionManagerFns`, creating it on first use together with one InterOp listener that, for each message, awaits every handler registered for its lower-cased `Type`, passing the message object. |
| `AddMessageHandler` | static method | `AddMessageHandler(messageType, fn, ignoreServer?)` | Appends async `fn(msg)` to the handler list for lower-cased `messageType`; unless `ignoreServer`, also calls `AddServerEvent(messageType)`. Handlers must be removed explicitly. |
| `RemoveMessageHandler` | static method | `RemoveMessageHandler(messageType, fn, ignoreServer?)` | Removes `fn` from the list (no-op if not found); unless `ignoreServer`, calls `RemoveServerEvent`. Deletes the list when empty. |

#### Inner functions of `SessionManager.Init`

| Function | Signature | Description |
|---|---|---|
| `WindowClose` (child frame, L383) | `WindowClose()` | On `beforeunload`/`pagehide` in a same-origin child frame: removes both listeners, collects every owned event (repeated by its count), `DecRef`s them and posts `SessionManager.RemoveEvents` `{Events}`. |
| `StartServerTimeSync` | `StartServerTimeSync()` | Starts the master's clock sync loop (inner `update`) against `<Current>../../../serverTime` (`fetch`, `cache: "reload"`). The first request appends `?<IANA time zone>;<navigator.language>`. |
| `update` (inner of `StartServerTimeSync`) | async `update()` | Stops if not master. Measures the round trip; offset = server ms + latency/2 − local now. First success sets `window.SysWeaverServerTimeOffset` and posts `SessionManager.TimeOffset {O}`; later changes are applied gradually via `updateTime`. Reschedules itself after `nextUpdate` (5 s doubling up to ≥300 s; `updateDelay` doubles from 2 up to ≥30), or after 2 s / 15 s on failure. |
| `updateTime` (inner of `update`) | `updateTime()` | Steps the offset linearly from the old to the new value over `updateDelay` steps 500 ms apart, posting `SessionManager.TimeOffset {O}` each step. Stops if no longer master. |
| `serverEventsChanged` | `serverEventsChanged()` | Aborts the current `masterAbort` controller so the long-poll restarts with the new `ServerEvents` set. |
| `RunMaster` | async `RunMaster(startCc?)` | Master loop. Sets the change counter `cc`, starts `StartServerTimeSync`, replays and stops the `StartUp` buffer through `eventHandler`. While master: POSTs `ToTypedJson({Cc, MessageTypes})` to `SessionManager.Url` with the abort signal; on 200 sets `cc = response.Cc`, adds `response.Prefix` to each of `response.Messages` and re-posts each as an InterOp message of its `Type` with `From: "Server"`; posts `server.restore` after an error streak. An `AbortError` restarts immediately; other failures call `onError` and wait 5 s. |
| `onError` (inner of `RunMaster`) | `onError()` | Increments the error count; on the 3rd consecutive error posts InterOp `server.error` and resets `cc` to 0. |
| `sendEvents` | `sendEvents()` | Posts `SessionManager.AddEvents` with all `ServerEvents` keys (its only call is commented out). |
| `response` | `response(name, fn)` | Local registrar: first bound to `masterMap.set`, then reassigned to `childMap.set`. Master handlers: `AddEvents` (AddRef, then `serverEventsChanged` if any is new), `Ping` (reply `Pong {To}` unless own), `RemoveEvents` (DecRef, `serverEventsChanged` if any reached 0), `GetTimeOffset` (reply `TimeOffset {To, O}`). Child handlers: `Pong` (if addressed to this window, sets `gotMasterPong` and dispatches `GotPong` on the internal `SysWeaver-EventHandler` element), `MasterClosed` (calls `Create()`; unless self-sent, takes `ServerEvents` from the message and calls `StartMaster(1, data.Cc)`), `TimeOffset` (stores `O` unless self-sent). |
| `eventHandler` | async `eventHandler(ev)` | The InterOp listener: extracts the message with `InterOp.GetMessage` and awaits the matching handler from `masterMap` or `childMap` depending on `isMaster`. |
| `HaveMaster` | async `HaveMaster(first?)` → boolean | Posts `SessionManager.Ping` and waits (via `waitEvent2`) for `GotPong` or a 100 ms `GotTimeOut`; returns whether a master answered. |
| `StartMaster` | async `StartMaster(how, startCc?)` | Runs inner `Start` under the Web Lock `"SysWeaver.SessionManager"` (`navigator.locks.request`), or directly if locks are unavailable. `how`: 0 = initial, 1 = from `MasterClosed` message, 2 = from polling (only affects the log line). |
| `Start` (inner of `StartMaster`) | async `Start()` | Re-entrancy guarded by `isStarting`. If no master answers `HaveMaster` and this window is not master: clears the child poll timer, sets `isMaster`/`SessionManager.IsMaster`, logs, and calls `RunMaster(startCc)` (not awaited). Always removes `InternalOnMsg` and nulls `StartUp` afterwards. |
| `MaybeStartMaster` | async `MaybeStartMaster(first?)` | `StartMaster(first === true ? 0 : 2)`; called every second by the child timer. |
| `StartChild` | `StartChild()` | Logs, posts `SessionManager.GetTimeOffset`, and starts a 1 s `setInterval(MaybeStartMaster)` to take over if the master disappears. |
| `WindowClose` (top window, L780) | `WindowClose()` | On `beforeunload`/`pagehide`: removes listeners and `eventHandler`, `DecRef`s owned events. A child stops its timer and posts `RemoveEvents`; the master posts `SessionManager.MasterClosed {Cc, ServerEvents: entries}` so a child can take over, and clears `IsMaster`. |

#### class `InterOpBroadcast`

Message bus built on `BroadcastChannel`; created once by `InterOp.B` as `new InterOpBroadcast("SysWeaver")`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| constructor | constructor | `new InterOpBroadcast(name)` | Opens `BroadcastChannel(name)` and keeps a private listener list. |
| `Post` | field (function) | `this.Post = m => …` | `postMessage(m)` on the channel, then calls every local listener with `{data: m}` (so the posting window also receives its own messages); listener exceptions are caught and logged with `console.warn`. Logs a line when `m.Type === "chat.replace.openai.pure"`. |
| `AddListener` | field (function) | `this.AddListener = l => …` | Adds `l` to the local list and as a channel `message` listener. |
| `RemoveListener` | field (function) | `this.RemoveListener = l => …` | Removes `l` from the channel and from the local list (last occurrence). |

#### class `InterOpWindows`

Fallback bus used by `InterOp.B` when constructing a `BroadcastChannel` throws (logs "Using opened windows fallback").

| Member | Kind | Signature | Description |
|---|---|---|---|
| constructor | constructor | `new InterOpWindows()` | Collects the `window.parent` chain into a private window list, saves `window.open` as `window._open` and replaces `window.open(url, name, params)` with a wrapper that also adds the opened window to the list when `url` is relative or same-origin. |
| `Post` | field (function) | `Post(m)` | `postMessage(m)` to every tracked window; closed or failing windows are removed from the list. |
| `AddListener` / `RemoveListener` | field (function) | `(l)` | Add / remove `l` as a `window` `message` listener. |

#### class `InterOp`

Static facade for cross-window messages. Messages are plain objects tagged with `Type`, `From` and `MagicXyz: "SysWeaver"`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Id` | static field | number | `Date.now() + Math.random()`, computed once; also stored in `window.SysWeaverId`. |
| `B` | static field | `InterOpBroadcast` \| `InterOpWindows` | The underlying bus. |
| `Post` | static method | `Post(type, data?, isFromServer?)` | Uses `data` if it is an object (mutating it) or creates `{}`; sets `Type = type`, `From = isFromServer ? "Server" : InterOp.Id`, `MagicXyz = "SysWeaver"`, and posts through `B`. `isFromServer` is only used by the `SessionManager` master. |
| `AddListener` | static method | `AddListener(fn)` → fn | Adds a listener receiving a `MessageEvent`-like `{data}`. |
| `RemoveListener` | static method | `RemoveListener(fn)` → fn | Removes the listener. |
| `GetMessage` | static method | `GetMessage(ev)` → object | Returns `ev.data` when it has `MagicXyz === "SysWeaver"` and a string `Type`; otherwise `{Type: null, From: null}`. |

#### Functions: downloads and URL parameters

| Function | Signature | Description |
|---|---|---|
| `downloadText` | `downloadText(filename, text)` | Downloads `text` as `filename` via a temporary hidden `<a download href="data:text/plain;charset=utf-8,...">` that is clicked and removed. |
| `downloadFile` | `downloadFile(filename?, url)` | Same with `href = url`; `filename` defaults to the last path segment of `url`. |
| `getUrlParams` | `getUrlParams()` → URLSearchParams | Copy of `location.search` with lower-cased keys, cached in `window.sysweaverParams`. |
| `getNumberParam` | `getNumberParam(params, name)` → number \| null | `parseFloat` of `params.get(name)` (may be `NaN`), or `null` when absent. |
| `getIntParam` | `getIntParam(params, name)` → number \| null | `parseInt` of `params.get(name)`, or `null` when absent. |

#### Functions: platform detection

| Function | Signature | Description |
|---|---|---|
| `isMobile` | `isMobile()` → boolean | `navigator.userAgentData.mobile` when available, otherwise the classic detectmobilebrowsers regexes on `navigator.userAgent \|\| navigator.vendor \|\| window.opera`. |
| `getPlatform` | `getPlatform()` → string | Lower-cased `userAgentData.platform`; else `"android"` if the UA says so, else lower-cased `navigator.platform`; else UA heuristics (`macos`, `ios`, `windows`, `android`, `linux`) or `"unknown"`. |
| `getMobileParams` | `getMobileParams()` → `[isMobile, useSingleColumn, isPortrait, scale]` | Portrait = width < height. Single column when portrait and (mobile or width ≤ max scale width: 400 for mobile portrait, else 800). On mobile, `scale` = width/max if wider than max, or width/min (200 or 400) if narrower than min; else 1. |

#### Functions: random, encoding and hashing

| Function | Signature | Description |
|---|---|---|
| `CryptRandomInt32` | `CryptRandomInt32()` → number | Signed 32-bit random from `crypto.getRandomValues`, falling back to `Math.random`. |
| `CryptRandomGuid` | `CryptRandomGuid()` → string | `crypto.randomUUID()`, falling back to a v4-shaped GUID from `Math.random` using a hex lookup table cached in `CryptRandomGuid.lut`. |
| `legacySha256` | `legacySha256(buffer)` → Uint8Array | Pure-JS SHA-256 of a byte array, used by `hashData` when `crypto.subtle` fails. The hash function (with its prime-derived constants) is built once and cached in `legacySha256.data`. |
| `convertEndian` (inner of `legacySha256`) | `convertEndian(word)` → number | Byte-swaps a 32-bit word on little-endian platforms; identity otherwise. |
| `rightRotate` (inner of `legacySha256`) | `rightRotate(word, bits)` → number | 32-bit rotate right. |
| `getFractionalBits` (inner of `legacySha256`) | `getFractionalBits(n)` → number | First 32 bits of the fractional part of `n` (for the SHA-256 initial state and round constants). |
| `RemoveNulls` | `RemoveNulls(o)` → boolean | Recursively deletes properties whose value is `null` from objects (and objects inside arrays), in place. Returns true only when `o` itself is `null`. |
| `MakeJsObject`, `MakeCsObject` | — | Commented out (L1485–1542): would recursively camelCase / PascalCase property names. Not defined at runtime. |
| `Base64EncodeString` | `Base64EncodeString(anyString)` → string | Base64 of the UTF-8 bytes of the string. |
| `Base64DecodeString` | `Base64DecodeString(base64String)` → string | Inverse: base64 → bytes → UTF-8 string. |
| `MakeUriSafeBase64` | `MakeUriSafeBase64(str)` → string | Replaces `+`→`-`, `/`→`_` and cuts at the first `=` (removes padding). |
| `Uint8ArrayToBase64` | `Uint8ArrayToBase64(bytes, uriSafe?)` → string | Uses native `Uint8Array.prototype.toBase64` (`base64url`, no padding when `uriSafe`) when available, otherwise `btoa` over a binary string, then `MakeUriSafeBase64` if `uriSafe`. |
| `bufferToHex` | `bufferToHex(buffer)` → string \| null | Lower-case hex of the bytes; `null` for a falsy input. |
| `bufferToBase64` | async `bufferToBase64(buffer, keepUri?)` → string \| null | Wraps an ArrayBuffer/Uint8Array in a `Blob` (Blobs used as-is) and reads it with `FileReader.readAsDataURL` (its `reader.onload` resolves the promise). Returns the whole `data:` URI if `keepUri`, otherwise only the base64 part. `null` for a falsy input. |
| `base64ToArray` | `base64ToArray(base64String)` → Uint8Array \| null | Decodes base64 to bytes; `null` for a falsy input. |
| `hashDataSha1` | async `hashDataSha1(hashData)` → ArrayBuffer | `crypto.subtle.digest("SHA-1", data)`. |
| `hashStringSha1` | async `hashStringSha1(hashSrc)` → string | SHA-1 of the UTF-8 string, as base64. |
| `hashData` | async `hashData(hashData)` → ArrayBuffer \| Uint8Array | `crypto.subtle.digest("SHA-256", data)`, falling back to `legacySha256` when that throws (e.g. insecure context). |
| `hashString` | async `hashString(hashSrc)` → string | SHA-256 of the UTF-8 string, as base64. |
| `concatArrays` | `concatArrays(uint8arrays)` → Uint8Array | Concatenates an array of byte arrays into one new `Uint8Array`. |

#### Functions: DOM traversal, focus and click helpers

| Function | Signature | Description |
|---|---|---|
| `isInDocument` | `isInDocument(el)` → boolean | True if walking `parentElement` reaches `document.body`. |
| `isHidden` | `isHidden(element)` → boolean | True if the element's computed `display` is `none` or it is `<head>`. Parents are not checked. |
| `traverseChildren` | `traverseChildren(element, okFn, traverseHidden?)` → any | Depth-first, document-order walk of `element` and its descendants (iterative, with a stack), calling `okFn(el)`; returns the first truthy result, else `null`. Hidden subtrees are skipped unless `traverseHidden`. |
| `traverseForward` | `traverseForward(element, okFn, traverseHidden?)` → any | Document-order walk starting after `element` (its own subtree included first, `element` itself not passed), continuing through following elements and wrapping around from `document.documentElement`, until it returns to `element`. Returns the first truthy `okFn` result or `null`. |
| `traverseBackward` | `traverseBackward(element, okFn, traverseHidden?)` → any | Reverse document-order counterpart, starting before `element` and wrapping around. Does not apply the `isHidden` filter (the `traverseHidden` parameter is unused). |
| `isTabable` | `isTabable(element)` → boolean | True if `tabIndex >= 0` or it is a non-read-only `<input>`. |
| `focusNextTab` | `focusNextTab(element)` → Element \| null | Focuses and returns the next `isTabable` element found by `traverseForward`. |
| `focusPrevTab` | `focusPrevTab(element)` → Element \| null | Focuses and returns the previous `isTabable` element found by `traverseBackward`. |
| `tabToNextOnEnter` | `tabToNextOnEnter(inputElement, onNextItemFn?)` | Sets `inputElement.onkeyup`: on an unmodified Enter, moves focus with `focusNextTab` and awaits `onNextItemFn(nextFocused, inputElement)`. |
| `keyboardClick` | `keyboardClick(element)` → element | Ensures `tabIndex = 0` (if unset/negative), adds class `KeyClick`, and sets `onkeyup` so unmodified Space/Enter call `element.click()`. |
| `removeKeyboardClick` | `removeKeyboardClick(element)` → element | Clears `tabIndex`, removes `KeyClick`, clears `onkeyup`. |
| `rightClick` | `rightClick(element)` → element | Sets `oncontextmenu` to call `element.click()` and suppress the menu; modified right clicks keep the default menu (`badClick(ev, true)`). |
| `removeRightClick` | `removeRightClick(element)` → element | Clears `element.oncontextmenu` and returns `element`. |
| `isPureClick` | `isPureClick(ev)` → boolean | True when `ev` is falsy or no Shift/Alt/Ctrl/Meta key is held. |
| `badClick` | `badClick(ev, keepProp?)` → boolean | Unless `keepProp`, calls `preventDefault()` and `stopPropagation()`. Returns true if Shift/Alt/Ctrl/Meta is held (the caller then ignores the click); false for a falsy `ev`. Used by nearly every click handler. |
| `insertAtCursor` | `insertAtCursor(myField, myValue, updateSelection?)` → number | Replaces the current selection of an `<input>`/`<textarea>` with `myValue`. If there is no selection start, uses `myField.NextLast` (remembered end of the previous insertion). With `updateSelection` selects the inserted text; otherwise stores the new end in `NextLast`. Returns the end position. |
| `GetFunction` | `GetFunction(functionName, context = window)` → any | Resolves a dotted path (e.g. `"a.b.fn"`) starting at `context`. |
| `GetRgbColor` | `GetRgbColor(colorKeyword)` → string | Returns the computed `rgb(...)` string of any CSS colour by briefly appending the hidden `window.DivGetRgbColor` div to `<body>`. |
| `RankScoreMatch` | `RankScoreMatch(searchFor, ...inTexts)` → number | Search score: for each occurrence of `searchFor` in text `i` at position `p`, adds `1/(1+0.5i) * 1/(1+0.5p)`. 0 means no match. Case-sensitive (callers normalise casing). |

#### Functions: HTML safety, validation and attributes

| Function | Signature | Description |
|---|---|---|
| `makeHtmlAttributeSafe` | `makeHtmlAttributeSafe(s, preserveCR?)` → string | Escapes `& ' " < >` to entities and replaces line breaks with `&#13;` (if `preserveCR`) or `\n`. |
| `makeHtmlSafe` | `makeHtmlSafe(text)` → string | Sets `innerText` on a temporary `SysWeaver-Temp` element and returns its `innerHTML`. |
| `isValidEmail` | `isValidEmail(s)` → boolean | Loose check: no surrounding whitespace, exactly one `@` not at position 0, domain of 2–4 non-empty dot-separated parts without surrounding whitespace. |
| `isValidPhone` | `isValidPhone(s)` → boolean | True if `s` has no surrounding whitespace and at least 3 characters. |
| `getNumberAttribute` | `getNumberAttribute(element, attribute, defValue = 0)` → number | `parseFloat` of the attribute; returns `defValue` if missing, negative or `NaN`. |
| `getIntegerAttribute` | `getIntegerAttribute(element, attribute, defValue = 0)` → number | Same with `parseInt`. |
| `ArgAsHtml` | `ArgAsHtml(argValue, elementClass?, elementType?)` → function \| undefined | Wraps a `_T`/`ValueFormat.stringFormat` argument so it is rendered as HTML: returns a function `(number, fmt, index)` that builds an element (`elementType`, default `b` despite the doc comment saying `span`) with optional `class`, sets its `innerText` to `fmt ? fmt(v, index) : v` and returns its `outerHTML`. Returns `undefined` for an undefined argument. |

#### Functions: smooth `src` loading (images / iframes)

| Function | Signature | Description |
|---|---|---|
| `doHandleSrc` | `doHandleSrc(e)` → boolean | True if the node has `data-sw-fade`, `data-sw-fadein` or `data-sw-useevent` (used by `SysWeaverInit` to select elements for `fixElementWithSrc`). |
| `fixElementWithSrc` | `fixElementWithSrc(element, showError?, fadeIn?, fadeDelay?, fadeChannel?, fadeParent?)` → element \| null | Sets up hide-while-loading and fade-in once per element (marker: numeric `e.FadeIn`). Defaults come from attributes `data-sw-fadein` (150), `data-sw-fadedelay` (0), `data-sw-fadechannel` (`"name,delayMs"`, delay default 100), `data-sw-fadeparent` (0 = animate the element itself, n = the n-th ancestor). Images get `draggable=false`. Iframes with `data-sw-useevent` wait for a window `message` with `Type === "PageLoaded"` instead of `load`. Registers the element in `window.FadeChannels` for staggering. Stores `FadeIn`, `FadeDelay`, `FadeInCount`, `FadeStyle`, `FadeChannel`, `FadeChannelIndex`, `StopListen` and `SrcObs` (a `MutationObserver` on `src` that hides the element and restarts listening). If the source has already loaded (complete image, or iframe `readyState === "complete"`) it reveals immediately or via `InternalSrcElementOnLoad` when there is a delay; otherwise it listens, or hides the element (`visibility:hidden; opacity:0`) when `src` is empty. |
| `msgHandler` (inner of `fixElementWithSrc`) | `msgHandler(ev)` | Window `message` listener: on `ev.data.Type === "PageLoaded"` calls `InternalSrcElementOnLoad({target: e})`. |
| `startListen` (inner of `fixElementWithSrc`) | `startListen()` | Adds `msgHandler` (useevent iframes) or a `load` listener, plus an `error` listener when `showError`. |
| `e.StopListen` (assigned by `fixElementWithSrc`) | `StopListen()` | Cancels a pending fade animation frame (`e.AnimReq`) and removes the message, `error` and `load` listeners. |
| `removeFadeChannel` | `removeFadeChannel(e)` | Removes the element from its fade channel; deletes the channel when its item count reaches 0 and `window.FadeChannels` when no channels remain. |
| `cleanElementWithSrc` | `cleanElementWithSrc(e)` → e | Undoes `fixElementWithSrc` state: calls `StopListen`, deletes `FadeIn`/`FadeDelay`/`FadeStyle`, leaves the fade channel and hides the element. Used for removed nodes by the `SysWeaverInit` mutation observer. The `src` observer itself is not disconnected. |
| `InternalSrcElementOnEvent` | `InternalSrcElementOnEvent(ev)` | Empty function. |
| `InternalSrcElementOnLoad` | `InternalSrcElementOnLoad(ev)` | Load handler (`ev.target` = element): stops listening and increments `FadeInCount`. With a fade channel, the first element sets the channel start time and releases delayed elements; other elements wait in `Delayed` until the first has started, then add `Delay * FadeChannelIndex` minus the time elapsed. Then fades opacity from 0 to 1 over `FadeIn` ms after the delay with `requestAnimationFrame` (`e.AnimReq`), clearing inline `opacity`/`visibility` at the end. With `FadeIn` 0 it just clears them. |
| `smooth` (inner of `InternalSrcElementOnLoad`) | `smooth(t)` → number | Smootherstep `t³(t(6t − 15) + 10)`. |
| `anim` (inner of `InternalSrcElementOnLoad`) | `anim(t)` | Animation frame callback that applies `smooth` to the fade style's opacity. |
| `InternalSrcElementOnError` | `InternalSrcElementOnError(ev)` | Error handler: stops listening and makes the element visible (clears inline `opacity`/`visibility` on the element itself), so the browser's broken-image rendering shows. |
| `createElementWithSrc` | `createElementWithSrc(tagName, showError?, fadeIn?)` → element | `fixElementWithSrc(document.createElement(tagName), showError, fadeIn)`. |
| `createIFrame` | `createIFrame(fadeIn?)` → HTMLIFrameElement | `createElementWithSrc("iframe", false, fadeIn)`. |
| `createImg` | `createImg(showError?, fadeIn?)` → HTMLImageElement | `createElementWithSrc("img", showError, fadeIn)`. |
| `SetImageSource` | `SetImageSource(img, src?, fallback?, flushCache?, onLoaded?, reloadOnMessage?)` | Loads `src` (default: current `img.src`) into `img` with opacity 0 until done (restoring the previous opacity, or 1). With `flushCache`, first runs `FlushCacheSync` and then calls itself without flushing. On the first error switches to `fallback` if given. `onLoaded(true)` on success with the primary URL, `false` otherwise. Sets/clears `img.onload` and `img.onerror`. With `reloadOnMessage`, registers once (`img.MsgHandler`) an InterOp listener (inner `handler`) and `SessionManager.AddServerEvent(reloadOnMessage)`. |
| `handler` (inner of `SetImageSource`) | async `handler(ev)` | On an InterOp message whose `Type` equals `reloadOnMessage`: if `img` is still in the document, awaits `FlushCache(src)` (shared via `msg.Waiter` among images) and reloads; otherwise unregisters itself and calls `RemoveServerEvent`. |

#### Functions: dynamic includes and geolocation

| Function | Signature | Description |
|---|---|---|
| `includeJs` | `includeJs(current?, file, noThrow?)` → Promise<{element, error, errorEv, loaded}> | Resolves `file` against the folder of `current` (default `ValueFormat.Current`, i.e. `common.js`) unless it starts with `http://`/`https://`. De-duplicated through `window.IncMap`: a repeat call polls every 10 ms (inner `ResCaches`) until the first load is no longer `Pending`, then resolves with the stored entry (or rejects if it has an `error` and not `noThrow`). Otherwise appends `<script async src=file>` to `<head>`; on `load` resolves a copy of the entry with `loaded: true`; on `error` removes the script, stores a translated `error` text and `errorEv` in the map entry, and resolves (`noThrow`) or rejects with that map entry (`loaded: false`). Synchronous exceptions reject with `{element: null, error, errorEv}`. |
| `ResCaches` (inner of `includeJs` / `includeCss`) | `ResCaches()` | Polls the cached `IncMap` entry every 10 ms until not `Pending`, then resolves or rejects. |
| `includeCss` | `includeCss(current?, file, noThrow?)` → Promise<{element, error, errorEv, loaded}> | Same as `includeJs` but appends `<link rel="stylesheet" href=file>`. Used by `applyTheme` for `theme@css` and by `SysWeaverInit` for URL `css=`. |
| `getDevicePosition` | async `getDevicePosition(lowPrecision?, timeout?, maximumAge?)` → GeolocationPosition | Promise wrapper of `navigator.geolocation.getCurrentPosition` with `enableHighAccuracy = !lowPrecision`, `timeout` (non-positive → `Infinity`) and `maximumAge` (non-positive → 0). Throws an `Error` on failure. |

#### Functions: async utilities

| Function | Signature | Description |
|---|---|---|
| `waitEvent` | `waitEvent(element, eventName, fn?, timeout?)` → Promise<Event \| null> | Adds a one-shot listener (inner `end`), then calls `fn(element)`; resolves with the event, or `null` after `timeout` ms (if positive). |
| `end` (inner of `waitEvent` / `waitEvent2` / `waitFor`) | `end(ev)` | In `waitEvent`/`waitEvent2`: clears the timeout, removes the listener(s) and resolves. In `waitFor`: dispatches `SysWeaverWaitCompleted` on the private element. |
| `waitEvent2` | `waitEvent2(element, eventName1, eventName2, fn?, timeout?)` → Promise<Event \| null> | Same as `waitEvent` for whichever of two events fires first. |
| `waitFor` | async `waitFor(fn)` | Calls async `fn(end)` and resolves once `end()` is called (implemented with `waitEvent` on a private `SysWeaver-Wait` element). |
| `setMediaSrcAndWait` | `setMediaSrcAndWait(mediaElement, url, onResult?)` → Promise | Sets `mediaElement.src = url` and resolves on `canplaythrough` or `error`, calling `onResult(success, mediaElement, url)` first. |
| `delayOneFrame` | `delayOneFrame()` → Promise | Resolves on the next `requestAnimationFrame`. |
| `delay` | `delay(msToWait)` → Promise | Resolves after `setTimeout(msToWait)`. |
| `delayWithAbort` | `delayWithAbort(msToWait, abortHandler)` → Promise<boolean> | Resolves `false` after the delay, or `true` early when `abortHandler.raise()` is called; removes its listener either way. |

#### class `AbortHandler`

Simple abort signal for `delayWithAbort`, created with `new AbortHandler()`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| constructor | constructor | `new AbortHandler()` | Creates `this.Event` (`Event("AbortHandlerEvent")`) and a private `this.Element` (`<div>`) used as event target. |
| `raise` | method | `raise()` | Dispatches the abort event to all listeners. |
| `addListener` | method | `addListener(fn)` | Adds `fn` for `AbortHandlerEvent`. |
| `removeListener` | method | `removeListener(fn)` | Removes `fn`. |

#### Functions: navigation, `postMessage` and URLs

| Function | Signature | Description |
|---|---|---|
| `ReloadAll` | `ReloadAll(flushCache?, clearHash?)` | With `clearHash`, `history.replaceState(null, null, ' ')`; then `location.reload(true)` (if `flushCache`) or `location.reload()`. |
| `PostAll` | `PostAll(name, data?, targetOrigin = "*")` | Sets `data.Type = name` (creates `{Type}` if no data) and `postMessage`s it to `window.opener ?? window.top`; errors are swallowed. |
| `PostTop` | `PostTop(name, data?, targetOrigin = "*")` | Same, to `window.top`. |
| `PostParent` | `PostParent(name, data?, targetOrigin = "*")` | Same, to `window.parent`, or `window.opener` when the page is not framed. |
| `Open` | `Open(l, target?)` | For `target === "_self"` with a relative or same-origin URL: posts `IframeNavigating {Url: absolute}` to the top window (so an app shell can navigate) and after 100 ms calls `window.open(appendTheme(l), "_self")` with a "Parent didn't respond in time!" warning. Otherwise `window.open(appendTheme(l), target ?? "_blank")`. |
| `EditOrOpenUrl` | async `EditOrOpenUrl(read, target?, save?, remove?)` | Calls `sendRequest("/Api/GetExtensionEditor", {Read, Save, Delete})` with absolute URLs. If no editor, or the editor URL equals the viewer from `/Api/GetExtensionViewer`, calls `Open(read, target)`; otherwise opens the editor (`_blank` if absolute, else `"/" + url` in `_self`). |
| `EditUrl` | async `EditUrl(read, target?, save?, remove?)` | Like `EditOrOpenUrl` without the viewer comparison. |
| `ViewUrl` | async `ViewUrl(read, target?)` | `sendRequest("/Api/GetExtensionViewer", absoluteRead)`; opens the returned viewer (`_blank` if absolute, else `"/" + url` in `_self`), or `Open(read, target)` when none. |
| `IsSameOrigin` | `IsSameOrigin(url)` → boolean | Compares `new URL(url).origin` with `location.origin` (throws for a relative `url`). |
| `IsAbsolutePath` | `IsAbsolutePath(url)` → boolean | True if `url` starts with `http://` or `https://`. |
| `GetAbsolutePath` | `GetAbsolutePath(url, current?)` → string \| null | `null` for a falsy url; absolute URLs unchanged; otherwise resolved against `current` or `location.href`. |
| `onLink` | `onLink(ev, url, target, useEdit?, useView?)` | Click handler used by `ValueFormat.createLink`. A modified click (per `badClick`) copies `url` with `ValueFormat.copyToClipboardInfo`; otherwise `ViewUrl`, `EditUrl` or `Open`. |
| `fixLinks` | `fixLinks()` | Replaces every `<a>` in the document with a `SysWeaver-Link` from `ValueFormat.createLink(href, text, target ?? "_self", title)`; an `<a>` whose first child is an `<img>` becomes an image link; other child elements log "Can't fix a tag!". |

#### Functions: cache flushing

| Function | Signature | Description |
|---|---|---|
| `FlushCache` | `FlushCache(url)` → Promise | Sends an `XMLHttpRequest` GET with `Cache-Control: no-cache, no-store, must-revalidate, max-age=0`, `Pragma: no-cache`, `Expires: 0` and a 1 ms timeout, to make the browser revalidate its cached copy. Resolves (via `waitFor`) when the request times out (`x.ontimeout`), reaches `readyState >= 2` (`x.onreadystatechange`) or throws. |
| `FlushCacheSync` | `FlushCacheSync(url, whenDone?)` | Same request without a Promise; calls `whenDone()` once at the same points. |

#### Functions: toasts

| Function | Signature | Description |
|---|---|---|
| `Message` | `Message(text, duration?, className, noCopyOnClick?)` | Logs `text` and shows it in a single reused `<SysWeaver-Fail>` element appended to `<body>` (`body.FailElement`), with `class = className` (`MsgError` / `MsgInfo`). `duration` defaults to 5000 ms; `true` means 24 h. Each call restarts the timer (`body.FailTimer`). A click (`fe.onclick`, unmodified) removes it and, unless `noCopyOnClick`, copies the text with `ValueFormat.copyToClipboard`; the tooltip says which. |
| `Del` (inner of `Message`) | `Del()` | Removes the toast element and clears `FailElement` / `FailTimer`. |
| `Fail` | `Fail(text, duration?, noCopyOnClick?)` | `Message(text, duration, "MsgError", noCopyOnClick)`. |
| `Info` | `Info(text, duration?, noCopyOnClick?)` | `Message(text, duration, "MsgInfo", noCopyOnClick)`. |

#### class `ColorIcon`

A monochrome (or full-colour) icon element `<icon-WxH>` whose image is a CSS mask and whose colour is `background-color` from a colour class. Created with `new ColorIcon(...)`; the element is `icon.Element`. The first icon of each size inserts its CSS rules into a `<style id="ColorIcon">` in `<head>`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `GenCss` | static field | `Map<string, true>` | Keys of sizes (`"WxH"`) and size/colour pairs (`"WxH_name"`) whose CSS has been generated. |
| `GetStyleSheet` | static method | `GetStyleSheet()` → CSSStyleSheet | Finds or creates `<style id="ColorIcon">` in `<head>` and returns its sheet. |
| `SetImageClass` | static method | `SetImageClass(element, imageClass)` | Falsy `imageClass` clears mask/background and hides the element. A value containing `.` is a URL: with a trailing `*` it becomes `background-image` with transparent background colour (full colour); otherwise inline `mask-image`. Any other value is added as a CSS class (expected to define `mask-image`). |
| `RemoveImageClass` | static method | `RemoveImageClass(element, imageClass)` | Clears inline mask/background styles and removes `imageClass` as a class if it starts with a letter (`isLetter`). |
| constructor | constructor | `new ColorIcon(imageClass, colorClass, width = 16, height = 16, title?, onClick?, buttonClass = "IconButton", disabledClass = "IconDisabled", captureContextMenu?)` | On first use of a size inserts rules for `icon-WxH` (size `calc(var(--ThemeIconSize)*Npx)`, mask settings), `.IconButton` hover/focus/active lift (distance 3 % of the smaller side, clamped 0.2–8 px), `.IconDisabled`, and colour classes `IconColorThemeBackground`, `IconColorThemeMain`, `IconColorThemeAcc1`, `IconColorThemeAcc2` (`var(--Theme...)`). Creates the element, applies image and colour classes and title. With `onClick`: adds `buttonClass`, `keyboardClick`, and an async `onclick` that ignores modified clicks, disabled state and the working state (`OldImage`); with `captureContextMenu` a right click (`e.oncontextmenu`) triggers the same handler. |
| `setTitleAttr` | method | `setTitleAttr(tit)` | Sets or removes the element's `title` attribute. |
| `SetTitle` | method | `SetTitle(title)` → this | Stores `Title`; updates the attribute unless the icon is disabled. |
| `SetEnabled` | method | `SetEnabled(enabled)` → this | Toggles `DisabledStyle` class; when disabled clears the title and `tabindex`, when enabled restores title and (`onclick` and `TabStop`) `tabindex=0`. |
| `AddIconColor` | static method | `AddIconColor(name, width, height, col)` | Once per size/name, inserts `icon-WxH.name {background-color: col}`. |
| `AddColor` | method | `AddColor(name, col)` | `AddIconColor` for this icon's size. |
| `ChangeImage` | method | `ChangeImage(imageClass)` → this | Replaces the image (via `RemoveImageClass`/`SetImageClass`); while working, the new image becomes the one restored by `StopWorking`. |
| `ChangeColor` | method | `ChangeColor(colorClass)` → this | Swaps the colour class. |
| `StartWorking` | method | `StartWorking(iconName = "IconWorking", changeAfterMs = 250)` | Marks the icon as working (`OldImage`, which also blocks clicks) and after `changeAfterMs` swaps the image to `iconName` (`WorkImage`, timer `WorkTimer`). No-op if already working. |
| `StopWorking` | method | `StopWorking()` | Cancels the timer, removes the working image and restores `OldImage`. |
| `Element`, `ImageElement` | field | HTMLElement | The `<icon-WxH>` element (both point to it). |
| `ImageClass`, `ColorClass`, `Title`, `Width`, `Height`, `DisabledStyle` | field | | Current image, colour class, title, size and the disabled class name. |
| `IsEnabled` (default true), `TabStop` (default true) | field | boolean | Enabled state; whether enabling restores `tabindex`. |
| `OldImage`, `WorkImage`, `WorkTimer` | field | | Working-state bookkeeping set by `StartWorking`. |

#### class `ButtonStyle`

Plain options object for `Button`; `Button.DefaultStyle` is one instance.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `ButtonElement` | field | `"Sysweaver-Button"` | Tag name of the button element. |
| `ButtonStyle` | field | `null` | Optional extra class added to the button. |
| `MinPressed` | field | `20` | Minimum ms the `Pressed` class stays after a click. |
| `IconWidth`, `IconHeight` | field | `24`, `24` | Icon size passed to `ColorIcon`. |
| `IconColor` | field | `"IconColorThemeBackground"` | Icon colour class. |

#### class `Button`

A text button with optional icon: `<Sysweaver-Button><Sysweaver-ButtonText>text</…>[<icon-WxH>]</…>`. Created with `new Button(...)`; append `button.Element`. `e.Button` points back to the instance.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `CreateRow` | static method | `CreateRow()` → HTMLElement | Returns a new `<SysWeaver-CenterBlock>` container for a row of buttons. |
| `DefaultStyle` | static field | `ButtonStyle` | Used when `style` is falsy. |
| constructor | constructor | `new Button(style?, text, title, imageClass?, enabled, onclick?)` | Builds the element and an async click handler (`OnClick`, inner `clickFn`): ignores modified clicks, adds `Pressed`, disables `onclick` while awaiting `onclick(button)`, waits `MinPressed` ms, re-enables if still enabled, removes `Pressed`, then calls the exit function the callback may return. Exceptions from the callback are swallowed. `e.onmousedown` (left button) / `e.onmouseover` (while held) add `Pressed`, `e.onmouseout` removes it. Creates a `ColorIcon` when `imageClass` is given. Calls `Enable()` or adds class `Disabled`. |
| `ChangeImage` | method | `ChangeImage(imageClass)` | Forwards to the icon. |
| `ChangeImageColor` | method | `ChangeImageColor(imageColorClass)` | `ColorIcon.ChangeColor` on the icon. |
| `ChangeText` | method | `ChangeText(newText)` | Sets the text element's `textContent`. |
| `ChangeTitle` | method | `ChangeTitle(newTitle)` | Updates `Title`, the element title and the icon title. |
| `SetEnabled` | method | `SetEnabled(enable)` | `Enable()` or `Disable()`. |
| `Enable` | method | `Enable()` | Sets `tabIndex=0`, title, `onclick = OnClick`, an `e.onkeyup` that runs it on Enter, and removes `Disabled`. |
| `Disable` | method | `Disable()` | Removes `tabindex`, title, `onclick`, `onkeyup`, and adds `Disabled`. |
| `StartWorking` / `StopWorking` | method | `StartWorking(iconName?, changeAfterMs?)` / `StopWorking()` | Forward to the icon's working state. |
| `Element`, `Title`, `IsEnabled`, `OnClick`, `UserClick`, `Text`, `Icon` | field | | The button element, title, enabled state, internal click handler, user callback (may be replaced), text element and optional `ColorIcon`. |

#### class `ValueFormat`

Static-only utility class (never instantiated) holding the value formatting / display helpers used by tables, property views and forms: number, currency, size and time-span formatting, a `{n}` string templating engine, clipboard helpers, and a family of `update*` "cell formatters" that render a typed value into a DOM element (`el`) with title (tool tip), click-to-copy and flash animation. The cell formatters all share the signature `(el, value, formats, nextValue, flash, type, onRefresh)`, where `formats` is the parsed format array (`formats[0]` = format kind, `formats[1..]` = kind-specific options), `nextValue` is an optional second value (e.g. the next column) that can be referenced as `{1}` in templates, `flash` is a CSS class name re-applied to animate a change, `type` is the .NET type name of the value and `onRefresh` an optional callback used by interactive formatters to request a re-render. They are dispatched through `ValueFormat.update` via the `typeFormatter` / `formatFormatter` maps.

##### General text and number helpers

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Current` | static field | `string` | URL of the script that defined the class (`document.currentScript.src`, or `"."`), captured at load time. |
| `setElementText` | static method | `(element, text)` | Sets `element.textContent` to `text` (coerced to string) but preserves leading/trailing spaces by converting them to `&nbsp;` entities in `innerHTML`; an all-space string becomes all `&nbsp;`. |
| `AddNonNullLine` | static method | `(to, header, value)` → `string` | Appends `header: value` as a new line to `to` (newline-separated); skips `null`/`undefined`/empty-string values and returns `to` unchanged. Value is trimmed; when it contains line breaks the header is followed by `":\n"` instead of `": "`. `header` may be falsy (value only). |
| `removeCamelCase` | static method | `(str, space = ' ')` → `string` | Splits a CamelCase identifier into words: each upper-case char following a non-upper char is lower-cased and preceded by `space` (e.g. `"MaxValue"` → `"Max value"`). |
| `toString` | static field (function) | `(value, decimals, thousandSeparator = ' ', decimalSeparator = '.')` → `string` | Formats a number with thousands grouping (every 3 integer digits) and exactly `decimals` fraction digits (rounded, zero-padded). Negative `decimals` means "show `-decimals` decimals only if the value is not an integer". `0`/falsy decimals gives an integer string without separator. |
| `toAmount` | static method | `(currencyInfo, amount, options = CurrencyFormatOptions.Default, decimalSeparatorOverride, thousandSeparatorOverride)` → `string` | Formats a money amount using an `IsoCurrency` record (`DecimalSeparator`, `ThousandSeparator`, `DecimalNumbers`, `UnderUnits`, `Iso4217`, `SymbolPrefix`, `SymbolSuffix`) and `CurrencyFormatOptions` flags: `ForceRounding` (round to whole units), `AutomaticRounding` (omit fraction when it is zero), `ApplyThousandsSeparator`, and the `Symbol` style bits (`IsoPrefix` → `"SEK 10.00"`, `IsoSuffix` → `"10.00 SEK"`, `Symbol` → prefix/suffix symbol). |
| `keyValueSplit` | static field (function) | `(text, split)` → `[key, value]` | Splits `text` at the first occurrence of `split`, trimming both halves; returns `[text, ""]` if `split` is not found. |
| `countSuffix` | static method | `(count, singularSuffix, pluralSuffix)` → `string` | Spells out counts 0–9 with translated words (`_TF("zero")` … `_TF("nine")`) followed by the singular suffix for 1 and plural suffix otherwise; other counts give `"" + count + pluralSuffix`. |
| `countPrefix` | (commented out) | `(singularPrefix, pluralPrefix, count)` | Disabled code inside a block comment (prefix variant of `countSuffix`); not part of the runtime API. |
| `FormatMaps` | static field | `Map<char, (val, index) => string>` | Modifier functions for `stringFormatArgs` placeholders: `'_'` lower-cases, `'^'` upper-cases, `'$'` lower-cases unless the placeholder is at position 0 of the template. |
| `stringFormatArgs` | static field (function) | `(format, args)` → `string \| undefined` | Replaces `{n}`, `{_n}`, `{^n}`, `{$n}` in `format` with `args[n]` (with the `FormatMaps` modifier applied). Placeholders whose argument is `undefined` are left as-is. If an argument is a function it is called as `val(number, modifierFn, index)` and its result is used verbatim (modifier not applied). Returns `undefined` for a falsy `format`. |
| `stringFormat` | static field (function) | `(format, ...args)` → `string` | Variadic wrapper around `stringFormatArgs`. |
| `formatSuffix` | static method | `(value, suffix, emptySuffix, k = 1024)` → `string` | Scales `value` by powers of `k` and appends a unit prefix: below `5*k` → integer + `' ' + emptySuffix`; otherwise one decimal with `' k'`, `' M'`, `' G'`, `' T'` or `' E'` + `suffix`. Negative values are prefixed with `"- "`. Used for byte sizes / speeds. |
| `formatTimeSpanMs` | static method | `(value, blankIfNeg)` → `string` | Formats a duration in milliseconds as a translated human string picking one unit: ms (<100 ms), seconds (1 decimal under 10 s, integer under 600 s), minutes (<600 min), hours (<72 h), days (<3 years), else years. Negative values get a `"-"` prefix, or if `blankIfNeg` is set return `blankIfNeg` (when a string) or `""`. |
| `formatTimeSpan` | static method | `(value, zeroAsThis)` → `string` | Formats a .NET `TimeSpan` string (`[-][d.]hh:mm:ss[.fffffff]`) as a single translated unit: years (≥3·365 days), days (≥3), hours (≥10), minutes (≥10), seconds (≥10, 1 decimal), ms (≥10, 1 decimal), else µs. Empty input gives `"-"`; a zero span gives `zeroAsThis` or `"0"`; negatives are prefixed `"- "`. A value without `:` is treated as a number of seconds and split into hours, minutes and seconds. |

##### DOM update primitives and clipboard

| Member | Kind | Signature | Description |
|---|---|---|---|
| `TextNode` | static field | `HTMLTableCellElement` | A pre-created `<td>` element (template node for text cells). |
| `updateText` | static field (function) | `(el, text, title, flash)` → `boolean` | Sets trimmed `text` as `el.textContent` (null/undefined → `''`) and `title` as `el.title` if truthy and changed. Returns `false` (no change) if the text is equal and `el` has no child elements. When changed and `flash` is a class name, removes and re-adds that class on the next tick and removes it again after 1050 ms (CSS flash animation). |
| `updateHtml` | static field (function) | `(el, html, title, flash)` → `boolean` | Same as `updateText` but compares and sets `el.innerHTML` (no trimming). |
| `updateFormat` | static field (function) | `(el, formatName)` → `boolean` | Tracks which formatter last rendered `el` in the expando `el.Format`. First call just stores it and returns `true`; same name returns `false`; a different name resets the element (clears `el.Updater` interval, inline `color`/`backgroundColor`, sets text `"error"`, removes classes `Right`, `Center`, `Null`, `False`, `Monospaced`, `Text`, `Toggle`, clears `onclick` and calls `removeKeyboardClick`) and returns `true`. Formatters use the `true` result to add their one-time classes. |
| `readFromClipboard` | async static method | `()` → `Promise<string \| null>` | Reads text via `navigator.clipboard.readText()`; resolves `null` if the API is missing or fails (warning logged). |
| `copyToClipboard` | async static method | `(v)` → `Promise<boolean>` | Writes `v` to the clipboard; logs the result to the console and resolves `true`/`false`. |
| `copyToClipboardInfo` | async static method | `(v, genericText)` → `Promise<boolean>` | Like `copyToClipboard` but also shows a translated toast: `Info(...)` on success, `Fail(...)` with the exception message on error. With `genericText` truthy the message does not include the copied text. |
| `copyOnClick` | static method | `(el, value, addKeyboard, addTitleToElement)` → `string` | Makes `el` click-to-copy: adds class `MouseClick` and sets `el.onclick` (ignoring `badClick` events) to copy `value` (or, if `value` is null/undefined, the element's current `textContent`) via `copyToClipboardInfo`. With `addKeyboard` also calls `keyboardClick(el)` and sets `el.oncopy`. Returns a translated tool tip ("Click to copy ..."), which is also appended to `el.title` when `addTitleToElement` is set. Inner async function `Copy()` performs the copy. |
| `joinNonEmpty` | static method | `(delim = '\n', ...args)` → `string` | Joins the truthy `args` with `delim`. |

##### Cell formatters: color, default, type, number, URL and image

| Member | Kind | Signature | Description |
|---|---|---|---|
| `updateWebColor` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` | Format `"WebColor"`: renders a CSS color string. Text is `stringFormat(formats[1] ?? "{0}", value, nextValue)` and is also used as `el.style.backgroundColor`; resolves the color via `GetRgbColor` and adds class `False` when the luminance (0.2989R+0.5870G+0.1140B) is below 128 (dark background → light text). Title = optional `formats[2]` template plus Red/Green/Blue/Alpha component lines and the copy hint; click copies the color text. A falsy value clears the cell. |
| `updateDefault` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` | Format `"Default"` (plain text, class `Text`): text = `stringFormat(formats[1] ?? "{0}", value, nextValue)` (empty for `null`); title = `formats[2]` template (default `"Raw: {2}"` when the text differs from the raw value). `formats[3]` controls copy: `undefined`/truthy enables click-to-copy, a string is a template for the copied value, falsy disables it. |
| `updateType` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` | Renders a .NET type name as a link: if `formats` is empty it is filled with a URL format pointing to `../edit/type.html?q={2}` (target `_self`) and a translated tool tip, then delegates to `updateUrl`. |
| `updateNumber` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` | Format `"Number"` (classes `Right`, `Monospaced`): `formats[1]` = decimals (default -2; negative means `-decimals` for decimal types listed in `isDecimal`, 0 otherwise), `formats[2]` = text template (`{0}` formatted, `{1}` next value, `{2}` raw), `formats[3]` = title template (default `"Raw: {2}"`), `formats[4]` = copy enable (default on). On change sets class `False` for negative, `Null` for zero, neither for positive values. |
| `LinkNode` | static field | `HTMLElement` | Reusable `<SysWeaver-Link>` template element whose `outerHTML` is copied into URL cells. |
| `ImgNode` | static field | `HTMLImageElement` | Reusable `<img>` template element whose `outerHTML` is copied into image cells. |
| `getUrlTarget` | static method | `(url)` → `[url, target]` | Decodes a target prefix on a URL: `+` → `_blank`, `*` → `_self`, `^` → `_top`, `-` → `_parent` (prefix stripped); no prefix → `_blank`. Empty/falsy input gives `["", ""]`. |
| `setOnClickAttr` | static method | `(element, url, target)` | Sets an inline `onclick="onLink(event,'url','target')"` attribute (survives `outerHTML` serialization, unlike a property handler). |
| `createLink` | static method | `(url, text, target = "_self", title, isImage, useAttr, useEdit, useView)` → `HTMLElement` | Creates a focusable `<SysWeaver-Link>` that navigates via `onLink(ev, url, target, useEdit, useView)` — as an inline attribute when `useAttr`, else as an `a.onclick` property plus `keyboardClick`. `text` becomes the text, or with `isImage` the `src` of a child `<img>` (class `Image` on the link). |
| `updateUrl` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` | Format `"Url"`: `formats[1]` = text template (default `{0}`), `formats[2]` = URL template (default `{2}` = text; may carry a `getUrlTarget` prefix; resolved with `GetAbsolutePath`), `formats[3]` = title template (`{3}` = url; default "Click to open ..." when there is a URL, otherwise empty). With a URL it renders a `<SysWeaver-Link>` with an inline `onLink` handler via `updateHtml` and enables `keyboardClick` on the cell; when the URL resolves to nothing it shows the text as plain text and removes keyboard activation. Null/undefined clears the cell. |
| `updateImg` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` | Format `"Img"`: `formats[1]` = image src template, `formats[2]` = optional click URL template (with target prefix), `formats[3]` = title, `formats[4]`/`formats[5]` = max width/height as multiples of `--ThemeIconSize`, `formats[6]` = prefix prepended to relative values. A leading `-` / `+` on the src adds cell class `Left` / `Right` (`*` is just stripped). Clickable images get class `ImgLink` and an inline `onLink` handler; with no click URL the image is plain and the cell gets no keyboard activation. |

##### Date / time formatters

| Member | Kind | Signature | Description |
|---|---|---|---|
| `getTimeStampTitle` | static method | `(dd, onlyDate, prefix = "At: ", tab = "", fmt)` → `[title, text]` | Converts a `Date` / ISO string / epoch-ms into display text `"yyyy-MM-dd HH:mm:ss"` (date part only with `onlyDate`) and a multi-line tool tip (prefix + text, plus Local / Full / UTC / ISO representations unless `fmt === "local"`). `fmt`: `"utc"` = display the value as-is (UTC); `"local"` = value is already local; otherwise the value is shifted from UTC to local for display. `tab` is inserted after each tool tip newline. |
| `convertLocalDateToUTCDate` | static method | `(date)` → `Date` | Returns a new `Date` shifted by the timezone offset (so its ISO form shows the local wall-clock time). |
| `convertUTCDateToLocalDate` | static method | `(date)` → `Date` | Inverse shift of `convertLocalDateToUTCDate`. |
| `updateDateTimeLive` | static method | `(el, value, formats, nextValue, flash, type, onRefresh, noCopy, blankIfNeg, invert)` → `el` | Format `"Uptime"` (classes `Monospaced`, `Right`): shows the elapsed time since `value` (or until it, with `invert`) using `formatTimeSpanMs`, wrapped in template `formats[2]` (default `{0}`), and keeps it live with a self-rescheduling `setTimeout` stored in `el.Updater` (every 100 ms, or aligned to whole minutes once over 10 min). Updates stop once the element has been attached and is then detached (`isInDocument`). `"0001-01-01T00:00:00"` shows `-` ("Not set"); falsy shows "Now". Inner arrow function `update` performs each tick. |
| `updateDateTime` | static method | `(el, value, formats, nextValue, flash, type, onRefresh, onlyDate, noCopy)` → `el` | Renders a timestamp via `getTimeStampTitle` with `formats[0]` as `fmt` (`"utc"` / `"local"`); adds classes `ValueFormatDt`, `ValueFormatDt<fmt>`, `Monospaced`, `Right`. `"0001-01-01T00:00:00"` shows `-`. A falsy value shows the current time, refreshed every second by inner arrow function `update` (timeout in `el.Updater`, stops when detached). Click copies the raw value unless `noCopy`. |
| `updateTimeSpan` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` → `el` | Format `"Duration"` and type `System.TimeSpan` (classes `Monospaced`, `Right`): uses `formatTimeSpan(value, formats[4])`; `formats[1]` = text template, `formats[2]` = title (default `"Raw: {2}"`), `formats[3]` = copy (undefined → copy raw value, empty → no copy, string → template with `{0}` formatted, `{1}` next, `{2}` raw, `{3}` text). |

##### Text, JSON and Markdown formatters

| Member | Kind | Signature | Description |
|---|---|---|---|
| `jsonToHtml` | static method | `(jsonText)` → `string` | HTML-escapes JSON text and wraps tokens in `<span class="key">`, `string`, `number`, `boolean` or `null` spans for syntax highlighting. |
| `updateJson` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` → `el` | Format `"Json"` (class `Monospaced`): shows the value capped at `formats[1]` chars (min 4) or at the first line break (class `Capped` when truncated). Title = `formats[2]` (default `{1}` = pretty-printed JSON; `{0}` raw, `{2}` capped). `el.onclick` (ignored when text is selected in the cell, via `haveElementSelection`, or on `badClick`) opens a `PopUp` with a `<SysWeaver-Code class="SysWeaverCode">` showing highlighted, 4-space indented JSON; when `formats[3]` (copy template) is set a copy `ColorIcon` (`IconCopy`) is added to the popup buttons. |
| `SetMarkDown` | async static method | `(el, text)` → `Promise<void>` | Sets `el.innerHTML` to `await MarkDownToHTML(text)`. |
| `MarkDownToHTML` | async static method | `(text)` → `Promise<string>` | Lazily loads `marked.js` via `includeJs` and installs a custom renderer with `marked.use` (on every call): object-literal methods `link(l)` renders `<a target="_blank">` with an attribute-safe href (relative links get `appendTheme`), a default "Click to open ... in a new tab" title and class `Image` when wrapping an image; `image(l)` renders an image wrapped in a new-tab link (inside a link it returns `false` = default rendering); `html(l)` lets through only whitelisted raw tags (`hr`, `br`, `img`, `figure`, `figcaption`, `b`, `strong`, `i`, `em`, `mark`, `small`, `del`, `ins`, `sub`, `sup`, `blockquote`, `q`, `abbr`, `address`, `cite`, `bdo`) and strips all other raw HTML. Returns `marked.parse(text)`. |
| `isAnythingSelected` | static method | `(selection = window.getSelection())` → `boolean` | True if any range of the selection is non-empty. |
| `haveElementSelection` | static method | `(el)` → `boolean` | True if the current selection's anchor node is a direct child of `el` and something is selected (prevents opening pop-ups when the user is selecting text). |
| `updateMarkDown` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` → `el` | Format `"MD"`: capped display like `updateJson` (`formats[1]` cap, `formats[2]` title, default `{0}` = full value, `{1}` = capped). `el.onclick` opens a `PopUp` with a `<SysWeaver-MdText>` rendered by `MarkDownToHTML`, plus a copy icon when `formats[3]` is set; enables `keyboardClick`. |
| `updateLongText` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` → `el` | Format `"Text"`: same capping / title / copy scheme as `updateMarkDown`; `formats[4] === "True"` makes it monospaced (cell class `Monospaced`, popup element class `Mono`). `el.onclick` opens a `PopUp` with a `<SysWeaver-LongText>` showing the full text. |

##### Other cell formatters and dispatch

| Member | Kind | Signature | Description |
|---|---|---|---|
| `updateBoolean` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` → `el` | Shows translated `"☑ True"` / `"☐ False"`; on change sets class `Text` (true) or `False` (false). Click copies the value. |
| `formatByteSize` | static method | `(value)` → `string` | `formatSuffix(value, "b", "bytes", 1024)`, e.g. `"12.5 kb"`. |
| `updateByteSize` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` → `el` | Format `"ByteSize"` (classes `Right`, `Monospaced`): text = `formats[2]` template over `formatByteSize(value)` (`{0}` formatted, `{1}` next, `{2}` raw), title = `formats[3]` (default `"Raw: {2}"`); always click-to-copy the raw value. |
| `updateByteSpeed` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` → `el` | Format `"ByteSpeed"`: like `updateByteSize` with suffixes `"b/s"` / `"bytes/s"` (registers itself under the format name `"ByteSize"` in `updateFormat`). |
| `TagSpanNode` | static field | `HTMLSpanElement` | Reusable `<span>` template for tag rendering. |
| `updateTags` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` → `el` | Format `"Tags"`: splits a comma-separated value into `<span>` tags (empty string → a single `"🔑"` tag; `¤` inside a tag becomes `,`). Each tag is split at `:` with `keyValueSplit` (`{0}` = whole tag, `{1}` key, `{2}` value); `formats[1]` = text template (default `{1}`), `formats[2]` = title (default `{2}`), `formats[3]` = per-tag copy template (enables click-to-copy per span), `formats[4] === "true"` (case-insensitive) makes the whole cell copy the raw value. |
| `updatePerRowFormat` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` → `el` | Format `"PerRowFormat"`: the effective format comes from the row — `nextValue` is `"type|format|nextValue"` (type truncated at the first `,`) and the cell is re-dispatched through `ValueFormat.update`; without `nextValue` falls back to `updateDefault`. |
| `TempToggle` | static field | `HTMLElement` | Reusable `<SysWeaver-toggle>` template. |
| `updateToggle` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` → `el` | Format `"Toggle"`: `value` is `"bool[,arg,...]"` (`"true"`/`"1"` = checked); template args are `[bool, nextValue, ...rest]`. Renders a focusable `<SysWeaver-toggle>` with `"☑ " + formats[1]` (default "True") or `"☐ " + formats[2]` (default "False") and title `formats[3]` / `formats[4]` (default "Click to uncheck" / "Click to check"). `te.onclick` (re-entry guarded, class `Null` while busy) sends `getRequest(stringFormatArgs(formats[5], args), true)` and then calls `onRefresh()`. Cell gets class `Toggle`. |
| `TempAction`, `TempSpan`, `TempImage` | static fields | `HTMLElement` | Reusable `<SysWeaver-action>`, `<SysWeaver-actionText>` and `<img>` templates for `updateActions`. |
| `updateActions` | static method | `(el, value, formats, nextValue, flash, type, onRefresh)` → `el` | Format `"Actions"`: each `formats[1..]` entry is `"text|title|url|icon"` (templates over the comma-split value with `nextValue` inserted at index 1). Renders one `<SysWeaver-action>` per entry (optional 20 px `ColorIcon` + `<SysWeaver-actionText>`). Entries with a URL get a `te.onclick` (re-entry guarded, class `Null` while busy): `&url` → `Open(url, "_self")`, `@url` → `Open(url, "_blank")`, otherwise `getRequest(url, true)`; then `onRefresh()`. Entries without URL get class `Disabled`. Cell gets class `Toggle`. `null` clears the cell. |
| `isDecimal` | static field | `Map<string, true>` | .NET types that show decimals by default: `System.Single`, `System.Double`, `System.Decimal`. |
| `typeFormatter` | static field | `Map<string, Function>` | Default formatter per .NET type: integer and floating point types → `updateNumber`, `System.DateTime` → `updateDateTime`, `System.TimeSpan` → `updateTimeSpan`, `System.Boolean` → `updateBoolean`, `System.Type` → `updateType`. |
| `formatFormatter` | static field | `Map<string, Function>` | Formatter per explicit format name (`formats[0]`): `Number`, `Url`, `Img`, `Tags`, `PerRowFormat`, `ByteSize`, `ByteSpeed`, `Toggle`, `Actions`, `Duration` (→ `updateTimeSpan`), `Json`, `Text` (→ `updateLongText`), `MD` (→ `updateMarkDown`), `Uptime` (→ `updateDateTimeLive`), `WebColor`. |
| `update` | static field (function) | `(el, type, value, flash, format, nextValue, onRefresh)` → `el` | Main entry point: splits `format` on `;` into `formats`, picks the formatter from `formatFormatter[formats[0]]`, else `typeFormatter[type]`, else `updateDefault`; keeps exactly one `ValueFormat<formatName>` class on `el` (removing other `ValueFormat*` classes), then calls the formatter. |

#### class `FullScreen`

Static-only helper around the browser Fullscreen API (with webkit/moz/ms fallbacks).

| Member | Kind | Signature | Description |
|---|---|---|---|
| `IsFull` | static method | `()` → `boolean` | True if a fullscreen element exists (standard or webkit) or a `display-mode: standalone` / `fullscreen` media query matches (e.g. installed PWA). Only if those queries throw, falls back to comparing `screen` size with `window.outerWidth/outerHeight` (within 16 px). |
| `Enter` | async static method | `()` → `Promise<boolean>` | Requests fullscreen on `document.documentElement` using the first available vendor method; resolves `true` on success, else `false`. |
| `Exit` | async static method | `()` → `Promise<void>` | Calls `document.exitFullscreen()`, falling back to `document.webkitCancelFullScreen()` on failure (errors swallowed). |
| `Toggle` | async static method | `()` → `Promise<void>` | Exits fullscreen if `IsFull()`, else enters it. |

#### Functions — JSON, wake lock, binary data and HTTP requests

| Function | Signature | Description |
|---|---|---|
| `ToTypedJson` | `(data, optionalType)` → `string` | Serializes `data` to JSON making sure a `$type` property (the .NET polymorphic type discriminator) is the first key: uses `optionalType`, else the existing `data.$type`. Returns `"null"` for null, plain `JSON.stringify` when no type is known or `$type` is already first; otherwise copies the object with `$type` moved/added at the front. |
| `EnterNoSleep` | async `()` → `Promise<void>` | Prevents the device from sleeping by playing a tiny looping muted inline `<video>` (embedded WebM + MP4 data URIs, added through inner function `Add(type, dataURI)` which appends a `<source>`; a `timeupdate` listener keeps seeking back near 0). State `{ Video, Count }` is stored on `document.NoSleep`; reference counted — the video starts playing on the first call. |
| `IsSleepPrevented` | `()` → `boolean` | True if `document.NoSleep.Count > 0`. |
| `ExitNoSleep` | async `()` → `Promise<void>` | Decrements the `EnterNoSleep` reference count and pauses the video when it reaches zero. |
| `ConcatUint8Arrays` | `(arrayOfArrays)` → `Uint8Array` | Merges an array of `Uint8Array`s into one (returns an empty array for none and the single element as-is for one). |
| `ReadAllData` | async `(stream)` → `Promise<Uint8Array>` | Reads all chunks of an async-iterable stream and concatenates them with `ConcatUint8Arrays`. |
| `DeflateCompress` | async `(buffer)` → `Promise<Uint8Array>` | Compresses `buffer` with the browser `CompressionStream("deflate-raw")` (wrapping it in a `ReadableStream` whose `start(controller)` enqueues the buffer and closes). |
| `postData` | `(url, data)` → `boolean` | Fire-and-forget POST via `navigator.sendBeacon` (JSON `Blob` when `data` is given). Returns whether the beacon was queued. The body is `JSON.stringify(data)`. |
| `sendRequest` | async `(url, data, reload, errHandler, returnBlob, setupRequestFn, usePost)` → `Promise<object \| Blob \| null \| undefined>` | Sends `data` (serialized with `ToTypedJson`) to an API endpoint. Unless `usePost` (in debug builds `usePost` defaults to `true` via the `//DEBUG_BEGIN`/`//DEBUG_END` block), small payloads (< 16 KiB JSON) go as GET `url?<payload>`: URI-encoded JSON if under 128 chars, else `_d` + base64url of deflate-raw-compressed bytes (when ≥ 1/8 smaller) or `_u` + base64url of raw bytes, whichever is shorter, if under 4096 chars. Otherwise POSTs JSON (with `Content-Encoding: deflate` when compressed data is available). `reload` → `cache: "reload"`; `setupRequestFn(request)` may modify the `Request`. On non-200 status (or a redirect when `errHandler` is set) `await errHandler(status)` returning true retries; otherwise throws `Error("status - statusText[\n500 body]")`. A redirected response navigates the page (`Open(res.url, "_self")`) and returns `null` (a second redirect throws). Returns parsed JSON, or a `Blob` with `returnBlob`; `undefined` if decoding fails. |
| `getRequest` | async `(url, reload, returnBlob, setupRequestFn, returnFn, maxRetryCount = 10000)` → `Promise<any>` | GET request (`url` may be a string or a prepared `Request`; `setupRequestFn` only applies to strings). Throws on non-200 status (message includes the body for 500); a redirect navigates the page via `Open(res.url, "_self")` and returns `null`. Returns `await returnFn(response)` when given, else a `Blob` (`returnBlob`) or parsed JSON; `undefined` if reading fails. |

#### Globals — file extension tables

| Name | Kind | Description |
|---|---|---|
| `imageExtensions` | const (frozen object) | Lower-case image extensions → 1: `png`, `gif`, `jpg`, `jpeg`, `avif`, `webp`, `tiff`, `tif`, `svg`, `jfif`. |
| `videoExtensions` | const (frozen object) | `webm`, `mp4`, `ogg`, `mov`. |
| `audioExtensions` | const (frozen object) | `mp3`, `wav`, `aac`. |
| `programmingExtensions` | const (frozen object) | Source/text formats: `js`, `json`, `css`, `html`, `xml`, `cs`, `cpp`, `c`, `h`, `vbs`, `bat`, `vb`. |
| `compressExtensions` | const (frozen object) | Additional compressible text formats: `txt`, `log`, `cfg`, `svg`. |

#### Functions — files, text, scrolling, focus and loading overlay

| Function | Signature | Description |
|---|---|---|
| `getFileExtension` | `(path)` → `string` | Text after the last `.` (case preserved), or `""`. |
| `isImageExt`, `isAudioExt`, `isProgrammingExt`, `isVideoExt` | `(ext)` → `boolean` | Membership test of a (lower-case, dot-less) extension in the corresponding table. |
| `isCompressibleExt` | `(ext)` → `boolean` | True for `programmingExtensions` or `compressExtensions` members. |
| `isImageFile`, `isVideoFile` | `(path)` → `boolean` | Lower-cases the path's extension and tests it against `imageExtensions` / `videoExtensions`. |
| `isLetter` | `(c)` → `boolean` | True if the character has distinct upper/lower case; for other non-ASCII characters tests whether it is a valid JS identifier character by `eval`-ing `function <c>(){}`. ASCII non-cased chars return false. |
| `IsAtMaxScrollV` | `(element)` → `boolean` | True if `element` is scrolled to the bottom (within 1 px). |
| `StickToBottom` | `(element, stickByDefault = true, onBottom, onFree, onTop, instant)` | Makes a scroll container "stick" to the bottom (log/chat style): sets `element.onscroll` to detect when the user scrolls away (calls `onFree(element)`, removes class `Stick`) or back to the bottom within 4 px (calls `onBottom(element)`, adds class `Stick`), and calls `onTop(element)` when at the top. While stuck, a `ResizeObserver` and a subtree `MutationObserver` keep it scrolled to the end via inner function `stickIt(isMutating)` (smooth unless `instant`; scroll events it causes are ignored for one frame). |
| `PageLoaded` | `()` | Signals that the page finished loading: `PostParent("PageLoaded")` and `PostTop("LoaderRemoved", null, "*")`. |
| `BlockTab` | `(element)` | Traps keyboard focus inside `element` (for pop-ups). Pushes `[element, document.activeElement]` on the stack `window.TabBlockers`; on first use installs a capturing `focus` listener on `document.body` (inner arrow `tabBlockFn`, also stored as `window.TabBlockFn`) that, when focus moves outside the top-most blocker (or onto the blocker itself or a `<SysWeaver-TabBlock>` sentinel), puts focus back on the previously focused element inside it (or the blocker). A detached blocker is auto-removed with a warning. Pair with `UnblockTab`. |
| `UnblockTab` | `(element)` | Removes `element` from `window.TabBlockers`; when it was the last blocker removes the focus listener and deletes `window.TabBlockFn` / `window.TabBlockers`. If it was the top-most blocker, restores focus to the element that was focused when it was blocked (if still attached). |
| `AddLoading` | `(page = document.body, text, opaque, useIcon)` → `function(noEvents)` | Adds a `<SysWeaver-Loading>` overlay (class `Opaque` if `opaque`) that swallows mouse down/up and key events (`le.onmousedown`, `le.onmouseup`, `le.onkeydown`) and traps focus via `BlockTab`, with optional `<SysWeaver-LoadingText>` (class `Center` when there is no icon) and a 64 px `ColorIcon` (`useIcon`, default `IconWorking`; `""` = none). Returns a remover: it unblocks tabs, removes the overlay and, unless `noEvents`, calls `PageLoaded()`, posts `InterOp.Post("WindowLoaded")` and restores the scroll position saved in the history state (`ScrollX`/`ScrollY`) through inner function `tryScroll`, which re-applies `window.scrollTo` each frame (incrementing `window.ScrollCounter`) until the document height has been stable for 1.5 s or the user interacted (`window.DidInteract`). If the URL has `noloader`, nothing is added and `PageLoaded` itself is returned. |

#### class `ProgressBar`

A themed progress bar element. Create with `new ProgressBar()`, insert `.Element` into the DOM, then call `SetValue` / `SetPercentage`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `()` | Builds `<SysWeaver-ProgressBar>` containing `<SysWeaver-ProgressBarTextInside>`, `<SysWeaver-ProgressBarTextOutside>` and `<SysWeaver-ProgressBarFill>`. |
| `Element`, `Prog`, `TI`, `TO` | field | `HTMLElement` | The root element, the fill element, and the inside / outside text elements. |
| `SetValue` | method | `(current, max, text, title)` | Sets progress as `current / max` (0 when `max <= 0`). Default text is `"current / max"`; a supplied `text` / `title` is a template where `{0}` is kept for the percentage and `{1}` = current, `{2}` = max (default title `"current / max - {0}"`). Calls `SetPercentage`. |
| `SetPercentage` | method | `(value, text, title)` | Clamps `value` to 0–100, positions the fill (`left: -(100-value)%`) and sizes the inside/outside text elements; text and title default to `"xx.xx%"`, or are templates with `{0}` = percentage. Class `Active` goes on the inside text when ≥ 50 %, else on the outside text. |

#### class `Animator`

Static per-frame animation scheduler sharing a single `requestAnimationFrame` loop.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Add` | static method | `(func)` | Registers `func(elapsedMs)` to run every frame; `elapsedMs` starts at 0 on its first frame. Returning `true` removes it. Starts the loop (inner arrow `fn(timeStamp)`, stored in `HaveAnimation`) if not running; the loop stops itself when no functions remain. Exceptions from `func` are swallowed. |
| `Remove` | static method | `(func)` | Unregisters a function. |
| `Functions` | static field | `Map<Function, number>` | Registered functions → start timestamp (-1 until first frame). |
| `HaveAnimation` | static field | `Function \| null` | The active frame callback, or null/undefined when the loop is idle. |

#### class `PendingImages`

Tracks a set of image loads so callers can wait for all of them. Create with `new PendingImages()`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Count`, `InProgress`, `Fails` | field | `number` | Images started, currently loading, and failed. |
| `Start` | method | `(imgElement, src)` | Wraps `imgElement.onload` / `imgElement.onerror` (chaining previous handlers) to decrement `InProgress` (and increment `Fails` on error), increments counters, and sets `src` if given. |
| `WaitAll` | async method | `()` → `Promise<void>` | Polls every 10 ms (`delay(10)`) until `InProgress <= 0`. |

#### Functions — element geometry and tracking

| Function | Signature | Description |
|---|---|---|
| `IsAttached` | `(el)` → `boolean` | True if `el` has `document.body` as an ancestor (or is the body). |
| `GetAbsoluteRect` | `(el)` → `{found, left, top, width, height, right, bottom}` | Document-relative rectangle of `el`, measured against a hidden absolutely positioned `<div>` appended to the body on first use (cached in `GetAbsoluteRect.offsetBase`). `found` is undefined and all values 0 if it cannot be measured. |
| `TrackElement` | `(mouseEventOrElement, targetElement, margin)` | Keeps a `position: fixed` `targetElement` (e.g. a context menu or tooltip) anchored to an element (or to the click point within `MouseEvent.target`). The anchor corner is chosen by which half of the screen the start point is in. Inner arrow `updatePos` recomputes `top`/`left` (clamping inside the viewport when `margin >= 0`); it is driven by a `ResizeObserver` on the tracked element, body and target and by a document `scroll` listener (inner function `OnScrollFn`, throttled with `requestAnimationFrame`). Observers/listeners are removed once the target is detached. |

#### Functions — per-page history state

State persisted per page so it survives reload/back navigation: in a top-level window it lives in `history.state` (via `history.replaceState`); inside an iframe in `sessionStorage` under the key `window.location.href`. It is cached in `window.HistoryState`.

| Function | Signature | Description |
|---|---|---|
| `InternalGetHistoryState` | `()` → `object` | Returns (and caches in `window.HistoryState`) the state object, loading it from `history.state` or `sessionStorage` (JSON) or creating `{}`. |
| `ClearHistoryState` | `()` | Clears `window.HistoryState` and the stored state (`history.replaceState(null)` or `sessionStorage.removeItem`). |
| `ClearOtherHistoryState` | `(url)` | Removes the `sessionStorage` state of another (iframe) page URL. |
| `SetHistoryState` | `(key, value)` | Sets one key in the state and persists it. |
| `UpdateHistoryState` | `(onStateFn)` | Calls `onStateFn(state)`; persists the state if it returns true. |
| `GetHistoryState` | `(key, ifNotFoundValue)` → `any` | Reads one key, or returns `ifNotFoundValue` if missing. |
| `SetTemporaryInnerText` | `(element, text, duration, optionalNewText)` | Shows `text` in `element.textContent` for `duration` ms, then restores the previous text (or sets `optionalNewText`) unless the text was changed meanwhile or another temporary text superseded it (tracked with expandos `element.TempText` / `element.TempId`). |

#### class `Cache`

Static in-memory cache with expiry, keyed by `cacheName + "|" + JSON.stringify(cacheKey)`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `InternalCache` | static field | `Map<string, {Value, Exp}>` | Cache storage (internal). |
| `InternalPrune` | static field (set at runtime) | `boolean` | True while the prune timer is running. |
| `Get` | async static method | `(cacheName, cacheKey, lifeTime, fetchFn)` → `Promise<any>` | Returns the cached value if not expired, else `await fetchFn(cacheKey, cacheName)` and caches it for `lifeTime` ms. On first use starts inner function `Prune`, which every 5 s deletes expired entries and stops when the cache is empty. |
| `GetRequest` | async static method | `(url, lifeTime)` → `Promise<any>` | Cached `getRequest(url, false)` (cache name `"_Greq" + url`). |
| `SendRequest` | async static method | `(url, data, lifeTime)` → `Promise<any>` | Cached `sendRequest(url, data, false)` keyed on url and data (cache name `"_Sreq" + url`). |

#### Functions — media capture

| Function | Signature | Description |
|---|---|---|
| `CaptureVideoFrame` | async `(video, mime = "image/jpeg", quality = 80)` → `Promise<string>` | Draws the current frame of a `<video>` playing a `MediaStream` (`srcObject`, sized from the first video track's settings) onto a canvas, encodes it with `canvas.toBlob(mime, quality/100)` and returns it base64-encoded via `bufferToBase64`. Throws if encoding fails. |

#### Functions — page initialization

| Function | Signature | Description |
|---|---|---|
| `SysWeaverIgnoreUserChanges` | `()` | Sets `window.IgnoreUserChanged = true`, which makes the server-push reload/refresh handlers in `SysWeaverInit` do nothing (also triggered by URL param `ignoreuserchanges=true`). |
| `SysWeaverInit` | async `()` → `Promise<void>` | Per-page bootstrap, executed automatically at the end of `common.js` (after `SessionManager.Create()`). Runs once (guarded by `window.HaveSysWeaverInit`). See the inner-function table and the globals/events below for details. |

**Immediately executed code at end of file:** `SessionManager.Create();` followed by `SysWeaverInit();`.

`SysWeaverInit` performs, in order:

1. **Theme / CSS:** URL params `css` (extra stylesheet loaded with `includeCss`), `settheme` (persisted to `localStorage["SysWeaver.Theme"]`), `usetheme` (sets `window.UseTheme`); then `applyTheme()` (not awaited).
2. **Smooth loading:** runs `fixElementWithSrc` on every `<img>` / `<iframe>` (via inner `onSmooth`) present now and added later, and `cleanElementWithSrc` on removed ones, using a document-wide `MutationObserver` (`data-sw-fade`, `data-sw-fadein`, `data-sw-useevent`, `data-sw-fadedelay` attributes, see the JSDoc).
3. **Polyfill:** defines `String.prototype.replaceAll` if missing.
4. **Mobile detection:** `window.Mobile` from URL param `mobile`, else `localStorage["SysWeaver.Mobile"]` (JSON), else the parent window's `Mobile` (same-origin iframe), else `isMobile()`. On `window` `load` adds `SwMobile` or `SwDesktop` to `<html>` and `<body>`, `Transparent` when URL param `transparent=true` or in a child frame (unless `transparent=false`), and `FillScreen` when `fillscreen` is present.
5. **Color scheme:** URL param `colorscheme` adds a `<meta name="color-scheme">` (child frames update it on the `color.scheme` message).
6. **Layout classes/events:** inner `onSizeChange` (run now, on `load` and from a `ResizeObserver` on `<html>`).
7. **Reload detection:** clears the history state (`ClearHistoryState`) when the navigation type is a reload.
8. **Interaction tracking:** inner `onInteraction` on first `mousedown` / `keydown` / `touchstart`.
9. **Scroll saving:** inner `saveScroll` registered on `window` for `"beforeunload"`.
10. **Server-push messaging:** builds a message-type → handler map, registers it with `InterOp.AddListener` (dispatching on `InterOp.GetMessage(ev).Type`) and calls `SessionManager.Init()`.

#### Globals set by `SysWeaverInit`

| Name | Kind | Description |
|---|---|---|
| `window.HaveSysWeaverInit` | window global | Run-once guard for `SysWeaverInit`. |
| `window.UseTheme` | window global | Forced theme name (from `usetheme`, the `theme.use` message, or reset to `null` on `Theme.Changed`); read by `applyTheme`. |
| `window.Mobile` | window global | `true` on mobile devices (see step 4). |
| `window.SwWidth`, `window.SwHeight` | window global | Last seen `innerWidth` / `innerHeight` (change detection in `onSizeChange`). |
| `window.Portrait`, `window.Narrow` | window global | Layout of the top-level SysWeaver window (taken from the parent in same-origin child frames): portrait = width ≤ height, narrow = width ≤ 600 px. |
| `window.SelfPortrait`, `window.SelfNarrow` | window global | Same, but for this window/iframe itself. |
| `window.DidInteract` | window global | Set to `true` on the first user interaction (stops scroll restoration in `AddLoading`). Optional callback `window.OnInteraction(ev)` is invoked then. |
| `window.ScrollCounter` | window global | Counter of programmatic scrolls (incremented by `AddLoading`'s `tryScroll`; used by a currently commented-out scroll listener). |
| `window.IsTop` | window global | `true` if this is the top window, or a frame whose top window is cross-origin. Top windows handle server state messages. |
| `window.IgnoreUserChanged` | window global | See `SysWeaverIgnoreUserChanges`. |
| `sessionStorage["SysWeaver.ServerInstance"]` | storage key | Id of the server instance last connected to; used to detect a server restart (new instance → full reload). |

#### Inner functions of `SysWeaverInit`

| Function | Signature | Description |
|---|---|---|
| `onSmooth` | `(root, fn)` | Calls `fn` on `root` and on all descendant `<img>` and `<iframe>` elements for which `doHandleSrc(e)` is true. |
| `onSizeChange` | `()` | When the window size changed, recomputes `Portrait`/`Narrow`/`SelfPortrait`/`SelfNarrow`, toggles classes `SwPortrait`/`SwLandscape`, `SwNarrow`/`SwWide`, `SwSelfPortrait`/`SwSelfLandscape`, `SwSelfNarrow`/`SwSelfWide` on `<body>` and `<html>`, and dispatches `CustomEvent`s on `<body>` (with `detail` = new boolean) when values change: `SwOrientation` + `SwPortrait`/`SwLandscape`, `SwLayout` + `SwNarrow`/`SwWide`, `SwSelfOrientation` + `SwSelfPortrait`/`SwSelfLandscape`, `SwSelfLayout` + `SwSelfNarrow`/`SwSelfWide` (the `Self` variant event names are chosen from the window's own `SelfPortrait`/`SelfNarrow` values). |
| `onInteraction` | `(ev)` | First-interaction handler: logs, sets `window.DidInteract`, calls `window.OnInteraction(ev)` if defined, removes its own listeners and dispatches an `UserInteracted` event on `<body>`. |
| `saveScroll` | `()` | Stores `window.scrollX` / `scrollY` as `ScrollX` / `ScrollY` in the history state (`UpdateHistoryState`), restored later by `AddLoading`. |
| `setServerInstance` | `(newValue)` | Updates the remembered server instance id in a local variable and `sessionStorage["SysWeaver.ServerInstance"]` (removed when falsy). |
| `blockFn` | `(ev)` → `false` | Event handler that stops propagation and prevents the default action. |
| `setElementBlock` | `(el, fn)` | Assigns `fn` to `el`'s key/mouse `on*` handler properties and adds (`fn` set) or removes (`fn` null, then removing `blockFn`) matching event listeners for `keydown`, `click`, `mousedown`, `mouseover`, `mousemove`, `mouseenter`, `mouseleave`, `mouseout`. Used to block all input while the server overlay is shown. |
| `StartBlock` | `(title, waiting, icon, replace)` | Shows (or, with `replace`, updates) the full-page `<SysWeaver-ServerBlock>` overlay containing `<SysWeaver-ServerImage>` (128 px `ColorIcon` `icon`), `<SysWeaver-ServerTitle>` (`title`), `<SysWeaver-ServerText>` (`waiting`), `<SysWeaver-ServerWaiting>` (spinner `IconWorking`) and `<SysWeaver-ServerTime>` (live elapsed time via `ValueFormat.updateDateTimeLive`). Blocks input on the overlay and the document via `setElementBlock`. Logs `title` as a warning. |
| `RemoveBlock` | `()` | Removes the server overlay and the document input block. |
| `delayedReload` | async `(ev)` | Unless `window.IgnoreUserChanged`, waits 100 ms then `ReloadAll(true, true)`. |
| `delayedRefresh` | async `(ev)` | Unless `window.IgnoreUserChanged`, waits 100 ms then `location.reload(true)`. |
| `delayedUserReload` | async `(ev)` | Same as `delayedReload` (used for login/logout). |
| `ReloadFiles` | async `(msg)` | Handler for `filereload` messages (`msg.Prefix`, `msg.Value` = changed file paths, `msg.DidReload`). In the master session window (`SessionManager.IsMaster`) it flushes the browser cache for each file (`FlushCache`) and re-posts the message with `DidReload = true` via `InterOp.Post`; child windows act only on that re-posted message. Then it scans all body elements for `href` / `src` / `data` attributes and inline `backgroundImage` / `maskImage` styles referencing a changed file, re-fetches those URLs with `getRequest(url, true)` and re-applies the attributes/styles so the browser reloads them. (A stylesheet-rule scan using `updateCss` exists but is commented out.) |
| `updateAttribute` | `(el, atr)` | Inner function of `ReloadFiles`: if attribute `atr` resolves (query stripped, `GetAbsolutePath`) to a changed file, registers the URL for reloading and queues an action that removes and re-sets the attribute. |
| `IsWs`, `IsPureWs` | `(c)` → `boolean` | Inner functions of `ReloadFiles`: true for whitespace or `,` (both currently identical). |
| `getUrls` | `(a)` → `[urls, fullUrls] \| null` | Inner function of `ReloadFiles`: parses `url(...)` tokens (quoted or unquoted) in a CSS value string and returns the matching original token texts and absolute URLs that are in the changed-file map, or `null`. |
| `updateStyle` | `(el, style)` | Inner function of `ReloadFiles`: for inline style property `style` containing changed URLs, registers them for reloading and queues an action that clears and re-sets the property. |
| `updateCss` | `(sm, style, rule)` | Inner function of `ReloadFiles` (its only caller is commented out): rewrites `url(...)` references of a CSS rule's `styleMap` property with a `__forceRefresh__=<random>` query parameter, queuing a set-to-`inherit` action and a post-refresh action that applies the new value. Uses an undeclared (implicitly global) `cleanUrl` variable. |

#### InterOp messages handled by `SysWeaverInit`

| Message `Type` | Windows | Handling |
|---|---|---|
| `Theme.Changed` | all | If sent by another window (`msg.From !== InterOp.Id`): clears `window.UseTheme` and `applyTheme(true)`. |
| `filereload` | all | `ReloadFiles(msg)` (live reload of changed static files). |
| `reload` | top | `delayedReload` → `ReloadAll(true, true)`. |
| `refresh` | top | `delayedRefresh` → `location.reload(true)`. |
| `user.logout` | top | `navigator.credentials.preventSilentAccess()` then `delayedUserReload`. |
| `user.login` | top | Clears the stored server instance, then `delayedUserReload`. |
| `server.connect` | top | Stores `msg.Value` as server instance; if a different instance was remembered, reloads; else removes the server block overlay. |
| `server.reconnect` | top | Same, but also reloads when no instance was remembered. |
| `server.pause`, `server.restart`, `server.shutdown`, `server.error` | top | `StartBlock` with translated texts and icons `IconPaused`, `IconReload`, `IconPower`, `IconWarning` respectively. |
| `server.continue`, `server.restore` | top | `RemoveBlock()`. |
| `color.scheme` | child (only if `colorscheme` param given) | Updates the color-scheme `<meta>` content. |
| `theme.set` | child (only if `settheme` param given) | Stores `msg.Value` in `localStorage["SysWeaver.Theme"]` and `applyTheme()`. |
| `theme.use` | child (only if `usetheme` param given) | Sets `window.UseTheme = msg.Value` and `applyTheme()`. |

### `anim.js`

Animation and interpolation helpers with no DOM access and no dependencies on `common.js`. It is loaded as a classic script with a static `<script defer src="../common/anim.js">` tag by the media viewer pages of SysWeaver.MicroService.Media (`mediaView/MediaView.html`, `MediaPreview.html` and the `backgroundEffects/`, `collageEffects/`, `counterEffects/`, `imageEffects/` and `textEffects/` `index.html` pages). Its consumer is `mediaPlayer/media_player_effect.js`, which uses `SmoothBool`, `SmoothValue`, `SpringAnimation`, `BounceAnimation` and `Smoothness`. Every top-level `const` and `class` becomes a global lexical binding (not a `window` property). Time arguments named `timeMs` are milliseconds. Where the code falls back to `performance.now()` for a missing `timeMs`, this is stated per member. No code runs at load time except the static field initializers of `MathTools` and `SpringAnimation`.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `Smoothness` | const (frozen object, enum) | Interpolation curve selector: `Normal: 0` (linear, C0), `Smooth: 1` (smoothstep, C0+C1), `Smoother: 2` (smootherstep, C0+C1+C2), `Step: 3` (hard step, no continuity). Used as an index into `MathTools.#DoubleSmoothers`. |
| `FalseValue` | const number | `-0.0001`. The internal `SmoothValue` target that `SmoothBool` uses for `false`. It lies slightly outside [0, 1], so the clamped output reaches exactly 0. |
| `TrueValue` | const number | `1.0001`. The internal `SmoothValue` target that `SmoothBool` uses for `true`. It lies slightly outside [0, 1], so the clamped output reaches exactly 1. |
| `MathTools`, `CubicHermite`, `CubicHermiteInterpolator`, `SmoothValue`, `SmoothBool`, `SpringAnimation`, `ProjectileMotion`, `BounceMotion`, `BounceAnimation` | class | See the class sections below. |

#### Functions

None at top level. Everything is in classes.

#### class `MathTools`

Static easing and smoothing functions. It is never instantiated.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Smooth` | static method | `Smooth(t)` → number | Smoothstep `t²(3−2t)`. Returns 0 for `t <= 0` and 1 for `t >= 1`. |
| `Smoother` | static method | `Smoother(t)` → number | Smootherstep `t³(t(6t−15)+10)`. Returns 0 for `t <= 0` and 1 for `t >= 1`. |
| `Step` | static method | `Step(t)` → 0 or 1 | Returns 0 for `t < 0.5`, otherwise 1. |
| `EaseSmooth` | static method | `EaseSmooth(time, easeDuration = 0.1)` → number [0, 1] | Ease-in/ease-out envelope over the normalized interval: `Smooth(time/easeDuration) * Smooth((1−time)/easeDuration)`. `time` is clamped to [0, 1] and `easeDuration` is capped at 1. A non-number `easeDuration` becomes 0.1. Returns 1 when `easeDuration <= 0`. |
| `EaseSmooth2` | static method | `EaseSmooth2(time, easeInDuration, easeOutDuration)` → number | Like `EaseSmooth`, but with separate ease-in and ease-out fractions (each capped at 1). A fraction `<= 0` disables that side (its factor is `Smooth(1)` = 1). There are no defaults for non-number arguments. |
| `EaseSmoother` | static method | `EaseSmoother(time, easeDuration = 0.1)` → number | `EaseSmooth` using `Smoother`. |
| `EaseSmoother2` | static method | `EaseSmoother2(time, easeInDuration, easeOutDuration)` → number | `EaseSmooth2` using `Smoother`. |
| `GetSmoothDoubleFunction` | static method | `GetSmoothDoubleFunction(s)` → `function(number):number` | Returns `#DoubleSmoothers[s]` for a `Smoothness` value. An unknown index returns `undefined`. |
| `#DoubleSmoothers` | static field (private) | array | `[x => x, MathTools.Smooth, MathTools.Smoother, MathTools.Step]`, indexed by `Smoothness`. |

#### class `CubicHermite`

Static Hermite basis functions. It is never instantiated.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `CalcBasis` | static method | `CalcBasis(t)` → `{fromW, initW, toW, exitW}` | Cubic Hermite basis weights at `t`: h00 = `(1+2t)(1−t)²`, h10 = `t(1−t)²`, h01 = `t²(3−2t)`, h11 = `t²(t−1)`. `t` is not clamped. |
| `CalcDerivativeBasis` | static method | `CalcDerivativeBasis(t)` → `{fromW, initW, toW, exitW}` | Derivatives of the basis weights with respect to `t`: `6(t²−t)`, `3t²−4t+1`, `6(t−t²)`, `3t²−2t`. |

#### class `CubicHermiteInterpolator`

An immutable single cubic Hermite segment from a value with a start tangent to a value with an end tangent. It is created by `SmoothValue` (and can be copied).

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `new CubicHermiteInterpolator(from, to, init = 0, exit = 0)` | Stores the end values and tangents. Non-number `init`/`exit` become 0. **Copy form:** if `from` is a non-null object (another `CubicHermiteInterpolator`), all four private fields are copied from it and the other arguments are ignored. |
| `#From`, `#To`, `#Init`, `#Exit` | private | number | Start value, end value, start tangent and end tangent. |
| `Target` | getter | → number | `#To`. |
| `ValueAt` | method | `ValueAt(t)` → number | Interpolated value at segment time `t` (expected range [0, 1], not clamped). |
| `DerivativAt` | method | `DerivativAt(t)` → number | Derivative at segment time `t`, with respect to normalized segment time. The name is spelled without the "e". |

#### class `SmoothValue`

A scalar that glides smoothly (C1-continuous) toward new targets. Each `Update` with a new target starts a new Hermite segment from the current value and velocity. It is created with `new SmoothValue(...)` and is also used internally by `SmoothBool`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `new SmoothValue(timeMs, value = 0, duration = 1000, init = 0, from)` | `timeMs` defaults to `performance.now()`. It creates a segment from `from` (or `value` if `from` is not a number) to `value` with start tangent `init`. `duration` (ms) becomes the default change duration (`#FixedTime`). The adaption time starts at 0, so the value stays at the start value until the first `Update`. **Copy form:** if `timeMs` is a non-null object, the state is copied from the *second* argument `value`, which must be a `SmoothValue` (its interpolator is cloned). |
| `#Time` | private | number | Timestamp (ms) at which the current segment started. |
| `#Hermite` | private | `CubicHermiteInterpolator` | The current segment. |
| `#AdaptionTime` | private | number | Duration (ms) of the current segment. 0 means the value is stationary at the segment's start. |
| `#FixedTime` | private | number | The default duration for `Update` calls that pass no `duration`. |
| `Update` | method | `Update(timeMs, value, duration = #FixedTime, exitVelocity = 0)` → number | Evaluates the current value `tv` at `timeMs` (default `performance.now()`), with elapsed time normalized by the adaption time and clamped to [0, 1]. If `duration <= 0`, `tv` is the new `value` (an immediate jump). If `value` differs from the current target, or `duration` differs from the current adaption time, it starts a new segment at `timeMs` from `tv` to `value`. The new segment's start tangent is the current segment's normalized derivative (carried over unscaled), its end tangent is `exitVelocity`, and its adaption time is `duration`. Returns `tv`. |
| `Target` | getter | → number | The target of the current segment, that is, the last value passed to `Update`/`SetTo`. |
| `ValueAt` | method | `ValueAt(timeMs)` → number | Value at `timeMs` (default `performance.now()`). The normalized time is clamped to [0, 1], so the value holds at the target after the segment ends. |
| `DerivativeAt` | method | `DerivativeAt(timeMs)` → number | Derivative at `timeMs` (default `performance.now()`), with respect to normalized segment time. |
| `SetTo` | method | `SetTo(timeMs, value, init = 0)` → number | Jumps to `value`: it creates a stationary segment `value`→`value` with tangents `init`, sets the start time to `timeMs` (default `performance.now()`) and the adaption time to 0. Returns `value`. This breaks continuity. |

#### class `SmoothBool`

A boolean whose visual state (0 = false, 1 = true) transitions smoothly. It wraps a `SmoothValue` that moves between `FalseValue` and `TrueValue` and shapes the clamped output with a `Smoothness` function.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `new SmoothBool(timeMs, startState, duration, smoothness = Smoothness.Normal)` | Creates the internal `SmoothValue(timeMs, startState ? TrueValue : FalseValue, duration)` and looks up the smoothing function with `MathTools.GetSmoothDoubleFunction`. A non-number `smoothness` means `Normal`. |
| `#Smooth` | private | function | The smoothing function. |
| `#Spline` | private | `SmoothValue` | The underlying value. |
| `#InternalTarget` | private | boolean | The last requested state. |
| `Target` | getter | → boolean | The last state passed to the constructor, `Update` or `SetTo`. |
| `ValueAt` | method | `ValueAt(timeMs)` → number [0, 1] | The underlying value at `timeMs`, clamped to [0, 1] and passed through the smoothing function. |
| `Update` | method | `Update(timeMs, state, duration)` → number [0, 1] | Retargets the underlying value to `TrueValue`/`FalseValue` (see `SmoothValue.Update`), records `state`, and returns the current clamped, smoothed value. |
| `SetTo` | method | `SetTo(timeMs, state)` → undefined | Jumps the underlying value to `TrueValue`/`FalseValue` and records `state`. Unlike its JSDoc, it returns nothing. |

#### class `SpringAnimation`

A damped cosine oscillation ("springy" displacement) as a function of time.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `#InternalZoom` | static field (private) | `SpringAnimation` | `new SpringAnimation(3, 1, 500, 0.001)`, created at class definition. |
| `Zoom` | static getter | → `SpringAnimation` | Returns `#InternalZoom`, a shared preset for a springy zoom. |
| `constructor` | constructor | `new SpringAnimation(frequency = 7, initialLength = 100, decay = 90, snap = 0.01)` | Stores the angular frequency `frequency·2π` (`frequency` is in cycles per second), the amplitude `initialLength` and the decay rate `decay·0.01` (per second). The end time is `ln(initialLength/snap)/decayRate` seconds when `snap > 0`, otherwise 1000 s. |
| `#Frequency`, `#InitialLength`, `#Decay`, `#EndTime` | private | number | Angular frequency, amplitude, decay rate and cutoff time in seconds. |
| `Displacement` | method | `Displacement(timeMs)` → number | `timeMs` defaults to `performance.now()` and is converted to seconds. Returns 0 after the end time. Negative times are clamped to 0. Otherwise returns `initialLength·cos(ω t)/e^(decay·t)`. Callers pass the elapsed time since the animation started. |
| `ValueAt` | method | `ValueAt(timeMs)` → number | Alias for `Displacement`. |

#### class `ProjectileMotion`

Static formulas for a projectile launched straight up from height 0.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `FlightTime` | static method | `FlightTime(initialVelocity, gravity)` → number | `2v/g`. |
| `MaxHeight` | static method | `MaxHeight(initialVelocity, gravity)` → number | `v²/(2·|g|)`. |
| `Displacement` | static method | `Displacement(initialVelocity, gravity, time)` → number | Height `(v − g·t/2)·t`. The result is negative after the flight time. |
| `Velocity` | static method | `Velocity(initialVelocity, gravity, time)` → number | `v − g·t`. |
| `Solve` | static method | `Solve(maxHeight, flightTime)` → `{initialVelocity, gravity}` | Inverse problem: `initialVelocity = 4h/T`, `gravity = 8h/T²`. |

#### class `BounceMotion`

Static formulas for a bouncing projectile whose launch velocity is multiplied by `restitution` at each bounce. Period `p` has velocity `r^p·v` and lasts `r^p·T`, where `T` is the first flight time.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Period` | static method | `Period(initialVelocity, gravity, restitution, flightTime, time)` → integer | The bounce index that `time` falls in: `floor(max(0, log(max(1e-9, (r−1)·t/T + 1))/log r))`. `initialVelocity` and `gravity` are unused. |
| `RestAfterPeriod` | static method | `RestAfterPeriod(initialVelocity, restitution, restVelocity)` → integer | `floor(max(0, log(restVelocity/v)/log r))`, the first period whose velocity is at or below `restVelocity`. |
| `RestTime` | static method | `RestTime(initialVelocity, restitution, restVelocity, flightTime)` → number | `PeriodEndTime(r, RestAfterPeriod(...)) · flightTime`. |
| `PeriodVelocity` | static method | `PeriodVelocity(initialVelocity, restitution, period)` → number | `r^period · v`. |
| `PeriodDuration` | static method | `PeriodDuration(restitution, period)` → number | `r^period`, as a fraction of the first flight time. |
| `PeriodEndTime` | static method | `PeriodEndTime(restitution, period)` → number | `1 + r(r^period − 1)/(r − 1)`, the normalized end time of `period`. Multiply by the flight time to get seconds. |
| `Displacement` | static method | `Displacement(initialVelocity, gravity, restitution, time)` → number | Computes the flight time with `ProjectileMotion.FlightTime` and calls `Displacement2`. |
| `Displacement2` | static method | `Displacement2(initialVelocity, gravity, restitution, flightTime, time)` → number | Finds the period `p` and the time inside that period (clamped to [0, period duration]), then returns `ProjectileMotion.Displacement(PeriodVelocity(p), gravity, dt)`. |
| `Velocity` | static method | `Velocity(initialVelocity, gravity, restitution, time)` → number | Computes the flight time with `ProjectileMotion.FlightTime` and returns `BounceMotion.Velocity2(...)`. |
| `Velocity2` | static method | `Velocity2(initialVelocity, gravity, restitution, flightTime, time)` → number | Velocity counterpart of `Displacement2`: finds the current period with `BounceMotion.Period`, the time into that period from `BounceMotion.PeriodEndTime` / `PeriodDuration`, the period's start velocity from `BounceMotion.PeriodVelocity`, and returns `ProjectileMotion.Velocity(v, gravity, dt)`. |

#### class `BounceAnimation`

A preconfigured bounce curve evaluated by time. It is used by `media_player_effect.js`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `new BounceAnimation(initialVelocity, gravity, restitution, restVelocity = 0.1, normalize = true, startAtPeak = false)` | Stores the parameters and the first flight time. With `normalize`, the output is scaled by `1/MaxHeight`, so the first peak is 1. With `startAtPeak`, the time is offset by half a flight time. If `restitution < 1`, the rest time is computed with `BounceMotion.RestTime`. |
| `#TimeAdjust` | private | number (s) | 0, or half the flight time with `startAtPeak`. |
| `#Scale` | private | number | 1, or `1/MaxHeight` with `normalize`. |
| `#InitialVelocity`, `#Gravity`, `#Restitution`, `#FlightTime` | private | number | Motion parameters. |
| `#RestAt` | private | number (s) | The rest time. It is 0 (meaning "never rests") when `restitution >= 1`. |
| `ValueAt` | method | `ValueAt(timeMs)` → number | Converts `timeMs` to seconds and adds `#TimeAdjust`. If `#RestAt <= 0`, it repeats the first parabola forever (time modulo the flight time). Otherwise it returns 0 from the rest time on, and before that `BounceMotion.Displacement2(...)·#Scale`. There is no `performance.now()` fallback: `timeMs` must be a number. |

### `tab.js`

A small tab bar control. It is loaded with a static `<script defer src="../common/tab.js">` (plus `<link rel="stylesheet" href="../common/tab.css">`) by `explore/api.html` (SysWeaver.HttpServer.ExploreModule) and `edit/type.html` (SysWeaver.MicroService.Edit). `explore/api.js` creates `new Tab("Api.Args")` and `new Tab("Api.Results")`. No use was found in `edit/type.js`. The only global it defines is the class `Tab`. It depends on `badClick(ev)` from `common.js`. Styling is in `tab.css` (`SysWeaver-TabBar`, `SysWeaver-Tab`, class `tabactive`).

#### Globals

| Name | Kind | Description |
|---|---|---|
| `Tab` | class | Tab bar control (see below). |

#### Functions

None at top level.

#### class `Tab`

A tab bar backed by a `<SysWeaver-TabBar>` element. Callers append `tab.Element` to the page and call `AddTab` for each tab. Selection moves the `tabactive` class between the tab header elements and their associated content elements. Content elements are usually `SysWeaver-TabContent`, which `tab.css` hides unless it has `tabactive`. With a save key, the selected index is persisted in `localStorage`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `new Tab(saveKey = null)` | Creates `this.Element = <SysWeaver-TabBar>` (not attached) and sets `Selected = -1`. If `saveKey` is given, the storage key becomes `"SysWeaver.Tabs." + saveKey`, and `Selected` is initialised from `parseInt(localStorage[key] ?? "-1") \| 0` (an unparsable value becomes 0). Storage errors are swallowed. |
| `Element` | field | `HTMLElement` | The `<SysWeaver-TabBar>` element. Its children are the tab header elements. |
| `Selected` | field | number | The selected tab index, or -1. It may hold a restored index that does not exist yet. |
| `SaveKey` | field | string \| null | The full `localStorage` key (`SysWeaver.Tabs.<saveKey>`), or `null`. |
| `Select` | method | `Select(index)` | When `index` is `undefined` or negative, it keeps a valid current selection and returns. Otherwise it selects index 0 without saving, or sets `Selected = -1` if there are no tabs. It returns early if `index` is already selected. Deselecting the previous tab restores its `title` to `Title`, removes `tabactive` from an `OnActivate` that is an element, calls `OnDeactivate(tabEl, oldIndex)` if it is a function, and removes `tabactive` from its `Element` and from the tab header itself. Selecting the new tab sets its `title` to `TitleSelected` and adds `tabactive` to the header. If `OnActivate` is an element, it gets `tabactive`. If it is a function, it is called as `OnActivate(tabEl, index)`. `Element` also gets `tabactive`. Explicit selections are saved to `localStorage[SaveKey]`, with errors swallowed. Finally it sets `Selected`. |
| `AddTab` | method | `AddTab(name, onActivate, title, onDeactivate, element, titleSelected)` → `HTMLElement` | Appends a `<SysWeaver-Tab>` with text `name` to the bar. `onActivate` is either a function `(tabEl, index)` or an element that receives `tabactive`. `onDeactivate` is an optional function `(tabEl, index)`. `element` is an optional content element that receives `tabactive`. `title`/`titleSelected` are the tooltips for the unselected/selected state (default `""`). These values are stored as expandos `Element`, `OnActivate`, `OnDeactivate`, `Title` and `TitleSelected` on the tab element. Its click handler (`e.onclick`) ignores clicks rejected by `badClick(ev)`, computes the tab's index from its preceding siblings, and calls `Select(index)`. After the append, if nothing is selected (`Selected < 0`), it calls `Select(0)`, which also saves. If the new tab's index equals a restored `Selected`, that tab is activated. Returns the tab element. |
| `RemoveTab` | method | `RemoveTab(index)` | Removes the tab at `index` (does nothing if there is none). If it was selected, it first selects a neighbour (`index−1`, or `index+1` for index 0, capped at the last index) with `this.Select`; when no other tab exists, `Selected` becomes −1. A selection above `index` is decremented and, with a `SaveKey`, the new index is stored in `localStorage`. No callers were found. |

## CSS reference

Conventions: "common.js" refers to `common/common.js` in this folder. Element names are case-insensitive in HTML, so `Sysweaver-Button` and `SysWeaver-Button` match the same elements. Colour custom properties are listed once under `theme.css` and only referenced from the other files.

### `theme.css`

The global stylesheet of the framework. About 54 pages load it with `<link rel="stylesheet" href="../common/theme.css">` (or `common/theme.css` from the root `index.html`). The ExploreModule also passes it as `STYLECSSFILE`. It defines the theme custom properties and the theme variants selected with `data-theme` on `<html>`, and it holds the base `html`/`body`/`a` styling and the styles of every `SysWeaver-*` element that `common.js` creates. It imports the *Nunito* font from Google Fonts (`@import url('https://fonts.googleapis.com/css2?family=Nunito...')`, used by the `pro` theme) and references the background images `graph-paper.svg`, `racing-stripes.svg`, `Hacker.png`, `moroccan.svg` and `glamorous.svg` in this folder, plus `../icon.svg` and `../auth/UserImages/Current/small`.

**Template.** The file is served as a server template: `HttpServerBase.cs` registers it with `AddTemplateMatch("common/theme.css")`, and `${Name}` tokens are replaced with the server's static template variables before the file is sent. The only tokens in the file are in the `application` theme:

| Template variable | Value supplied by the server (`HttpServerBase.cs`) | Used for |
|---|---|---|
| `${Color.Background}` | `HashColors.AppColors.Background`: a lower-case `#rrggbb` from the application's hash colours (complement hue, value 0.1) | `--ThemeBackground` in `[data-theme="application"]` |
| `${Color.Color}` | `HashColors.AppColors.Acc1` (accent hue 1, value 0.9) | `--ThemeMain` in `[data-theme="application"]` |
| `${Color.Acc1}` | `HashColors.AppColors.Acc3` (hue + 30°, value 0.7) | `--ThemeAcc1` in `[data-theme="application"]` |
| `${Color.Acc2}` | `HashColors.AppColors.Acc4` (hue + 330°, value 0.7) | `--ThemeAcc2` in `[data-theme="application"]` |

`HashColors.AppColors` is derived from `EnvInfo.AppName` and `EnvInfo.AppSeed`, so the `application` theme has a stable colour set per application. The values are always 7-character hex, which is the format `applyTheme` in common.js can split into R, G and B.

**Theme selection (common.js).** `getTheme()` returns `window.UseTheme` if set. Otherwise it starts from `"dark"`, switches to `"light"` if the OS reports `prefers-color-scheme: light`, and is overridden by `localStorage["SysWeaver.Theme"]`. `applyTheme()` removes `data-theme` from `<html>` for `"light"` and sets `data-theme="<name>"` for every other theme. A name of the form `name@url` first loads the extra stylesheet `url` with `includeCss`. `applyTheme()` then reads the computed `--ThemeBackground`, `--ThemeMain`, `--ThemeAcc1` and `--ThemeAcc2` and writes the derived properties (`…R`, `…G`, `…B`, `…RGB`, `…25`, `…50`, `…75`) as inline styles on `<html>`. It sets `window.IsDarkMode` from the body's computed `color-scheme`, and registers `window[name + "ThemeAnimate"]` (only `hackerThemeAnimate` exists) with `Animator`. `setTheme`, `toggleTheme` and `resetTheme` write or clear the storage key and post the InterOp message `"Theme.Changed"`.

#### Theme variants (`[data-theme=...]`)

| Selector(s) | Description |
|---|---|
| `:root` (no `data-theme`) = **light** | Default property values (see Custom properties), `color-scheme: light`, and `body` with the `graph-paper.svg` background. |
| `[data-theme="dark"]`, `:root[data-theme="dark"]`, `html[data-theme="dark"] textarea`, `html[data-theme="dark"] body` | **dark**: background `#242226`, main `#aaaaaa`, Acc1 `#D0BF2F`, Acc2 `#5ecad6`. `color-scheme: dark` on the root and on `textarea`. The body background is `racing-stripes.svg` over a radial gradient of `rgba(--ThemeAcc1RGB, 0.1)` from the bottom centre. |
| `[data-theme="hacker"]`, `:root[data-theme="hacker"]`, `html[data-theme="hacker"] textarea`, `html[data-theme="hacker"] body::before` | **hacker**: background `#041004`, main `#22cc22`, Acc1 `#bb8822`, Acc2 `#22bbaa`. The font is the monospace stack and all radii are 1px. Dark color-scheme. `body::before` is a fixed, non-interactive overlay (`z-index: 10000000`, 100vw × 200vh) tiled with `Hacker.png` at 64px. Its vertical `background-position` is `var(--ThemePos)`, which `hackerThemeAnimate(time)` in common.js updates every animation frame (`-round((time·0.01) % 16)·4` px) to scroll the pattern. |
| `[data-theme="application"]`, `:root[data-theme="application"]`, `html[data-theme="application"] textarea`, `html[data-theme="application"] body` | **application**: the four colours come from the template variables above. Dark color-scheme. The body has a fixed `moroccan.svg` pattern plus three fixed radial gradients: Main at 20% from the bottom centre, Acc2 at 10% from the left, and Acc1 at 10% from the right. |
| `[data-theme="pro"]`, `html[data-theme="pro"] body`, `:root[data-theme="pro"]`, `html[data-theme="pro"] textarea` | **pro**: background `#171c2e`, main `#ffffff`, Acc1 `#bedbff`, Acc2 `#FFE2BE`. The font is `"Nunito", sans-serif` at `14px`, `--ThemeIconSize: 0.75`, and the radii are asymmetric (`4px 2px 16px 2px`, `3px 1px 12px 1px`, `2px 1px 8px 1px`). The body has `glamorous.svg` over an Acc1 radial gradient and `font-optical-sizing: auto`. Dark color-scheme. |
| `html [data-theme] body` | Sets `scrollbar-color: --ThemeBackground --ThemeMain`. The selector has a descendant space (`html [data-theme] body`), so it only matches a `body` inside a descendant element with `data-theme`. With `data-theme` on `<html>` itself, it does not match. |
| `html[data-theme] body::-webkit-scrollbar-thumb`, `html[data-theme] body::-webkit-scrollbar-track` | WebKit scrollbar colours (thumb = background, track = main) for any non-light theme. |

#### Base elements and page-level classes

| Selector(s) | Description |
|---|---|
| `*` | `scrollbar-color: var(--ThemeMain50) var(--ThemeBackground)` everywhere. |
| `html` | Defines `--Margin: 24px`. No margin or padding, `box-sizing: border-box`, 100% width and height. |
| `html.SwNarrow` | An empty rule. `SwNarrow`/`SwWide` (and `SwPortrait`/`SwLandscape`, `SwSelf*`, `SwMobile`/`SwDesktop`) are set on `<html>` and `<body>` by the resize observer in `SysWeaverInit` (common.js). |
| `body` | Theme background colour with a fixed, repeated `graph-paper.svg`. Text colour `--ThemeMain`, font `--ThemeFont` at `--ThemeFontSize`, and `user-select: none`. |
| `html.FillScreen` | No margin, minimum 100% size. Added to `<html>` and `<body>` by `SysWeaverInit` on `load` when the URL has `?fillscreen`. |
| `html.Embedded`, `html.Embedded.SwNarrow`, `html.Embedded body` | Pages embedded in the app shell: `app/application.js` adds `Embedded` to the iframe document's `<html>`. Padding is `--Margin` (top only when narrow), the height is reduced by `--Margin`, and the body background is forced transparent. |
| `html.Transparent`, `html.Transparent body` | Transparent body background (`!important`). `SysWeaverInit` adds `Transparent` on `load` when `?transparent=true`, or when the page is a child frame and `transparent` is not `false`. |
| `input[type=password]::-ms-reveal`, `input[type=password]::-ms-clear` | Hides the Edge/IE password reveal and clear buttons (attribute selector `type`). |
| `a`, `a:focus`, `a:hover`, `a:visited`, `a:visited:hover` | Bold Acc1 links without underline, with a 6px radius and 0.1s transitions. Focus shows a 2px Acc2 outline. Hover inverts the colours (background-coloured text on Acc1). |
| `a.Image`, `a.Image img`, `a.Image:hover`, `a.Image:hover:focus` | Image links: no padding, `vertical-align: bottom` on the image, and a 2px Acc1 outline instead of a fill on hover. |
| `em` | Bold, non-italic, Acc1 colour. |

#### Theme colour utility classes

| Selector(s) | Description |
|---|---|
| `.ThemeBackgroundBg`, `.ThemeBackgroundFg` | `background-color` / `color` = `--ThemeBackground`. |
| `.ThemeMainBg`, `.ThemeMainFg` | `background-color` / `color` = `--ThemeMain` (`ThemeMainBg` is used by `explore/explore.js`). |
| `.ThemeAccBg`, `.ThemeAccFg` | `background-color` / `color` = `--ThemeAcc1` (`ThemeAccFg` is used by `explore/explore.js`). |
| `.ThemeAcc2Bg`, `.ThemeAcc2Fg` | `background-color` / `color` = `--ThemeAcc2`. |
| `.ThemeImgClick`, `.ThemeImgClick:focus`, `.ThemeImgClick > img`, `.ThemeImgClick > icon-24x24`, `.ThemeImgClick > icon-48x48` (with `:focus` / `:hover` variants) | A clickable image wrapper: pointer cursor and no focus outline. The child `img`, `icon-24x24` or `icon-48x48` (the element names `ColorIcon` creates for 24×24 and 48×48 icons) gets a 1px four-way `drop-shadow` outline, Acc2 on focus and Acc1 on hover, with a 0.1s filter transition. No JS user was found in the `srcWeb` folders. |

#### Links, page and messages

| Selector(s) | Description |
|---|---|
| `SysWeaver-Link`, `:focus`, `:hover`, `:visited`, `:visited:hover` | A clickable link element created by `ValueFormat` in common.js (`ValueFormat.LinkNode` and link formatting). It looks like `a`, but the radius is `--ThemeRadInp` and it has a pointer cursor. |
| `SysWeaver-Link.ThemeMain` (`:hover`, `:visited`) | A link variant in the main colour that hovers to a Main background. |
| `SysWeaver-Link.Image`, `SysWeaver-Link.Image:hover`, `SysWeaver-Link.Image.ThemeMain:hover` | An inline-block image link with a transparent 1px border that turns Acc1 (or Main) on hover. |
| `SysWeaver-Page`, `SysWeaver-Page.Wide` | The centred page column: absolutely positioned and centred with `left: 50%` and `translate(-50%)`. Max width `52em`, or `78em` with `.Wide`, which `edit/type.js` and `translator/Translator.js` add. Top padding is `--Margin`, and there is a soft Acc1 glow `box-shadow`. Pages declare the element in their HTML. |
| `SysWeaver-Fail`, `SysWeaver-Fail.MsgError`, `SysWeaver-Fail.MsgInfo` | A fixed toast at the bottom of the viewport (`z-index: 10000`, 125% font, pointer cursor). `Message()` in common.js creates it, and `Fail()` and `Info()` pass the class `MsgError` (red: `#f00` border, `#800` background) or `MsgInfo` (green: `#4b4` border, `#383` background). |
| `SysWeaver-Text`, `SysWeaver-Text.KeyClick`, `SysWeaver-Text.KeyClick:focus` | A block text container with a 0.5em radius (used by `edit/type.js` and `translator/Translator.js`). `keyboardClick(element)` in common.js adds `KeyClick` (pointer cursor, Acc2 focus outline) to make an element keyboard-clickable, and removes it again in the undo path. |
| `SysWeaver-CenterBlock`, `SysWeaver-CenterBlock > *:first-child:last-child` | A wrapping flex row with `space-around`; a single child is centred with auto margins. `Button.CreateRow()` in common.js creates it, as do several page scripts (auth, media, translator). |

#### Buttons

| Selector(s) | Description |
|---|---|
| `SysWeaver-Button` | The element created by the `Button` class in common.js (`ButtonStyle.ButtonElement = "Sysweaver-Button"`). It is an inline-flex pill: bold 80% text, minimum width 128px, Acc2 background with background-coloured text, radius `--ThemeRad`, line height `--ThemeIconSize·24px`, drop shadow, and opacity 0.8. |
| `SysWeaver-Button:focus`, `SysWeaver-Button:hover` | Focus shows a 3px Acc1 outline. Hover sets opacity 1. |
| `SysWeaver-Button.Pressed` | Shifted by 1px/2px with a smaller shadow. The `Button` constructor adds `Pressed` while the button is held (with a minimum pressed time, `ButtonStyle.MinPressed`). |
| `SysWeaver-Button.Disabled`, `SysWeaver-Button.Disabled:hover` | Pressed look at opacity 0.4 with the default cursor. The `Button` class toggles it (also used by `app/application.js`, `explore/table.js`, `auth/Passkey.js` and the ServerManager pages). |
| `SysWeaver-Button.SysWeaverButtonCenter` | An inline-block button centred with auto horizontal margins. It is applied through `ButtonStyle.ButtonStyle` (the class name a style adds to the element). No user was found by name. |
| `SysWeaver-Button.ThemeMain`, `SysWeaver-Button.ThemeAcc1` | Alternative button colours (Main or Acc1 background), also applied through `ButtonStyle.ButtonStyle`. |
| `SysWeaver-Button Sysweaver-ButtonText` | The button's label element, created by the `Button` constructor. It is floated left and vertically centred. |
| `SysWeaver-Button Icon-24x24` | The button's `ColorIcon` (24×24 by default from `ButtonStyle.IconWidth/IconHeight`), floated right with an 8px left margin. |

#### Loading overlay and progress bar

| Selector(s) | Description |
|---|---|
| `SysWeaver-Loading` (first rule) | The overlay created by `AddLoading(page, text, opaque, useIcon)` in common.js. It is fixed and full-size, `z-index: 1000`, with a `--ThemeBackground75` background, a 4px backdrop blur, a forced default cursor and `overflow: hidden`. |
| `SysWeaver-Loading` (second rule, later in the file) | A second rule for the same selector that overrides it with `position: absolute` and `background-color: rgba(--ThemeBackgroundRGB, 0.5)`. Blur, cursor and overflow still come from the first rule. |
| `SysWeaver-Loading.Opaque` | A solid `--ThemeBackground` without blur. `AddLoading` adds it when `opaque` is set. |
| `SysWeaver-LoadingText`, `SysWeaver-LoadingText.Center` | Large (200%) bold Acc1 loading text, fixed and centred horizontally 3em above the bottom. `.Center` (added by `AddLoading` when no icon is shown) moves it to `top: 50vh`. Its `transform: translate(-50% -50%)` has no comma and is therefore invalid, so the base `translateX(-50%)` stays. |
| `SysWeaver-Loading > Icon-64x64`, `SysWeaver-Loading > Icon-64x64.LoadingIcon` | The 64×64 `ColorIcon` (default class `IconWorking`) that `AddLoading` appends. It is centred horizontally at 30vh (fixed). The `.LoadingIcon` variant is absolutely centred at 50%/50%. No JS adding `LoadingIcon` was found. |
| `SysWeaver-ProgressBar` | The track created by the `ProgressBar` class in common.js: a 2em high rounded bar in Main at 10% alpha. |
| `SysWeaver-ProgressBarFill` | The Acc1 fill. `ProgressBar.SetPercentage` positions it by setting `style.left` to minus the remaining percentage. |
| `SysWeaver-ProgressBarTextInside`, `SysWeaver-ProgressBarTextInside.Active` | Percentage text drawn inside the fill (right-aligned), transparent unless `.Active`, which makes it background-coloured. |
| `SysWeaver-ProgressBarTextOutside`, `SysWeaver-ProgressBarTextOutside.Active` | Percentage text drawn after the fill (left-aligned), transparent unless `.Active`, which makes it Main-coloured. `SetPercentage` toggles `Active` (`const a = "Active"`) on whichever of the two fits. Both use `font-family: var(--ThemeMono)`, a property that is not defined in this file. |

#### Server-unavailable blocker

| Selector(s) | Description |
|---|---|
| `SysWeaver-ServerBlock` | A full-screen, fixed blocking layer (`z-index: 1111111`, background at 95% alpha, blur, `not-allowed` cursor, centred content column at least 500px wide). `StartBlock` inside `SysWeaverInit` (common.js) creates it on the server messages `server.pause`, `server.restart`, `server.shutdown` and `server.error`, and `RemoveBlock` removes it. |
| `SysWeaver-ServerImage`, `SysWeaver-ServerTitle`, `SysWeaver-ServerText`, `SysWeaver-ServerWaiting`, `SysWeaver-ServerTime` | The blocker's children: an image block (a 128×128 `ColorIcon` showing `IconPaused`, `IconReload`, `IconPower` or `IconWarning`), a 200% bold Acc1 title, 150% text, a waiting area with a 5em top margin (holding a 48×48 `IconWorking`), and bold Acc2 time text. |

#### Icon classes

| Selector(s) | Description |
|---|---|
| `.IconWorking`, `.IconPaused`, `.IconPower`, `.IconWarning`, `.IconReload` | Mask icons as inline base64 SVG `mask-image` data URIs, coloured by the `ColorIcon` colour class. `IconWorking` is an animated SVG spinner made of three rotating arcs (the default icon of `AddLoading`, the server blocker and several pages). `IconPaused` (two bars), `IconPower` (power symbol) and `IconReload` (circular arrow) are the server-blocker images for pause, restart and shutdown, and `IconWarning` is the image for a lost connection. `IconWarning` is a triangle with an exclamation mark (also used by `auth/Passkey.js`). |
| `.IconLogo` | A background image `../icon.svg` (the application favicon SVG served by the server) with a transparent background colour (`!important`). |
| `.IconCurrentUser`, `.IconCurrentUserFill` | The current user's avatar `../auth/UserImages/Current/small` as a background image. The `Fill` variant adds `background-size: cover`. Used by `app/application.js` for the header user button. |

#### Code, JSON, long text and markdown

| Selector(s) | Description |
|---|---|
| `SysWeaver-Code` | A scrollable, full-size, selectable monospace (`--ThemeFontMono`) block with `white-space: pre`. Created by `ValueFormat.updateJson` in common.js. |
| `.SysWeaverCode`, `.SysWeaverCode span.key`, `span.string`, `span.number`, `span.null`, `span.boolean` | JSON syntax colouring. The base text is Main at 40% alpha. Spans with class `key` are Main, `string` Acc2, `number` Acc1, `null` Acc2 at 40% opacity, and `boolean` Acc1 italic. `ValueFormat.updateJson` adds `SysWeaverCode` to its `SysWeaver-Code` element, and `explore/api.js` uses it too. |
| `SysWeaver-LongText`, `SysWeaver-LongText.Mono` | A scrollable, selectable `white-space: pre` text block (minimum height 4em) in the theme font, or monospace with `.Mono`. Created by `ValueFormat.updateLongText` in the popup it opens on click, which adds `Mono` when the value is formatted as monospace. |
| `SysWeaver-MdText`, `SysWeaver-MdText *` | A scrollable, selectable markdown container (minimum height 4em). Created by `ValueFormat.updateMarkDown`, which renders with the vendored `marked`. All descendants get `margin-block-start: 0.25em`. |
| `SysWeaver-MdText h1, h2, h3, h4, h5` | Acc2 headings with `margin-block-end: 0.25em`. In this selector list only `h1` is scoped to `SysWeaver-MdText`; the `h2`–`h5` parts are global and apply to all such headings on the page. |
| `SysWeaver-MdText ul, ol` | `padding-inline-start: 24px`. Only `ul` is scoped; `ol` is global. |
| `SysWeaver-MdText li`, `li:hover`, `li::marker` | List items fade to Acc1 on hover (over `--ThemeFadeDuration`). Markers are Acc1. |
| `SysWeaver-MdText a`, `SysWeaver-MdText a:visited` | Acc1 links with smaller padding (`0.1em 0.25em`). |
| `SysWeaver-MdText em` | Main-coloured bold emphasis, overriding the global Acc1 `em`. |

#### Custom properties

The colour-derived properties are placeholders (`0`) in the stylesheet. `applyTheme()` in common.js overwrites them as inline styles on `<html>` after every theme change. That makes them per-theme only after `applyTheme` has run, and they are never set for a colour that is not 4- or 7-character hex (in which case R, G and B stay 0).

| Variable | Default / source | Used for |
|---|---|---|
| `--ThemeBackground` | `#ffffff` (light). Overridden per theme: dark `#242226`, hacker `#041004`, application `${Color.Background}`, pro `#171c2e`. | Page background, text on accent fills, scrollbars, `.ThemeBackgroundBg/Fg`. |
| `--ThemeMain` | `#444`. dark `#aaaaaa`, hacker `#22cc22`, application `${Color.Color}`, pro `#ffffff`. | Body text colour, `.ThemeMain` variants, scrollbars. |
| `--ThemeAcc1` | `#234D9C`. dark `#D0BF2F`, hacker `#bb8822`, application `${Color.Acc1}`, pro `#bedbff`. | Links, `em`, titles, progress fill, hover outlines, active tabs. |
| `--ThemeAcc2` | `#6A1834`. dark `#5ecad6`, hacker `#22bbaa`, application `${Color.Acc2}`, pro `#FFE2BE`. | Buttons, focus outlines, markdown headings, JSON strings. |
| `--ThemeFont` | `Verdana, Geneva, Tahoma, sans-serif`. hacker uses the mono stack, pro uses `"Nunito", sans-serif`. | `body`, `SysWeaver-LongText`, `SysWeaver-MdText`. |
| `--ThemeFontMono` | `'Roboto Mono', 'Lucida Console', monospace` | `SysWeaver-Code`, `.SysWeaverCode`, `SysWeaver-LongText.Mono`, `SysWeaver-TabContent.CODE` (tab.css). |
| `--ThemeFontSize` | `100%`. pro `14px`. | `body` font size. |
| `--ThemeIconSize` | `1.0`. pro `0.75`. | A multiplier for button line height and for the `icon-WxH` rules that `ColorIcon` generates in common.js. |
| `--ThemeRad`, `--ThemeRadInp`, `--ThemeRadInp2` | `12px`, `8px`, `5px`. hacker `1px` each, pro `4px 2px 16px 2px` / `3px 1px 12px 1px` / `2px 1px 8px 1px`. | Corner radii: large containers and buttons (`--ThemeRad`, also tabs), inputs and links (`--ThemeRadInp`), small inputs (`--ThemeRadInp2`). The last is not used in this file; it is available to page CSS. |
| `--ThemeFadeDuration` | `0.1s` | Transition durations (`SysWeaver-MdText li`). Also available to page CSS. |
| `--ThemeOk`, `--ThemeError`, `--ThemeWarning` | `#0b0`, `#c00`, `#ba0` | Status colours for page CSS and scripts. Not referenced inside this file. |
| `--ThemeBackgroundR`, `--ThemeBackgroundG`, `--ThemeBackgroundB`, `--ThemeMainR`, `--ThemeMainG`, `--ThemeMainB`, `--ThemeAcc1R`, `--ThemeAcc1G`, `--ThemeAcc1B`, `--ThemeAcc2R`, `--ThemeAcc2G`, `--ThemeAcc2B` | `0`. Set by `applyTheme` (`fix(name)`) to the 0–255 channel. | Individual channels for page CSS. |
| `--ThemeBackgroundRGB`, `--ThemeMainRGB`, `--ThemeAcc1RGB`, `--ThemeAcc2RGB` | `0`. Set by `applyTheme` to `"r,g,b"`. | `rgba(var(--…RGB), alpha)` tints: page glow, gradients, progress track, server blocker, loading overlay, tab hover. |
| `--ThemeBackground25`, `--ThemeBackground50`, `--ThemeBackground75`, `--ThemeMain25`, `--ThemeMain50`, `--ThemeMain75`, `--ThemeAcc125`, `--ThemeAcc150`, `--ThemeAcc175`, `--ThemeAcc225`, `--ThemeAcc250`, `--ThemeAcc275` | `0`. Set by `applyTheme` to `rgba(r,g,b,0.25 / 0.50 / 0.75)`. | Pre-mixed translucent colours (`--ThemeMain50` for the global scrollbar thumb, `--ThemeBackground75` for the loading overlay). |
| `--ThemePos` | Not defined by default. Declared `0px` in the hacker theme and updated by `hackerThemeAnimate` (`document.documentElement.style`). | Vertical scroll offset of the hacker `body::before` pattern. |
| `--Margin` | `24px` on `html` | `SysWeaver-Page` top padding, and `html.Embedded` padding and height. |
| `--ThemeMono` | Referenced (progress bar text) but not defined anywhere in this file | `font-family` of `SysWeaver-ProgressBarText*`. |

#### Animations, media queries

The file has no `@keyframes` and no `@media` rules. Theme switching uses `data-theme` (see above). Motion comes from the animated SVG in `.IconWorking`, the CSS transitions on links, buttons and icons, and the JS-driven `--ThemePos`.

### `common.css`

An older, non-templated subset of `theme.css`. No reference to it by name was found in the repositories (no page `<link>` and no server registration). It contains no theme variants, no `@import`, no `html`/`body`/`a`/`em` base rules, no colour utility classes, and none of the `Code`/`LongText`/`MdText`/`IconCurrentUser*` rules. Its rules otherwise match the corresponding `theme.css` rules, with the differences listed below.

| Selector(s) | Description |
|---|---|
| `:root` | The same custom property set as `theme.css` (`--ThemeBackground`, `--ThemeMain`, `--ThemeAcc1`, `--ThemeAcc2`, `--ThemeFont`, `--ThemeFontMono`, `--ThemeFontSize`, `--ThemeIconSize`, `--ThemeRad`, `--ThemeRadInp`, `--ThemeRadInp2`, `--ThemeFadeDuration`, `--ThemeOk`, `--ThemeError`, `--ThemeWarning`, the `…R/G/B`, `…RGB` and `…25/50/75` placeholders), but the colour defaults are the **dark** palette (`#242226`, `#aaaaaa`, `#D0BF2F`, `#5ecad6`). There is no `color-scheme`, `--Margin` or `--ThemePos`. |
| `SysWeaver-Link` (+ `:focus`, `:hover`, `:visited`, `:visited:hover`, `.ThemeMain`, `.ThemeMain:hover`, `.ThemeMain:visited`, `.Image`, `.Image:hover`, `.Image.ThemeMain:hover`) | As in `theme.css`, but the radius is a fixed `6px` instead of `--ThemeRadInp`. |
| `SysWeaver-Page`, `SysWeaver-Page.Wide` | As in `theme.css`, but the top padding is `1em` (no `--Margin`) and there is no `top: 0`. |
| `SysWeaver-Fail`, `.MsgError`, `.MsgInfo` | Identical to `theme.css`. |
| `SysWeaver-CenterBlock` (+ single-child rule) | Identical. |
| `SysWeaver-Button`, `.SysWeaverButtonCenter`, `:focus`, `:hover`, `.Pressed`, `.Disabled`, `.Disabled:hover`, `Sysweaver-ButtonText`, `Icon-24x24`, `.ThemeMain`, `.ThemeAcc1` | As in `theme.css`, but `line-height: 24px` (no `--ThemeIconSize` scaling) and `border-radius: 15px` (no `--ThemeRad`). |
| `SysWeaver-Loading`, `SysWeaver-Loading.Opaque`, `SysWeaver-LoadingText`, `SysWeaver-LoadingText.Center`, `SysWeaver-Loading > Icon-64x64` | Only the first (fixed, blurred) `SysWeaver-Loading` rule is present. There is no second override and no `.LoadingIcon` rule. |
| `SysWeaver-ProgressBar`, `SysWeaver-ProgressBarFill`, `SysWeaver-ProgressBarTextInside` (+ `.Active`), `SysWeaver-ProgressBarTextOutside` (+ `.Active`) | Identical, including the undefined `--ThemeMono`. |
| `SysWeaver-Text`, `SysWeaver-Text.KeyClick`, `.KeyClick:focus` | Identical. |
| `SysWeaver-ServerBlock`, `SysWeaver-ServerImage`, `SysWeaver-ServerTitle`, `SysWeaver-ServerText`, `SysWeaver-ServerWaiting`, `SysWeaver-ServerTime` | Identical. |
| `.IconWorking`, `.IconPaused`, `.IconPower`, `.IconWarning`, `.IconReload`, `.IconLogo` | Identical mask and background icons. |

#### Custom properties

The same as `theme.css` (see above), with dark colour defaults. The JS-computed placeholders are filled by `applyTheme` only if a page loads this file and common.js.

#### Animations, media queries, theme variants

None.

### `tab.css`

Styles for the `Tab` control in `tab.js`. It is loaded with `<link rel="stylesheet" href="../common/tab.css">` by `explore/api.html` and `edit/type.html`, together with `tab.js`. It is not templated and depends on the theme variables from `theme.css` (`--ThemeBackground`, `--ThemeMain`, `--ThemeAcc1`, `--ThemeAcc1RGB`, `--ThemeRad`, `--ThemeFontMono`).

| Selector(s) | Description |
|---|---|
| `SysWeaver-TabBar` | The bar element (`Tab.Element`): a full-width block with `overflow: hidden` and `white-space: nowrap`, so tabs do not wrap. |
| `SysWeaver-Tab`, `SysWeaver-Tab:first-child`, `SysWeaver-Tab:last-child`, `SysWeaver-Tab:hover` | A tab header created by `Tab.AddTab`. It is an inline-block with a pointer cursor, bold uppercase 80% text, and a 1px Acc1 border on the left, top and right. The top corners have radius `--ThemeRad` and the bottom corners are square. The first and last tabs get 16px outer margins. Hover tints the background with Acc1 at 10% alpha. |
| `SysWeaver-Tab.tabactive`, `SysWeaver-Tab.tabactive:hover` | The selected tab: Acc1 fill with background-coloured text and the default cursor. `Tab.Select` adds and removes `tabactive`. |
| `SysWeaver-TabContent` | A tab panel (created by page scripts, for example `explore/api.js`, and passed to `AddTab` as `onActivate` or `element`). It has a 1px Acc1 border with radius `--ThemeRad` and 4px padding, and is hidden (`display: none`) by default. |
| `SysWeaver-TabContent.tabactive` | The visible panel (`display: block`). `Tab.Select` toggles it. |
| `SysWeaver-TabContent.CODE` | A code/JSON panel: monospace (`--ThemeFontMono`), 80% size, 16px padding, `white-space: pre`, selectable text. `explore/api.js` adds `CODE` to its JSON and result panels. |

#### Custom properties, animations, media queries

It defines no custom properties and has no `@keyframes` or `@media` rules.
