# brand

> The brand fonts of the web UI and their licences. They are local copies; nothing is loaded from the internet. How apps brand themselves: [`_docs/WebBranding.md`](../../../_docs/WebBranding.md).

## Contents

| File | Use |
|---|---|
| `fonts.css` | GENERATED: `@font-face` rules for Barlow (400 Regular, 500..900 Medium) and Saira Semi Condensed (SemiBold), the font files as base64 data urls. Imported by `common/theme.css` |
| `fonts/*.ttf` | The font sources of `fonts.css` (desktop TTFs; no woff2 was available offline) |
| `fonts/OFL-Barlow.txt`, `fonts/OFL-SairaSemiCondensed.txt` | SIL Open Font License 1.1 of the two families (keep them with the fonts) |

There are no logo or icon files here, on purpose: SysWeaver marks are not shipped, so an app is never branded as SysWeaver. Every app's `icon.svg` (square), `logo.svg` (4:3, 512x384), `banner.svg` (9:2, the icon in its left square), `favicon.ico`, `apple-touch-icon.png`, `icon-180.png`, `icon-192.png`, `icon-512.png`, `logo.png` and `banner.png` are generated per app by the server (`HttpServerBase_Favicon.cs`) from the app name / `AppSeed` in the application colours (`HashColors.AppColors`), unless the app's own srcWeb serves a file of that name. The pages use them through `--sw-brand-icon`, `--sw-brand-small`, `--sw-brand-logo`, `--sw-brand-stacked` and `--sw-brand-banner` (ui/tokens.css).

## Rules

- The weave pattern (`.sw-weave` in ui/tokens.css) is for backgrounds only. Text and logos sit on a solid panel (`.sw-weave-panel`).
- Data colors follow the palette too: chart series use `--sw-chart-1` .. `--sw-chart-7` (ui/tokens.css: copper and steel from the theme accents plus validated extra hues).

## Build notes

- The web build embeds compressed text files (`.br`) and `.png` files only, so `.ttf` / `.woff2` files would not be in a deployed app. That is why the fonts are data urls inside `fonts.css`: about 400 kB of css, 150 kB brotli compressed, cached by the browser.
- Regenerate `fonts.css` after a font change: base64-encode each `fonts/*.ttf` into the matching `src: url("data:font/ttf;base64,...")` of `fonts.css`. Keep the header comment and the weights.
