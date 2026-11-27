# common

[⬆ srcWeb](../README.md)

> The shared client-side library of all SysWeaver web pages, served under `common/`: core scripts, themes and background patterns, the tab control, animation helpers and a few vendored libraries.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Every page in the framework links `common/theme.css`. New format pages load the bundles `sw/core.js` + `sw/ui.js` (see [`../ui`](../ui/README.md)); old format pages load the legacy bundle `common/common.js` here instead, which contains the same core and ui sources plus the old globals. `theme.css` is served as a template (`${Variable}` substitution, e.g. for the configured colours) and defines the themes selected through the `data-theme` attribute (e.g. `dark`, `hacker`, `application`), several of which use the background images in this folder. The remaining scripts are loaded on demand by the pages that need them.

## Contents

| Item | Description |
|---|---|
| [`common.js`](common.js) | **GENERATED legacy compatibility bundle** (`SwWeb bundle`, entry in `sw/bundles.json`; do not edit): `core/*.js` + `core/legacy_session.js` (the legacy messaging names: `SessionManager`, `InterOp`, `SysWeaverInit` ...) + `ui/*.js` (the Sw library) + `ui/compat_common.js`, which defines every other legacy global (`sendRequest`, `getRequest`, `includeJs`, `includeCss`, `SessionManager`, `InterOp`, `ValueFormat`, `ColorIcon`, `Button`, `Fail`, `Info`, `AddLoading`, `FullScreen`, `PendingImages` ...) on top of `Sw`, and links `sw/ui.css` + `ui/compat_common.css`. Old format pages, other projects and srcData templates load it; new pages load `sw/core.js` (+ `sw/ui.js`). A page that renders its own UI and only needs the messaging of common.js (`SessionManager.Create(); SysWeaverInit();`) loads the small `sw/session.js` (+ `sw/theme.js` for the theme; core/README.md "Messaging only"). The bundle is built load once (each source runs once per page, also next to `sw/session.js` or `sw/core.js`) |
| [`theme.css`](theme.css) | Theme variables and theme variants (template) |
| [`common.css`](common.css) | LEGACY, unreferenced non-templated subset of `theme.css` (no page, script, server registration or srcData template references it); kept for compatibility, do not use |
| [`tab.js`](tab.js), [`tab.css`](tab.css) | LEGACY tab bar control `Tab` (no first-party page uses it any more: `explore/api.js` uses `Sw.Tabs` with the same `SysWeaver.Tabs.<key>` storage key; new code uses `Sw.Tabs`). Standalone (no common.js / Sw dependency), keyboard operable (tablist / tab roles, Left / Right / Home / End, Enter / Space), touch sized headers, selection remembered in `localStorage["SysWeaver.Tabs.<key>"]` |
| [`anim.js`](anim.js) | Animation and interpolation maths (`Smoothness`, `MathTools`, cubic Hermite interpolation, `SmoothValue`, `SmoothBool`, `SpringAnimation`, `ProjectileMotion`, `BounceMotion`, `BounceAnimation`); global names kept, also available as `Sw.Anim.<Name>`; no DOM, no dependencies |
| [`marked.js`](marked.js) | Vendored *marked* v15.0.3 Markdown parser (MIT, minified) |
| [`UZIP.js`](UZIP.js) | Vendored UZIP JavaScript zip / deflate library, used with `md5.js` by upload pages |
| [`md5.js`](md5.js) | Vendored YaMD5 MD5 hasher |
| [`racing-stripes.svg`](racing-stripes.svg), [`moroccan.svg`](moroccan.svg), [`glamorous.svg`](glamorous.svg), [`graph-paper.svg`](graph-paper.svg), [`Hacker.png`](Hacker.png) | Background patterns used by the themes in `theme.css` (`Hacker.png` is a 32×32 tile for the hacker theme) |
| [`hexagons.svg`](hexagons.svg), [`floating-cogs.svg`](floating-cogs.svg), [`plus.svg`](plus.svg) | Further patterns / images; no reference to them by name was found in the repositories |

## The compatibility bundle

`common/common.js` provides every legacy global name, signature and result; the visual parts are drawn by the Sw ui library:

- **Toasts** (`Fail`, `Info`, `Message`): `Sw.Toast` elements (`div.sw-toast` inside `div.sw-toasts`). Several messages can be stacked (up to 4), identical ones are merged with a counter, and hovering pauses the timer. Duration 5 s (`true` = 24 h), click-to-copy (unless `noCopyOnClick`) and console logging.
- **Loading overlay** (`AddLoading`): `Sw.Loader` (`div.sw-loader`). The remover posts PageLoaded / LoaderRemoved / WindowLoaded and restores the scroll position unless called with `true`; no overlay with the `noloader` url flag.
- **Server block overlay**: `Sw.ServerBlock` (a small dialog `.sw-dialog.sw-serverblock` above every pop-up) with the server state texts and the elapsed time; no reload button (the page reloads by itself when a new server instance answers, never after a drop of the same instance); movable, resizable, × / Escape hide it until the server is back (`_docs/WebMessaging.md` §4).
- **Cells** (`ValueFormat.update`): Type and Url cells render a real `<a class="sw-link" href>`; Toggle and Actions render `<button>` elements; Json / Text / MD open a `Sw.Dialog` (`pre.sw-code-json` for Json); the DateTime classes are `sw-cell-dt` / `sw-cell-dt-<fmt>`. The legacy cell classes `Right`, `Monospaced`, `Null`, `False`, `Text`, `Toggle`, `Capped`, `ValueFormat<Kind>`, `MouseClick`, `KeyClick` are set too.
- **Requests and urls**: `sendRequest` retries requested by `errHandler` work, concurrent `includeJs` callers all get `loaded: true`, and `EditUrl` / `ViewUrl` / `EditOrOpenUrl` resolve the api against the site root.
- **Pro theme**: no internet resources; pro uses the brand fonts (Barlow, Saira Semi Condensed), compact sizes (ui/tokens.css) and a mid dark slate (_docs/WebBranding.md "The built-in themes").

## Relationships

- **Part of:** [SysWeaver.HttpServer](../../README.md)
- **Used by:** [`app/`](../app/README.md), the explorer pages of [SysWeaver.HttpServer.ExploreModule](../../../SysWeaver.HttpServer.ExploreModule/srcWeb/explore/README.md) and the web pages of most other projects, e.g. [SysWeaver.Chat](../../../SysWeaver.Chat/README.md) (`UZIP.js`, `md5.js`), [SysWeaver.MicroService.Media](../../../SysWeaver.MicroService.Media/README.md) (`anim.js`), [SysWeaver.MicroService.FileUploader](../../../SysWeaver.MicroService.FileUploader/README.md) and [SysWeaver.MicroService.ServerManager](../../../SysWeaver.MicroService.ServerManager/README.md)

## Files

### [`theme.css`](theme.css)

Served as a template, registered with `AddTemplateMatch("common/theme.css")` in `HttpServerBase.cs`. It loads no external resources (every theme uses the local brand fonts from `../brand/fonts.css`, hacker a local monospace font) and defines:

- **`:root` defaults** (the *light* theme) for all theme variables, plus placeholders for the derived `*R/*G/*B/*RGB/*25/*50/*75` variables that the theme code overwrites.
- **Theme blocks** selected by `[data-theme=...]`. Each non-light theme sets `color-scheme: dark` on `:root` and `textarea`, and has its own body background (pattern SVG + radial gradients).

  | Theme | Notes |
  |---|---|
  | `dark` | Dark colours. |
  | `hacker` | Also `--ThemePos`, a monospace font, and a fixed `body::before` overlay using `Hacker.png`. |
  | `application` | Colours from `${Color.Background}`, `${Color.Color}`, `${Color.Acc1}`, `${Color.Acc2}` (the per application colours from the app name / `AppSeed`, see _docs/WebBranding.md "Application colors"). |
  | `pro` | Compact (smaller text, controls and icons in ui/tokens.css), mid dark brand slate, small radii, no pattern. |

- **Base element styles**: `html` and `body` (theme background, colour and font, `graph-paper.svg` pattern), the page modes `html.FillScreen`, `html.Embedded` and `html.Transparent`, links, and the colour utility classes `.ThemeBackgroundBg/Fg`, `.ThemeMainBg/Fg`, `.ThemeAccBg/Fg`, `.ThemeAcc2Bg/Fg`.
- **Styles of the legacy `SysWeaver-*` elements** (links, pages, buttons, loading and progress bars, text, code / long text / markdown pop-up content).
- **Icon mask classes**: `.IconWorking` (animated spinner), `.IconPaused`, `.IconPower`, `.IconWarning`, `.IconReload` (inline SVG data URIs), `.IconLogo` (`../icon.svg`) and `.IconCurrentUser` / `.IconCurrentUserFill` (`../auth/UserImages/Current/small`).

### [`tab.js`](tab.js) / [`tab.css`](tab.css)

`class Tab` (global), standalone (no dependency on `common.js` or Sw):

| Member | Behaviour |
|---|---|
| `constructor(saveKey)` | Creates `<SysWeaver-TabBar>` (`this.Element`). With `saveKey`, the selected index is restored from and saved to `localStorage["SysWeaver.Tabs." + saveKey]`. |
| `AddTab(name, onActivate, title, onDeactivate, element, titleSelected)` | Appends a `<SysWeaver-Tab>` with text `name` and tooltip `title`/`titleSelected`. A non-modified click selects it. As tabs are added, the first tab (or the restored index) is selected. Returns the tab element. |
| `Select(index)` | Moves class `tabactive` between the tab elements and their associated content `element`. `onActivate` may be a function `(tabEl, index)` or an element that receives `tabactive`. `onDeactivate(tabEl, index)` is called on the previous tab. The index is saved when selected explicitly; out of range indices are ignored. |
| `RemoveTab(index)` | Adjusts the selection and removes the tab; a removed selected tab is deactivated. |

The bar is a `tablist` with a roving tab stop (`tab` headers with `aria-selected`, content elements get `role=tabpanel`); Left / Right / Home / End move and select, Enter / Space select. `tab.css`: the bar scrolls horizontally, headers are at least `--sw-touch` high and show a focus ring.

### [`anim.js`](anim.js)

Standalone (no dependency on `common.js`); all declarations are global and also available as `Sw.Anim.<Name>`:

| Name | Behaviour |
|---|---|
| `Smoothness` (frozen enum) | `Normal`, `Smooth`, `Smoother`, `Step`. |
| `MathTools` | `Smooth(t)` (smoothstep), `Smoother(t)` (smootherstep), `Step(t)`, `EaseSmooth(t, d)`, `EaseSmooth2(t, in, out)`, `EaseSmoother(t, d)`, `EaseSmoother2(t, in, out)`, `GetSmoothDoubleFunction(smoothness)`. |
| `CubicHermite` | `CalcBasis(t)`, `CalcDerivativeBasis(t)`. |
| `CubicHermiteInterpolator(from, to, init, exit)` | `ValueAt(t)`, `DerivativeAt(t)` (alias `DerivativAt`), `Target`; copy-constructible. |
| `SmoothValue(timeMs, value, duration=1000, init, from)` | A value that glides to new targets along a C1-continuous Hermite curve: `Update(timeMs, value, duration, exitVelocity)`, `ValueAt(timeMs)`, `DerivativeAt(timeMs)`, `SetTo(timeMs, value, init)`, `Target`. Time defaults to `performance.now()`. |
| `SmoothBool(timeMs, startState, duration, smoothness)` | A boolean as a smoothed 0..1 value (`ValueAt`, `Update`, `SetTo`, `Target`), using the constants `FalseValue`/`TrueValue`. |
| `SpringAnimation(frequency, initialLength, decay, snap)` | Damped cosine `Displacement(timeMs)`/`ValueAt`; static `Zoom` preset. |
| `ProjectileMotion` | `FlightTime`, `MaxHeight`, `Displacement`, `Velocity`, `Solve(maxHeight, flightTime)`. |
| `BounceMotion` | Period-based bounce maths: `Period`, `RestAfterPeriod`, `RestTime`, `PeriodVelocity`, `PeriodDuration`, `PeriodEndTime`, `Displacement`, `Displacement2`, `Velocity`, `Velocity2`. |
| `BounceAnimation(v0, g, restitution, restVelocity, normalize, startAtPeak)` | `ValueAt(timeMs)`. |
