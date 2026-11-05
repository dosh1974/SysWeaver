# core

[⬆ srcWeb](../README.md)

> The UI-less SysWeaver client core (namespace `Sw`): requests, server push messages, cross-window messaging, theme engine, layout state, page protocol, page state, formatting and translation fallbacks. A page (or another project) can use the core alone and still get server messages, InterOp, themes and requests.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../../README.md) |
| **Kind** | Embedded web assets (JavaScript source modules) |

## Purpose

These files are the source modules of the core bundle `sw/core.js` (built by `SwWeb bundle` from `sw/bundles.json`) and of the legacy bundle `common/common.js` (core + ui + compatibility layer). They are classic scripts (no ES modules), each wrapped in an IIFE with `"use strict"`, and work both concatenated in order (the bundle) and loaded one by one in the same order:

```html
<script defer src="../core/base.js"></script>
<script defer src="../core/i18n.js"></script>
<!-- ... url, storage, format, net, interop, theme, session, layout, page ... -->
<script defer src="../core/init.js"></script>
```

Normal pages load the bundle instead: `<script defer src="../sw/core.js"></script>`.

Nothing in the core renders visible UI. Where the old library showed something (server block overlay, loading overlay, error toasts) the core emits events on `Sw.Events` or calls hooks (`Sw.Page.OnLoading`) and the ui library renders.

The core is wire compatible with the old `common/common.js`: old and new windows of the same origin share one session master, exchange InterOp messages and keep the same storage keys.

## Contents

| File | Namespace | Description |
|---|---|---|
| [`base.js`](base.js) | `Sw`, `Sw.Events`, `Sw.Emitter`, `Sw.Util`, `Sw.Platform` | Namespace, event bus, async helpers, bytes / base64 / deflate, hashing (SHA-256 with pure JS fallback, SHA-1), frame loop (`Animator`), platform detection |
| [`i18n.js`](i18n.js) | translation marker globals, `Sw.I18n` | Runtime fallbacks of the translation markers (identical to the old library), placeholder formatting |
| [`url.js`](url.js) | `Sw.Url` | Site root (from the script location), cached url parameters, url helpers, `AppendTheme` |
| [`storage.js`](storage.js) | `Sw.Storage`, `Sw.History` | Never throwing local / session storage, per page state |
| [`format.js`](format.js) | `Sw.Format` | DOM free value formatting (numbers, bytes, durations, time stamps, currency, text helpers, typed values) |
| [`net.js`](net.js) | `Sw.Net` | Api calls (`Send`, `Get`), errors, redirects, includes, cache flush, memoization |
| [`interop.js`](interop.js) | `Sw.InterOp`, `Sw.Frame` | Cross window message bus (BroadcastChannel "SysWeaver"), raw frame postMessage |
| [`theme.js`](theme.js) | `Sw.Theme`, theme globals | Theme resolution, apply, derived color variables, custom themes, OS scheme following |
| [`session.js`](session.js) | `Sw.Session` | Session manager: master election, GetMessages long poll, subscriptions, server time, default reactions |
| [`layout.js`](layout.js) | `Sw.Layout` | Mobile / narrow / portrait state, `Sw*` classes and layout events |
| [`page.js`](page.js) | `Sw.Page` | Loading protocol, navigation, reload, editor / viewer lookup, interaction and scroll tracking |
| [`access.js`](access.js) | `Sw.Access`, `Sw.Page.ShowNotAllowed` | What the signed in user may do (auth tokens vs api info), guarded actions, the shared "not allowed" state |
| [`init.js`](init.js) | `Sw.Init` | Start up: url flags, theme, layout, InterOp theme messages, session (runs automatically) |

## Relationships

- **Part of:** [SysWeaver.HttpServer](../../README.md) `srcWeb`.
- **Used by:** `sw/core.js` and `common/common.js` (generated bundles), the ui library (`ui/`) and the shell (`shell/`).
- **Server contract:** `Api/application/GetMessages`, `serverTime`, `Api/GetExtensionEditor`, `Api/GetExtensionViewer`; templated `common/theme.css` defines the base colors `--ThemeBackground/Main/Acc1/Acc2`.

## Conventions

- Members are PascalCase; functions taking many optional parameters take an options object with PascalCase keys.
- Message types, storage keys and the Web Lock name are fixed by the wire contract (see "Wire contract" below).
- The translation markers may only be written as real calls with a literal first argument (the server rewrites them); never write a marker name directly followed by a parenthesis anywhere else, not even in comments.
- Lines between `//DEBUG_BEGIN` and `//DEBUG_END` are removed by the release build (used in `Sw.Net.Send`: POST is the default in debug builds, unless the web server Params `OptimizedSend` is true: then debug builds use the release GET encodings too; read from `sysweaver_optimized_send` of the templated `app/manifest.json`, one request per page, B-C5).

## API conventions

The rules for new code in the core (the ui library follows the same rules, see [ui API conventions](../ui/README.md#api-conventions)):

- **PascalCase** for every public name: namespaces (`Sw.Net`), functions and members (`Sw.Url.Get`, `Sw.Session.ServerTime()`), state
  properties (`Sw.Theme.Current`, `Sw.Layout.Mobile`) and options keys (`{ Retry, Signal, NoNavigate }`). **camelCase** for locals,
  parameters and private functions inside the module IIFEs. Event names on `Sw.Events` are lower case dotted (`theme.changed`,
  `auth.redirect`); wire message types keep their contract spelling (`Theme.Changed`, `SessionManager.Ping`).
- **Options objects** for optional parameters beyond the essential ones (`Sw.Net.Send(url, arg, opts)`); new APIs don't add boolean
  positional parameters.
- **Subscriptions return an off function** (`Sw.Events.On`, `Sw.InterOp.On`, `Sw.Theme.OnChange`, `Sw.Layout.OnChange`); a loading phase
  returns a remover (`Sw.Page.Loading`).
- **Errors** are thrown (`Sw.Net.Error`) and also emitted as events (`net.error`, `page.error`); helpers in `Sw.Storage` never throw.
- **No UI** in the core: visible reactions are core events or hooks the ui library implements.
- **Legacy names are kept**: every global of the old `common.js` lives in `ui/compat_common.js` and maps onto `Sw.*`; old member names that
  callers used stay as aliases on the new classes (`AbortHandler.raise/addListener/removeListener`); the permanent globals (the theme
  functions `getTheme`, `applyTheme`, `setTheme`, `resetTheme`, `toggleTheme`, `appendTheme` and the translation functions) keep their names
  for ever. A rename is never done in place: a new name is added and the old one stays as a documented alias.

Known inconsistencies, kept because pages and other projects call them (prefer the first form in new code):

| Inconsistency | Prefer |
|---|---|
| `Sw.Util.Clamp` / `Sw.Util.Debounce` / `Sw.Util.Throttle` are also exposed by the ui as `Sw.clamp`, `Sw.Debounce`, `Sw.Throttle`; `Sw.Format.Number` also as `Sw.FormatNumber` | the `Sw.Util` / `Sw.Format` names |
| Two ways to follow a change: `Sw.Theme.OnChange(fn)` and `Sw.Events.On("theme.changed", fn)` (same event) | `OnChange` |
| Boolean positional parameters: `Sw.Session.Subscribe(type, fn, noServer)`, `Sw.Format.Uptime(date, blankIfNeg, invert, now)`, `Sw.Format.DurationMs(ms, blankIfNeg)`, `Sw.Net.IncludeJs(baseUrl, file, noThrow)`, `Sw.Page.Reload(flush, clearHash)` | name the argument in a comment at the call site |
| Result shapes: `Sw.Format.TimeStamp(...)` returns `[tooltip, text]`, `Sw.Format.Value(...)` returns `{ Kind, Text, Title }`; `Sw.Net.IncludeJs` resolves lower case keys `{element, error, errorEv, loaded}` (the old result) | as documented |
| `Sw.InterOp.AddListener` / `RemoveListener` (raw, old style) next to `On` / `Off` (typed) | `On` / `Off` |
| `Sw.Platform.MobileParams()` returns an array `[isMobile, singleColumn, isPortrait, scale]` | `Sw.Layout` state |

## API reference

### Sw (base.js)

| Member | Description |
|---|---|
| `Sw.Version` | Version string of the core. |
| `Sw.Debug`, `Sw.Log(...args)` | Diagnostic logging: `Sw.Log` writes to the console only when `Sw.Debug` is true (default false; on when `localStorage["SysWeaver.Debug"]` is "1" or "true", read at load). The core logs session, server time and theme diagnostics this way; warnings and errors always use `console.warn` / `console.error`. |
| `Sw.Emitter` | Class: `On(name, fn)` → off function, `Once`, `Off(name, fn)`, `Has(name)`, `Emit(name, ...args)` → results (sync, exceptions logged), `EmitAsync`. |
| `Sw.Events` | The global emitter. Core events: `server.block` {Type, Title, Text, Icon}, `server.unblock` {Type}, `server.message` (msg), forced server types by name (`user.login`, `filereload`, ...), `net.error` {Url, Status, Error}, `auth.redirect` {Url}, `theme.changed` {Theme, IsDark}, `layout.changed`, `page.loading`, `page.loaded`, `page.error` {Error}, `page.interacted`. |

### Sw.Util (base.js)

| Member | Description |
|---|---|
| `Delay(ms)`, `DelayFrame()` | Promise delays. |
| `DelayAbort(ms, abort)` | Resolves true if aborted (`AbortHandler` or `AbortSignal`). |
| `AbortHandler` | Class: `Raise()`, `On(fn)`, `Off(fn)` (plus the old `raise/addListener/removeListener`). |
| `WaitEvent(target, names, timeout, beforeFn)` | First of one or more events, null on time out. |
| `Debounce(fn, ms)`, `Throttle(fn, ms)` | Rate limiting wrappers (`Debounce(...).Cancel()`). |
| `Clamp(v, min, max)`, `Random(min, max)`, `RandomInt32()`, `RandomGuid()` | Math / random. |
| `ToBytes`, `Utf8Encode`, `Utf8Decode`, `ConcatBytes(arrays)`, `ReadAll(stream)` | Bytes. |
| `Base64Encode(bytes, uriSafe)`, `Base64Decode(str)`, `Base64UrlSafe(str)`, `Base64EncodeString(s)`, `Base64DecodeString(s)`, `ToHex(bytes)`, `ToBase64Async(data, keepUri)` | Encodings. |
| `Deflate(bytes)`, `Inflate(bytes)` | Raw deflate (as used by the server for `_d` arguments and `Content-Encoding: deflate`). |
| `Sha256(data)`, `Sha256B64(data)`, `HashString(s)`, `Sha1(data)`, `Sha1B64(data)` | Hashing (SHA-256 falls back to pure JS in insecure contexts). `Sha256Sync(bytes)`: the synchronous pure JS SHA-256 (the old `legacySha256`). |
| `RemoveNulls(o)`, `Resolve(path, ctx)` | Json / object helpers. |
| `Animator.Add(fn)`, `Animator.Remove(fn)` | Shared frame loop; `fn(elapsedMs)` returns true to stop. |

### Sw.Platform (base.js)

`IsMobileDevice`, `IsTouch`, `IsMac`, `IsIos`, `IsAndroid`, `IsWebView2`, `IsCef`, `Name` (computed once), `MobileParams()` → `[isMobile, singleColumn, isPortrait, scale]`, `GetPosition(lowPrecision, timeout, maximumAge)`.

### Translation (i18n.js)

| Member | Description |
|---|---|
| marker globals | The four translation markers (formatting, HTML formatting, plain, HTML plain): runtime fallbacks identical to the old library. |
| `Sw.I18n.FormatArgs(format, args)`, `Sw.I18n.Format(format, ...args)` | `{n}` placeholders, `{_n}` lower case, `{^n}` upper case, `{$n}` lower case unless at position 0; undefined arguments keep the placeholder; function arguments are called. |
| `Sw.I18n.ArgAsHtml(value, cls, tag)` | Argument rendered as an HTML element (default `b`). |
| `Sw.I18n.FormatMaps`, `Sw.I18n.Language()` | Case formatters; page language (html lang). |

### Sw.Url (url.js)

| Member | Description |
|---|---|
| `Root`, `Common`, `Script` | Site root (with prefix, ends with "/"), root + "common/", core script url. |
| `Params()`, `ParamsOf(search)` | Url parameters with lower-cased keys (current url is cached). |
| `Get(name)`, `Has(name)`, `GetNumber(name, def)`, `GetInt(name, def)` | Current url parameters (case insensitive). |
| `IsAbsolute(url)`, `Absolute(url, base)`, `IsSameOrigin(url)`, `Join(...parts)`, `AddParam(url, name, value)`, `AppendTheme(url)` | Helpers. `AddParam` and `AppendTheme` put the parameter before any `#hash` part (`a.html#h` -> `a.html?useTheme=dark#h`); `AppendTheme` replaces a `usetheme` parameter already in the url (any case) instead of adding a second one. |
| `SiteUrl(url, base)`, `SitePath(url, base)` | The one rule for a site url in an url parameter (`q` of `explore/table.html` and `explore/api.html`): a value starting with `./`, `../`, `/`, `\` or a scheme resolves like a link on the page (against `base`); any other value (`Api/debug/ApiTable`, the form the server lists apis in) is relative to the site root. So `../Api/x` and `Api/x` are the same url. `SiteUrl` returns the absolute url (null when empty / invalid; not a security check), `SitePath` the decoded path below the site root without query (`Api/debug/ApiTable`, the server's api key), or null when the url is not a http(s) url of this site. |
| `SafeSameOrigin(url, base)` | Guard for untrusted urls (url parameters, deep links, messages): the absolute url when it is a http(s) url of this site, else null (other sites, `//host`, `javascript:`, `data:`, `blob:`, ...). Use it before fetching, framing or navigating to such an url (table `q`, chart `q`, shell deep links). |
| `IsHttp(url, base)`, `IsScript(url)` | The resolved url is http(s); the url runs script (`javascript:` / `vbscript:`). |
| `IsPageUrl(url, base)` | The url is a page or file that may be shown as a page, not a raw server method (a GET of a method may perform an action, ex: `ServerManager/KillProcess`): a path ending in `/`, a file name with an extension, or an `explore` folder listing; under `Root + "Api/"` only `.html` / `.htm`. The extension is tested on the raw (undecoded) path, and a path with an encoded delimiter (`%3F`, `%23`, `%26`, `%2F`, `%5C`, `%2E`, `%3B`, any case), a double encoding (`%25`) or an encoded control character is never a page (the server decodes before routing: `KillProcess%3F.html` would run `KillProcess`). Used by the shell (`SafeContentUrl`), `Sw.Page.Navigate` and MapView. Not a server side protection. |

### Sw.Storage, Sw.History (storage.js)

| Member | Description |
|---|---|
| `Sw.Storage.Local`, `Sw.Storage.Session` | `Get(key, def)`, `Set(key, value)` (null removes), `Remove(key)`, `GetJson(key, def)`, `SetJson(key, value)`; never throw. `Sw.Storage.Get/Set/...` = Local. |
| `Sw.History.Get(key, def)`, `Set(key, value)`, `Update(fn)`, `Clear()`, `ClearOther(url)` | Page state: `history.state` in the top window, `sessionStorage[location.href]` in frames. |

### Sw.Format (format.js)

`Number(v, decimals, thousandSep, decimalSep)` (never exponent notation: `Number(8.6e-7, 8)` = "0.00000086", `Number(1e21)` = "1 000 000 000 000 000 000 000"; `Value` and the ui Number cell raise the decimals of a tiny non zero value to two significant digits), `Amount(currency, amount, options, decSep, thouSep)`, `CurrencyOptions`, `Suffix(v, suffix, emptySuffix, k, decimals, prefixes)`, `Bytes(v, decimals)` ("12.3 KB", "63.4 GB"), `ByteSpeed(v, decimals)` ("1.2 MB/s"), `DurationMs(ms, blankIfNeg)`, `TimeSpan(v, zeroText)`, `Uptime(date, blankIfNeg, invert, now)`, `TimeStamp(date, onlyDate, prefix, tab, fmt)` → `[tooltip, text]`, `IsoText(date, onlyDate)`, `LocalToUtc(d)`, `UtcToLocal(d)`, `StringFormat`, `StringFormatArgs`, `CountSuffix`, `RemoveCamelCase`, `KeyValueSplit`, `JoinNonEmpty`, `AddNonNullLine`, `HtmlSafe`, `HtmlAttributeSafe`, `JsonToHtml`, `RankScoreMatch`, `IsValidEmail`, `IsValidPhone`, `IsLetter`, `FileExtension`, `Ext` (Image, Video, Audio, Programming, Compress), `IsImageExt`, `IsVideoExt`, `IsAudioExt`, `IsProgrammingExt`, `IsCompressibleExt`, `IsImageFile`, `IsVideoFile`, `DecimalTypes`, `TypeKinds`, `Value(type, value, format, nextValue)` → `{Kind, Text, Title}` (Default, Number, Boolean, DateTime, Duration, ByteSize, ByteSpeed, Uptime).

### Sw.Net (net.js)

| Member | Description |
|---|---|
| `Send(url, arg, opts)` | Api call with one argument (`$type` first). GET with `?json`, `?_d<base64url deflate>` or `?_u<base64url utf8>` when small enough, else POST (deflated when worth it). Options: `Reload`, `Post` (force POST / allow GET), `Blob`, `Text`, `Read(res)`, `ErrHandler(status)` (true = retry), `Setup(request)`, `Signal`, `Retry` (network errors, 502/503/504), `NoNavigate`. Returns the decoded json (undefined if not decodable). A redirect (not signed in) never returns null: see Redirects below. |
| `Get(url, opts)` | GET (url may be a Request). Same options except Post / ErrHandler. |
| `Error` | `Sw.Net.Error` (Status, StatusText, Text, Url); message "status - statusText" (+ the body of a 500); a 403 has `Denied` true (`Sw.Access.IsDenied`). |
| `Beacon(url, data)`, `Download(url, name)`, `DownloadText(name, text)`, `FlushCache(url)` | Fire and forget post, downloads, cache re-validation. |
| `IncludeJs(baseUrl, file, noThrow, opts)`, `IncludeCss(...)` | Load once; `file` relative to the folder of `baseUrl` (null = `common/`); resolves `{element, error, errorEv, loaded}`. A file the page already loads with a static `<script src>` / `<link>` tag is not loaded again (scripts resolve after the window load event). **Site urls only** (offline rule): a resolved url of another site (from `file` or `baseUrl`) is refused with the error result and a console warning, nothing is requested; a deliberate, documented online feature opts in with `opts = { AllowOnline: true }` (ex: `Sw.Net.IncludeJs(null, "https://maps.example.com/api.js", false, { AllowOnline: true })`; `SwWeb check` flags such literal urls). data urls count as local. |
| `Cached(name, key, lifeTimeMs, fetchFn)`, `CachedGet(url, ms)`, `CachedSend(url, arg, ms)` | Memoization. |
| `ToTypedJson(data, type)`, `Api(path)` | Serialization; `Root + "Api/" + path`. |

Not signed in: an api call without a signed in user is answered with a 401 and the sign in page in the `X-Login-Url` header (CR-S18; older servers and urls that are not apis answer with a 302 to `auth/Login.html`, a redirected response). Both mean "not signed in" (the error has `SignIn` true; `Redirect` true only for a 302; a 401 without `X-Login-Url` is a plain 401 error): it emits `auth.redirect` and navigates there (`Sw.Page.Navigate(url, "_self")`, only to a http(s) url of this site). The call does not settle while the page leaves (callers never see a null result); if the page is still there after 15 s it rejects with a `Sw.Net.Error` (`Status` 302, `Redirect` true, a "not signed in" message). With `NoNavigate` that error is thrown at once. A SIGNED IN user is never sent to the sign in page: when `Sw.Access.User(true)` says signed in, the redirect means "not allowed for this role" and the call throws a `Sw.Net.Error` with `Status` 403, `Denied` and `Redirect` true.

### Sw.InterOp, Sw.Frame (interop.js)

| Member | Description |
|---|---|
| `Sw.InterOp.Id` | This window's id (number). |
| `Sw.InterOp.Post(type, data, isFromServer)` | Post to all same-origin windows including this one (envelope `{Type, From, MagicXyz:"SysWeaver"}`). |
| `Sw.InterOp.On(type, fn)` → off, `Off(type, fn)` | Typed handlers (exact type, `"*"` = all). |
| `Sw.InterOp.AddListener(fn)`, `RemoveListener(fn)`, `GetMessage(ev)` | Raw listeners (old style). The window message fallback (no BroadcastChannel) drops messages from other origins (`GetMessage`). |
| `Sw.Frame.PostParent(name, data, targetOrigin)`, `PostTop`, `PostAll`, `On(type, fn, anyOrigin)` | Raw `{Type, ...}` postMessage between frames (PageLoaded, LoaderRemoved, IframeNavigating...). Security: `targetOrigin` defaults to this page's origin (a foreign window framing the page receives nothing); pass `"*"` explicitly only for a host of another origin, never with secrets. `On` passes on same-origin messages only unless `anyOrigin` is true. |

### Sw.Theme (theme.js) and the theme globals

| Member | Description |
|---|---|
| `Get()`, `Apply()`, `Reapply()`, `Set(name)`, `Reset()`, `Toggle()` | Resolution: forced → `localStorage["SysWeaver.Theme"]` → OS scheme (mapped to the app themes named by `--sw-theme-light` / `--sw-theme-dark` in `app/app.css`, remembered in `localStorage["SysWeaver.ThemeAuto"]`) → "dark". `Set/Reset` post `Theme.Changed`. A name may be `name@css`. Apply sets `data-theme` and `data-sw-scheme` (`light` / `dark`: the registered `Scheme`, else the css `color-scheme`, else the background lightness). |
| `Forced`, `Current`, `IsDark`, `StorageKey` | State. |
| `Register(nameOrTheme, def)`, `GetRegistered(name)`, `List()`, `Custom()` | Custom themes `{Name, Title, Description, Scheme, Colors: {Background, Foreground, Accent1, Accent2}, Base}` (lower case names; without `Colors` the theme is `[data-theme="name"]` css, ex: in `app/app.css`). They reach every page: unknown names are looked up in the top window (the shell runs `app/app.js`), then in `localStorage["SysWeaver.ThemeDefs"]` (written when a registered theme is applied). Registering the applied theme re-applies it. `Custom()` = the ones with a `Title` (the shell lists them in its settings menu). |
| `BrandReady`, `IsDark` | Promise: the brand stylesheet `app/app.css` was applied to this page (linked render blocking by index.html, app/Home.html, app/Welcome.html and auth/Login.html; else fetched once and adopted as the last style sheet only when it has content; the server serves it empty, so no 404) and the theme re-applied. No frame of the default brand: what a page found is remembered per site in `localStorage["SysWeaver.ThemeBrand"]` (`{Has, Css}`, css up to 1 M characters) and the first `Apply()` of the next page adopts it before the first paint (replaced by the fetched text when it changed; kept when offline); while nothing is known (first visit) a page without the link stays hidden until the brand was applied, at most 3 s. `IsDark`: the applied scheme. |
| `OnChange(fn)`, `WatchOs()`, `Animations`, `UpdateDerived()` | Change events, OS scheme following, per theme animations, derived variables. |
| Globals | `getTheme`, `applyTheme`, `setTheme`, `resetTheme`, `toggleTheme`, `appendTheme` (used by the server Theme menu). |

Derived variables written on `html` for `--ThemeBackground/Main/Acc1/Acc2`: `R`, `G`, `B`, `RGB`, `25`, `50`, `75` suffixes.

### Sw.Session (session.js)

| Member | Description |
|---|---|
| `Subscribe(type, fn, noServer)`, `Unsubscribe(type, fn, noServer)` | Message handlers (lower-cased types) + ref counted server subscription. |
| `AddEvent(name)`, `RemoveEvent(name)`, `Events()` | Server subscriptions only. |
| `ServerTime()`, `ServerTick()`, `TimeOffset` | Latency corrected server time. |
| `IgnoreUserChanges()`, `IsIgnoringUserChanges` | No automatic reloads on user / reload / refresh messages. |
| Master election | Lost master (crashed / killed tab, no `MasterClosed`): the new master posts `SessionManager.GetEvents`; every window and frame answers `SessionManager.OwnedEvents {To, Events: [[name, count]]}`; Add/RemoveEvents of windows that did not answer yet are held back up to 2 s (B-C1). `server.reconnect` reloads only a window that knew the previous instance (a new tab learns it from the master: `TimeOffset.Si`; B-C6). |
| `IsMaster`, `IsStarted`, `Blocked`, `ForcedTypes` | State. |
| `Init()`, `InitReactions()` | Called by `Sw.Init`. |

### Sw.Layout (layout.js)

`Mobile`, `Portrait`, `Narrow`, `SelfPortrait`, `SelfNarrow`, `Width`, `Height`, `IsChild`, `IsTransparent`, `NarrowWidth` (600), `Init()`, `Update(force)`, `OnChange(fn)`. Classes on `html` and `body`: `SwMobile`/`SwDesktop`, `SwPortrait`/`SwLandscape`, `SwNarrow`/`SwWide`, `SwSelfPortrait`/`SwSelfLandscape`, `SwSelfNarrow`/`SwSelfWide`, `Transparent`, `FillScreen`. Body events (detail = boolean): `SwOrientation`, `SwPortrait`, `SwLandscape`, `SwLayout`, `SwNarrow`, `SwWide` and the `SwSelf*` equivalents.

### Sw.Page (page.js)

| Member | Description |
|---|---|
| `IsTop`, `IsEmbedded`, `DidInteract` | State. `IsTop`: the top window, a frame of another origin, or a frame started with the url flag `top=true` (CH-B1: a frame whose same origin host page does not load the SysWeaver core; it then runs its own session manager like a top window, see Sw.Session). |
| `Loading(opts)` → remover(noEvents), `OnLoading(hook)` | Loading phase; the ui adds the visual through the hook. Url flag `noloader` skips the hooks. |
| `SignalLoaded()`, `Loaded()` | `PageLoaded` to parent, `LoaderRemoved` to top; `Loaded` also posts InterOp `WindowLoaded` and restores the scroll position. |
| `Main(fn, opts)` | Run the page main after DOM ready (`opts.Loading`: show a loading phase; a refused request, 403, shows the shared "not allowed" state `ShowNotAllowed` instead of a blank page, `opts.NotAllowed: false` opts out). |
| `Navigate(url, target)`, `Replace(url)`, `Reload(flush, clearHash)` | Navigation (`IframeNavigating` protocol for `_self`, only for http(s) urls of this site; `Replace` = redirect without a history entry). Security (the url is untrusted: url parameters, table links): another site never replaces this, the parent or the top window: every target but `_blank` (`_self`, `_top`, `_parent` and named targets, which may name this window or the shell) opens it in a new tab with `noopener` (inside the app shell, `_self` with the exact url of an external `Embedded` menu item is shown in the shell); `_blank` uses `noopener`. Target keywords are matched case-insensitively like browsers do (`_TOP` = `_top`). The url is converted with `String()` first (an Array or a String object can't skip the checks) and only the parsed href is opened. Script and data urls (`javascript:`, `vbscript:`, `data:`) are refused for every target, `_blank` too; a `blob:` url only of this origin and only for `_blank` (with `noopener`); inside the app shell a raw server method of this site (not `Sw.Url.IsPageUrl`, ex: `Api/...`, `ServerManager/KillProcess`) never replaces the content frame or the shell: with any target but `_blank` it opens in a new tab with `noopener`; other non http(s) schemes always (`mailto:`, `tel:`, `sms:`, `callto:` are allowed); a refusal logs a warning, shows a toast (ui library) and is announced. |
| `Target(target, fallback)` | Normalize a window target: `_self`, `_blank`, `_top`, `_parent` in any case become lower case, other names are kept, empty gives `fallback` (default `_self`). Compare targets only after this (`Navigate`, `Sw.Cells.CreateLink` use it). |
| `Announce(text)` | Read a text to screen readers (a visually hidden polite live region, `div.sw-page-live`), ex: next to a toast for a refused link or a failed page change. The text is cleared after about 7 s (no stale message at the end of the page). |
| `EnsureHeading()` / `AutoHeading` | Called by `Loaded`: a page without an `h1` (or `role=heading` level 1) gets a visually hidden `h1.sw-page-heading` with the page name: the menu name the app shell shows when embedded in it, else the document title with an upper case first letter (first element of `main`, else of `body`; follows title changes; removed as soon as the page adds its own `h1` and added again when the page removes it later: the body stays watched). A title set by `SetLinkTitle` is named "Text from the link: <title>". Opt out: `Sw.Page.AutoHeading = false` before `Loaded`, or `data-sw-no-heading` on `html` / `body`. |
| `LinkText(text, max)`, `LinkCaption()`, `SetLinkTitle(text)`, `TitleFromLink` | Untrusted text from the url (a title or name parameter): `LinkText` makes it plain text (control, bidi and invisible formatting characters removed, white space collapsed, cut to `max` = 80 characters with "…"); `LinkCaption()` is the translated "Text from the link"; `SetLinkTitle(text)` sets the document title to `LinkText(text, 80)` and remembers it in `TitleFromLink`, so the app shell shows it after a "Text from the link" caption (unless it is the menu name of the page). Set `TitleFromLink = null` before setting a title the server confirmed (explore/api.html, edit/type.html). Used by explore/table.html `n`, explore/api.html and edit/type.html `q`. |
| `NameFromUrl(url)`, `SetName(name)` | A page whose data never arrived (refused, not found, failed) is named after its data instead of its generic file title ("Table"): `NameFromUrl("../Api/debug/ServicesTable")` = "Services" (last segment without a `Get` prefix and a `Table` suffix, camel case split); `SetName(name)` sets the document title (the shell shows its menu name when the url has one, else this title) unless a title from the link is set. |
| `AppName()` | Promise of the application display name for pages that are not server templates (ex: app/Welcome.html): the app shell window title (`SysWeaverAppTitle`, its templated index.html) when shown in the shell, else `name` of the templated `app/manifest.json` (memoized; white space collapsed; "" when unknown). |
| `Edit(read, target, save, remove)`, `View(read, target)`, `EditOrOpen(...)` | Open the registered editor / viewer page. |
| `RestoreScroll()`, `TrackInteraction()`, `TrackScroll()` | Used by `Sw.Init`. |

### Sw.Access (access.js) - what the user may do, and the shared "not allowed" state

| Member | Description |
|---|---|
| `User(reload)`, `Tokens()` | The signed in user (`Api/auth/GetUser`, null when not signed in), its lower cased tokens; cached 30 s. |
| `Check(auth, tokens)` | The server rule for an auth text (`ApiInfo.Auth`, `WebApiAuth`): null = open, `""` = any signed in user, `"-"` = no one, else at least one of the comma separated tokens. |
| `Can(what)` | Promise true / false for an ApiInfo (`{Auth}`), an auth text or a token list. |
| `ApiInfo(url)`, `CanCall(url)` | The auth of a server method for the signed in user (`Api/auth/CanCall`, CR-S18a: `{Uri, Auth, Allowed}`, the urls asked in the same task go in one request, cached 60 s; older servers: the api info of `Api/debug/GetApiInfo`, see `LegacyApiInfo`), null when unknown; and whether the user may call it (true / false / null = unknown; the server's `Allowed` when known). |
| `HasApiInfo()` | Promise true when the server has `Api/auth/CanCall`. Older servers: true / false: asks once when nothing asked yet (one `GetApiInfo` request, shared with `ApiInfo` callers), so it is never a guess; false once the server refused `GetApiInfo` for this user (403, sign in redirect, 401, 404); null when it can't tell (ex: no connection). See "Dependency on GetApiInfo" below. |
| `RoleText(auth)`, `Requires(auth)` | "Admin or Ops" (Debug left out unless alone); "Requires Admin" (unknown: "Your role doesn't allow this"). |
| `IsDenied(e)` | A refused request: `Sw.Net.Error` with `Status` 403 (or `Denied` true). |
| `Guard(target, {Url, Auth, Hide})` | Disable (or hide) a `Sw.Button` / element the user may not use, tool tip "Requires Admin". Requirement from `Auth` or the api info of `Url`; when unknown the target is disabled after the first `access.denied` of that url (any url without `Url`). |
| `Disable(target, text, hide)`, `ActionDenied(e, target)` | Disable now with a reason (class `sw-denied-action`); a refused action: toast naming the role + target disabled (`Sw.Button` does this for a 403 rejection of `OnClick`). |
| `Denied({Url, Error})` | Emits `Sw.Events` `"access.denied"`. |
| `NotAllowed(opts)` | Creates the state element `div.sw-denied` (role alert: lock icon, "You don't have access to this page", "You are signed in as dev.", "This page requires the role Admin or Ops ..." or "Your role doesn't give access to it.", "Ask an administrator ...", a "Go to Home" link: the shell's top window when embedded). No "Try again": a refusal is not transient. A part of a working page (`Scope: "section"`, the default with `Element`) says "You don't have access to this part of the page" / "It requires the role ..." and has no Home link; `Name` names what is refused ("You don't have access to Users"). |
| `NotFound(opts)` | Creates the "not found" state `div.sw-denied.sw-notfound` (role status: search icon, "Not found" / "Services was not found", 'The server has nothing at the address "Api/x/MissingTable" ...', an optional `Hint`, `Links` `[{Text, Url, Icon}]` (default for a page: "Go to Home"); `Title` / `Text` replace the heading / first line). No "Try again": a missing page or table is not transient. |

`Sw.Page.ShowNotAllowed(opts)` (also in access.js): shows that state. `opts`: a reason text, or `{Url, Auth, Reason, Error, Name, Scope, Element}`; without `Element` it replaces the page (the body's children are hidden, so no page action can be used), with `Element` it replaces that element's content. A page scope state with a `Name` also names the page (`Sw.Page.SetName`: the document title and the shell title, ex: "Services" instead of "Table"). It emits `access.denied` and signals "loaded" if the page did not yet; the state is announced once (role alert, no second live region copy). `Sw.Page.ShowNotFound(opts)` shows the not found state the same way (`{Url, Name, Title, Text, Hint, Links, Scope, Element}`; no event); explore/table.js uses it for an unknown table url (404), with the `q` rule and "Show all data tables". Used automatically by `Sw.Page.Main` (a 403 thrown by the main function), `Sw.Table` (explore/table.js), charts (chart/chart.js), the text viewer (edit/text.js, log file), explore logo/icons pages. Styles: `ui/components.css` (`.sw-denied*`).

**Dependency on GetApiInfo (older servers).** Servers with `Api/auth/CanCall` (CR-S18a) answer the role and the permission of any asked url for the current session, `GetApiInfo` (now Dev and Debug only) is then not used by `Sw.Access` (only by the api inspector page). On an older server the first CanCall request gets a 404 (one per page) and `Sw.Access` falls back to `Api/debug/GetApiInfo`: If a server locks it down (protected, Debug only, or no type service), `Sw.Access` degrades without errors: the first refusal (403, a sign in redirect, 401 or 404) is the only request of the page (concurrent callers wait for it), it is remembered for that user in the browser session (sessionStorage `SysWeaver.Access.NoApiInfo`), `ApiInfo` / `CanCall` return null, `Guard` leaves its target usable and disables it after the first refused request of its url ("Your role doesn't allow this"), and the not-allowed state says "Your role doesn't give access to it." instead of naming the role. A page never breaks; it only loses the role names and the "disabled before the first click". Servers with `Api/auth/CanCall` don't have these limits.

Example:
```js
//  Hide an action the role can't use (requirement read from the api info)
Sw.Access.Guard(deleteButton, { Url: "../Api/logFile/DeleteLogFile" });
//  A page that loads its data itself
try { data = await Sw.Net.Send("../Api/x/Get", null); }
catch (e) { if (Sw.Access.IsDenied(e)) return Sw.Page.ShowNotAllowed({ Error: e }); throw e; }
```

### Sw.Init (init.js)

Runs once automatically. Url flags: `settheme`, `usetheme`, `css` (only a stylesheet of this site, `Sw.Url.SafeSameOrigin`; others are ignored with a warning), `colorscheme`, `ignoreuserchanges`, plus `mobile`, `transparent`, `fillscreen` (Sw.Layout) and `noloader` (Sw.Page.Loading). Url flag `top=true`: this frame acts as a top window (`Sw.Page.IsTop`, own session manager; CH-B1). InterOp messages handled: `Theme.Changed` (all windows; a theme forced by the `usetheme` url flag is kept, B-C3), `color.scheme`, `theme.set`, `theme.use` (frames started with the matching flag).

## Wire contract (kept identical to the old library)

- BroadcastChannel `SysWeaver`, envelope `{Type, From, MagicXyz: "SysWeaver", To?}`; `From` = sender id or `"Server"`.
- Session: `SessionManager.Ping`, `.Pong {To}`, `.MasterClosed {Cc, ServerEvents}`, `.AddEvents {Events}`, `.RemoveEvents {Events}`, `.TimeOffset {O, To?}`, `.GetTimeOffset`, `.GetEvents`, `.OwnedEvents {To, Events}` (new, B-C1), `.TimeOffset` may carry `Si` (server instance, B-C6); Web Lock `SysWeaver.SessionManager`; client generated `server.error` / `server.restore`.
- Server: `Api/application/GetMessages` `{Cc, MessageTypes}` → `{Cc, Prefix, Messages}` or null; `serverTime[?tz;lang]`.
- Theme: `Theme.Changed`, `color.scheme`, `theme.set`, `theme.use`.
- Frames: `PageLoaded`, `LoaderRemoved`, `IframeNavigating {Url, Id}` (answered by the shell with `IframeNavigationHandled {Id, Url, Handled}`, B-C4), InterOp `WindowLoaded`.
- Storage: `localStorage["SysWeaver.Theme"]`, `localStorage["SysWeaver.ThemeDefs"]` and `["SysWeaver.ThemeAuto"]` (per site root), `localStorage["SysWeaver.Mobile"]`, `localStorage["SysWeaver.Debug"]` (client only, `Sw.Debug`), `sessionStorage["SysWeaver.ServerInstance"]`, page state keys `ScrollX`, `ScrollY`.
