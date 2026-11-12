(`Card: false` is the clean choice there). The cells wrap (flex): the cells of a short last row share its width, so no row ends in an empty cell (Designer D28; two per row on phones, an odd last cell takes the row; one per row: `flex-basis: 100%` on the cells) |# ui - the SysWeaver UI library (Sw)

A small, dependency free, touch first component library for SysWeaver pages: vanilla JavaScript classic scripts (no modules, no
frameworks), one namespace `Sw` shared with the UI-less core (`../core`, bundle `sw/core.js`). It is a port of the EffectWeaver `Ew`
library, rebranded and extended with everything the SysWeaver pages need (icon classes, toasts, loaders, the server block overlay,
menus with submenus, searchable selections, value cells ...).

* Touch, mouse and keyboard: pointer events, controls `--sw-control-h` high (the density: 44 px on touch screens / the mobile layout,
  34 px with a mouse, 28 px compact; every target at least 24 px), no hover-only features (tooltips also show on long press and keyboard focus), visible focus rings, Escape closes, arrow keys in
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
| `page.js` | sw/ui.js (last) | the page anatomy: `PageLayout`, `MenuTitle`, `Card`, `Facts`, `Kpi`, `Meter`, `ListCard`, `Grid`, `SectionTitle`, `StateView`, `UploadButton`, `UploadTarget`, `ReportUpload`, `FillTable` (section "Page anatomy"), `SelectList`, `ListDetail` ("List / detail"), `Form` ("Forms"), `Steps` ("Wizards") |
| `editors.js` | sw/ui.js | `PropertyEditor` |
| `cells.js` | sw/ui.js | `Sw.Cells` (value cell renderers for TableData formats), `Sw.Markdown` |
| `code.js` | (on demand) | `Sw.CodeEditor` (code editor), `Sw.CodeLanguages`, `Sw.GlslDocs` |
| `compat_common.js` | common/common.js (legacy bundle = core + ui + this) | Every global of the old `common/common.js` mapped onto Sw (old element based classes `ColorIcon`, `Button`, `ProgressBar`, `ValueFormat.createLink` keep their `icon-WxH` / `Sysweaver-Button` / `SysWeaver-*` elements styled by `common/theme.css`); sets the old cell class names on `Sw.Cells.Classes`; links `sw/ui.css` and `compat_common.css` on old pages. Not for new pages |
| `compat_common.css` | (linked by compat_common.js) | Old look of the Sw cell elements on old pages (links, tag pills) |
| `tokens.css`, `components.css`, `overlays.css`, `cells.css`, `code.css`, `icons.css`, `page.css` | sw/ui.css (in this order) | design tokens, component styles, SysWeaver icon classes, the page anatomy |
| `gallery.html`, `gallery.js` | - | the component gallery (every component in every state; testing and reviews) |

Load order: core first. Each js file is an IIFE (`"use strict"` inside) that works concatenated (bundle) and loaded one by one.

```html
<meta name="viewport" content="width=device-width, initial-scale=1">
<script defer src="../sw/core.js"></script>
<script defer src="../sw/ui.js"></script>
<script defer src="page.js"></script>          <!-- the page code: a deferred file, never an inline script (it would run before the bundles) -->
<link rel="stylesheet" href="../common/theme.css">
<link rel="stylesheet" href="../sw/ui.css">
<link rel="stylesheet" href="page.css">
...
<body class="sw-root">
```

```js
// page.js
Sw.Page.Main(async () => {
    const page = Sw.PageLayout({ Type: "form", Title: _T("Playback"), Description: _T("How fast clips play.") });
    const s = new Sw.Slider({ Min: 0, Max: 1, Value: 0.5, Label: _T("Speed"), OnChange: v => console.log(v) });
    page.Add(Sw.Card({ Title: _T("Speed"), Body: s }));
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
| `Sw.Dialog.Confirm(title, text, okText, { ... })` and `Sw.Dialog.Prompt(title, value, placeholder, { ... })` are half positional, `Sw.Dialog.Show({ ... })` takes options only | as documented; `Confirm` also takes the options form `Confirm({ Title, Text, OkText, ... })`; `Show` for custom dialogs |
| Boolean positional parameters: `Set(value, silent)`, `Sw.Session.Subscribe(type, fn, noServer)`, `Sw.Format.Uptime(date, blankIfNeg, invert, now)`, `Sw.Focus.Set(el, scroll)` | name the argument in a comment at the call site |
| Result shapes differ: `Sw.Format.TimeStamp(...)` returns `[tooltip, text]`, `Sw.Format.Value(...)` returns `{ Kind, Text, Title }` | as documented |
| `Sw.FullScreen` (ui) and the alias `Sw.Fullscreen` (Ew spelling); the legacy `FullScreen` class in `compat_common.js` is a separate wrapper of it | `Sw.FullScreen` |
| `Sw.Cells` renderers keep the old positional signature `(el, value, formats, nextValue, flash, type, onRefresh)` | `Sw.Cells.Update(el, type, value, flash, format, nextValue, onRefresh)` |
| `Sw.el({ html })` sets `innerHTML`: never pass server or user data to it | `text` (or `Sw.Markdown.Set` for user markdown) |
| There is no PascalCase `Sw.El` alias, and nothing is marked `@deprecated`: the legacy globals and aliases are documented as legacy (compatibility), not scheduled for removal | the names in this README |
| The bundles (`sw/core.js`, `sw/ui.js`, `sw/shell.js`) are not minified (readable, with JSDoc; the server compresses the responses) | - |

### Naming rules (one name per concept)

New code uses the canonical name. The other names are aliases: they keep working and are not scheduled for removal.

| Concept | Canonical | Accepted aliases | Notes |
|---|---|---|---|
| Visible label above a control | `Label` (TextBox, NumberBox, Select, Toggle, Slider in a `Sw.Field`, `Sw.Field`) | - | the form field layout, see "Form fields" |
| Accessible name only (no visible label) | `AriaLabel` (every control: Button, Segmented, Slider, ProgressBar, TextBox, NumberBox, Select) | `Label` on Button, Segmented, ProgressBar, Slider (where it never was visible) | `Tabs`, `Split`, `Menu`, `Card`, `Grid`, `Meter` (page parts) still take `Label` as the accessible name |
| List of choices | `Items: [{ Value, Text, Icon, Title, Disabled }]` | `Options` (and `Label` for the text) on `Sw.Select` and `Sw.Segmented` | `SetOptions(items)` takes either form |
| Buttons in a header | `Actions` (option and area: `PageHeader`, `PageLayout`, `Group`) | `Tools` (option and `Group.Tools` / `Card.Tools` area) | `Group.Actions` is the same element as `Group.Tools` |
| Red / green tone | `Kind: "danger"` / `"ok"` (Button, Badge, Toast, header status) | `"error"` / `"success"` | the css classes keep their names (`sw-danger` on buttons, `sw-error` on badges and toasts) |
| Options form | `Sw.Badge({ Text, Kind, Title })` | `Sw.Badge(text, kind, title)` | every component takes one options object |
| Required field | `Required: true` (or the message) on TextBox, NumberBox, Select, Toggle, Segmented; `Sw.Field({ Required })` marks the label | - | `Validate()` returns the message or null and shows it under the field; `Sw.Form` calls it |
| Optional helper arguments | have defaults | - | ex: `Sw.Cells.ExternalLink(url)` works without the text and the `External(url)` result |

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
base colors, so all derived tokens follow. Everything below is computed from the four base colors (color-mix, per scheme), so the five
themes, the app colours (Application theme) and a brand follow automatically **when page css uses these role tokens** (Design v2).

| Role | Tokens | For |
|---|---|---|
| Surfaces | `--sw-canvas` (page, top bar), `--sw-nav` (sidebar), `--sw-surface` (cards, tables, forms, buttons, inputs), `--sw-surface-sunken` (table head, wells, code, tracks), `--sw-surface-raised` (menus, dialogs, tooltips), `--sw-surface-strong` (a visible neutral fill), `--sw-surface-inverse` / `--sw-on-inverse` (toasts) | tonal layering instead of shadows |
| Lines | `--sw-border` (every hairline), `--sw-border-strong` (button borders, hover), `--sw-border-input` (inputs, >= 3:1) | 1 px borders |
| Text | `--sw-text` (dark schemes: a step below the main colour, about #E4E8EB on Slate 900; the page body uses it), `--sw-text-2` (secondary, >= 4.5:1), `--sw-text-3` (icons, placeholders, axis; never essential text), `--sw-text-accent` (links, the current item), `--sw-on-accent`, `--sw-on-danger`; weights `--sw-sans-weight` 400, `--sw-weight-medium` 500 (labels, buttons, values; Hacker 400), `--sw-head-weight` 600 | headings and labels are `--sw-text`, accent only for meaning; use the weight tokens, not 500 / 600 literals |
| States | `--sw-hover`, `--sw-pressed`, `--sw-selected`, `--sw-selected-strong` (layers painted over any surface), `--sw-focus` + `--sw-focus-ring` | hover only with `@media (hover: hover)` for rows; focus 2 px `--sw-focus` |
| Status | `--sw-ok`, `--sw-warning`, `--sw-danger`, `--sw-info` and `-soft` fills | always icon + text, never colour alone |
| Accent | `--sw-accent1` (the one primary action per region), `--sw-accent2`, `--sw-accent1-soft`, `--sw-accent2-soft` | |
| Data | `--sw-chart-1` .. `--sw-chart-7`, `--sw-chart-grid`, `--sw-chart-axis` | chart series (below) |
| Elevation | `--sw-elev-0` (none: header, sidebar, cards, tables, inputs, buttons), `--sw-elev-1` (menus, popovers, tooltips), `--sw-elev-2` (dialogs, drawers, toasts), `--sw-scrim` (behind a dialog) | only what floats has a shadow |
| Radii | `--sw-radius-sm` 4, `--sw-radius` 6 (controls), `--sw-radius-md` 8 (cards, menus, tables), `--sw-radius-lg` 12 (dialogs), `--sw-radius-pill` (badges, chips, switches); Pro 3 / 4 / 6 / 8, Hacker 0 | never px radii in page css |
| Space | `--sw-space-1` .. `--sw-space-12` (4 px grid: 4, 8, 12, 16, 20, 24, 32, 40, 48), `--sw-page-px` (page padding: 32, 24 compact, 16 on phones) | |
| Type | `--sw-text-xs` 12, `-sm` 13, `-md` 14 (body, controls; density), `-lg` 16, `-xl` 20, `-2xl` 26, `-3xl` 32 (rem), `--sw-lh`, `--sw-lh-tight`; fonts `--sw-sans`, `--sw-head` (headings), `--sw-mono`, `--sw-serif` | numbers `font-variant-numeric: tabular-nums` (no monospace for numbers) |
| Density | `--sw-control-h` (28 / 34 / 44) and `--sw-control-h-sm` (24 / 28 / 36): the only two control heights (small buttons, chips, the buttons inside a field or a tab, menu tools use the small one; the shell top bar buttons the default one); every form element takes the page font (no browser default font), `--sw-row-h` (32 / 40 / 48), `--sw-nav-row-h`, `--sw-icon`, `--sw-cell-px`, `--sw-text-md` | sizes of your own controls and rows |
| Layout | `--sw-side-w`, `--sw-rail-w`, `--sw-top-h`, `--sw-page-max` (100rem), `--sw-form-max` (75rem), `--sw-read-max` (45rem), `--sw-label-w` | |
| Motion | `--sw-ease`, `--sw-dur-1` (hover, 50 ms), `--sw-dur-2` (menus, switches, 100 ms), `--sw-dur-3` (drawers, dialogs, 150 ms), each times the theme's motion scale `--sw-motion` (1 = normal; registered `<time>` properties, so scripts read "0.1s"); slide distances `--sw-dist-1` (small shifts, 2 px), `--sw-dist-2` (menus, tool tips, toasts, 4 px), `--sw-dist-3` (dialogs, sheets, drawers, 16 px). Per theme: Pro `--sw-motion: 0.25` (12 / 25 / 38 ms) and 1 / 2 / 8 px; Hacker `--sw-motion: 4` while the motion review lasts (200 / 400 / 600 ms: every transition is easy to see; it was 0 = no animation); an app sets its own scale in `app/app.css` (`:root { --sw-motion: 0.5; }`). Example: `transition: opacity var(--sw-dur-2) var(--sw-ease), transform var(--sw-dur-2) var(--sw-ease); transform: translateY(var(--sw-dist-2));` | `prefers-reduced-motion` wins over every theme: no movement (the slide distances 0, `--sw-zoom` 1, the shell's width animation off), the fades and colour changes keep their durations (opacity and colour are not motion; with everything at 1 ms the app looked dead with Windows "Animation effects" off). Only motion is reduced: use the tokens for every transition and keyframe; there is no blanket rule on every element (an element without a transition must not get one), and a colour fade in place with its own fixed duration keeps it |

**Density**: `html[data-density="compact" | "comfortable" | "touch"]` (`Sw.Theme.Density.Set(name)` / `Set(null)` = automatic, the
Settings menu of the shell, the url parameter `?density=`; stored in localStorage `SysWeaver.Density`, every window follows). Automatic:
touch on touch screens and in the mobile layout, compact for Pro, else comfortable. Never write fixed 44 px heights or media queries on
`pointer` in page css: use the density tokens.

**Text size**: `html[data-text-size="80" | "110" | "125" | "150"]` (percent; no attribute = 100; `Sw.Theme.TextSize.Set(size)`, the
Settings menu of the shell; stored in localStorage `SysWeaver.TextSize`, every window follows). It multiplies the html font size and
`--sw-scale` (`--sw-text-scale`), so every rem size (text, icons, controls, rows, the shell) grows and the layout reflows: not `zoom`
(that also scales px borders and breaks pointer and element positions in scripts) and not a font size per element. Size in rem, not px,
so a page follows it; media queries in rem keep the browser's size (the drawer threshold does not move).

**Old token names** stay as aliases (`--sw-bg-1` = `--sw-surface`, `--sw-bg-2` / `--sw-bg-inset` = `--sw-surface-sunken`, `--sw-bg-3` =
`--sw-surface-strong`, `--sw-line` = `--sw-border`, `--sw-line-strong`, `--sw-muted` = `--sw-text-2`, `--sw-faint` = `--sw-text-3`,
`--sw-accent1-fg` = `--sw-text-accent`, `--sw-shadow` = `--sw-elev-1`, `--sw-backdrop` = `--sw-scrim`, `--sw-touch` = `--sw-control-h`,
`--sw-size` = `--sw-text-md`, `--sw-speed` = `--sw-dur-2`), so old pages keep working; new code uses the role names (`SwWeb migrate`
renames the uses, [PORTING.md B1](../PORTING.md#b1-old-token-names-aliases)). Overriding an alias (`--sw-touch: 52px`) no longer
restyles the components: set the new token (B2).
`--sw-focus` (every focus ring) is a fixed blue per scheme, never the accent2 / danger red: light `#1a66d6`, dark schemes (dark,
application) `#6cb6ff`, hacker `#3fd2ff`, pro `#7cc4ff` (contrast >= 4.5:1 on the theme background); a theme may set `--ThemeFocus`.
Fonts (the local brand fonts, see Brand below, then system fonts): `--sw-sans` (= `--ThemeFont`), `--sw-mono` (= `--ThemeFontMono`), `--sw-head` (= `--ThemeFontHead`),
`--sw-serif`; `--sw-mono-size`. Large high resolution screens (`(min-device-width: 2200px) and (min-width: 1400px), (min-width: 2200px)`, and a second step at 3200 px) scale the html font size (so every rem token follows) and `--ThemeIconSize` (desktop 1920 is unchanged); `--sw-scale` (1, 1.125, 1.375) tells pages the step for their own px sizes (`calc(20px * var(--sw-scale))`).
Restyle a component through its tokens on a page scope (`.big-form { --sw-control-h: 2.75rem; }`), never through its inner classes.

Helper classes: `sw-root` (font, color, `box-sizing: border-box` for everything inside: **every page that uses Sw components has
`<body class="sw-root">`**; the components, `Sw.Table` among them, are built for it; `SwWeb migrate --level design` adds it, check rule
`design-sw-root`), `sw-page` (opaque background), `sw-mono`, `sw-serif`, `sw-head`, `sw-muted`,
`sw-faint`, `sw-noselect`, `sw-selectable`, `sw-scroll`, `sw-row` (`sw-start`, `sw-end`), `sw-stack`, `sw-sr-only`.

### Brand

The default light and dark themes are the SysWeaver brand (light: Mist 50 / Slate 900 / Copper 700 / Steel 600; dark: Slate 900 /
Mist 50 / Copper 500 / Steel 300). Text on accent fills is the theme background (`--sw-on-accent`), text on danger fills
`--sw-on-danger`. Fonts: Barlow (`--ThemeFont`, `--sw-sans`) and Saira Semi Condensed SemiBold (`--ThemeFontHead`, `--sw-head`: the
headings `h1`..`h4` of `sw-root` pages, `sw-head`, the shell title), from `../brand/fonts.css` (local data urls, imported by
`common/theme.css`); pro and hacker keep their own font. Icon and logo: the app's own, generated per app by the server
(`HttpServerBase_Favicon.cs`) from the app name / seed unless the app serves its own files; no SysWeaver marks, the same files in
every scheme: `--sw-brand-icon` and `--sw-brand-small` = `url("../icon.svg")` (square), `--sw-brand-logo` and `--sw-brand-stacked` =
`url("../logo.svg")` (4:3), `--sw-brand-banner` = `url("../banner.svg")` (9:2, the app icon in its left square: the shell's
expanded menu, crossfaded with `--sw-brand-small` when it collapses; the sign in dialogs). Classes `sw-brand-mark` (square, `aspect-ratio: 1 / 1`), `sw-brand-logo` and `sw-brand-stacked` (4:3) show
them as a background (give the element a size; no minimum widths). The weave: `sw-weave` (background only) with a solid `sw-weave-panel` for the content. These
variables have zero specificity, so an app may point them at its own files in `app/app.css` (`url("../app/x.svg")`), see
[`_docs/WebBranding.md`](../../../_docs/WebBranding.md). The gallery's Brand card shows them all.

Chart series palette: `--sw-chart-1` .. `--sw-chart-7` (zero specificity). Slot 1 is `--sw-accent1` (copper), the steel slot
`--sw-accent2` (slot 7 in light, slot 6 in dark), the others are the brand's extra hues per scheme (light: blue `#2A78D6`, green
`#138A60`, violet `#6250D6`, magenta `#C4507E`, ochre `#A87400`; dark: `#3987E5`, `#199E70`, `#9085E9`, `#C98500`, `#D55181`),
validated as a set (adjacent pairs: CVD separation >= 11, normal vision >= 18 OKLab dE; >= 3:1 against the brand backgrounds).
Status colors are never series colors. Used by the chart viewer's default "Theme colors" mode (chart/chart.js).

## Helpers (base.js)

| Function | Description |
|---|---|
| `Sw.el(tag, props, ...children)` | Create an element. `tag` may have classes (`"div.a.b"`). Props: `class, text, html, title, attrs, style, dataset, on: {event: fn}` or any element property. Children: nodes, texts and numbers; **arrays are flattened** (any depth: `Sw.el("div", null, [a, b], c)` = `Sw.el("div", null, a, b, c)`); a **component** (`{E}`, ex: a `Sw.Button`) adds its `E`; `null` / `undefined` / `false` are skipped; any other object is left out with a console error (it used to become the text "[object Object]"). An array or a component in the `props` place is the first child |
| `Sw.Icon(name, cls)` | An inline svg icon (24×24, stroked; `Sw.Icons.List()` names), or for icon classes / urls a mask icon (`Sw.IconClass`). An **unknown name** shows a neutral dashed square (`svg.sw-icon-unknown`, `data-icon` = the name) and logs one console warning per name; it used to show "info", which reads as a real meaning |
| `Sw.Icons.Add(name, svgInner)`, `Has(name)`, `List()` | Register / query inline icons |
| `Sw.IconClass(image, {Size, Color, Title, Class})` | Mask icon from a SysWeaver icon class (`"IconReload"`) or an url (`"../icons/x.svg"`; a trailing `*` = full color image). Drawn in the text color, or `Color` (css color or an old `IconColorTheme*` name) |
| `Sw.SetIconClass(el, image)` | Change the image of a mask icon |
| `Sw.IsPureEvent(ev)`, `Sw.BadClick(ev, keepProp)` | No modifier keys / the old click guard. `BadClick` also moves the focus on a modifier click (`Sw.ModifierClick`) |
| `Sw.ModifierClick(ev, target)` | UX guide INP-2, the one handling of a modifier click: `if (Sw.ModifierClick(ev)) return;` in a click handler. Shift / Ctrl / Alt / Meta + click focuses the clicked interactive element, else its list / table item (cell, row, option, tree item, `li`, `.sw-card`, `[data-sw-item]`), and shows the ring; returns true (never run the pure action). A key combination (Ctrl+Enter, `detail` 0) returns true without moving the focus. Every Sw control uses it; tables and pages should too |
| `Sw.Focus.Show(el, scroll)` | Focus and show the ring after a pointer action: `focus({ focusVisible: true })`, else (the browser ignores the option, or the element had the focus already) the class `sw-focus-shown` (the `--sw-focus` outline) until the next pointer down or blur |
| `Sw.IsPureContextMenu(ev)` | A `contextmenu` the element's own menu may take: no modifier (Ctrl alone on Apple = right click), or from the keyboard / a touch long press (INP-3) |
| `Sw.MenuButton(el, items, { Menu, NoClick, AtPointer })` | A menu button (INP-3): a pure click, a pure right click, a long press, the Menu key and Shift+F10 open `Sw.Menu`; `aria-haspopup="menu"`, `aria-expanded`. Returns `{ Open, Close, Remove }`. `Sw.Button({ Menu })` uses it. Any other button with `aria-haspopup="menu"` gets the right click too (a global handler clicks it) |
| `Sw.Disabled.Set(el, disabled, reason)`, `Is(el)`, `Reject(el, ev)`, `SetReason(el, text)`, `Shadow(el, set)` | Disabled the reachable way (INP-12): `aria-disabled="true"` + class `sw-disabled`, focusable, a capturing click guard; an attempt (click, tap, Enter / Space) shows the reason in a `Sw.Hint` bubble (3.5 s) and announces "Save is not available: Select a row first" (the same input on the same element within 1 s once). The reason is the tool tip while disabled (the old title comes back when enabled). `{ Native: true }` sets the attribute instead (only where the focus must not land). `Shadow` makes `el.disabled` read / set the aria state (the Sw controls do it, so `button.E.disabled` keeps working) |
| `Sw.KeyClick(el, onClick)`, `Sw.RemoveKeyClick(el)` | Make an element keyboard clickable (Enter, Space) |
| `Sw.ActionName(title)` | An accessible name from a tool tip: drops a leading "Click to " / "Click ", a trailing period and lines after a blank line ("Click to sort by Name." → "Sort by Name"); other languages pass unchanged. Used for icon only buttons, sort buttons, table image alt texts |
| `Sw.LongPress(el, fn, ms or { Ms, Skip(ev), Mouse })` | Touch / pen hold (550 ms, a 10 px move cancels), a **pure** right click, or the context menu key / Shift+F10 call `fn(event)`; a right click with a modifier is left to the browser's menu (INP-3); a mouse hold does nothing unless `Mouse: true`; `Skip(ev)` returns true to leave an event to the browser (ex: selected text); an aria-disabled element shows its reason instead. Returns a remover. Nested long press elements: the innermost one handles the event. |
| `Sw.NoContextMenu()` | No browser context menu on a **touch** long press outside text inputs, editable content and selected text (it would cover the page's own long press); a mouse right click and the keyboard keep the browser's menu (INP-4). Returns a remover |
| `Sw.HasSelectionIn(el)` | The user selected text inside the element (a click is then not an action) |
| `Sw.Focus.Trap(el)` / `Release(el)` | Keep the focus inside an element (stacked; restores the focus on release). A trap never moves the focus itself, it only reacts to focus / Tab events of its own document |
| `Sw.Focus.Initial(el, target)` | the element a trap gives the focus to when the focus enters it before anything inside had it (a dialog opened in a frame that had no focus: the focus arrives later). `Sw.Dialog` sets it to its default focus, so a danger dialog focuses Cancel, not the Close button, however it was opened |
| `Sw.Focus.CanTake()` / `Set(el, scroll)` | May this document move the focus (top window, or a frame whose document has the focus)? `Set` focuses only then. Overlays (loader, dialogs, popups, server block) use it, so an embedded page never steals the focus from its parent page |
| `Sw.Focus.Tabbables(root)`, `IsTabbable(el)`, `Next(el)`, `Prev(el)`, `NextOnEnter(input, fn)`, `Sentinel(edge)` | Focus helpers |
| `Sw.Focus.Restore(target)` | Focus a target the safe way (FOC-1, never the body): the target when it can take the focus and is shown, else its first tabbable element (a component root `list.E`, a card, a row); false when nothing could |
| `Sw.Focus.Keep(container)` → `{ Restore(), Stop() }`; `Sw.Focus.Keep(container, work)` → Promise; `Sw.Focus.Keep(container, null, { From: el })` | Keep the keyboard place through a rebuild, a hidden or removed element, or async work (row actions, refreshed lists and cells, replaced page actions): remembers the focused element inside the container (`From`: the place when the focus is not in it yet, ex: a row button whose confirmation dialog gives the focus back later; inside a closing dialog it does nothing: the dialog gives the focus back to its opener); `Restore()` acts only when the focus was lost (body, a removed or hidden element): the same element, else the same id / `data-sw-focus-key`, else in the same row / item (`data-sw-item`, row position) the element with the same name, its failure note, done note or first tabbable, else the row itself (never an element of another row while that row is still listed), else the same name, else the same position, else the container (`tabindex -1`); a developer warning when nothing could take it. With `From`, `From` is the place even when the focus sits elsewhere in the container, and a focus that code or a closing dialog put on another row is taken as lost until the user moves it (Ops R4-7: never another row's "Kill: done" after this row failed). With `work` (a function or promise) it restores after it and resolves with its result. `Sw.Button`, `Sw.OnClick`, row actions, `Sw.Danger.Run`, `Sw.Cells.SetNodes`, the page header actions and `Sw.EmptyState.Set` use it |
| `Sw.Keys.Add(combo, fn, {Title, InInputs, Global, Replace, Scope})` | Keyboard shortcut (`"Mod+K"` = Ctrl, or Cmd on Apple); `Sw.Keys.List()` / `Format(combo)` for help screens. The newest registration of a combination wins and only it is listed; a second registration gives a developer warning (with both titles) unless `Replace: true`; a combination the browser keeps (Mod+W / T / N / Q, Ctrl+Tab, Ctrl+PageUp / PageDown, Alt+F4) gives one too. **`Scope: element`** (or a component): the shortcut runs only while that element is connected and shown (not in a hidden tab panel, `inert`, `display: none`), so two lists in tabs can each register "/" for their own search: the newest *shown* one runs, no duplicate warning, `List()` lists the shown ones. Register the page keys of a component that can sit in a tab with its root as the scope: `Sw.Keys.Add("/", () => search.Focus(), { Title, Scope: list.E })`. A scope element that has never been shown when its key is pressed (nothing runs) gives a developer warning; the options are checked (DevMode) |
| `Sw.Drag(el, {OnStart, OnMove, OnEnd, OnTap, Threshold, Button, Touch})` | Pointer drag helper with capture |
| `Sw.Clipboard.Copy(value, {Toast, Generic, What, Button})` | Copy (works on plain http too). From a copy button (the `Button` option, or automatic when it runs in the `OnClick` of a `Sw.Button` with a copy icon or `Copy: true`) it **confirms in place** (`Sw.Clipboard.Confirm`: a check mark for 1.5 s, the tool tip "Copied", announced "API key copied" with `What`), no toast; else an info toast names the value (INP-31). A failure is a fail toast. `Toast: true` adds the toast to an in place confirmation, `false` shows none |
| `Sw.Clipboard.CopyOnClick(el, value, {Keyboard, AddTitle, What, Generic})` | A copy action on a value, as the original `copyOnClick` (INP-31 1.6.0): **a plain left click copies the value** (a toast names it; not with a modifier, while text is selected in it or on a link / control inside it; a click on such an inner link / control is left untouched: not cancelled or stopped, so the link navigates and a Ctrl / Shift / middle click keeps its new tab); Ctrl+C on the focused value (no text selected) copies the value (a toast names it), and its menu (pure right click, long press, Menu key, Shift+F10) has "Copy value" and "Select text". `Keyboard: true` makes the value a Tab stop on which Enter / Space copy, as the original `copyOnClick(…, true)`. On a button element a click copies and confirms in place. Returns the tool tip hint ("Click to copy "…" to the clipboard", the original text); `Sw.Clipboard.RemoveCopy(el)` removes it. |
| `Sw.CopyButton({ Value or Get, What, Title, Label, Generic, Small, Kind, Text })` | The copy button next to a value: a ghost icon button (44 px hit area on touch), copies exactly the value, confirms in place. Give repeated ones a `Label` with the item ("Copy the API key of MailService") |
| `Sw.Clipboard.Read()` | Read text |
| `Sw.Clipboard.LinkUrl(a)` / `Sw.Clipboard.LinkItem(a, name)` | The absolute address of an `<a href>` (null for none, a script or an in page anchor) / a **Copy link** menu item for it (`{ Text, Title, Icon, OnClick }`, "Copy link: <name>" when a menu lists several links; null when there is no copyable link). The data table cell menu uses it for link cells |
| `Sw.Clipboard.AltClickLinks(root)` | **Alt+click** on a link inside `root` copies its address (a toast names it) instead of the browser's Alt+click (a download in Chromium); plain, Ctrl / Meta / Shift and middle clicks keep the browser's link gestures (UX guide INP-2 exception). Returns a function that removes the handler. Used by the data tables and the explore listing (the original copied on any modifier click) |
| `Sw.Color` | `HsvToRgb`, `RgbToHsv`, `ToHex`, `FromHex` (0..1 floats), `ParseCss(css)` → `[r, g, b, a]`, `Luma(r, g, b)` |
| `Sw.FadeSrc(el, {ShowError, FadeIn, FadeDelay, Channel, Parent, UseEvent})`, `Sw.UnfadeSrc(el)` | Smooth image / iframe loading: hidden while loading, faded in; attributes `data-sw-fade`, `data-sw-fadein`, `data-sw-fadedelay`, `data-sw-fadechannel="name,ms"`, `data-sw-fadeparent`, `data-sw-useevent` work automatically |
| `Sw.CreateImg(o)`, `Sw.CreateIFrame(o)` | Create a smoothly loading img / iframe (old positional arguments accepted) |
| `Sw.SetImageSource(img, src, {Fallback, FlushCache, OnLoaded, ReloadOn})` | Set / reload an image source (fallback url, cache flush, reload on a server message) |
| `new Sw.PendingImages()` | `Start(img, src)`, `await WaitAll()`, `Count`, `InProgress`, `Fails` |
| `Sw.clamp`, `Sw.snap`, `Sw.FormatNumber`, `Sw.Debounce`, `Sw.Throttle` | Small helpers (Debounce / Throttle are the core's `Sw.Util` ones when present) |
| `Sw.Reserve.Line(el, {Lines})`, `Show(el, on)`, `Stack({Sizer, SizerClass, Class})`, `SetSizer(stack, sizer, sizerClass)` | Reserved space: nothing moves while the user types (UX guide LAY-9). See [No layout shift on input](#no-layout-shift-on-input-swreserve-lay-9) |

### No layout shift on input (`Sw.Reserve`, LAY-9)

Whatever appears, changes or disappears while the user types (a validation message, a hint, a password rule list, the status next to a
button, conditional help) has its space **before the first key**, and only its opacity changes: the fields and buttons below never move.

- `Sw.Reserve.Line(el, { Lines })`: `el` keeps its box when hidden (the `hidden` attribute or `Show(el, false)`: `visibility: hidden`,
  opacity 0, never `display: none`; a flex line sets `--sw-reserve-display: flex`), is at least `Lines` lines high (default 1, its own
  line height) and keeps the **tallest height it had** while it is in the page (a shorter or a cleared message never pulls up what
  follows; the height starts over when the window size changes). Returns `el`.
- `Sw.Reserve.Show(el, on)`: fade a reserved line in / out.
- `Sw.Reserve.Stack({ Sizer, SizerClass, Class })`: one grid cell for things shown one at a time or of a changing size (append them);
  the cell is as tall as its tallest child. `Sizer`: the longest texts (or nodes) the cell will hold, as invisible `aria-hidden` copies
  with the classes `SizerClass` (so they look like the real line); `SetSizer(stack, …)` replaces them. Ex: the sign in page sizes its
  rule list by the longest list any policy has, and its hint cell by "To sign in, your password needs: …" with every rule.
- **The framework does it for its own lines:** the message line of a `Sw.TextBox` / `NumberBox` / `Select` with `Required` or `Validate`
  is reserved from the start, in a cell as high as its longest known message (the `Required` text and the new `Messages` option: pass
  the longest texts `Validate` returns); a message set from code keeps its line once shown; `Sw.Form` reserves the line of every field it
  checks and keeps its status line once shown; `SetHint` keeps the hint line once shown; the PropertyEditor keeps a shown message line.
- **Developer checks (`Sw.DevMode`):** a message that needs more room than its field reserved is a `[Sw UX]` warning that names the field
  and asks for `Messages`; a layout shift inside a form or a dialog within a second after the user typed in one of its fields is a
  `[Sw UX] Layout shift while typing in "…"` warning that names what moved (search and filter boxes are not checked). The dialog
  self-test (`ui/dialogtest.html`) types into validated fields in every frame and fails on any move.
- Measure a page: a `PerformanceObserver` for `layout-shift` entries summed **including** `hadRecentInput` (the browser leaves shifts
  right after input out of CLS; typing is exactly that) must stay 0 while typing.

## Components

### Button
`new Sw.Button({ Text, Icon, Title, AriaLabel, Kind: "primary" | "accent" | "ghost" | "danger", Small, Block, Pressable, Pressed, AutoToggle, Repeat, Disabled, Working, IconRight, Type, Class, OnClick(button, event) })`
* Accessible name: `AriaLabel`; else the visible `Text` (the `Title` stays the tool tip, read as the description); else (icon only, or a text without letters / digits) the `Title` as an action (`Sw.ActionName`); else the icon name. Give buttons that repeat per item an `AriaLabel` with the item ("Copy the url of the Fire effect page"); `SetLabel(text)` changes it.
* `Icon`: an inline icon name, an icon class (`"IconReload"`) or an url.
* `OnClick` returning a promise makes the button busy until it settles (FBK-2 / 3): repeated clicks and Enter are absorbed, `aria-busy`, `cursor: progress`; after 250 ms a spinner in the icon's place, or in front of the label of a text only button (the one busy look, `Sw.Work.SpinBeside`: clear of the rounded edge; on a wide / full width button right before the label, which stays; on a tight one spinner and label move together by the missing room; the label stays at full strength, the width stays, nothing is covered) and "Working…" announced. An async handler that started a request before its first `await` without awaiting it stays busy until that request ends too. Returning a `Sw.Activity` job (`Start`, or the job `StartServer` resolves) keeps the button busy until the job ends: done = done, failed = not done (the activity list says it in the shell; a stand-alone page gets the failure toast), cancelled = stopped.
* **One result path** (`Sw.Work.Outcome` / `Report`, the same for `Sw.OnClick`, auto saving controls, row actions, `Sw.Danger.Run` and `Sw.Form`): a throw, a rejection, a request it started that failed, or a promise that resolves `false` is **not done**; work whose signal was aborted (`Abort()`, a dialog's Cancel, the page was left) is **stopped**, never "done". The result is shown **once**: nothing more when the action showed it itself (a toast, a status line, a field message); else a failure toast "Save: not done. <reason>" (a lost connection: "Save: not done, the connection to the server was lost", nothing while the shell shows "Offline"; a refused request, 403, names the role needed and disables the button: `Sw.Access.ActionDenied`), "Save: not done." for a `false` that said nothing (plus a developer warning: say why, or throw an Error with the reason), "Transfer stopped: it may already have been done on the server." for an aborted write (an aborted read is only announced "Load stopped."), else "Save: done" announced (`Done: false`: none, `Done: "text"`: your text). `Click()` resolves with the outcome `{ Ok, Aborted, Error, NotDone, Wrote, Value, Said }`. The failure sentence is built from the error object (`Sw.Work.FailText(name, error)`: "Save: not done. <the server's reason, or what the status means>", never "not done.: …"). Nothing is said when the error / abort was announced already (`error.SwSaid === true`: a dialog's "Save cancelled" / "Stopped waiting for Save…", the activity list's failure; `out.Said`).
* **Guard rails:** requests started inside `OnClick` (`Sw.Net.Send` / `Get`, `fetch`) make the button busy **even when the promise is not returned** (a developer warning on localhost / `Sw.Debug` says to return it, so a failure is shown). `OnClick(button, event, { Signal })` gets an `AbortSignal`: `button.Abort()` (a dialog's Cancel) and leaving the page fire it, and requests started in `OnClick` without a signal of their own use it (`CanAbort()` tells whether one runs). While a dialog opened by the click waits for the user (`await Sw.Dialog.Confirm(...)`) the button is not busy (no spinner, no "Working…"). Busy for more than 2 s: the tool tip says "Save: working (3 s)" and leaving the page asks "Save may not finish." (`Leave: false` opts out). `Saves` (default true for `Kind: "primary"`, false opts out): while the action runs the edited controls of its card / form don't mark the page, and when it succeeds they are clean ([unsaved edits](#unsaved-edits-of-the-controls-saf-10)). Any other button (and Enter in a TextBox with `OnEnter`) is a save too when its work succeeded and **sent the edited values** (a write whose payload carries them): those controls are clean; a write that carried none of them leaves them dirty with a developer warning ("mark it `Saves: true` / `false`"). Work that is not the click's (telemetry, a live view, a long poll): `Sw.Net.Send(url, data, { Detached: true })` / `Get(url, { Detached: true })` / `fetch(url, { Detached: true })` never makes the button busy (the flag belongs to the request: the inner `fetch` of `Sw.Net.Get` / `Send` is part of the outer call). A `Detached` write that carries the edited values of the page is the button's own save marked by mistake: a developer warning ("remove Detached and return the request"). `Saves: true` also marks the button `data-sw-saves`: a declared save anywhere on the page (ex: the page header's `Actions`) makes the edits of the page tracked.
* `Pressable`: a toggle button (`aria-pressed`, flips on click unless `AutoToggle: false`). `Repeat`: repeats `OnClick` while held (acts on press); a click without a press (screen reader, voice control) acts once, Enter / Space once per key press (INP-1, INP-7).
* `Disabled: true | "reason"`, `SetDisabled(bool, reason)`: **aria-disabled** (INP-12), not the attribute: the button stays focusable and muted (`cursor: not-allowed`), its tool tip is the reason, a click / tap / Enter shows the reason in a bubble and announces it (`Sw.Disabled`). `button.E.disabled` still reads / sets the state. Porting: css that styles `:disabled` of a Sw control must use `:is(:disabled, [aria-disabled="true"])`; Playwright treats `aria-disabled` as disabled (a test that clicks it needs a mouse click or `Force`).
* `Menu: items | fn(button, event)` (+ `MenuOptions`): a menu button (click, pure right click, long press, Menu key, Shift+F10; `aria-haspopup="menu"`, `aria-expanded`; `OpenMenu()`). `Copy: true`: a copy button (automatic with a copy icon). `Shortcut: "Mod+S"` (or "Ctrl+S"): "(Ctrl+S)" after the `Title` tool tip and `aria-keyshortcuts` (register the key with `Sw.Keys.Add`).
* A click with Shift / Ctrl / Alt / Meta held only moves the focus (INP-2, `Sw.ModifierClick`); the same in every Sw control (Segmented, Toggle, Select, Slider, Chip, ColorPicker, list options).
* Icon only buttons with a `Title`: the title also shows on long press and keyboard focus (`Sw.Hint`).
* Methods: `SetContent(text, icon)`, `SetText`, `SetIcon`, `SetTitle`, `SetLabel`, `SetPressed`, `SetDisabled`, `SetWorking`, `SetKind`, `Click()`, `Abort()`, `CanAbort()`, and the [common methods](#common-methods-every-control).

`Sw.ButtonRow(...items)`: a wrapping, centered row (`sw-row`).

### Group
`new Sw.Group({ Title, Sub, Actions, Class, Flush, Content })`: a group of controls drawn as a card (Design v2: a flat surface with a
hairline, a header row with the title, an optional sub line and the actions on the right, then the body; no fieldset look). Append to
`.Body`; `SetTitle(text)`, `SetSub(text)`; `Head`, `Title`, `Tools`. For dashboards and pages prefer `Sw.Card` (Page anatomy below), which
has the same look and a count / tools api. `--sw-frame-bg` (the background behind a title drawn on a border) is obsolete.

### Form fields: `Label` means the same on every control
* `Label` is the **visible label**. `TextBox`, `NumberBox` and `Select` show it the same way: a `label.sw-field-label` above the control
  (small, medium weight, secondary text colour), the label names the control (`label[for]`; a Select reads "Priority, Normal"), and
  `Hint` adds a line under it (`.sw-field-hint`, the control's `aria-describedby`). `E` is then the field (`div.sw-field`); `Input`
  (TextBox, NumberBox) / `Button` (Select) is the control itself.
* `AriaLabel` names a control without a visible label, for a row that is labelled already (a property editor row, a table toolbar).
  Without `Label` or `AriaLabel` the old names stay (`Title`, the placeholder).
* `Toggle`: `Label` is the visible text beside the switch (it is the switch's name).
* Any other control (a `Slider`, a `Segmented`, a custom element): `new Sw.Field({ Label, Hint, Control, Width, Class })` gives it the same
  field (`Control` is an element or a component; its `Input` / `Button` / `E` is named by the label: `label[for]`, or `aria-labelledby`
  for elements that are not labelable). Methods `SetLabel`, `SetHint`.
* In a dialog every field has a label: `new Sw.TextBox({ Label: "Customer" })`, `new Sw.Select({ Label: "Priority", ... })`,
  `new Sw.NumberBox({ Label: "Quantity" })`; fields stretch to the dialog width, a select / number box keeps its own width under its label.
* Porting: an existing `NumberBox({ Label })` that meant only the accessible name (inside a labelled row) becomes `AriaLabel` (PORTING.md).
* Required fields and validation (the same on TextBox, NumberBox, Select, Toggle and Segmented): `Required: true` (or the message) puts a
  red asterisk after the label (`.sw-field-req`, hidden from screen readers; the control has `aria-required`). `Validate()` checks now
  (Required, then the control's own `Validate(value)` option), shows the message under the field (`.sw-text-error`, `aria-invalid`, linked
  by `aria-describedby` after the hint, so a screen reader reads the reason with "invalid") and returns the message or null;
  `SetError(text)` shows a message of your own (ex: from the server). TextBox and NumberBox also check on change; typing, a pick or a
  `Set()` checks a shown message again. Empty messages: "Enter a value" (TextBox, NumberBox), "Choose a value" (Select, a Segmented with
  `Value: null`), "Turn this on to continue" (a Required Toggle must be on, ex: accepting the terms). `Sw.Form` (page.js) calls `Validate()`.
* A `Segmented`, a `Toggle` or a button row (`sw-row`) in a `Sw.Field` keeps its own width under the label (it is not stretched to the field).

### Toggle (switch)
`new Sw.Toggle({ Label, Value, Title, Disabled, Required, Validate(v), OnChange(bool) })`, `Get`, `Set`, `SetLabel`, `SetDisabled`. Space / Enter
flip it. `Required`: the switch must be on (a marker after the label, `aria-required`, "Turn this on to continue" under it).

### Segmented
`new Sw.Segmented({ Items: [{ Value, Text, Icon, Title, Disabled }], Value, AriaLabel, Small, Class, Required, Validate(v), OnChange(value, item) })`:
a few mutually exclusive options side by side (a radio group; arrows, Home / End move and select). `Get`, `Set`, `SetDisabled`. A visible
label: put it in a `Sw.Field`.

### Slider
`new Sw.Slider({ Min, Max, Step, Value, Default, Log, Vertical, Fader, Center, Accent2, Title, AriaLabel, Disabled, OnStart, OnInput, OnChange, OnEnd })`
* Dragging the thumb is relative (no jump), tapping the track jumps there. Double tap resets to `Default`.
* `Log`: logarithmic (`Min` > 0), snaps to 2 significant digits and to `Center`. `Center` draws a mark. `Vertical` fills the container's height.
* Keyboard: arrows (Shift = ×10), PageUp / PageDown, Home / End. `Set` is ignored while the user drags (pass `force`). `SetRange`, `SetDisabled`, `.Dragging`.
* `Fader`: a mixer cap across the track (a tall cap on a horizontal fader, a wide one on a vertical fader) whose line marks the value, no fill (crossfaders, channel faders, scrub handles). Size it on a page scope with `--sw-fader-cap` (across the track) and `--sw-fader-cap-w` (along it).

### NumberBox
`new Sw.NumberBox({ Min, Max, Step, Value, Width, Label, AriaLabel, Hint, Title, Disabled, Required, Validate(v), Format(v), OnChange })`: commits on Enter / blur, arrows step; Escape keeps the typed text (as the original inputs; `EscapeReverts: true` reverts it). `Get()` returns the number, also while it is still being typed (a form read on a button click), and null for an empty box (`Value: null` or `Required`).
`Label` is visible (see Form fields; `.Input` is the input). The box grows with a long value (`field-sizing: content`, at least 6em); without
a `Step` the value is shown as it is ("1", "0.25"), not as "1.000".

### TextBox
`new Sw.TextBox({ Value, Placeholder, Label, AriaLabel, Hint, Title, Type, Multiline, Rows, Clear, Reveal, Copy, Icon, MaxLength, AutoComplete, InputMode, SpellCheck, Width, Disabled, ReadOnly, Mono, Required, Validate(v), OnInput, OnChange, OnEnter })`
* `Clear`: a clear button, `Reveal`: show / hide a password, `Icon`: a leading icon, `Validate`: an error text or null (checked on change).
* `Copy`: `true`, or what it copies (ex: `"user ID"`: "Copy the user ID", "user ID copied"): a small copy button in the box (`Sw.CopyButton`, class `sw-text-copy`, the right most button; property `CopyBtn`). It is always there, so nothing moves while typing (LAY-9): while the box is empty it is disabled with the reason "There is nothing to copy yet". A mouse / touch helper, not a tab stop (the keyboard copies in the box with Ctrl+C). Never on a password (a developer warning; no button).
* Escape keeps the typed text, as the original inputs did (it goes on: it closes a dialog, see Dialogs). `EscapeReverts: true` makes Escape revert uncommitted typing instead (only then the event is stopped, so Escape in an unchanged box still closes a dialog); the revert dispatches `input` like typing (a dialog and the page compare the value again).
* `Draft: "context"` (or `{ Context, Dirty }`): the text is kept as a draft while typing and restored on the next load (UX guide SAF-11, `Sw.Draft`); the "Draft restored" notice goes on its own line under the field row. `ClearDraft()` after the text was sent or saved (`Sw.Form` does it); `Destroy()`, or the box leaving the page (a closed dialog), disposes the draft handle. Never for passwords.
* A valid commit, or `Set()` from code, clears a shown message (also one set by a form, FRM-3).
* Unsaved typing marks the page dirty only with `Dirty: true` ([unsaved edits](#unsaved-edits-of-the-controls-saf-10); the original never asked).
* `.Input` is the input element. Methods: `Get`, `Set`, `Commit`, `Focus(select)`, `SetError(text)`, `Validate()`, `SetDisabled`, `ClearDraft()`, `IsDirty()`, `MarkClean()`.

### Select
`new Sw.Select({ Items: [{ Value, Text, Icon, Hint, Title, Disabled }], Value, Placeholder, Small, Title, Width, Search, SearchFrom, Label, AriaLabel, Hint, Disabled, Required, OnChange })`
(`Label` / `AriaLabel` / `Hint`: see Form fields; `.Button` is the button.)
A button that opens a list: a menu for short lists, a filter box + list for long ones (`SearchFrom`, default 12; or `Search: true`).
Keyboard: Enter / Space / Down open; in the list arrows, Page Up / Down, Ctrl+Home / End, Enter, typing filters. `SetOptions(items, value)`, `Get`, `Set`, `SetDisabled`, `Open()`, `Validate()`, `SetError(text)`.
`Sw.ListPicker(anchor, items, value, onPick)` shows the searchable list alone.

### ColorPicker
`new Sw.ColorPicker({ Value: [r, g, b(, a)] | "#rrggbb", Hex, Title, Disabled, OnInput, OnChange })`: a swatch; the popup has a
saturation / value square, a hue bar (both keyboard operable) and a hex field. HDR colors (components > 1) get an intensity slider.

### Badge, Chip
`Sw.Badge({ Text, Kind, Title })` (`Kind`: ok, danger, warning, info, accent, accent2) → span. Status badges are calm (Design v2 3.10): a
20 px soft tonal pill, 12 px 500, no frame, and the status by icon + text: ok / warning / error / info get a 12 px line glyph (check,
triangle, cross, i) before the text. Counts (`class sw-count`) and the dot variant (`sw-dot`, live states such as "Running") have no glyph;
`sw-noicon` opts out. Never use a solid status fill for a state that most rows share ("Enabled" on every row); a table Toggle cell is a
small tonal switch + text with no frame at rest (ui/cells.css).
`new Sw.Chip({ Text, Icon, Title, Pressed, Pressable, Removable, OnChange, OnRemove, OnClick })`: a rounded toggle; removable chips have an × (Delete / Backspace too).

### ProgressBar, Spinner
`new Sw.ProgressBar({ Value, Text, Title, Indeterminate, Small, AriaLabel })`: `SetValue(current, max, text, title)` (templates `{0}` percent,
`{1}` current, `{2}` max), `SetPercentage(value, text, title)`, `SetIndeterminate(bool)`. The text stays readable over the filled part.
`new Sw.Spinner({ Size, Title, Class })`.

### Common methods (every control)

The controls share one method set, so form code can treat them alike (a class keeps its own version where it has one, ex: `Button.SetLabel`):

| Method | Button | TextBox | NumberBox | Select | Toggle | Segmented | Slider | ColorPicker | Chip | Field | ProgressBar | Group, Spinner |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `Get()` / `Set(value, silent)` | - | yes | yes | yes | yes | yes | yes | yes | yes | - | - | - |
| `SetDisabled(bool, reason)` (aria-disabled) | yes | yes | yes | yes | yes | yes | yes | yes | yes | - | - | - |
| `Focus(select)` | yes | yes | yes | yes | yes | yes (the checked one) | yes | yes | yes | yes (its control) | - | - |
| `SetLabel(text)`: the visible label (the required marker stays), else the accessible name | yes | yes | yes | yes | yes | yes | yes | yes | yes | yes | yes | - |
| `SetError(text)` / `Validate()` | - | yes | yes | yes | yes | yes | yes | yes | yes | - | - | - |
| `Destroy()`: removes the element, its message and an open popup | yes | yes | yes | yes | yes | yes | yes | yes | yes | yes | yes | yes |
| `Locked(bool)` / `Locked()`: no value change by the user while work runs (see below) | yes | yes | yes | yes | yes | yes | yes | yes | yes | yes (its control) | - | - |

`Validate()` checks `Required`, then the `Validate(value)` option, shows the message and returns it (null when valid). `SetError(text)`
shows a message of your own; it is linked by `aria-describedby`, with `aria-invalid`. Option names here are the canonical ones; the
accepted aliases are listed once, in the [Naming rules](#naming-rules-one-name-per-concept).

**Locked (busy work: UX guide POP-6, FRM-5, FRM-8).** `control.Locked(true)` makes a control read only for the user while work runs
(a dialog's save calls it on every control in its body, `E.SwControl`): no change by keyboard (a NumberBox's arrows / PageUp too),
pointer, wheel, paste or drop, and an open list / picker closes; the focus, the caret, selection and copy stay, Tab and Escape pass,
nothing moves (text fields are `readonly`, the others `aria-readonly`). A refused change fires a bubbling `sw-locked` event
(`detail { Control, How: "key" | "pointer" | "wheel" | "input" }`): a dialog shows its "Save is running…" line for it (it calls
`preventDefault()`), else the page announces "Port can't be changed while the work runs…" (only while work runs for it; a lock the
page keeps after its work says "Port can't be changed now."). Values set by code still apply (the work may write the server's value).
Every value control's root has `E.SwControl` (the control), so a container finds them.

**Lock for work, the scoped way (always undone).** Tie the lock to the work, so no failure can leave a control locked:
`Sw.Lock(targets, work)` (alias `Sw.Work.Lock`) locks the targets (a control, its element, a container: every Sw control in it, or an
array), runs the work (a function, or a promise) and unlocks the same controls when it settles, whatever the outcome; the work's result
or failure comes back to you. `control.Locked(true, work)` is the same for one control. A bare `Locked(true)` inside a click's work is
undone (with a developer warning) when that work fails.

```js
const port = new Sw.NumberBox({ Label: "Port", Value: 80 });
new Sw.Button({ Text: "Test", OnClick: () => Sw.Lock(port, Sw.Net.Send(api + "TestPort", { Port: port.Get() })) });
port.Set(8080);      // code still sets a locked control
```

**Developer checks of the options (DevMode).** Every control checks its options through `Sw.Work.CheckOptions` (the core's
`Sw.DevOptions` plus ui rules): an unknown option ("Lable" → did you mean "Label"?), a wrong type (`Toggle Value: "false"`, a
`Disabled` that is neither true nor a reason), colliding options (`Label` + `AriaLabel`: the visible label is not shown; `Pressed`
without `Pressable`; `Reveal` without `Type: "password"`; `OnEnter` on a `Multiline` box), a `Value` that is not one of the items
(`1` vs `"1"`), a Slider / NumberBox `Value` outside Min..Max, `ProgressBar.SetPercentage(0.4)` (percent, not a fraction). A Sw
component turned into text (`div.append(card)`, a template string) shows nothing instead of "[object Object]" and warns: append `x.E`.
A label stays on its input when page code changes the input's `id`. `Sw.Button({ Submit: true })` is the form's send button (type
submit, primary unless a Kind is given); `Hidden: true` starts hidden.
The options are checked as you wrote them (aliases are normalized after the check: `Sw.Select({ Items })` is quiet). **One registry:**
every ui api registers its schema by name with `Sw.Work.RegisterOptions(where, schema)` (the core's `Sw.DevOptions.Register`, which
`core/optionstest.html` walks): the controls, `Sw.Group`, `Sw.Spinner`, `Sw.CopyButton`, the page parts (`Sw.Card`,
`Sw.PageLayout`, `Sw.PageHeader`, `Sw.StateView`, `Sw.Meter`, `Sw.Facts` and its items, `Sw.Kpi`, `Sw.ListCard` and its items,
`Sw.Grid`, `Sw.UploadButton`, `Sw.SelectList`, `Sw.ListDetail`, `Sw.Form`, **every `Sw.Form` field** (`{ Requred: true }` → "did you
mean Required", the field would not be required), `Sw.Steps` and its steps) and the layout parts (`Sw.Tabs` and its tabs, `Sw.Split`,
`Sw.Pager`, `Sw.Columns`, `Sw.EmptyState`). A new api of your own: register its schema next to it and call
`Sw.Work.CheckOptions(where, o)` (the schema is looked up by name).

### Disabled, busy and copy (UX guide)

The controls implement these UX guide 1.0 rules; use the same helpers in your own elements.

* **Disabled (INP-12):** every Sw control's `Disabled` option and `SetDisabled(bool, reason)` use `aria-disabled` (`Sw.Disabled.Set`): the
  control keeps its place in the Tab order, looks muted, ignores activation, and an attempt shows the reason next to it and announces it.
  Text / number boxes become read only (the value can still be selected and copied; typing, paste and drop show the reason). Give a
  reason that says what enables it: `b.SetDisabled(true, _TF("Select a row first", "…"))`.
  An attempt names only the reason (never the control's normal tool tip, which says what it does); without one it says "Not available
  right now" and, on localhost / `Sw.Debug`, a developer warning names the control. `Sw.Disabled.Set` on a native `select` / `input` /
  `textarea` also blocks the keyboard (arrows, typing, paste, drop); on a non interactive element it warns.
* **Busy (FBK-1 / 2 / 3):** return the promise from `OnClick`; see Button (a forgotten `return` is caught). The same busy state on any
  element: `Sw.Work.Busy(el, promise, { Name, Leave, Kind, Work })` (table row actions, cards, tiles: label and colour kept, spinner
  after 250 ms in the icon's place or in front of the label, elapsed time in the tool tip after 2 s, never faded; `Kind: "server"` +
  `Work`: the leave question says the true consequence, `Sw.Page.WorkText`); `Sw.Work.Scope(fn)` / `Sw.Work.Promise(scope)` collect
  the requests a handler starts. **`Sw.Work.Current(el)`** → `Promise<boolean>` | null: the work running now (inside the synchronous
  part of a click's work: that work; else the busy work of a button / field in the element's form, card, section or dialog), true when
  it succeeded, false when it failed or was stopped. `Sw.Draft` clears a draft only after it (FD R4 F6: `d.Clear(); return Send(...)`
  that fails keeps the text); `Sw.Form` and a button whose successful write carried a drafted text clear the drafts after the success.
  It never waits for itself: asked for the work of the element that runs it (`await Sw.Work.Current(b.E)` in b's own OnClick) it answers
  null with a developer warning; await your own request instead (`{ Then: true }`: you only chain on it, no self rule).
  `Sw.Work.Running(el)` = the busy work of el's form / card / dialog (a check), `Sw.Work.InUser()` = the synchronous part of work the
  user started runs now. The click's signal is a `Sw.Cancel.Controller()` (a sent write is never aborted) and every recorded request is
  classified by the core's `Sw.Net.IsWrite` (a `{ ReadOnly: true }` send or `Sw.Net.Read` is a read).
* **The focus never stays on the page body (FOC-1, FOC-2):** when the focused element is removed or hidden (a form replaced by its
  result, "Invitation sent", "Account deleted") and nothing takes the focus within 300 ms, it goes to the nearest remaining part's
  result (`[data-sw-result]`, a form status, `role=status` / `alert`, a `Sw.StateView` title), else its heading, else its first
  control, with a developer warning naming it (put it there yourself: `Sw.Focus.Show(resultElement)`). Dialogs, menus, navigation,
  tables and containers that a `Sw.Focus.Keep` watches place the focus themselves and are left alone.
* **One result path (FBK-5):** for your own async handlers use the same pieces the controls use:

  ```js
  const sc = Sw.Work.Scope(signal => save(signal));          // the requests it starts join, with the signal
  const said = Sw.Work.Watch();                              // did the work show a message itself?
  const res = await Sw.Work.Busy(el, Sw.Work.Promise(sc), { Name: "Save" });
  const out = Sw.Work.Outcome(sc, res);                     // { Ok, Aborted, Error, NotDone, Wrote, Value, Said }
  Sw.Work.Report(out, { Name: "Save", Said: said.Stop(), Spun: res.Spun });   // once: the error / "not done" / "stopped" / "done"
  ```

  `Report` shows one result per outcome: a second `Report` of the same outcome shows nothing and gives a developer warning.
  `Sw.Work.Error(x, { Name, Button })` is the one failure display (role refused, lost connection, the reason; the sentence is
  `Sw.Work.FailText(name, x)`); `Sw.Work.Record()` records the writes started anywhere until `Stop()` (how a button is recognized as a
  save; `Detached` writes are listed with `Detached: true`).
* **Clickable elements (INP-1, 2, FBK-2):** `Sw.OnClick(el, (ev, ctx) => work, { Name })` (`ctx.Signal`: aborted when the work is cancelled) instead of `addEventListener("click")`:
  Enter / Space (a Tab stop), pure clicks only (a modifier click moves the focus there), the disabled reason, busy while the work
  runs, errors as a toast. `Sw.Card({ OnClick | Href })` uses it (the title becomes the button).
* **Destructive actions (SAF-1 / 2 / 3):** `Sw.Danger.Run({ Action: "Delete", Target: 'the email "Test"', Consequence, Confirm, Run,
  Button, Focus, Prepare })`: the confirmation (Cancel focused, a verb button), then the work with the busy state on its button, one result, the
  focus kept. An action can't skip its confirmation this way. `Sw.Danger.Confirm` = `Sw.Cells.ConfirmDanger`.
  **`Prepare: signal => promise`** fetches what the confirmation needs first (a one time delete token with facts): it runs outside the
  click's work (the click only asks: no "Delete: done", no "returned no promise"), with the busy state on `Button` at once, or, when the
  button is hidden (a "More actions" menu), a small working dialog with Cancel after 300 ms; its result's `Title`, `Item`, `ItemLabel`,
  `Text`, `Warning`, `Facts`, `Confirm`, `OkText` fill the confirmation, and `Run(signal, prepared)` gets it (`ConfirmDanger` resolves to
  it when confirmed). **A failed Prepare (an error, a 403, no answer) shows the reason and opens nothing: never a weaker confirmation.**
  A confirmed `Run` whose button is hidden shows a working dialog too. The click handler doesn't return the promise:
  ```js
  const del = new Sw.Button({ Text: "Delete", Kind: "danger", OnClick: () => { Sw.Danger.Run({ Action: "Delete", Title: "Delete the log file?",
      Confirm: "delete", Button: del, Prepare: async signal => { const t = await Sw.Net.Send(tokenApi, null, { Signal: signal });
          if (!t || !t.Token) throw new Error("the server gave no delete token"); return { Token: t.Token, Item: t.Name, Facts: facts(t) }; },
      Run: (signal, p) => Sw.Net.Send(deleteApi, { Token: p.Token }), Done: "Log file deleted" }); } });
  ```
* **Developer warnings:** one channel, `Sw.DevWarn(message, key)` (core/base.js: console "[Sw UX] …", once per key) gated by one switch,
  `Sw.DevMode()`: on for localhost or `Sw.Debug`, off on any other host (users never see them); `localStorage["SysWeaver.DevWarn"] =
  "0"` silences them on localhost (a quiet demo), `"1"` shows them on a production host; a page may replace it (`Sw.DevMode = () =>
  false`). The controls warn for an `OnClick` that started a request but returned no promise, a `false` result that showed nothing, a
  disabled control without a reason, a copy button without a value, a learned hint key used twice, an unknown icon name, a duplicate or
  browser-reserved `Sw.Keys` combination, a page `SetDirty` for an edit the controls / `Sw.Form` track already, a `Draft` on a field
  that looks secret, a button that sent data but did not clean the edits around it, and a lost focus `Sw.Focus.Keep` could not place.
* **Modifier click (INP-2):** `if (Sw.ModifierClick(ev)) return;` (or `Sw.BadClick(ev)`) in every click handler of your own.
* **Menu buttons (INP-3):** `Sw.Button({ Menu })` or `Sw.MenuButton(el, items)`; never `contextmenu` + `preventDefault` by hand (use
  `Sw.LongPress`, which leaves a modifier right click to the browser). A button that opens its menu from its own click handler
  (`Sw.Menu.Show` in `OnClick`, page markup) only declares `aria-haspopup="menu"`: the library then turns a pure right click (and the
  Menu key, Shift+F10) on it into a click, so every menu button answers the right mouse button.
* **Copy (INP-31):** `Sw.CopyButton({ Value, What })` next to the value; `Sw.Clipboard.CopyOnClick(el)` for a value without a button
  (a plain click copies, as the original; also Ctrl+C and its menu). A secret is never quoted (SEC-1): `Secret: true` on `CopyButton` / `Clipboard.Copy`,
  and a value that looks like one (an api key `name:key`, a token, a long random string, a password field: `Sw.Clipboard.LooksSecret`)
  is announced as "Copied text to the clipboard" (with `What`: "API key copied"); `Show: true` quotes it anyway. A copy button with
  `Get` announces only "API key copied" (no "done"); an empty value is not copied (an error, no "Copied").
* **Touch targets (INP-24):** in the touch density small buttons, chips, segments, tab close and field buttons get a 44 px hit area
  (an invisible `::after`), the visual size stays.
* **Usage hints (TIP-8):** the ui library has no usage hint component: the learned hints (`Sw.Help.Learned`, "Show hints again") were
  removed (owner, OWN3-Shell-4: no page used them). Make the UI explain itself; instructions a field needs go into the field `Hint`.

### Unsaved edits of the controls (SAF-10)

**Opt in (parity with the original, P3-A-4):** the original pages never asked "Leave site?", so a control marks the page only with
`Dirty: true` (or the leave sentence). TextBox, NumberBox, Select, Toggle and Segmented with it mark the page dirty by themselves
(`Sw.Page.SetDirty`), so leaving with unsaved edits asks "Leave without saving? Your changes to Mail settings are lost." without page code:

* Dirty = the user changed the value away from its base: the `Value` option, or the last `Set()` from code (loading data is no edit;
  changing it back is clean again). One sentence per container: the closest card / group / form / section title
  (`data-sw-dirty-group="the invoice"` names it yourself).
* Clean again when saved, by **any** save path: a primary (or `Saves: true`) button's action in the same container ends without an
  error; any other button, or Enter in a TextBox with `OnEnter`, whose work succeeded and sent the edited values; a `Sw.Form` submit
  that succeeded (also the controls around it); an `OnChange` that returned a promise or started a request (an auto saving control)
  succeeded; `control.MarkClean()` / `Sw.Edits.Saved(button)`; the control left the page.
* **An auto save that fails** (the `OnChange` threw, rejected, a request it started failed, or it resolved `false`): a picked value
  (Toggle, Select, Segmented, NumberBox) goes back to the saved one, a typed text stays (it is the user's work), and the reason is shown
  once: at the control when it is on the screen ("Weekly digest: not saved. Save failed (500). The previous value is back.", its message
  line, made an assertive live region for it: said once), else as a toast; nothing more when the handler showed it itself. The
  control is `aria-busy` while it saves.
* **One save at a time per control, the latest value wins** (fast clicks / keys on an auto saving switch): a change made while the
  control's save runs is not sent at once; when the save ends only the newest waiting value is sent (unless it is the server's value
  already), so the shown value is always the one the server ends with; a failure of the last save puts the server's value back and
  says why.
* **An opted-in control is tracked whenever the user changes it** (S5-A-4): there is no save-path detection (the automatic
  "is there a Save on the page" rules, the untracked-edit warning and the learned save button went with the opt in). **Dirty belongs to
  the page** for the cleaning (A3-10): any button whose work succeeded and sent the edited values cleans them, also those of another
  card (texts of 3+ characters and objects, which a payload carries clearly) and a `Sw.Form`'s fields (`form.SentBy`); a button whose
  write carried none of them leaves them dirty with a developer warning ("mark it `Saves: true`, or `Saves: false`").
* **Only the user's changes are edits** (FD R4 F2): a value written by code (`Set()`, `.value`, a dispatched event: the server's
  customer number after Create, a normalised value) is never an edit and never a "newer edit"; `IsDirty()` is true only after a user
  change. `Sw.Edits.UserSince(root, since)` = the user changed a field / control inside `root` after `since` (a `performance.now()`
  time: trusted `input` / `change` events and the Sw controls' user changes), `Sw.Edits.UserAt(root)` = the time of the last one (0 =
  never). Dialogs use it for "your newer changes are not saved yet".
  **Except code that acts for the user:** a `Set()` inside the synchronous part of work the user started (a `Sw.Button` OnClick,
  `Sw.OnClick`, Enter, a pick: `Sw.Work.InUser()`) is the user's edit, like typing: a "Same as billing" button that copies the address
  with `ship.Set(bill.Get())` makes the card dirty, and leaving asks first. Not in dialogs, toolbars and tables (a dialog compares its
  own values; a value its work writes is no newer edit). Say it explicitly with the second argument: `Set(v, { User: false })` (loading
  data inside a click: the new base), `Set(v, { User: true })` (always the user's); `{ Silent: true }` = the old `Set(v, true)`.

  ```js
  new Sw.Button({ Text: "Same as billing", OnClick: () => ship.Set(bill.Get()) });   // dirty: the user asked for it
  new Sw.Button({ Text: "Reload", OnClick: async () => { const a = await load(); ship.Set(a.Ship); } }); // after an await: data, clean
  ```
* **An auto save that never answers** (FD R4 P7): after `Sw.Edits.SlowMs` (10 s) the control says "Weekly digest: still saving…" (a
  muted line under it, `.sw-ctl-status`) and a developer warning asks for a time limit; after `Sw.Edits.GiveUpMs` (30 s) it is a
  failure like any other ("not saved. There was no answer from the server. The previous value is back."), the newest waiting change
  goes next; an answer that still comes later and succeeds puts the value back ("saved after all").
* **A locked field offers no draft Restore** (Admin N12): a TextBox with `Draft` that becomes disabled / read only (ex: a sent
  invitation) locks its draft (`handle.Lock(true)` when `Sw.Draft` has it, else its draft notices are hidden while locked).
* Not tracked (not data the page will save): `Dirty: false` (also marks the root `data-sw-nodirty`, so a dialog never asks "Discard
  changes?" for it: a type-to-confirm word); a **live filter** (a control with `OnInput` and no `OnChange` / `OnEnter`); a control
  whose card / form has **no button** while the page has no save path (a calculator, a tool); controls of a `Sw.Form`
  (the form tracks them) or inside a dialog (the dialog asks, POP-6); search boxes (`Type` / `Icon` "search"), passwords, read only
  boxes, a TextBox with `Draft` or drafted by hand (`Sw.Draft.Attach(box.Input)`: the draft keeps the text); a value put in by code
  (an `input` event that is not the user's, ex: a draft restore); a committed value whose `OnChange` returns nothing (it applies at
  once: view options, settings); controls in toolbars, page headers, card tools, tables, `[data-sw-nodirty]`; and every control
  without `Dirty: true` (or a text), the default.
* A page that also calls its own `Sw.Page.SetDirty` for the same edit gets a developer warning (the leave question would say it twice):
  remove it, or give the control `Dirty: false`.
* Every user change dispatches a bubbling `sw-change` event on the control's `E`: `detail { Control, Value, Changed, NoData, User }` (Changed =
  differs from the base and is user data; `NoData` for `Dirty: false` / `data-sw-nodirty`; `User: true` for the user's change, `User:
  false` for the framework's own write, ex: a failed auto save putting the saved value back; a `Set()` from code fires nothing); dialogs
  use it to compare with the opening values. `IsDirty()`, `MarkClean()`, `Pick(value)` (select as the user did) on each of these controls; `Sw.Edits.IsDirty()` for all.

### Popup
`Sw.Popup.Show(anchor, content, { Side, Align, Gap, Dim, Class, MinWidth, Modal, NoEscape, NoLayer, NoTrap, Focus, Role, Label, OnClose, Owner })`
Shows content next to an element (or `{X, Y}`, or null = centered), keeps it on screen (flips sides), re-places on resize, traps the focus.
Tapping outside / Escape closes it (unless Modal / NoEscape). Returns `{ Box, Close(), Place() }`. `Sw.Popup.CloseAll()`, `Sw.Popup.IsOpen()`.
* **A popup belongs to its owner (UX-POP-5):** the open pop-up or dialog its anchor is in (or `Owner`: an element in it, a dialog handle, a
  popup; `null` for none). It sits just above its owner, its outside layer covers the owner too (a press on the dialog behind a menu closes
  the menu and does nothing else) and it closes with its owner (a dialog closed by its ×, its work or code takes its menus and questions
  with it). Closing gives the focus back to the opener (a component root gives it to its first control, `Sw.Focus.Restore`).
`Sw.Place(box, anchor, opts)` positions any fixed element; `Sw.Track(anchorOrMouseEvent, box, opts)` keeps it there while scrolling (returns stop).
* **Outside press (UX-POP-2):** a mouse / pen press outside closes on `pointerdown`; on touch only a tap (released within 10 px) closes, so a
  scroll gesture doesn't. The rest of that pointer's events (`pointerup`, `click`, `contextmenu` …) are swallowed: nothing behind acts.
* **Escape (UX-POP-3):** a visible tool tip / hint bubble (`Sw.Hint`) takes the first Escape, then fields, then the top pop-up level.
* **Back (UX-MOB-3):** `Sw.Popup.Depth()` (open levels; the top window adds its framed pages' levels), `Sw.Popup.OnDepth(fn)` (returns
  remove), `Sw.Popup.CloseTop(kind = "back")`: closes the top menu / popup, or makes a close attempt on the top dialog (a dirty one asks,
  a busy one shows the rejection cue); in the top window it reaches a framed page's pop-up. Returns true when a pop-up was open (Back used up).
  ```js
  window.addEventListener("popstate", e => { if (Sw.Popup.CloseTop("back")) history.pushState(null, ""); });  // shell keeps one entry while Depth() > 0
  ```

### Menu
`Sw.Menu.Show(anchor, items, { Side, Align, MinWidth, NoFocus, Focus, DrillDown, Label, OnClose, Owner })`
Items: `{ Text, Icon, Hint, Title, Desc, Checked, Radio, Disabled, Danger, Keep, Class, OnClick(item, event), Items }` (`Desc`: a muted second line under the text), `"-"` (separator),
`{ Title }` (heading). `Items` (a submenu) may be an array or a function returning an array / a promise of one.
* **A menu is its own pop-up level, always** (also inside a dialog): one outside press closes it and does nothing else (POP-2: a click on
  the dialog's body, Close or × behind the dialog's ⋮ menu only closes the menu); it belongs to the pop-up its anchor is in and closes with
  it (never an orphan menu); an anchor has one menu at a time (showing it again, ex: a synthetic second click on ⋮, closes the open one;
  `aria-expanded` follows on an anchor with `aria-haspopup`). Every level leaves with its exit transition (`Sw.Motion.Out`).
* **One modal layer across frames:** a menu or pop-up open in a framed page also covers the app shell around the frame (transparent press
  catchers in the top window): a press on the shell header or sidebar only closes the menu and does nothing else; the second press acts.
  Nothing to do for pages (the framed page tells the shell `SwPopupDepth { Count, Dialogs }`; the shell answers `SwPopupOutside`).
* Submenus cascade to the side on wide screens with a mouse (hover opens them) and drill down (a back row) on touch / narrow screens.
* Keyboard: Up / Down / Home / End, type ahead, Enter / Space, Right opens a submenu, Left / Backspace / Escape go back, Tab closes.
* `Checked: true/false` makes a check item (`Radio: true` a radio item); `Keep` items stay open (check items flip in place).
* **Control rows** (the menu stays open while they are changed): `{ Text, Title, Icon, Disabled, Slider: { Min, Max, Step, Value, Default, Log,
  Format(value), OnInput(value), OnChange(value) } }` (an `Sw.Slider`, `Format` adds the value text after it) and `{ Text, ..., Input: { Value,
  Placeholder, Type, Width, OnInput(value), OnChange(value) } }` (a text box; `OnChange` on Enter, which also closes the menu, or when the box
  is left). Up / Down move to the next item, the control keeps Left / Right, Home / End, typing and Backspace.
* `Class` (a class of the menu) is also on every sub menu level (ex: a theme scope).
`Sw.Menu.Context(element, itemsOrFn, opts)`: a context menu on long press, right click, the context menu key and Shift+F10.
`Sw.Menu.Appearance({ Theme, Density, TextSize, Hacker })`: the Theme (Automatic, the built in themes, the themes registered with a `Title`), Density and Text size submenus as radio items, for pages with their own menu instead of the shell's settings menu (an app page, a console, a kiosk): `Sw.Menu.Show(b, [..., "-", ...Sw.Menu.Appearance()])`. Leave a part out with `false`; `Hacker: true` lists the Hacker theme (the server shows it to Debug users only).

### Dialog
* `await Sw.Dialog.Show({ Title, Icon, Content, Buttons: [{ Text, Kind, Icon, Title, Label, Labels, Value, Close, Dismiss, Disabled, WhileBusy, Cancellable, OnClick(close, value, { Signal, Dialog }) }], Id, NoMove, Wide, Full, Close, Modal, NoEscape, EnterValue, Focus, TouchFocus, AutoDirty, BusyButtons, Above, Class, AriaLabel, Role, Describe, Owner, EditWhileBusy, OnShow(close, box, body), OnClose(value) })`
  resolves to the button's `Value` (null when dismissed). `Content` may be a node, a text, or a function `(body, close, tools)` returning a
  node or a promise of one (`tools`: the header area for extra buttons). A button `OnClick` returning false keeps the dialog open;
  an `EnterValue` function returning undefined keeps it open too. A button's `Disabled` may be the reason as a text (disabled with that
  reason: `Disabled: "Type anna to confirm"`; change it later with `Buttons[i].Button.SetDisabled(on, reason)`). `Owner` (a dialog handle /
  an element): this dialog closes with that one (a question about its state). An unknown option or button key (ex: `Default: true`) gives
  a developer warning.
* **Developer warnings** go through `Sw.DevWarn` (localhost or `Sw.Debug`, `[Sw UX]`, never in production): a dialog without a title, two
  open dialogs with one `Id`, a Confirm whose button says "OK", `SetProgress` outside 0..1, an unknown option key, a footer button that
  moved, a title bar control that moved while busy or while the line shows, a footer button that got a longer label (declare `Labels`), a
  control in the body without `Locked(bool)`, a footer button whose OnClick sent a request without returning it, and an overlay (the
  rejection line, a toast, any `Sw.Overlay` registration) that covers a control.
* Accessible name and description: `aria-labelledby` is the title, `aria-describedby` the message: by default the whole body of an
  `alertdialog` (Confirm, Alert, the table action confirmation), else the first `.sw-dialog-text` of the body (Prompt's `Text`).
  `Describe` overrides it (an element or a selector inside the body; `false`: no description, ex: a body with a long list).
  The server unavailable overlay is labelled by its title and described by its text the same way.
* Sizes and colors: a dialog is 500 px wide (`Wide`: 1100 px) times `--sw-scale` (it grows with the text on large screens), at most the window width minus 32 px; the title (`h2.sw-dialog-title`) is in the normal text color (`--sw-fg`), not the theme's heading accent.
* `Sw.Dialog.Open(options)` → `{ Box, Body, Tools, Id, Close(value), Result, SetDirty, IsDirty, Recheck, Rebase, SetBusy, IsBusy, SetProgress, Attempt, OnClose(fn), Own(x) }` (`Close` from code always closes; see "Dirty, busy and close attempts"). The promises of `Show`, `Confirm`, `Prompt` and `Alert` carry `SwDialog = true` (waiting for the user, not work: `Sw.Button` shows no busy state for them).
* `Sw.Dialog.Of(node)` → the handle of the open dialog that holds a node (or null): components bind to their dialog with it (`h.OnClose(({ Value, Discarded }) => ...)`: `Discarded` is true after "Discard changes?" → Discard; `h.Own(x)` disposes a function / an object with `Dispose()` when the dialog closes). `Sw.Dialog.OpenSince(t)`: a dialog opened at or after `t` is open; `Sw.Dialog.OnChange(fn)`: a dialog opened or closed.
* `await Sw.Dialog.Confirm(title, text, okText, { CancelText, Danger, Safe, OkIcon, CancelIcon, OkTitle, CancelTitle, Target, Id, Owner })` → bool (or the options form `Confirm({ Title, Text, OkText, Danger, … })`: its own schema `"Sw.Dialog.Confirm({...})"`, so an unknown name there lists `Title`, `Text` and `OkText` too, and `SwWeb check --design` reports it, rule `design-confirm-options`). **Cancel has the focus and Enter never confirms** (Enter on Cancel cancels, UX-SAF-3), with or without `Danger` (a danger OK button and a warning icon, as `Sw.Cells.ConfirmDanger`); only `Safe: true` (a harmless, undoable action) focuses OK and confirms on Enter. `okText` is the verb ("Archive"); a missing one or "OK" / "Yes" / "No" gives a developer warning (POP-9). With no title and a `Target` the title is `DangerTitle(okText, Target)`. `Owner`: the confirmation closes with that dialog.
* `Sw.Dialog.DangerTitle(action, target)`: the title of a destructive confirmation, the action and its target:
  `DangerTitle("Kill", "Name: devenv, Id: 33936")` = "Kill devenv (33936)?", `DangerTitle("Delete", "LogFile.txt")` = "Delete LogFile.txt?".
  `Sw.Dialog.ShortTarget(item)` makes the short name ("Label: value" parts → the first text, then the first number in brackets).
  `Sw.Cells.ConfirmDanger({ Action, Target, ... })` uses it when there is no `Title`, and every table cell action with a known row does
  (`Kill`, `End session`, `Remove`: the server's short question, "Kill the process?", is then left out).
  `Confirm: "anna"` adds a type to confirm input ("Type anna to confirm", focused): the danger button stays disabled until the text matches
  (for actions that delete a lot, ex: an account and all its data).
* `await Sw.Dialog.Prompt(title, value, placeholder, { Text, Label, Multiline, Type, Validate, OkText, CancelText, Mono, Id })` → text or null.
* `await Sw.Dialog.Form(title, defs, values, { Text, OkText, CancelText, Id, Wide, Validate, OnOk, OnChange, OnReady })` → all values or null: a form of
  `Sw.PropertyEditor` definitions (`Type: "string"` for texts, `NoSlider` numbers, `Help` lines), starting unmodified. `Validate(values)` returns
  null, `{ key: message }` (under the fields) or a text (a toast); `OnOk(values)` does the work (a promise makes the dialog busy; `false` or
  `{ key: message }` keeps it open, a thrown error is shown); `OnReady(editor, values, form)` / `OnChange(key, value, values, editor)` enable or hide fields (`SetEnabled`, `SetVisible`), `form.SetOk(enabled, reason)` enables / disables OK (disabled it stays reachable: a click or Enter shows the reason, `Sw.Disabled`); `form.Set(values)` sets fields as if typed (ex: a one click fix); `After` = an element under the fields (ex: a preview).
* `await Sw.Dialog.Alert(title, text, okText)` (the button reads "Close" by default).
* `await Sw.Dialog.Select({ Title, Tip, Placeholder, StartText, GetAll, Items(filter, close) })` → the picked value (items: elements or `{Text, Hint, Icon, Value, Title}`).
* `const close = Sw.Dialog.Working({ Title, Text, Icon, Work, Cancel, Background, Promise, Progress, CloseText, Stop, Key, Id, NoMove })` (`NoMove`: no dialog menu, for a short wait before a question): a busy
  dialog (spinner, text, time so far). **`Work: ({ Signal, Progress }) => promise`** starts the work: reading `Signal` (pass it to
  `Sw.Net.Send(url, data, { Signal })` / `fetch`) makes it cancellable, so Cancel is offered and aborts it; without a cancellable job no
  Cancel is shown. `Cancel()`: an own stop function. `Promise`: closes by itself when it settles. `Background` (default true with a
  `Work` / `Promise`): "Continue in background" (only where the activity list `Sw.Activity` exists; `Background: false` turns it off).
  With no way out a Close naming the consequence (`CloseText`; default "Close (the work stops)" with `Stop`, else "Close (the work still
  finishes)") is added after 10 s, left of the other buttons. `close.Handle`, `close.SetText(t)` (a phase change, announced),
  `close.SetProgress(0..1, text)`. Old calls (`Working(title, text, icon)`) keep working.
* `Sw.Dialog.Top()` → `{ Box, Attempt(kind), Handle }` of the top dialog (or null); `Sw.Dialog.LiveHost()`: where live regions belong (the top dialog, else the body).
* `new Sw.SearchSelection({ Id, GetId(data), Render(data, select, isRecent), Find(filter, recentMap, count, offset), MaxRecent })`, `await sel.Show({ Title, Tip, Placeholder, PageSize, OnSelect })`:
  a paged searchable selection that remembers the filter and the recently selected items (local storage `SysWeaver.PopUpRecent.<Id>`).
On narrow screens dialogs are sheets (full width at the bottom, a handle), on low screens (landscape phones) they fill the width, head and foot compact.
**Touch:** a dialog opened by touch never focuses a text field (the on-screen keyboard would pop up unasked, FOC-2): the dialog itself
(its title is read) or Cancel of a danger dialog gets the focus; `TouchFocus: true` keeps the field focus (a search picker).
**Leaving:** every overlay leaves with its theme transition (`Sw.Motion.Out`, below): a closed dialog is `inert` at once and
`.sw-dialog.sw-leaving` until its fade / slide ends (tests that count dialogs use `.sw-dialog:not(.sw-leaving)` or `Sw.Popup.IsOpen()`).

#### Dirty, busy and close attempts (UX-POP-6)
`Sw.Dialog.Open` returns `{ …, SetDirty(bool), IsDirty(), SetBusy(promise | bool, o), IsBusy(), SetProgress(value, text), Attempt(kind) }`.
* **Dirty = user input only:** a value the user changed away from its opening value; **no `SetDirty` is needed for fields and Sw
  controls**. A field's opening value is taken right before the user's first input on it (a press, a key, text input, paste, drop; a
  focus alone is none), so values a page sets from code (also an async fill after the dialog opened) are opening values: a `Set()` from code,
  a native `input` / `change` event the page dispatched, a switch / select state changed without input on that control. Native inputs /
  selects / text areas are compared by value, Sw switches / sliders / selects by their state (`aria-checked`, `aria-valuenow`, the select
  label; a control that sets `E.SwControl` by its `Get()` value), Sw value controls by their own verdict (`sw-change` `Changed`):
  switching a toggle on and off again is clean. **Not user data, never tracked:** `type=search`, `data-sw-nodirty` on the field, its
  control or anywhere on the event's path, a control with `Dirty: false`. `h.Recheck()`: values set from code since the open count as the
  user's (a restored draft); `h.Rebase()` after a save that keeps the dialog open; `SetDirty(v)` takes over (automatic tracking off).
  **Confirmations are never dirty:** `Role: "alertdialog"` (Confirm, Alert, `Sw.Cells.ConfirmDanger` with its type to confirm input) has
  `AutoDirty: false` by default (`AutoDirty: true` for an alertdialog with real input). A save closes without asking.
* **One leave sentence per dialog:** the dialog reports its whole state to the page as one entry (unsaved input only after the page's
  own `handle.SetDirty(true)`; the automatic field tracking asks "Discard changes?" inside the dialog but adds no "Leave site?", as the
  original; work that stops with the page always counts) (`Sw.Page.SetDirty("dlg:…")`): dirty
  "Your changes in the dialog "Rename server" are not saved." (Leave without saving?), busy with work that stops with the page "Upload
  stops." (Leave this page?), both "… are not saved, and Save may not finish."; its footer buttons add no sentence of their own (`Leave: false`).
  Closing the dialog clears it.
* **Escape in a field** of the dialog is a close attempt (a dirty dialog asks "Discard changes?" before anything is thrown away; a
  field's own Escape revert doesn't run first); an open list of the field (`aria-expanded`), search fields and `[data-sw-ownescape]` keep
  their Escape.
* **Busy** = work runs: `SetBusy(promise | ({ Signal, Progress }) => promise, { Cancel, Abort, Background, InputSent, Text, Progress,
  CloseText, Stop, StopsWithPage, Announce, Key, Title, NoClose })`, or a button's async `OnClick` (`BusyButtons: false` to opt out).
  Busy shows `aria-busy`, the status slot (what it waits for, determinate when known, the time so far "9 s" / "1 min 5 s"; never a live
  region; the start and phase texts are announced, at most one "Still working" every 30 s). Work that stops with the page is part of
  the dialog's leave sentence (above).
* **Busy means locked:** while work runs only the declared ways out act (a Cancel that can stop the work, a plain Close, "Continue in
  background", the 10 s Close); every other footer button is `aria-disabled` with the reason "Still working" (`WhileBusy: true` opts a
  button out) and a dialog that opens busy focuses its way out. "Done" never closes running work.
* **Input while busy (FRM-5: never lose input):** while work runs the dialog's text fields are **read only** (not inert: the focus, the
  caret, selection and copy stay; the FRM-8 look) and its other controls (switches, check boxes, selects, buttons in the body) don't
  change; a try gives the line "Save is running: you can change the fields again when it is done". So nothing typed during a save can
  be lost. `EditWhileBusy: true` keeps the fields editable; then, and in every case, **a close after the work never drops a newer edit**:
  when a field or control differs from what it was when the button was pressed, the dialog stays open, dirty against the saved values,
  with the line "Save is done; your newer changes are not saved yet" (also when the work closes the dialog itself with its `close`).
  `data-sw-whilebusy` on an element keeps it usable while busy (a "Show details" toggle).
  **Locked by capability:** every Sw control in the body is locked with `E.SwControl.Locked(true)` while the work runs (keys, wheel,
  arrows, steppers, pop-ups: a NumberBox doesn't change by ArrowUp or the wheel) and unlocked after; its `sw-locked` refusal shows the
  dialog's line. A control without `Locked` gives a developer warning; number boxes, spin buttons and sliders are also guarded by the
  dialog itself (ArrowUp / Down, PageUp / Down, the wheel).
  **Newer edits are the user's (who changed it):** only a change the user made after the click counts (trusted input, a control's
  `sw-change` with `User: true`, `Sw.Edits.UserSince` of ui/base.js). A value the work writes itself (the server's customer number with
  `Set()`, a normalised text through `.value`) is no newer edit: the dialog closes with "Save: done", never "Discard changes?".
  ```js
  //  the work writes the server's value into the dialog: still closes
  { Text: _TF("Create", "…"), Kind: "primary", Value: "ok", OnClick: async () => { const r = await Sw.Net.Send(url, data); no.Set(r.Number); } }
  ```
* **One cancellation model (POP-6, FBK-5): running → stopping → stopped / done / failed.** "Cancel" is offered only for work that can
  really be stopped: a footer button's `OnClick(close, value, { Signal })` that reads the signal and sent **no write** (every request
  sent in the click or later with that signal is recorded and classified by the one write test of the framework, `Sw.Net.IsWrite`:
  `Sw.Net.Send` and a `fetch` that is not GET / HEAD are writes, unless the request says `{ ReadOnly: true }` or is a
  `Sw.Net.Read(url, arg, { Signal })` / `Sw.Net.Get` (a read api) or `{ AbortSafe: true }` (the server stops / undoes the work when the
  request is aborted); the inner fetch of a `Sw.Net` call is not counted again),
  a button marked `Cancellable: true` (a read, a server job that honours the abort) **while no write went out** (a write turns it into
  Stop waiting, with a developer warning: `Cancellable: true` never aborts a write the server already got), or `SetBusy` with `Cancel` /
  `Abort` / a function that reads `{ Signal }`. Cancel asks the work to stop and the status says "Save: Stopping…" until it has ended (the words of `Sw.Cancel.Texts`, as in the activity list)
  (at most 2 s): aborted = "Save: Cancelled", the dialog closes; it finished anyway = the dialog closes with its value, "Save: Finished
  before it could stop"; it failed = the dialog stays and shows the failure; still running after 2 s = the dialog stops waiting
  ("may still finish"). Work that sent a write or ignores the signal can't be cancelled: the footer Cancel is replaced **in its slot** by
  **"Stop waiting (Save may still finish)"**, which closes the dialog (clean / dirty as usual) while the work goes on (a sent write is
  never aborted); its later result is still said: a failure is a toast "Save: not done. <reason>" (`Sw.Net.FailText`), a success a toast "Save finished after all"
  (a dialog still open closes; one that holds newer edits stays, see above; when "Discard changes?" is open both close: "…your changes
  were saved and the dialog is closed"). Work that closes the dialog itself and then fails gives the toast too. A busy dialog without
  a footer Cancel gets a way out after 10 s (`Sw.Dialog.WayOutMs`, tests may lower it). After a Cancel or Stop waiting the button's click
  ends as aborted: **never "Save: done"**; the focus goes back to the opener (never the body).
* **One footer layout engine (LAY-1, TXT-17), any number of buttons:** the title bar, a **status slot** after it (0 high while empty: the
  rejection line opens it in place, so it never covers the title, ⋮, × or anything else), the body, the footer.
  - **Reserved room:** when a dialog opens it reserves the room its later states need (the rejection line in as many lines as the longest line this dialog can show takes: its
    real texts with its own button labels, so a one-line dialog reserves one line, Designer S5b N2; the busy status
    row and the 10 s Close row when they can't share the button row), less what the body can give while its largest field stays whole;
    the rest is an empty block at the end of the body (`--sw-dlg-reserve`). Short dialogs (and sheets) are a little taller, and later
    **nothing grows**: the title, ⋮ and × stay under the finger, on sheets too.
  - **Busy:** the box keeps its height (the body gives the reserved room and scrolls; the focused field, with its label when both fit, is
    kept whole in view) and **every footer button is pinned where it was** (its idle offset from the footer's bottom right edge, its idle
    width): whatever the busy state adds can't move it. "Stop waiting" takes Cancel's slot (its right and bottom edges; it grows left
    into free room only). **Its label is measured on one line and never wraps:** the full label "Stop waiting (Save may still finish)"
    when it fits beside the status, else the short "Stop waiting" (the status then says "it may still finish"), else the short label
    with the status giving up its room (its own row, or the time only), else the icon only; the full text is always its accessible name
    and tool tip. A footer Cancel of a dialog that runs work is sized for the short label when the dialog opens (a declared label, like
    `Labels`), so the short label fits at 1920, on phones and at 400 %. An added button (Cancel, Continue in background, the 10 s Close)
    goes left of the bottom row when its one-line label fits there, else into a row of its own above the buttons; **in a low window
    (400 % zoom, a phone in landscape) the 10 s way out never adds a row** (it would cover the focused field): the title bar's × becomes
    that way out (its name and tool tip say "Stop waiting (Save may still finish)", the line names it "× (…)"). The status shares the
    bottom row with about 6 em, else takes a row of its own above (sheets always; a low window shows the time only). The footer only
    grows upwards. The idle footer comes back when the work ends.
  - **Labels never break inside a word (TXT-17).** Footer labels wrap only between words; in a low window (400 % zoom, a phone in
    landscape) the footer buttons that need more than one row wrap into compact rows, never a sideways scroll (S6-D1, WCAG 1.4.10
    reflow: `sw-foot-wrap`, one line per label, the primary action last at the bottom right; more than two rows share two,
    `sw-foot-grid2`, two per row), and a label longer than its place is cut with … and has the full text as the tool tip. `Sw.Dialog.CheckLabels(root)` →
    the problems ("the label "…" breaks inside a word", "… is cut without its full text as the tool tip"); developer mode runs it on
    every footer / title bar change, the self-test fails on it.
  - **Focus in view:** any focus that lands in the body (the user's, the default focus, a focus trap giving the focus back when the
    frame or window gets it again) brings the field whole into view, idle or busy.
  - **Content that changes while open** (a field message after a lookup): a centered dialog keeps its bottom edge, so the footer stays.
  - **Labels that change** (Cancel → Close when the row is gone, Continue ↔ Force): a footer button never gets narrower or wider while the
    dialog is open (it keeps its slot; a longer label stays on one line, cut with … and its full text as the tool tip, and gives a
    developer warning). Only a label change keeps the slots; the same labels at another width (the web font arrived) are measured again.
    Declare every label a button shows with `Labels` and its slot fits the widest from the start (measured on one line):
    ```js
    { Text: _TF("Cancel", "…"), Value: null, Labels: [_TF("Close", "…")] }
    ```
  So **nothing that was on screen moves, nothing covers the focus or the buttons, Save is never pushed off screen**, with 1 to 4 footer
  buttons, long labels, on desktop, sheets, phones in landscape and at 400 %. Developer mode warns when a footer button moves, a title bar
  control moves while busy / while the line shows, or an overlay covers a control.
  **Self-test:** `ui/dialogtest.html` generates footers of 1 to 4 buttons with short and long labels and bodies of one / three fields /
  text, runs every state (idle, busy, line, typing / arrows / wheel while busy, the 10 s way out, the work writing the server's value, a
  real Cancel while stopping, a lookup that keeps the dialog open, typing in validated fields (field messages that come, change and
  go; a longer message set by code must give the developer warning), label swaps, progress, Confirm / Prompt / Alert / Working / "Discard
  changes?", a longer label set by code without `Labels`) at 320x180 (400 %), 390x844, 844x390 and 1920x1080 in the 5 themes (800
  cases), and **fails** (no notes) on a moved button or title bar control, a body label, input or text that moved while typing (LAY-9), Stop waiting outside Cancel's slot, an overlap, a covered
  control, a focused field not fully visible, lost input, a NumberBox changed while locked, a "newer changes" line after a code write, a
  label broken inside a word or cut without its tool tip, a developer warning or a `Sw.Overlay.Check()` problem (positions are taken
  after the opening animation ended). **It is a gate:** `window.SwDialogSelfTest { Done, Passed, Frames, Checks, Problems, Waived,
  Waivers }` (`?only=<theme|WxH>`, `?quick`, `?bundle`); the page title starts with PASSED / FAILED and `Passed` is true only with 0
  problems. **Run it with visible (classic) scroll bars**, as users of Windows desktops see them: a headless Chromium / Edge without
  `--hide-scrollbars` (Playwright: drop it from the default arguments); a scroll bar that appears at 320x180 is seen only then, which hidden scroll bars never
  show (P2-A-12: the pinned busy footer was 10 px off with one). It also fails when a footer scrolls sideways or a footer button lies
  outside the box (S6-D1, with the case `save.3.s6d1.one`: the busy three-button dialog with "Save and restart" and a title of two lines). A problem can only be accepted as a **written waiver** (`Waivers` in `ui/dialogtest.js`, with its reason, listed here).
  **Waivers: none** (0 problems in all 20 frames, 800 cases). Integrate / smoke: fail when `Passed` is false.
* **Close attempts:** × , footer Cancel / Close (a button without OnClick whose Value is null / false, or `Dismiss: true | "cancel" | "close"`),
  Escape, an outside click, the sheet swipe, Back (`Sw.Popup.CloseTop`), tab close / reload (`beforeunload`). Clean: closes. Dirty: asks
  "Discard changes?" ("Discard" / "Keep editing", Keep editing focused; an outside click on it keeps editing, the focus back in the field);
  an outside click gives the rejection cue. Busy: Cancel (or Stop waiting) then acts as clean / dirty; × acts as "Continue in background"
  when offered; everything else gives the **rejection cue**: a pulse (none in zero motion themes), a line "Unsaved changes: use Save or
  Cancel" / "Still working: use Cancel" naming only the visible buttons (in the status slot after the title bar; part of
  `aria-describedby`), announced (the same input within 1 s once). The line stays until the next close attempt or a change of the dirty /
  busy state (progress ticks don't touch it; when the ways out change, ex: the 10 s Close arrives, it names the new ones).
* A busy dialog with no way out gets, after 10 s, a Close that names the consequence (`CloseText`; default "Close (the work stops)" with
  `Stop`, else "Close (the work still finishes)"), left of the other buttons, announced once.
* `Modal: true`: an outside click never closes (rejection cue). Default: a clean dialog closes on an outside click.
```js
const h = Sw.Dialog.Open({ Title: "Upload report.xlsx", Id: "upload", Content: "…", Buttons: [{ Text: "Cancel", Value: null }, { Text: "Done", Kind: "primary", Value: 1 }] });
h.SetBusy(({ Signal }) => upload(file, Signal), { Text: "Uploading report.xlsx", Background: true });   // Cancel aborts the upload

// A save: once its request is sent it can't be cancelled, the footer offers "Stop waiting (Save may still finish)" instead of Cancel
Sw.Dialog.Show({ Title: "Edit customer", Content: form, Buttons: [{ Text: "Cancel", Value: null },
    { Text: "Save", Kind: "primary", Value: "saved", OnClick: (close, value, { Signal }) => Sw.Net.Send(url, data(), { Signal }) }] });

// A read (or a server job that stops on abort) can be cancelled: say so
Sw.Dialog.Show({ Title: "Find duplicates", Content: "…", Buttons: [{ Text: "Cancel", Value: null },
    { Text: "Search", Kind: "primary", Cancellable: true, OnClick: (close, value, { Signal }) => Sw.Net.Send(findApi, query(), { Signal }).then(show) }] });
```

#### Movable and resizable (UX-POP-7)
Drag the title bar (dead zone 4 px, 10 px on touch), drag an edge or corner (8 px), double click the title bar to maximize / restore. The
dialog menu (⋮ "Dialog options") has Move, Size, Maximize / Restore, Reset position; Move / Size show step buttons (50 px, 44 px on touch),
Revert and Done; arrows step 10 px (Shift 50), Enter keeps, Escape reverts; the new position is announced. Every dialog kind remembers its
position and size: `Id` names the kind; without it a stable id is derived from the page path, the title (numbers taken out) and the button
labels. Stored in local storage `SysWeaver.Dialog.<site>.<Id>` (per site root; the old `SysWeaver.Dialog.<Id>` is still read) and
restored clamped (the whole dialog inside the viewport, at most the viewport, never narrower than the content). While the user moves it
(drag, Move mode) it moves freely both ways: the title bar's height stays on screen, and sideways at least 160 px of it (`MinOnScreen`);
a resize keeps the whole width on screen. Desktop dialogs have no transitions: position and size follow the pointer in the same frame. A press on the title bar
keeps the focus in the field. Two open dialogs with the same `Id` give a developer warning. Phone layout: not movable; sheets have a handle (drag up: taller, drag down: a close attempt) and Size in the menu. With a mouse (`pointer: fine`) the desktop dialog is used from 480 px wide (else from 761 px), so a page in the shell's narrower content frame of a desktop window gets a movable dialog, not a sheet (`deskMq` / `sheetMq` in overlay.js, the same queries in overlays.css).
`NoMove: true` turns it off. The framework dialogs use the ids `sw.confirm`, `sw.prompt`, `sw.alert`, `sw.select`, `sw.working`, `sw.discard` and `sw.search.<Id>` (`Sw.SearchSelection`).

#### Modal background and layers (UX-POP-1, POP-5)
While a dialog is open everything else in the document is `inert` (the shell chrome too; a page that frames a dialog page makes its
own content inert except the frame), one scrim (a confirmation over a dialog moves the dim, none when the shell already dims). Toasts and
the `Sw.Page.Announce` live region move into the top dialog (announced, toast buttons in its Tab cycle; F6 moves between the dialog and its
toasts). `Above: true` opens a dialog above every pop-up (the session time-out warning); elements marked `data-sw-above-modal` are never made inert.

#### Dialog rules (one anatomy, one wording)
Every dialog, framework or app, follows these rules; `Sw.Dialog.Confirm` / `Prompt` / `Alert` and `Sw.Cells.ConfirmDanger` already do.
1. **Anatomy:** a title, always (a question for a confirmation: "Remove the API key?"; a details view names what it shows: "Last exception · ExchangeRate · 09:04:56"; `Sw.Dialog` gives a developer warning for a dialog without `Title`, and `AriaLabel` names one that really has no visible title), a left aligned body (text first, then fields), a footer with
   the buttons on the right: Cancel first, the primary (or danger) action last. Never put buttons in the body (`Sw.ButtonRow` inside
   `Content`): pass them as `Buttons` (`Close: false` + an `OnClick` that returns false keeps it open while you validate / call the server;
   the created buttons are in `Buttons[i].Button`). The body is `text-align: start` whatever the page css says.
2. **Destructive actions:** use `Sw.Cells.ConfirmDanger({ Title, ItemLabel, Item, Text, Warning, OkText })` (warning icon, the item in
   bold, "This cannot be undone.", Cancel focused) or `Sw.Dialog.Confirm(..., { Danger: true })`. The title names the action and the
   target: "Kill devenv (33936)?", "Reboot HOME-SERVER?", "End the session of ops?"; `ConfirmDanger({ Action: "Kill",
   Target: item })` / `Sw.Dialog.DangerTitle` build it. A generic "Kill the process?" is not enough when the user picked one of many rows.
   A table row is named for people: by its identity column (User, Name, E-mail, Host ...; `Sw.Cells.RowName`, or `data-sw-name` on the row),
   not by an opaque key (a token): "End session for ops?", with a few short facts of the row under the item (`Facts`: address, agent,
   start; times as the cells show them). A server confirmation of two lines, "question
explanation" (C# `TableDataActionsExAttribute`),
   gives the title and the text, and its explanation replaces "This cannot be undone.". Titles wrap (a long address is never cut).
3. **Wording:** buttons are verbs that say what happens ("Remove key", "Delete", "Create key", "Save"), "Cancel" for the way out.
   No "Yes" / "No", no exclamation marks, no shouting in new texts (the framework shows a caller's button text as given, so ported texts such as "Yes, remove it!" read as in the original), sentences end
   with a full stop, product names match the page ("API key", not "API-key"). Don't end the text with "Are you sure?": the title asks.
4. **Focus:** an input of the body gets the focus; otherwise Cancel when the footer has a danger button (a destructive action is never the
   default, Enter on Cancel cancels); otherwise the primary button. `Focus` overrides it. This holds however the dialog was opened (a click,
   a key, or code with no user action): when its frame has no focus yet, the trap keeps the default (`Sw.Focus.Initial`) and gives it as
   soon as the window gets the focus (the app shell moves the focus into the frame, also down nested frames), so Tab, Enter and Escape
   work at once.
5. **Keys:** Escape is a close attempt (a clean dialog resolves null; dirty asks, busy refuses with the cue; `NoEscape`: the cue); Enter confirms only where `EnterValue` says so (never for Danger; `Confirm` only with `Safe`); Escape in a field is a close attempt too.
6. **Modal over the whole app:** a dialog in a page shown by the app shell dims the shell too. The page posts
   `{Type: "SwDialogModal", Count}` to the top window (overlay.js, automatic for every `Sw.Dialog`, also through `compat_common.js`);
   the shell puts its scrim (`app-modalscrim`) over the top bar and the menu, raises the content frame above it, makes the chrome
   `inert` and moves the focus into the frame; a tap on the shell scrim, or Escape while the focus is in the shell, is passed back
   (`SwDialogOutside`) and acts like a tap / Escape on the page's own scrim (non modal dialogs close, modal ones stay). A navigation or
   `pagehide` resets it. Popups and menus do not dim.
   **Nested frames** (a framed page that frames another page, ex: an app Home that shows a library page): the inner page also posts
   `{Type: "SwFrameModal", Count}` to its parent; the page in between dims itself with the same scrim (`.sw-frame-scrim`), raises the
   frame (`iframe.sw-frame-modal`), keeps the focus in it and passes a tap on its scrim and Escape down. One scrim for the whole app,
   nothing to do in the pages.

### Motion out (Sw.Motion)
`Sw.Motion.Out(el, { Class, Keep, Done })` → promise: the one exit path of every overlay (dialogs, sheets, scrims, popups, menus, toasts,
hints, the loader, the server block; UX-MOT-1). It adds `sw-leaving` (or `Class`), makes the element `inert` and `aria-hidden` at once,
waits for the animation / transition that class starts (theme durations `--sw-dur-*` times `--sw-motion`; a zero duration ends at once, reduced motion fades without moving, with a
fallback timer), then removes it. Own overlays use it the same way: `.my-pop.sw-leaving { animation: sw-fade-out var(--sw-dur-2) var(--sw-ease) forwards; }`
(keyframes `sw-fade-out`, `sw-pop-out`, `sw-dialog-out`, `sw-sheet-out` in overlays.css). `Sw.Motion.Reduced()`: reduced motion is asked for.

### Toasts
`Sw.Toast(text, { Kind: "error" | "ok" | "info" | "warning", Duration, NoCopy, Title })`, `Sw.Fail(text, duration, noCopy)`,
`Sw.Info(text, duration, noCopy)`, `Sw.Message(text, duration, className, noCopy)`. Toasts stack at the bottom (full width on phones),
lifted above a bar at the bottom of the window so they never cover a pager / footer (`footer`, `[role=toolbar]`, `[role=navigation]`,
`nav`, `.sw-pager`, a sheet (`.sw-sheet`) or `[data-sw-toastavoid]` touching the bottom edge; measured when a toast shows, every second
while one is shown, when a pop-up opens or closes and on resize, css variable `--sw-toast-lift` on `.sw-toasts`; the probes look into
same-origin framed pages, so the app shell's toasts keep clear of a framed table's pager). A sheet or `[data-sw-toastavoid]` surface counts
however tall it is (the activity sheet on a phone); with no room above it the toasts move to the top of the window (`.sw-toasts-top`),
never over it,
identical messages are merged with a counter, hovering pauses the timer, a click (or Enter) copies the text and removes the toast.
`duration` true = 24 hours. `Details`: a folded "Details" part under the text (copied with it).
**Timing and access (UX-FBK-8):** a toast stays 5 s + 60 ms per character, at least 10 s with an `Action` (a longer `Duration` wins);
**errors stay until closed**. Hovering or focusing a toast pauses it, a hidden tab waits. Close by its button, a click, Escape on it, or on
touch a sideways swipe. The action is announced ("Undo available."). While a dialog is open the toasts sit inside it (in its Tab cycle;
F6 moves between the dialog and its toasts, also from a toast's button or its Details); a toast never takes the focus. For the shell's
F6 cycle: `Sw.Toast.Region()` (the region while it shows a toast, else null), `Sw.Toast.Focus()` (remembers where the focus was: when the
last toast closes with the focus in it, the focus goes back there, never to the body). `Sw.Fail` stays quiet for network errors while `Sw.Net.Online === false`
(the shell's offline line says it once).

**Failed requests read as messages for people.** `Sw.Fail(textOrError)` passes the text through `Sw.Net.ErrorText`: a raw
"Could not save: 404 - Not Found" shows "Could not save: The server could not find it. It may have been removed or renamed; reload the
page and try again." with the raw text under Details (an error toast stays until it is closed); a 5xx shows the server's own message; a text without an HTTP status is
unchanged. Pages keep passing `err.message` (or the error itself); no change needed.

**Placement rule: a toast never covers a control.** Toasts sit bottom right (full width on phones), at most 3, the
newest at the bottom. When a toast shows (and on resize, every second while one shows, when a pop-up opens or closes) they are lifted:
1. above any bar touching the bottom edge: the selectors above plus `.sw-edit-formfoot` (the sticky Save footer), `.sw-dialog-foot`, and
   any `position: fixed / sticky` element there (so your own sticky action bar counts without a class);
2. above any primary / danger button (`.sw-button.sw-primary`, `.sw-danger`, `[type=submit]`), and any control of such a bar, in the
   toasts' column near the bottom;
3. **placed with every avoid zone** (the overlay check drives the place, it does not only warn): the stack goes to the lowest place in
   its column where it covers no control a user can act on (the `Sw.Overlay` list: buttons, links, fields, focusable elements; a large
   focusable region such as a grid or a chart is not a control, its buttons are), no open pop-up, no registered overlay
   (`Sw.Overlay.Register`) and no bar, measured by geometry in this page **and in same-origin framed pages**, also while a pop-up is
   open (a framed pager under the shell's open activity list counts, so the toast does not jump when the list closes). The data rows
   of a table are content a toast may lie over for its few seconds (rows scroll; their actions are also in the row menu and on the
   keyboard; the toasts' registration owns them); the table's tool bar, header and pager always count. With no free place below, the
   top of the window when that is free, else the place that covers the fewest. So `Sw.Overlay.Check()` reports no toast over a control.
A page with a special bottom area marks it with `data-sw-toastavoid`. Field errors belong next to the field (`SetError`); a toast is for
the outcome of an action.

### Overlay: "covers nothing" (Sw.Overlay)
Every **fixed overlay that is not a modal layer** (toasts, an activity line, an offline line, a floating bar, a page's own fixed helper)
registers, and the framework checks its geometry: in developer mode a `[Sw UX] Sw.Overlay: "<Name>" covers "<control>"` warning names
every button, link or field it lies over (the overlay is the topmost element there) that it doesn't own (LAY-1, FBK-8, POP-6). Pop-ups,
menus and dialogs are modal layers and don't register. Toasts are registered by the framework, and **placed** clear of every control
and registered overlay (see the toast placement rule): the check that finds a cover also moves the toast. A large focusable region (a
grid, a chart or a scroll area with `tabindex`: a fifth of the window or 40 % of its height) is not a control here; its own buttons are.
A control that another layer covers at its middle (ex: a dialog's scrim while it leaves) can't be used now and is not counted (the same
rule as the toast placement), and the toasts are placed again right before their check (F10: the check saw the place of a moment ago).
```js
const off = Sw.Overlay.Register(line, { Name: "Activity line", Owns: ".my-line-target" });   // checked on changes, resize and scroll
Sw.Overlay.Check();          // → ["\"Toasts\" covers \"Next\""]: every registered overlay now (self-tests, smoke runs; also without DevMode)
Sw.Overlay.Covers(el);       // the same check for any element
off();                       // or Sw.Overlay.Unregister(line); leaving the document unregisters too
```
Options: `Name` (for the message), `Owns` (what it may lie over: an element, a selector, an array, a function(element) → bool),
`Settle` (ms after a change before the check, default 400).

**Overlay options are checked (one registry).** The options of `Sw.Place`, `Sw.Track`, `Sw.Popup.Show`, `Sw.Menu.Show` / `Context`,
`Sw.Dialog.Open` / `Show` / `Confirm` / `Prompt` / `Select` / `Working` / `DangerTitle`, a dialog button, `Sw.Toast`, `Sw.Loader.Show`,
`Sw.Motion.Out` and `Sw.Overlay.Register` / `Covers` are registered schemas (`Sw.DevOptions.Register`, `Sw.DevOptions.Schema(name)`):
in developer mode an unknown option ("did you mean …"), a wrong type or a value out of its list (`Side: "top"`) is a `[Sw UX]`
warning; nothing in production.

### Hint
`Sw.Hint.Attach(element, text)`: the title (or text) shows on long press (touch / pen) and on keyboard focus. `Sw.Hint.Show(element, text, ms)`, `Sw.Hint.Hide()`, `Sw.Hint.IsShown()`. A visible hint takes the first Escape (UX-TIP-5).

### Loader
`const remove = Sw.Loader.Show({ Target, Text, Opaque, Icon, Delay })`: a loading overlay that blocks input and the focus (the whole page,
or only `Target`). The core's `Sw.Page.Loading(...)` / `Sw.Page.Main(fn, {Loading})` show it automatically (`noloader` url flag: none).

### ServerBlock
`Sw.ServerBlock.Show("server.pause" | "server.restart" | "server.shutdown" | "server.error" | {Type, Title, Text, Icon, Time})`, `Hide()`, `IsShown()`.
A dialog above every other pop-up (an `Sw.Dialog` with `Above`, class `sw-serverblock`): the state as its title, the text, a spinner
with the time since the server became unavailable (m:ss) and a note; the page behind takes no input. No reload button: the session
reloads the page by itself when the server is back as a new instance (after a network drop, the same instance: no reload, the dialog
goes away). Like every dialog it moves, resizes and closes (×, Escape, a press outside); closing only hides it until the server is
back (`IsShown()` is false meanwhile) or a block of another kind comes. Shown automatically on the core's `server.block` /
`server.unblock` events (top windows).

### FullScreen
`Sw.FullScreen.IsFull()` (also an installed app running standalone), `Is()`, `await Enter(element)`, `await Exit()`, `await Toggle(element)`,
`OnChange(fn)`, `OnTouch(element, {AnyPointer})`, `OnCorner(element, {Corner, Size, Area, OnToggle})`. Alias `Sw.Fullscreen`.

### Tabs
`new Sw.Tabs({ Closable, OnSelect(tab, previous), OnClose(tab), OnAdd(), AddTitle, Small, Label, Fill, Class })`
`Add({ Id, Title, Icon, Tip, Content, Closable, Data, Select, End })` (`End`: the tab sits at the right end of the strip, ex: panels that are not about the current document) returns `{ Id, Head, Content, Data, SetTitle, SetTip, SetDirty, SetBadge(text, kind) }`.
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
`RowMenu(def) -> [menu items]` (a ⋯ button at the end of every row for its own actions, ex: "Edit…"; it stays usable on a disabled row), a definition's own `Menu` (items, a function(def) or a promise of them) and `MenuTitle` (only that row gets a ⋯ button, ex: "Choose a picture"),
`SetEnabled(keyOrFn, enabled)`, `Get(key)`, `SetError(key, text)`, `SetHelp(key, text)`. Modified values get an accent label and a "modified" dot after the name
(a click on the dot, or a long press / right click on the label, resets it). On narrow screens labels are shown above their values.
Per definition: `Map: { Min, Max, Unit, Format }` (a normalized number, ex: a 0..1 controller, also shown in the units it drives, linearly: "= 3000 lm"), `Format` (the display of a number, a template: `"{0} BPM"`, `"{0:1} %"` = 1 decimal, shown after the value box instead of the Unit; `PropertyEditor.Display(spec, value)`), `Scale` (numbers, default 1: shown and typed × Scale, its range and step too: 0.3 with `Scale: 100, Unit: "%"` shows as "30 %", the value stays 0.3; `PropertyEditor.Scaled` / `Unscaled`). Units and mapped values sit in a slot of one width on every number row of an editor, so the sliders and boxes line up. Per definition: `Hint` (the line under the name, 12 px secondary text; default for numbers "min to max unit", ex: "0.05 to 8 ×"; `""` = none;
the option `Hints: false` hides every hint), `Help` (a help text under the value), `ReadOnly` (the value as plain text, tabular figures, no
control). Row markup (components.css, Design v2, PORTING.md B14, the same look as Sw.Edit's name over its type hint):
`div.sw-prop-label > span.sw-prop-name + button.sw-prop-dot + small.sw-prop-hint`, `div.sw-prop-value > controls + span.sw-prop-unit`,
then `div.sw-prop-help` / `div.sw-prop-error` under the value (`span.sw-prop-ro` for read only values; null and an empty text show a quiet "null" / "empty" token, classes `sw-prop-null` / `sw-prop-empty`, like Sw.Edit).

### Page anatomy (page.js, page.css; Design v2)

Every page of every app has the same anatomy (Design v2, [PORTING.md B10 / B11](../PORTING.md#b10-page-anatomy-page-header-and-container)): the shell's top bar (breadcrumb) -> a **page header**
(title, one line description, status, actions) -> the content in a page container (dashboards: facts strip, KPI tiles, cards, lists;
fill pages: one table / log / chat that takes the height). Build pages from these shared parts instead of page specific pills, donuts,
fieldsets or boxes, so a restyle of the framework reaches every app. All parts are flat (surface + 1 px hairline, radius 8, no shadows,
no gradients) and follow the 5 themes and the densities through the tokens.

| Part | JS (callable with or without `new`) | Markup / classes (static pages can write them by hand) |
|---|---|---|
| Page skeleton | `Sw.PageLayout({ Type: "dash" \| "form" \| "read" \| "fill", Title, Description, Status, Actions, Compact, MenuTitle, Main, Parent, Class, Header })` -> `{ E, Header, Add(...items) }` | `main.sw-page-dash` (max 100rem), `.sw-page-form` (75rem), `.sw-page-read` (45rem), `.sw-page-fill` (full width + height; its `.sw-fill` child takes the rest); the container pads the page (`--sw-page-px`), inside the shell `html.Embedded` adds no padding then |
| Page header | `Sw.PageHeader({ Title, Description, Status, Actions, Compact, Level, Class, MenuTitle, Overflow })` -> `{ E, TitleE, DescE, StatusE, ActionsE, MoreB, FromMenu, SetTitle, SetDescription, SetStatus, SetActions, AddAction, FitActions }` (ui/layout.js) | `header.sw-ph > div.sw-ph-text > (div.sw-ph-titlerow > h1.sw-ph-title + span.sw-ph-status) + p.sw-ph-desc`, then `div.sw-ph-actions`; `.sw-ph-compact` on fill pages (hidden on phones: the top bar shows the title). **Overflow** (default on): the actions stay on one line, beside the text on wide headers (the text keeps 20rem), on their own line on narrow ones (< 760 px); the last actions that do not fit move into a "More actions" kebab menu (`button.sw-ph-more`, a menu button: click, right click, long press, Menu key, Shift+F10, `aria-expanded`; items with the action's text, icon, tool tip, disabled, danger; a click clicks the hidden button, so confirmations stay); the primary action and non button controls never move; refits on resize and on action changes (`.sw-ph-fit`, `.sw-ph-overflowed`). `Overflow: false` keeps the old wrapping row. **Menu only actions**: an action with the class `sw-ph-in-menu` (`new Sw.Button({ Text: _T("Delete"), Kind: "danger", Class: "sw-ph-in-menu", OnClick })`) always lives in the "More actions" menu, also when everything fits (a destructive action kept away from the daily ones, like the table row kebab); destructive menu items come last after a separator. **Phones**: a compact header (fill pages) is hidden below 760 px, but when it has visible actions it keeps one action row there (no text: the top bar names the page; actions left, kebab right) |
| Menu title | `Sw.MenuTitle()` -> the name of this page's main menu item when shown in the app shell, else null | A level 1 `Sw.PageHeader` uses it by default (and sets the document title to it), so the h1, the breadcrumb and the menu say the same ("Process information", not "Process info table"). `MenuTitle: false` keeps `Title` (a page about one record); then `Title` and every `SetTitle(text)` also set the document title, and the shell follows it, so the breadcrumb says what the h1 says (no more `document.title = t` by hand) |
| Grid | `Sw.Grid({ Content, Class, Label, Tag })` -> `{ E, Add }` | `div.sw-grid` (12 columns, 16 px gap), children `.sw-span-3/4/6/8/12` (3 -> 6 below 1280 px; 6 -> 12 below 900 px; KPI tiles 2 per row on phones) |
| Card | `Sw.Card({ Title, Sub, Count, Tools (alias Actions: elements, components or button options { Text, Icon, Kind, OnClick }), Body, Flush, Span, Tag, Level, Class, Label, Collapsible, Collapsed, StateKey, OnCollapse })` -> `{ E, HeadE, TitleE, SubE, CountE, ToolsE, BodyE, SetTitle, SetSub, SetCount, SetTools, SetBody, Collapse(collapsed, silent), IsCollapsed, ToggleE }` | `section.sw-card > div.sw-card-head (h2.sw-card-title, span.sw-card-sub, .sw-card-tools or .sw-push) + div.sw-card-body` (`.sw-flush`: no padding). **Fields in a card body are stacked** (LAY-8): every field directly in the body (`.sw-field`: NumberBox / Select with a Label, `.sw-text`: TextBox) (or a `Sw.Group` body) takes its own line at its own width, with the form gap before what follows (`Body: [name, note, saveButton]` reads top to bottom); side by side only in a `.sw-row`. A multi line TextBox there takes the line up to 40rem. **An object editor in a card**: `card.BodyE.append(new Sw.Edit(type).Element)` needs no options: an editor directly in a card body drops its own frame and, when the card has a title, its menu button joins the card tools (no second title or collapse arrow); `Sw.Edit.Options.CleanHints.Clone()` drops the menu too and keeps the hint lines, the class `sw-edit-framed` keeps the old framed editor (edit/README "Options and presets"). **Collapsible** (`Collapsible: true`): a chevron button before the title (`button.sw-card-toggle`, `aria-expanded`, named "Collapse <title>" / "Expand <title>") and a pure click on the free area of the head fold the body away (`.sw-card-collapsed`, the body `hidden`); `Collapsed` is the state until the user changes it, the user's choice is kept in localStorage (`SysWeaver.Card.<page path>|<StateKey or title>`: give a card with a changing title a `StateKey`); `Collapse(on)` from code is not stored; `OnCollapse(collapsed, card)` |
| Facts strip | `Sw.Facts({ Items: [{ Key, Label, Value, Title, Href, OnClick, Copy, Mono }], Card, Label })` -> `{ E, Add(item) -> { E, ValueE, Set, SetTitle }, Get(key), Set(key, value) }` | `dl.sw-facts.sw-card > div.sw-fact > dt + dd` (a value that opens a page: `button.sw-fact-value`); replaces rows of fact pills. **Inside a card** the strip drops its own frame (no box in a box, also with the default `Card: true`) and keeps only the hairlines between its cells; directly in a `.sw-card-body` it runs edge to edge, with a hairline to the content before / after it (`Card: false` is the clean choice there) |
| Upload button | `Sw.UploadButton({ Text, Title, Repo, OnUploaded, Guard, Small, Icon, Class })` -> `Sw.Button`; helpers `Sw.UploadTarget(el, repo, onUploaded, guard)`, `Sw.ReportUpload(res, files, onUploaded)` | `button.sw-button.sw-dropbtn` (`.Dragging`: dashed accent frame; `.Selecting` / `.Uploading`). A secondary button with the upload icon that is also a drop target for a server repo; one toast per upload (error / already uploaded / uploaded). Needs `common/fileUploader.js` (FileUploader module) on the page, else it is disabled with a tool tip. `Guard: { Url }` hides it from users without the role (`Sw.Access.Guard`). Was `SmShared.UploadButton` (alias kept) |
| Fill table | `await Sw.FillTable(page, url, { Class, ...Sw.Table.Create options })` -> the table | `div.sw-fill.sw-fill-table` after the page header of a fill page (`Sw.PageLayout({ Type: "fill" })`): a live `Sw.Table` in page mode taking the free height; its own compact header is dropped and its refresh line moves under the page description. Needs `explore/table.js`. Was `SmShared.FillTable` (alias kept) |
| KPI tile | `Sw.Kpi({ Label, Icon, Value, Unit, Sub, Meter: true \| { Max, Warn, Danger, Good, Absolute, Levels }, Span, Href, OnClick, Title })` -> `{ E, ValueE, UnitE, SubE, Meter, Set({ Value, Unit, Sub, MeterValue, Max }), Level() }` | `div.sw-card.sw-kpi` (or `button` when it opens a page) `> .sw-kpi-label (icon + span), .sw-kpi-value (number + small unit), .sw-meter > i, .sw-kpi-sub`; replaces donuts / gauges for one value of a total |
| Meter | `Sw.Meter({ Label, Value, Max, Warn, Danger, Good, Absolute, Levels })` -> `{ E, Set(v, max), Level }` | `div.sw-meter[role=meter] > i` (`.is-warn`, `.is-danger`; show the level in text too, ex: a "High" badge). **Levels only with thresholds**: without `Warn` / `Danger` it is a plain bar (a progress, a share). `Good: "low"` (default, a use: at or above `Warn` / `Danger` is bad; cpu `{ Max: 100, Warn: 0.8, Danger: 0.92 }`) or `Good: "high"` (more is better: at or below is bad; on time % `{ Max: 100, Good: "high", Warn: 95, Danger: 90 }`, revenue of a target). A threshold of 0..1 is a fraction of `Max`, above 1 an absolute value on the scale (`{ Max: 100, Warn: 70, Danger: 90 }`, or 30 GB of a 64 GB drive); `Absolute: true` reads every threshold as a value (`{ Max: 5, Absolute: true, Warn: 1 }`). `Levels: false` never colours |
| List card | `Sw.ListCard({ ...Card options, Items: [{ Key, Text, Icon, Status: { Text, Kind, Dot }, Meta, Title, Href, OnClick }], Empty: { Title, Text, Icon, Actions }, Count })` -> card + `{ ListE, SetItems(items) }` | `section.sw-card > .sw-card-body.sw-flush > ul.sw-rows > li (badge, icon, span.sw-rows-text, span.sw-rows-meta)`; a row that opens something is `li > button.sw-rows-link` |
| Section title | `Sw.SectionTitle(title, sub)` | `div.sw-section-title > h2 + span`; `.sw-section + .sw-section` are 32 px apart |
| Tiles | (markup) | `div.sw-tiles > button.sw-tile > span.sw-tile-ico + span.sw-tile-text (b + span)` (shortcuts) |
| Empty / loading / error | `Sw.StateView({ Kind: "empty" \| "loading" \| "error", Icon, Title, Text, Actions, Details, Small, Lines })` -> `{ E, Set }`; `Sw.EmptyState({ Icon, Title, Text, Actions, Error })` (ui/layout.js) | `div.sw-state.is-<kind>` (`.is-small` in cards and lists; skeleton lines `.sw-skel`), `div.sw-empty` (`.sw-error`) |

Rules (checked by `SwWeb check --design`, the rule names are in [PORTING.md](../PORTING.md)):
- The page: `<body class="sw-root">`, `<meta name="viewport" content="width=device-width, initial-scale=1">`, the bundles with `defer`
  and the page code in its own deferred script (an inline `<script>` that calls `Sw.Page.Main` runs before the bundles: `script-order`);
  libraries a page uses are included by the page (`Sw.Table`: `../explore/table.js` + `../explore/table.css`, `missing-include`).
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
    const cpu = Sw.Kpi({ Label: "CPU", Icon: "cpu", Unit: "%", Meter: { Max: 100, Warn: 0.8, Danger: 0.92 } });
    const list = Sw.ListCard({ Title: "Services", Count: true, Span: 12, Empty: { Title: "No services" } });
    page.Add(facts, Sw.Grid({ Content: [cpu] }), Sw.Grid({ Content: [list] }));
    cpu.Set({ Value: "12.5" });
    list.SetItems([{ Text: "Mail", Status: { Text: "Running", Kind: "ok", Dot: true }, Meta: "120 MB" }]);
});
```

### List / detail (page.js: `Sw.SelectList`, `Sw.ListDetail`)
A selectable list and the list / detail layout that every inbox, picker and master / detail page uses (the fake email inbox is built on them).

| Part | API | Notes |
|---|---|---|
| Selectable list | `Sw.SelectList({ Items, Value, Multi, Label, Render(item, li), Empty, OnSelect(value, items, how), OnOpen(item), Class })` -> `{ E, ListE, Items, SetItems(items), Get(), Set(value, silent), Item(key), Row(key), Active, Focus(), Move(delta) }` | `ul[role=listbox]` with the focus on the list and the active row in `aria-activedescendant` (live re-renders keep the focus). Item: `{ Key, Text, Sub, Meta, Icon, Status, Title, Class, Disabled }`; `Key` is a string or a number and `Get()`, `Active` and `OnSelect` return it as given (`Set(7)` -> `Get()` = 7, not "7"; `Set` also accepts the key text); `Render` replaces the default row content (icon, text + sub line, meta, status badge). Single select: the selection follows Up / Down / Home / End / Page keys and type ahead; Enter, double click and a click on the selected row call `OnOpen`. A modifier click never selects or opens (UX guide INP-2: it makes the row active and shows the focus), except Ctrl / Cmd and Shift in a multi select list. `Multi: true`: check boxes, Space toggles, Shift + arrows / Shift + click select a range, Ctrl / Cmd + click toggles, Ctrl / Cmd + A selects all. `how` is `"click"`, `"key"` or `"api"`. No items: a compact `Sw.StateView` (`Empty: { Icon, Title, Text, Actions }` or a function returning it). |
| List / detail | `Sw.ListDetail({ List, Detail, Label, ListWidth, Breakpoint, Back, BackText, OnBack, OnLayout(narrow), Class })` -> `{ E, ListE, DetailE, Narrow, Showing, ShowDetail(on, focus), Back(), Layout() }` | Side by side (the list in a `--sw-ld-list-w` column, default `clamp(17rem, 34%, 26rem)`, each pane scrolls) while the element is at least `Breakpoint` px wide (default 640; its own width, not the window: it works in a card, a dialog or next to a side bar). Narrower: one pane at a time; `ShowDetail(true)` shows the detail with a Back bar (`Back: false`: your own back button calls `Back()`), Escape in the detail goes back and the focus returns to the list. |

Rules: open the detail on a phone only for a click (`how === "click"`) or `OnOpen`, so arrow keys never jump away from the list; keep
the selection when going back (the row stays highlighted); the detail of nothing selected is a `Sw.StateView` ("Select an email to read it").

```js
const detail = Sw.el("div.my-detail");
const list = Sw.SelectList({
    Label: "Services", Items: services.map(s => ({ Key: s.Id, Text: s.Name, Sub: s.Path, Status: s.Running ? { Text: "Running", Kind: "ok", Dot: true } : null })),
    Empty: { Icon: "server", Title: "No services", Text: "Installed services show up here." },
    OnSelect: (key, items, how) => { showService(detail, items[0]); if (key && how === "click") ld.ShowDetail(true, true); },
    OnOpen: () => ld.ShowDetail(true, true),
});
const ld = Sw.ListDetail({ List: list, Detail: detail, Label: "Services" });
page.Add(ld);   // in a fill page, or give the host a height
```

### Forms: `Sw.Form` (page.js)
One validation helper for any form built from Sw controls (or plain inputs): the fields with Required and custom rules, the message
under the field, the focus on the first bad field, re-checks while the user fixes a field, and a submit that only runs when all is valid.

`Sw.Form({ Fields, Submit, OnSubmit(values), Gate })` -> `{ Fields, Field(name), Add(field), Values(), Validate(focus) -> { Ok, Values, Errors }, SetError(name, message), Clear(), Submit() }`

A drafted field (the form's `Draft`, a TextBox `Draft`, `Sw.Draft.Attach` by hand) keeps its text, so it never makes the leave question
say "lost" (`IsDirty()` still reports it). A Save outside the form (the page header, `Saves: true`, any button) whose request carried
every edited value of the form cleans it too (`form.SentBy(text)`, called by `Sw.Button`; the live forms are in `Sw.Edits.Forms`).

- A field: `{ Name, Control, Label, Required, Validate(value, values), When(values) }`. `Control` is a component (TextBox, NumberBox,
  Select, Segmented, Toggle, ColorPicker, any object with `Get()`) or an input element. `Required: true` gives "<Label> is required.";
  a text gives your own message (prefer an instruction: "Enter a billing e-mail."). Empty = null, blank text, NaN, an empty array or
  false (a Toggle that must be on). `Validate` returns a message or null. `When` returning false skips the field (a hidden mode).
- The control's own rule runs too: its `Validate()` (when it has one) or, for a TextBox with `Validate`, `Commit()`.
- Messages: the control's `SetError` (TextBox), else a shared line `div.sw-text-error.sw-form-error` inside the `.sw-field` (under the
  hint) or right after the input, with `aria-invalid` and `aria-describedby`. One error surface: no toast for field errors.
- `Submit`: a button (Sw.Button or element) whose click validates and then calls `OnSubmit(values)` (busy while a returned promise
  runs). `Gate: "required"` disables it (aria-disabled, the reason "Fill in <labels> first" as tool tip and on an attempt) while a required field is empty (default: always enabled, a click shows what is missing: UX guide FRM-7). A field marked `Required` here also gets `aria-required` on its input.
- Server errors for one field: `form.SetError("Mail", text)`.
- **Unsaved edits (SAF-10), opt in with `Dirty: true` / a sentence** (the original never asked; P3-A-4): while a field the user edited differs from its value before the user's first edit
  (values set by code, also after the form was made and in a field never focused, a draft restore and a click or key that changed
  nothing are no edits) the page is dirty: leaving asks "Leave without saving? Your changes to `Title` are lost." (`Title: "the
  invitation"`; without a Title the title of its card / section; `Dirty: "own sentence"`; without `Dirty` the form never asks).
- **One result (FBK-5, FRM-3):** `OnSubmit` runs in a click scope: a throw, a rejection, a request it started that failed, or `false`
  is "not saved", shown once in the form's status line (a `false` that showed nothing says "Not saved." and gives a developer
  warning: use `SetError(name, message)` or throw an Error with the reason); never also a toast. A successful `OnSubmit` makes it clean, and while it
  runs the page is not marked (a save that navigates when done is not asked about). Inside a dialog the dialog asks instead; a form
  with a password field (sign in, change password) never asks. `IsDirty()`, `MarkClean()`.
- **Drafts (SAF-11):** `Draft: "invite"` keeps every multi line text field as a draft (`<Draft>.<Name>`; a field's `NoDraft: true`
  opts out), cleared on a successful submit.
- **A lost connection (FBK-9):** an `OnSubmit` that throws a network error never ends in an uncaught error: the values and the dirty
  state stay, a status line at the form (`div.sw-form-status`, `role="alert"`, before the submit button's row) says "Not saved: the
  connection was lost. Your entries are kept." with Try again; when the connection is back (`net.online`) it says so, or with
  `Resend: true` (saves that are safe to repeat) the form is sent again by itself. Other errors are shown in the same line.
- `Submit` as a `Sw.Button` without its own `OnClick`: the form sets `OnClick` to `Submit()`, so the button is busy and absorbs repeats
  (one save per click). `Submit()` resolves false when it was not saved.
- `Required` fields get the visible marker (also a Toggle and a field marked required only by the form) and `aria-required`.

```js
const name = new Sw.TextBox({ Label: "Company name" });
const mail = new Sw.TextBox({ Label: "Billing e-mail", Type: "email" });
const plan = new Sw.Select({ Label: "Plan", Items: [{ Value: "", Text: "Choose a plan" }, { Value: "team", Text: "Team" }] });
const save = new Sw.Button({ Text: "Save", Kind: "primary" });
const form = Sw.Form({
    Fields: [
        { Name: "Name", Control: name, Required: true },
        { Name: "Mail", Control: mail, Required: "Enter a billing e-mail.", Validate: v => /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(v) ? null : "Enter an e-mail like name@example.com." },
        { Name: "Plan", Control: plan, Required: "Choose a plan." },
    ],
    Submit: save,
    Title: "the billing details",            // "Leave without saving? Your changes to the billing details are lost."
    OnSubmit: v => Sw.Net.Send("../Api/my/Save", v),   // the button says "Save: done"; no "Saved" toast (FBK-5)
});
```

### Wizards: `Sw.Steps` (page.js)
A step by step flow with one anatomy: the step list (numbered marks; done = a check, the current one `aria-current="step"`; on narrow
containers "Step 2 of 4" and a progress line), the step heading (focused on every move, so screen readers hear the step), the content, an
inline step message and a footer: Cancel (ghost, left), Back and Next / the finish button (the one primary button, right).

`Sw.Steps({ Steps: [{ Title, Sub, Content, Form, Validate(steps), OnShow(steps) }], Label, FinishText, NextText, BackText, OnFinish(steps), OnCancel(steps), Start, Class })`
-> `{ E, Index, Count, Go(i), Next(), Back(), SetBusy(on), Body, Step(i) }`

- Next validates the step first: its `Form` (`Sw.Form`: messages under the fields, focus on the first bad one), then `Validate`
  (false or a message = stay; a message shows inline above the footer; a promise keeps the buttons busy).
- Enter in a single line field = Next. Done steps are buttons in the step list (go back); later steps open only in order.
- `Content` may be a function `(steps) => content`, built once when the step is first shown (ex: the review step fills in `OnShow`).
- `OnFinish` may return a promise (the finish button is busy); put the result on the page (`Sw.StateView`) or close the dialog.

```js
const steps = Sw.Steps({
    Label: "Create a workspace",
    Steps: [
        { Title: "Company", Sub: "Who is the workspace for?", Content: [name, mail], Form: companyForm },
        { Title: "Plan", Content: [plan, seats, new Sw.Field({ Label: "Billing", Control: billing })], Form: planForm },
        { Title: "Review", Content: review, OnShow: () => review.replaceChildren(Sw.Facts({ Items: facts() }).E) },
    ],
    FinishText: "Create workspace",
    OnCancel: () => history.back(),
    OnFinish: async () => { await create(); page.E.replaceChildren(Sw.StateView({ Icon: "check", Title: "Workspace created" }).E); },
});
page.Add(Sw.Card({ Body: steps }));
```

### Value cells (cells.js)
`Sw.Cells.Update(el, type, value, flash, format, nextValue, onRefresh)` renders a value like the old `ValueFormat.update`: the format
`"Name;arg1;arg2..."` (TableDataColumn.Format: Default, Number, Url, Tags, PerRowFormat, ByteSize, ByteSpeed, Toggle, Actions, Img,
Duration, Json, Text, MD, Uptime, WebColor) picks the renderer, else the C# type (numbers, DateTime, TimeSpan, Boolean, Type), else Default.
* Url target prefixes: `+` / none = new tab, `*` = same window, `^` = top, `-` = parent. A link (`CreateLink`) keeps the browser's link gestures with a modifier key (Ctrl+click = new tab, INP-2); `OnLink` on other elements only focuses on a modifier click (it used to copy the url).
* Links to other sites (Url and Img cells; `Sw.Cells.External(url)`, `Sw.Cells.ExternalLink(url, text, ext)`): always a new tab with `rel="noopener noreferrer"`, an external icon after the text (`a.sw-link-ext`, `.sw-link-ext-icon`) and the tool tip "Opens <host> in a new tab (another web site)" after the server title. A web search (Google, Bing, DuckDuckGo, Yahoo `/search`) is named as one: the search icon before the text and "Search the web: opens <host> in a new tab and sends this search to it:" with the searched text. Offline (`navigator.onLine` false, or `Sw.Cells.Internet = false` set by an app that knows the internet is not reachable) these links are plain text (`span.sw-link-off`; an Img cell shows the picture without a link) with "Needs the internet (not connected): <url>" in the tool tip; explore/table.js renders the cells again on the browser's online / offline events.
* Actions: `text|title|url|icon` per button; `&url` opens in the same tab, `@url` in a new tab, anything else is a GET request; then `onRefresh()`. Destructive actions (`Sw.Cells.IsDestructiveAction`: Kill, Delete, Remove, Stop, Uninstall, Reboot, Reset ... in the text, the title or the link method name, or a title that warns "can not be undone") ask first: a dialog titled with the action ("Kill the process?") that names the item (`Sw.Cells.ItemName`: the row's `data-sw-item`, set by explore/table.js from the key columns), Cancel focused, the action as a danger button; this is `Sw.Cells.ConfirmDanger(o)` ({Title, Item, ItemLabel, Text, Warning, OkText, OkIcon, OkTitle, CancelTitle} -> Promise<boolean>), the one confirmation of destructive actions (also used by edit/text.js Delete / Reload): warning icon, question title, item, "This cannot be undone.", Cancel / verb. Kill on process id 0, or id 4 named "System" (Windows), is disabled (the server sends no per row availability). TimeSpan / Duration cells use one short style per column: µs / ms below 10 s, "48.3 s", then two units ("9 min 19 s", "1 h 18 min", "2 d 18 h"). Tags are filled pills as the original (`sw-tag`: `--sw-accent1` with bold `--sw-on-accent` text, `--sw-radius-sm`; owner report 4 #9); the "🔑" tag of an empty list has the tool tip "Any signed in user". An auth requirement column (C# `TableDataAuth`, the Tags format with the 6th part "Auth") shows `Sw.Cells.AuthTag(value)`: null = the "Open" tag in the second accent (`sw-tag-open`: anyone, no sign in; it stands out from the token tags), "" = the outlined "Any logged in user" tag (`sw-tag-signedin`), else one tag per token; the api inspector and the explore listing use the same tags. Img cells with a short word value (ex: POST / GET) use the value as alt text and tool tip instead of the image url.
* Toggle: true / false texts and titles, the api url. Failed requests show a fail toast.
* **Row action results (Actions / Toggle requests; FBK-2, FBK-5, FOC-1):** the server's outcome decides: a failed request **or a
  `false` answer** is "not done" (never "done" unless the server said so). Before any toast the button dispatches a bubbling
  **`sw-cell-result`** event, `detail { Ok, Aborted, Value, Error (the reason), Text (the action), Name (the row), Reason (the reason as one sentence: capitalised, a period, the advice once), Note (the short visible text for the row: "Kill: not done. Process #24224 is protected by policy."), Outcome (the server's explicit kind, `X-Sw-Outcome` via `Sw.Net.OutcomeKind`: "AlreadyDone" → "Kill: not needed. …", "Gone" → "Kill: no longer there. …", "Refused" / "Denied" / "Invalid" / "" → "Kill: not done. …"; never guessed from the status code: a 409 without the kind is "not done"), Final (AlreadyDone or Gone: the row is final, its actions go like after a done final action), Neutral (= Final, kept for older tables), Work (the one name:
  the confirmation's title "Kill msedge (63564)", else "Kill (msedge)"), Url, Ms, InPlace: false }`; a listener that shows the result
  where it happened (explore/table.js, when the row is on the screen) sets `detail.InPlace = true`: then no toast, and the activity job
  (after 2 s in the shell) ends quietly (`job.Quiet`). Else a failure is one toast "Kill msedge (63564): not done. <reason>".
  `onRefresh(detail)` gets the same object. The busy button keeps its width (the spinner at the icon's size), leaving asks with the
  true consequence (`Kind: "server"`), and the focus stays in the table (`Sw.Focus.Keep` from the button, also for "not done" and after
  a confirmation: the row's note, the same action, or the next row; restored while lost up to 2 s after the result). A row that went
  away during its confirmation or its work (a refresh, another admin; `detail.Gone`, `detail.Status`): done = one toast "<action>:
  done", a 404 / 409 / 410 = an information toast with the server's reason (most likely done elsewhere), never an error.
  The type-to-confirm word is a check, not an edit: Escape closes the confirmation (it never steps the word back).
  A type-to-confirm `ConfirmDanger({ Confirm })` disables its danger button with the reason "Type anna to confirm" (also said on Enter),
  and its word is no user data (Cancel never asks "Discard changes?"); the Item line is left out when the title shows its values.
* Json / Text / MD cells show a shortened text; click / Enter opens the full text in a dialog (markdown via `common/marked.js`, loaded on
  demand, raw html restricted to formatting tags, script urls removed).
* Uptime / empty DateTime cells stay live (one shared timer). Img cells: a picture that fails to load shows the cell value as text (`span.sw-cell-img-text`, ex: "GET", "SE") instead of a broken image; `iso_data/` flags are requested directly, as the original (no `Api/HaveService` probe); without the IsoDataService a flag fails to load and the value is shown as text. Number cells round to the format's decimals as the original ("Number;4": 0.0000); a Number format with a fifth option (C# `TableDataCurrency`: an ISO 4217 code or a template like "{1}" = the next column) is an amount, shown with the currency symbol (and, for negative decimals, its decimals) from `iso_data/currencies.js` (loaded on demand) and the same separators for every currency ("$1 234.50", "1 234.50 kr"), else "1 234.50 XYZ". Most cells have a copy action, as the original: a plain click copies the value (`Sw.Clipboard.CopyOnClick`), also Ctrl+C on the focused value and its menu (right click / long press). Disabled cell actions are aria-disabled with the reason as tool tip. Read only Boolean cells show "☑ True" / "☐ False" (null too; False in the second accent), as the original; Toggle cells are the original's check box pill (`button.sw-cell-toggle[role=checkbox]`: a check box and the text, filled with `--sw-accent2` when checked, a light tint of it when not; owner report 4 #8) and an Actions button of the same row with the same request is left out; an empty Tags value shows "🔑", as the original. Duration / TimeSpan cells use the original one unit text (`Sw.Format.TimeSpan`: "0", "2 672.1 ms", "559.0 seconds", "78 minutes", "66 hours", "11 days"; a number of whole seconds without the decimal: "1 second", "30 seconds", over 1 500 000 000 s "∞", ex: the cache durations of the APIs table, C# `TableDataDuration` on an int), Uptime cells the original elapsed text (`Sw.Format.DurationMs`: "41 seconds", "16 minutes"), a "{0} s" Number cell is a plain number ("3 600 s"), sizes use the units KB, MB, GB ("12.3 KB", "5.0 MB", "716.7 GB"), a SysWeaver.MessageLevels value is its number (the level name in the tool tip), and a tiny number rounds as the original ("0.000"; P2-A).
* Renderers: `Sw.Cells.Default`, `Number`, `Url`, `Img`, `Tags` (and `Sw.Cells.AuthTag(auth)`, the tag of an auth requirement), `PerRowFormat`, `ByteSize`, `ByteSpeed`, `Toggle`, `Actions`, `TimeSpan`
  (`Duration`), `Json`, `LongText`, `MarkDown`, `DateTimeLive` (`Uptime`), `DateTime`, `Boolean`, `Type`, `WebColor` (old signature
  `(el, value, formats, nextValue, flash, type, onRefresh)`); maps `TypeFormatters`, `FormatFormatters`; helpers `Reset`, `SetText`,
  `SetNodes`, `UrlTarget`, `CreateLink`, `External`, `ExternalLink`, `OnLink`, `ShowJson`, `ShowText`, `ShowMarkdown`; setting `Internet` (null / false).
* Classes: `Sw.Cells.Classes` (replaceable, ex: by the compat layer with the old names): `sw-right`, `sw-left`, `sw-center`, `sw-mono`,
  `sw-null`, `sw-false`, `sw-cell-text`, `sw-cell-actions`, `sw-capped`, `sw-cell-link`, kind `sw-fmt-<Format>`. Pass `"sw-flash"` as flash to
  highlight changes. The flash classes of one task start together at its end (one style pass, never a layout per cell; S7a-A2-2), so
  a live refresh of many changed cells stays cheap and the animation's first painted frame is its start (removed after 1050 ms).
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
`SetDots([{ Line, Kind, Title }])` (clickable gutter dots, ex: a breakpoint; option `OnDot(line, dot, event)`), `SetSpans([{ Line, Col, Len, Kind }])`
(highlighted text ranges, class `sw-code-span sw-<Kind>`), option `OnAltClick(info, editor)` (Alt + click: the word under the mouse, or the
selection when the click is inside it), option `ContextMenu(info, editor, where, event)` (a right click, the menu key or Shift+F10: the menu
items it returns for the word / selection, `where = { Pos, Line, Col, InComment }`, then the editor's own cut, copy, paste, select all, find,
replace, go to line, comment; a right click outside the selection moves the cursor first; with a modifier key, or a touch long press, the
browser's menu shows; without the option the browser's menu shows), `Undoable()` (false while the editor is hidden: edits of a hidden editor, ex: an inactive tab, are
applied directly without undo, a hidden textarea can't take the focus).
Keys: Tab / Shift+Tab, Enter (keeps the indentation), Home (smart), Ctrl+/ (comments), Ctrl+D, Ctrl+Shift+K, Alt+Up / Down, Ctrl+F / Ctrl+H
(find / replace: case, word, regex), F3 / Shift+F3, Ctrl+G, Ctrl+Space. Colors: `--sw-code-*` tokens (a light variant for the light theme). GLSL `// var: { ... }` setting comments are dimmed (class `cv`): the code stays the focus.

## Icon classes (icons.css)

Every icon class named by the server (C# `IconClass` values, contract Appendix B) and by the pages is defined as a css class setting
`mask-image` to an svg of the icon module (`../icons/*.svg`): the element's background color shows through the mask, so icons follow the
theme (`Sw.IconClass` elements use the text color; old `ColorIcon` elements their `IconColorTheme*` class). `IconLogo`, `IconCurrentUser(Fill)`
and `IconFile*` are full color background images. `IconWorking` is animated. The four color classes `IconColorThemeBackground/Main/Acc1/Acc2`
are defined too. Pages may override a class in their own style sheet. Icon "classes" may also be urls (`icons/x.svg`, `../icons/x.svg`,
a trailing `*` for full color images). `IconOk` (dialog OK / confirm buttons) is a check mark drawn by an inline data: svg (the icon module has no check mark file; it used to be the `fav_on.svg` heart). The gallery lists every class; Debug / Icons (explore/icons.html) shows each svg with its class names and copies the class name.

**One outline style.** The shell menu, the rail and the top bar menus draw the IconModule icons as their line twins (`Sw.LineIcon(image, cls)`,
`Sw.Icons.LineOf(image)`, the `LineIcons` table in base.js: home, alert, list, fileText, chart, db, session, puzzle, cpu, shield ...; one
glyph per svg file, 24 px grid, 1.75 stroke, `currentColor`), so filled and outline art never mix. An app's own menu icon keeps its art as a
monochrome mask: draw it as a 24 px outline with a 1.5 to 2 px stroke, or give it a line twin with `Sw.Icons.Add(name, svg)` +
`Sw.Icons.AddLine("orders.svg", name)` in `app/app.js`.

**One icon per page.** The rail shows icons only, so two menu items must never show the same glyph. Distinct classes for
framework pages that used to share one: `IconProblems` (warning), `IconLogFile`, `IconSessionCache`, `IconFolderManaged` / `IconFolderShared` /
`IconFolderRemote`, `IconTableRefs`, `IconTableEmails`, `IconTableMessages`, `IconAiImageModels`, `IconAiTextModels`, `IconEndPoints`,
`IconTableClients`, `IconLoadedServices`, `IconKeyFiles`, `IconBackgroundEffects`, `IconImageEffects`, `IconCollageEffects`. The shell
(`shell/menu.js` `MenuIcons` / `DistinctIcons`) applies them by page when a server still sends the shared icon (only when two pages of
one menu show the same svg); apps name a distinct icon in their `WebMenu*` attributes. In the rail the icons use `--sw-text-2` (they are
the only cue there).

Folders count too. A page that shows its folder's glyph gets its distinct class (ex: OpenAI LLM models, `IconAiTextModels`,
now chat, under the "AI models" brain folder); when no distinct class exists the folder gives way and shows `IconMenuFolder` (a plain
folder). The menu has a scroll cue: a cut-off edge fades and a "More menu items" chevron under the tree scrolls it (sidebar, rail and
drawer; `shell/README.md`).

## Gallery

`ui/gallery.html` (served at `/ui/gallery.html`) is the showcase and test bench of the library. An intro says what the library is and how
to load it, with links to this README and the [quick start guide](../../../_docs/WebQuickStart.md). Every card names its API (`Sw.Button`,
`Sw.ButtonRow` ...), links to its README section (Docs) and has a Code toggle with a minimal copyable snippet (Copy); the snippets live in
the `Docs` table of `gallery.js`. It shows every component in every state (header: theme select, mobile layout toggle, keyboard help; `?` and `Mod+K`
shortcuts). It loads the source files one by one, or the bundles with `?bundle`. Core url flags work (`?settheme=dark`, `?mobile=true`).
Elements that automated tests use carry `data-g` attributes. The demo texts are not translated (a developer page, `translate="no"`).
The Brand card shows the app icon and logo ("App icon", "App logo"), the 4 theme colours and the 7 chart slots `--sw-chart-1..7` of the current theme (`data-g="chart-slots"`, in legend order, see the chart README). The Icons card lists every icon class of the style sheets; classes whose svg comes from an optional service (the 4 passkey icons of the PassKey service, `OptionalIconFiles`) are only shown after one `Api/HaveService` call found the service, so apps without it request no missing files.
