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
