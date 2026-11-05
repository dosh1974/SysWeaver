# Porting an app to the new front end and Design v2

[⬆ srcWeb](README.md)

> How to move the web pages of an app (any project with a `srcWeb` / `srcData` folder) from the old UI (`common/common.js`, `app/application.js`), or from no framework at all (plain html / js / css, [A0](#a0-a-page-that-does-not-use-the-framework)), to the `Sw` core / ui / shell and the Design v2 look. Each rule says what changed, shows a before / after, and whether [`SwWeb`](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwWeb/README.md) does it (**migrate**), finds it (**check**) or leaves it to you (**manual**).

Nothing breaks if you do nothing: old pages keep working on the legacy bundles, and the framework pages, the shell, the tables and the editors get Design v2 by themselves. Porting means adopting the new API and the shared page parts, so the next restyle of the framework reaches your pages too. **Never fork or copy framework files** (`sw/*`, `ui/*`, `common/theme.css`): brand through `app/app.css`, `app/app.js` and the `*.Services.json` ([B16](#b16-themes-and-branding-without-forking)).

The section ids (A0 .. A13, B1 .. B20, C1 .. C6) are stable: the SwWeb messages and the framework READMEs refer to them.

Contents: [Checklist](#checklist-porting-a-page) · [Workflow](#workflow) · [Part A: old UI to Sw](#part-a-from-the-old-ui-to-the-sw-core-ui-and-shell) · [Part B: Design v2](#part-b-design-v2) · [Part C: UX guide rules](#part-c-ux-guide-rules) · [Summary table](#summary-what-swweb-does) · [Verify](#verify)

## Checklist: porting a page

1. **Worksheet:** `SwWeb migrate <srcWeb> --dry-run --level design --report port.md` lists every automatic change and every manual item ([Workflow](#workflow)). A page that loads no SysWeaver bundle at all is "framework-free": the worksheet lists its port steps, start with [A0](#a0-a-page-that-does-not-use-the-framework).
2. **Automatic part:** `SwWeb migrate <srcWeb> --backup <dir>`, then `SwWeb migrate <srcWeb> --level design`: includes (with `common/theme.css`), API renames, token renames, viewport, `sw-root`, danger confirmations, library includes, `_T` around plain texts, the framework's copy of a CDN library ([A1](#a1-includes-and-page-setup), [A5](#a5-dialogs-menus-and-toasts), [A7](#a7-libraries-tables-editor-chat-charts-upload-media-auth), [B1](#b1-old-token-names-aliases), [B3](#b3-legacy-theme-variables-in-page-css)).
3. **Entry point:** the page code runs from its own deferred script with `Sw.Page.Main` ([A2](#a2-page-entry-and-loader)).
4. **Page anatomy:** `Sw.PageLayout({ Type, Title, Description, Actions })`, content in `Sw.Card` / `Sw.Grid`, tables in a `.sw-fill` host ([B10](#b10-page-anatomy-page-header-and-container), [B11](#b11-dashboard-parts-instead-of-page-specific-widgets), [B13](#b13-tables)). Delete the page's own header, wrapper and widget css.
5. **Left over names:** rewrite what migrate reported (menus, buttons, window state, old helpers: [A5](#a5-dialogs-menus-and-toasts), [A6](#a6-icons-and-buttons), [A8](#a8-global-state-and-window-members), [A11](#a11-old-helpers-without-a-sw-counterpart)), then remove `sw/compat.js` / `sw/compat.css`.
6. **Page css on tokens:** role tokens, no literal colours, no shadows on permanent surfaces, no gradients, radius tokens, no bare element rules ([B3](#b3-legacy-theme-variables-in-page-css)-[B8](#b8-bare-element-rules)).
7. **Dialogs and texts:** dialog rules (question titles, verb buttons, `Danger` for destructive actions), texts through `_T` ([A5](#a5-dialogs-menus-and-toasts), [A9](#a9-translation-and-texts)).
8. **UX rules:** click actions, hover only in `@media (hover: hover)`, `aria-disabled` instead of `disabled`, motion tokens, reserved media sizes, labels, tool tips that add something ([Part C](#part-c-ux-guide-rules), the [UX guide](../../_docs/UX_Guide.md)).
9. **Gate:** `SwWeb check <srcWeb> --strict --design` = 0 findings, then the browser checks of [Verify](#verify). 0 findings is necessary, not sufficient: the gate knows the patterns of this guide (also the indirect ones: own custom properties, `setProperty` / `Object.assign` / `cssText`, `window["confirm"]`, a header hidden by css), not your page's look; look at it in the five themes and on a phone.

## Workflow

```
SwWeb migrate <srcWeb> --dry-run --level design --report port.md   # 1. the worksheet: changes = automatic, warnings = manual
SwWeb migrate <srcWeb> --backup <dir>                                # 2. the automatic part (includes, API renames, token aliases)
SwWeb migrate <srcWeb> --level design                                # 3. + the Design v2 rewrites (same report, manual items stay)
SwWeb check <srcWeb> --strict --design                               # 4. fix the manual items until 0 findings
```

- The worksheet **is the list that `check --strict --design` gives after the migration** (the migrated texts are checked in memory): each `check:` / `design:` line names the rule, the file and the line of the file as it is now; the report starts with the count per rule. `migrate --level design` does all of its changes in one run (a second run changes nothing), so the dry run counts exactly what the real run changes; for a framework-free page the worksheet also lists the `no-framework: port step:` lines, and the steps that stay after the includes are check rules (`tr-untranslated`, `design-font`, `design-own-ui` for an own fetch, `design-page-header`).
- `migrate` is idempotent and changes only what is safe (renames of the same value, include tags, one-answer replacements); everything else is a warning with the suggested replacement. Run it again after manual fixes: it adds includes for libraries your new code uses ([A7](#a7-libraries-tables-editor-chat-charts-upload-media-auth)) and says when the compat includes can go.
- `check --strict` = the server contract (translation, templates, file names, offline, legacy globals in new pages). `--design` adds the Design v2 rules (`design-*`, `a11y-*`, warnings).
- Deliberate exceptions carry a reason: `/* SwWeb:allow-design (why) */`, `/* SwWeb:allow-element-css (why) */` on the line or the line before, `<!-- SwWeb:no-page-header (why) -->` / `<!-- SwWeb:no-sw-root (why) -->` / `<!-- SwWeb:no-framework (why) -->` in the page, `/* SwWeb:allow-token (why) */` for a `--sw-*` token that a host page defines.
- `check` also stops mistakes that only show at run time: `_TF` with format arguments and `_T` placeholders without an argument (`tr-format`, [A9](#a9-translation-and-texts)), a `var(--sw-...)` that no token defines (`unknown-token`, it names the closest token, [B7](#b7-radii-spacing-type-motion)), a library api without its include (`missing-include`), inline `Sw` code before the deferred bundles (`script-order`), a page without `common/theme.css` (`theme-include`: it stays light in the dark themes), an alias of `_T` (`tr-alias`).
- Exceptions for texts: `// SwWeb:no-tr (why)` on the line (or the line before) of a literal that must not be translated (a product name, a code), `SwWeb:no-tr-file (why)` in the header comment of a developer-only file.
- Several projects merge into one web root at run time: migrate all your `srcWeb` folders in one run (a common parent, ex: the repository). On a folder above the web roots `migrate` and `check` only look at its `srcWeb` / `srcData` folders (build outputs, `*.br`, `*.Services.json` and project folder names are left out).
- Your own libraries with legacy names: `--map my_map.json` with a section per library (`Globals`, `Css`, `Tokens`; format in the SwWeb README).

---

## Part A: From the old UI to the Sw core, ui and shell

### A0. A page that does not use the framework

A page that loads neither `sw/core.js` / `sw/ui.js` / `sw/ui.css` nor the old `common/common.js` (its own `fetch` wrapper, toasts, `<dialog>`, light / dark palette, theme toggle, svg icons, `confirm()`) gets none of the themes, densities, translation, dialogs or the shell title, and no other rule can judge it. Porting it is mostly deleting code: the components do what the page did by hand.

- **check**: `no-framework` on the page (a warning; an **error** with `--strict` in a `srcWeb` folder) plus one `port step:` warning per thing the page does itself, at its line (in the page and its own scripts / style sheets). After the includes, the remaining steps are check rules until they are gone: `ux-pop-9` (native `confirm()` / `alert()` / `prompt()`, also as `window["confirm"]` or a reference), `ux-pop-1` (an own `<dialog>`), `design-own-ui` (an own `fetch` / `XMLHttpRequest`, `prefers-color-scheme` css, an own colour palette), `tr-untranslated` (texts made by code without `_T`), `design-font` (an own font), `design-page-header`.
- **migrate** (`--level design`, one run): adds `common/theme.css`, `sw/ui.css`, `sw/core.js`, `sw/ui.js` before the page's own css / scripts (the page css still wins), the viewport tag and `class="sw-root"`, and replaces a CDN copy of Chart.js / its data labels plugin / hammer.js with the framework's (`chart/external/`, the app must include SysWeaver.MicroService.ChartJs); the page keeps working and follows the five themes as far as its own css lets it, the port steps are in the worksheet. The summary counts "framework-free pages".
- **manual**, in this order:

| The page does itself | Replace with |
|---|---|
| Header, logo, title, toolbar, own layout css | `Sw.PageLayout({ Type, Title, Actions })` ([B10](#b10-page-anatomy-page-header-and-container)); `Compact: true` for an app-like fill page; status in the header (`Status: { Text, Kind, Dot }`); destructive bulk actions `Kind: "danger", Class: "sw-ph-in-menu"` |
| `fetch` / `XMLHttpRequest` wrapper, own error text | `Sw.Net.Send(url, arg)` and `Sw.Fail(e)` ([A3](#a3-requests), [A13](#a13-access-checks-and-errors)) |
| Own toasts | `Sw.Toast(text, { Kind })` / `Sw.Fail` / `Sw.Info` ([A4](#a4-toasts-clipboard-full-screen-formatting)) |
| `<dialog>` + `showModal`, `confirm()`, `alert()`, `prompt()` | `Sw.Dialog.Open` / `Show`, `Sw.Dialog.Confirm` (`Danger` for destructive), `Sw.Cells.ConfirmDanger`, `Sw.Dialog.Prompt` ([A5](#a5-dialogs-menus-and-toasts)) |
| Own light / dark palette, `prefers-color-scheme`, theme toggle | delete; the role tokens ([B3](#b3-legacy-theme-variables-in-page-css), [B4](#b4-colours-tokens-not-literals)) follow the five themes and the user's choice |
| Inline svg icons | `Sw.Icon(name)` / `Sw.Button({ Icon })` ([A6](#a6-icons-and-buttons)) |
| Text inputs, selects, tabs / folder switches, empty and error states | `Sw.TextBox` (`Label`, `Icon`, `Clear`), `Sw.Select`, `Sw.Segmented`, `Sw.StateView` ([B12](#b12-components-and-form-fields)) |
| List + reader, master / detail (mail, chat, records), own `ul[role=listbox]` key handling, pane swaps on phones | `Sw.SelectList({ Items, OnSelect, OnOpen })` in `Sw.ListDetail({ List, Detail })` (one pane at a time below its `Breakpoint`, Back bar, Escape) (ui/README.md "List / detail"); shortcuts with `Sw.Keys.Add` |
| Hand-rolled validation, errors in toasts, own wizard step lists | `Sw.Form({ Fields: [{ Name, Control, Required, Validate }], Submit, OnSubmit })`, `Sw.Steps({ Steps, OnFinish })` ([B14](#b14-editors-and-forms)) |
| Untranslated texts | `_T("text", "context")` / `_TF` ([A9](#a9-translation-and-texts)) |
| `localStorage` settings | `Sw.Storage.Local.Get` / `Set` (never throws) |

- **An app Home that frames a library page** (`<iframe src="../fakeEmail/index.html">`): prefer a page that forwards to it (`<meta http-equiv="refresh" content="0; url=../fakeEmail/index.html">`, what FakeEmailService does) or a menu item that opens the page itself: one frame keeps the page title, the breadcrumb and the deep links right (nested frames work: dialogs dim every frame and get the keyboard). check: a `port step` "frames a page of this site".
- **Exceptions** (a render surface for a player, a page made for a foreign host): `<!-- SwWeb:no-framework (why) -->` in the page.
- **A page that renders its own UI on purpose** (a WebGL editor, a console, a kiosk screen with its own look and theming) but needs the SysWeaver messaging (server events, the session keep-alive, InterOp, server time, the automatic layout classes, the legacy `SessionManager` / `SysWeaverInit` names): replace `common/common.js` by the small bundle `sw/session.js` (no theme, no components, no framework css; the same requests and reactions as `common.js`), plus `sw/theme.js` after it when the page wants the SysWeaver theme, and `sw/net.js` + `auth/Auth.js` when it signs in (core/README.md "Messaging only", `_docs/WebMessaging.md`). check accepts such a page (no `no-framework` report) and reports `missing-include` when it calls the theme (`getTheme()` ... `appendTheme()`, `Sw.Theme`) without `sw/theme.js` or `Sw.Net.Send` / `auth/Auth.js` without `sw/net.js`. Keep `common/common.js` while the page uses anything else of it (toasts, `AddLoading`, `PopUp` ...).
- **No session where the original had none**: the framework starts a session (`serverTime`, the `GetMessages` long poll) on every page that loads it. An old page that had no `common/common.js` sent none of these, so give the ported page `<html data-sw-no-session>` (core/README.md "Init"; the Fake email inbox does). A page that loaded `common/common.js` had a session: leave it.
- Worked example: the Fake email inbox (`SysWeaver.Email.Fake/srcWeb/fakeEmail`) went from 1 770 lines (own fetch, toasts, dialog, palette, theme toggle, icons, `confirm()`) to about 500 lines on `Sw.PageLayout`, `Sw.Split`, `Sw.Segmented`, `Sw.Select`, `Sw.TextBox`, `Sw.Card`, `Sw.Badge`, `Sw.StateView`, `Sw.Dialog`, `Sw.Cells.ConfirmDanger`, `Sw.Net`, `Sw.Storage` and `Sw.Toast`, and looks right in the five themes and on phones without theme code. The shipped inbox is the reference: `Sw.SelectList` + `Sw.ListDetail` for the list and reader, `Sw.Form` in the composer.

### A1. Includes and page setup

`common/common.js` = `sw/core.js` + `sw/ui.js`; `app/application.js` = `sw/shell.js` (only the shell page; pop-ups and menus are in `sw/ui.js`); `app/application.css` = `sw/ui.css` (+ `sw/shell.css`). `common/theme.css` stays (the templated theme).

```html
<!-- before -->
<script defer src="../common/common.js"></script>
<script defer src="../app/application.js"></script>
<link rel="stylesheet" href="../common/theme.css">
<link rel="stylesheet" href="../app/application.css">
<body class="ThemeBaseBg ThemeMainFg" onload="pageMain()">

<!-- after -->
<meta name="viewport" content="width=device-width, initial-scale=1">
<script defer src="../sw/core.js"></script>
<script defer src="../sw/ui.js"></script>
<script defer src="page.js"></script>
<link rel="stylesheet" href="../common/theme.css">
<link rel="stylesheet" href="../sw/ui.css">
<link rel="stylesheet" href="page.css">
<body class="sw-root">
```

- **migrate** (`api`, also `--level includes`): the includes, keeping prefixes, attributes, `?v=` and templated prefixes (`${@Base Url}`). A page whose scripts still use legacy names also gets `sw/compat.js` / `sw/compat.css` and one warning listing the names. `--level design`: adds the viewport tag, removes zoom blocking parts (`user-scalable=no`, `maximum-scale=1`; WCAG 1.4.4) and adds `class="sw-root"` to the body (not the shell, `index.html`, `debug.html`). A page without `common/theme.css` gets it before `sw/ui.css` (`api`): the Sw tokens follow its theme variables, without it the page stays light in Dark, Pro, Application and Hacker while the shell around it switches.
- **check**: `theme-include` (a page with `sw/ui.css` or `sw/shell.css` and no `common/theme.css` before it), `legacy-include` (a new page that also loads a legacy bundle), `design-legacy-page` (an old format page), `design-viewport`, `design-sw-root`, `design-legacy-class` (the old `Theme*Bg` / `Theme*Fg` body classes).
- **manual**: rewrite the listed names (A2-A11), then remove the compat includes; remove the `Theme*Bg` / `Fg` classes (`sw/ui.css` gives the body `--sw-canvas` / `--sw-text`); add `<meta charset="utf-8">` and `<html lang="en">`.
- **Why `sw-root`:** it gives the page the Sw font, colour and `box-sizing: border-box` that the components are built for. Review own fixed widths / paddings (they now include border and padding). Pages that load `auth/AuthUi.js` get it from the library.

### A2. Page entry and loader

```js
// before
function pageMain() { const done = AddLoading(document.body); ...; done(); }   // <body onload="pageMain()">
// after (page.js, deferred after sw/ui.js)
Sw.Page.Main(async () => { ... }, { Loading: true });
```

- **migrate**: `AddLoading(page, text, opaque, icon)` -> `Sw.Page.Loading({ Element, Text, Opaque, Icon })` (same remover), `PageLoaded()` -> `Sw.Page.SignalLoaded()`, `Open(url, target)` -> `Sw.Page.Navigate(url, target)`, `Open(url)` -> `Sw.Page.Navigate(url, "_blank")` (the old default; drop `"_blank"` where the page should go there).
- **check**: `legacy-global`; `design-navigate`: `Sw.Page.Navigate(url)` without a target for another site or a file (an http(s) url, `.xlsx`, `.csv`, `.json`, `.pdf` ...); `script-order` (error): an inline `<script>Sw.Page.Main(...)</script>` runs while the page is parsed, before the deferred bundles ("Sw is not defined", an empty page). Call it from the page's deferred script, `<body onload>` or `DOMContentLoaded`.
- `applyTheme()`, `getTheme()`, `setTheme()`, `resetTheme()`, `toggleTheme()`, `appendTheme()` are permanent core globals: keep them.
- **`Sw.Page.Navigate(url)` without a target goes there in the same window** (the shell's content frame inside the shell). Pass `"_blank"` for a new tab. The old `Open(url)` (compat) keeps opening a new tab, so a migrated `Open(url)` that meant a new tab becomes `Sw.Page.Navigate(url, "_blank")`; a direct `Sw.Page.Navigate(url)` that relied on the old new tab default needs `"_blank"` (check reports the obvious ones, `design-navigate`; for the others search for `Navigate(` with one argument). Urls of other sites still never replace this window.

### A3. Requests

```js
const r = await sendRequest("../Api/GetX", arg, true, null, true);                         // before
const r = await Sw.Net.Send("../Api/GetX", arg, { Reload: true, ErrHandler: null, Blob: true });   // after
```

- **migrate**: `sendRequest`, `getRequest`, `postData`, `includeJs` / `includeCss`, `Cache.*` ... (options objects built from the passed arguments). `downloadFile(name, url)` swapped its arguments: reported.
- **check**: `legacy-global`; `external-resource`, `external-request` (pages must work offline).

### A4. Toasts, clipboard, full screen, formatting

`Fail` / `Info` / `Message` -> `Sw.Fail` / `Sw.Info` / `Sw.Message`; `ValueFormat.*` -> `Sw.Format.*` (pure) and `Sw.Cells.*` (DOM); `FullScreen` -> `Sw.FullScreen`; `isPureClick` / `badClick` / `keyboardClick` -> `Sw.IsPureEvent` / `Sw.BadClick` / `Sw.KeyClick`; `delay`, `hashString`, `getUrlParams` ... -> `Sw.Util.*` / `Sw.Url.*`.

- **migrate**: every pure rename of the map (`SwWeb mapping` prints it), also `removeKeyboardClick(el)` -> `Sw.RemoveKeyClick(el)` and `ValueFormat.copyOnClick(el, value, keyboard, addTitle)` -> `Sw.Clipboard.CopyOnClick(el, value, { Keyboard, AddTitle })`; signature changes are reported with the new call. The helpers set `sw-keyclick` / `sw-copyable` instead of `KeyClick` / `MouseClick`.
- **check**: `legacy-global` / `legacy-compat`; `design-legacy-class` for page css on the old classes.

### A5. Dialogs, menus and toasts

```js
// before
PopUpMenu(button, (close, back) => { const m = new WebMenu(); m.Items.push(new WebMenuItem(...)); return m; });
if (await Confirm("Delete", "Delete this version?\nAre you sure?", "Yes, Delete!", "No, Keep it!")) ...
// after
Sw.Menu.Show(button, [{ Text: _T("Rename"), Icon: "IconEdit", OnClick: rename }, "-", { Text: _T("Delete"), Danger: true, OnClick: del }]);
if (await Sw.Dialog.Confirm(_T("Delete the version?"), _T("This cannot be undone."), _T("Delete"), { Danger: true })) ...
if (await Sw.Cells.ConfirmDanger({ Action: "Delete", Target: name, Text, Warning })) ...   // title "Delete 1.2.0?", item row, Cancel focused
```

**Dialog rules** (ui/README.md "Dialog rules"; the framework dialogs follow them):
- A confirmation title asks a question that names the action and the item ("Kill devenv (33936)?"; `Sw.Dialog.DangerTitle(action, item)`, or `Target` in `Confirm` / `ConfirmDanger`). The text does not end with "Are you sure?".
- Buttons are verbs ("Delete", "Remove key", "Create key"), "Cancel" for the way out: no "Yes, ...!" / "No, keep it!", no "!" (the framework drops a trailing one). Buttons go in `Buttons`, not a `Sw.ButtonRow` in the body.
- A destructive action is a danger dialog: `{ Danger: true }` or `Sw.Cells.ConfirmDanger` (danger button, Cancel focused however the dialog opens, Enter never confirms).
- A dialog in a page inside the shell dims and blocks the whole app; Escape and a tap on the shell scrim act on the page's dialog (automatic, also for `Sw.Dialog.Show`).
- **Every dialog has a title.** `Sw.Dialog.Open` / `Show` log a console warning for a dialog without `Title` (a screenshot and a screen reader name it by its title); a details view names what it shows ("Last exception · ExchangeRate · 09:04:56", as the table text popups do). A dialog that really has no visible title passes `AriaLabel`. The old `PopUp` (compat) is named by the page title. **manual** (the warning in the console finds them).

- **migrate**: `Confirm(title, text, okText, cancelText, okIcon, cancelIcon, okTitle, cancelTitle)` -> `Sw.Dialog.Confirm(title, text, okText, { CancelText, OkIcon, ... })`. A **destructive** call (title or OK text Delete, Remove, Kill, Terminate ...) gets `Danger: true` (a call without an OK text gets the title's verb as its button); literal labels "Yes, Delete!" / "No, Keep it!" become "Delete" / "Cancel" (also inside `_T`). One warning per call lists what needs a person: a title that is no question, "Are you sure?" in the text, labels that are not literals. A call with `okStyle` / `cancelStyle` is reported. `PopUp`, `PopUpMenu`, `WebMenu`, `WebMenuItem`, `PopUpSelection`, `PopUpEdit` are reported (the shape changes).
- **check**: `legacy-global`; `design-confirm` (a `Sw.Dialog.Confirm` with a destructive title / OK text without `Danger`, "Yes ..." / "No ..." labels, a destructive title that is no question and has no `Target`, "Are you sure?" in the text: the same wording migrate reports); `ux-pop-9` (native `confirm()` / `alert()` / `prompt()`, "OK" / "Yes" / "No" buttons), `ux-pop-1` (an own `<dialog>`).
- **manual**: `Sw.Menu` items / `Sw.Dialog.Show({ Title, Content, Buttons })`; question titles; texts through `_T`.
- A **server menu** (`Api/application/GetMenu`, the old `WebMenu.From(...)`) converts with `Sw.Shell.ToMenuItems(menu.Items)` from `sw/shell.js`: add `<script defer src="../sw/shell.js">` after `sw/ui.js` (migrate adds it once the code uses `Sw.Shell`; check: `missing-include`).
- **Toasts** (automatic): lifted above sticky / fixed bottom bars and above primary / danger buttons in their column; mark a special bottom area with `data-sw-toastavoid`. Field errors belong next to the field (`SetError`), a toast is for the outcome of an action.

### A6. Icons and buttons

```js
const b = new ColorIcon("IconReload", "IconColorThemeAcc1", 32, 32, "Reload", reload);   // before: b.Element
const b = new Sw.Button({ Icon: "IconReload", Title: _T("Reload the list"), Kind: "ghost", OnClick: reload });   // after: b.E
```

- **migrate**: reported (`ColorIcon`, `Button`, `ButtonStyle`).
- **manual**: `Sw.Button` for anything clickable (an icon only button needs a `Title`: tooltip and accessible name); a toggling icon is one button with `b.SetTitle(...)` / `b.SetIcon(...)`; an `OnClick` that returns a promise makes the button busy. `Sw.IconClass(cls, { Size, Color })` for a picture. Your own mask icon classes work as `Icon` names. Icons take the text colour: drop the old colour classes; no coloured icons in chrome.
- An unknown `Sw.Icon` name shows a dashed square and one console warning: fix the name (`Sw.Icons.List()`).

### A7. Libraries (tables, editor, chat, charts, upload, media, auth)

Old global names stay as aliases (`Edit` === `Sw.Edit`, `TableDataState` === `Sw.Table.State` ...).

```js
const t = await Table.addTable("../Api/MessageTable", host, 25, param);                     // before
const t = await Sw.Table.Create({ Url: "../Api/MessageTable", Element: host, Param: param });   // after
```

- **The page includes the libraries it uses**; the bundles contain none of them: `Sw.Table` needs `../explore/table.js` (after `sw/ui.js`, before the page script) + `../explore/table.css`; `Sw.Shell` `../sw/shell.js`; `Sw.Edit` `../edit/Edit.js` (it loads its css); `Sw.Upload` `../common/fileUploader.js`; `Sw.AuthUi` `../auth/AuthUi.js`; `Sw.Chat` `../chat/chat.js`. Without it the page fails at run time ("Cannot read properties of undefined").
- **migrate** (`api`): pure aliases renamed; `Table.addTable(...)` -> `Sw.Table.Create({ Url, Element, Param, OnFirstLoad, Name, State, OnFirstData, Filter })` (the ignored `rowsPerPage` is dropped when simple); upload wrappers `fileUploaderElement` / `Click` / `Drop` / `Drag*` -> `Sw.Upload.ForElement` / `Sw.Upload.Handlers.*`, `selectFiles(multiple)` -> `Sw.Upload.Pick({ Multiple })`, `fileUploader(repo, files, apiBase)` -> `Sw.Upload.Files(repo, files, { ApiBase })`; other wrappers are reported (they keep working without compat). Adds the missing library includes with the prefix of the bundle tags.
- **check**: `legacy-global` (names of the old common.js / application.js); `missing-include` (error; the hint shows the include with the page's prefix). An optional use is tested first (`if (Sw.Table)`); a shared helper script whose other pages load the library is not reported.
- **App wide extensions** (edit plug-ins, cell formats, anything in `app/app.js` or a shared script that extends a library only some pages load): register them deferred, `Sw.Extend.EditSpec(spec, factory)`, `Sw.Extend.CellFormat(name, renderer)` or `Sw.Ready("Edit", E => ...)`. They run at once where the library is loaded and as soon as it loads elsewhere (a pop-up editor that loads Edit.js later gets the plug-in too). An existing unguarded top level `Sw.Edit.RegisterSpec(...)` / `Sw.Cells.FormatFormatters.set(...)` in `app/app.js` is queued the same way and no longer throws on pages without the library (nothing to do; inside functions that run later use `Sw.Ready`). `if (Sw.Edit) Sw.Edit.Ready.then(...)` guards can be replaced by `Sw.Ready("Edit", ...)`. **check**: not flagged.

### A8. Global state and window members

`window.IsTop`, `window.Mobile`, `window.Narrow`, `IsDarkMode`, `UseTheme` ... -> `Sw.Page.IsTop`, `Sw.Layout.Mobile` / `Narrow`, `Sw.Theme.IsDark`, `Sw.Theme.Forced`.

- **migrate**: reported (`window.X` and `typeof X` are never rewritten). **check**: `legacy-global` / `legacy-compat` after the migration (without compat they are `undefined`).
- `Sw.el(spec, props, children)`: arrays are flattened and components (`{E}`) are added by their `E`; other objects are skipped with a console error.

### A9. Translation and texts

User visible text only through `_T` / `_TF` / `_TH` / `_TFH` with literal arguments; never rename, wrap or alias them; no `\n`, backticks or mixed quotes inside translated literals.

```js
_T("Failed to send.\n{0}", e)                                  // before: not translated (the minifier makes it a backtick literal)
_T("Failed to send.{1}{0}", e, String.fromCharCode(10))        // after (migrate does it)
```

**Which one:** `_T(format, args..., "context")` / `_TH` format `{0}` .. placeholders with the arguments; a trailing quoted literal is always the translation context (removed by the server), never an argument. `_TF(text, "context")` / `_TFH` never format: use them for a text without values, or a format that code fills later (`Sw.I18n.Format(_TF("Remove {0}", "..."), name)`).

```js
_TF("{0} emails, sent from {1}", "Mail list summary", n, from)   // before: shows "{0} emails, sent from {1}"
_T("{0} emails, sent from {1}", n, from, "Mail list summary")    // after
_T("{0} is {1}", name, "on")                                     // wrong: "on" is the context, {1} stays
```

- **Where texts need `_T`:** the server translates the page's html text and its `title` / `placeholder` / `alt` / `aria-label` attributes, and the literals of `_T` calls; nothing else. Texts made by code need `_T`: Sw options (`Title`, `Text`, `Description`, `Label`, `Placeholder`, `Tooltip`, `Content` ...), `Sw.Toast` / `Sw.Info` / `Sw.Fail` / `Sw.Dialog.*` texts, `textContent` / `innerText` / `title` / `placeholder` / `aria-label`, `createTextNode`, and the words in html strings (`innerHTML`, `insertAdjacentHTML`: build the html around `_T` texts, or use `Sw.el` / `textContent`).

```js
Sw.Toast("Saved")                          // before: never translated
Sw.Toast(_T("Saved", "toast after save"))   // after (migrate --level design writes _T("Saved"); add the context)
```

- **migrate**: newline escapes become placeholders; `_TF` / `_TFH` without `{` become `_T` / `_TH`; `--level design` wraps a plain literal text in one of the places above in `_T(...)` (only in scripts of pages on the framework; not a literal with `{`, escapes, both quote kinds, html, a concatenation or a template).
- **check**: `tr-not-literal`, `tr-member-call`, `tr-identifier-suffix`, `tr-in-text`, `tr-newline`, `tr-mixed-quotes`, `tr-alias` (`const t = _T`, `list.map(_T)`: never translated) (errors); `tr-untranslated` (warning: a literal text in one of the places above, or a page whose scripts make texts and never call `_T`; `// SwWeb:no-tr (why)` for a product name or a code); `tr-format` (error: `_TF` / `_TFH` with format arguments, a `_T` placeholder without an argument; warning: `_T` arguments without a placeholder, a `_T` placeholder without any argument).
- Framework source texts with typos were fixed ("cannot", "Markdown", "Text too long: at most {0} characters ..."): they are new translation keys, re-translate them. Exporter ids ("Mark down", "CSV (semi colon)") are unchanged, only their display names.

### A10. Contract and build rules

One dot per file name, no `-` or `.` in folder names, classic scripts (no modules), nothing from the internet (no CDN, no web fonts: embed fonts as `data:` urls), JSDoc file headers, no `${` in templated files, no internal change tracking ids in shipped comments.

- **migrate**: adds a JSDoc file header to `.js` files without one (describe the file afterwards).
- **check**: `file-name-dots`, `folder-name-*`, `mjs-file`, `es-module`, `external-*`, `root-relative-url`, `font-file`, `jsdoc-header`, `template-*`, `debug-markers`, `minifier-reorder` (a destructuring default that reads an earlier declarator of the same declaration: the release minifier reorders it); `internal-id` (strict).

### A11. Old helpers without a Sw counterpart

Only `sw/compat.js` keeps these; the platform does it in a line or two. Write the line (or a helper named after your page) and drop compat:

| Old | Replacement |
|---|---|
| `await setMediaSrcAndWait(video, url, ok => failed = !ok)` | `await new Promise(r => { video.addEventListener("canplaythrough", () => r(true), { once: true }); video.addEventListener("error", () => r(false), { once: true }); video.src = url; })` |
| `CaptureVideoFrame(video, mime, quality)` | `canvas.getContext("2d").drawImage(video, 0, 0)`, then `canvas.toBlob(cb, mime, quality / 100)` |
| `EnterNoSleep()` / `ExitNoSleep()` / `IsSleepPrevented()` | `lock = await navigator.wakeLock.request("screen")` / `lock.release()` / `!lock.released` (https or localhost only; a plain http kiosk keeps its own looping muted video) |
| `GetAbsoluteRect(el)` | `el.getBoundingClientRect()` + `window.scrollX` / `scrollY` |
| `IsAtMaxScrollV(el)`, `StickToBottom(el)` | `el.scrollTop >= el.scrollHeight - el.clientHeight - 1`; remember it on `scroll`, set `el.scrollTop = el.scrollHeight` after adding content |
| `SetTemporaryInnerText(el, text, ms)` | `el.textContent = text; setTimeout(() => el.textContent = old, ms)` (or a `Sw.Info` toast) |
| `getIntegerAttribute` / `getNumberAttribute(el, name, def)` | `parseInt` / `parseFloat(el.getAttribute(name))`, the default when NaN or negative |
| `insertAtCursor(field, text)` | `field.setRangeText(text, field.selectionStart, field.selectionEnd, "end")` |
| `isHidden(el)` | `getComputedStyle(el).display === "none"` (or `!el.checkVisibility()`) |
| `traverseChildren` / `traverseForward` / `traverseBackward` | `document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT)`; moving the focus: `Sw.Focus.Next` / `Sw.Focus.Prev` |
| `doHandleSrc`, `removeFadeChannel`, `InternalSrcElementOn*` | remove: `Sw.FadeSrc(el, { ... })` handles the smooth loading |

- **migrate**: reported with the replacement (map section `compatonly`). **check**: `legacy-compat` / `legacy-global`.

### A12. Dates and times

- **One time rule** (core/README.md "Times"): every time in the UI is the viewer's local time; a time on another clock is labelled with its zone ("server time 02:12 (UTC+2)"); "server time" only labels the server's own clock. `TimeStamp` tool tips name the zone.
- **Automatic:** `Sw.Format.TimeStamp`, `Value("System.DateTime")`, `Uptime`, the DateTime cells and the old `getTimeStampTitle` / `convertUTCDateToLocalDate` show the right local day in every zone. An ISO date time **without** a zone (`"2026-10-19T22:24:00"`) is read as UTC (the server convention); a `"yyyy-MM-dd"` value is shown as that day.
- **Act:** a server that sends local wall clock times without a zone sends them with their offset (`DateTimeKind.Local` / `DateTimeOffset`), or the column uses the format `local`. A page that shows server-local text (a log, `GetServerInfo.Time`) converts it with `Sw.Format.ServerToLocalText(text)` / `ServerToDate(text)` or labels it with `Sw.Format.ServerZoneText()`; a page that knows the server zone calls `Sw.Format.SetServerZone(Sw.Format.ServerZoneFrom(info.Time, info.Utc))`. Own date parsing: `Sw.Format.ParseDate(v)`; a time column tool tip: `Sw.Format.ZoneHint()`.
- The `serverTime` endpoint answers `"<ms>;<offset minutes>"`; clients of every version read the ms the same way.
- **check**: none.

### A13. Access checks and errors

- **No token names in page code:** show a button or a part only to users who may use it by asking the server: `if (await Sw.Access.CanCall(Sw.Url.Root + "<api>") !== false) ...` (null = unknown: show it, the server still refuses). Comparing `user.Tokens` with "admin" / "ops" leaves out Debug and custom tokens. **check**: not flagged; search your code for `Tokens` next to role names.
- **Errors** (automatic): `Sw.Fail` turns raw HTTP texts ("404 - Not Found") into messages for people with the raw text under Details (`Sw.Net.ErrorText`); `err.message` is unchanged.

---

## Part B: Design v2

A flat, structured admin console: full height sidebar, quiet top bar with the breadcrumb, left aligned page headers, cards drawn with hairlines and tone instead of shadows, a data grid with a fixed frame, the accent only for meaning. Everything derives from four base colours (`--ThemeBackground`, `--ThemeMain`, `--ThemeAcc1`, `--ThemeAcc2`), so the five themes, the densities, the app colours and your brand follow automatically **if your css uses the tokens**.

### B1. Old token names (aliases)

The old names still resolve (each is `var(--new)`), but code uses the role names:

| Old | New | | Old | New |
|---|---|---|---|---|
| `--sw-bg-1` | `--sw-surface` | | `--sw-faint` | `--sw-text-3` |
| `--sw-bg-2`, `--sw-bg-inset` | `--sw-surface-sunken` | | `--sw-accent1-fg` | `--sw-text-accent` |
| `--sw-bg-3` | `--sw-surface-strong` | | `--sw-shadow` | `--sw-elev-1` |
| `--sw-line` | `--sw-border` | | `--sw-backdrop` | `--sw-scrim` |
| `--sw-line-strong` | `--sw-border-strong` | | `--sw-touch` | `--sw-control-h` |
| `--sw-muted` | `--sw-text-2` | | `--sw-size` / `--sw-speed` | `--sw-text-md` / `--sw-dur-2` |

- **migrate** (`api`): every `var()` use in css, `<style>`, `style` attributes and JavaScript strings (comments skipped, fallbacks kept). `--sw-gap` is not an alias (Pro has its own).
- **check**: `design-token-alias`.

### B2. Overrides of old names no longer restyle the components

```css
.big-form { --sw-touch: 52px; }        /* before: nothing reads --sw-touch any more */
.big-form { --sw-control-h: 52px; }    /* after (better: let the density do it, B9) */
```

- **migrate**: no. A token that a file of the run overrides is not renamed anywhere in the run (its uses read the override), so the page renders as before until you move the override.
- **check**: `design-legacy-token` ("overrides an old alias").

### B3. Legacy theme variables in page css

The `--Theme*` variables are the theme's inputs; pages read the role tokens (they follow dark mode, the Application theme and the densities).

| Old use | Use instead | migrate (`design`) |
|---|---|---|
| `var(--ThemeAcc1)` | `color`, `fill`: `var(--sw-text-accent)` (only for meaning: links, the current item); elsewhere `var(--sw-accent1)` (the one primary action; `var(--sw-selected)` for selected items) | yes, by property |
| `var(--ThemeAcc2)` | `var(--sw-accent2)` | yes |
| `var(--ThemeRad)` / `--ThemeRadInp` / `--ThemeRadInp2` | `var(--sw-radius-md)` (cards, menus) / `var(--sw-radius)` (controls) / `var(--sw-radius-sm)` | yes |
| `var(--ThemeFont)` / `--ThemeFontHead` / `--ThemeFontMono` | `var(--sw-sans)` / `var(--sw-head)` / `var(--sw-mono)` | yes |
| `var(--ThemeError)` / `--ThemeOk` / `--ThemeWarning` / `--ThemeFocus` | `var(--sw-danger)` / `--sw-ok` / `--sw-warning` / `--sw-focus` | yes |
| `var(--ThemeFadeDuration)` | `var(--sw-dur-2)` (motion `--sw-dur-1/2/3` + `--sw-ease`) | yes |
| `var(--SysWeaverEditDesktopRad*)`, `--SysWeaverEditFadeDuration` (edit/Edit.css) | `var(--sw-radius-md)`, `--sw-radius`, `--sw-radius-sm`, `--sw-dur-2` | yes |
| `color: var(--ThemeMain)` | `var(--sw-text)` (secondary `--sw-text-2`, tertiary `--sw-text-3`) | no: pick the role |
| `border-color: var(--ThemeMain)` / `...50` | `var(--sw-border)` / `var(--sw-border-strong)` | no |
| `background: var(--ThemeBackground)` | `var(--sw-canvas)` (page), `var(--sw-surface)` (cards, tables, forms), `var(--sw-surface-sunken)` (wells, heads) | no |
| `rgba(var(--ThemeMainRGB), 0.1)`, `var(--ThemeMain25)` | `var(--sw-hover)`, `var(--sw-pressed)`, `var(--sw-selected)`, or `color-mix(in srgb, var(--sw-fg) 10%, transparent)` | no |
| `var(--ThemeIconSize)`, `--ThemeFontSize` | `var(--sw-icon)`, the type scale `--sw-text-xs` .. `--sw-text-3xl` | no |

- **migrate** (`design`): the "yes" rows, in css, `<style>`, `style` attributes and JavaScript strings. A variable that a file of the run sets below the root (`.card { --ThemeFont: serif }`) is not renamed in the run; a theme on the root (`:root { --ThemeAcc2: ... }` in `app/app.css`) does not block it (the role token reads it). A legacy variable as the fallback of a role token (`var(--sw-text-2, var(--ThemeMain))`) is left alone.
- **check**: `design-legacy-token` (says which ones migrate renames); setting `--Theme*` in a theme (`app/app.css`) is fine.

### B4. Colours: tokens, not literals

```css
.badge-new { background: #393; color: #fff; }                              /* before: wrong in dark, pro, hacker, app colours */
.badge-new { background: var(--sw-ok-soft); color: var(--sw-text); }       /* after (or Sw.Badge(text, "ok")) */
:root { --acme-badge: #4fc3f7; } .x { color: var(--acme-badge); }          /* an own colour: define a token (per theme if needed) */
```

Roles: text `--sw-text` / `-2` / `-3`; surfaces `--sw-canvas`, `--sw-nav`, `--sw-surface`, `--sw-surface-sunken`, `--sw-surface-raised`; lines `--sw-border`, `--sw-border-strong`, `--sw-border-input`; states `--sw-hover`, `--sw-pressed`, `--sw-selected(-strong)`; status `--sw-ok`, `--sw-warning`, `--sw-danger` (+ `-soft`), `--sw-info`; accent `--sw-accent1` / `--sw-text-accent` / `--sw-on-accent`; data `--sw-chart-1..7`. Status is icon + text, never colour alone.

- **check**: `design-color` (hex, colour functions with literal values, named colours in colour properties; definitions, fallbacks, `url()`, forced colours and masks are fine). Mail templates (`*Mail.html`) and documents without the bundles are not checked.
- Colours that are data stay fixed on purpose (text over a video, a terminal's ANSI palette, a swatch): define them once as app tokens on `:root` with a `SwWeb:allow-design (why)` comment; an always dark surface also sets `color-scheme: dark`.

### B5. Elevation: flat surfaces

| Level | Token | For |
|---|---|---|
| 0 | `none` + `1px solid var(--sw-border)` | header, sidebar, cards, tables, inputs, buttons, tiles |
| 1 | `var(--sw-elev-1)` | menus, popovers, select lists, tooltips |
| 2 | `var(--sw-elev-2)` | dialogs, drawers, toasts |
| scrim | `var(--sw-scrim)` | behind a dialog / drawer |

```css
.card { box-shadow: 0 4px 12px rgba(0, 0, 0, 0.35); border-radius: 12px; }                         /* before */
.card { box-shadow: none; border: 1px solid var(--sw-border); border-radius: var(--sw-radius-md); } /* after (or Sw.Card) */
.panel { box-shadow: 0 0 32px var(--sw-shadow); }   /* invalid: --sw-shadow is a complete shadow, the browser ignores it */
```

No glows (`text-shadow`, `drop-shadow()`, coloured shadows; the Hacker theme's glow is the theme's own). Focus: `outline: 2px solid var(--sw-focus)` or `box-shadow: var(--sw-focus-ring)`.

- **migrate** (`design`): the invalid `<lengths> var(--sw-shadow)` becomes `none` (a floating layer then gets `var(--sw-elev-1)` by hand).
- **check**: `design-shadow` (rings, inset lines, focus rings, elevation tokens are fine), `design-inline-style` for shadows set from JavaScript.

### B6. Gradients

No gradients on chrome, cards, bars or buttons. Repeating patterns, masks and real colour swatches (marked) are fine. **check**: `design-gradient`, `design-inline-style`.

### B7. Radii, spacing, type, motion

Radii `--sw-radius-sm` 4, `--sw-radius` 6 (controls), `--sw-radius-md` 8 (cards, menus, tables), `--sw-radius-lg` 12 (dialogs), `--sw-radius-pill`; Pro and Hacker have their own scale, so never write px radii. Spacing `--sw-space-1` .. `--sw-space-12` (4 px grid), page padding `--sw-page-px`. Type `--sw-text-xs` 12 .. `--sw-text-3xl` 32 (rem: the root size scales on large screens and in Pro), headings `--sw-head`, numbers `font-variant-numeric: tabular-nums`. Motion `--sw-dur-1/2/3` + `--sw-ease`, nothing animates with `prefers-reduced-motion`.

- **migrate**: `--ThemeRad*` / `--ThemeFont*` / `--ThemeFadeDuration` (B3). **check**: `design-radius`, `design-legacy-token`; px font sizes and paddings are not checked (manual). A radius, shadow or gradient in an own custom property (`--my-shadow: 0 8px 24px ...; box-shadow: var(--my-shadow)`) is judged by its value: an own name doesn't make it a token.
- **Fonts:** the themes set the fonts (`--sw-sans`, headings `--sw-head`, code `--sw-font-mono`; Hacker a terminal font); weights `--sw-weight-medium` (labels, values; 400 in Hacker) and `--sw-head-weight`. **check**: `design-font` (an own `font-family` / `font` family in page css or set from code).
- **Focus:** never remove the focus ring (`:focus-visible { outline: none }`) without drawing another one in the same rule (`box-shadow: var(--sw-focus-ring)`). **check**: `a11y-focus`.
- **Token names:** css drops a `var(--sw-...)` whose token doesn't exist without a word (`--sw-fs-sm` for `--sw-text-sm`, `--sw-accent` for `--sw-accent1`, `--sw-accent-soft` for `--sw-accent1-soft`). **check**: `unknown-token` (warning) names the closest token; a token you define yourself (css or `setProperty`) in the checked folders is known; one that a host page defines gets `/* SwWeb:allow-token (why) */`. The list: ui/README.md "Theming" (the tool's copy: `SwWeb tokens <SysWeaver>`).

### B8. Bare element rules

`button { width: 100% }`, `input { ... }`, `td { padding: 0 }`, `img { width: 100% }`, `body { font-size: 18px }` or `body { overflow: hidden }` in page css also change every Sw component (tables, pagers, dialogs, menus) and clip phones. Scope them: `.my-page button`. Table, `img` and `iframe` rules are only reported in `.css` files and pages that load the bundles (an export document may style its own table).

- **check**: `element-css`, `body-overflow`. A custom element of your page scopes like a class (`tiu-display input`); `sw-*` and `SysWeaver-*` elements don't.
- A full window app (display client, kiosk, terminal) may hide the body overflow: `/* SwWeb:allow-element-css (why) */` plus `<!-- SwWeb:no-page-header (why) -->`.

### B9. Density

`html[data-density="compact | comfortable | touch"]` (`Sw.Theme.Density`, the settings menu, `?density=`; default comfortable, compact for Pro, touch on touch screens) sets `--sw-control-h` (28 / 34 / 44), `--sw-row-h` (32 / 40 / 48), `--sw-nav-row-h`, `--sw-icon`, `--sw-cell-px`, `--sw-text-md`. Size your controls and rows with these tokens, never fixed 44 px heights or `pointer` media queries. **migrate**: `--sw-touch` -> `--sw-control-h` (B1). **check**: B2.

### B10. Page anatomy (page header and container)

Every page: breadcrumb (the shell's top bar) -> **page header** (title, one line description, status, actions) -> content in a **page container**: `dash` (dashboards), `form` (forms), `read` (text), `fill` (tables, logs, chat: full width and height). All containers start at the page padding (left aligned); dash / form / read stop at their measure.

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

- **Title:** in the shell a level 1 header shows the **menu item name** (`Sw.MenuTitle()`), so h1, breadcrumb and menu agree: name your menu items as the pages should be titled. A page about one record (app Home overrides too) passes `MenuTitle: false`; then `Title` / `SetTitle` also set the document title and the breadcrumb (drop `document.title = ...` workarounds).
- **Actions:** one primary action per region; destructive actions `Kind: "danger"` with a confirmation. Actions never wrap: the last ones that don't fit move into a "More actions" kebab (put the important ones first; `Overflow: false` keeps a wrapping row). `Class: "sw-ph-in-menu"` keeps an action (Delete) always in that menu, last, after a separator. Drop own css that wrapped or shrank `.sw-ph-actions` children.
- **Fill pages** use `Compact: true`: on phones the title row hides (the top bar shows it) and the actions stay as one row.
- Table pages (`Sw.Table.Create({ Page: true })`, `explore/table.html`) make their own compact header; a text viewer on a menu page (`edit/text.html`) shows the menu name and the file name.
- Remove centred titles, hero logos and "Welcome x" pills from content pages (the brand lives in the sidebar). A page that wants a centred card (sign in) centres the card, not the container.
- A page of many alike things placed with `position: absolute`: `Sw.Grid({ Content })` of `Sw.Card({ Title, Sub, Tools, Body, Flush: true, Span: 6 })`, `Sw.StateView({ Kind: "empty" })` for none; a fixed size preview keeps its layout with `aspect-ratio` on a wrapper and a scaled iframe.
- **check**: `design-page-header`, `design-page-container`. **manual**: as above, or `<!-- SwWeb:no-page-header (why) -->`.
- **A page that frames other pages of this site** (a dashboard of frames, an overview with a preview, a chart wall; also iframes created or pointed from code): the framed pages notice it themselves (`html.SwNested`, core/layout.js) and drop their page padding (`--sw-page-px: 0`), so the host page's padding is the only one. Give the frames a gap or a card in the host page if they need one. Automatic; **check**: not flagged.

### B11. Dashboard parts instead of page specific widgets

| Old pattern | Design v2 part (ui/page.js) |
|---|---|
| fieldset look, `Sw.Group` with the title in the border | `Sw.Card({ Title, Sub, Tools, Body })` (a `Sw.Group` is a card; `--sw-frame-bg` is obsolete) |
| a row of fact pills (host, OS, version ...) | `Sw.Facts({ Items: [{ Label, Value, Href }] })`; inside a `Sw.Card` it drops its own frame |
| a donut / gauge for one value | `Sw.Kpi({ Label, Icon, Value, Unit, Meter: { Max, Warn, Danger } })` |
| bar series over time | a line chart in a card (`chart/chart.html?type=line&title=false`) |
| ad hoc lists with status pills | `Sw.ListCard({ Title, Items: [{ Text, Status, Meta, Href }], Empty })` |
| spinners, "No data", red error text | `Sw.StateView({ Kind: "loading" \| "empty" \| "error", Title, Text, Actions, Details })` |
| layout tables, ad hoc flex grids | `Sw.Grid({ Content })` / `.sw-grid` + `.sw-span-3/4/6/8/12` |
| an own upload button / drop target, a fill page around a table | `Sw.UploadButton`, `Sw.UploadTarget`, `Sw.ReportUpload` (need `common/fileUploader.js`), `Sw.FillTable(page, url, opts)` (needs `explore/table.js`); the old `SmShared.*` names are aliases |
| an own selectable list / inbox, master / detail panes | `Sw.SelectList({ Items: [{ Key, Text, Sub, Meta, Icon, Status }], Multi, OnSelect, OnOpen, Empty })`, `Sw.ListDetail({ List, Detail, Breakpoint })` (ui/README.md "List / detail") |
| a count or status badge with an own icon | `Sw.Badge(text, kind)` shows a glyph for ok / warning / error / info; a count badge adds `sw-count`, a live dot state `sw-noicon`; drop own badge icons |

- **Meters:** no thresholds = a plain bar. A use meter (cpu, memory, disk) gives `Warn: 0.8, Danger: 0.92` (or its own); more-is-better (on time %) gives `Good: "high", Warn, Danger`; a value above 1 is absolute on the meter scale, 0..1 a fraction of Max (`Absolute: true` forces absolute); `Levels: false` for a plain bar.
- **Charts:** the chart viewer's default "Theme colors" use the slots `--sw-chart-1..7` in legend order; Hacker and Application have their own series hues. Bars are not outlined, the method graph uses horizontal bars, title and legend are left aligned; inside the shell `chart.html` gets a compact header and a card; `title=false` hides the chart's own title in a card; embedded tiles (`m=false` / `transparent=true`) are unchanged. Set slots in `app/app.css` per theme (`:root[data-theme="dark"]`, never `data-sw-scheme`, see [B16](#b16-themes-and-branding-without-forking)).
- Pills / badges (`Sw.Badge`, `Sw.Chip`) are for status and filters only.
- **check**: `design-meter-levels` (a meter without Warn / Danger / Levels), `design-pill-facts`, `design-fieldset`, and `design-shadow` / `-gradient` / `-color` on old widget css.

### B12. Components and form fields

- Buttons: default (bordered), `primary` (accent fill, at most one per region), `ghost` (toolbars, icon buttons), `danger` (outline; solid only in the confirmation). Tabs `Sw.Tabs` (underline), switches `Sw.Toggle`, segmented `.sw-seg`, inputs `Sw.TextBox` / `Sw.NumberBox` / `Sw.Select`, menus `Sw.Menu`, dialogs `Sw.Dialog` (bottom sheet on phones).
- **Form fields:** `Label` is the **visible** label on `TextBox`, `NumberBox` and `Select` (the field `div.sw-field` is `E`, `.Input` / `.Button` the control); `AriaLabel` = the accessible name only (use it where a NumberBox `Label` meant only that); `Hint`; `new Sw.Field({ Label, Hint, Control })` for any other control. A NumberBox without `Step` shows "1" and grows with long values. **check**: not flagged; search `new Sw.NumberBox(` for `Label:`.
- **Required and validation:** `Required: true` (or the message) on `TextBox`, `NumberBox`, `Select`, `Toggle` (must be on: "Turn this on to continue") and `Segmented` (with `Value: null`) marks the label and shows the message under the control, linked by `aria-describedby` with `aria-invalid`; `Validate()` returns the message or null (`Sw.Form` calls it); `NumberBox.Get()` reads the number being typed; a `Set()` checks a shown message again. Replace own "required" checks, error toasts and own `aria-errormessage` wiring with them. **check**: not flagged.
- **Common methods:** every control has `Focus(select)`, `SetLabel(text)`, `Destroy()`, and every value control `Get` / `Set` / `SetDisabled` / `SetError(text)` / `Validate()` (ui/README "Common methods"). Replace own `x.Input.focus()`, `x.E.remove()` and label rewrites with them. **check**: not flagged.
- **One name per concept** (ui/README "Naming rules"): `AriaLabel` for an accessible name only, `Items: [{ Value, Text }]` for choices, `Actions` for header buttons, `Kind: "danger"` for the red tone. The old names (`Options`, option `Label`, `Tools`, `"error"`) keep working; new code uses the canonical ones. **check**: not flagged.
- **Control heights:** two per density, `--sw-control-h` (default 34 px, compact 28, touch 44) and `--sw-control-h-sm` (small buttons, chips, table cell actions, the buttons inside a text field or a tab, the shell menu tools: 28 / 24 / 36; the shell top bar buttons use the default). Own controls use these tokens, not pixel heights. Every `button`, `input`, `select` and `textarea` takes the page font (zero specificity rule in ui/tokens.css): an own button that relied on the browser's 13.33 px Arial now has the page font and size; set `font-size` in its class if it must stay smaller.
- **Weights and dark text:** use `--sw-weight-medium` (500; Hacker 400) and `--sw-head-weight`, not `font-weight: 500 / 600` literals, so the themes can tune them. Dark schemes draw the page text in `--sw-text` (a step below `--ThemeMain`); own css that colours text with `--sw-fg` / `--ThemeMain` stays brighter than the rest: use `--sw-text`. **check**: not flagged.
- States: hover `--sw-hover` (rows only with `@media (hover: hover)`), pressed `--sw-pressed`, selected `--sw-selected`, focus 2 px `--sw-focus`, disabled 45 % opacity. Restyle a component through its tokens on a page scope, never through its inner classes.
- **check**: `element-css`, `design-legacy-class` (styles on old element hooks such as `SysWeaver-Button`, `SysWeaver-PopUpBackground`, `.KeyClick`: only the compat layer creates them).

### B13. Tables

Table pages and embedded tables get the fixed frame automatically (tool bar with search and tools, filter chips, a rows area that takes the free height, a one line footer; skeleton / empty / error states; cards on phones). The C# TableData attributes, the request / response, the `Sw.Table.Create` options and the legacy `Table.addTable` are unchanged. If your app styles tables itself:

| Change | What to do | SwWeb |
|---|---|---|
| `Table.addTable(url, el, rowsPerPage, ...)` | `Sw.Table.Create({ Url, Element, Param, ... })`; the page size is automatic | migrate (`api`) |
| Rows, skeleton and states are in `div.sw-table-area`: `.sw-table > .sw-table-scroll` no longer matches | drop the `>` or write `.sw-table-area > ...` | check `design-legacy-class` |
| Search and tools moved to the tool bar: `.sw-table-tools` is gone (`.sw-table-ttools > .sw-table-tbtn`); below 480 px Download / Refresh / Density / layout are in a "More tools" menu | move the rules to `.sw-table-toolbar` / `.sw-table-ttools` | check `design-legacy-class` |
| Footer slots (`.sw-table-fleft`, `.sw-table-pps`, `.sw-table-nav`, `.sw-table-pageno`) never wrap and only grow; the range reads "1-25, more follow" (a counted total: "1-25 of 183") | no `flex-wrap`, widths or `min-width` on them; tests read the new range text | - |
| Page tables and **fill tables** have the footer at the bottom of the frame; a table whose container has `sw-fill` / `sw-table-fill` is a fill table (`Fill: true / false` overrides); other embedded tables are as high as their rows | give the host a definite height (`.sw-fill` in `.sw-page-fill`); drop css that stretched the table | - |
| Rows are `--sw-row-h` high (density), headers sentence case, at most 2 lines and aligned like their values, tabular numbers, row hairlines only, stripes only with "Striped rows" (`.sw-table-zebra`); wide frames don't stretch sparse grids | restyle with `--sw-row-h`, `--sw-cell-px`, `--sw-table-row`, `--sw-table-row-alt`, `--sw-table-row-hover` (or `--sw-table-rowbg` on a `tr` class), not `background` on cells; `ColumnLines: true` for column rules; remove css that drew cell borders | - |
| Row actions are always visible (quiet ghost buttons in a column pinned at the right edge, UX guide LST-2; the earlier hover-only gutter is gone); the buttons sit in `span.sw-table-actbox`; a successful action shows "<Action>: done (<row>)" and marks the row | `td.sw-cell-actions button` (descendant, not `>`); `RowActions: "always"` is the only mode (nothing to set); drop own "done" toasts | - |
| Cards (phones): title from `CardTitle` (a column name) or an identity column; row actions in a heading kebab | set `CardTitle` where the guess is wrong | - |
| Icon only columns (`TableDataNoTitleAttribute`) are compact and centred; a chart emoji link becomes a line icon | nothing | - |
| Auth requirement columns (`null` = open, `""` = any signed in user, else tokens; C# `[TableDataTags(...)]` on an `Auth` field): open ones showed an empty cell and "any signed in user" a 🔑 | use C# `[TableDataAuth]` (the accent "Open" tag, the "Any logged in user" tag, one tag per token; the same tags as the API inspector and the explore listing); a list of the user's own tokens stays `[TableDataTags]` | - |
| Auto rows per page never scroll: the page size is at most the whole rows that fit the frame (a multiple of 5; a page table counts the compact row height), and rows that grow later (a live refresh with longer, wrapped values) lower it to the rows that fit at their real heights (Fit, `FitCap`) until the window, the columns or the rows asked for change: paging never needs scrolling | nothing | - |
| File type images `icons/ext/<ext>.svg` in a cell are the built-in multicolour icons again (they were monochrome glyphs) | pass the extension without the dot (`TableDataFileExtensionImage`) | - |
| Pinned columns are opaque with a 1 px divider, no shadow | replace own sticky shadows with `border-right: 1px solid var(--sw-border)` | check `design-shadow` |
| Empty texts per table: `EmptyTitle` / `EmptyText` (`table.html`: `et` / `ed`); `Header: false` / `true` (`&header=false` for a `table.html` iframe inside a page with its own header) | set them where needed | - |
| The header h1 is the menu item name (also on a failed load: "Not loaded" in the footer); `sw-table-loading` is only on until the first answer | name the menu item; pass `Name` with `Header: true`; use `.sw-table-busy` as busy hook | - |
| Database tables (`Db.GetAsTableData` / `GetAsTypedTableData`) apply `SearchText` without a full text index (parameterized `LIKE` over public string columns) | exclude a column with `[TableDataSearch(false)]`; very large tables get `[FullTextSearchIndex]` | - |

The table is `border-box` on any page, and low screens (<= 500 px high) get a compact grid, whatever the density.

### B14. Editors and forms

- **Own forms and wizards:** `Sw.Form({ Fields: [{ Name, Control, Required, Validate, When }], Submit, OnSubmit(values) })` validates on submit, shows each error under its field (never a toast), focuses the first bad field and gates the submit button; `Sw.Steps({ Steps: [{ Title, Sub, Content, Form, Validate, OnShow }], FinishText, OnFinish, OnCancel })` is the shared wizard (step list, Back / Next / Cancel, focus). Replace hand-rolled step lists and "validate every control, show a toast" code (ui/README.md "Forms", "Wizards"). **check**: not flagged.
- **Object editor (`Sw.Edit`) and `Sw.PropertyEditor`** give Design v2 forms by themselves: wrapping labels with the type hint, help and validation under the control (`Validate(true)` shows the message under the field; show `err[1]`, it names the member), calm rows, control widths by type (tokens `--sw-edit-number-w`, `--sw-edit-field-w`, `--sw-edit-text-w`, `--sw-prop-field-w`, `--sw-label-w`), collections with "+ Add", a dashed `null` token, read only values as text, a colour editor, a sticky footer. Don't build label / value tables for C# types and don't style the inner rows.
- An editor directly in a `Sw.Card` body uses the card as its frame (its menu joins the card tools); `sw-edit-framed` keeps its own box; presets `Sw.Edit.Options.Clean`, `CleanHints`, `CleanHintsReadOnly`. Page code may create editors at DOM ready (`new Sw.Edit.Options()`, `new Sw.Edit(typeName)`, `AddType` before `Sw.Edit.Ready`). Client types and plug-ins: edit/README.md.
- **Read only renders text:** enums, booleans ("Yes" / "No"), numbers, colours and languages are display text (`.sw-edit-rotext`), not disabled controls; own plug-ins render `Sw.Edit.ReadOnlyText(...)`. `Sw.Edit.FormFooter` hides itself when nothing is editable (`ShowReadOnly: true` keeps it). A whole-object paste keeps read only members; load a record with `await editor.SetObject(value)`, user edits go through `ReplaceObject`.
- Typed json: `await Sw.Edit.CheckJson(type, value)` (null or `{ Path, Text }`, every depth, the line from `Sw.Edit.JsonLines`) instead of `JSON.parse` alone. Pasted / dropped collection items lose a `$type` equal to the element type. `EditTypeUnit.js` is one of the editor files: a page that loads them itself (rare, `Sw.Edit.Init` does it) must not skip it. `--sw-edit-chars` is no longer read: set `--sw-edit-number-w`. Colour text boxes read `0x` hex in non-integer members as `0xRRGGBBAA`. A null result is an empty state (`Sw.StateView`), never an object editor.
- **Describe your C# members** (server side, backward compatible): `[EditUnit(EditUnits.Seconds)]` (a number in its unit, typed units "1h 30m" / "32 MB", Min / Max, slider), `[EditColor(EditColorFormat.Hex)]` / `Css` / `Rgba8` ..., `[EditPassword]`, `[EditType(EditTypes.Url / Phone / Email)]`. They change only the editor, never the stored value; one per member, not combined with `EditHideIf`; no `EditType` on members that also take other forms such as `$(...)` templates. The shared `ApiKeyParams.ApiKey`, `CredentialParams.Password` and `AppColorParams` already have them.
- `explore/api.html` is a Design v2 page: its old sections (`.api-section`, `.api-head`, `.api-tags`, `.api-send`, `.api-desc`, `.api-lock`, `.api-message-*`) are gone; style `.api-meta`, `.api-tag`, `.api-signature`, `.api-card` or the tokens. Its title is "<Method> · API".
- **migrate**: the `Edit*` aliases (A7). **check**: `design-legacy-class` (the removed api.html sections), the generic rules on your form css.

### B15. Shell and menus

The shell is a CSS grid: a full height sidebar (brand bar at its top, the menu tools Pin / Collapse all / Expand all above the search, section labels, monochrome icons, the current item in accent, a scroll cue when cut off) and a top bar with the breadcrumb (`app-ContentTitle` is the breadcrumb container). The element names `app-header`, `app-menu`, `app-ContentTitle`, `app-content`, `app-PinButton` are kept, so `app/app.css` rules on them still match; rules that drew header gradients, shadows or a centred title are obsolete.

- **Icons:** the chrome uses the line set (`Sw.Icon`); the IconModule menu icons are drawn as their line twins (`Sw.LineIcon`), so the menu has one outline style. An app's own menu icon stays a monochrome mask (`--sw-text-3`): draw it as a 24 px outline (1.5 to 2 px stroke), or register a line twin in `app/app.js` (`Sw.Icons.Add(name, svg)`, `Sw.Icons.AddLine("myicon.svg", name)`). Full colour svg urls and `IconLogo` stay masks. Want colour? Show it in content. Css on the old header icon classes (`app-SettingsButton .IconSettings`, `app-BackButton .IconNavBack`) targets `.sw-shell-hicon` (an svg).
- **One icon per page:** two items of a menu must not show the same svg (the rail has no labels). The framework pages have distinct icons (ui/README.md "One icon per page"); pick an unused one for your `WebMenu*` attributes; a folder that shows the same icon as its child gets `IconMenuFolder`.
- On phones in portrait the theme toggle is in the settings menu and long titles wrap to two lines; landscape phones get a compact drawer.
- A deep link or menu item to a missing page shows "Page not found" with Back / Go to Home (automatic).
- **Badges:** `MenuTree.SetBadge(entry, count, text)` puts a count on a menu row; Problems gets one automatically. A sign out (here, in another tab, an admin ended the session, it expired) reloads the whole shell at once, so nothing of the user stays on screen; the reloaded shell says "You are signed out" once (automatic). Long single word titles on phones shrink or end with an ellipsis instead of breaking inside the word.
- **Menu alert on the menu button:** while the menu is a drawer (phones) or hidden, the header menu button shows a dot and says the badge in its name ("Show the menu, Problems: 1 part failing in the last 24 hours"). Automatic for the Problems badge; an app that sets its own badge sets `SysWeaverApp.MenuAlert = text` and calls `SysWeaverApp.UpdateMenuAlert()`.
- **The app logo in menus:** a menu item with the icon class `IconLogo` (the app's full colour `icon.svg`, ex: "App theme") is drawn as the line glyph `logo` (a mask of a filled logo was a grey block); `Sw.Icons.AddLine("IconLogo", name)` picks another glyph.
- **check**: the `design-*` rules on `app/app.css` like on any page css.

### B16. Themes and branding without forking

Everything an app brands lives in its own files (the folder added last wins); the framework always serves an empty `app/app.css` / `app/app.js`, and `app/app.css` reaches every page:

```css
/* app/app.css */
:root:not([data-theme]) { --ThemeAcc1: #c2185b; }                 /* the accent of Light */
:root[data-theme="dark"] { --ThemeAcc1: #f06292; }                /* the accent of Dark (Pro, Application, Hacker keep theirs) */
:root { --sw-brand-logo: url("../app/acme-logo.svg"); --sw-brand-icon: url("../app/acme-mark.svg"); }
:root[data-sw-scheme="dark"] { --sw-brand-logo: url("../app/acme-logo-dark.svg"); }   /* marks: every dark theme */
[data-theme="acme"] { --ThemeBackground: #101418; --ThemeMain: #e8ecef; --ThemeAcc1: #4fc3f7; --ThemeAcc2: #9fb6c8; color-scheme: dark; }
.acme-banner { background: var(--sw-surface); border: 1px solid var(--sw-border); }   /* own classes on tokens */
```

- **Brand bar (OWN3-Banner):** the shell menu shows the 9:2 `banner.svg` when expanded and `icon.svg` when collapsed to the rail, crossfaded, the icon placed exactly on the banner's left square (the generated banner draws the icon there). An app's own banner: 3:1 with the app icon in its left square for a seamless change (`--sw-brand-banner` points at another file). `--sw-brand-logo-h` / `-w` / `-ratio` no longer apply to the shell (the logo stays the Welcome page's). **check**: nothing to port; an app/app.css that set the old logo box tokens can drop them. The banner box has the shape `--sw-brand-banner-ratio` (ui/tokens.css, 4.5 = the generated 1260 x 280 banner; it was 3:1, 1200 x 400): an app that serves its own banner of another shape sets it in `app/app.css` (`:root { --sw-brand-banner-ratio: 3; }`), so the icon of the rail still lands on the banner's left square and the sign in card is not letterboxed.
- **`app/app.js` runs in every page** (also framed content pages): register cell formats, edit plug-ins, line icons and themes there; shell only code goes in `AppMain()` or behind `Sw.Page.IsTop`. An app.js that touched shell elements at the top level must move that code (it now also runs in content pages).
- **Selectors:** `data-sw-scheme="dark"` is set for **every** dark theme (Dark, Pro, Application, Hacker): use it for marks, never for colours, or Pro / Hacker lose their accent and the Application theme its per-app colour. A theme registered in code with `Colors` is applied as `data-theme="dark"` (its `Base`) with inline colours, so `[data-theme="<name>"]` never matches it: a theme that needs its own css is a css theme plus `Sw.Theme.Register(name, { Title, Scheme })` without `Colors`.
- Change the four base colours (or a whole theme), never the derived tokens one by one; declare `color-scheme`; keep text and accents >= 4.5:1.
- Browser and install icons: the server generates `icon.svg`, `logo.svg`, `favicon.ico` ... per app from the app name / `AppSeed` (never SysWeaver marks); to use your own, put files of the same names at the root of your web folder. No `@import` in `app/app.css`, urls in custom properties written as `../app/<file>`, fonts as `data:` urls.
- Full guide: [`_docs/WebBranding.md`](../../_docs/WebBranding.md) ("Selectors: which rule reaches which theme").
- **check**: `css-import`, `brand-url`, `font-file`, the `design-*` rules for the classes in `app/app.css`.

### B17. App colours (Application theme)

The Application theme gives every app its own hue so many open service tabs are distinguishable: the server derives the colours from `AppSeed` (`SysWeaver.AppInfo` params in `*.Services.json`), the same colours are used by the generated icon and logo and the e-mail templates. There are no explicit colours: to change them, change `AppSeed`. Seeds of apps often open together at least 40° apart (Debug > App theme previews them). Pages get this for free with the role tokens (B3, B4).

### B18. Custom controls must be accessible

```html
<div class="tile" onclick="open()">Reports</div>                     <!-- before: no keyboard, no role -->
<button class="tile" onclick="open()">Reports</button>               <!-- after (or role="button" tabindex="0" + Sw.KeyClick) -->
<button aria-label="Close" title="Close"><span class="IconClose"></span></button>   <!-- an icon only button has a name -->
```

Things that open a page are `<a href>` links (middle click, copy link), actions are buttons, images have `alt` (`alt=""` for decoration). **check**: `a11y-custom-control`, `a11y-name`.

### B19. Styles set from JavaScript

`x.style.boxShadow = "0 2px 8px #000"`, `x.style.background = "linear-gradient(...)"`, `"color: #fff"` style strings bypass the themes: toggle a class whose css uses tokens (values that come from data, ex: a user picked colour, are fine: mark them). **migrate**: token renames inside such strings (B1, B3). **check**: `design-inline-style` (also `style.setProperty(...)`, `Object.assign(x.style, { ... })`, `cssText` strings with a shadow, gradient, colour or px radius, and an own custom property set to one of those), `design-font`.

### B20. Framework pages and server side changes

If your app links to, embeds or restyles the framework's own pages (the pages need nothing from you):

- **Home** (`app/`): a dashboard (KPI tiles, shortcuts, recent pages, problems from the exception trackers: "Failing" badges are real failures); `.HomeStatus` / `img.UserImage` rules keep working.
- **ServerManager**: the classes `.sm-stat*`, `.sm-charts`, `.sm-history`, `.sm-commands`, `.sm-danger*`, `.si-name`, `.si-state`, `.si-grid*`, `.vi-name`, `.vi-values`, `ss-add`, `kf-table` and the global `smStat` are gone (check: `design-legacy-class`); its upload and fill table helpers are the shared components of B11 (`SmShared.KpiSub` / `LineCard` stay ServerManager helpers); danger dialogs read "Kill Mail?", "Delete 1.2.0?".
- **Auth pages**: `Sw.AuthUi.Page(icon, build, { Account: true, Description })` gives account pages the form layout; the sign-in card is on the plain canvas (`auth/Login.html?weave=true` for the weave). **A custom sign in page** must handle `Api/auth/Login` answering `{ Succeeded: false, PasswordChangeRequired: true, SentTo }` (an administrator forced a reset; no session; continue with the mailed code to `auth/ResetPassword.html?<code>`), else it says "wrong password".
- **Users** (UserManager): the user menu's Management > Users opens `auth/Users.html` (next to Invite user, the original's place; the main menu copies Administration > Users / Invite user are gone, S7a O3) (tabs Users / Pending invitations, Invite user, Force password reset as a row action); Admin may view and invite users and force resets. C#: `ForcePasswordReset(id, context)` (the request is a new hidden parameter; it signs out the user's sessions), `ForcePasswordResetUser(id)` (a result instead of an exception), `InvitationTable` / `CancelInvitation(token)`.
- **Active sessions**: an *End session* row action (`Api/debug/EndSession`, AdminOps, token from the table); 8 columns shown by default, the others in the column picker (tests show a hidden column first). C#: `ActiveSessions(r, context)`, `CloseSession(session, reason)`.
- **Maps** (Media): as the original, the map views load their tiles at once (OSM, or Google with `Api/GetGoogleMapsKey`); there is no `OnlineMaps` parameter or `GetMapsOnline` api any more.
- **Log file, translator, upload test, fake email, FakeText, chat**: fill pages with the buttons in the page header; record pages use `Sw.Facts` + cards with `MenuTitle: false`; log level badges and separators come from theme tokens.

## Part C: UX guide rules

The [UX guide](../../_docs/UX_Guide.md) holds the rules every page follows. `SwWeb check --design` reports the ones a static check can decide as `ux-*` warnings (the id is the guide rule: `ux-inp-25` = UX-INP-25; the message ends with the rule and its section), so the gate `check --strict --design` covers them. Fix the page; a deliberate exception gets a comment that names the rule and the reason on the line or the line before: `/* SwWeb:allow ux-inp-11 (a drag handle) */` (`SwWeb:allow-design` doesn't allow a UX rule, except `ux-pop-1`, `ux-pop-9`, `ux-inp-25`, which were design rules before). Nothing here is rewritten by `migrate` except the zoom lock (C1).

### C1. Input (INP)

- **Act on `click`** (`ux-inp-1`): a `pointerup` / `mouseup` / `touchend` handler without a press or move handler, or an inline `onmousedown` action, is an action on release / press: keyboard, screen readers and voice control never fire it. Use `click` with `if (Sw.BadClick(ev)) return;`. Drags (press + move + release) and press-and-hold repeats are fine.
- **Keep the browser menu** (`ux-inp-4`): no `oncontextmenu="return false"`, no page wide `preventDefault()`; an element with a menu of its own uses `Sw.LongPress`.
- **Cursors** (`ux-inp-10`): no `cursor: wait`; `cursor: progress` only on a busy control (`Sw.Button` with a promise from `OnClick` does it), never on `body`.
- **Hover only for a mouse** (`ux-inp-11`): put `:hover` rules in `@media (hover: hover) { ... }` (as the components do), else the hover look sticks after a tap.
- **Disabled buttons** (`ux-inp-12`): not the native `disabled` (it leaves the tab order, the reason can't be read): `aria-disabled="true"`, a title that says why ("Select a row first") and an activation guard; with `Sw.Button` use `SetDisabled(true, reason)` or the option `Disabled: "reason"` (ui/README.md "Button", "Disabled, busy and copy").
- **No Alt+Left / Alt+Right shortcuts** (`ux-inp-23`): they are the browser's Back / Forward.
- **No zoom lock** (`ux-inp-25`): no `user-scalable=no` / `maximum-scale=1`; **migrate** `--level design` removes them from the viewport tag.
- **No `selectstart` handlers** (`ux-inp-30`): chrome is not selectable by the theme, content opts in with `.sw-selectable`.

### C2. Pop-ups, focus, forms (POP, FOC, FRM)

- **`Sw.Dialog`, not an own `<dialog>`** (`ux-pop-1`) and **not `confirm()` / `alert()` / `prompt()`** (`ux-pop-9`): `Sw.Dialog.Confirm` (`Danger: true` for a destructive action) or `Sw.Cells.ConfirmDanger`, `Sw.Toast`, `Sw.Dialog.Prompt` (A5). **Buttons are verbs** (`ux-pop-9`): no "OK" / "Yes" / "No" labels; the way out is "Cancel".
- **No positive `tabindex`** (`ux-foc-4`): order the markup as it is shown; `0` or `-1` only.
- **Labels** (`ux-frm-1`): every field in the markup has a `<label for>` (or is inside a `<label>`, or has `aria-label` when its purpose is shown otherwise); a placeholder is not a label. **Validation errors inline** (`ux-frm-3`): not `Sw.Fail(_TF("The name is required"))` but the message under the field (`Sw.Form`, `Validate()`). **Passwords** (`ux-frm-6`): `autocomplete="current-password"` / `"new-password"`.

### C3. Motion, layout, help, text (MOT, LAY, TIP, ACC, TXT)

- **Durations from the theme** (`ux-mot-2`): `transition: opacity var(--sw-dur-2) var(--sw-ease)`, never `200ms` / `.2s` / `cubic-bezier(...)`, also not in style strings or `el.animate({ duration })`; a `transitionend` wait needs a `setTimeout` fallback (themes with zero motion). The durations scale with the theme's `--sw-motion` (50 / 100 / 150 ms at 1); a script that needs one reads the computed `--sw-dur-*` (a registered time, "0.1s"), Chart.js takes `--sw-dur-3` / `--sw-dur-2` for its animations. Infinite spinners are exempt.
- **Animate only opacity, transform and colours** (`ux-mot-4`): no transitions / keyframes of `width`, `height`, `top`, `left`, margins (an expanding section may animate `grid-template-rows`). No `<marquee>` / blinking text (`ux-mot-6`).
- **Reserve the space of media** (`ux-lay-1`): `<img>`, `<iframe>`, `<video>` get `width` and `height` attributes (the intrinsic size; css still scales it), a css width and height, or an `aspect-ratio`; fade them in with `Sw.FadeSrc`.
- **Tool tips add something** (`ux-tip-2`): `<button title="Save">Save</button>` / `{ Text: "Save", Title: "Save" }` repeats the name; say the target, the consequence or the shortcut, or drop the title.
- **One `<h1>`** (`ux-acc-3`); **an `aria-label` starts with the visible text** (`ux-acc-10`).
- **`translate="no"` in lower case** (`ux-txt-12`); **no words in css `content:`** (`ux-txt-15`): css is not translated, set texts from JS with `_TF`.
- **Slide distances from the theme** (MOT-2): `transform: translateY(var(--sw-dist-2))` (`--sw-dist-1` small shifts, `-2` menus / tool tips / toasts, `-3` dialogs / sheets), never `8px`. Hacker sets every duration and distance to 0 and Pro shorter ones (now: one scale, `--sw-motion`: 1 normal, Pro 0.25, Hacker 4 while testing): a page must not wait for a transition. `Sw.FadeSrc` fades with `--sw-dur-2` now (was 150 ms); `data-sw-fadein` / `FadeIn` still override it.

### C4. Data safety, feedback, sessions (SAF, FBK, ACC-7) - opt in, nothing breaks without it

- **Unsaved edits** (SAF-10, opt in; C13): the original framework never asked before leaving, so a port keeps that unless the old page asked itself. Where the old page had its own `beforeunload` (or the owner wants the question): call `Sw.Page.SetDirty(key, text)` while the form or editor differs from its saved state and `Sw.Page.ClearDirty(key)` after a save (or give the Sw controls / `Sw.Form` / `Sw.Edit.FormFooter` `Dirty: true`); the shell then asks "Leave without saving?" on menu, links and Back, and the tab close prompt comes for free. Drop that own `beforeunload` handler then (the text editor keeps its own and also calls `SetDirty`). Don't add a question the old page didn't ask. Work that stops with the page: `SetDirty(key, "The export stops.", { Work: true })` or `Sw.Activity.Start`.
- **Drafts** (SAF-11): replace own local storage drafts with `Sw.Draft.Attach(textarea, { Context })` and call `Clear()` after sending (keys `SysWeaver.Draft.*`; read an old key once and `Sw.Draft.Set` it to keep users' drafts). Mark secret fields `data-sw-nodraft`.
- **Background work** (FBK-4): long jobs go to `Sw.Activity.Start` / `Track` / `Send` (one running job per `Key`), so the header's activity list finds them; no own "jobs" toast lists.
- **Lost connection** (FBK-9): as the original, every top window blocks with "Server communication lost." after 3 failed message polls and unblocks with the first poll that succeeds (`Sw.Session.BlockOnLost = false`: no overlay); the shell also shows one offline line. Reads that should retry use `Retry: "online"`; a failed save with `Sw.Net.IsNetworkError(e)` keeps the values and offers Try again. Refresh data on `Sw.Events.On("net.online", ...)`.
- **Announcements in dialogs**: `Sw.Page.Announce` already places its live region inside the open modal; `Announce(text, { Assertive: true })` for errors.
- **Session limits the app knows** (ACC-7): `Sw.Session.WarnTimeOut({ At, Extend })`.
- **Hacker title cursor** no longer blinks (MOT-6). SwWeb: nothing to migrate; `check` reports own `beforeunload` and drafts, dirty keys never cleared, raw fields saved without tracking and multi-line text without a draft (`ux-saf-10`, `ux-saf-11`; C7).

### C5. Controls: disabled, modifier clicks, copy (INP) - works without changes

What changed for every page (nothing to do unless noted):

- **Disabled is `aria-disabled` now** (INP-12): `SetDisabled(bool, reason)` and the option `Disabled: true | "reason"` of every Sw control keep it focusable, muted and inert, and show the reason on an attempt; `b.E.disabled` still reads / sets it. **Do:** pass a reason that says what enables it (`b.SetDisabled(true, _TF("Select a row first", "..."))`); own elements use `Sw.Disabled.Set(el, true, reason)` instead of `el.disabled = true` (**check** `ux-inp-12`). Page css that styles `:disabled` on Sw controls becomes `:is(:disabled, [aria-disabled="true"])` (the generic muted look applies meanwhile). UI tests that wait for "enabled" treat `aria-disabled` like `disabled`.
- **Modifier clicks** (INP-2): in own click handlers `if (Sw.ModifierClick(ev)) return;` (or `Sw.BadClick(ev)`, which now also moves and shows the focus). `Sw.SelectList` follows it too: a Shift / Ctrl / Alt / Meta click in a single select list (and Alt in a multi select one) only makes the row active, it no longer selects or opens it (UI tests that used a modifier click to select must click plainly).
- **`Sw.Form({ Gate: "required" })`** (opt in) disables the submit button the reachable way with the reason "Fill in <labels> first"; fields marked `Required` in the form get `aria-required`. Nothing to do.
- **Page header "More actions"** is a menu button now (right click, long press, Menu key, Shift+F10 open it, `aria-expanded`); `ShowMore()` still opens it, `MoreItems()` returns its items.
- **Menu buttons** (INP-3): `Sw.Button({ Menu })` or `Sw.MenuButton(el, items)` (right click, Menu key, Shift+F10, `aria-haspopup` / `aria-expanded`); no hand-made `contextmenu` + `preventDefault` (**check** `ux-inp-4`). `Sw.NoContextMenu()` only blocks touch long-press menus now.
- **Copy** (INP-31, as the original): `Sw.Clipboard.CopyOnClick(el, value)` copies on a plain click (a toast names the value), as the old `copyOnClick`; Ctrl+C on the focused value and its menu (right click, long press) copy too; a link / control inside the value keeps its own click. `{ Keyboard: true }` (the old `addKeyboard`) makes it a Tab stop on which Enter / Space copy. The legacy `ValueFormat.copyOnClick` on an unported page keeps the original contract exactly (`el.onclick`, an inner link copies, the browser's right click menu). A copy button: `Sw.CopyButton({ Value, What })` (in place check mark + announcement, no toast); `Sw.Clipboard.Copy(v, { Button, What })` confirms on the button.
- **Busy buttons** (FBK-2, 3): return the promise from `OnClick`; the button announces "Working…" and the result (option `Done: false | "text"`) and keeps its width. Don't set `disabled` while saving (busy is not disabled).
- **Shortcuts** (TIP-1, TIP-2): option `Shortcut: "Ctrl+S"` (or "Mod+S") adds it after the `Title` tool tip and as `aria-keyshortcuts`.
- **Usage hints** (TIP-8): there is no usage hint component (`Sw.Help.Learned` and the shell setting "Show hints again" were removed, OWN3-Shell-4: unused). Usage help that a page really needs is plain page text; field instructions go into the field `Hint`.

### C6. Dialogs, toasts and tables (POP, FBK, LST) - behaviour changes to check

- **Clean dialogs close on an outside click** now (POP-2; the old "modal when it has buttons" default is gone): pass `Modal: true` for a dialog that must not (an outside click then gives the rejection cue).
- **Dirty dialogs ask "Discard changes?"** on Cancel, ×, Escape, Back and swipe (POP-6): fields changed by the user make a dialog dirty automatically. After a save that keeps the dialog open call `h.SetDirty(false)`; filter / helper fields get `data-sw-nodirty` (or the dialog `AutoDirty: false`); state the detection can't see: `h.SetDirty(true)`.
- **Busy dialogs**: a footer button with an async `OnClick` makes the dialog busy (× / Escape refused with the cue until it ends, after 10 s a Close appears); long work: `h.SetBusy(promise, { Cancel, Background, Text, Progress })` or `Sw.Dialog.Working({ Title, Text, Cancel, Background, Promise, Progress, CloseText })`, which now has a × and, without Cancel / Background, a 10 s "Close without waiting" (`CloseText` names the consequence). "Continue in background" hands the work to `Sw.Activity` (C4).
- **Movable dialogs** (POP-7): give a dialog kind a stable `Id` (`Sw.Dialog.Open({ Id: "orders.edit", ... })`) so its position and size are remembered; `NoMove: true` for one that must stay put. `Above: true` keeps a dialog above every pop-up (warnings).
- **Back closes pop-ups** (MOB-3): pages need nothing; a page with an own overlay registers it as a `Sw.Popup` / `Sw.Dialog`.
- **Toasts** (FBK-8): error toasts stay until closed (the duration argument of `Sw.Fail` is ignored), other toasts stay by text length; `Sw.Fail` says nothing for network errors while offline (the shell line does). Don't put information only in a toast. In the shell F6 / Shift+F6 also reach the shown toasts (of the shell or of the framed page).
- **Page changes are announced** (FOC-6): a page shown by the shell while the focus stays in the chrome (a menu item, Back) is announced by its name; nothing to do.
- **Tables**: modifier clicks only focus (never sort or run row actions), Ctrl+C copies the plain value, the live refresh holds the order under the pointer / focus (`tr[data-sw-move]` arrows, "Order on hold"), offline tables keep their rows ("Not updated since …"), the search needs every word (a C# change: apps get it at their next build). UI tests that clicked a row while it was refreshing see the order on hold. explore/README.md "Grid conventions".

### C7. The right way by default (dirty, busy, cancel, drafts, exit motion) - behaviour changes to check

The framework does what pages had to wire by hand. Nothing breaks; check these where a page relied on the old behaviour. Where an item here and the original behaviour differ, the parity rule of C13 wins (the owner's direction):

- **Unsaved edits are opt in** (SAF-10, parity P3-A-4; C13): the Sw value controls, `Sw.Form` and `Sw.Edit.FormFooter` don't mark the page dirty by themselves (the original never asked before leaving). **Opt in** where the old page asked (its own `beforeunload`): `Dirty: true` (or a sentence: "Your changes to <title> are lost.") on a `Sw.TextBox` / `NumberBox` / `Select` / `Toggle` / `Segmented`, a `Sw.Form` (dirty until a successful `OnSubmit`) or a `Sw.Edit.FormFooter` (`Changes() > 0`); the shell then asks before leaving. An opted-in control is tracked whenever the user changes it; `MarkClean()` after an own save, `data-sw-nodirty` on a container; a primary `Sw.Button` cleans its container when its promise succeeds (`Saves: false` for one that isn't a save); an `OnChange` that saves (returns a promise) counts as saved. A fill-in button ("Same as billing") counts only for an opted-in field. `Sw.Page.SetDirty(key)` alone marks dirty with a generic text (only `null` / `false` / `""` clear). Raw `<input>` pages call `SetDirty` themselves where the old page asked (**check** `ux-saf-10`).
- **Busy without a returned promise** (FBK-2): requests started synchronously in a `Sw.Button` / `Sw.OnClick` click get the click's `AbortSignal` and keep the button busy, but return the promise anyway: the button then shows a failure ("<name>: not done") and covers work started later (**check** `ux-fbk-2`). The button is not busy while a dialog it opened waits for the user. Busy work over 2 s asks before the page is left ("<name> may not finish."; `Leave: false` opts out).
- **Cancel cancels** (POP-6): a dialog footer `OnClick(close, value, { Signal })` and the opener `Sw.Button`'s click scope receive the signal that the dialog's Cancel aborts; pass it on to requests started after an `await` (`Sw.Net.Send(url, arg, { Signal })`, **check** `ux-pop-6`). While a dialog is busy its other buttons are `aria-disabled` ("Still working…"; `WhileBusy: true` keeps one active), and a Cancel that can't stop the work is hidden while it runs (its slot stays). `h.SetBusy(({ Signal }) => work)` and `Sw.Dialog.Working({ Work: ({ Signal }) => work })` are cancellable; `Working({ Promise })` offers "Continue in background" (`Background: false` opts out) (**check** `ux-fbk-4`).
- **Confirmations are safe by default** (SAF-3, POP-9): `Sw.Dialog.Confirm` focuses Cancel and Enter no longer confirms; a harmless confirmation that relied on Enter passes `Safe: true` (never from a danger opener, **check** `ux-saf-3`). `Sw.Dialog.Alert`'s button reads "Close". Destructive actions: `Sw.Danger.Run({ Action, Target, Item, Consequence, Run(signal) })` (confirm + busy + focus + one result) or `Sw.Cells.ConfirmDanger` (**check** `ux-saf-1`).
- **Dialogs**: Escape in a dialog field is a close attempt (it asks when dirty) instead of reverting the field (outside dialogs Escape keeps the typed text, as the original inputs; `EscapeReverts: true` reverts, C13) (`data-sw-ownescape` keeps a field's own Escape); on touch dialogs don't focus text fields (`TouchFocus: true`); every dialog kind remembers its position by a default id (page path + title + buttons), per site (the old keys are read); give two kinds two `Id`s (**check** `ux-pop-7`). A dirty dialog keeps its own "Discard changes?"; it reports itself to `Sw.Page.SetDirty` only when it opted in (`Dirty: true`, C13).
- **Exit motion** (MOT-1): closed dialogs, sheets, popups, menus, toasts and hints stay in the DOM as `.sw-leaving` for one theme duration; UI tests count `.sw-dialog:not(.sw-leaving)` or use `Sw.Popup.IsOpen()`. Own overlays leave with `Sw.Motion.Out(el)` instead of `el.remove()` (**check** `ux-mot-1`).
- **Drafts** (SAF-11): `Sw.TextBox({ Draft: "context" })` / `Sw.Form({ Draft })` instead of wiring `Sw.Draft.Attach`; `Attach` also takes a `Sw.TextBox`, never returns null, restores synchronously and places the notice under the field. A restored draft shows "Draft restored (saved …)" and does not make the page dirty (`Dirty: "restored"` opts in). In a dialog a draft is cleared on Discard and disposed on close; elsewhere keep the handle and `Clear()` it after the save (**check** `ux-saf-11`).
- **Clicks on cards and rows** (INP-2): `Sw.OnClick(el, (ev, { Signal }) => work)` (pure click, keyboard, modifier click, disabled reason, busy) or `Sw.Card({ OnClick | Href })` (**check** `ux-inp-2`). Lists of rows: `Sw.Table.Create({ Element, Rows, Cols })` instead of a hand-made table (**check** `ux-lst-3`); live rows: `SetRows(rows, true)` (**check** `ux-fbk-7`).
- **Disabled** (INP-12): an attempt names only the reason (no fallback to the title), so pass one (**check** `ux-inp-12` for `Disabled: true` and `SetDisabled(x)` without a reason); `Sw.Disabled.Set` on a native field also blocks keys, paste and changes. **Copy** (SEC-1): values that look secret are never quoted; `Show: true` is the exception (**check** `ux-sec-1`).
- **Offline** (FBK-9): `Sw.Form` keeps the entries and offers Try again ("Not saved: the connection was lost"; `Resend: true` resends on `net.online`); a failed `Sw.Net` request checks the connection before it throws, so `Sw.Net.Online` / `e.Offline` are right in the catch; use `if (!Sw.Net.ShouldReport(e)) return;` (synchronous, a boolean) before an own error message.
- **Table export** (FBK-4): `table.Export(mode, exporter)` is background work (busy Download button, an activity job with Cancel); own `ExportTableData` downloads are reported (**check** `ux-fbk-4`).
- **`SwWeb bundle`** refuses to write a `.js` bundle whose sources don't parse or call a name no bundle declares (the previous bundle stays); name other scripts' globals in a comment of the source (`/* global Edit */`) or run with `--no-verify`.

### C8. One result path, honest Cancel, developer warnings, server jobs - behaviour changes to check

- **One result for every async handler** (FBK-5): a `Sw.Button` / `Sw.OnClick` / auto-saving control / row action / `Sw.Danger.Run` whose work throws, rejects, has a failed request it started or resolves `false` is "not done", shown once (nothing when the work showed a toast, an alert, a form status or a field message itself, else an error toast / "<name>: not done."); an aborted signal is "stopped", never "done" (`Sw.Work.Outcome`, `Sw.Work.Report`, `Sw.Work.Error`). A handler that returns `false` should say why first (**check** `ux-fbk-5`). A failing auto save (`OnChange` that throws / resolves `false`) puts the picked value back and says why.
- **Saves clean by what they sent** (SAF-10): a non-primary button or Enter (`OnEnter` runs in a click scope now) whose work sent the edited values cleans them; a live filter (`OnInput` only) and controls without any save path are not tracked; an `input` event that isn't the user's (a draft restore, a script) is the new base. Remove own `Sw.Page.SetDirty` calls next to Sw value controls / `Sw.Form`: the leave question would name the change twice (**check** `ux-saf-10`).
- **Honest Cancel** (POP-6): a dialog footer button's work is cancellable only when it read `{ Signal }` and sent no write yet (or the button has `Cancellable: true`, for a read); else the footer Cancel becomes "Stop waiting (<name> may still finish)": the dialog stops waiting, the work goes on, a later failure is a toast, a later success closes the dialog. After Cancel the dialog announces "<name> cancelled", never "done". `SetBusy` with `Cancel` / `Abort` / `({ Signal }) => …` keeps Cancel (**check** `ux-pop-6`: a read started after an `await` without the signal).
- **Dialogs**: dirty means user input only (values set from code, also after an async load, are no edits; `h.Recheck()` counts code changes, only drafts need it); `Role: "alertdialog"` dialogs are not dirty tracked (`AutoDirty: true` for one with real input); one leave sentence per dialog; the rejection line is a row after the title (`.sw-dialog-status .sw-dialog-reject`); busy footer buttons never shrink. Menus and pop-ups opened from a dialog are owned by it (`Owner`): a press on the dialog behind closes the menu and is swallowed, they close with the dialog (UI tests close an open menu before clicking the dialog; **check** `ux-pop-1` for `Owner: null`).
- **Work that is not the click's**: `Sw.Net.Send / Get / fetch(..., { Detached: true })` (telemetry, a live view, a long poll) never makes the button busy.
- **Leave questions name the real consequence**: `Sw.Page.SetDirty(key, true, { Work: true, Name, Kind: "server" | "export" | "download" | "upload" | "save" | "local" })` / `Sw.Page.WorkText(name, kind)` ("You won't see the result of Kill devenv here; the server may still finish it."); the question closes by itself (as Stay) when the work ended and nothing else is lost (**check** `ux-saf-10` for `{ Work: true }` with the generic "stops" text).
- **Developer warnings**: one switch, `Sw.DevMode()` (localStorage `SysWeaver.DevWarn` = `"0"` off / `"1"` on, else `Sw.Debug`, else on for localhost; a page may set `Sw.DevMode = () => false`); every author warning goes through `Sw.DevWarn(msg, key)` (`[Sw UX]` prefix, once per key, never in production). New warnings: an own SetDirty next to a tracking control, a `TextBox({ Draft })` on a secret field, a second `Sw.Keys.Add` of a combination (`Replace: true` opts out) or a browser one, a second `Sw.Draft.Attach` on a field / a shared Context.
- **Drafts** (SAF-11, SEC-1): kept, never lost (`<key>~kept`, "You have N earlier drafts" + Restore); never drafted: password / one-time code fields and fields whose name / label / Context says key, token, secret, password, PIN, values that look secret (**check** `ux-sec-1`); `Sw.Draft.RemoveFor(prefix)` after deleting a subject whose drafts are per id (**check** `ux-saf-11`), `Sw.Draft.List(prefix)`.
- **`Sw.Net.ShouldReport(e)` is a boolean** (FBK-9): `if (!Sw.Net.ShouldReport(e)) return;`; `await Sw.Net.ShouldReportAsync(e)` waits for the connection check (**check** `ux-fbk-9`).
- **Server jobs** (FBK-4): long server work that should survive a reload runs as a server job (`request.Server.StartJob(...)` in C#; `Api/jobs/GetJobs`, `GetJob`, `WaitJob`, `GetJobResult`, `CancelJob`, `RemoveJob`, push `sw.job`); in the page `await Sw.Activity.StartServer(url, arg, { Save, DoneText })` (progress, Cancel, the file saved when ready, "Save again" after a reload; the shell calls `Sw.Activity.RestoreServer()`); table exports in the shell use `Api/StartExportTableApi` / `StartExportTableData`. Don't loop on `WaitJob` in page code (**check** `ux-fbk-4`).
- **Row actions**: a row action api answers "not done" with a reason (409), never a bare `false` (old servers' `false` is still "not done"); `ui/cells.js` dispatches `sw-cell-result` (`{ Ok, Aborted, Value, Error, Text, Name, Url, Ms, InPlace }`) on the button before any toast: a listener that shows the result in place sets `InPlace = true` (no toast, a quiet activity job). Active sessions send `IsCurrent` (the table marks "You"). Deleting the log file needs `Api/logFile/GetDeleteLogFileToken` first (`FileLogParams.DeleteNeedsToken`).
- **Focus after rebuilds** (FOC-1): `Sw.Focus.Keep(container)` -> `{ Restore(), Stop() }` around own list rebuilds, `Sw.Focus.Restore(target)` for component roots.
- **SwWeb**: the `ux-*` script rules now follow the call structure (helpers, callbacks, timers, aliases, destructuring, wrappers) and property / style writes in any spelling, so pages that passed by spelling a mistake differently are reported now (SwWeb README "How the script rules read a script").


### C9. Server reasons, one cancellation model, quiet restored jobs, drafts cleared when sent - behaviour changes to check

- **The server's reason reaches the user** (FBK-1, TXT-30): a 400 / 403 / 409 / 422 / 429 / 5xx whose body is a reason for people (marked `X-Sw-Reason: 1`, or a short plain text) is the "why" of every message (`Sw.Net.Error.Reason`, in `e.message` after the status line, `Sw.Net.ErrorText(e).Why`); the generic status sentence only without one. Build failure texts with **`Sw.Net.FailText(name, e)`** / **`Sw.Net.Message(what, e, { Do })`** instead of `ErrorText(e).Text + " Check the entries and try again."` (one advice, no ".: "). Code that parses `e.message` after the status line: it now holds the reason for 4xx too. A 403 with a reason (ex: "You can only invite users to your own domain") no longer disables the button (`Sw.Access.ActionDenied`).
- **`Detached` is a property of the request**: `Sw.Net.Get(url, { Detached: true })` inside an `OnClick` is no longer busy / abortable (it was: the inner fetch was collected). `Sw.Net.Live(url, arg)` = a detached `Send`. Nothing to change.
- **One cancellation model** (`Sw.Cancel`, core/README): Cancel of an activity job is "Stopping…" until the work confirmed it stopped (an AbortError, the server's answer), "Cancelled" only then; work that finishes anyway is "done" with "Finished before it could stop", and **nothing is saved or delivered after a Cancel** (a `Track` / `Run` `Result` that saves a file is not called; a server file is offered, not saved). Work that should stop must honour its signal: use **`await Sw.Sleep(ms, signal)`** (throws) instead of `Sw.Util.DelayAbort` (resolves), `Sw.Cancel.Check(signal)` between steps, and pass the signal to `Sw.Net`. `job.CancelFn` is the model's cancel (calling it is the same as `job.Cancel()`); assign `CancelFn` only to replace the job's cancel function.
- **`Track` / `Run` jobs reject on failure / cancel** (were: resolve `undefined`): `await job` throws the error / an AbortError (both `SwSaid`: the list showed them). A `Sw.Button` that returns the job shows nothing more; page code that awaits a job outside a button: wrap it in `try` (**check**: `await Sw.Activity.Run(` / `.Track(` outside an `OnClick`). `job.Finished` is the same promise for every job (also `StartServer` jobs); `Sw.Activity.RunServer(url, arg)` waits for a server job's end.
- **Restored server jobs are quiet**: after a reload, a new session or on another device a finished job is listed without a toast, with the server's end time; the shell follows the `sw.job` push (jobs of other tabs appear). Nothing to change.
- **Leave texts**: `Sw.Page.WorkText(name)` / `SetDirty(key, true, { Work: true, Name })` / an activity job without `Kind` say "<name> may not finish." (were: "<name> stops."); pass `Kind: "local"` for work that really stops with the page.
- **Stand-alone pages** (no app shell) show a small activity line at the bottom start while activity jobs run (and result toasts): a page with its own progress UI for the same work should not also start an activity job.
- **Drafts** (SAF-11): `d.Clear()` now also removes the kept earlier drafts of the field (pass `{ All: false }` for the old behaviour); call **`Sw.Draft.Sent(form)`** after a successful send (Sw.Form does it); a disabled / read only field shows no Restore; kept drafts are capped by size with a picker (no silent drop after 8); more fields count as secret (multi-language labels, `cc-*`, numeric code fields, connection strings, card numbers, IBANs, seed phrases): a field that was drafted before may not be any more (by design); `Sw.Draft.Attach` on a contenteditable is `Active: false` with a warning.
- **F6** inside an open dialog belongs to the dialog (between it and its toasts); the shell's region cycle starts again outside it.
### C10. Dialogs, controls, tables and the SwWeb rule families of round 3 - behaviour changes to check

- **Dialogs** (POP-6, FRM-5): while a footer button's work runs, the dialog's text fields are read only (focus, caret and copy stay) and other controls don't change; a newer edit typed before the lock keeps the dialog open and dirty against the saved values. Pass **`EditWhileBusy: true`** only when the save reads the fields again after its request (SwWeb `ux-frm-5`). Cancel follows the one cancellation model (C9): a write (`Sw.Net.Send`, a non-GET `fetch`) is **never aborted**, also with `Cancellable: true` (a DevWarn; mark a request that is safe to abort with `{ Read: true }` / `{ AbortSafe: true }`); a success after Stop waiting is said ("Save finished after all"), a failure after the work called `close()` is a toast. One footer layout engine; the self-test page `ui/dialogtest.html` (`window.SwDialogSelfTest`) checks every dialog state. **Check:** dialogs whose work keeps typing enabled on purpose; `Cancellable: true` on writes.
- **Prepared danger confirmations** (SAF-2, SAF-4): `Sw.Cells.ConfirmDanger({ ..., Prepare: async signal => facts })` / `Sw.Danger.Run({ Prepare })` fetch the facts or a one-use token busy on the button (or in a working dialog when the button is hidden in a menu); a failed prepare stops with the reason and **never falls back to a weaker question**. The log file delete uses it (`GetDeleteLogFileToken`). **Check:** own token / facts requests before a confirmation; a `catch` that opens a plain Confirm (SwWeb `ux-saf-4`).
- **Controls** (FBK-5, SAF-10, FOC-1): an auto-saving control saves one value at a time, the latest wins (a result for a value no longer shown is ignored); dirty belongs to the page: a save anywhere on the page counts (`Saves: true` on a button that is neither primary nor named as a save, SwWeb `ux-saf-10`); `Sw.Form` + a drafted field never says "lost"; `Sw.Keys.Add(combo, fn, { Scope: element })` runs only while that element is shown (two lists in tabs can both take "/"; SwWeb `ux-inp-23` asks for it in component methods); fields in a card body stack; row actions keep the focus on the row for every outcome.
- **Tables** (FBK-4, LST, SAF-12): an export's Cancel is `job.Cancel()` ("Stopping ..." until the server confirms, "Too late to cancel" when the file was ready; never saved after a Cancel); `Sw.Table.KeepStamp(text)` keeps a file name stamp on one line; counts never go stale after a row action; a row gone during its confirmation shows the real outcome; search, sort and page are kept per table. Nothing to change.
- **Server** (C#): refusals carry their reason (`HttpResponseException` 400 / 403 / 404 / 409 with `X-Sw-Reason: 1`; C9: the client shows it); `BackgroundJobContext.Request` is a detached copy of the request for the job's work, a cancelled job is "stopping" until its work returns, results without a file are allowed; a password reset link is one live token per user and purpose (an older link no longer works).
- **SwWeb** (Part C): `ux-fbk-2` "OnClick starts work and returns no promise" is an **error** now (also through a page method, `t.Delete(url)`), and it is checked in the framework's own files without `--framework`; a deliberate exception names the rule: `/* SwWeb:allow ux-fbk-2 (why) */`. New rule families (SwWeb README "Rule families"): `ux-sec-1` (secrets in local storage, cookies, url queries, logs), `ux-sec-2` (raw html of user data), `ux-txt-20` (numbers, sizes, dates by hand: use `Sw.Format`), `ux-txt-6` (concatenated sentences: `_T("Removed {0} items", n, "...")`), `ux-txt-30` (`Sw.Fail(e.message)`: use `Sw.Fail(e)` / `Sw.Net.FailText(name, e)`), `ux-txt-17` (clipped text without an ellipsis), `ux-inp-1` / `ux-inp-6` / `ux-inp-8` / `ux-inp-20` (actions on press, double click only, actions on hover, drag only), `ux-fbk-5` (silent failures: empty / log-only catches, `.catch(() => {})`, unhandled requests in listeners and timers), `ux-fbk-4` (`Sw.Activity.Run` work that ignores its signal, `DelayAbort` ignored: use `Sw.Sleep(ms, signal)`), `ux-mot-6` (endless blinks), `ux-inp-30` (content not selectable), `tr-untranslated` (also `Sw.Fail` texts like "Error: key rejected" and thrown error texts). **Check:** run `SwWeb check <srcWeb> --strict --design`; the gate is 0 / 0 for D:\SysWeaver and the services.

### C11. Role refusals, writes never aborted, user vs code edits, row reasons, link checks and the SwWeb families of round 4 - behaviour changes to check

- **Server** (C#): a 403 for a missing role keeps `X-Sw-Reason: 1` + the sentence and adds **`X-Sw-Denied: role`** (`HttpServerBase.DeniedHeader`); the server's own "sign in first" 401 adds `X-Sw-Denied: signin`; other refusals have no `X-Sw-Denied`. An exception that implements **`SysWeaver.Net.IHttpRefusal`** answers its status with its message as the reason (account mistakes are 400 / 404 / 409, a dead action token **410**, no more 500 "The action token has expired!"). UserManager: `Api/auth/CheckActionToken` (`{ Token, Kind }` → `{ State, Kind, Reason, Account, Target, Expires }`, never uses the token); tokens are revoked by purpose group (`TokenGroups`: a password change also kills open e-mail / phone links, and the other way round). A client that drops its connection is debug noise, never a "Failing" problem. **Check:** own `throw new HttpResponseException(..., ex.Message)`: the text is shown to users.
- **Role refusals disable again** (FBK-1, INP-12): `Sw.Access.ActionDenied` disables the target only on the role signal (`Sw.Net.Error.RoleDenied`, `Sw.Access.IsRoleDenied(e)`; also a 403 without a reason from an older server), with the server's sentence as its reason; a 403 with a reason and no role signal keeps the button usable. Nothing to change.
- **A sent write is never aborted** (POP-6, FBK-4): `Sw.Cancel.Controller()` marks a signal; `Sw.Net` never aborts a write sent with it (`Sw.Activity.Run` / `Send` use it): "Stopping… (what was already sent may still be done)", then the server's answer. **`{ Read: true }` is the body reader again**: the read mark is **`{ ReadOnly: true }`** or **`Sw.Net.Read(url, arg)`** (`Read: true` still works, with a DevWarn). `Sw.Net.IsWrite(opts, kind)` is the one test.
- **`Sw.Net.Message` returns a string** (`MessageText` the same; `MessageParts` the object, whose string value is the text): **check** `Sw.Net.Message(...).Text` (now `undefined`; SwWeb `ux-txt-30`).
- **Jobs** (FBK-4, FBK-5): `await job` of work that finished after a Cancel rejects with an AbortError marked `FinishedAnyway` (`Value` kept; toasts say "<title>: finished before it could stop", never "Done"); a cancel that can't reach the server stays "Cancel not sent yet …" and is sent when back online (also after a reload, 2 h); `job.Title` / `Label` / `Text` / `EndText` are the one source of the texts; `Track` whose `Result` throws fails the job; `job.Remove()` of a running job cancels first; `RunServer` shows a refused start once (`Quiet` opts out); `Progress` must be 0..1 (40 is unknown + a DevWarn). The stand-alone activity line reserves its height at the end of the page.
- **Drafts** (SAF-11): `handle.Clear()` is back to **this field only** (`{ All: true }` explicit) and never erases a newer text another tab typed; a `Sw.Button` / `Sw.Form` clears a drafted box only after a successful write (`Sw.Work.Current(el)`): **check** own `draft.Clear()` before the save's `await` (SwWeb `ux-saf-11`). The draft picker is a `Sw.Select`.
- **Controls** (POP-6, FRM-5, FRM-8, SAF-10): every control has **`Locked(bool)`** (no value change by key / wheel / pointer / paste; a refused change fires `sw-locked`); `sw-change` has `User: true / false` and code writes (`Set`, `.value`) never make a control dirty (`Sw.Edits.UserSince(root, since)`); a button whose OnClick sends values the page edits cleans them whatever its label ("Store settings"; since C13 only opted-in controls are tracked at all); an auto save without an answer says "still saving…" (10 s) and gives up with the old value (30 s); options of controls, `Sw.Keys.Add`, `ConfirmDanger`, `Sw.Danger.Run`, `Sw.Net`, `Sw.Activity`, `Sw.Draft` and `Sw.Table.Create` are checked in developer mode (**`Sw.DevOptions(where, o, schema)`**: unknown / misspelled / wrong type / colliding options are DevWarns, nothing in production); `x.Input.id = ...` keeps its label; a component used as a text is "" + a DevWarn (append `x.E`).
- **Dialogs** (POP-6, LAY-1, FRM-5, FRM-8): a busy dialog locks the Sw controls of its body (`Locked`) and says why on an attempt; a value the work writes into a field (the server's number, a normalised text) is no newer edit, the dialog closes; the dialog reserves the room of its busy line and footer (nothing grows, buttons keep their slots; at 400 % zoom the footer scrolls) and keeps the focused field in view. Fixed overlays of your own: register them with **`Sw.Overlay.Register(el, { Name, Owns })`** (a developer-mode warning when one covers a control; `Sw.Overlay.Check()` in self-tests). `ui/dialogtest.html` is the dialogs' self-test. Nothing to change.
- **Row actions** (FBK-1, FOC-1): the outcome has `Reason` and `Note` ("Kill: not done. <reason>") shown as visible text in the row and the card (kept across re-renders and the rows / cards switch); the focus follows the acted item, never another row's result; an action from the card kebab / a menu returns the focus there.
- **Link pages** (Auth / UserManager / PassKey): `Sw.AuthUi.CheckLinkToken(kind, { Page })` checks a link on load (dead links show why and offer Sign in instead of a form); an app's own link page adopts it with the three lines in auth/README "Link pages".
- **SwWeb** (Part C): 63 `ux-*` rules; the families judge what code does wherever it is written (the work of a user action = every function a handler reaches; secrets also from fields whose label / type says so; silent failures by data flow; methods of a server api object `api.X(...)` are requests). New: `ux-saf-2`, `ux-foc-2`, `ux-acc-11`, `ux-mot-3`, `ux-lst-4`, `ux-fbk-6`, `ux-acc-5`, `ux-inp-24`, `ux-lay-8`, `ux-mot-7`, `ux-inp-21`, `ux-txt-13`, `ux-lay-5` (SwWeb README "Rule families (round 4)"). A deliberate case names the rule: `/* SwWeb:allow ux-... (why) */`.

### C12. Outcome kinds, latest wins, scoped locks, the user's code edits, option schemas, contract tests and the SwWeb facts of round 5 - behaviour changes to check

- **Server** (C#): every 4xx refusal carries **`X-Sw-Outcome`** (`AlreadyDone` only when the code says so: `HttpResponseException.AlreadyDone(msg)`; `Gone` for 404 / 410; `Denied` for 401 / a role 403; `Refused` for any other 4xx: `.Refused(msg)`, `.Gone(msg)`, `IHttpRefusal.Outcome`); bad input is 400 / 415 with a reason, never a 500; job pushes (`sw.job`) carry the job to every session of the user; a `CancelJob` after the end sets `CancelRequested`. An api of your own that refuses with "already done" passes `AlreadyDone` explicitly, else the user reads "not done".
- **Outcome kinds** (FBK-1, FBK-5): `Sw.Net.Error.Outcome` / `Sw.Net.OutcomeKind(e)`; row actions say "not needed" (AlreadyDone), "no longer there" (Gone) or "not done" (anything else, also a 404 / 409 / 410 without the header), `sw-cell-result` has `Outcome` and `Final`. **Check** own code that compares `e.Status === 409` / `404` to say "already": use `OutcomeKind`.
- **Latest wins** (FBK-7, LST-7): **`Sw.Net.Latest(key).Read(url, arg)`** for search as you type (an older answer is never delivered; an older read is aborted, a write only loses its answer). **Check** `ctl.abort(); ctl = Sw.Cancel.Controller(); Sw.Net.Send(...)`: a write Send is never aborted, the older answer can win (SwWeb `ux-fbk-7`).
- **Locks and the user's edits** (FRM-8, INP-12, SAF-10): **`Sw.Lock(targets, work)`** / **`control.Locked(true, work)`** unlock on any outcome; a bare `Locked(true)` in a click's work is undone when the work fails (+ DevWarn): **check** `Locked(true)` … `Locked(false)` without `finally` (SwWeb `ux-frm-8`). A `Set()` in the synchronous part of a trusted click's work is the user's edit (a fill-in button such as "Same as billing" makes the card dirty); `Set(v, { User: false })` opts out.
- **`Sw.Work.Current(el)`** called from the element's own work returns null with a DevWarn (it would wait for itself); `{ Then: true }` keeps the old chaining; `Sw.Work.Running(el)` is the check without the self rule.
- **Option schemas** (every api): `Sw.DevOptions.Register(name, schema)` / `Sw.Work.RegisterOptions`; every core, ui, dialog and table api checks its options in developer mode (unknown names with "did you mean", wrong types; a Form field `{ Requred: true }` warns now); **`Sw.DevOptions.Walk()`** lists public apis that take options without a schema. An app's own api with options: register its schema the same way.
- **Contract tests**: `core/contracttest.html` (`?bundle` for the bundles) runs one or more checks per core README row, the walker and **`sw/readme_contracts.js`**, generated by **`SwWeb contracts <SysWeaver> --out srcWeb/sw/readme_contracts.js`** from every api row of core / ui / shell / explore README.md (the name exists, its schema accepts the documented options, the composers return their documented type). Regenerate the manifest after changing a README; `SwWeb contracts` alone checks the READMEs against the sources (exit code 3).
- **Dialogs** (POP-6, TXT-17, LAY-1): reads (`Sw.Net.Read`, `ReadOnly`, `Get`) get a real Cancel; labels never break inside a word (busy buttons shrink to "Stop waiting", then an icon); `ui/dialogtest.html` fails on any problem. **Toasts** are placed off every control, pop-up, registered overlay and bottom bar.
- **Focus** (FOC-1): when the element the user acted on is removed or hidden, the focus goes to the nearest result, heading or control (never BODY); a focus set by code that goes away is left alone.
- **Pages**: account request pages send the code when they open, as the original pages (a "Send the code" button only to retry after a failure); `Sw.AuthUi.Done(page, { Title, Text, Next })` ends a flow with a way on; Users: "Delete user…" with type to confirm, the server's consequences before a forced reset.
- **SwWeb** (Part C): 72 `ux-*` rules + `design-icon`; the families key on facts of the data flow **across the files of a web root**: a helper whose parameter reaches storage / a log / a url (any wrapper), a load whose catch only logs and returns an empty collection, request helpers (`M.f = function`, `const M = { f() {} }`, revealing modules). New: `ux-tip-1`, `ux-tip-7`, `ux-pop-11`, `ux-saf-6`, `ux-acc-2`, `ux-acc-4`, `ux-acc-7`, `ux-frm-2`, `ux-frm-8`, `design-icon` (`reload`, `add`, `delete` are no icons: `refresh`, `plus`, `trash`); wider: `ux-acc-3` (heading levels), `ux-frm-6` (e-mail / phone / url fields), `ux-saf-4` (delete-all without type to confirm), `ux-txt-30` (generic "Error", swapped `Message` arguments), `ux-fbk-2` / `-4` / `-5` / `-6` / `-7`, `ux-foc-2`, `ux-inp-6` / `-8` / `-30`, `ux-pop-6`, `ux-lst-4`, `ux-saf-1`. `SwWeb bundle` refuses a `let` / `const` / `class` declared twice in one block. Nothing breaks: run `SwWeb check --strict --design` and fix or allow (`/* SwWeb:allow ux-... (why) */`).

### C13. Parity with the original (parity rounds P1-P3, series 5) - the rule that wins

**Owner direction: the rewrite behaves and requests like the original; where a rule above (or the UX guide) and the original differ, the original wins** unless the owner approved the change (the redesign, confirmations for Kill / Reboot / delete, the `jp` removal, Problems, Activity, …). What that means for a port:

- **Escape keeps the typed text** in `Sw.TextBox` / `NumberBox` (the old inputs did); `EscapeReverts: true` opts into the revert. In a dialog Escape is the dialog's close attempt (it asks "Discard changes?" when the dialog has changes: owner decision).
- **No leave question by default** (SAF-10 / SAF-11 opt in): controls, `Sw.Form`, `Sw.Edit.FormFooter`, dialogs and restored drafts don't mark the page dirty; `Dirty: true` (or a sentence) opts in, `Sw.Page.SetDirty` stays explicit. Add it only where the old page asked (C4, C7).
- **A plain click copies** (`Sw.Clipboard.CopyOnClick`, table cells), `Keyboard: true` adds Enter / Space; the legacy `ValueFormat.copyOnClick` keeps the original contract exactly (C5).
- **Tables**: the filter row is on by default; an in-page table without a `Param` sends none (the old `addTable`), `explore/table.html` sends `Param: null`; the page size follows the original rule (a multiple of 5); search applies on Enter / leaving the field / Clear; a user change supersedes a running request without aborting it (its answer is ignored, as the original); a failure retries every 3 s; a link with the removed `jp` parameter shows a message instead of the table (explore/README.md).
- **Requests**: a stand-alone page that had no session in the original gets `data-sw-no-session` on `<html>` (core/README.md); while the server is down a live table's own retry is the only traffic (the connection probe waits while another request keeps failing); a refused row action logs with `console.log`, as the original.
- **Reloads**: `Sw.Page.Reload()` runs `Sw.Page.BeforeReload` first (the shell's touch Back guard).
- **Check** a port against the original with the parity gate `SysWeaver.Tests\RunParityTests.bat` (`SysWeaver.Tests/ParityTests/README.md`): it replays key flows on both sides (every app's shell and a table page, search / clear, a superseded request, sign in, sign out with 2 tabs, server down ...) and diffs the requests, aborts, page reloads and states; exit code 0 = no unapproved difference ([Verify](#verify)). A new difference is fixed, or the owner's decision goes into its `approved.txt`.

---

## Summary: what SwWeb does

| Rule | migrate | check |
|---|---|---|
| A0 framework-free page | theme + bundle includes, viewport, `sw-root`, CDN Chart.js -> framework copy (`design`, one run); port steps reported at their lines | `no-framework` (error with `--strict` in a `srcWeb`), then `ux-pop-9`, `ux-pop-1`, `design-own-ui`, `tr-untranslated`, `design-font` |
| A1 includes | yes (`api`, `includes`); `common/theme.css` added before `sw/ui.css` | `legacy-include`, `design-legacy-page`, `theme-include` |
| A2 `Open(url)` / `Navigate` targets | `Open(url)` -> `Sw.Page.Navigate(url, "_blank")` | `design-navigate` |
| A1 viewport (missing or blocking zoom), `sw-root`, old `Theme*Bg` classes | added / fixed (`design`); classes reported | `design-viewport`, `ux-inp-25`, `design-sw-root`, `design-legacy-class` |
| A2 `Sw.Page.Main` in an inline script | - | `script-order` (error) |
| A2-A4, A7 legacy globals with a pure / options mapping | yes (`api`) | `legacy-global`, `legacy-compat` |
| A5 `Confirm` | renamed; `Danger: true` for destructive calls, verb / "Cancel" labels; the rest reported | `design-confirm` (also the wording) |
| A5 native `confirm()` / `alert()` / `prompt()` (also indirect), own `<dialog>`, own `fetch` | reported (A0) | `ux-pop-9`, `ux-pop-1`, `design-own-ui` (fetch) |
| A5 `Sw.Dialog.Show` wording | - | `design-confirm` |
| A5, A6, A8 shape changes (menus, pop-ups, icons, window state) | reported | `legacy-global`, `legacy-compat` |
| A5 / A7 library includes | added (`api`) | `missing-include` (error) |
| A9 translation newlines | yes (`api`) | `tr-*` |
| A9 `_TF` with arguments, `_T` placeholders without one | - | `tr-format` (error) |
| A9 texts made by code without `_T`, aliases of `_T` | plain literal texts wrapped in `_T` (`design`) | `tr-untranslated`, `tr-alias` (error) |
| A10 contract | JSDoc headers | `file-name-*`, `folder-name-*`, `external-*`, `es-module`, `internal-id` ... |
| A11 helpers without a Sw counterpart | reported with the replacement | `legacy-compat`, `legacy-global` |
| A12, A13 times, access checks | - | - (manual) |
| B1 old token names | yes (`api`) | `design-token-alias` |
| B2 overrides of old names | reported (blocks the renames of that token) | `design-legacy-token` |
| B3 `--Theme*` in page css | one-answer variables renamed (`design`), role dependent ones reported | `design-legacy-token` |
| B4 hard-coded colours | - | `design-color` |
| B5 shadows | invalid `var(--sw-shadow)` shadows -> `none` (`design`) | `design-shadow`, `design-inline-style` |
| B6 gradients | - | `design-gradient`, `design-inline-style` |
| B7 radii / type | via B1 / B3 | `design-radius`, `design-legacy-token` |
| B7 token names that don't exist | - | `unknown-token` (closest name) |
| B7 own fonts, a removed focus ring | - | `design-font`, `a11y-focus` |
| B16 own palette / `prefers-color-scheme` | - | `design-own-ui` |
| B8 bare element rules | - | `element-css`, `body-overflow` |
| B10 page header / container | - | `design-page-header`, `design-page-container` |
| B11 pills, fieldsets, meters | - | `design-pill-facts`, `design-fieldset`, `design-meter-levels` |
| B12 components, editors | `Edit*` aliases; old hooks reported | `design-legacy-class`, `element-css` |
| B13 table structure | `Table.addTable` | `design-legacy-class`, `design-shadow` |
| B18 custom controls | - | `a11y-custom-control`, `a11y-name` |
| B19 inline styles | renames in strings | `design-inline-style` |
| B20 removed framework page classes | reported | `design-legacy-class` |
| C1-C3 UX guide rules (click actions, hover media, aria-disabled, durations, layout animations, media sizes, labels, verb buttons, tool tips, css words ...) | zoom lock removed (`design`) | `ux-*` (Part C) |
| C4-C8 usage patterns (OnClick without a returned promise, also through helpers, callbacks and timers; clicks on cards / rows, danger confirmations and default danger buttons, destructive calls without a question, own leave prompts and drafts, dirty keys never cleared, raw fields / contenteditable saved without tracking, own SetDirty next to tracking controls, location navigation, hand-made tables, busy dialogs without a way out, overlays removed in one frame, copied / drafted / announced secrets, "Saved" toasts and silent `false`, browser / duplicate shortcuts, ShouldReport misuse, own job loops, rewritten history entries) | - | `ux-fbk-2`, `ux-fbk-5`, `ux-fbk-9`, `ux-saf-12`, `ux-inp-2`, `ux-inp-23`, `ux-saf-1`, `ux-saf-3`, `ux-saf-10`, `ux-saf-11`, `ux-lst-3`, `ux-pop-6`, `ux-pop-7`, `ux-fbk-4`, `ux-fbk-7`, `ux-mot-1`, `ux-sec-1`, `ux-inp-31`; `bundle` verifies the sources |
| C10 rule families (secrets in storage / urls / logs, raw html of user data, numbers and dates by hand, concatenated sentences, raw error messages, clipped text, actions on press / hover / double click / drag only, silent failures, endless blinks, content not selectable, prepared danger confirmations, EditWhileBusy) | - | `ux-sec-1`, `ux-sec-2`, `ux-txt-20`, `ux-txt-6`, `ux-txt-30`, `ux-txt-17`, `ux-inp-1`, `ux-inp-6`, `ux-inp-8`, `ux-inp-20`, `ux-fbk-5`, `ux-fbk-4`, `ux-mot-6`, `ux-inp-30`, `ux-saf-4`, `ux-frm-5`, `tr-untranslated`; `ux-fbk-2` (no promise) is an error |
| C11 round 4 families (vague / weak confirmations, focus on a danger button, a change that navigates, smooth scrolling, a click on a cell that copies, focusable items without keys, lists without an empty state, components in the native DOM, unmasked secret fields, small targets, fixed page widths, faded content, links without href, untranslated attributes, live items at the top; silent failures by data flow; `Sw.Net.Message(...).Text`, `Read: true`, early draft clears, percent progress) | - | `ux-saf-2`, `ux-foc-2`, `ux-acc-11`, `ux-mot-3`, `ux-lst-4`, `ux-inp-20`, `ux-fbk-6`, `ux-acc-3`, `ux-acc-5`, `ux-sec-1`, `ux-inp-24`, `ux-lay-8`, `ux-mot-7`, `ux-inp-21`, `ux-txt-13`, `ux-lay-5`, `ux-fbk-5`, `ux-txt-30`, `ux-pop-6`, `ux-saf-11`, `ux-fbk-4` |
| C12 round 5 facts (a secret through a helper of any file, a load whose catch returns an empty collection, a list loaded through a helper without an empty state, focus on a danger button by any focus call, a double click whose work no click reaches, icon-only buttons without a tool tip, a hint per row, a dialog on load, "can't be undone" on a reversible action, delete-all without type to confirm, a status line without a live region, a timer that signs out, a required field not marked, colour alone, a lock without finally, latest wins with a write Send, a plain controller on a write, a click that awaits its own work, a job removed after Run, swapped Message arguments, heading levels that skip, e-mail fields of type text, an unknown icon name) | - | `ux-sec-1`, `ux-fbk-5`, `ux-fbk-6`, `ux-foc-2`, `ux-inp-6`, `ux-tip-1`, `ux-tip-7`, `ux-pop-11`, `ux-saf-6`, `ux-saf-4`, `ux-acc-4`, `ux-acc-7`, `ux-frm-2`, `ux-acc-2`, `ux-frm-8`, `ux-fbk-7`, `ux-pop-6`, `ux-fbk-2`, `ux-fbk-4`, `ux-txt-30`, `ux-acc-3`, `ux-frm-6`, `design-icon`; `SwWeb contracts` (README rows), `bundle` (double declarations) |

Not detectable statically (review by hand): a donut for one value (chart urls are built at run time), a command row at the bottom of a page, an h1 that differs from the menu name, px font sizes / paddings, child selectors into components other than the table frame, token names in access checks, NumberBox labels meant as accessible names. The structural changes (B10-B12) stay manual by nature; `check --design` finds the pages and css that still need them.

## Verify

- `SwWeb check <srcWeb> --strict --design`: 0 findings (or marked exceptions with a reason).
- Framework changes: `core/contracttest.html` (and `?bundle`) and `ui/dialogtest.html?bundle` pass with 0 problems.
- **Parity gate** (a change to any srcWeb, an upstream merge): `SysWeaver.Tests\RunParityTests.bat` = exit code 0. It runs the rewritten apps (8201-8211) against the original upstream apps (8301-8311) and fails on any difference in requests, aborts, page reloads, sign in / out or the page offset that is not an approved owner decision (`SysWeaver.Tests/ParityTests/README.md`, C13). It needs both app sets running, so it is not part of `dotnet test`.
- In the browser, in the shell and stand-alone: the five themes (light, dark, pro, application, hacker), desktop 2560x1440 / 1920x1080 / 1366x768, phone 390x844 and 844x390, keyboard only (Tab, Enter / Space, Escape, arrows), no console errors.
- Compare with the reference pages: the component gallery `ui/gallery.html` and the Design v2 prototype `ui/design.html` (`?theme=`, `?density=`, `?view=home|server|table|editor`).

## Relationships

- **Tool:** [SwWeb](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwWeb/README.md) (`migrate`, `check`, `mapping`, `rules`)
- **References:** [`core/README.md`](core/README.md) ("Times"), [`ui/README.md`](ui/README.md) ("Theming", "Page anatomy", "Dialog rules"), [`shell/README.md`](shell/README.md), [`edit/README.md`](../../SysWeaver.MicroService.Edit/srcWeb/edit/README.md), [`_docs/Web.md`](../../_docs/Web.md), [`_docs/WebBranding.md`](../../_docs/WebBranding.md), [`_docs/WebQuickStart.md`](../../_docs/WebQuickStart.md)
