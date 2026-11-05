# Web messaging: server events, InterOp, themes and the automatic classes

[⬆ _docs](README.md)

> How a SysWeaver web page talks to the server and to the other windows of the site, as implemented by the `Sw` core: the session (the GetMessages long poll and its master election), the server events and their default reactions, the server time, InterOp (cross window messages) and the frame protocol, the theme hand-shake (`?setTheme`, `?useTheme`) and the automatic html / body classes. The old `common/common.js` started all of it with `SessionManager.Create(); SysWeaverInit();`; the core does the same, wire compatible, and a page that renders its own UI gets it from the small bundle `sw/session.js`.

| | |
|---|---|
| **Repository** | [SysWeaver](../README.md) |
| **Kind** | Developer reference |

Related: [core/README](../SysWeaver.HttpServer/srcWeb/core/README.md) (the api of `Sw.Session`, `Sw.InterOp`, `Sw.Theme`, `Sw.Layout`), [WebQuickStart](WebQuickStart.md) ("A page with its own UI: messaging only"), [Web.md](Web.md).

## Contents

- [1. Which script](#1-which-script)
- [2. Start up](#2-start-up)
- [3. The session: GetMessages](#3-the-session-getmessages)
- [4. Server events and the default reactions](#4-server-events-and-the-default-reactions)
- [5. Subscribing](#5-subscribing)
- [6. Server time](#6-server-time)
- [7. InterOp and the frame protocol](#7-interop-and-the-frame-protocol)
- [8. Themes](#8-themes)
- [9. The automatic classes](#9-the-automatic-classes)
- [10. Legacy names](#10-legacy-names)
- [11. Differences from the original](#11-differences-from-the-original)

## 1. Which script

| What the page is | Scripts | Messaging | Theme | Components |
|---|---|---|---|---|
| A page with its own UI | `sw/session.js` | yes | no (the requested theme is known: `Sw.Session.RequestedTheme`) | no |
| A page with its own UI and the SysWeaver theme | `sw/session.js` + `sw/theme.js` | yes | yes | no |
| ... that signs in or calls apis with `Sw.Net` | + `sw/net.js` (+ `auth/Auth.js`) | yes | as above | no |
| A new format page | `sw/core.js` + `sw/ui.js` | yes | yes | yes |
| An old format page | `common/common.js` | yes | yes | yes (legacy names) |

Every JavaScript bundle is built from the same source files with a load once guard per source file (SwWeb `"LoadOnce"`, all of them: `SwWeb bundle` refuses a bundle that shares a source with a load once bundle without being one): a page that loads `sw/session.js` and later `sw/core.js` runs every shared file once, and an old page with `common/common.js` that adds `sw/ui.js` (or `sw/shell.js`, `app/application.js`) runs the ui library once (`SwWeb check` warns that the include adds nothing, rule `bundle-overlap`). `sw/session.js` is about 208 KB raw / 52 KB gzip (2026-10-27; the doc comments are kept; its budget in `sw/bundles.json`, checked by `SwWeb bundle --check`, is 218 / 54 KB), `sw/theme.js` 54 / 14 KB; the original `common.js` was 375 / 90 KB.

**Script order**: `sw/theme.js` may come before or after `sw/session.js` (a classic `<script>` before a deferred `sw/session.js`, or `async` scripts): run first, it starts when the messaging core starts (`Sw.AfterSession`); a page without `sw/session.js` / `sw/core.js` gets a console error when it has loaded. `sw/net.js` and `auth/Auth.js` come after `sw/session.js` (`Auth.js` checks at load).

## 2. Start up

When the script is evaluated (no call needed; `SysWeaverInit()` (a Promise, as the original) and `SessionManager.Create()` exist and start nothing more):

1. **Url flags**: `colorscheme=<v>` adds `<meta name="color-scheme" content="v">` (a frame started with it follows the InterOp message `color.scheme` {Value}); `ignoreuserchanges=true` = `SysWeaverIgnoreUserChanges()`; `mobile`, `transparent`, `fillscreen` (the classes, section 9); `top=true` (a frame acts as a top window: its same-origin host does not load SysWeaver).
2. **The requested theme** (section 8): `usetheme`, `settheme`.
3. **The automatic classes** (section 9).
4. **The session** (section 3): the default reactions, then the master election. Not with `<html data-sw-no-session>` (a page that had no session in the original: no `serverTime`, no `GetMessages`).
5. With `sw/theme.js` / `sw/core.js`: the theme is applied (`css=<url>`: an extra stylesheet of this site).
6. With `sw/core.js`: page state on a browser reload, interaction and scroll tracking, the app script `app/app.js`.

InterOp messages are buffered from the moment the script runs until this window knows whether it is the master.

## 3. The session: GetMessages

**One poller per browser and site.** Of all the top windows (tabs) of a site, one is the **master**: it long-polls the server and relays every server message to all windows. The others are **children**. A frame (a same-origin window that is not the top) is never master, unless it was started with `top=true`.

**Election** (Web Lock `SysWeaver.SessionManager` around it when the browser has Web Locks): the window posts the InterOp message `SessionManager.Ping` and waits 100 ms for `SessionManager.Pong {To: its id}`. No answer: it becomes master. Else it is a child and tries again every second. When the waiting window was itself busy (its own start up: the timer came late), it waits another 100 ms for the answer, up to 10 times, so a busy page does not become a second master.

**Exactly one master.** The Ping / Pong alone cannot tell a missing master from a slow one: a master tab that is busy or throttled (a background tab, a loaded machine) may answer after the 100 ms. So the master holds a second Web Lock, `SysWeaver.SessionManager.Master`, for as long as it is the master: a window without a Pong in time becomes master only when it gets that lock at once (`ifAvailable`); else a master exists and it stays a child. After `MasterClosed` the taking-over window waits up to 1 s for the lock, which the closing master releases right after posting `MasterClosed` (also when it goes into the back/forward cache). A crashed or killed tab's lock is released by the browser; a frozen master (page lifecycle `freeze`) releases it too, and on `resume` steps down if another window took over. The messages, their order and the 100 ms are unchanged. Without Web Locks (an http page on a host other than localhost: Web Locks need a secure context) the old election applies, with these safeguards: a master that gets a Pong addressed to it (the late answer to its election Ping) steps down (its poll ends, the other master keeps polling; it rejoins as a child), and a window that saw a late Pong waits twice that long (at most 1 s) in later elections. Two masters can still happen (every child gets the same `MasterClosed` at the same moment and nothing serializes them), so without Web Locks: a child that takes over after `MasterClosed` first waits a random 0-300 ms (the first one's Pong keeps the others children), and the master Pings every second; a master answers a Ping with `{To, Ms}` (`Ms`: when it became master), and of two masters the younger steps down (equal: the higher id), so two masters heal within about a second (OWN3-Down-3, AS6b-1; an old `common.js` master answers without `Ms`: the new window steps down). With Web Locks none of this is sent. A `MasterClosed` that arrives while an election from the 1 s retry is running hands over its `Cc` to it.

**The poll** (master): `POST Api/application/GetMessages` with `{"Cc": <change counter>, "MessageTypes": [<subscribed types>]}`:

| Request | Server answer |
|---|---|
| `Cc` 0 (first poll) | at once: `{Cc, Messages: [{Type: "server.connect", Value: <instance>}]}` |
| `Cc` above the server's (the server restarted) | at once: `server.reconnect` {Value: instance} |
| otherwise | waits up to 90 s for messages of the subscribed types (and the forced types, section 4); `{Cc, Prefix, Messages}`, `{Cc}` or `null` (time-out: keep `Cc`) |

Every message gets `Prefix` (the server's url prefix) and is posted to all windows with `From: "Server"`. On a failure the master waits 5 s; the 3rd failure in a row posts `server.error` (and resets `Cc` to 0: the next success is a `server.connect`), the next success `server.restore`. **A restarted server is always noticed:** after any failed poll the next one asks with `Cc` 0, whose answer `server.connect` names the server instance. Another instance: the page reloads (a restart may bring a new front end; the reload happens only after the server answered, so the page loads). The same instance (a network drop, a proxy that ended the poll): the window keeps its own `Cc` (no message pushed meanwhile is lost) and nothing reloads. Before (and in the original) a server that came back before the 3rd failure was missed when its new session counter was not below the window's `Cc` (no `server.reconnect`), e.g. a page that had no message yet (`Cc` 1) (OWN3-Down-1). An empty answer that comes back at once again and again (a proxy that ends long polls) backs off 1 s .. 30 s.

**Handover**: when the master page is hidden or closed (`pagehide`) it posts `SessionManager.MasterClosed {Cc, ServerEvents: [[type, count]]}`; a child takes over with the same `Cc` and subscriptions. A master that crashed (no handover): the next master asks every window with `SessionManager.GetEvents`, and each answers `SessionManager.OwnedEvents {To, Events}` (2 s).

The requests of a page, in order: `GET serverTime?<IANA time zone>;<language>`, `POST GetMessages {"Cc":0,"MessageTypes":[]}`, `POST GetMessages {"Cc":<n>,"MessageTypes":[]}` (the long poll), `GET serverTime` after 5 s. A child sends none.

## 4. Server events and the default reactions

The server always delivers these types (no subscription needed): `user.logout`, `user.login`, `server.shutdown`, `server.pause`, `server.continue`, `server.restart`, `reload`, `refresh`, `filereload`. Other types are delivered only when a window subscribed them (section 5). `server.error` and `server.restore` are made by the client.

| Type | Where | Reaction |
|---|---|---|
| `server.connect` {Value} | top windows | a known, different server instance: reload after 100 ms (hash cleared; also on pages that ignore user changes, ex: the sign-in form shown in place of a page, S7a-A1-6); else remember it (`sessionStorage["SysWeaver.ServerInstance"]`) and unblock |
| `server.reconnect` {Value} | top windows | the same, a window that knows no instance yet never reloads |
| `user.login` | top windows | reload after 100 ms (unless `SysWeaverIgnoreUserChanges()`) |
| `user.logout` {Value: reason} | top windows | `navigator.credentials.preventSilentAccess()`, the page is blanked and reloads after 100 ms (unless ignored, or `Sw.Session.HandleSignOut(fn)` handled it) |
| `reload` / `refresh` | top windows | reload (hash cleared) / plain reload after 100 ms (unless ignored) |
| `server.pause` / `server.restart` / `server.shutdown` | top windows | `Sw.Events` `server.block` {Type, Title, Text, Icon, Time}: the page is blocked (see below) |
| `server.error` | top windows | the same block, "Server communication lost. Waiting for network or server to become available.", as the original on every page: the shell, a `common/common.js` page and a `sw/session.js` page. It comes after 3 failed polls (the first at once, then every 5 s: about 10-15 s after the server went away). In the shell the calm "Offline, retrying…" line shows as well (under the overlay). `Sw.Session.BlockOnLost = false`: no overlay, the offline line only (a page without the shell then shows nothing; it reads `Sw.Net.Online` / `net.offline` itself) |
| `server.continue` / `server.restore` / a connect of the same instance | top windows | `server.unblock` {Type}: `server.restore` comes with the first poll that succeeds again |
| `filereload` {Value: urls} | all windows | the master re-validates the urls in the browser cache and posts the message again with `DidReload`; every window refreshes the `src` / `href` / `data` attributes and inline background / mask images that reference them |
| any type | all windows | `Sw.Events` `server.message` (msg); the forced types also by name |

**The block overlay**: with the ui library (`sw/ui.js`, `common/common.js`) the designed overlay; on a `sw/session.js` page a minimal self-styled one (title, text, the elapsed time), as every page of the original. Both are small dialogs (the same texts, the elapsed time m:ss) without a reload button: the page reloads by itself when the server that answers again is a new instance, and never after a drop of the same instance. They are movable and resizable, and × / Escape hide them until the server is back, so the page can be read and copied meanwhile (OWN3-Down-1, see the table in §5). While shown, both block the input: no key, click or wheel reaches the page's own listeners (a page's keyboard shortcut never runs against a paused or lost server); only the dialog's own controls (move, resize, ×, Escape) and Tab work. While blocked, the first block stays (as the original: a restart whose polls then fail keeps "Server is restarting."). A page that draws its own listens to `server.block` / `server.unblock` (any other handler, or `info.Handled = true`, claims it); `<html data-sw-no-block>` or `Sw.Session.FallbackBlock = false`: none.

**While the server is away** the requests are the original's: the master's poll retries every 5 s and its server time sync every 15 s, and their answers decide `Sw.Net.Online` (the window that polls sends no probe of its own; a child tab is told by the master's `server.error` / `server.restore`, and checks itself every 15 s; a page without a session probes every 15 s). A request that fails while the state is still "online" sends one probe, so a single failed request doesn't show a false "offline".

## 5. Subscribing

```js
const fn = Sw.Session.Subscribe("my.type", msg => show(msg.Value));   //  lower-cased; subscribes the server type
Sw.Session.Unsubscribe("my.type", fn);
Sw.Session.Subscribe("my.local", fn, true);                            //  an InterOp message only (no server subscription)
//  legacy: SessionManager.AddMessageHandler(type, fn, ignoreServer) / RemoveMessageHandler(...)
```

Subscriptions are ref counted per window; the first (last) one posts `SessionManager.AddEvents` (`RemoveEvents`) `{Events: [type]}` to the master, which re-issues its poll with the new `MessageTypes` at once. Handlers of a type are awaited in order; an exception is logged. A closing window releases its subscriptions. The server drops messages of unsubscribed types: subscribe before you need them. The server side: `HttpSession.PushMessage`, `HttpServerBase.PushMessageUser / AllUsers / AllSessions` (`PushMessage` with `Type`, `PushMessageStringValue` {Value} ...).

## 6. Server time

The master reads `serverTime` (the first call `serverTime?<time zone>;<language>`, which tells the server the browser's zone; the answer is `<ms>` or `<ms>;<server utc offset in minutes>`), takes half the round trip as latency, and posts `SessionManager.TimeOffset {O, Z}`; a later different offset is reached in 500 ms steps. It syncs again after 5 s, doubling up to 300 s (on a failure after 2 s, then 15 s). Children and frames ask with `SessionManager.GetTimeOffset`. Api: `Sw.Session.ServerTime()` (a Date), `ServerTick()`, `TimeOffset`; legacy `GetServerTime()`, `GetServerTick()`, `window.SysWeaverServerTimeOffset`.

## 7. InterOp and the frame protocol

**InterOp** is the message bus of all windows, tabs and frames of the site: `BroadcastChannel("SysWeaver")` (same origin by construction), envelope `{...data, Type, From: <window id> | "Server", To?, MagicXyz: "SysWeaver"}`. `Sw.InterOp.Post(type, data)` also calls this window's own listeners (a channel does not echo). Without BroadcastChannel a fallback posts with `window.postMessage` to the parent chain and the windows this one opened, and accepts messages of this origin only.

```js
const off = Sw.InterOp.On("my.event", m => console.log(m.From, m.Value));    //  "*" = every message
Sw.InterOp.Post("my.event", { Value: 1 });
//  legacy: InterOp.Post(type, data), InterOp.AddListener(fn) (raw: InterOp.GetMessage(ev)), InterOp.Id
```

Message types of SysWeaver itself: the session (`SessionManager.*`), the server types (`From: "Server"`), `Theme.Changed`, `theme.set`, `theme.use`, `color.scheme`, `Density.Changed`, `WindowLoaded`, `filereload`, `SysWeaver.SignedOut`, `SysWeaver.AuthCheck`.

**The frame protocol** (raw `window.postMessage` of `{Type, ...}` without the marker): `Sw.Frame.PostParent(name, data)` (the parent, or the opener of a top window), `PostTop`, `PostAll` (the opener, else the top); legacy `PostParent`, `PostTop`, `PostAll`. The page messages `PageLoaded` (to the parent) and `LoaderRemoved` (to the top) come from the page protocol of `sw/core.js` (`Sw.Page`), `IframeNavigating {Url}` asks the app shell to navigate. `Sw.Frame.On(type, fn)` listens. **Same origin only**: the posts go to windows of this origin (target origin `"/"`), and `On` passes on messages of this origin only (see section 11).

## 8. Themes

The theme is chosen in this order: the theme forced for this window, else the stored choice `localStorage["SysWeaver.Theme"]`, else the OS scheme (light / dark, or the app's themes named in `app/app.css`), else dark. A name may carry a stylesheet, `"name@css/url.css"` (relative to `common/`).

| Url flag / call | Stored | Effect |
|---|---|---|
| `?setTheme=X` | yes | stored and applied in this window; other open windows pick it up on their next apply (no message), as the original. A frame started with it follows `theme.set` {Value} from its host |
| `?useTheme=X` | no | forced for this window only (`Sw.Session.RequestedTheme`, legacy `window.UseTheme`); `appendTheme(url)` / `Sw.Url.AppendTheme` add `useTheme=X` to the urls this page opens (the shell does it for every content frame). A frame started with it follows `theme.use` {Value} from its host |
| `setTheme(X)` (the server Theme menu) | yes | applied and posted as `Theme.Changed`: **every other window drops its forced theme** and re-applies (as the original) |
| `resetTheme()` | removed | applied, `Theme.Changed` |
| `toggleTheme()` | yes | light / dark |
| the OS scheme changes | no | every window re-applies; the top window posts `Theme.Changed` when its theme changed |

**Applying** (`sw/theme.js`, part of `sw/core.js` and `common/common.js`): `light` removes `data-theme` from html, any other name sets `data-theme=<name>` (css defines it: `common/theme.css` has light, dark, application, pro, hacker); the derived variables of `--ThemeBackground/Main/Acc1/Acc2` (`R`, `G`, `B`, `RGB`, `25`, `50`, `75`) are written on html; `data-sw-scheme` (light / dark: what the style sheets declare; without `common/theme.css`, a page with its own look, the built in theme's own scheme: light = light, dark / pro / hacker / application = dark) and `data-density` are set; the brand stylesheet `app/app.css` of the shell is adopted.

**A page with its own theming** (`sw/session.js` without `sw/theme.js`) applies nothing, but knows what the hand-shake asks for: `Sw.Session.RequestedTheme` (forced: `?useTheme`, `theme.use`; null after another window's `setTheme`), `Sw.Session.StoredTheme` (the user's choice) and the event `Sw.Events` `theme.request` {Theme, Stored, Reason}. Use `Theme || Stored || your default` at start and on every event.

## 9. The automatic classes

On **both html and body**:

| Classes | Rule |
|---|---|
| `SwPortrait` / `SwLandscape` | width <= height of the top SysWeaver window (a same-origin child frame copies its parent's value) |
| `SwNarrow` / `SwWide` | width <= 600 px (the parent's value in a child frame) |
| `SwSelfPortrait` / `SwSelfLandscape`, `SwSelfNarrow` / `SwSelfWide` | this window's own values |
| `SwMobile` / `SwDesktop` | `?mobile=true/false`, else `localStorage["SysWeaver.Mobile"]`, else the parent frame's value, else the device (user agent data / user agent) |
| `Transparent` | `?transparent=true`, or a child frame of a SysWeaver page unless `?transparent=false` |
| `FillScreen` | `?fillscreen` |
| `SwNested` | a frame of a page of this site that is not the app shell (one page padding level) |

They are set at once and updated on every resize (and at the window `load`). Body events on a change (detail = the boolean, not bubbling): `SwOrientation` + `SwPortrait` / `SwLandscape`, `SwLayout` + `SwNarrow` / `SwWide`, and the `SwSelf*` pairs; `Sw.Events` `layout.changed`. State: `Sw.Layout` (`Mobile`, `Portrait`, `Narrow`, `SelfPortrait`, `SelfNarrow`, `Width`, `Height`), legacy `window.Mobile`, `Portrait`, `Narrow`, `SelfPortrait`, `SelfNarrow`, `SwWidth`, `SwHeight`. A child frame reads its parent only on its own resize. `data-density` is not one of them: it belongs to the theme (`sw/theme.js`).

## 10. Legacy names

`sw/session.js` (and `common/common.js`, `sw/compat.js`) define the old names as window properties: `SessionManager` (`AddMessageHandler`, `RemoveMessageHandler`, `AddServerEvent`, `RemoveServerEvent`, `IsMaster`, `Url`, `Current`, `ServerEvents`, `OwnedEvents`, `Create`, `Init`), `InterOp` (`Id`, `B`, `Post`, `AddListener`, `RemoveListener`, `GetMessage`), `InterOpBroadcast`, `InterOpWindows`, `SysWeaverInit`, `SysWeaverIgnoreUserChanges`, `GetServerTime`, `GetServerTick`, `PostTop`, `PostParent`, `PostAll`, `ReloadAll`, `getUrlParams`, `isMobile`, `getPlatform`, `getMobileParams`, and the window state `Mobile`, `Portrait`, `Narrow`, `SelfPortrait`, `SelfNarrow`, `SwWidth`, `SwHeight`, `IsTop`, `UseTheme`, `SysWeaverServerTimeOffset`, `SysWeaverId`, `IgnoreUserChanged`, `HaveSysWeaverInit`, `HaveSessionManager`. The theme globals (`getTheme` ... `appendTheme`) come with the theme. `sw/core.js` has the theme globals but not the legacy messaging names: new pages use the `Sw.*` api.

They behave as the original's (checked by the parity scenarios `msg-legacy-api-*`): `SysWeaverInit()` returns a Promise (it starts nothing more: the core starts itself); `PostTop` / `PostParent` / `PostAll` set `data.Type` on the caller's object; `SessionManager.ServerEvents` / `OwnedEvents` are live ref count Maps with the original's counts (in the master its own `AddEvents` echo counts too: 2 after one handler, 3 after two; `AddServerEvent` ×2 then `RemoveServerEvent` ×3 return true, false, false, false, true), while the real subscriptions are `Sw.Session`'s exact ref counts; `window.IgnoreUserChanged = false` turns the reloads on again (`Sw.Session.IgnoreUserChanges(false)`).

## 11. Differences from the original

Measured with the parity test (`D:\SysWeaver.Tests\ParityTests`, plugin `MessagingPlugin`: the original's common.js against `sw/session.js`, `sw/session.js` + `sw/theme.js` and the rewritten common.js): the requests, their bodies and order, the election, the handover, the reactions, the theme hand-shake and the classes are the same. The differences:

| What | Original | Now | Why |
|---|---|---|---|
| A master's own subscription | reached the server with the next poll (up to 90 s later) | re-polls at once | an original bug |
| A closing child tab | never unsubscribed | releases its subscriptions | an original bug |
| A busy window at its start up | could become a second master (two pollers) | waits while its own timer came late | robustness |
| A busy or throttled master (answers the Ping after 100 ms) | the asking tab became a second master (two pollers, every message relayed twice) | the master holds the Web Lock `SysWeaver.SessionManager.Master`: exactly one master; without Web Locks a late Pong makes the second master step down | robustness (S6-Core-1) |
| `server.error` (connection lost) | blocking overlay "Server communication lost." in every top window | the same (owner parity, S6-Core-11; series 5 had replaced it by the calm offline line only), plus the shell's offline line under it; `Sw.Session.BlockOnLost = false` leaves only the line | parity |
| The block overlay element | `SysWeaver-ServerBlock` (full page, no way to see the page) | a dialog above every pop-up (`.sw-dialog.sw-serverblock`; the fallback of a `sw/session.js` page: `div.sw-serverblock[data-sw-fallback]`): the same texts, the elapsed time m:ss, no reload button (the page reloads by itself on a new instance), movable, resizable, × / Escape hide it until the server is back (OWN3-Down-1) | the owner: see and copy the page while waiting |
| A probe while the server is away | none (only the poll's 5 s retry and the time sync's 15 s) | the same in the polling window; one `serverTime` probe when a request fails while still "online", and every 15 s in a child tab / a page without a session | a single failed request must not look like an outage |
| Frame posts (`PostTop`, `PostParent`, `PostAll`, `PageLoaded`, `LoaderRemoved`) | target origin `"*"` (any host, also another site) | this origin only; the window message fallback and `Sw.Frame.On` drop other origins | security: a foreign page framing a SysWeaver page gets nothing, and cannot drive the shell (`IframeNavigating`) |
| Global classes | `SessionManager`, `InterOp` were lexical class globals | window properties | a superset |
| Additions | | `SwNested`, `data-sw-scheme`, `data-density`, `?top=true`, `data-sw-no-session`, `SysWeaver.SignedOut`, `GetEvents` / `OwnedEvents`, `TimeOffset.Z` / `Si`, the bfcache restart | |

**Cross-origin hosts** (decided by the coordinator, the owner may change it): the frame posts stay same origin. A page of another site that embeds a SysWeaver page and waits for `PageLoaded` / `LoaderRemoved` gets nothing; no SysWeaver project does this today. Should it be needed, an opt-in could look like this (not implemented): the embedding url carries `?hostorigin=https://other.site` (one exact origin, http(s), no wildcard); the page posts its frame messages to that origin as well, and `Sw.Frame.On` accepts messages from it, never `"*"`, and only the frame protocol types, never the InterOp bus or the session.
