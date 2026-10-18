# fakeEmail

[⬆ srcWeb](../README.md)

> The fake email web inbox: a self-contained single page that lists, shows, composes and deletes the emails held by `FakeEmailService`.

| | |
|---|---|
| **Project** | [SysWeaver.Email.Fake](../../README.md) |
| **Kind** | Embedded web assets (source) |

## Purpose

A standalone page (it does not use the SysWeaver web application shell or any external library) titled "Fake Mail". It talks to the web API of [`FakeEmailService`](../../FakeEmailService.cs), by default at `../Api/fakeEmail/` relative to the page; an `?api=` query parameter can point it at another API base. It polls `GetEmails` for live updates (with a connection status indicator), fetches single emails with `GetEmail`, and uses `SendEmail` / `FakeIncoming` to compose emails as the system or as a user, and `DeleteEmail` to remove them.

At build time the files are Brotli-compressed into `web/fakeEmail/` and embedded (see [srcWeb](../README.md)); the page is opened at `fakeEmail/index.html`.

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | Page layout: header with sender address, search and theme toggle, mailbox side bar, message list, reading pane and composer (inline SVG icons) |
| [`email.js`](email.js) | All behaviour: API calls, polling, search with highlighting, html bodies in a sandboxed iframe or as source, compose (text or html, with preview), reply, delete, read state and settings kept in local storage, keyboard shortcuts |
| [`email.css`](email.css) | Styles, including light and dark themes |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.Email.Fake](../../README.md)
- **Uses:** [`FakeEmailService`](../../FakeEmailService.cs) (`Api/fakeEmail/`)

## Behavior (web analysis)

### Loading and initialization

`index.html` contains the full static layout (inline SVG icons, element ids used by the script) and loads only `email.css` and `email.js` (`defer`). It does not load common.js, the app shell, theme.css or translations. At the end of `email.js`, `init()` runs immediately (it relies on `defer`). It applies the theme, restores the read set from localStorage, wires all handlers and keyboard shortcuts, and starts `pollLoop()`. The first poll response populates the list; at that point an email id in `location.hash` is selected.

The page is registered by `[WebMenuLink(null, "Debug/FakeEmail", "Fake email", "fakeEmail/index.html", ...)]` on `FakeEmailService` (`[WebApiUrl("fakeEmail")]`). The same service also exposes a generic `MessageTable` (`WebMenuTable` "Emails") that the explorer table UI shows; that table is not part of this folder.

### Files

#### `index.html`
Static skeleton:
- **Header**: brand, `#fromAddress` (the system sender), `#search`, the `#status` dot and the `#theme` toggle.
- **`nav.side`**: `#compose`, `#folders` (`li[data-folder=all|out|in|failed]` with `span[data-count]`), `#systemsBox`/`#systems` (filter by "System" tag), `#markAllRead` and `#clearAll`.
- **`section.list-pane`**: `#folderTitle`, `#listInfo`, `ul#list`, `#empty`.
- **`section#reader`**: `#readerEmpty` (shortcut help) and `article#mail` with the toolbar (`#back`, `#viewMode` Rendered/Source, `#reply`, `#openTab`, `#delete`), header fields (`#mailSubject`, `#mailAvatar`, `#mailFromName`, `#mailFrom`, `#mailTo`, `#mailTags`, `#mailTime`), `#mailError` and `#mailBody`.
- **`dialog#composer`**: mode (`data-mode=in|out`), fields `#cFrom`, `#cName`, `#cTo`, `#cSubject`, `#cSystem` (the `in-only` ones are hidden in "out" mode by CSS), format (`data-format=text|html`), `#cBody`, sandboxed `#cPreview`, hidden `#cReplyTo`, `#composeHint`, `#composeSend`.
- `#toasts` live region.

#### `email.js`
Strict-mode classic script with global functions and constants:
- `Api`: the base URL, from `?api=` or `../Api/fakeEmail/`, normalized to end with `/`. `$(id)` is a `getElementById` shorthand.
- `State` (a single mutable object): `version`, `from`, `emails` (summaries), `byId` (Map), `folder`, `system`, `query`, `selected`, `view` (`render|source`), `current` (the full email), `known` (ids at last render, used to mark new items `fresh`), `read` (Set).
- `Folders`: `{title, test(e), empty}` per folder (`all`; `out` = `!Incoming`; `in` = `Incoming`; `failed` = `Error`).
- `load(key, def)` / `save(key, value)`: JSON in localStorage under `fakeEmail.<key>`, errors ignored.
- `call(method, data)`: `fetch(Api+method, POST JSON, cache: no-store)`. A non-OK status throws with the response text; an empty body returns null. This is its own fetch wrapper, not `sendRequest`.
- Helpers:
  - `el(tag, cls, text)`, `toast(text, isError)` (3 s / 7 s);
  - `hue(s)` (string hash → hue), with `party(e)`, `displayName(e)` and `avatar(e, big)` for colored initials;
  - `shortTime` / `longTime`;
  - `appendHighlighted` (wraps query hits in `<mark>`), `appendLinkified` (plain-text URLs → `<a target=_blank>`);
  - `frameHtml(body)`: wraps HTML for an iframe `srcdoc`, with `<base target=_blank>` and a minimal style.
- Rendering:
  - `matches(e)` filters by folder, system and query (over From/To/SenderName/Subject/Preview/System); `visible()` returns the filtered list.
  - `renderSide()` sets the unread/total counts per folder and the systems list (shown when there is more than one system, or a single non-empty one).
  - `renderList()` rebuilds `#list` completely. Each item gets classes `sel`/`unread`/`fresh`, plus avatar, direction badge, failed tag, time, subject and preview.
  - `renderAll()` runs both.
- Reader:
  - `select(id, noScroll)` sets `#<id>` in the URL hash with `history.replaceState`, adds `body.reading` (used by the mobile layout), marks the email read, and fetches the full email with `GetEmail {Id}`. The response is ignored if the selection has changed in the meantime.
  - `showMail(e)` fills the header and tags (direction, HTML/Plain, System, "Reply to <id>", which links to the original if present, Id) and `#mailError`.
  - `renderBody()`: HTML in render mode goes into a sandboxed iframe (`allow-popups allow-popups-to-escape-sandbox`, no scripts) via `srcdoc`. Otherwise the body goes into a `<pre>` as source, or linkified plain text.
  - `openInTab()` opens a Blob URL, revoked after 60 s.
  - `deleteSelected()` calls `DeleteEmail {Id}`, removes the email locally and selects its neighbour.
  - `move(delta)` steps through the visible list.
- Live updates, `pollLoop()`: an endless loop of `GetEmails {Version}`.
  - The server holds the request until the version changes or `TimeOut` passes (default 30 s), then returns `{Version, From, Unchanged}` or `{Version, From, Emails[]}`.
  - On error the status dot turns red and the loop backs off exponentially (1 s, doubling, max 15 s).
  - `applyList(emails)` replaces the whole list. On the very first load, if no read set was ever stored, every existing email counts as read. New ids flash the document title (`(n) Fake Mail`) and show a toast.
- Composer:
  - `Composer {mode, format}`, `setComposeMode`, `setComposeFormat`, `showPreview` (HTML preview in a sandboxed iframe).
  - `openComposer(opts)` restores the last From/Name from storage.
  - `quote(e)`: an HTML blockquote, or `> ` prefixed text.
  - `reply()` replies to an incoming mail as the system ("out", `SendEmail`), or to a system mail as the user ("in", `FakeIncoming` with `InReplyTo`).
  - `sendComposed()` posts `{From, SenderName, To, Subject, Body, IsHtml, System, InReplyTo}`. The returned id is marked read, since the user wrote it.
- Theme: `initTheme()` sets `documentElement.dataset.theme` to `light|dark`, from storage or `prefers-color-scheme`, and follows OS changes unless the user has set it explicitly.
- Keyboard (outside inputs, composer closed): `j`/`↓` next, `k`/`↑` previous, `r` reply, `c` compose, `/` search, `Del` delete, `Esc` deselect (or blur an input). Ctrl/Cmd+Enter sends in the composer. Search is debounced by 120 ms.

#### `email.css`
A self-contained design system. CSS variables on `:root` with a `:root[data-theme="dark"]` override (not the SysWeaver `--Theme*` variables), and a flex three-pane layout. Responsive breakpoints are 1100px and 720px; at 720px `body.reading` swaps the list for the reader. State classes: `on`, `sel`, `unread`, `fresh` (an animation), `status.on|off`, `composer.out` (hides `.in-only`), `composer.html` (monospace textarea), `tag.in|out|failed`, `dir.in|out`. `[hidden]` is forced to `display:none !important`.

### Server interaction

All calls are POST JSON to `<Api><Method>`:

| Method | Request | Response |
|---|---|---|
| `GetEmails` | `{Version}` | `{Version, From, Unchanged?, Emails?: FakeEmailSummary[]}` (`Id, Time, From, To, SenderName, Subject, Preview, Incoming, Error, System`); long-poll |
| `GetEmail` | `{Id}` | full email (adds `Body, IsHtml, InReplyTo`) |
| `DeleteEmail` | `{Id}` | none |
| `Clear` | `null` | none |
| `SendEmail` | `FakeEmailRequest` | id (sent "as the system" through the fake sender) |
| `FakeIncoming` | `FakeEmailRequest` | id (queued as incoming; consumers read it via `GetIncoming`) |

There are no session push or WebSocket channels: the page's live updates come only from long-polling `GetEmails`.

### State and persistence

- localStorage: `fakeEmail.read` (array of read ids, pruned to existing ids), `fakeEmail.theme`, `fakeEmail.lastFrom`, `fakeEmail.lastName`.
- URL: `?api=` (API base) and `#<emailId>` (selected email).
- Everything else is in-memory in `State`.

### Dependencies

None outside this folder: no common.js, app shell, theme or i18n. It needs only the `FakeEmailService` API. Consumer: the `Debug/FakeEmail` menu link. It works as a standalone page too, and with `?api=` it can point at another server.

### Structural observations

- This page is the odd one out in the framework: hand-rolled fetch wrapper, toasts, theme and dialogs (`<dialog>`), English-only text (no `_T`), its own CSS variables, and no `PageLoaded`/`Fail`/`SysWeaver-*` elements. It follows modern vanilla JS style (template-free DOM building, `replaceChildren`, `Set`/`Map`).
- The list is rebuilt fully on every change; selection, read state and "fresh" detection depend on `State.known`.
- Implicit contracts with the C# DTOs: field names (`Incoming`, `Error`, `System`, `Preview`, `InReplyTo`, `Unchanged`, `Version`) and the long-poll semantics of `GetEmails`.

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function. `$(id)` is this file's `document.getElementById` shortcut, and element ids refer to `index.html`.

### `email.js`

The standalone script of the Fake Mail page (`index.html`), loaded with a static `<script src="email.js" defer>`. It does not use common.js or the app shell. It runs in strict mode and defines all its state as top-level globals (`Api`, `$`, `State`, `Folders`, `Today`, `Composer` and the functions below). The last line calls `init()` immediately, which wires up the static DOM from `index.html` and starts the long-poll loop. All server calls are JSON POSTs to `Api + <method>` (`GetEmails`, `GetEmail`, `DeleteEmail`, `Clear`, `SendEmail`, `FakeIncoming`). Persistent per-browser state is kept in localStorage under the `fakeEmail.` prefix (`read`, `theme`, `lastFrom`, `lastName`).

#### Globals

| Name | Kind | Description |
|---|---|---|
| `Api` | const string | The API base URL. It is taken from the `?api=` query param, or defaults to `"../Api/fakeEmail/"`, and always ends with `/`. |
| `$` | const function | `$(id)` → `document.getElementById(id)`. |
| `State` | const object | Mutable page state. `version`: the last `GetEmails` version. `from`: the system's sender address. `emails`: the current list summaries. `byId`: a `Map` of id → summary. `folder`: the current folder key, default `"all"`. `system`: the system filter, or `null` for all (`""` means "(no system)"). `query`: the lower-cased search text. `selected`: the selected id. `view`: `"render"` or `"source"`. `current`: the loaded full email. `known`: a `Set` of ids seen at the last list render, used to mark new items `fresh`; it is `null` before the first list arrives. `read`: a `Set` of read ids. |
| `Folders` | const object | The folder definitions, keyed `all`, `out`, `in` and `failed`. Each has a `title`, a predicate `test(e)` (`all`: always true; `out`: `!e.Incoming`; `in`: `e.Incoming`; `failed`: `!!e.Error`) and an `empty` message. The keys match `data-folder` / `data-count` in `index.html`. |
| `Today` | const function | `Today()` → `new Date().toDateString()`, used by `shortTime`. |
| `Composer` | const object | Composer state: `mode` (`"in"` = user → system, `"out"` = system → user) and `format` (`"text"` or `"html"`). |
| (top-level call) | immediate code | `init()` is called when the script runs (after DOM parsing, because of `defer`). |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `load` | `load(key, def)` → any | Reads `localStorage["fakeEmail." + key]` and parses it as JSON. Returns `def` if the key is missing or on any error. |
| `save` | `save(key, value)` | Writes `JSON.stringify(value)` to `localStorage["fakeEmail." + key]`, ignoring errors. |
| `call` | async `call(method, data)` → `Promise<any>` | Sends `fetch(Api + method)` as a POST with a JSON body (`data ?? null`) and `cache: "no-store"`. A non-OK response throws an `Error` with the response text or the status. Otherwise it resolves to the parsed JSON, or `null` for an empty body. |
| `el` | `el(tag, cls, text)` → `HTMLElement` | Creates an element, optionally sets `className` and `textContent` (when `text` is not null/undefined), and returns it. |
| `toast` | `toast(text, isError)` | Appends a `div.toast` (with `err` added for errors) to `#toasts`. It is removed after 3 s, or 7 s for errors. |
| `errText` | `errText(e)` → string | Returns `e.message` if present, else `String(e)`. |
| `hue` | `hue(s)` → number | A deterministic 31-multiplier string hash, mapped to 0–359. It gives the avatar color. |
| `party` | `party(e)` → string | The "other party" (the user side) of an email: `e.From` for incoming mail, `e.To` for outgoing mail, or `""`. |
| `displayName` | `displayName(e)` → string | `e.SenderName` for incoming mail that has one, else `party(e)`. |
| `avatar` | `avatar(e, big)` → `HTMLElement` | Creates a `div.avatar` (plus `big` when `big` is truthy) holding the first character of `displayName(e)` (or `?`). Its background is `hsl(hue(party) 55% 50%)`. |
| `shortTime` | `shortTime(d)` → string | List timestamp: the time (HH:MM) for today, "Mon D" for this year, otherwise "Mon D, YYYY" (locale formatted). |
| `longTime` | `longTime(d)` → string | `toLocaleString` with `dateStyle`/`timeStyle` set to `"medium"`. |
| `appendHighlighted` | `appendHighlighted(parent, text, query)` | Appends `text` to `parent`, wrapping every case-insensitive occurrence of the lower-case `query` in a `<mark>`. Without a query it appends the text as is. |
| `appendLinkified` | `appendLinkified(parent, text)` | Appends plain text, turning `http(s)://` URLs (with trailing punctuation excluded) into `<a target="_blank" rel="noopener noreferrer">` links. |
| `frameHtml` | `frameHtml(body)` → string | Wraps an HTML body in a document with `<base target="_blank">`, a UTF-8 meta and a fixed light inline style. It is used for the sandboxed reader iframe, the composer preview and `openInTab`. |
| `matches` | `matches(e)` → boolean | True when `e` passes the current folder's `test`, the system filter (`State.system`), and the search query (a substring of `From`, `To`, `SenderName`, `Subject`, `Preview` or `System`). |
| `visible` | `visible()` → `Array` | `State.emails.filter(matches)`. |
| `renderSide` | `renderSide()` | Updates each folder count (`[data-count=…]`): it shows the unread count if there is one (and adds class `unread`), otherwise the total, with a tooltip of "u unread of n". It toggles `on` on the `#folders` items for the current folder. It rebuilds `#systems` from the distinct `System` values and hides `#systemsBox` unless there are at least two systems or one non-empty system. Uses inner helpers `unread` and `add`. |
| `renderList` | `renderList()` | Rebuilds `#list`. Each email becomes an `li.item` with `data-id`. It gets `sel` when selected, `unread` when not in `State.read`, and `fresh` when not in `State.known`. It contains an avatar; a `.who` line with a `span.dir.in`/`span.dir.out` label ("FROM"/"TO"), the highlighted name and an optional `span.tag.failed`; a `<time>` (short form, long-form tooltip); a highlighted `.subj`; and a highlighted `.prev`. Each item's `li.onclick` calls `select(id)`. Afterwards it resets `State.known` to all current ids, and updates `#folderTitle` (folder title plus system filter), `#listInfo` ("n emails" or "n of m"), the visibility of `#empty`, and `#emptyText` (no-match text or the folder's `empty` message). |
| `renderAll` | `renderAll()` | `renderSide()` followed by `renderList()`. |
| `select` | async `select(id, noScroll)` → `Promise<void>` | Selects an email, or clears the selection when `id` is falsy. Clearing hides `#mail`, shows `#readerEmpty`, removes `reading` from `body` and strips the URL hash. Selecting first sets `#<id>` in the URL with `history.replaceState`, adds the `body.reading` class, and scrolls the item into view unless `noScroll` is set. An unread email is marked read (`saveRead`, `renderSide`). It then loads `call("GetEmail", {Id})` and calls `showMail` if the selection is still the same; errors become an error `toast`. |
| `showMail` | `showMail(e)` | Fills the reader. It sets `State.current`, the subject, and a new big avatar (`#mailAvatar` is replaced). The from name is the sender name or address for incoming mail, or "System" for outgoing mail; it also sets the `<from>` address, the to address and the `#mailTime` text and `dateTime`. It builds the `#mailTags` `.tag` spans: direction (`tag in`/`tag out`), HTML/Plain text, `System: …`, "Reply to …" (clickable via `t.onclick` → `select(InReplyTo)` when that email is known), and the id. It shows or hides `#mailError` with `e.Error`, shows `#viewMode` only for HTML mail, then calls `renderBody()`. |
| `renderBody` | `renderBody()` | Renders `State.current.Body` into `#mailBody` and toggles `on` on the `#viewMode` buttons that match `State.view`. HTML mail in `"render"` view becomes an `<iframe sandbox="allow-popups allow-popups-to-escape-sandbox">` with `srcdoc = frameHtml(body)`. HTML in source view becomes a `<pre>` with the raw text. Plain text becomes a `pre.text` with linkified URLs. |
| `openInTab` | `openInTab()` | Opens the current body in a new tab from a Blob URL (`text/html` via `frameHtml`, or `text/plain`). The URL is revoked after 60 s. |
| `deleteSelected` | async `deleteSelected()` → `Promise<void>` | Calls `call("DeleteEmail", {Id})` for the selected email. It removes the id from `State.read` (`saveRead`) and from the local list (`removeLocal`), then selects the next visible email, or the previous one, or none. Errors become a `toast`. |
| `removeLocal` | `removeLocal(id)` | Removes an email from `State.emails` and `State.byId`, then calls `renderAll()`. |
| `move` | `move(delta)` | Selects the visible email `delta` steps from the current one, clamped to the list. With no selection it selects the first. |
| `saveRead` | `saveRead()` | Prunes `State.read` to ids still present in `State.byId` (only when `byId` is non-empty), then saves it as `fakeEmail.read`. |
| `setStatus` | `setStatus(on, text)` | Sets `#status` to `status on` or `status off` with `text` as its tooltip. |
| `pollLoop` | async `pollLoop()` → never resolves | An endless long-poll loop. It calls `call("GetEmails", {Version: State.version})`, marks the status connected, stores `r.Version`, updates `State.from` and `#fromAddress` when `r.From` changes, and calls `applyList(r.Emails)` unless `r.Unchanged`. On error it marks the status disconnected and waits with exponential back-off (1 s doubling up to 15 s). |
| `applyList` | `applyList(emails)` | Replaces `State.emails` and `State.byId`. On the first call (`State.known === null`), if no `fakeEmail.read` is stored, all existing emails are marked read; it then saves and, after rendering, selects the email named in the URL hash, if it exists. On later calls it detects new ids, sets `document.title` to "(n) Fake Mail" for 4 s and shows a toast ("Received: …", "Sent: …" or "n new emails"). It clears the selection if the selected email disappeared, and always calls `renderAll()`. |
| `setComposeMode` | `setComposeMode(mode)` | Sets `Composer.mode` and toggles class `out` on `#composer`, which hides the `.in-only` fields. It marks the matching `#composeMode` button `on`, and sets the `#cTo` placeholder and the `#composeHint` text. |
| `setComposeFormat` | `setComposeFormat(format)` | Sets `Composer.format`, toggles class `html` on `#composer`, marks the matching `#composeFormat` button `on`, and shows `#cPreviewBtn` only for HTML. Leaving HTML closes the preview. |
| `showPreview` | `showPreview(on)` | Switches between the `#cBody` textarea and the `#cPreview` iframe (`srcdoc = frameHtml(cBody.value)`), and toggles the button text between "Edit" and "Preview". |
| `openComposer` | `openComposer(opts)` | Resets the composer from `opts` (`mode`, `format`, `from`, `name`, `to`, `subject`, `system`, `body`, `replyTo`, `focus`). `from` and `name` default to the stored `fakeEmail.lastFrom` (`"user@example.com"`) and `fakeEmail.lastName`. It opens `#composer` with `showModal()` and focuses `opts.focus`, else `#cFrom` (in mode with an empty from), else `#cSubject`. |
| `quote` | `quote(e)` → string | Builds a reply quote: "On <time>, <who> wrote:" followed by the body. HTML mail gets a styled `<blockquote>`; plain text gets `> `-prefixed lines. |
| `reply` | `reply()` | Opens the composer as a reply to `State.current`, adding a "Re: " subject prefix if one is missing. For an incoming email it opens in out mode to the sender. For an outgoing email it opens in in mode, from the original recipient to the system, with `replyTo` set to the original id. The cursor goes to the start of the body. |
| `sendComposed` | async `sendComposed()` → `Promise<void>` | Builds `{From, SenderName, To, Subject, Body, IsHtml, System, InReplyTo}`; the user-side fields are only set in in mode. It posts this to `FakeIncoming` (in mode) or `SendEmail` (out mode), with `#composeSend` disabled during the call. On success it saves `lastFrom`/`lastName` (in mode), closes the dialog and marks the returned id read. Errors become a `toast`. |
| `applyTheme` | `applyTheme(t)` | Sets `document.documentElement.dataset.theme` (`light` or `dark`). |
| `initTheme` | `initTheme()` | Applies the stored `fakeEmail.theme`, else the OS `prefers-color-scheme`, and follows OS changes while nothing is stored. The `#theme` button toggles the theme and saves the choice. |
| `init` | `init()` | Calls `initTheme()` and loads `State.read`. It wires the folder items (`li.onclick` sets `State.folder`), a 120 ms debounced `#search` input, the `#viewMode` buttons (`b.onclick` sets `State.view`), and the `#compose`, `#reply`, `#delete`, `#openTab` and `#back` buttons. `#markAllRead` marks everything read. `#clearAll` asks `confirm`, then calls `call("Clear")`. It also wires the `#composeMode`/`#composeFormat` buttons (`b.onclick`), `#cPreviewBtn`, close and cancel, form submit and Ctrl/Cmd+Enter (→ `sendComposed`). Global keyboard shortcuts are active only when the dialog is closed, no modifier is held, and focus is not in a field (Escape in a field blurs it): `j`/ArrowDown and `k`/ArrowUp → `move`, `r` → `reply`, `c` → `openComposer`, `/` → focus search, Delete → `deleteSelected`, Escape → deselect. Finally it starts `pollLoop()`. |

#### Inner functions of `renderSide`

| Function | Signature | Description |
|---|---|---|
| `unread` | inner arrow `unread(e)` → boolean | `!State.read.has(e.Id)`. |
| `add` | inner arrow `add(value, label)` | Appends an `li` to `#systems` with a `span.dot` and the label (class `on` when it is the active filter). Its `li.onclick` toggles `State.system` between `value` and `null` and calls `renderAll()`. |

## CSS reference

### `email.css`

The only stylesheet of the standalone Fake Mail page (`index.html`, static `<link>`). It is self-contained: it uses no SysWeaver theme.css variables and defines its own palette as custom properties on `:root`, with a dark variant on `:root[data-theme="dark"]` set by `applyTheme` in `email.js`. It is not templated. Most classes are static markup in `index.html`. The classes that `email.js` creates or toggles are noted below.

#### Base and utilities

| Selector(s) | Description |
|---|---|
| `*` | `box-sizing: border-box` everywhere. |
| `html`, `body` | Full height. `body` is a column flex container with no overflow, and uses `--bg`/`--text` and a 14px system-ui font. |
| `svg` | Inline icons are 18×18 and do not flex. |
| `.muted` | Muted text color (`--muted`). |
| `.grow` | `flex: 1` spacer. |
| `[hidden]` | Forces `display: none !important`. JS hides elements with the `hidden` property (`#systemsBox`, `#empty`, `#mail`, `#readerEmpty`, `#mailError`, `#viewMode`, `#cPreviewBtn`, `#cPreview`, `#cBody`). |
| `kbd` | Small monospace key caps, used in the reader help text and the Send button. |
| `button`, `button:hover` | Base button: panel background, line border, 8px radius, inline-flex with a gap, and a hover background. |
| `button:focus-visible`, `input:focus-visible`, `textarea:focus-visible`, `.list:focus-visible` | A 2px accent focus outline. |
| `button.primary`, `button.primary:hover`, `button.primary kbd` | The accent-filled primary buttons (Compose, Send). They brighten on hover; the `kbd` inside becomes transparent with a current-color border. |
| `.icon-btn` | A borderless, transparent, icon-only button (theme, back, open tab, delete, close). |
| `.link-btn`, `.link-btn:hover`, `.link-btn.danger` | Text-link-style buttons (Mark all as read, Preview); `.danger` (Delete all) uses `--failed`. |
| `input`, `textarea` | Full-width fields with panel background, line border and 8px radius. |

#### Header

| Selector(s) | Description |
|---|---|
| `.top` | The header bar (flex, panel background, bottom border). |
| `.brand`, `.brand > svg`, `.brand h1`, `.brand span` | Logo, title and `#fromAddress` (filled by `pollLoop`). |
| `.search`, `.search input` | A pill-shaped search box around `#search`; the input is borderless and transparent. |
| `.actions` | The right-aligned header group (status and theme button). |
| `.status`, `.status.on`, `.status.off` | The connection dot `#status`. `setStatus` sets its class to `status on` (green `--in` with a glow ring via `color-mix`) or `status off` (`--failed`). |

#### Layout and sidebar

| Selector(s) | Description |
|---|---|
| `.layout` | A three-column grid: 220px sidebar, a 300–400px list and a flexible reader. |
| `.side` | The sidebar column (`<nav>`): a scrollable flex column with a right border. |
| `.compose-btn` | The Compose button (`#compose`). |
| `.folders`, `.folders li`, `.folders li:hover`, `.folders li.on` | Folder list `#folders` and systems list `#systems`. `.on` (toggled by `renderSide` and created by its inner `add`) marks the active folder or system filter. |
| `.folders .count`, `.folders .count.unread` | Per-folder counters (`[data-count]`). `renderSide` toggles `unread` to switch the color to accent. |
| `.dot`, `.dot.in`, `.dot.out`, `.dot.failed` | Small colored circles in the folder items. `renderSide`'s `add` creates plain `span.dot`s for systems. (The markup also uses `dot all`, which has no rule of its own.) |
| `.systems`, `.systems h3` | The "Systems" box (`#systemsBox`) and its small uppercase heading. |
| `.side-foot` | The bottom sidebar links (`#markAllRead`, `#clearAll`). |

#### Email list

| Selector(s) | Description |
|---|---|
| `.list-pane`, `.list-head`, `.list-head h2`, `.list-head span` | The list column with a header (`#folderTitle`, `#listInfo`). |
| `.list` | The scrollable `#list` `<ul>`. |
| `.item`, `.item:hover` | List rows created by `renderList`: a grid of 36px avatar, content and time, with a left accent border slot. |
| `.item.sel` | The selected row (set by `renderList` and `select`): selection background and accent left border. |
| `.item.unread .who`, `.item.unread .subj`, `.item.unread .who::before` | Unread rows (set by `renderList`, removed by `select`): bold text and an accent dot before the name. |
| `.item.fresh` | Rows new since the last render (set by `renderList`): plays the `fresh` animation. |
| `.item .avatar`, `.item .who`, `.item .who span`, `.item time`, `.item .subj`, `.item .prev` | Row parts built by `renderList`. The avatar spans 3 rows; who, subject and preview are ellipsized; subject and preview span the content and time columns; the preview is muted at 13px. |
| `.dir`, `.dir.in`, `.dir.out` | The "FROM"/"TO" direction label in `.who` (`renderList`), colored `--in` or `--out`. |
| `.avatar`, `.avatar.big` | Round letter avatars from `avatar()`; `big` is used for `#mailAvatar` in the reader. The background color is set inline. |
| `.tag`, `.tag.failed`, `.tag.in`, `.tag.out` | Pill tags: the "Failed" tag in list rows (`renderList`) and the reader tags in `#mailTags` (`showMail`: direction `in`/`out`, format, system, reply-to, id). |
| `mark` | Search-hit highlight from `appendHighlighted`: a 30% accent tint via `color-mix`. |
| `.empty`, `.empty svg`, `.empty p` | Centered empty states (`#empty` in the list, `#readerEmpty` in the reader). |

#### Reader

| Selector(s) | Description |
|---|---|
| `.reader` | The reader column (`#reader`). |
| `.mail` | The `<article id="mail">` container. |
| `.mail-toolbar` | The reader toolbar (back, view mode, reply, open, delete). |
| `.back` | The Back button (`#back` → `select(null)`). It is hidden on wide screens and shown at ≤ 720px. |
| `.seg`, `.seg button`, `.seg button + button`, `.seg button.on` | Segmented toggles: `#viewMode` (`renderBody` toggles `on`), `#composeMode` (`setComposeMode`) and `#composeFormat` (`setComposeFormat`). |
| `.mail-subject` | `#mailSubject` heading. |
| `.mail-head`, `.mail-meta`, `.mail-meta .tags`, `.mail-head time` | The sender block: avatar, names and addresses, `#mailTags` and `#mailTime`. |
| `.error-box` | `#mailError`, the delivery error text (`showMail`). |
| `.mail-body`, `.mail-body iframe`, `.mail-body pre`, `.mail-body .text`, `.mail-body a` | The body host `#mailBody` (`renderBody`). The sandboxed iframe has a white background; source and plain-text views are a `<pre>`. `.text` (plain-text mail) switches back to the proportional font. Links from `appendLinkified` use the accent color. |

#### Composer dialog

| Selector(s) | Description |
|---|---|
| `.composer`, `.composer::backdrop` | The `<dialog id="composer">` (up to 720px wide, rounded, with a shadow) and its dimmed backdrop. |
| `.composer form` | Column layout of the form. |
| `.composer-head`, `.body-tools`, `.composer-foot` | Horizontal flex rows: mode toggle and close; format toggle and preview; hint, cancel and send. |
| `.composer.out .in-only` | When `setComposeMode("out")` adds `out` to the dialog, the `.in-only` fields (From, Name, System) are hidden. |
| `.fields`, `.fields label`, `.fields label:has(#cSubject)` | A two-column field grid with stacked labels; the label containing `#cSubject` spans the full width. |
| `.composer textarea`, `.composer.html textarea` | The body textarea `#cBody`. `setComposeFormat("html")` adds `html` to the dialog, which switches it to a monospace font. |
| `.preview` | The `#cPreview` iframe for the HTML preview (`showPreview`). |

#### Toasts

| Selector(s) | Description |
|---|---|
| `.toasts` | The fixed bottom-right stack `#toasts`. |
| `.toast`, `.toast.err` | Messages created by `toast()`: inverted colors with the `toast-in` animation. `err` (errors) uses a red background. |

#### Custom properties

All are defined on `:root` (light), and every one except `--radius` is overridden under `:root[data-theme="dark"]`.

| Variable | Default / source | Used for |
|---|---|---|
| `--bg` | `#f4f5f8` / dark `#12151b` | page and search background |
| `--panel` | `#ffffff` / `#1a1e26` | panels, buttons, inputs, dialog, toast text |
| `--panel-2` | `#f9fafb` / `#161a21` | `kbd` background |
| `--line` | `#e3e6eb` / `#2a303b` | borders, empty-state icon |
| `--text` | `#1d2330` / `#e6e9ef` | text color, toast background |
| `--muted` | `#6b7385` / `#8d95a6` | secondary text, default dot and status |
| `--accent` | `#3b6cf6` / `#6d93ff` | primary buttons, focus outlines, selection border, unread markers, links, `mark` |
| `--accent-soft` | `#e8eefe` / `#24304d` | active segment background, `fresh` animation start |
| `--accent-text` | `#ffffff` / `#0d1120` | text on primary buttons |
| `--hover` | `#f0f2f6` / `#222733` | hover backgrounds, tag background |
| `--sel` | `#e8eefe` / `#263255` | selected row and active folder |
| `--in` | `#11a37f` / `#34c79c` | incoming direction, status on |
| `--out` | `#3b6cf6` / `#6d93ff` | outgoing direction |
| `--failed` | `#e0464b` / `#ff6b70` | failures, danger link, status off, error toast |
| `--failed-soft` | `#fdecec` / `#3a1f23` | failed tag and error box background |
| `--shadow` | `0 10px 40px rgba(20,30,60,.18)` / `rgba(0,0,0,.5)` | dialog, toast and floating compose button |
| `--radius` | `10px` | `.mail-body` corner radius |

`color-scheme` is set to `light` or `dark` with the variables.

#### Animations, media queries and theme variants

| Item | Description |
|---|---|
| `@keyframes fresh` | The background fades from `--accent-soft` to normal over 1.6 s on `.item.fresh`. |
| `@keyframes toast-in` | A toast slides up 10px and fades in over 0.2 s. |
| `:root[data-theme="dark"]` | The dark palette, selected by `data-theme` on `<html>` (`applyTheme`/`initTheme`). |
| `@media (max-width: 1100px)` | The sidebar collapses to a 64px icon rail. Compose label, inactive folder counts, `.systems` and `.side-foot` are hidden. Folder text is hidden (`font-size: 0`) with larger dots and corner badge counts. The brand text is hidden. |
| `@media (max-width: 720px)` | A single-column mobile layout. The sidebar becomes a horizontal scrolling folder strip, and Compose becomes a floating round button. The reader is hidden until `body.reading` (added by `select`), which hides `.list-pane` and the compose button and shows `.reader`. `.back` becomes visible, and `.fields` turns into one column. |
