# Porting an app to the new front end and Design v2

[⬆ srcWeb](README.md)

> The rules for moving the web pages of an app (any project with a `srcWeb` / `srcData` folder) from the old UI (`common/common.js`, `app/application.js`, `common/theme.css` era) to the `Sw` core / ui / shell and to the Design v2 look. Each rule says what changed, shows a before / after, and whether [`SwWeb`](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwWeb/README.md) does it (`migrate`) or finds it (`check`).

Nothing breaks if you do nothing: old pages keep working on the generated legacy bundles, and the framework pages, the shell, the tables and the editors get Design v2 by themselves. Porting means adopting the new API and the shared page parts, so the next restyle of the framework reaches your pages too. **Never fork or copy framework files** (`sw/*`, `ui/*`, `common/theme.css`): brand and adapt through `app/app.css`, `app/app.js` and the `*.Services.json` (section [B16](#b16-themes-and-branding-without-forking)).

Contents: [Workflow](#workflow) · [Part A: old UI to Sw](#part-a-from-the-old-ui-to-the-sw-core-ui-and-shell) · [Part B: Design v2](#part-b-design-v2) · [Summary table](#summary-what-swweb-does) · [Verify](#verify)

## Workflow

```
SwWeb migrate <srcWeb> --dry-run --level design --report port.md   # 1. the worksheet: changes = automatic, warnings = manual
SwWeb migrate <srcWeb> --backup <dir>                                # 2. the automatic part (includes, API renames, tokens)
SwWeb migrate <srcWeb> --level design                                # 3. + the Design v2 rewrites (same report, manual items stay)
SwWeb check <srcWeb> --strict --design                               # 4. fix the manual items until 0 findings
```

- `migrate` is idempotent and changes only what is safe (token level renames, include tags, aliases of the same value); everything else is a warning with file, line and the suggested replacement. Run it again after manual fixes: pages on `sw/compat.js` get an info when the compat includes can go.
- `check --strict` = the server contract (translation, templates, file names, offline, legacy globals in new pages). `check --design` adds the Design v2 porting rules (`design-*`, `a11y-*`): warnings, so they only fail together with `--strict`.
- Deliberate exceptions: `/* SwWeb:allow-design (why) */` on the line or the line before (a colour swatch, a map tile, an email preview), `<!-- SwWeb:no-page-header (why) -->` in a page that has no header on purpose (a dialog page, an embed, a full window app).
- Several projects merge into one web root at run time: migrate all of your `srcWeb` folders in one run (a common parent folder) so page globals and scripts of the other projects are known.
- Your own libraries with legacy names: add a `--map my_map.json` with a section per library (`Globals`, `Css`, `Tokens`; format in the SwWeb README).

Legend used below: **migrate** = what `SwWeb migrate` does (`api` is the default level, `design` = `--level design`); **check** = the rule that finds it; **manual** = what you do by hand.

---

## Part A: From the old UI to the Sw core, ui and shell

### A1. Includes

**Change.** `common/common.js` = `sw/core.js` + `sw/ui.js`; `app/application.js` = `sw/shell.js` (only the shell page needs it; pop-ups and menus are in `sw/ui.js`); `app/application.css` = `sw/ui.css` (+ `sw/shell.css`). `common/theme.css` stays (it is the templated theme). A page with UI also links `sw/ui.css`.

```html
<!-- before -->
<script defer src="../common/common.js"></script>
<script defer src="../app/application.js"></script>
<link rel="stylesheet" href="../common/theme.css">
<link rel="stylesheet" href="../app/application.css">

<!-- after -->
<script defer src="../sw/core.js"></script>
<script defer src="../sw/ui.js"></script>
<script defer src="page.js"></script>
<link rel="stylesheet" href="../common/theme.css">
<link rel="stylesheet" href="../sw/ui.css">
<link rel="stylesheet" href="page.css">
```

- **migrate** (`api`, also `--level includes`): yes, keeping the path prefix, attributes, quotes, `?v=` and templated prefixes (`${@Base Url}`). A page whose scripts still use legacy names after the renames also gets `sw/compat.js` / `sw/compat.css` (the old globals on top of the new bundles) and one warning listing the names.
- **check**: `legacy-include` (strict: a new page that also loads a legacy bundle), `design-legacy-page` (design: an old format page).
- **manual**: rewrite the listed names (A2-A11), then remove `sw/compat.js` / `sw/compat.css`.
- Old pages often lack `<meta name="viewport">` (phones then render them 980 px wide, zoomed out) and put the old theme colour classes on the body (`<body class="ThemeBaseBg ThemeMainFg">`). `migrate --level design` adds `<meta name="viewport" content="width=device-width, initial-scale=1">` after the charset tag (check: `design-viewport`); remove the `Theme*Bg` / `Theme*Fg` classes yourself (`ThemeBaseBg` no longer exists, `sw/ui.css` gives the body `--sw-canvas` / `--sw-text`; check: `design-legacy-class`). Add `<meta charset="utf-8">` and `<html lang="en">` while you are there.

### A2. Page entry and loader

```js
// before
function pageMain() { const done = AddLoading(document.body); ...; done(); }   // <body onload="pageMain()">
// after
Sw.Page.Main(async () => { ... }, { Loading: true });   // or keep <body onload> and call Sw.Page.Loading() / remover()
```

- **migrate**: `AddLoading(page, text, opaque, icon)` -> `Sw.Page.Loading({ Element, Text, Opaque, Icon })` (same remover), `PageLoaded()` -> `Sw.Page.SignalLoaded()`, `Open(url, target)` -> `Sw.Page.Navigate(url, target)`.
- **check**: `legacy-global` (strict).
- **manual**: nothing, unless you want `Sw.Page.Main` (error reporting, DOM ready).
- `applyTheme()`, `getTheme()`, `setTheme()`, `resetTheme()`, `toggleTheme()` and `appendTheme()` are permanent core globals (`core/theme.js`): keep them; neither migrate nor `check --strict` reports them.

### A3. Requests

```js
// before
const r = await sendRequest("../Api/GetX", arg, true, null, true);
// after
const r = await Sw.Net.Send("../Api/GetX", arg, { Reload: true, ErrHandler: null, Blob: true });
```

- **migrate**: yes (`sendRequest`, `getRequest`, `postData`, `includeJs` / `includeCss`, `Cache.*`, ...: options objects built from the passed arguments only). `downloadFile(name, url)` has the argument order swapped and is reported.
- **check**: `legacy-global`. Internet urls: `external-resource`, `external-request` (pages must work offline).

### A4. Toasts, clipboard, full screen, formatting

`Fail` / `Info` / `Message` -> `Sw.Fail` / `Sw.Info` / `Sw.Message`; `ValueFormat.*` -> `Sw.Format.*` (pure) and `Sw.Cells.*` (DOM); `FullScreen` -> `Sw.FullScreen`; `isPureClick` / `badClick` / `keyboardClick` -> `Sw.IsPureEvent` / `Sw.BadClick` / `Sw.KeyClick`; `delay`, `hashString`, `getUrlParams` ... -> `Sw.Util.*` / `Sw.Url.*`.

- **migrate**: yes for every pure rename in the default map (`SwWeb mapping` prints it), also `keyboardClick(el)` -> `Sw.KeyClick(el)`, `removeKeyboardClick(el)` -> `Sw.RemoveKeyClick(el)` and `ValueFormat.copyOnClick(el, value, keyboard, addTitle)` -> `Sw.Clipboard.CopyOnClick(el, value, { Keyboard, AddTitle })`; signature changes are reported with the new call. The new helpers set the classes `sw-keyclick` / `sw-copyable` instead of the old `KeyClick` / `MouseClick` (`design-legacy-class` reports page css on the old ones).
- **check**: `legacy-global` / `legacy-compat`.

### A5. Pop-ups and menus

```js
// before
PopUpMenu(button, (close, back) => { const m = new WebMenu(); m.Items.push(new WebMenuItem(...)); return m; });
if (await Confirm("Delete?", "The file is removed.", "Delete")) ...
// after
Sw.Menu.Show(button, [{ Text: _T("Rename"), Icon: "IconEdit", OnClick: rename }, "-", { Text: _T("Delete"), Danger: true, OnClick: del }]);
if (await Sw.Dialog.Confirm(_T("Delete the file?"), _T("This cannot be undone."), _T("Delete"), { Danger: true })) ...
```

- **migrate**: `Confirm` with up to 3 arguments is renamed; `PopUp`, `PopUpMenu`, `WebMenu`, `WebMenuItem`, `PopUpSelection`, `PopUpEdit` are reported (the shape changes).
- **check**: `legacy-global`.
- **manual**: build `Sw.Menu` items / `Sw.Dialog.Show({ Title, Content, Buttons })`; destructive actions get `Danger: true` (Cancel has the focus, Enter never confirms).

### A6. Icons and buttons

```js
// before
const b = new ColorIcon("IconReload", "IconColorThemeAcc1", 32, 32, "Reload", reload);   // b.Element
// after
const b = new Sw.Button({ Icon: "IconReload", Title: _T("Reload the list"), Kind: "ghost", OnClick: reload });   // b.E
```

- **migrate**: reported (`ColorIcon`, `Button`, `ButtonStyle`).
- A toggling icon (mute / pause, the old `ColorIcon` with `SetTitle` / `ChangeImage`): keep one `Sw.Button` and call `b.SetTitle(...)` / `b.SetIcon(...)` in its `OnClick`; `b.Element` is `b.E`. Your own mask icon classes (`.IconMute { mask-image: url(...) }` in your css) work as `Icon` names. An `OnClick` that returns a promise makes the button busy until it settles. Icons take the text colour: drop the old colour classes (`IconColorThemeBackground` ...).
- **manual**: `Sw.Button` for anything clickable (an icon only button needs a `Title`: it becomes the tooltip and the accessible name), `Sw.IconClass(cls, { Size, Color })` for a picture. No coloured icons in chrome (Design v2: icons in `--sw-text-3` / `--sw-text-2`, accent only for the current item).

### A7. Libraries (tables, editor, chat, charts, upload, media, auth)

Their old global names stay as aliases of the new ones (`Edit` === `Sw.Edit`, `TableDataState` === `Sw.Table.State` ...).

```js
// before
const t = await Table.addTable("../Api/MessageTable", host, 25, param);
// after
const t = await Sw.Table.Create({ Url: "../Api/MessageTable", Element: host, Param: param });
```

- **migrate**: pure aliases are renamed; `Table.addTable(...)` becomes `Sw.Table.Create({ Url, Element, Param, OnFirstLoad, Name, State, OnFirstData, Filter })` (the ignored `rowsPerPage` is dropped when it is a simple value, else reported). Wrappers with other signatures are reported with the new call; they keep working without `sw/compat.js`. A library that publishes its own alias (`window.Edit = Edit`) is an info, not a warning. The upload wrappers are renamed too: `fileUploaderElement`, `fileUploaderClick`, `fileUploaderDrop`, `fileUploaderDrag` / `DragEnter` / `DragLeave` -> `Sw.Upload.ForElement` / `Sw.Upload.Handlers.*`, `selectFiles(multiple)` -> `Sw.Upload.Pick({ Multiple })`, `fileUploader(repo, files, apiBase)` -> `Sw.Upload.Files(repo, files, { ApiBase })`.
- **check**: `legacy-global` (only for names of the old common.js / application.js).

### A8. Global state and window members

`window.IsTop`, `window.Mobile`, `window.Narrow`, `IsDarkMode`, `UseTheme` ... -> `Sw.Page.IsTop`, `Sw.Layout.Mobile` / `Narrow`, `Sw.Theme.IsDark`, `Sw.Theme.Forced`.

- **migrate**: reported (`window.X` is never rewritten; `typeof X` neither).
- **manual**: use the `Sw.*` property.

### A9. Translation

User visible text only through `_T` / `_TF` / `_TH` / `_TFH` with literal arguments; never rename, wrap or alias them; no `\n`, backticks or mixed quotes inside translated literals.

```js
_T("Failed to send.\n{0}", e)                                  // before: not translated (the minifier makes it a backtick literal)
_T("Failed to send.{1}{0}", e, String.fromCharCode(10))        // after (migrate does it)
```

- **migrate**: newline escapes become placeholders; `_TF` / `_TFH` without `{` become `_T` / `_TH`.
- **check**: `tr-not-literal`, `tr-member-call`, `tr-identifier-suffix`, `tr-in-text`, `tr-newline`, `tr-mixed-quotes` (errors, always).

### A10. Contract and build rules

One dot per file name, no `-` or `.` in folder names, classic scripts (no modules), nothing from the internet (no CDN, no web fonts: embed fonts as `data:` urls), JSDoc file headers, no `${` in templated files.

- **migrate**: adds a JSDoc file header to `.js` files without one (describe the file afterwards).
- **check**: `file-name-dots`, `folder-name-*`, `mjs-file`, `es-module`, `external-*`, `root-relative-url`, `font-file`, `jsdoc-header`, `template-*`, `debug-markers` (always).

### A11. Old helpers without a Sw counterpart

A few common.js helpers are only kept by `sw/compat.js`: the platform does it in a line or two, so `Sw` has no replacement. Write the line (or a small helper in your page script, named after your page, not after the old global) and drop `sw/compat.js`:

| Old | Replacement |
|---|---|
| `await setMediaSrcAndWait(video, url, ok => failed = !ok)` | `await new Promise(r => { video.addEventListener("canplaythrough", () => r(true), { once: true }); video.addEventListener("error", () => r(false), { once: true }); video.src = url; })` |
| `CaptureVideoFrame(video, mime, quality)` | `canvas.getContext("2d").drawImage(video, 0, 0)`, then `canvas.toBlob(cb, mime, quality / 100)` |
| `EnterNoSleep()` / `ExitNoSleep()` / `IsSleepPrevented()` | `lock = await navigator.wakeLock.request("screen")` / `lock.release()` / `!lock.released` (https or localhost only; a plain http kiosk keeps its own looping muted video) |
| `GetAbsoluteRect(el)` | `el.getBoundingClientRect()` + `window.scrollX` / `scrollY` |
| `IsAtMaxScrollV(el)`, `StickToBottom(el)` | `el.scrollTop >= el.scrollHeight - el.clientHeight - 1`; remember it on `scroll` and set `el.scrollTop = el.scrollHeight` after adding content |
| `SetTemporaryInnerText(el, text, ms)` | `el.textContent = text; setTimeout(() => el.textContent = old, ms)` (or a `Sw.Info` toast) |
| `getIntegerAttribute` / `getNumberAttribute(el, name, def)` | `parseInt` / `parseFloat(el.getAttribute(name))`, the default when NaN or negative |
| `insertAtCursor(field, text)` | `field.setRangeText(text, field.selectionStart, field.selectionEnd, "end")` |
| `isHidden(el)` | `getComputedStyle(el).display === "none"` (or `!el.checkVisibility()`) |
| `traverseChildren` / `traverseForward` / `traverseBackward` | `document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT)`; moving the focus: `Sw.Focus.Next` / `Sw.Focus.Prev` |
| `doHandleSrc`, `removeFadeChannel`, `InternalSrcElementOn*` | remove: `Sw.FadeSrc(el, { ... })` handles the smooth loading |

- **migrate**: reported with the replacement (map section `compatonly`). **check**: `legacy-compat` / `legacy-global` until the names are gone.

---
## Part B: Design v2

Design v2 in one sentence: a flat, structured admin console (full height sidebar, quiet top bar with the breadcrumb, left aligned page headers, cards drawn with hairlines and tone instead of shadows, a data grid with a fixed frame, copper only for meaning). Everything derives from four base colours (`--ThemeBackground`, `--ThemeMain`, `--ThemeAcc1`, `--ThemeAcc2`), so the five themes, the densities, the app colours and your brand follow automatically **if your css uses the tokens**.

### B1. Old token names (aliases)

The old names still resolve (each is defined as `var(--new)`), but new code and migrated code use the role names:

| Old | New | | Old | New |
|---|---|---|---|---|
| `--sw-bg-1` | `--sw-surface` | | `--sw-faint` | `--sw-text-3` |
| `--sw-bg-2`, `--sw-bg-inset` | `--sw-surface-sunken` | | `--sw-accent1-fg` | `--sw-text-accent` |
| `--sw-bg-3` | `--sw-surface-strong` | | `--sw-shadow` | `--sw-elev-1` |
| `--sw-line` | `--sw-border` | | `--sw-backdrop` | `--sw-scrim` |
| `--sw-line-strong` | `--sw-border-strong` | | `--sw-touch` | `--sw-control-h` |
| `--sw-muted` | `--sw-text-2` | | `--sw-size` / `--sw-speed` | `--sw-text-md` / `--sw-dur-2` |

```css
.ft-intro { color: var(--sw-muted); border-bottom: 1px solid var(--sw-line, #ccc); }        /* before */
.ft-intro { color: var(--sw-text-2); border-bottom: 1px solid var(--sw-border, #ccc); }     /* after */
```

- **migrate** (`api`): yes, every `var()` use in css, `<style>`, `style` attributes and JavaScript strings (comments skipped, fallbacks kept). `--sw-gap` is not an alias (Pro has its own): keep it or use `--sw-space-*`.
- **check**: `design-token-alias`.

### B2. Overrides of old names no longer restyle the components

```css
.big-form { --sw-touch: 52px; }        /* before: made the controls taller; now nothing reads --sw-touch */
.big-form { --sw-control-h: 52px; }    /* after */
```

- **migrate**: no. A token that any file of the run defines is not renamed anywhere in the run (its uses read the override), so the page keeps rendering as before until you move the override.
- **check**: `design-legacy-token` ("overrides an old alias").
- **manual**: set the new token; better, don't override sizes at all: the density setting (B9) does it.

### B3. Legacy theme variables in page css

The `--Theme*` variables are the theme's inputs. Pages read the derived role tokens instead (they follow dark mode, the application theme and densities):

| Old use | Use instead |
|---|---|
| `color: var(--ThemeMain)` | `var(--sw-text)` (secondary `--sw-text-2`, tertiary `--sw-text-3`) |
| `border-color: var(--ThemeMain)` / `...50` | `var(--sw-border)` / `var(--sw-border-strong)` |
| `background: var(--ThemeBackground)` | `var(--sw-canvas)` (page), `var(--sw-surface)` (cards, tables, forms), `var(--sw-surface-sunken)` (wells, heads) |
| `color: var(--ThemeAcc1)` | `var(--sw-text-accent)` only for links / the current item; headings and labels are `var(--sw-text)` |
| `background: var(--ThemeAcc1)` | `var(--sw-accent1)` for the one primary action, `var(--sw-selected)` for selected / current items |
| `rgba(var(--ThemeMainRGB), 0.1)`, `var(--ThemeMain25)` | `var(--sw-hover)`, `var(--sw-pressed)`, `var(--sw-selected)`, or `color-mix(in srgb, var(--sw-fg) 10%, transparent)` |
| `var(--ThemeRad)` / `--ThemeRadInp` / `--ThemeRadInp2` | `var(--sw-radius-md)` (cards, menus) / `var(--sw-radius)` (controls) / `var(--sw-radius-sm)` |
| `var(--ThemeFont)` / `--ThemeFontHead` / `--ThemeFontMono` | `var(--sw-sans)` / `var(--sw-head)` / `var(--sw-mono)` |
| `var(--ThemeIconSize)`, `--ThemeFontSize` | `var(--sw-icon)`, the type scale `--sw-text-xs` .. `--sw-text-3xl` |
| `var(--ThemeError)` / `--ThemeOk` / `--ThemeWarning` / `--ThemeFocus` | `var(--sw-danger)` / `--sw-ok` / `--sw-warning` / `--sw-focus` |
| `var(--SysWeaverEditDesktopRad)` ... (edit/Edit.css) | `var(--sw-radius-md)`, `--sw-radius`, `--sw-radius-sm`, `--sw-dur-2` |

- **migrate**: no (not the same value everywhere: a page without `sw/ui.css` has no role tokens, and the right role depends on the property). Reported with the property aware suggestion.
- **check**: `design-legacy-token`.
- **manual**: pick the role. Defining `--Theme*` in a theme (`app/app.css`) is fine and not reported.

### B4. Colours: tokens, not literals

```css
.badge-new { background: #393; color: #fff; }                              /* before: wrong in dark, pro, hacker, app colours */
.badge-new { background: var(--sw-ok-soft); color: var(--sw-text); }       /* after (or a Sw.Badge(text, "ok")) */
:root { --acme-badge: #4fc3f7; } .x { color: var(--acme-badge); }          /* an own colour: define a token (per scheme if needed) */
```

Roles: text `--sw-text` / `-2` / `-3`, surfaces `--sw-canvas`, `--sw-nav`, `--sw-surface`, `--sw-surface-sunken`, `--sw-surface-raised`, lines `--sw-border`, `--sw-border-strong`, `--sw-border-input`, states `--sw-hover`, `--sw-pressed`, `--sw-selected(-strong)`, status `--sw-ok`, `--sw-warning`, `--sw-danger` (+ `-soft`), `--sw-info`, accent `--sw-accent1` / `--sw-text-accent` / `--sw-on-accent`, data `--sw-chart-1..7`. Status is always icon + text, never colour alone.

- **migrate**: no.
- **check**: `design-color` (hex, colour functions with literal values, named colours in colour properties; custom property definitions, `var()` fallbacks, `url()`, forced colours and masks are fine).
- Colours that are data, not theme, stay fixed on purpose: text and badges over a video or photo (the name / status overlays of a display client: always light on a dark scrim), the 16 colour ANSI palette of a terminal, a colour swatch. Define them once as app tokens on `:root` (`--tiu-overlay-bg: rgb(0 0 0 / 0.85)`, `--fenrir-ansi-1: #000080` ...) with a `SwWeb:allow-design (why)` comment, and use `var()` in the rules; a surface that is always dark (a terminal) also sets `color-scheme: dark`.

### B5. Elevation: flat surfaces

| Level | Token | For |
|---|---|---|
| 0 | `none` + `1px solid var(--sw-border)` | header, sidebar, cards, tables, inputs, buttons, tiles, sections |
| 1 | `var(--sw-elev-1)` | menus, popovers, select lists, tooltips |
| 2 | `var(--sw-elev-2)` | dialogs, drawers, toasts |
| scrim | `var(--sw-scrim)` | behind a dialog / drawer |

```css
.card { box-shadow: 0 4px 12px rgba(0, 0, 0, 0.35); border-radius: 12px; }                         /* before */
.card { box-shadow: none; border: 1px solid var(--sw-border); border-radius: var(--sw-radius-md); } /* after (or use .sw-card / Sw.Card) */
.panel { box-shadow: 0 0 32px var(--sw-shadow); }       /* invalid: --sw-shadow is a complete shadow, the browser ignores this */
```

No glows (`text-shadow`, `drop-shadow()`, coloured shadows). Focus: `outline: 2px solid var(--sw-focus)` or `box-shadow: var(--sw-focus-ring)`.

- **migrate** (`design`): the invalid `<lengths> var(--sw-shadow)` becomes `none` (a floating layer then gets `var(--sw-elev-1)` by hand); other shadows: no.
- **check**: `design-shadow` (rings `0 0 0 Npx`, inset lines, focus rings, elevation tokens and single app tokens are fine), `design-inline-style` for shadows set from JavaScript.

### B6. Gradients

No gradients on chrome, cards, bars or buttons; tone + hairline instead. Repeating patterns (hatching), masks and real colour swatches are fine (mark swatches with `SwWeb:allow-design`).

- **migrate**: no. **check**: `design-gradient`, `design-inline-style`.

### B7. Radii, spacing, type, motion

Radii `--sw-radius-sm` 4, `--sw-radius` 6 (controls), `--sw-radius-md` 8 (cards, menus, tables), `--sw-radius-lg` 12 (dialogs), `--sw-radius-pill` (badges, chips, switches); Pro and Hacker have their own scale, so never write px radii. Spacing on the 4 px grid `--sw-space-1` .. `--sw-space-12`; page padding `--sw-page-px`. Type `--sw-text-xs` 12 .. `--sw-text-3xl` 32 (rem: the root size scales on 2560 / 4K screens and in Pro), headings in `--sw-head`, numbers with `font-variant-numeric: tabular-nums` (no monospace for numbers). Motion `--sw-dur-1/2/3` + `--sw-ease`, nothing animates with `prefers-reduced-motion`.

- **migrate**: no (only the aliases of B1). **check**: `design-radius` (fixed radii), `design-legacy-token` for `--ThemeRad*` / `--ThemeFont*`; px font sizes and paddings are not checked.
- **manual**: replace px radii / font sizes / paddings by the tokens.

### B8. Bare element rules

`button { width: 100% }`, `input { ... }`, `body { font-size: 18px }` or `body { overflow: hidden }` in page css also change every Sw component (pagers, dialogs, menus) and clip phones. Scope them: `.my-page button`.

- **check**: `element-css`, `body-overflow` (always, warnings). A custom element of your page scopes like a class (`tiu-display input` is fine; `sw-*` and `SysWeaver-*` are framework elements and don't scope).
- A full window app (a display client, a kiosk, a terminal) may hide the body overflow: mark the rule with `/* SwWeb:allow-element-css (why) */` and the page with `<!-- SwWeb:no-page-header (why) -->`.

### B9. Density

`html[data-density="compact | comfortable | touch"]` (`Sw.Theme.Density`, the Settings menu, `?density=`; default comfortable, compact for Pro, touch on touch screens) sets `--sw-control-h` (28 / 34 / 44), `--sw-row-h` (32 / 40 / 48), `--sw-nav-row-h`, `--sw-icon`, `--sw-cell-px`, `--sw-text-md`. Size your own controls and rows with these tokens, never with fixed 44 px heights or media queries on `pointer`.

- **migrate**: `--sw-touch` uses become `--sw-control-h` (B1). **check**: overrides of the old names (B2).

### B10. Page anatomy (page header and container)

Every page: breadcrumb (the shell's top bar) -> **page header** (title, one line description, status, actions) -> content in a **page container**: `dash` (dashboards, max 1600 px), `form` (forms, 1200 px), `read` (text, 720 px), `fill` (tables, logs, chat: full width and height).

```js
// before: an own heading and wrapper
const main = el("main.ft-page");
main.append(el("h1.ft-title", { text: _T("Fake text messages") }), el("p.ft-intro", { text: _T("Simulate a text message ...") }));
document.body.append(main);

// after: the shared anatomy
const page = Sw.PageLayout({ Type: "form", Title: _T("Fake text messages"), Description: _T("Simulate a text message ..."),
    Actions: [new Sw.Button({ Text: _T("Simulate incoming text"), Kind: "primary", OnClick: send })] });
page.Add(form, table);          // page.E is main.sw-page-form, page.Header the Sw.PageHeader
```

Static markup works too: `<main class="sw-page-form"><header class="sw-ph"><div class="sw-ph-text"><div class="sw-ph-titlerow"><h1 class="sw-ph-title">...</h1></div><p class="sw-ph-desc">...</p></div><div class="sw-ph-actions">...</div></header>...</main>`.

- In the shell a level 1 page header shows the **menu item name** (`Sw.MenuTitle()`), so the h1, the breadcrumb and the menu agree; `MenuTitle: false` for a page about one record. Name your menu items as the pages should be titled.
- One primary action per region; destructive actions `Kind: "danger"` (outline) with a confirmation; fill pages use `Compact: true` (hidden on phones, the top bar shows the title).
- Table pages (`Sw.Table.Create({ Page: true })`, `explore/table.html`) make their own compact header from the menu name / table title.
- Header actions never wrap or squeeze (F49-Left): `Sw.PageHeader` keeps them on one line and moves the last ones that do not fit into a "More actions" kebab menu (the primary action and non button controls stay). Pages need no change; drop own css that wrapped or shrank `.sw-ph-actions` children, and put the most important actions first (the last ones move first). `Overflow: false` keeps the old wrapping row.
- A text file viewer on a menu page (`edit/text.html` opened by a menu item, ex: Log file) shows the menu name as its h1 and the file name as the description; links to `edit/text.html` from tables are unchanged (the file name stays the heading).
- Remove centred titles, hero logos and "Welcome x" pills from content pages (the brand lives in the sidebar).
- A page of many alike things (displays, devices, cameras) that placed fixed size boxes with `position: absolute` / `style.top`: `Sw.PageLayout({ Type: "dash", ... })` + `Sw.Grid({ Content })` of `Sw.Card({ Title, Sub, Tools: [buttons], Body, Flush: true, Span: 6 })`, and `Sw.StateView({ Kind: "empty", ... })` when there are none. A fixed size preview (an iframe of a 1280x720 page) keeps its layout at any card width with `aspect-ratio: 16 / 9` on a wrapper and the iframe at `200%` size with `transform: scale(0.5)`.
- **migrate**: no (structure). **check**: `design-page-header` (says whether the page makes its own `<h1>` and lacks a container), `design-page-container` (a header without a container). **manual**: as above, or `<!-- SwWeb:no-page-header -->` for a page without one on purpose.

### B11. Dashboard parts instead of page specific widgets

| Old pattern | Design v2 part (ui/page.js, ui/page.css) |
|---|---|
| fieldset look, `Sw.Group` with the title in the border | `Sw.Card({ Title, Sub, Tools, Body })` / `section.sw-card` (a `Sw.Group` is a card now; `--sw-frame-bg` is obsolete) |
| a row of fact pills (host, OS, version ...) | `Sw.Facts({ Items: [{ Label, Value, Href }] })` (`dl.sw-facts`) |
| a donut / gauge for one value of a total | `Sw.Kpi({ Label, Icon, Value, Unit, Meter: { Max, Warn, Danger } })` |
| bar series over time, rotated time labels | a line chart in a card (`chart/chart.html?type=line&title=false`) |
| ad hoc lists with status pills | `Sw.ListCard({ Title, Items: [{ Text, Status: { Text, Kind, Dot }, Meta, Href }], Empty })` |
| spinners, "No data" text, red error text | `Sw.StateView({ Kind: "loading" \| "empty" \| "error", Title, Text, Actions, Details })` / `Sw.EmptyState` |
| layout tables, ad hoc flex grids | `Sw.Grid({ Content })` / `.sw-grid` + `.sw-span-3/4/6/8/12` |

Pills / badges (`Sw.Badge`, `Sw.Chip`) are for status and filters only.

- **migrate**: no. **check**: `design-pill-facts` (pill / stat chip css), `design-fieldset` (fieldset frames), and the generic `design-shadow`, `design-gradient`, `design-color` on the old widget css; donuts for one value are not detectable statically (chart urls are built at run time). **manual**: rebuild with the parts.

### B12. Components and states

- Buttons: default (bordered surface), `primary` (accent fill, at most one per region), `ghost` (toolbars, icon buttons), `danger` (outline; solid only inside the confirmation). No press transforms.
- Tabs are underline tabs (`Sw.Tabs`), switches `Sw.Toggle` (`role=switch`), segmented choices `.sw-seg`, inputs `Sw.TextBox` / `Sw.NumberBox` / `Sw.Select`.
- Dialogs: `Sw.Dialog` (bottom sheet on phones, `--sw-elev-2`); menus `Sw.Menu`; toasts `Sw.Toast` / `Sw.Fail` (inverse surface, bottom right).
- States: hover `--sw-hover` (only with `@media (hover: hover)` for rows), pressed `--sw-pressed`, selected `--sw-selected`, focus 2 px `--sw-focus`, disabled 45 % opacity.
- Restyle a component through its tokens (`--sw-control-h`, `--sw-radius`, `--sw-border-input` ...) on a page scope, never through its inner classes.
- **check**: `element-css`, `design-legacy-class` (styles on old element hooks such as `SysWeaver-Button`, `SysWeaver-PopUpBackground`, `.KeyClick`: only the legacy compat layer creates those, the components use `.sw-*`).

### B13. Tables

Table pages and embedded tables get the fixed frame automatically (tool bar on top with search and tools, filter chips, rows area that takes the free height, one line footer with fixed slots; skeleton / empty / error states; cards on phones). The C# TableData attributes, the request / response, `Sw.Table.Create` options and the legacy `Table.addTable` are unchanged. If your app styles tables itself:

| Change | What to do | SwWeb |
|---|---|---|
| `Table.addTable(url, el, rowsPerPage, ...)` | `Sw.Table.Create({ Url, Element, Param, ... })`; the page size is automatic (or the user's choice) | migrate (`api`) |
| The rows, skeleton and states moved into `div.sw-table-area`: `.sw-table > .sw-table-scroll`, `.sw-table-page > .sw-table-empty` no longer match | drop the `>` or write `.sw-table-area > ...` | check `design-legacy-class` |
| Search and tools moved from the footer to the tool bar: `.sw-table-tools` is gone (`.sw-table-ttools > .sw-table-tbtn`), `.sw-table-foot > .sw-table-search` no longer matches | move the rules to `.sw-table-toolbar` / `.sw-table-ttools` | check `design-legacy-class` (map entry) |
| Footer slots (`.sw-table-fleft`, `.sw-table-pps`, `.sw-table-nav`, `.sw-table-pageno`) never wrap and only grow | don't set `flex-wrap`, widths or `min-width` on them | - |
| A page table (`Page: true`) fills its host | give the host a definite height (a flex column child with `min-height: 0`, ex: `.sw-fill` in `.sw-page-fill`) | - |
| Rows are `--sw-row-h` high (density), sentence case muted headers, tabular numbers, striped rows only with the Density menu's "Striped rows" (`.sw-table-zebra`, default on in compact) | restyle with the tokens (`--sw-row-h`, `--sw-cell-px`), not the selectors | - |
| The page header h1 is the menu item name | name the menu item as the page should be titled | - |
| `sw-table-loading` is only on until the first answer | use `.sw-table-busy` as a busy hook | - |
| **Footer rule** (F49-Left): page tables and **fill tables** have the footer at the bottom of the frame; other embedded tables are as high as their rows (footer after the last row, sticky at the window bottom). An embedded table whose container has the class `sw-fill` (ex: a `Sw.PageLayout` fill page section) or `sw-table-fill` is a fill table automatically (option `Fill: true / false` overrides) | a fill page with a form and a table: give the table host `.sw-fill` and drop own css that tried to stretch the table (`.host > * { flex: 1 }`, heights on `.sw-table`) | - (no reliable pattern) |
| Per table empty texts: `EmptyTitle` / `EmptyText` options (`table.html`: `et` / `ed` url parameters); default "No rows to show" / "There is nothing in <name> right now." | set them where an empty table needs a sentence of its own (ex: "No messages yet" / "Simulated texts show up here.") | - |
| `Header` option: page tables can hide their compact header (`Header: false`, `table.html?...&header=false`), embedded tables can show one (`Header: true`, an h2 with the table name and the note) | an iframe of `table.html` inside a page with its own `Sw.PageHeader`: add `&header=false` | - |

### B14. Editors and forms

Forms built with `Sw.Edit` (the object editor) and `Sw.PropertyEditor` get the Design v2 rows by themselves: a label column that wraps (never clipped, never italic) with the type / constraint hint under the label, help and validation under the control, flat group sections, collections with a "+ Add" button, drag handles and an item menu, a dashed `null` token with "Set a value", read-only values rendered as text (no input), a colour editor, the writable JSON tab of the api page, a sticky footer with the primary action.

- Don't build your own label / value tables for C# types: use `Sw.Edit` (`edit/Edit.js`) with the type info; for settings use `Sw.PropertyEditor`.
- Don't style the editor's inner rows; the label width is `--sw-label-w`.
- A null result is never an object editor: show an empty state (`Sw.StateView`) for "no value".
- Read only stays read only: a whole-object paste or drop keeps the current values of read only members (one toast says what was kept), so don't re-implement paste for your types. Code that loads a new record into an existing editor uses `await editor.SetObject(value)` (not merged, not undoable); user edits go through `ReplaceObject` (merged, one undo step). Pasted / dropped collection items lose a `$type` equal to the element type.
- Colour text boxes read `0x` hex in non-integer members as `0xRRGGBBAA` (before `0xAARRGGBB`; a note says so); integer layouts (UInt32Argb ...) are unchanged. `EditTypeUnit.js` is part of the editor files: a page that loads them itself (rare, `Sw.Edit.Init` does it) must not skip it.
- A page that lets users type json for a type validates it with `await Sw.Edit.CheckJson(type, value)` (null or `{ Path, Text }`; every depth, with the line of the error, `Sw.Edit.JsonLines`) instead of `JSON.parse` alone.
- **Describe your C# members, the editor does the rest** (server side, one attribute each, backward compatible): `[EditUnit(EditUnits.Seconds)]` (durations, sizes, ratios: a number in its unit with a readable hint, typed units such as "1h 30m" / "32 MB", Min / Max notes, optional slider), `[EditColor(EditColorFormat.Hex)]` / `Css` / `Rgba8` ... (a colour editor in the stored format, alpha kept or reported), `[EditPassword]` (secrets), `[EditType(EditTypes.Url / Phone / Email)]` (input mode and validation; not on members that also take other forms such as `$(...)` templates). They change only the editor, never the stored value or the json; `EditUnit`, `EditColor` and `EditType` are editor types (one per member, not combined with `EditHideIf`). Without them the members stay plain numbers / texts. The shared `ApiKeyParams.ApiKey`, `CredentialParams.Password` and `AppColorParams` already have them.
- `explore/api.html` is a Design v2 page (page header, a meta line of badges, the signature, Input / Result cards with a sticky footer): its old boxed sections (`.api-section`, `.api-head`, `.api-tags`, `.api-send`, `.api-desc`, `.api-lock`, `.api-message-*`) are gone; style `.api-meta`, `.api-tag`, `.api-signature`, `.api-card` or the tokens.
- **migrate**: the `Edit*` legacy aliases (A7). **check**: `design-legacy-class` (the removed api.html sections), the generic rules (`design-*`, `element-css`) on your form css.

### B15. Shell

The shell is a CSS grid: a full height sidebar (brand at its top, app identity, the menu tools Pin / Collapse all / Expand all above the search, section labels, monochrome icons, the current item in accent) and a top bar over the content with the breadcrumb (`app-ContentTitle` is the breadcrumb container now). No shadows or gradients; the Application theme marks the sidebar with a 3 px accent edge. The element names `app-header`, `app-menu`, `app-ContentTitle`, `app-content`, `app-PinButton` (now inside the menu tools) are kept, so `app/app.css` rules on them still match; rules that drew header gradients, shadows or a centred title are obsolete.

- Icons (F49-Left, Design v2 3.12): the shell chrome uses the line set (`Sw.Icon`), menu icons are monochrome masks (`--sw-text-3`); full colour menu icon urls (`...svg*`) and `IconLogo` are shown as masks in the sidebar, the rail and the shell's popup menus. Want colour? Show it in content (Home tiles, your pages). App css on the old header icon classes (`app-SettingsButton .IconSettings`, `app-BackButton .IconNavBack`) must target `.sw-shell-hicon` (an svg now).
- **check**: the `design-*` rules on `app/app.css` like on any page css.

### B16. Themes and branding without forking

Everything an app brands lives in its own files (the folder added last wins); the framework always serves an empty `app/app.css` / `app/app.js`, and `app/app.css` reaches every page (shell, frames, stand-alone and old pages):

```css
/* app/app.css */
:root { --ThemeAcc1: #c2185b; }                                   /* the accent in every theme */
:root[data-sw-scheme="dark"] { --ThemeAcc1: #f06292; }           /* per scheme */
:root { --sw-brand-logo: url("../app/acme-logo.svg"); --sw-brand-icon: url("../app/acme-mark.svg"); }
[data-theme="acme"] { --ThemeBackground: #101418; --ThemeMain: #e8ecef; --ThemeAcc1: #4fc3f7; --ThemeAcc2: #9fb6c8; color-scheme: dark; }
:root { --sw-chart-2: #2a78d6; }                                  /* a chart slot */
.acme-banner { background: var(--sw-surface); border: 1px solid var(--sw-border); }   /* own classes on tokens */
```

- Change the four base colours (or a whole theme), never the derived tokens one by one; declare `color-scheme` in a theme; keep text and accents >= 4.5:1. Register themes with `Sw.Theme.Register` in `app/app.js` when they need code.
- Browser and install icons: your own `icon.svg`, `favicon.ico`, `logo.svg` ... at the root of your web folder.
- No `@import` in `app/app.css`, urls in custom properties written as `../app/<file>`, fonts as `data:` urls.
- Full guide: [`_docs/WebBranding.md`](../../_docs/WebBranding.md).
- **check**: `css-import`, `brand-url`, `font-file` (always), the `design-*` rules for the classes in `app/app.css`.

### B17. App colours (Application theme)

The Application theme gives every app its own hue so many open service tabs are distinguishable: the server derives the colours from `AppSeed` (`SysWeaver.AppInfo` params in `*.Services.json`); the same seed gives the generated icon and logo. Explicit colours are an opt-in: `"AppColors": { "Accent1": "#4FC3F7" }` (also `Accent2`, `Background`, `Main`). Choose seeds of apps often open together at least 40° apart (Debug > Logo design previews them). Pages get this for free when they use the role tokens (B3, B4); hard-coded colours don't follow it.

### B18. Custom controls must be accessible

```html
<div class="tile" onclick="open()">Reports</div>                     <!-- before: no keyboard, no role -->
<button class="tile" onclick="open()">Reports</button>               <!-- after (or role="button" tabindex="0" + Sw.KeyClick) -->
<button><span class="IconClose"></span></button>                     <!-- before: an icon only button without a name -->
<button aria-label="Close" title="Close"><span class="IconClose"></span></button>
```

Things that open a page are `<a href>` links (middle click, copy link), actions are buttons, images have `alt` (`alt=""` for decoration).

- **migrate**: no. **check**: `a11y-custom-control` (HTML `onclick` on non-controls, `el("div", { on: { click } })` without role / tabindex), `a11y-name` (icon only buttons and links, images without alt).

### B19. Styles set from JavaScript

`x.style.boxShadow = "0 2px 8px #000"`, `x.style.background = "linear-gradient(...)"`, `"color: #fff"` style strings bypass the themes. Toggle a class whose css uses tokens instead (inline values that come from data, ex: a user picked colour, are fine: mark them).

- **migrate**: token aliases inside such strings are renamed (B1). **check**: `design-inline-style`.

### B20. Framework pages your app builds on

If your app links to, embeds or restyles the framework's own pages, these changed with Design v2 (the pages themselves need nothing from you):

- **Home** (`app/`): a dashboard (KPI tiles, shortcuts, recent pages, problems); inside the shell the status badge only shows news (not connected, problems: the sidebar footer shows "Connected, server time"), the user picture only when it is an upload target. `app/app.css` rules on `.HomeStatus` / `img.UserImage` keep working.
- **ServerManager**: the classes `.sm-stat*`, `.sm-charts`, `.sm-history`, `.sm-commands`, `.sm-danger*`, `.si-name`, `.si-state`, `.si-grid*`, `.vi-name`, `.vi-values`, `ss-add`, `kf-table` and the global `smStat` are gone (facts strip, KPI tiles, line cards, page header actions); `SmShared.KpiSub`, `LineCard`, `UploadButton`, `FillTable` (a fill page around `Sw.Table.Create({ Page: true })`) are the shared helpers. Use `Compact: false` on a fill page header that has actions (a compact header is hidden on phones, its actions with it). **check**: `design-legacy-class` reports css on the removed classes.
- **Auth pages**: `Sw.AuthUi.Page(icon, build, { Account: true, Description })` gives signed-in account pages the form layout (page header + card), stand-alone pages keep the centred card; tool pages call `page.SetHeader({ Title, Description, Actions })`.
- **Chart page**: inside the shell `chart.html` gets a compact page header and a card; embedded tiles (`m=false` / `transparent=true`) are unchanged; `title=false` hides the chart's own title when a card head names it.
- **Log file, translator, upload test, fake email, FakeText, chat**: fill pages with the buttons in the page header actions (no sticky bars with shadows); record pages use `Sw.Facts` + cards with `MenuTitle: false`; app Home overrides keep the app name (`MenuTitle: false`); chat lost its glow and inset shadows (hairlines).

---

## Summary: what SwWeb does

| Rule | migrate | check |
|---|---|---|
| A1 includes | yes (`api`, `includes`) | `legacy-include`, `design-legacy-page` |
| A2-A4, A7 legacy globals with a pure / options mapping | yes (`api`) | `legacy-global`, `legacy-compat` |
| A5, A6, A8 shape changes (menus, pop-ups, icons, window state) | reported | `legacy-global` |
| A7 `Table.addTable` | yes (`api`) | - |
| A9 translation newlines | yes (`api`) | `tr-*` |
| A10 contract | JSDoc headers | `file-name-*`, `folder-name-*`, `external-*`, `es-module`, ... |
| A11 helpers without a Sw counterpart | reported with the platform replacement | `legacy-compat`, `legacy-global` |
| A1 viewport meta tag, old `Theme*Bg` / `Fg` classes | viewport added (`design`); classes reported | `design-viewport`, `design-legacy-class` |
| B1 old token names | yes (`api`) | `design-token-alias` |
| B2 overrides of old names | reported (blocks the renames of that token) | `design-legacy-token` |
| B3 `--Theme*` in page css | reported with the role | `design-legacy-token` |
| B4 hard-coded colours | - | `design-color` |
| B5 shadows | invalid `var(--sw-shadow)` shadows -> `none` (`design`) | `design-shadow`, `design-inline-style` |
| B6 gradients | - | `design-gradient`, `design-inline-style` |
| B7 radii / type | aliases only | `design-radius`, `design-legacy-token` (`--ThemeRad*`, `--ThemeFont*`) |
| B8 bare element rules | - | `element-css`, `body-overflow` |
| B10 page header / container | - | `design-page-header`, `design-page-container` |
| B11 pills, fieldsets | - | `design-pill-facts`, `design-fieldset` |
| B12 old element hooks | reported | `design-legacy-class` |
| B13 table structure | `Table.addTable` | `design-legacy-class` (`.sw-table-tools`, child selectors) |
| B14 editors (removed api.html sections, C# edit attributes) | `Edit*` aliases | `design-legacy-class` |
| B18 custom controls | - | `a11y-custom-control`, `a11y-name` |
| B19 inline styles | aliases in strings | `design-inline-style` |
| B20 removed framework page classes | reported | `design-legacy-class` |

Not detectable statically (review by hand): a donut for one value (chart urls are built at run time), a command row at the bottom of a page (its actions belong in the page header), an h1 that differs from the menu name (the UI runner checks it), px font sizes / paddings, child selectors into components other than the table frame.

The structural changes (B10-B14: page anatomy, dashboard parts, components) stay manual by nature; `check --design` finds the pages and css that still need them.

## Verify

- `SwWeb check <srcWeb> --strict --design`: 0 findings (or marked exceptions with a reason).
- In the browser, in the shell and stand-alone: the five themes (light, dark, pro, application, hacker), desktop 2560x1440 / 1920x1080 / 1366x768, phone 390x844 and 844x390, keyboard only (Tab, Enter / Space, Escape, arrows), no console errors.
- Compare with the reference pages: the component gallery `ui/gallery.html` and the Design v2 prototype `ui/design.html` (`?theme=`, `?density=`, `?view=home|server|table|editor`).

## Relationships

- **Tool:** [SwWeb](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwWeb/README.md) (`migrate`, `check`, `mapping`, `rules`)
- **References:** [`core/README.md`](core/README.md), [`ui/README.md`](ui/README.md) ("Theming", "Page anatomy"), [`shell/README.md`](shell/README.md), [`_docs/Web.md`](../../_docs/Web.md), [`_docs/WebBranding.md`](../../_docs/WebBranding.md), [`_docs/WebQuickStart.md`](../../_docs/WebQuickStart.md)
