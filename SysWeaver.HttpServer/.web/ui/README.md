# ui - the SysWeaver UI library (Sw)

A small, dependency free, touch first component library for SysWeaver pages: vanilla JavaScript classic scripts (no modules, no
frameworks), one namespace `Sw` shared with the UI-less core (`../core`, bundle `sw/core.js`). It is a port of the EffectWeaver `Ew`
library, rebranded and extended with everything the SysWeaver pages need (icon classes, toasts, loaders, the server block overlay,
menus with submenus, searchable selections, value cells ...).

* Touch, mouse and keyboard: pointer events, targets of at least `--sw-touch` (44 px on touch screens / the mobile layout, 34 px with a
  mouse), no hover-only features (tooltips also show on long press and keyboard focus), visible focus rings, Escape closes, arrow keys in
  lists / menus / tabs / sliders, Enter / Space activate, aria roles and labels.
* Desktop, mobile portrait and mobile landscape (responsive dialogs, toasts, menus that drill down on touch / narrow screens).
* Every SysWeaver theme (light, dark, hacker, application, pro, and custom themes: registered with `Sw.Theme.Register` or defined in `app/app.css`; light or dark by their declared scheme, `data-sw-scheme`).
* User visible texts go through the translation functions with literal arguments (contract rule 7).

## Files

| File | Bundle | Contents |
|---|---|---|
| `base.js` | sw/ui.js | `Sw.el`, icons (`Sw.Icon`, `Sw.Icons`, `Sw.IconClass`), input helpers (`Sw.KeyClick`, `Sw.LongPress`, `Sw.BadClick` ...), `Sw.Focus`, `Sw.Keys`, `Sw.Drag`, `Sw.Clipboard`, `Sw.Color`, smooth image / iframe loading |
| `controls.js` | sw/ui.js | `Button`, `ButtonRow`, `Group`, `Toggle`, `Slider`, `NumberBox`, `TextBox`, `Select`, `ColorPicker`, `Badge`, `Chip`, `ProgressBar`, `Spinner` |
| `overlay.js` | sw/ui.js | `Place`, `Track`, `Popup`, `Menu`, `Dialog`, `SearchSelection`, `Toast` / `Fail` / `Info` / `Message`, `Hint`, `Loader`, `ServerBlock`, `FullScreen` |
| `layout.js` | sw/ui.js | `Tabs`, `Split`, `Pager`, `Columns`, `PageHeader`, `EmptyState` |
| `page.js` | sw/ui.js (last) | the page anatomy: `PageLayout`, `MenuTitle`, `Card`, `Facts`, `Kpi`, `Meter`, `ListCard`, `Grid`, `SectionTitle`, `StateView` (section "Page anatomy") |
| `editors.js` | sw/ui.js | `PropertyEditor` |
| `cells.js` | sw/ui.js | `Sw.Cells` (value cell renderers for TableData formats), `Sw.Markdown` |
| `code.js` | (on demand) | `Sw.CodeEditor` (code editor), `Sw.CodeLanguages`, `Sw.GlslDocs` |
| `compat_common.js` | common/common.js (legacy bundle = core + ui + this) | Every global of the old `common/common.js` mapped onto Sw (old element based classes `ColorIcon`, `Button`, `ProgressBar`, `ValueFormat.createLink` keep their `icon-WxH` / `Sysweaver-Button` / `SysWeaver-*` elements styled by `common/theme.css`); sets the old cell class names on `Sw.Cells.Classes`; links `sw/ui.css` and `compat_common.css` on old pages. Not for new pages |
| `compat_common.css` | (linked by compat_common.js) | Old look of the Sw cell elements on old pages (links, tag pills) |
| `tokens.css`, `components.css`, `overlays.css`, `cells.css`, `code.css`, `icons.css`, `page.css` | sw/ui.css (in this order) | design tokens, component styles, SysWeaver icon classes, the page anatomy |
| `gallery.html`, `gallery.js` | - | the component gallery (every component in every state; testing and reviews) |

Load order: core first. Each js file is an IIFE (`"use strict"` inside) that works concatenated (bundle) and loaded one by one.

```html
<link rel="stylesheet" href="../common/theme.css">
<link rel="stylesheet" href="../sw/ui.css">
<script defer src="../sw/core.js"></script>
<script defer src="../sw/ui.js"></script>
<script defer src="page.js"></script>
<body class="sw-root">
```

```js
Sw.Page.Main(async () => {
    const s = new Sw.Slider({ Min: 0, Max: 1, Value: 0.5, OnChange: v => console.log(v) });
    document.body.append(s.E);
}, { Loading: true });
```

The code editor is not bundled: `await Sw.Net.IncludeJs(null, "../ui/code.js")` (its styles are in sw/ui.css).

## Conventions

* Components are classes constructed with one options object (PascalCase keys, all optional): `new Sw.Button({ Text: "Save" })`.
* The root element is `.E`; append it where you want it.
* Values: `Get()`, `Set(value, silent)` (`silent` = no callbacks). Callbacks: `OnInput(value)` while dragging / typing,
  `OnChange(value)` when committed (released, Enter, blur).
* Helpers that take elements also take components (`{E}`) where noted (`Sw.ButtonRow`, `Sw.Columns.Add`, `Sw.Group` Content).
* `Sw.Frame` is the core's frame messaging namespace, so the Ew `Frame` component is called `Sw.Group`.

## API conventions

The rules new code follows (the same as the core, see [core API conventions](../core/README.md#api-conventions)):

* **PascalCase** for everything public: namespaces, classes, functions, members, events of components (`Sw.Menu.Show`, `button.SetText`,
  `OnChange`) and the keys of options objects (`{ Text, Icon, Kind }`). **camelCase** for locals, parameters and private helpers inside
  a module IIFE. CSS classes are `sw-*` (kebab case), tokens `--sw-*`.
* **One options object** for anything with more than two or three optional parameters; positional parameters only for the
  essential ones (`Sw.Menu.Show(anchor, items, opts)`). Boolean flags go into the options object in new APIs.
* **Components**: `new Sw.X(options)`, root element `.E`, `Get()` / `Set(value, silent)`, `OnInput` / `OnChange`, `SetDisabled(bool)`;
  helpers that build a single element return it (`Sw.Badge`, `Sw.ButtonRow`, `Sw.IconClass`).
* **Results**: async APIs return promises (dialogs resolve to the chosen value, null when dismissed); "remove / close" handles are
  functions (`const close = Sw.Loader.Show(...)`).
* **User visible texts** are passed in by the caller (already translated) or come from the translation functions with literal
  arguments inside the library.
* **Accessible names** say what a control does and, when it repeats per item, which item ("Copy the url of the Fire effect page"),
  not the mouse action: tool tips may start with "Click to ...", names do not (`Label` option, `Sw.ActionName`). Controls with a
  visible word are named by that word. Clickable tiles are buttons (`role="button"` in a `role="group"`, not list items), things that
  open a page are real `<a href>` links (middle click / copy link work).
* **Legacy names are kept, never renamed away**: the old globals live in the compatibility files (`compat_common.js`,
  `../shell/compat_application.js`), which map them onto the new API (`Fail` → `Sw.Fail`, `PopUpMenu` → `Sw.Menu` ...), and the old
  member names that callers used stay on the new classes as thin aliases (ex: `AbortHandler.raise/addListener`, `Sw.Fullscreen`). A
  new PascalCase name may be added next to an existing name, the existing name then stays as an alias (documented, not removed).

### Known inconsistencies (kept for compatibility)

Not renamed (call sites in pages and other projects use them); listed so new code picks the preferred form:

| Inconsistency | Prefer |
|---|---|
| `Sw.el(tag, props)` is camelCase and its props are lower case (`class`, `text`, `html`, `attrs`, `on`, `dataset`), unlike the PascalCase options of components | keep using `Sw.el` (the most used helper); its props mirror DOM property names on purpose |
| `Sw.clamp`, `Sw.snap` (camelCase, from Ew) next to `Sw.Util.Clamp` (core) | `Sw.Util.Clamp`; `Sw.snap` has no core equivalent |
| Duplicate names: `Sw.FormatNumber` / `Sw.Format.Number`, `Sw.Debounce` / `Sw.Util.Debounce`, `Sw.Throttle` / `Sw.Util.Throttle` (the ui names are the core functions when the core is loaded) | `Sw.Format.Number`, `Sw.Util.Debounce`, `Sw.Util.Throttle` |
| Change notification in two ways: `Sw.Theme.OnChange(fn)` and the `Sw.Events` event `theme.changed` (same for layout: `Sw.Layout.OnChange` / `layout.changed`) | `OnChange` (returns an off function) |
| Toast family: `Sw.Toast(text, { Kind, Duration, NoCopy })` takes options, `Sw.Fail(text, duration, noCopy)`, `Sw.Info(...)` and `Sw.Message(text, duration, className, noCopy)` are positional (the old signatures; `duration` true = 24 hours) | `Sw.Fail(text)` / `Sw.Info(text)` for the defaults, `Sw.Toast(text, {...})` for anything else |
| `Sw.Dialog.Confirm(title, text, okText, { ... })` and `Sw.Dialog.Prompt(title, value, placeholder, { ... })` are half positional, `Sw.Dialog.Show({ ... })` takes options only | as documented; `Show` for custom dialogs |
| Boolean positional parameters: `Set(value, silent)`, `Sw.Session.Subscribe(type, fn, noServer)`, `Sw.Format.Uptime(date, blankIfNeg, invert, now)`, `Sw.Focus.Set(el, scroll)` | name the argument in a comment at the call site |
| Result shapes differ: `Sw.Format.TimeStamp(...)` returns `[tooltip, text]`, `Sw.Format.Value(...)` returns `{ Kind, Text, Title }` | as documented |
| `Sw.FullScreen` (ui) and the alias `Sw.Fullscreen` (Ew spelling); the legacy `FullScreen` class in `compat_common.js` is a separate wrapper of it | `Sw.FullScreen` |
| `Sw.Cells` renderers keep the old positional signature `(el, value, formats, nextValue, flash, type, onRefresh)` | `Sw.Cells.Update(el, type, value, flash, format, nextValue, onRefresh)` |
| `Sw.el({ html })` sets `innerHTML`: never pass server or user data to it | `text` (or `Sw.Markdown.Set` for user markdown) |
| There is no PascalCase `Sw.El` alias, and nothing is marked `@deprecated`: the legacy globals and aliases are documented as legacy (compatibility), not scheduled for removal | the names in this README |
| The bundles (`sw/core.js`, `sw/ui.js`, `sw/shell.js`) are not minified (readable, with JSDoc; the server compresses the responses) | - |

## Theming

The four base colors come from the SysWeaver theme (`common/theme.css`, server templated):

| Token | From |
|---|---|
| `--sw-bg` | `--ThemeBackground` |
| `--sw-fg` | `--ThemeMain` |
| `--sw-accent1` | `--ThemeAcc1` |
| `--sw-accent2` | `--ThemeAcc2` |

`Sw.Theme` (core) sets `data-theme` on `<html>` (no attribute = light) and `data-sw-scheme` (`light` / `dark`, the scheme the theme declares; dark, hacker, application and pro are dark
schemes) and writes custom themes (`Sw.Theme.Register({Name, Scheme, Colors: {Background, Foreground, Accent1, Accent2}})`) as inline
base colors, so all derived tokens follow. Derived (color-mix): `--sw-bg-1/2/3` (raised surfaces), `--sw-bg-inset`, `--sw-line`,
`--sw-line-strong`, `--sw-muted`, `--sw-faint`, `--sw-accent1-soft`, `--sw-accent2-soft`, `--sw-accent1-fg`, `--sw-on-accent`,
`--sw-danger(-soft)`, `--sw-ok(-soft)`, `--sw-warning(-soft)`, `--sw-shadow`, `--sw-backdrop`.
`--sw-focus` (every focus ring) is a fixed blue per scheme, never the accent2 / danger red: light `#1a66d6`, dark schemes (dark,
application) `#6cb6ff`, hacker `#3fd2ff`, pro `#7cc4ff` (contrast >= 4.5:1 on the theme background); a theme may set `--ThemeFocus`.
Fonts (the local brand fonts, see Brand below, then system fonts): `--sw-sans` (= `--ThemeFont`), `--sw-mono` (= `--ThemeFontMono`), `--sw-head` (= `--ThemeFontHead`),
`--sw-serif`; sizes `--sw-size`, `--sw-mono-size`. Sizing: `--sw-touch`, `--sw-radius`, `--sw-radius-lg`, `--sw-gap`, `--sw-speed`. Large high resolution screens (`(min-device-width: 2200px) and (min-width: 1400px), (min-width: 2200px)`, and a second step at 3200 px) scale the size tokens, the html font size and `--ThemeIconSize` (desktop 1920 is unchanged); `--sw-scale` (1, 1.2, 1.45) tells pages the step for their own px sizes (`calc(20px * var(--sw-scale))`).
Overriding any token in page css (or on an element) restyles the components below it. `--sw-frame-bg` sets the background behind
`Sw.Group` titles.

Helper classes: `sw-root` (font, color, box sizing), `sw-page` (opaque background), `sw-mono`, `sw-serif`, `sw-head`, `sw-muted`,
`sw-faint`, `sw-noselect`, `sw-selectable`, `sw-scroll`, `sw-row` (`sw-start`, `sw-end`), `sw-stack`, `sw-sr-only`.

### Brand

The default light and dark themes are the SysWeaver brand (light: Mist 50 / Slate 900 / Copper 700 / Steel 600; dark: Slate 900 /
Mist 50 / Copper 500 / Steel 300). Text on accent fills is the theme background (`--sw-on-accent`), text on danger fills
`--sw-on-danger`. Fonts: Barlow (`--ThemeFont`, `--sw-sans`) and Saira Semi Condensed SemiBold (`--ThemeFontHead`, `--sw-head`: the
headings `h1`..`h4` of `sw-root` pages, `sw-head`, the shell title), from `../brand/fonts.css` (local data urls, imported by
`common/theme.css`); pro and hacker keep their own font. Marks (files in `../brand/`, the version for the scheme):
`--sw-brand-icon` (the SW mark, 48 px and up), `--sw-brand-logo` (mark and wordmark, at least 140 px wide), `--sw-brand-stacked`,
`--sw-brand-small` (the small master below 48 px); classes `sw-brand-mark`, `sw-brand-logo`, `sw-brand-stacked` show them as a
background (give the element a size). The weave: `sw-weave` (background only) with a solid `sw-weave-panel` for the content. These
variables have zero specificity, so an app overrides them in `app/app.css` (`url("../app/x.svg")`), see
[`_docs/WebBranding.md`](../../../_docs/WebBranding.md). The gallery's Brand card shows them all.

Chart series palette: `--sw-chart-1` .. `--sw-chart-7` (zero specificity). Slot 1 is `--sw-accent1` (copper), the steel slot
`--sw-accent2` (slot 7 in light, slot 6 in dark), the others are the brand's extra hues per scheme (light: blue `#2A78D6`, green
`#138A60`, violet `#6250D6`, magenta `#C4507E`, ochre `#A87400`; dark: `#3987E5`, `#199E70`, `#9085E9`, `#C98500`, `#D55181`),
validated as a set (adjacent pairs: CVD separation >= 11, normal vision >= 18 OKLab dE; >= 3:1 against the brand backgrounds).
Status colors are never series colors. Used by the chart viewer's default "Theme colors" mode (chart/chart.js).

User pictures: `Sw.UserPicture` (base.js) shows server generated user pictures (svg) as a brand monogram, see the helpers below.

## Helpers (base.js)

| Function | Description |
|---|---|
| `Sw.el(tag, props, ...children)` | Create an element. `tag` may have classes (`"div.a.b"`). Props: `class, text, html, title, attrs, style, dataset, on: {event: fn}` or any element property |
| `Sw.Icon(name, cls)` | An inline svg icon (24×24, stroked; `Sw.Icons.List()` names), or for icon classes / urls a mask icon (`Sw.IconClass`) |
| `Sw.Icons.Add(name, svgInner)`, `Has(name)`, `List()` | Register / query inline icons |
| `Sw.IconClass(image, {Size, Color, Title, Class})` | Mask icon from a SysWeaver icon class (`"IconReload"`) or an url (`"../icons/x.svg"`; a trailing `*` = full color image). Drawn in the text color, or `Color` (css color or an old `IconColorTheme*` name) |
| `Sw.SetIconClass(el, image)` | Change the image of a mask icon |
| `Sw.IsPureEvent(ev)`, `Sw.BadClick(ev, keepProp)` | No modifier keys / the old click guard |
| `Sw.KeyClick(el, onClick)`, `Sw.RemoveKeyClick(el)` | Make an element keyboard clickable (Enter, Space) |
| `Sw.ActionName(title)` | An accessible name from a tool tip: drops a leading "Click to " / "Click ", a trailing period and lines after a blank line ("Click to sort by Name." → "Sort by Name"); other languages pass unchanged. Used for icon only buttons, sort buttons, table image alt texts |
| `Sw.LongPress(el, fn, ms)` | Touch and hold, right click, or the context menu key / Shift+F10 call `fn(event)`; returns a remover |
| `Sw.NoContextMenu()` | No browser context menu outside text inputs (needed for long press on touch screens); returns a remover |
| `Sw.HasSelectionIn(el)` | The user selected text inside the element (a click is then not an action) |
| `Sw.Focus.Trap(el)` / `Release(el)` | Keep the focus inside an element (stacked; restores the focus on release). A trap never moves the focus itself, it only reacts to focus / Tab events of its own document |
| `Sw.Focus.CanTake()` / `Set(el, scroll)` | May this document move the focus (top window, or a frame whose document has the focus)? `Set` focuses only then. Overlays (loader, dialogs, popups, server block) use it, so an embedded page never steals the focus from its parent page |
| `Sw.Focus.Tabbables(root)`, `IsTabbable(el)`, `Next(el)`, `Prev(el)`, `NextOnEnter(input, fn)`, `Sentinel(edge)` | Focus helpers |
| `Sw.Keys.Add(combo, fn, {Title, InInputs, Global})` | Keyboard shortcut (`"Mod+K"` = Ctrl, or Cmd on Apple); `Sw.Keys.List()` / `Format(combo)` for help screens |
| `Sw.Drag(el, {OnStart, OnMove, OnEnd, OnTap, Threshold, Button, Touch})` | Pointer drag helper with capture |
| `Sw.Clipboard.Copy(value, {Toast, Generic})` | Copy (works on plain http too); an info / fail toast |
| `Sw.Clipboard.CopyOnClick(el, value, {Keyboard, AddTitle})` | Click copies a value (or the text); returns the hint text |
| `Sw.Clipboard.Read()` | Read text |
| `Sw.Color` | `HsvToRgb`, `RgbToHsv`, `ToHex`, `FromHex` (0..1 floats), `ParseCss(css)` → `[r, g, b, a]`, `Luma(r, g, b)` |
| `Sw.FadeSrc(el, {ShowError, FadeIn, FadeDelay, Channel, Parent, UseEvent})`, `Sw.UnfadeSrc(el)` | Smooth image / iframe loading: hidden while loading, faded in; attributes `data-sw-fade`, `data-sw-fadein`, `data-sw-fadedelay`, `data-sw-fadechannel="name,ms"`, `data-sw-fadeparent`, `data-sw-useevent` work automatically |
| `Sw.CreateImg(o)`, `Sw.CreateIFrame(o)` | Create a smoothly loading img / iframe (old positional arguments accepted) |
| `Sw.SetImageSource(img, src, {Fallback, FlushCache, OnLoaded, ReloadOn})` | Set / reload an image source (fallback url, cache flush, reload on a server message) |
| `new Sw.PendingImages()` | `Start(img, src)`, `await WaitAll()`, `Count`, `InProgress`, `Fails` |
| `Sw.UserPicture.Watch(img, name)` | Every picture the user image loads is checked (`IsGenerated(url)`: the server answers an svg for a generated picture, a png for an uploaded one); a generated one is replaced by `Monogram(name)` (initials on a brand color picked from the name, `Colors`: `[background, text]` pairs, all >= 4.5:1). An app palette set in `app/app.js` (`Sw.UserPicture.Colors = [...]`, runs in the shell) reaches every page like registered themes: `GetColors()` uses this page's palette, else the shell window's, else the one the shell remembered (localStorage `SysWeaver.UserPicture.Colors`, per site) for stand-alone pages. Hidden while checked, then class `sw-picture-checked`; the real url stays in `img.SwPictureSrc`. Used by the shell user button and Home |
| `Sw.clamp`, `Sw.snap`, `Sw.FormatNumber`, `Sw.Debounce`, `Sw.Throttle` | Small helpers (Debounce / Throttle are the core's `Sw.Util` ones when present) |

## Components

### Button
`new Sw.Button({ Text, Icon, Title, Label, Kind: "primary" | "accent" | "ghost" | "danger", Small, Block, Pressable, Pressed, AutoToggle, Repeat, Disabled, Working, IconRight, Type, Class, OnClick(button, event) })`
* Accessible name: `Label`; else the visible `Text` (the `Title` stays the tool tip, read as the description); else (icon only, or a text without letters / digits) the `Title` as an action (`Sw.ActionName`); else the icon name. Give buttons that repeat per item a `Label` with the item ("Copy the url of the Fire effect page"); `SetLabel(text)` changes it.
* `Icon`: an inline icon name, an icon class (`"IconReload"`) or an url.
* `OnClick` returning a promise makes the button busy until it settles (clicks ignored, a spinner after 250 ms, a rejection shows a fail toast; a refused request, 403, shows a toast naming the role needed and disables the button: `Sw.Access.ActionDenied`).
* `Pressable`: a toggle button (`aria-pressed`, flips on click unless `AutoToggle: false`). `Repeat`: repeats `OnClick` while held.
* Icon only buttons with a `Title`: the title also shows on long press and keyboard focus (`Sw.Hint`).
* Methods: `SetContent(text, icon)`, `SetText`, `SetIcon`, `SetTitle`, `SetLabel`, `SetPressed`, `SetDisabled`, `SetWorking`, `SetKind`, `Click()`.

`Sw.ButtonRow(...items)`: a wrapping, centered row (`sw-row`).

### Group
`new Sw.Group({ Title, Class, Content })`: a border with the title in it (like a fieldset). Append to `.Body`, `SetTitle(text)`.

### Toggle (switch)
`new Sw.Toggle({ Label, Value, Title, Disabled, OnChange(bool) })`, `SetLabel`, `SetDisabled`. Space / Enter flip it.

### Slider
`new Sw.Slider({ Min, Max, Step, Value, Default, Log, Vertical, Fader, Center, Accent2, Title, Label, Disabled, OnStart, OnInput, OnChange, OnEnd })`
* Dragging the thumb is relative (no jump), tapping the track jumps there. Double tap resets to `Default`.
* `Log`: logarithmic (`Min` > 0), snaps to 2 significant digits and to `Center`. `Center` draws a mark. `Vertical` fills the container's height.
* Keyboard: arrows (Shift = ×10), PageUp / PageDown, Home / End. `Set` is ignored while the user drags (pass `force`). `SetRange`, `SetDisabled`, `.Dragging`.

### NumberBox
`new Sw.NumberBox({ Min, Max, Step, Value, Width, Label, Title, Disabled, Format(v), OnChange })`: commits on Enter / blur, arrows step, Escape reverts.

### TextBox
`new Sw.TextBox({ Value, Placeholder, Label, Title, Type, Multiline, Rows, Clear, Reveal, Icon, MaxLength, AutoComplete, InputMode, SpellCheck, Width, Disabled, ReadOnly, Mono, Validate(v), OnInput, OnChange, OnEnter })`
* `Clear`: a clear button, `Reveal`: show / hide a password, `Icon`: a leading icon, `Validate`: an error text or null (checked on change).
* Escape reverts uncommitted typing (only then the event is stopped, so Escape in an unchanged box still closes a dialog).
* `.Input` is the input element. Methods: `Get`, `Set`, `Commit`, `Focus(select)`, `SetError(text)`, `SetDisabled`.

### Select
`new Sw.Select({ Options: [{ Value, Label, Icon, Hint, Disabled }], Value, Placeholder, Small, Title, Width, Search, SearchFrom, Disabled, OnChange })`
A button that opens a list: a menu for short lists, a filter box + list for long ones (`SearchFrom`, default 12; or `Search: true`).
Keyboard: Enter / Space / Down open; in the list arrows, Page Up / Down, Ctrl+Home / End, Enter, typing filters. `SetOptions(options, value)`, `Get`, `Set`, `SetDisabled`, `Open()`.
`Sw.ListPicker(anchor, options, value, onPick)` shows the searchable list alone.

### ColorPicker
`new Sw.ColorPicker({ Value: [r, g, b(, a)] | "#rrggbb", Hex, Title, Disabled, OnInput, OnChange })`: a swatch; the popup has a
saturation / value square, a hue bar (both keyboard operable) and a hex field. HDR colors (components > 1) get an intensity slider.

### Badge, Chip
`Sw.Badge(text, kind, title)` (`kind`: ok, error, warning, accent, accent2) → span.
`new Sw.Chip({ Text, Icon, Title, Pressed, Pressable, Removable, OnChange, OnRemove, OnClick })`: a rounded toggle; removable chips have an × (Delete / Backspace too).

### ProgressBar, Spinner
`new Sw.ProgressBar({ Value, Text, Title, Indeterminate, Small, Label })`: `SetValue(current, max, text, title)` (templates `{0}` percent,
`{1}` current, `{2}` max), `SetPercentage(value, text, title)`, `SetIndeterminate(bool)`. The text stays readable over the filled part.
`new Sw.Spinner({ Size, Title, Class })`.

### Popup
`Sw.Popup.Show(anchor, content, { Side, Align, Gap, Dim, Class, MinWidth, Modal, NoEscape, NoLayer, NoTrap, Focus, Role, Label, OnClose })`
Shows content next to an element (or `{X, Y}`, or null = centered), keeps it on screen (flips sides), re-places on resize, traps the focus.
Tapping outside / Escape closes it (unless Modal / NoEscape). Returns `{ Box, Close(), Place() }`. `Sw.Popup.CloseAll()`, `Sw.Popup.IsOpen()`.
`Sw.Place(box, anchor, opts)` positions any fixed element; `Sw.Track(anchorOrMouseEvent, box, opts)` keeps it there while scrolling (returns stop).

### Menu
`Sw.Menu.Show(anchor, items, { Side, Align, MinWidth, NoFocus, Focus, DrillDown, Label, OnClose })`
Items: `{ Text, Icon, Hint, Title, Desc, Checked, Radio, Disabled, Danger, Keep, Class, OnClick(item, event), Items }` (`Desc`: a muted second line under the text), `"-"` (separator),
`{ Title }` (heading). `Items` (a submenu) may be an array or a function returning an array / a promise of one.
* Submenus cascade to the side on wide screens with a mouse (hover opens them) and drill down (a back row) on touch / narrow screens.
* Keyboard: Up / Down / Home / End, type ahead, Enter / Space, Right opens a submenu, Left / Backspace / Escape go back, Tab closes.
* `Checked: true/false` makes a check item (`Radio: true` a radio item); `Keep` items stay open (check items flip in place).
`Sw.Menu.Context(element, itemsOrFn, opts)`: a context menu on long press, right click, the context menu key and Shift+F10.

### Dialog
* `await Sw.Dialog.Show({ Title, Icon, Content, Buttons: [{ Text, Kind, Icon, Title, Value, Close, OnClick(close, value) }], Wide, Full, Close, Modal, NoEscape, EnterValue, Focus, Class, Role, Describe, OnShow(close, box, body), OnClose(value) })`
  resolves to the button's `Value` (null when dismissed). `Content` may be a node, a text, or a function `(body, close, tools)` returning a
  node or a promise of one (`tools`: the header area for extra buttons). A button `OnClick` returning false keeps the dialog open;
  an `EnterValue` function returning undefined keeps it open too.
* Accessible name and description: `aria-labelledby` is the title, `aria-describedby` the message: by default the whole body of an
  `alertdialog` (Confirm, Alert, the table action confirmation), else the first `.sw-dialog-text` of the body (Prompt's `Text`).
  `Describe` overrides it (an element or a selector inside the body; `false`: no description, ex: a body with a long list).
  The server unavailable overlay is labelled by its title and described by its text the same way.
* Sizes and colors: a dialog is 500 px wide (`Wide`: 1100 px) times `--sw-scale` (it grows with the text on large screens), at most the window width minus 32 px; the title (`h2.sw-dialog-title`) is in the normal text color (`--sw-fg`), not the theme's heading accent.
* `Sw.Dialog.Open(options)` → `{ Box, Body, Tools, Close(value), Result }` (close it from code).
* `await Sw.Dialog.Confirm(title, text, okText, { CancelText, Danger, OkIcon, CancelIcon, OkTitle, CancelTitle })` → bool. With `Danger` (a danger OK button and a warning icon) Cancel has the focus and Enter never confirms (Enter on Cancel cancels), as in `Sw.Cells.ConfirmDanger`.
* `await Sw.Dialog.Prompt(title, value, placeholder, { Text, Label, Multiline, Type, Validate, OkText, CancelText, Mono })` → text or null.
* `await Sw.Dialog.Alert(title, text, okText)`.
* `await Sw.Dialog.Select({ Title, Tip, Placeholder, StartText, GetAll, Items(filter, close) })` → the picked value (items: elements or `{Text, Hint, Icon, Value, Title}`).
* `const close = Sw.Dialog.Working({ Title, Text, Icon })`: a busy dialog the user can't close.
* `new Sw.SearchSelection({ Id, GetId(data), Render(data, select, isRecent), Find(filter, recentMap, count, offset), MaxRecent })`, `await sel.Show({ Title, Tip, Placeholder, PageSize, OnSelect })`:
  a paged searchable selection that remembers the filter and the recently selected items (local storage `SysWeaver.PopUpRecent.<Id>`).
On narrow screens dialogs use the full width, on low screens (landscape phones) the head and foot are compact.

### Toasts
`Sw.Toast(text, { Kind: "error" | "ok" | "info" | "warning", Duration, NoCopy, Title })`, `Sw.Fail(text, duration, noCopy)`,
`Sw.Info(text, duration, noCopy)`, `Sw.Message(text, duration, className, noCopy)`. Toasts stack at the bottom (full width on phones),
lifted above a bar at the bottom of the window so they never cover a pager / footer (`footer`, `[role=toolbar]`, `[role=navigation]`,
`nav`, `.sw-pager` or `[data-sw-toastavoid]` touching the bottom edge; measured when a toast shows and on resize, css variable
`--sw-toast-lift` on `.sw-toasts`),
identical messages are merged with a counter, hovering pauses the timer, a click (or Enter) copies the text and removes the toast.
`duration` true = 24 hours.

### Hint
`Sw.Hint.Attach(element, text)`: the title (or text) shows on long press (touch / pen) and on keyboard focus. `Sw.Hint.Show(element, text, ms)`, `Sw.Hint.Hide()`.

### Loader
`const remove = Sw.Loader.Show({ Target, Text, Opaque, Icon, Delay })`: a loading overlay that blocks input and the focus (the whole page,
or only `Target`). The core's `Sw.Page.Loading(...)` / `Sw.Page.Main(fn, {Loading})` show it automatically (`noloader` url flag: none).

### ServerBlock
`Sw.ServerBlock.Show("server.pause" | "server.restart" | "server.shutdown" | "server.error" | {Type, Title, Text, Icon, Time})`, `Hide()`, `IsShown()`.
A full page overlay with the state, a live elapsed time and a reload button; page input is blocked. Shown automatically on the core's
`server.block` / `server.unblock` events (top windows).

### FullScreen
`Sw.FullScreen.IsFull()` (also an installed app running standalone), `Is()`, `await Enter(element)`, `await Exit()`, `await Toggle(element)`,
`OnChange(fn)`, `OnTouch(element, {AnyPointer})`, `OnCorner(element, {Corner, Size, Area, OnToggle})`. Alias `Sw.Fullscreen`.

### Tabs
`new Sw.Tabs({ Closable, OnSelect(tab, previous), OnClose(tab), OnAdd(), AddTitle, Small, Label, Fill, Class })`
`Add({ Id, Title, Icon, Tip, Content, Closable, Data, Select })` returns `{ Id, Head, Content, Data, SetTitle, SetTip, SetDirty, SetBadge(text, kind) }`.
`Select`, `Close` (asks `OnClose`, false keeps the tab), `Remove`, `Next(dir)`, `Get`, `All`, `Active`, `Tools`.
Keyboard: Left / Right / Home / End move and select, Delete closes. Middle click closes. `Fill: false` lets the body grow with the content.

### Split
`new Sw.Split({ A, B, Vertical, Size, Min, Key, Label, OnResize })`: two panes and a bar (drag, double click / double tap resets; keyboard:
arrows, Shift = bigger steps, Home / End, Enter resets). `SetSize(fraction)`, `Collapse(null | "A" | "B")`.

### Pager
`new Sw.Pager({ OnChange(pager), Swipe })`: a strip of equally wide items in `.Content`, moved by whole pages with `Next()` / `Prev()` or
swipes; keyboard focus on a hidden item pages to it. `CanPrev()`, `CanNext()`, `Reveal(item)`, `Go(offset)`, `CancelSwipe()`.

### Columns
`new Sw.Columns({ MinWidth, Gap, Max, Class, Content })`, `Add(...items)`: as many columns of at least `MinWidth` as fit (one on phones).

### PropertyEditor
Renders setting definitions (the C# `SettingDef` shape) as a property grid:
```js
const ed = new Sw.PropertyEditor({
    Defs: [
        { Key: "Speed", Label: "Speed", Type: "float", Min: 0.05, Max: 8, Log: true, Default: 1, Unit: "×", Group: "Clip" },
        { Key: "Tint", Label: "Tint", Type: "color", Default: [1, 0.5, 0.2], Desc: "Tooltip of the label" },
        { Key: "Mode", Type: "enum", Default: 0, Options: [{ Value: 0, Label: "Fast" }, { Value: 1, Label: "Nice" }] },
    ],
    Values: { Speed: 2 },
    OnInput: (key, value) => {},
    OnChange: (key, value) => {},
});
ed.SetValues({ Speed: 1.5 });
```
Types: `float`, `int`, `bool`, `enum`, `color`, `color4`, `vec2`, `vec3`, `vec4` (`Names`, `Descs`), `string`. `NoSlider`, `ResetButtons`,
`SetEnabled(keyOrFn, enabled)`, `Get(key)`. Modified values get an accent label; long press / right click on a label resets it.
On narrow screens labels are shown above their values.

### Page anatomy (page.js, page.css; Design v2)

Every page of every app has the same anatomy (`43_DesignV2.md` 2.5, 3.9, 3.17): the shell's top bar (breadcrumb) -> a **page header**
(title, one line description, status, actions) -> the content in a page container (dashboards: facts strip, KPI tiles, cards, lists;
fill pages: one table / log / chat that takes the height). Build pages from these shared parts instead of page specific pills, donuts,
fieldsets or boxes, so a restyle of the framework reaches every app. All parts are flat (surface + 1 px hairline, radius 8, no shadows,
no gradients) and follow the 5 themes and the densities through the tokens.

| Part | JS (callable with or without `new`) | Markup / classes (static pages can write them by hand) |
|---|---|---|
| Page skeleton | `Sw.PageLayout({ Type: "dash" \| "form" \| "read" \| "fill", Title, Description, Status, Actions, Compact, MenuTitle, Main, Parent, Class, Header })` -> `{ E, Header, Add(...items) }` | `main.sw-page-dash` (max 100rem), `.sw-page-form` (75rem), `.sw-page-read` (45rem), `.sw-page-fill` (full width + height; its `.sw-fill` child takes the rest); the container pads the page (`--sw-page-px`), inside the shell `html.Embedded` adds no padding then |
| Page header | `Sw.PageHeader({ Title, Description, Status, Actions, Compact, Level, Class, MenuTitle, Overflow })` -> `{ E, TitleE, DescE, StatusE, ActionsE, MoreB, FromMenu, SetTitle, SetDescription, SetStatus, SetActions, AddAction, FitActions }` (ui/layout.js) | `header.sw-ph > div.sw-ph-text > (div.sw-ph-titlerow > h1.sw-ph-title + span.sw-ph-status) + p.sw-ph-desc`, then `div.sw-ph-actions`; `.sw-ph-compact` on fill pages (hidden on phones: the top bar shows the title). **Overflow** (F49-Left, default on): the actions stay on one line, beside the text on wide headers (the text keeps 20rem), on their own line on narrow ones (< 760 px); the last actions that do not fit move into a "More actions" kebab menu (`button.sw-ph-more`, items with the action's text, icon, tool tip, disabled, danger; a click clicks the hidden button, so confirmations stay); the primary action and non button controls never move; refits on resize and on action changes (`.sw-ph-fit`, `.sw-ph-overflowed`). `Overflow: false` keeps the old wrapping row |
| Menu title | `Sw.MenuTitle()` -> the name of this page's main menu item when shown in the app shell, else null | A level 1 `Sw.PageHeader` uses it by default (and sets the document title to it), so the h1, the breadcrumb and the menu say the same ("Process information", not "Process info table"). `MenuTitle: false` keeps `Title` (a page about one record) |
| Grid | `Sw.Grid({ Content, Class, Label, Tag })` -> `{ E, Add }` | `div.sw-grid` (12 columns, 16 px gap), children `.sw-span-3/4/6/8/12` (3 -> 6 below 1280 px; 6 -> 12 below 900 px; KPI tiles 2 per row on phones) |
| Card | `Sw.Card({ Title, Sub, Count, Tools, Body, Flush, Span, Tag, Level, Class, Label })` -> `{ E, HeadE, TitleE, SubE, CountE, ToolsE, BodyE, SetTitle, SetSub, SetCount, SetTools, SetBody }` | `section.sw-card > div.sw-card-head (h2.sw-card-title, span.sw-card-sub, .sw-card-tools or .sw-push) + div.sw-card-body` (`.sw-flush`: no padding) |
| Facts strip | `Sw.Facts({ Items: [{ Key, Label, Value, Title, Href, OnClick, Copy, Mono }], Card, Label })` -> `{ E, Add(item) -> { E, ValueE, Set, SetTitle }, Get(key), Set(key, value) }` | `dl.sw-facts.sw-card > div.sw-fact > dt + dd` (a value that opens a page: `button.sw-fact-value`); replaces rows of fact pills |
| KPI tile | `Sw.Kpi({ Label, Icon, Value, Unit, Sub, Meter: true \| { Max, Warn, Danger }, Span, Href, OnClick, Title })` -> `{ E, ValueE, UnitE, SubE, Meter, Set({ Value, Unit, Sub, MeterValue, Max }), Level() }` | `div.sw-card.sw-kpi` (or `button` when it opens a page) `> .sw-kpi-label (icon + span), .sw-kpi-value (number + small unit), .sw-meter > i, .sw-kpi-sub`; replaces donuts / gauges for one value of a total |
| Meter | `Sw.Meter({ Label, Value, Max, Warn, Danger })` -> `{ E, Set(v, max), Level }` | `div.sw-meter[role=meter] > i` (`.is-warn`, `.is-danger`; show the level in text too, ex: a "High" badge). `Warn` (default 0.8) / `Danger` (default 0.92): a value of 0..1 is a fraction of `Max`, a value above 1 an absolute value on the meter scale (`{ Max: 100, Warn: 70, Danger: 90 }`, or 30 GB of a 64 GB drive) |
| List card | `Sw.ListCard({ ...Card options, Items: [{ Key, Text, Icon, Status: { Text, Kind, Dot }, Meta, Title, Href, OnClick }], Empty: { Title, Text, Icon, Actions }, Count })` -> card + `{ ListE, SetItems(items) }` | `section.sw-card > .sw-card-body.sw-flush > ul.sw-rows > li (badge, icon, span.sw-rows-text, span.sw-rows-meta)`; a row that opens something is `li > button.sw-rows-link` |
| Section title | `Sw.SectionTitle(title, sub)` | `div.sw-section-title > h2 + span`; `.sw-section + .sw-section` are 32 px apart |
| Tiles | (markup) | `div.sw-tiles > button.sw-tile > span.sw-tile-ico + span.sw-tile-text (b + span)` (shortcuts) |
| Empty / loading / error | `Sw.StateView({ Kind: "empty" \| "loading" \| "error", Icon, Title, Text, Actions, Details, Small, Lines })` -> `{ E, Set }`; `Sw.EmptyState({ Icon, Title, Text, Actions, Error })` (ui/layout.js) | `div.sw-state.is-<kind>` (`.is-small` in cards and lists; skeleton lines `.sw-skel`), `div.sw-empty` (`.sw-error`) |

Rules (checked by `SwWeb check`, see `49_PortingNotes.md` "Pages"):
- One page header per page, first in the page container; at most one primary action, destructive actions as `Kind: "danger"` (outline,
  confirmed by a dialog). Pages that show no header (legacy) still get the title in the top bar.
- Facts are a facts strip, not pills (pills / badges are for status and filters). A single value of a total is a KPI tile with a meter,
  not a donut. Time series are line charts (`chart.html?type=line&title=false` inside a card whose head names the series and the range).
- No `box-shadow` or gradients in page css (only floating layers: menus, popovers, dialogs, toasts use `--sw-elev-1/2`); radii from the
  tokens (`--sw-radius-sm/-/-md/-lg/-pill`), spacing from the 4 px scale (`--sw-space-*`).

```js
Sw.Page.Main(async () => {
    const page = Sw.PageLayout({ Type: "dash", Title: "Server", Description: "Live resource use.", Actions: [new Sw.Button({ Text: "Reboot...", Icon: "power", Kind: "danger", OnClick: reboot })] });
    const facts = Sw.Facts({ Items: [{ Label: "Host", Value: info.Machine }, { Label: "Processes", Value: n, Href: "../explore/table.html?q=..." }] });
    const cpu = Sw.Kpi({ Label: "CPU", Icon: "cpu", Unit: "%", Meter: true });
    const list = Sw.ListCard({ Title: "Services", Count: true, Span: 12, Empty: { Title: "No services" } });
    page.Add(facts, Sw.Grid({ Content: [cpu] }), Sw.Grid({ Content: [list] }));
    cpu.Set({ Value: "12.5" });
    list.SetItems([{ Text: "Mail", Status: { Text: "Running", Kind: "ok", Dot: true }, Meta: "120 MB" }]);
});
```

### Value cells (cells.js)
`Sw.Cells.Update(el, type, value, flash, format, nextValue, onRefresh)` renders a value like the old `ValueFormat.update`: the format
`"Name;arg1;arg2..."` (TableDataColumn.Format: Default, Number, Url, Tags, PerRowFormat, ByteSize, ByteSpeed, Toggle, Actions, Img,
Duration, Json, Text, MD, Uptime, WebColor) picks the renderer, else the C# type (numbers, DateTime, TimeSpan, Boolean, Type), else Default.
* Url target prefixes: `+` / none = new tab, `*` = same window, `^` = top, `-` = parent. A click with a modifier key copies the url.
* Links to other sites (Url and Img cells; `Sw.Cells.External(url)`, `Sw.Cells.ExternalLink(url, text, ext)`): always a new tab with `rel="noopener noreferrer"`, an external icon after the text (`a.sw-link-ext`, `.sw-link-ext-icon`) and the tool tip "Opens <host> in a new tab (another web site)" after the server title. A web search (Google, Bing, DuckDuckGo, Yahoo `/search`) is named as one: the search icon before the text and "Search the web: opens <host> in a new tab and sends this search to it:" with the searched text. Offline (`navigator.onLine` false, or `Sw.Cells.Internet = false` set by an app that knows the internet is not reachable) these links are plain text (`span.sw-link-off`; an Img cell shows the picture without a link) with "Needs the internet (not connected): <url>" in the tool tip; explore/table.js renders the cells again on the browser's online / offline events.
* Actions: `text|title|url|icon` per button; `&url` opens in the same tab, `@url` in a new tab, anything else is a GET request; then `onRefresh()`. Destructive actions (`Sw.Cells.IsDestructiveAction`: Kill, Delete, Remove, Stop, Uninstall, Reboot, Reset ... in the text, the title or the link method name, or a title that warns "can not be undone") ask first: a dialog titled with the action ("Kill the process?") that names the item (`Sw.Cells.ItemName`: the row's `data-sw-item`, set by explore/table.js from the key columns), Cancel focused, the action as a danger button; this is `Sw.Cells.ConfirmDanger(o)` ({Title, Item, ItemLabel, Text, Warning, OkText, OkIcon, OkTitle, CancelTitle} -> Promise<boolean>), the one confirmation of destructive actions (also used by edit/text.js Delete / Reload): warning icon, question title, item, "This cannot be undone.", Cancel / verb. Kill on process id 0, or id 4 named "System" (Windows), is disabled (the server sends no per row availability). TimeSpan / Duration cells use one short style per column: µs / ms below 10 s, "48.3 s", then two units ("9 min 19 s", "1 h 18 min", "2 d 18 h"). Tags are neutral pills (`sw-tag`: `--sw-bg-3` with a `--sw-line` border, every theme). Img cells with a short word value (ex: POST / GET) use the value as alt text and tool tip instead of the image url.
* Toggle: true / false texts and titles, the api url. Failed requests show a fail toast.
* Json / Text / MD cells show a shortened text; click / Enter opens the full text in a dialog (markdown via `common/marked.js`, loaded on
  demand, raw html restricted to formatting tags, script urls removed).
* Uptime / empty DateTime cells stay live (one shared timer). Img cells: a picture that fails to load shows the cell value as text (`span.sw-cell-img-text`, ex: "GET", "SE") instead of a broken image; `iso_data/` flags are requested only when the IsoDataService runs (one `Api/HaveService` probe per page), else the value is shown as text (no 404s). Number cells: a non zero value that would round to zero gets 2 significant digits (0.000041, not 0.0000); a Number format with a fifth option (C# `TableDataCurrency`: an ISO 4217 code or a template like "{1}" = the next column) is an amount, shown with the currency symbol (and, for negative decimals, its decimals) from `iso_data/currencies.js` (loaded on demand when the IsoDataService runs) and the same separators for every currency ("$1 234.50", "1 234.50 kr"), else "1 234.50 XYZ". Most cells copy their value on click. Read only Boolean cells show a check mark + "Yes" or a dash + "No"; Toggle cells are a switch (track and knob) and an Actions button of the same row with the same request is left out; an empty Tags value shows "🔑 Signed in". Duration, Uptime and "{0} s" Number cells share the duration style ("0 s", "31.8 s", "30 min 59 s", "4 d 14 h", "50 years"); a "{0} s" Number cell above 1 500 000 000 s (a "forever" cache duration) reads "Never expires", as the Explore cache columns.
* Renderers: `Sw.Cells.Default`, `Number`, `Url`, `Img`, `Tags`, `PerRowFormat`, `ByteSize`, `ByteSpeed`, `Toggle`, `Actions`, `TimeSpan`
  (`Duration`), `Json`, `LongText`, `MarkDown`, `DateTimeLive` (`Uptime`), `DateTime`, `Boolean`, `Type`, `WebColor` (old signature
  `(el, value, formats, nextValue, flash, type, onRefresh)`); maps `TypeFormatters`, `FormatFormatters`; helpers `Reset`, `SetText`,
  `SetNodes`, `UrlTarget`, `CreateLink`, `External`, `ExternalLink`, `OnLink`, `ShowJson`, `ShowText`, `ShowMarkdown`; setting `Internet` (null / false).
* Classes: `Sw.Cells.Classes` (replaceable, ex: by the compat layer with the old names): `sw-right`, `sw-left`, `sw-center`, `sw-mono`,
  `sw-null`, `sw-false`, `sw-cell-text`, `sw-cell-actions`, `sw-capped`, `sw-cell-link`, kind `sw-fmt-<Format>`. Pass `"sw-flash"` as flash to
  highlight changes.
* `Sw.Markdown.ToHtml(text)`, `Sw.Markdown.Set(element, text)`, `Sw.Markdown.Load()`. The output is sanitized (DOM based allow list in an
  inert template): only markdown / formatting elements; attributes only `title` plus `a` href/target/rel, `img` src/alt, `th`/`td` align,
  `ol` start, `code` language class, disabled task list check boxes; href/src must not be `javascript:` / `vbscript:` / non image `data:`;
  `style`, `class`, `id`, `srcset`, `on*` etc. are removed; script / style / iframe / svg / form elements are dropped with their content.
* **Image rule (user content must not fetch urls by itself):** markdown images (`![x](url)` and raw `<img>`) load automatically only when
  the url is `data:image/png|jpeg|gif|webp|avif|bmp` or a **same origin static image file**: http(s), no query string, no `Api` path
  segment (any case, also percent encoded), an image file extension (`png`, `jpg`, `jpeg`, `gif`, `webp`, `avif`, `bmp`, `ico`, `svg`).
  Everything else (other origins = tracking pixels, same origin `Api/...` or `?query` urls = credentialed GET requests) is rendered as a
  "Show image" placeholder (`span.sw-md-img-off`, role button, the url in the tool tip) that loads the image (no referrer) only on
  click / Enter / Space (one delegated capture phase handler on the document, so a surrounding link or row does not react).
  Allowed images get `referrerpolicy="no-referrer"`. Uploaded files (`../storage/...png`) keep loading. Apply the same rule to other user
  supplied image urls with `Sw.Markdown.CanAutoLoadImage(url)` and `Sw.Markdown.ImagePlaceholder(url, alt)` (the chat does for attachments).
* **Emphasis** in rendered markdown (`.sw-md`, ui/cells.css): `em` italic and semi bold, `strong` / `b` bold, both in the normal text color in every theme (never an accent color that reads like an error).

### CodeEditor (code.js)
A code editor without dependencies: a textarea (input, selection, clipboard, IME, native undo) with transparent text over a highlighted
copy of the visible lines (virtualized). Languages `glsl`, `md`, `json` (used by the JSON tab of explore/api.html), `text`.
```js
const ed = new Sw.CodeEditor({ Value: src, Language: "glsl", ReadOnly: false, AutoComplete: true, Completions: () => [], OnChange: ed => {} });
ed.SetMarkers([{ Line: 12, Kind: "error", Text: "'x' : undeclared identifier" }]);
```
`Get()`, `Set(text)`, `Edit(text)`, `Insert(text, pos)`, `Replace(start, end, text, selStart, selEnd)`, `Cursor()`, `GoTo(line, col, select)`,
`GoToLine()`, `Find(replace)`, `Focus()`, `SetReadOnly(bool)`, `Refresh()`, `Destroy()`, option `Hover(info, editor)`.
Keys: Tab / Shift+Tab, Enter (keeps the indentation), Home (smart), Ctrl+/ (comments), Ctrl+D, Ctrl+Shift+K, Alt+Up / Down, Ctrl+F / Ctrl+H
(find / replace: case, word, regex), F3 / Shift+F3, Ctrl+G, Ctrl+Space. Colors: `--sw-code-*` tokens (a light variant for the light theme).

## Icon classes (icons.css)

Every icon class named by the server (C# `IconClass` values, contract Appendix B) and by the pages is defined as a css class setting
`mask-image` to an svg of the icon module (`../icons/*.svg`): the element's background color shows through the mask, so icons follow the
theme (`Sw.IconClass` elements use the text color; old `ColorIcon` elements their `IconColorTheme*` class). `IconLogo`, `IconCurrentUser(Fill)`
and `IconFile*` are full color background images. `IconWorking` is animated. The four color classes `IconColorThemeBackground/Main/Acc1/Acc2`
are defined too. Pages may override a class in their own style sheet. Icon "classes" may also be urls (`icons/x.svg`, `../icons/x.svg`,
a trailing `*` for full color images). `IconOk` (dialog OK / confirm buttons) is a check mark drawn by an inline data: svg (the icon module has no check mark file; it used to be the `fav_on.svg` heart). The gallery lists every class; Debug / Icons (explore/icons.html) shows each svg with its class names and copies the class name.

## Gallery

`ui/gallery.html` (served at `/ui/gallery.html`) is the showcase and test bench of the library. An intro says what the library is and how
to load it, with links to this README and the [quick start guide](../../../_docs/WebQuickStart.md). Every card names its API (`Sw.Button`,
`Sw.ButtonRow` ...), links to its README section (Docs) and has a Code toggle with a minimal copyable snippet (Copy); the snippets live in
the `Docs` table of `gallery.js`. It shows every component in every state (header: theme select, mobile layout toggle, keyboard help; `?` and `Mod+K`
shortcuts). It loads the source files one by one, or the bundles with `?bundle`. Core url flags work (`?settheme=dark`, `?mobile=true`).
Elements that automated tests use carry `data-g` attributes. The demo texts are not translated (a developer page, `translate="no"`).
The Brand card shows the marks, the 4 theme colours and the 7 chart slots `--sw-chart-1..7` of the current theme (`data-g="chart-slots"`, in legend order, see the chart README). The Icons card lists every icon class of the style sheets; classes whose svg comes from an optional service (the 4 passkey icons of the PassKey service, `OptionalIconFiles`) are only shown after one `Api/HaveService` call found the service, so apps without it request no missing files.
