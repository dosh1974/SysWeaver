# fakeEmail

[⬆ srcWeb](../README.md)

> The fake email web inbox ("Fake Mail"): lists, shows, composes and deletes the emails held by `FakeEmailService`, live updated by a long poll.

| | |
|---|---|
| **Project** | [SysWeaver.Email.Fake](../../README.md) |
| **Kind** | Embedded web assets (source) |

## Purpose

A single page application on the SysWeaver Sw core and ui library (`../sw/core.js`, `../sw/ui.js`, `../common/theme.css`, `../sw/ui.css`), so it follows the five SysWeaver themes, uses Sw toasts / dialogs / menus and the page loading protocol of the app shell. It is reached from the embedded menu item *Debug/FakeEmail* "Fake email inbox" (`WebMenuEmbedded`, opens inside the shell; it can also be opened stand alone) and talks to the web API of [`FakeEmailService`](../../FakeEmailService.cs), by default `../Api/fakeEmail/` (the `?api=` url parameter overrides it).

At build time the files are Brotli-compressed into `web/fakeEmail/` and embedded; the page is served at `fakeEmail/index.html`.

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | Layout: header (brand, system sender address, search, connection dot, theme button), folder side bar, email list, reader, and the composer form (hidden; shown in a `Sw.Dialog`). Inline svg icons. |
| [`email.js`](email.js) | All behavior; one global `fakeEmailMain` (body onload), everything else private in an IIFE. |
| [`email.css`](email.css) | Styles; the page palette is mapped onto the Sw tokens (`--sw-*`); responsive at 1100 px (icon rail) and 720 px (single column). |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.Email.Fake](../../README.md)
- **Uses:** [`FakeEmailService`](../../FakeEmailService.cs) (`Api/fakeEmail/`), the Sw core / ui of [SysWeaver.HttpServer](../../../../SysWeaver.HttpServer/srcWeb/README.md)

## Behavior

### Start
`fakeEmailMain()` shows the loader (`Sw.Page.Loading`), translates the folder labels, migrates the old page theme, restores the read ids, wires the controls and starts the long poll; the loader is removed after the first answer (or error), which also lets the shell reveal the frame.

### Live updates
Endless `GetEmails {Version}`: the server answers `{Version, From, Unchanged}` or `{Version, From, Emails[]}` (the whole list, which replaces the local one). The connection dot turns green ("Connected, updates are live") or red ("Disconnected: <error>") with an exponential back off of 1 s doubling to 15 s. New emails flash the document title "(n) <title>" for 4 s, show an info toast ("Received: subject", "Sent: subject" or "n new emails") and play the `fresh` row animation. On the first list, if `fakeEmail.read` was never stored, all existing emails count as read, and the email named in the url hash is selected.

### Folders, systems, search
Folders All mail, Sent by system (`!Incoming`), Received by system (`Incoming`), Failed (`Error`) with unread counts (else totals; tool tip "u unread of n"). A Systems filter list appears with more than one system (or one non-empty system). Search is debounced 120 ms, case insensitive over From, To, SenderName, Subject, Preview, System with `<mark>` highlights. Side bar entries are keyboard operable (role button, Enter / Space). Headings (the folder title) use the text color, not the theme's accent 2 heading color (maroon read as a warning).

### List and reader
Rows: colored initial avatar, FROM / TO badge, name, Failed tag, short time (long time tool tip), subject, preview; classes `sel`, `unread`, `fresh`; the list is a `listbox` with `aria-activedescendant`. Selecting marks the email read, writes `#<id>` with `history.replaceState`, and loads `GetEmail {Id}` (stale answers ignored). The reader shows subject, avatar, sender, recipient, tags (direction, HTML / Plain text, System, "Reply to <id>" button that selects the original, Id), time, error box and the body: html in a sandboxed iframe (`allow-popups allow-popups-to-escape-sandbox`, no scripts, `<base target=_blank>`) or as source, plain text with linkified urls. Every html email document starts with a content security policy (`EmailCsp`: no scripts, frames, objects, fetches or form posts; inline styles and data / https images only), a second guard next to the sandbox. "Open in new tab" opens a Blob url (revoked after 60 s, `noopener`): an html body is never the blob document itself (a blob url has the app's origin, so script in it could use the session), but a script free viewer page (`viewerHtml`, own CSP) that shows the email in a full size iframe with the same sandbox as the reader; plain text is a `text/plain` blob. Delete (the button and the Del key) asks first, because the server keeps no copy (UX guide SAF-1; no Undo): `Sw.Cells.ConfirmDanger` with the title "Delete “<subject>”?", the facts From / To / Time, "This cannot be undone.", Cancel focused; then `DeleteEmail {Id}`, the neighbour is selected and the focus goes to the list (its active row is the neighbour; Compose when the list is empty; never the page body), "Deleted the email …" is announced. Mark all as read; Delete all asks with the same confirmation and a type to confirm field ("Type delete all to confirm", SAF-4: all items of a kind; "Delete all emails?", "n emails will be deleted.") and calls `Clear`. Both bulk actions are disabled with the reason "There are no emails yet" (`Sw.Disabled.Set`) while the inbox is empty. Both are page header actions on wide screens (`#markAllReadTop`, `#clearAllTop` `.head-act`, Delete all as the danger outline; the old side bar footer links `.side-foot` stay in the markup, hidden); where they are hidden (1100 px and narrower: icon rail, phones in portrait and landscape) the top bar shows a "More" button (`#more`, `.more-btn`, labelled on touch screens) with a menu holding both actions.

### Composer
The form of index.html moves into a `Sw.Dialog` ("New email", wide, modal; Escape / close button / Cancel are close attempts: a changed composer asks "Discard changes?", a sending one stays open; the form moves back on close). Drafts (UX guide SAF-11): the Message is a `Sw.Draft` ("fakeEmail.compose.body", a reply "fakeEmail.reply.<id>.body": its draft replaces the quote when there is one) and the Subject of a new email is kept with `Sw.Draft.Set` ("fakeEmail.compose.subject"); they come back after a reload or a closed tab with "Draft restored (saved 10:42)" and "Discard draft" under the message, and are removed when the email is sent or the composer is discarded; an email with an unsent reply shows the tag "Reply draft" (`Sw.Draft.List`); deleting an email (or Delete all) removes its reply draft too (`Sw.Draft.RemoveFor("fakeEmail.reply." + id + ".")`: it can never be sent any more). Sending makes the dialog busy (`SetBusy`, one send; after 10 s "Close (the email may still be sent)") and leaving the page meanwhile asks first (`Sw.Page.SetDirty(..., { Work: true })`). Mode User -> System (`FakeIncoming`, fields From, Name, To, Subject, System) or System -> User (`SendEmail`, user fields hidden), format Plain text / HTML (monospace, Preview / Edit toggle with a sandboxed frame). Reply: incoming mail is answered by the system, system mail by the user (with `InReplyTo`), with "Re: " and a quoted original (blockquote or `> ` lines), caret at the top. Send: submit button or Ctrl / Cmd+Enter, through the composer's `Sw.Form` (`Submit` = the Send button, `OnSubmit` = `sendWork`): a failure is shown once, in the form's status line above the Send row (no toast besides it; the values stay), and a FakeIncoming answer without an id counts as not sent ("The email was not sent: the server did not accept it. ..."); FakeIncoming gets `{From, SenderName, To, Subject, Body, IsHtml, System, InReplyTo}` (FakeEmailRequest) and its returned id counts as read; SendEmail is the IEmailSender api: it gets `{Email, Subject, Body, IsHtml}` (OutgoingEmailMessage) and answers nothing (sent unless the reply is an error; the email arrives with the next poll); From / Name are remembered.

### Keyboard
Outside inputs, with no dialog / popup open and no modifier: j / ArrowDown next, k / ArrowUp previous, r reply, c compose, / search, Delete delete (asks first), Escape deselect; Escape in an input blurs it.

### Theme
The theme button opens a `Sw.Menu` with the shared theme list, the same as the Theme section of the app shell settings menu (`themeList`: the server menu `Api/application/GetMenu` "Theme", then the themes the app registered with a title, `Sw.Theme.Custom` of the top window; `setTheme` / `resetTheme`, shared with the whole application). Without a server theme menu it lists Automatic, Light, Dark, Hacker, Application, Pro. The old page setting `fakeEmail.theme` (light / dark) is applied once when no SysWeaver theme is stored and then removed.

### Responsive
<= 1100 px: the side bar is a 64 px icon rail (active count as a badge). <= 720 px: single column, the folder tabs wrap to a second row (full labels, never cut off), Compose is a floating round button, the reader replaces the list while `body.reading` (Back button), one column fields, a shorter composer body. Low landscape screens get a smaller body minimum; landscape phones (721..1100 px wide, <= 480 px high) show the folders as a labelled tab strip above the list and the reader (Compose at its end) instead of the icon rail. Touch only devices (`hover: none` and `pointer: coarse`) hide the keyboard hints (`.kbd-help`, the Ctrl+Enter key cap on Send); elsewhere the reader hint `.kbd-help` is small and faint and shows only after the first key press (`body.kbd-user`). Px sizes (fonts, side bar and list widths, avatars, icons) scale with `--fe-scale` (= `--sw-scale`: 1, 1.2, 1.45 on large high resolution screens); in the wide layout long folder names wrap instead of being cut. Touch targets are `--sw-touch` (44 px on touch screens). Inside the shell the page fills the frame (no `html.Embedded` margin) and, as the shell already shows the application header and the theme control, hides its own logo and theme button (`initInShell`, `body.in-shell`: the top window runs the shell, also through the app's Home wrapper page); the header is then the compact page header of a fill page (Design v2; on the canvas without the panel band and rule, the h1 in the page title size, the page padding): the title is the menu item name (`Sw.MenuTitle`, ex: "Fake email inbox", also the document title; "Fake Mail" when the page is not a menu item, ex: in the Home wrapper) above the sender address, then search, connection, More. Design v2 roles (surface, sunken, border, hover, selected), flat (no shadows except the floating phone Compose button), segmented toggles in the shared look. Opened stand alone it keeps its own header and theme button. Opened stand alone (ex: an old bookmark; the menu item opens it embedded in the shell) a "Back" link (`#backToApp`, `.app-back`, `initAppBack`) returns to the application: `history.back()` when the referrer is this site, else the site root. Touch devices get a search place holder without the "/" key hint. Every icon button is named (`nameButtons`): a translated tool tip (with its key short cut), accessible name and `aria-keyshortcuts`; the name also shows on long press (touch / pen) and keyboard focus (`Sw.Hint`), as do the connection indicator (`#statusBox`) and the folder entries (named also in the icon rail). On touch devices short text labels show below the icons (Back, Live / Offline, Theme, Open, Delete), Compose is a labelled pill on phones and in the landscape tab strip, and the logo is hidden below 480 px to keep the search box wide. Counts use separate singular / plural texts ("1 email", "Delete the only email?").

## Server interaction

All calls are `Sw.Net.Send(<api> + method, data, {Post: true, Reload: true})` (POST json, never cached); errors show the server's text (`Sw.Net.Error.Text`). Methods: `GetEmails`, `GetEmail`, `DeleteEmail`, `Clear`, `SendEmail`, `FakeIncoming`. An unauthenticated call is redirected to the sign in page by `Sw.Net`. The api and the menu item require the role of `FakeEmailParams.Auth` (default Ops, Debug or Dev; null = anyone, for an isolated test machine): a signed in user without it gets the shared "not allowed" state (`showDenied`, `Sw.Access.NotAllowed`, `.denied-page`) instead of the inbox, and the long poll stops.

## State and persistence

| Key | Content |
|---|---|
| `localStorage["fakeEmail.read"]` | JSON array of read ids (pruned to existing emails) |
| `localStorage["fakeEmail.lastFrom"]`, `["fakeEmail.lastName"]` | Last composer sender address / name (JSON strings) |
| `localStorage["fakeEmail.theme"]` | Old page theme, read once and removed (migrated to `SysWeaver.Theme`) |
| url hash `#<id>` | The selected email |

## JavaScript reference (`email.js`)

| Name | Description |
|---|---|
| `fakeEmailMain()` | The only global: page entry point (body onload). |
| `Api`, `KeyPrefix`, `State`, `Folders` / `folders()` | Api base, storage prefix, page state, translated folder definitions `{title, test(e), empty}`. |
| `load(key, def)`, `save(key, value)` | JSON storage under `fakeEmail.` (`Sw.Storage.Local`). |
| `call(method, data)`, `errText(e)` | Api call; displayable error text. |
| `el`, `hue`, `party`, `displayName`, `avatar`, `shortTime`, `longTime`, `appendHighlighted`, `appendLinkified`, `frameHtml` | Rendering helpers. |
| `matches(e)`, `visible()`, `sideEntry(li, on, fn)`, `renderSide()`, `renderList()`, `renderAll()` | List filter and rendering. |
| `select(id, noScroll)`, `tag(text, cls)`, `showMail(e)`, `renderBody()`, `openInTab()`, `deleteSelected()`, `move(delta)`, `saveRead()` | Reader and selection. |
| `setStatus(on, text)`, `applyList(emails)`, `pollLoop(onFirst)` | Live updates (`setStatus` also sets the `#statusBox` tool tip and the `#statusText` touch label). |
| `setSeg`, `setComposeMode`, `setComposeFormat`, `showPreview`, `openComposer(opts)`, `closeComposer()`, `quote(e)`, `reply()`, `sendComposed()` (the form's Submit), `sendWork()` (its OnSubmit) | Composer. |
| `migrateTheme()`, `themeList()`, `themeMenu(anchor)` | Theme (the shared theme list, the menu). |
| `initInShell()` | Inside the app shell: `body.in-shell` (no own logo or theme button; the title becomes the menu name, see `main`). |
| `overlayOpen()`, `onKey(ev)`, `wire()`, `nameButtons()`, `main()` | Keyboard, icon button names / touch labels, start up. |

## CSS reference (`email.css`)

| Selector(s) | Description |
|---|---|
| `:root` custom properties `--bg`, `--panel`, `--panel-2`, `--line`, `--text`, `--muted`, `--accent`, `--accent-soft`, `--accent-text`, `--hover`, `--sel`, `--in`, `--out`, `--failed`, `--failed-soft`, `--shadow`, `--radius` | The page palette, mapped onto `--sw-*` tokens. |
| `html.Embedded`, `body` | Fill the window / shell frame, opaque Sw background, no page selection (text parts opt in). |
| `:where(.top, .layout, .composer) button` / `input` / `textarea`, `.primary`, `.icon-btn`, `.link-btn(.danger)`, `kbd` | Controls (touch sized, focus rings). Only the app's own controls: the Sw components (dialog frame, confirm, menus) keep the framework look (`SwWeb check` rule `element-css`); `:where()` keeps the element specificity. `body { overflow: hidden }` is a deliberate full window layout (the panes scroll), marked `SwWeb:allow-element-css`. |
| `.top`, `.brand`, `.search`, `.actions`, `.status-box`, `.status(.on/.off)` | Header (`.status-box`: the connection indicator with the dot and its touch label). |
| `.tlabel`, `.labelled` | Short text labels of icon buttons, shown on touch devices only (`hover: none` and `pointer: coarse`). |
| `.layout`, `.side`, `.compose-btn`, `.folders li(.on)`, `.count(.unread)`, `.dot(.in/.out/.failed)`, `.systems`, `.side-foot`, `.more-btn` (the "More" menu button, only shown where `.side-foot` is hidden) | Layout and side bar. |
| `.list-pane`, `.list-head`, `.list`, `.item(.sel/.unread/.fresh)`, `.who`, `.subj`, `.prev`, `.dir`, `.avatar(.big)`, `.tag(.failed/.in/.out/.link)`, `mark`, `.empty` | List. |
| `.app-back` | The "Back" link to the application (stand alone only; icon only <= 720 px). |
| `.reader`, `.mail`, `.mail-toolbar`, `.back`, `.seg`, `.mail-subject`, `.mail-head`, `.mail-meta`, `.error-box`, `.mail-body` | Reader. |
| `.composer-dialog`, `.composer(.out/.html)`, `.composer-head`, `.fields`, `.body-tools`, `.composer-foot`, `.preview` | Composer form in the dialog. |
| `@keyframes fresh`; `@media (max-width: 1100px)`, `(max-width: 720px)`, `(max-height: 480px)`, `(min-width: 721px) and (max-width: 1100px) and (max-height: 480px)`, `(hover: none) and (pointer: coarse)` | Animation and responsive rules. |
