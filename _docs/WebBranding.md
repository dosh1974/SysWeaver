# Branding a deployed app

> How to give an application built on SysWeaver its own name, logo, icons, colors, themes and page styles, without forking or editing the framework. Everything here is plain files (and one manifest entry). It works offline: put fonts and images in your own files, never a CDN or a web font service.

Related: [Web.md](Web.md) (web client overview), [core/README](../SysWeaver.HttpServer/srcWeb/core/README.md) (`Sw.Theme`), [shell/README](../SysWeaver.HttpServer/srcWeb/shell/README.md), [app/README](../SysWeaver.HttpServer/srcWeb/app/README.md), [brand/README](../SysWeaver.HttpServer/srcWeb/brand/README.md) (the fonts).

## The default: the SysWeaver brand

Out of the box every app uses the SysWeaver look (the copper and slate themes, the fonts, the layout). The icon and logo are not SysWeaver's: the server generates them per app from the app name and `AppSeed`, so an app is never branded as SysWeaver. You change only what you want to change; everything else stays as it is.

| Part | Default | Where it is defined |
|---|---|---|
| Light theme (also "(Automatic)" on a light system) | Background Mist 50 `#F5F7F9`, Main Slate 900 `#1D2A33`, Accent 1 Copper 700 `#A85724`, Accent 2 Steel 600 `#4E6B80` | `common/theme.css` (`:root`) |
| Dark theme (also "(Automatic)" on a dark system) | Background Slate 900 `#1D2A33`, Main Mist 50 `#F5F7F9`, Accent 1 Copper 500 `#E5823F`, Accent 2 Steel 300 `#9FB6C8` | `common/theme.css` (`[data-theme="dark"]`) |
| Text on an accent fill | The theme background (brand rule): 4.8:1 in light, 5.3:1 in dark | `--sw-on-accent` in `ui/tokens.css` |
| Fonts | Barlow (text, `--ThemeFont`), Saira Semi Condensed SemiBold (headings and titles, `--ThemeFontHead`) | `brand/fonts.css`: local data urls, imported by `common/theme.css` |
| Icon, logo and banner in pages | The app's square icon (`../icon.svg`), 4:3 logo (`../logo.svg`) and 9:2 banner (`../banner.svg`, 1260 x 280: the shell's brand bar and the sign in card; an app banner of another shape also sets `--sw-brand-banner-ratio`, its width / height, default 4.5), the same in every scheme | `--sw-brand-icon` / `--sw-brand-small` (icon), `--sw-brand-logo` / `--sw-brand-stacked` (logo) and `--sw-brand-banner` (banner) in `ui/tokens.css` |
| Browser and install icons | `icon.svg` (square), `logo.svg` (4:3, 512x384), `favicon.ico`, `apple-touch-icon.png`, `icon-180/192/512.png`, `logo.png`: generated per app by the server (`HttpServerBase_Favicon.cs`) from the app name / `AppSeed` in the application colors (`HashColors.AppColors`), unless the app's web folder serves a file of that name | `app/manifest.json` lists `icon.svg`, `icon-180/192/512.png` and `logo.svg` (as screenshot) |
| Weave | Behind the stand-alone sign-in and Welcome pages, with the content on a solid panel | `.sw-weave` and `.sw-weave-panel` in `ui/tokens.css` |
| Chart colors | The chart viewer's default "Theme colors": slot 1 Accent 1 (copper), then blue, green, violet, magenta / ochre and Accent 2 (steel), stepped per scheme and checked for color vision deficiencies; status colors are never series colors. Series take the slots in legend order (see [Chart color slots](#chart-color-slots)). "Original" in the chart menu shows the server's colors. | `--sw-chart-1` .. `--sw-chart-7` in `ui/tokens.css`; `applyThemePalette` in `chart/chart.js` |
| User pictures | The shell user button and Home show `auth/UserImages/Current/small` as the server serves it: the uploaded picture, or the picture the server generates (AuthManagerService) | `auth/UserImages/...` (server) |
| Shell brand bar | The app banner (`../banner.svg`, 9:2, unaltered) at the top of the full sidebar and the phone drawer, the square icon (`--sw-brand-small`) in the collapsed rail. The icon is drawn exactly on the banner's left square (40 x 40 px on desktops, the banner 180 x 40) and the two crossfade while the menu expands / collapses, so with the generated banner (its icon sits in that square) the icon stays still. The banner names the app: there is no app name row under the bar (the bar has the name as its accessible name and tool tip), so an own banner should show the app name too. An own banner: keep it 3:1 and put the app icon in its left square (height x height) for the same effect; any other banner is drawn in the 9:2 box (left aligned) and simply crossfades. Size: `--sw-shell-brand-s` on `.sw-shell-brand` (the icon / banner height) | `.sw-shell-brand` / `.sw-shell-brandlogo` / `.sw-shell-brandmark` in `shell/shell.css` (`--sw-brand-banner`, `--sw-brand-small`) |

The framework pages show the icon and logo unaltered: no stretching, recoloring or effects (no glow, shadow or transparency). The Welcome page shows the logo (`../logo.svg`, 4:3), the sign-in page the square icon (`../icon.svg`).

The **Pro**, **Application** and **Hacker** themes are variants: they keep the structure (fonts, layout) and change colors and density. The server's Theme menu shows Hacker only to users with the Debug role (its menu entry has the auth "Debug", see [Limits](#limits-server-side)); every other user sees Light, Dark, Application and Pro.

### The built-in themes

| Theme | For | Look | Where |
|---|---|---|---|
| **Light** / **Dark** | Everyone (the "(Automatic)" pair) | The brand web app theme, see the table above | `common/theme.css` |
| **Pro** | Desktop work: much on the screen, a calm interface | Compact: about 13 % smaller text, controls and icons (13 px text and 30 px controls on a normal desktop, scaled up on large screens). Mid dark brand slate `#22303A` (between Slate 900 and Slate 700), Mist 100 text `#E6ECF0` (11.6:1), a lighter copper accent 1 `#EE9050` (5.6:1, also on raised surfaces) and Steel 300 accent 2 `#9FB6C8` (6.4:1), danger `#FF7A7F`, small radii, no background pattern. Touch screens and the phone layout keep 44 px touch targets and 15 px text. The Theme menu shows it with its own icon (`IconThemePro`, a display). | `common/theme.css`, `ui/tokens.css` (`:root[data-theme="pro"]`) |
| **Application** | Telling many open applications apart (one browser tab per service) | The colors of this application, generated by the server from the app name / `AppSeed` (see [Application colors](#application-colors)): a dark background, text and two accents in the app's hues, the same colors as the generated icon and logo. The shell marks the sidebar with a 3 px edge in accent 1 (`--sw-shell-edge`); everything else uses the same tonal surfaces as the other themes (no gradients, no glow). | `common/theme.css` (template), `shell/shell.css` |
| **Hacker** | Fun; work in a very dark room | A retro terminal: phosphor green text `#3FBF6A` (8.4:1, never bright white) on almost black `#050A06`, amber accent 1 `#D9A441`, teal accent 2 `#4CB8A8`, blue focus ring `#66B3FF`, monospace text, square corners (`--sw-radius*` 0), a faint text glow (the theme's own CRT look; page css never adds glows, PORTING.md B5), slowly scrolling scanlines (they stand still with `prefers-reduced-motion`) and a steady block cursor after the page title in the shell (no blinking, UX guide MOT-6). | `common/theme.css`, `ui/tokens.css`, `shell/shell.css` |

The Light, Dark, Pro and Hacker themes meet WCAG AA: text, accent text, the text on accent fills (the background color), the danger color and the focus ring reach at least 4.5:1 against the background. The Application colors are generated from the seed and are not contrast checked; preview a seed before you pick it (see below).

### Application colors

The server derives the colors of the **Application** theme from the app name and the application seed only: `AppName` and `AppSeed` in the `SysWeaver.AppInfo` entry of the `*.Services.json` (`HashColors.AppColors`). There is no setting for explicit colors: to change the app colors, change `AppSeed`. The same colors are used by the generated icon and logo (`icon.svg`, `logo.svg` and the PNG / ICO sizes when no web folder serves those files) and by the e-mail templates (UserManager: `[Background]`, `[Color]`, `[Acc1]`, `[Acc2]`), so they fit together.

The colors reach the pages through the template variables of `common/theme.css`, which `[data-theme="application"]` uses:

| Template variable | Value | Theme variable |
|---|---|---|
| `${Color.Background}` | `HashColors.AppColors.Background` (the complement hue, very dark) | `--ThemeBackground` |
| `${Color.Color}` | `HashColors.AppColors.Acc1` | `--ThemeMain` (text) |
| `${Color.Acc1}` | `HashColors.AppColors.Acc3` (main hue + 30°) | `--ThemeAcc1` |
| `${Color.Acc2}` | `HashColors.AppColors.Acc4` (main hue - 30°) | `--ThemeAcc2` |

The theme sets `--ThemeFocus: var(--ThemeMain)` and `color-scheme: dark`. The Sw tokens (`--sw-bg` ... in `ui/tokens.css`) derive from the four theme variables as in every theme. Template `Variables` cannot override `Color.*`.

Choose the seed so that the apps that are often open together get hues that are far apart (at least 40°): Debug > App theme (`logo_debug.svg?<seed>`) previews a seed. Apps with the same name and seed look the same.

## Where brand files live

The web server searches every registered web folder for a requested file. The folder added **last** wins, so a file in your app overrides the framework's file with the same path. Use one of these places:

| Where | When | How |
|---|---|---|
| The app's own `srcWeb` folder, for example `MyApp/srcWeb/app/app.css` | You build the app | The app project compresses `srcWeb` into `web/*.br` and embeds it. The manifest entry of the app's files type (for example `FakeEmailService.FakeEmailServiceFiles`) registers them. Rebuild the app after a change. |
| A folder on disk, served by a `FileHttpServerModule` entry in the manifest that is added after the others | You deploy someone else's build (ops) | Put the files in that folder. They are served live with no rebuild. |

The framework always answers `app/app.css` and `app/app.js` (it serves them empty when no folder has them), so these two hooks never cause a 404, also in apps that don't use them. Only the shell requests them, as the original: the other pages get the brand css without a request (from the shell window, or the text the shell remembered for the site), and load `app/app.js` only when the shell found it has content, so a stand-alone page of a site never opened in the shell has neither. `app/app.js` runs in every page: the shell loads it, and every other page of the site (framed content pages such as `explore/table.html`, and pages opened on their own) loads it after its own scripts, so custom cell formats, edit plug-ins, line icons (`Sw.Icons.AddLine`) and themes registered there reach every page. Code that only makes sense in the shell goes in `async function AppMain()` (only the shell calls it) or behind `if (Sw.Page.IsTop)`.

### App wide extensions in app/app.js (edit plug-ins, cell formats)

Not every page loads every library: the sign-in page, the shell and most framed pages have no `edit/Edit.js`, and a shell pop-up editor loads it later. Register extensions of a library with the deferred calls of the core, which run at once when the library is there and otherwise as soon as it loads (also later in the same window):

```js
//  app/app.js: safe on every page
Sw.Extend.CellFormat("Stars", (e, value, formats, nextValue, flash) => { /* see explore/README.md "Custom cell formats" */ return e; });
Sw.Extend.EditSpec("Stars", typeName => typeName === "System.Int32" ? new StarsPlugin() : null);   // see edit/README.md
Sw.Ready("Chart", Chart => { /* anything else that needs Sw.Chart */ });
```

`Sw.Ready(name, fn)` takes the `Sw` member name (`"Edit"`, `"Cells"`, `"Table"`, `"Chart"` ...) and returns a promise of the library. A direct call such as `Sw.Edit.RegisterSpec(...)` or `Sw.Cells.FormatFormatters.set(...)` at the top level of `app/app.js` is queued the same way instead of throwing, so the rest of the file still runs on pages without the library. Inside functions that run later (`AppMain`, event handlers) a missing library is `undefined` as before: use `Sw.Ready` there, or test `if (Sw.Edit)`. Each queued registration is guarded: an error is logged and the next one still runs.

## What you can brand

| What | How | Reaches |
|---|---|---|
| **Name** | The `SysWeaver.AppInfo` entry of the manifest: `AppName`, `AppDisplayName` (window title, Home page heading, PWA manifest), `AppDescription` (Home page), `AppVersion` (the version shown on the Home page; default the executable's version, `PathTemplate` variables allowed) | Every page and the server templates |
| **Icon and logo in pages** | By default the pages show the app's `../icon.svg` (`--sw-brand-icon`: sign-in; `--sw-brand-small`: the collapsed rail) and `../logo.svg` (`--sw-brand-logo`: Welcome and the brand bar at the top of the shell sidebar; `--sw-brand-stacked`). In `app/app.css`, point them at your own files, per scheme if needed (`:root[data-sw-scheme="dark"] { ... }`). | Every page |
| **Browser and install icons** | The server generates `icon.svg` (square), `logo.svg` (4:3, 512x384), `favicon.ico`, `apple-touch-icon.png`, `icon-180.png`, `icon-192.png`, `icon-512.png` and `logo.png` from the app name and `AppSeed` (`HttpServerBase_Favicon.cs`). To use your own, put files of the same names at the site root of your web folder; a served file wins over the generated one. The framework ships none of these files. | The browser tab, installed apps, the manifest |
| **Colors** | `app/app.css`: override the theme variables, or define your own theme (see below) | Every page (shell, content frames, stand-alone pages, old format pages) |
| **Own themes** | A css theme in `app/app.css`, or `Sw.Theme.Register` in `app/app.js`. Make one the automatic theme with `--sw-theme-light` / `--sw-theme-dark`. | Every page; also listed in the shell's settings menu when registered with a `Title` |
| **Page styles** | Any css in `app/app.css` (fonts, radii, spacing, your own page classes). Use the role tokens (`--sw-text`, `--sw-surface`, `--sw-border` ...) and the radius / space tokens in your rules, no shadows on permanent surfaces and no gradients (`SwWeb check --design`). Scope element rules to your own classes: a bare `button`, `input`, `select`, `textarea`, table cell, `img` or `iframe` rule, a `font-size` on a bare `body` or `body { overflow: hidden }` also changes every Sw component (tables, pagers, dialogs, menus) on every page; `SwWeb check` warns (`element-css`, `body-overflow`) | Every page |
| **Weave** | `app/app.css`: your own pattern on `.sw-weave` or none (step 4c); `.sw-weave` / `.sw-weave-panel` for your own pages | The sign-in and Welcome pages, pages that use the class |
| **Chart colors** | `app/app.css`: `:root { --sw-chart-2: #...; }` (any of `--sw-chart-1` .. `--sw-chart-7`; slot 1 follows `--ThemeAcc1` and the steel slot `--ThemeAcc2` by themselves). Series n of a chart takes slot n in legend order; the defaults per scheme are in [Chart color slots](#chart-color-slots). Keep neighbors apart for color blind users (check the set with a color vision deficiency simulator) and at least 3:1 against the background. | Every chart (`chart/chart.html`, chart tiles, ServerManager donuts) |
| **Online password leak check** | `app/app.css`: `:root { --sw-password-leak-check: off; }` turns off the only internet request of the auth pages (see [Web.md: Working offline](Web.md#working-offline)) | The sign-in and password pages |
| **Shell only** | `app/app.css` rules on the shell elements (`app-header`, `app-menu`, `app-ContentTitle` (the breadcrumb container), `app-content`, `app-PinButton`; header icons are `.sw-shell-hicon` svgs, see PORTING.md B15) and `app/app.js` (`async function AppMain()`, `Sw.Shell.Params` / `window.SysWeaverAppParams`) | The shell (index.html) |

### Chart color slots

The chart viewer's default "Theme colors" mode gives the series of a chart the slots `--sw-chart-1` .. `--sw-chart-7` **in legend order**: the first series in the legend has slot 1, the second slot 2, and so on. Series 8 and up reuse slots 1, 2 ... lighter, then darker. Series whose server colors have the same hue share a slot. In a chart whose bars are colored by group (the method graph, `Api/PerformanceChart`: one color per system), each group is one series, and the legend lists the groups in slot order, also when the bars are sorted by value. A color stays with its series when the user re-sorts the chart.

The defaults (`ui/tokens.css`). The order of the last three differs per scheme:

| Slot | Light themes (Light, light scheme) | Dark themes (Dark, Pro, dark scheme; Hacker and Application have their own series hues) |
|---|---|---|
| `--sw-chart-1` | Accent 1 (`--ThemeAcc1`; Light: copper `#A85724`) | Accent 1 (`--ThemeAcc1`; Dark: copper `#E5823F`) |
| `--sw-chart-2` | blue `#2A78D6` | blue `#3987E5` |
| `--sw-chart-3` | green `#138A60` | green `#199E70` |
| `--sw-chart-4` | violet `#6250D6` | violet `#9085E9` |
| `--sw-chart-5` | magenta `#C4507E` | ochre `#C98500` |
| `--sw-chart-6` | ochre `#A87400` | Accent 2 (`--ThemeAcc2`; Dark: steel `#9FB6C8`) |
| `--sw-chart-7` | Accent 2 (`--ThemeAcc2`; Light: steel `#4E6B80`) | magenta `#D55181` |

Overriding one slot keeps the other six, so choose the set as a whole. For example, an ochre slot 3 sits next to the default ochre slot (light 6, dark 5) and two legend entries look alike: override that slot too. The Dark theme needs its own values (`:root[data-theme="dark"] { --sw-chart-3: ...; }`; not `data-sw-scheme`, which would also replace the own series colours of Hacker and Application, see [Selectors](#selectors-which-rule-reaches-which-theme)). The gallery (`ui/gallery.html`, Brand card, "Chart slots") shows the 7 current values of the selected theme, including your `app/app.css`.

### How app/app.css reaches every page

- The shell (`index.html`) links `app/app.css` after its own styles.
- Every other page applies it through the theme engine (`core/theme.js`, part of `sw/core.js` and of the legacy `common/common.js`). The page fetches `app/app.css` once, which is a cached, empty 200 in apps without branding. Only when the file has content, the page adds it as the **last** style sheet of the document and re-applies the theme. Your rules therefore win over framework and page rules of the same specificity. `Sw.Theme.BrandReady` resolves when this is done.
- No frame shows the SysWeaver brand first. The sign-in page, Home and Welcome link `app/app.css` render blocking, like the shell. Every page remembers what it found per site in `localStorage["SysWeaver.ThemeBrand"]` (up to 1 M characters of css), and the next page applies that copy before its first paint (the fetched file replaces it if it changed). Only on the very first visit of a site, before anything is known, a page that does not link the file stays hidden until the brand was applied (at most 3 s). Apps without `app/app.css` make no extra request and never wait after the first page.
- Write `url(...)` relative to `app/`, for example `url(fonts/acme.woff2)` for `app/fonts/acme.woff2`.
- `@import` is not supported in `app/app.css`. Put everything in the one file.

### The theme variables

Every color of the UI comes from four base variables, defined per theme in `common/theme.css`:

| Variable | Meaning |
|---|---|
| `--ThemeBackground` | Page background |
| `--ThemeMain` | Text |
| `--ThemeAcc1` | Accent 1: links, titles, selection, primary buttons |
| `--ThemeAcc2` | Accent 2: secondary accent |

The Sw tokens (`--sw-bg`, `--sw-fg`, `--sw-accent1`, `--sw-accent2` and every Design v2 role derived from them in `ui/tokens.css`: surfaces `--sw-canvas`, `--sw-nav`, `--sw-surface`, `--sw-surface-sunken`, `--sw-surface-raised`, lines `--sw-border`, `--sw-border-strong`, `--sw-border-input`, text `--sw-text`, `--sw-text-2`, `--sw-text-3`, `--sw-text-accent`, states `--sw-hover`, `--sw-selected` ..., status, elevation; the full table is in [ui/README "Theming"](../SysWeaver.HttpServer/srcWeb/ui/README.md#theming)) follow automatically. Other theme variables: `--ThemeFont` (text), `--ThemeFontHead` (headings, titles), `--ThemeFontMono`, `--ThemeFocus` (focus ring color).

**Change the four base colours (or a whole theme), not the derived tokens one by one**: the roles are computed per scheme (light and dark mix differently), so a hand set `--sw-surface` breaks in the other scheme, in Pro and in the Application theme. Shape tokens may be set for the whole app: the radius scale (`--sw-radius-sm`, `--sw-radius`, `--sw-radius-md`, `--sw-radius-lg`; Pro and Hacker set their own), the chart slots, the icon / logo variables (`--sw-brand-*`). Sizes come from the **density** (`html[data-density]`: compact, comfortable, touch; the user picks it in the settings menu, `?density=` in the url): set `--sw-control-h` / `--sw-row-h` only on a page scope of your own, never fixed 44 px heights.

Keep the contrast at WCAG AA: text and accent text at least 4.5:1 on the background, and the text on accent fills (the background color) at least 4.5:1 on both accents. The focus ring (`--ThemeFocus`) must stand out from the accent: the defaults are blue, never copper.

#### Selectors: which rule reaches which theme

`<html>` carries two attributes. `data-theme` names the css theme that is applied: none for Light, `dark`, `pro`, `application`, `hacker`, or the name of your css theme. `data-sw-scheme` is `light` or `dark` and only says how the applied theme is built, so **every** dark theme has `data-sw-scheme="dark"`: Dark, Pro, Application, Hacker and your own dark themes.

| Selector in `app/app.css` | Reaches | Use it for |
|---|---|---|
| `:root:not([data-theme]) { --ThemeAcc1: ...; }` | Light only | your accent in the light theme |
| `:root[data-theme="dark"] { --ThemeAcc1: ...; }` | Dark only (and code themes, see below, that don't set the variable in `Colors`) | your accent in the dark theme |
| `[data-theme="acme"] { ... }` | your css theme `acme` only | the colours of your own theme (step 1) |
| `:root[data-sw-scheme="dark"] { ... }` | **every** dark theme: Pro, Application and Hacker too | marks and pictures that must fit a dark background (step 4b), the dark weave (4c); **not** colours |
| `:root { --ThemeAcc1: ...; }` | **every** theme: Pro, Application and Hacker too | only when your brand replaces the accent everywhere |

The trap: an accent per scheme written as `:root[data-sw-scheme="dark"] { --ThemeAcc1: ... }` (or one `:root` accent) also replaces the accent of Pro and Hacker, and the Application theme loses the per-app colour that tells the open apps apart. Set the accent of Light and Dark with the first two selectors and leave Pro, Application and Hacker alone. The same holds for the chart slots: Hacker and Application have their own series colours, so set dark slots with `:root[data-theme="dark"]`.

A theme registered **in code with `Colors`** (step 3) has no selector of its own: applying it sets `data-theme` to its `Base` (else `dark`, or none for `Scheme: "light"`) and its four colours inline on `<html>`, so a `[data-theme="night"]` rule never matches. If the theme needs css of its own, make it a **css theme**: the colours in `[data-theme="night"] { ...; color-scheme: dark; }` in `app/app.css`, and `Sw.Theme.Register("night", { Title: "Night", Scheme: "dark" })` without `Colors`. Then `data-theme="night"` is set, its own rules match, and it also works on stand-alone pages that never saw the shell.

### Light or dark: declare the scheme

Whether a theme gets the light or the dark intermediate colors (input fill, raised surfaces, text on accents, status colors) depends on the scheme the theme **declares**, not on its name. `Sw.Theme` sets `data-sw-scheme="light"` or `"dark"` on `<html>` from:
1. the `Scheme` of a registered theme; else
2. the css `color-scheme` of the theme; else
3. the lightness of its `--ThemeBackground`.

Always write `color-scheme: light` or `color-scheme: dark` in a css theme. It also gives form controls and scrollbars the right look.

### A complete css theme

The four colours are enough for a theme, the rest follows from them. A theme with its own look (a console, a terminal, a squarer style) sets what the built in themes set:

| What | Properties | Default when missing |
|---|---|---|
| Colours and scheme | `--ThemeBackground`, `--ThemeMain`, `--ThemeAcc1`, `--ThemeAcc2` (`#rrggbb`), `color-scheme` | required |
| Fonts | `--ThemeFont` (text), `--ThemeFontHead` (headings), `--ThemeFontMono` (code) | Barlow, Saira Semi Condensed, Cascadia Mono |
| Focus ring | `--ThemeFocus`: a colour clearly apart from both accents and the danger red | blue (#1a66d6 light, #6cb6ff dark) |
| Corners | `--sw-radius-sm`, `--sw-radius`, `--sw-radius-md`, `--sw-radius-lg` (Sw controls, cards, dialogs) and `--ThemeRad`, `--ThemeRadInp`, `--ThemeRadInp2` (old page elements) | 4 / 6 / 8 / 12 px |
| Status colours | `--sw-ok`, `--sw-warning`, `--sw-danger` (>= 4.5:1 on the background), `--sw-ok-soft`, `--sw-warning-soft`, `--sw-danger-soft`, `--sw-on-danger`; `--ThemeOk`, `--ThemeWarning`, `--ThemeError` for old pages | per scheme |
| Chart series | `--sw-chart-1` .. `--sw-chart-7`, when an accent is close to a default series hue (a blue accent 1 next to the blue slot 2) | slot 1 = accent 1, slot 6 = accent 2 |
| Weights | `--sw-weight-medium` (labels, buttons) and `--sw-head-weight`, ex: 400 / 700 for a font without a medium weight (Consolas) | 500 / 600 |
| Motion | `--sw-motion` (1 = normal; Pro 0.25) and the slide distances `--sw-dist-1` .. `-3` | 1 |
| Density | `--sw-theme-density: compact` (or `comfortable`): the automatic density of the theme (a user's own choice and touch screens still win) | comfortable (Pro: compact) |

Write the `--sw-*` tokens with `html:root[data-theme="x"]`: the scheme blocks of ui/tokens.css (`:root[data-sw-scheme="dark"]`) have the same specificity as `:root[data-theme="x"]` and may come later. The Fenrir console theme (SysWeaver.Temp, `fenrir/Fenrir.css`, `FenrirTheme`) is a complete example.

## Worked example: "Acme" (placeholder brand)

"Acme" is a made-up brand used to show the mechanism. This example was verified on the Fake Email Service (port 8202): shell, embedded pages, stand-alone pages, desktop 2560x1440 and phone 390x844 / 844x390. The files were served as the app's `app/app.css` and `app/app.js`.

**1. A custom light theme, used automatically on light systems.** In `MyApp/srcWeb/app/app.css`:

```css
/* Acme brand: a custom LIGHT theme, and the automatic theme on systems in light mode */
[data-theme="acme"] {
    --ThemeBackground: #fff8f0;
    --ThemeMain: #3b2a1a;
    --ThemeAcc1: #c2185b;
    --ThemeAcc2: #00897b;
    color-scheme: light;
}

:root {
    --sw-theme-light: acme;     /* "(Automatic)" on a light system uses acme */
    /* --sw-theme-dark: acme-dark;   optional: the theme for dark systems (default: dark) */
}
```

**2. List the theme in the settings menu.** In `MyApp/srcWeb/app/app.js`:

```js
//  The colors are in app/app.css (no Colors here), the Title adds it to the settings menu's Theme section
Sw.Theme.Register("acme", { Title: "Acme", Description: "The Acme brand colors", Scheme: "light" });
```

Result: the header, menu, tables, filter boxes and buttons of every page use the Acme colors. Inputs get the light inset fill, not the dark one. The settings menu lists "Acme" after the server's themes. The user can still pick Light, Dark, Application or Pro (and Hacker, for users with the Debug role). "(Automatic)" follows the OS: Acme on light systems, Dark on dark systems.

**3. A dark theme defined in code (alternative).** Verified on the Web Server Service (port 8210). In `app/app.js`:

```js
Sw.Theme.Register("night", {
    Title: "Night",
    Description: "Dark blue with an orange accent",
    Scheme: "dark",
    Colors: { Background: "#0b1a33", Foreground: "#d8e1f0", Accent1: "#ff6a00", Accent2: "#4fc3f7" },
});
```

`app/app.js` runs in every page (the shell, framed pages and stand-alone pages), but in a content page it runs after the page's first theme pass. So content frames ask the shell for the definition, and stand-alone pages (a page opened in a new tab, for example) use the copy that `Sw.Theme` keeps in `localStorage["SysWeaver.ThemeDefs"]` once the theme has been applied. If a stand-alone page must work before anyone has opened the shell, define the theme in css instead (step 1). A code theme with `Colors` is applied as `data-theme="dark"` (its `Base`, or none for a light scheme) with the colours inline, so css rules cannot target it by its name: when the theme needs css of its own, use a css theme (see [Selectors](#selectors-which-rule-reaches-which-theme)).

**4. Page styles for the whole app.** Also in `app/app.css`:

```css
:root {
    --sw-radius-sm: 2px;        /* a squarer radius scale everywhere (controls, cards, dialogs) */
    --sw-radius: 3px;
    --sw-radius-md: 4px;
    --sw-radius-lg: 6px;
}

@font-face {                    /* a local font (never a CDN or a web font service) */
    font-family: "Acme Sans";
    src: url("data:font/woff2;base64,...") format("woff2");
}

:root {
    --ThemeFont: "Acme Sans", "Segoe UI", system-ui, sans-serif;   /* text */
    --ThemeFontHead: "Acme Sans", "Segoe UI", system-ui, sans-serif;   /* headings and titles */
}
```

The build embeds only compressed text files and `.png` files. A `.woff2` or `.ttf` file in an app's `srcWeb` is therefore missing in the deployed app. Embed the font as a data url, the way `brand/fonts.css` embeds Barlow and Saira. A file in a `FileHttpServerModule` folder on disk (`url(fonts/acme-sans.woff2)`) works too.

**4b. Your own marks.** Also in `app/app.css`, with files in `app/` (svg or png). Write the urls as `../app/<file>`: a url in a css variable is resolved against the style sheet that uses the variable, and those style sheets are all one folder below the root (`sw/`, `app/`, `auth/` ...).

```css
:root {
    --sw-brand-icon: url("../app/acme-mark.svg");       /* the sign-in page (square) */
    --sw-brand-logo: url("../app/acme-logo.svg");       /* Welcome and the brand bar at the top of the shell sidebar */
}
:root[data-sw-scheme="dark"] {                          /* every dark theme: a mark must fit a dark background (not for colours, see Selectors) */
    --sw-brand-icon: url("../app/acme-mark-on-dark.svg");
    --sw-brand-logo: url("../app/acme-logo-on-dark.svg");
}
```

**4c. The weave (background pattern).** The SysWeaver weave is a tone-on-tone pattern behind the stand-alone sign-in and Welcome pages. It is a class, not a picture in the page:

- `.sw-weave` on a background element (the sign-in and Welcome pages put it on `<body>`) draws the pattern on `--sw-bg`. It has a light and a dark version (`:root[data-sw-scheme="dark"] .sw-weave`), both local data urls in `ui/tokens.css`.
- `.sw-weave-panel` is a solid panel in the theme background (rounded with `--sw-radius-lg`). Put text, forms and logos on such a panel, never directly on the pattern.
- `--sw-weave-scale` (on the `.sw-weave` element, 0.75 to 1.5) scales the pattern.

Use it in your own page:

```html
<body class="sw-root sw-weave">
    <main class="sw-weave-panel">...</main>
</body>
```

Replace it or turn it off for your brand in `app/app.css`. Write both selectors, because the dark rule is more specific:

```css
/* Your own pattern (a file in app/, written as ../app/, see 4b) */
.sw-weave { background-image: url("../app/acme-pattern.svg"); }
:root[data-sw-scheme="dark"] .sw-weave { background-image: url("../app/acme-pattern-dark.svg"); }

/* Or no pattern at all: a plain background */
.sw-weave,
:root[data-sw-scheme="dark"] .sw-weave { background-image: none; }
```

**5. Name and logo.** Add the manifest entry and your logo files:

```json
{
  "Type": "SysWeaver.AppInfo, SysWeaver.Common",
  "Params": { "AppName": "AcmeMail", "AppDisplayName": "Acme Mail", "AppDescription": "Mail for the Acme team." }
}
```

Without more files the server generates the icon and logo from `AppName` (and `AppSeed`). To use your own, add `MyApp/srcWeb/logo.svg` (4:3), `icon.svg` (square), `favicon.ico`, `apple-touch-icon.png`, `icon-180.png`, `icon-192.png`, `icon-512.png` and `logo.png`. They replace the generated files of the same names. `favicon.ico` and `icon.svg` are shown at 16 to 32 px, so use a simplified small version there.

## Checklist

- Check the built-in themes and your own themes in the settings menu, on the shell and on a content page (a table with filter boxes is a good test): Light, Dark, Application and Pro, which every user sees, and Hacker, which the Theme menu shows only to Debug users (sign in as a Debug user, or open a page with `?usetheme=hacker`). Check desktop and phone (portrait and landscape).
- Check a page opened stand-alone (settings > "Open in new tab").
- Check the browser console: no 404s and no errors.
- Check contrast: text on `--ThemeBackground`, and the background colour used as text on `--ThemeAcc1` / `--ThemeAcc2` buttons, should reach 4.5:1.
- Keep everything local: no `http(s)://` urls in your css or js.
- Lint your brand folder: `SwWeb check <your srcWeb> --strict --design` (the Design v2 rules check your own classes in `app/app.css` like any page css: role tokens, no shadows on permanent surfaces, no gradients, radius tokens). It reports internet urls (also in `Sw.Net.IncludeJs` / `IncludeCss`), `@import` in `app/app.css` (`css-import`), mark urls in `app/app.css` not written as `../app/` (`brand-url`), font files that the build would not embed (`font-file`), and css that restyles bare `button` / `input` / `select` / `textarea` elements or sets `body { overflow: hidden }` (`element-css`, `body-overflow`). `app/app.js` needs no JSDoc header (the examples above pass as they are); every other `.js` file does (`jsdoc-header`).

## Limits (server side)

- The colors of the built-in **Application** theme are generated from the app name and `AppSeed` only (see [Application colors](#application-colors)); change `AppSeed` to change them. Template `Variables` cannot override `Color.*`.
- The server's Theme menu lists the built-in themes: Light, Dark, Application and Pro for every user, Hacker only for users with the Debug role (its `WebMenuJs` entry in `HttpServerBase` has the auth "Debug"). The menu is server generated, so this cannot be changed from `app/app.css` or `app/app.js`; `?usetheme=hacker` still applies Hacker for anyone. Your themes are added by the shell when they are registered with a `Title`.
- `app/manifest.json` (PWA) uses the application colors: `theme_color` is `${$Color.Color}`, `background_color` is `${$Color.Background}`. It lists the generated `icon.svg`, `icon-180.png`, `icon-192.png`, `icon-512.png` (no maskable icon) and `logo.svg` as a 1024x768 screenshot. To change it, override `app/manifest.json` in your web folder; it is a server template, see `help/Templates.txt`.
- The server also generates `logo_debug.svg` (Debug > App theme) from the app name and a seed, to preview other seeds.
- When `app/app.css` and `app/app.js` are empty, the server sends them without a css / js content type.
