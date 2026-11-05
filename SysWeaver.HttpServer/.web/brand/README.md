# brand

> The SysWeaver brand assets of the web UI: logo and icon files, the brand fonts and their licences. They are local copies from the SysWeaver brand kit; nothing is loaded from the internet. How apps use or replace them: [`_docs/WebBranding.md`](../../../_docs/WebBranding.md).

## Contents

| File | Use |
|---|---|
| `sysweaver-icon-color-on-bright.svg`, `sysweaver-icon-color-on-dark.svg` | The SW mark (48 px and up) for bright and dark backgrounds: `--sw-brand-icon` (ui/tokens.css), Home, Welcome and sign-in pages, gallery |
| `sysweaver-logo-color-on-bright.svg`, `sysweaver-logo-color-on-dark.svg` | Mark and wordmark (at least 140 px wide): `--sw-brand-logo`, the shell menu logo (160 px, unaltered), gallery |
| `sysweaver-logo-stacked-color-on-bright.svg`, `sysweaver-logo-stacked-color-on-dark.svg` | Stacked logo (at least 100 px wide): `--sw-brand-stacked` |
| `sysweaver-icon-mono-*.svg`, `sysweaver-logo-mono-*.svg` | One colour versions (not used by default) |
| `sysweaver-small-32.svg` | The pixel grid master for sizes below 48 px (also the site root `icon.svg`, the browser tab icon) |
| `sysweaver-app-icon.svg` | The app icon on a slate tile (also the site root `logo.svg`) |
| `fonts.css` | GENERATED: `@font-face` rules for Barlow (400 Regular, 500..900 Medium) and Saira Semi Condensed (SemiBold), the font files as base64 data urls. Imported by `common/theme.css` |
| `fonts/*.ttf` | The font sources of `fonts.css` (the desktop TTFs of the brand kit; no woff2 was available offline) |
| `fonts/OFL-Barlow.txt`, `fonts/OFL-SairaSemiCondensed.txt` | SIL Open Font License 1.1 of the two families (keep them with the fonts) |

The site root of this srcWeb holds the browser and install icons, which override the server's generated ones (optional end points in `HttpServerBase`). They are made from the brand kit: `favicon.ico` (16, 32 and 48 px, from the small master PNGs), `icon.svg` (the small master), `apple-touch-icon.png` and `icon-180.png` (180 px, full bleed square), `icon-192.png` and `icon-512.png` (the rounded tile), `icon-maskable-512.png` (the mark inside the maskable safe zone on slate), `logo.svg` (the app icon), and `logo.png` (1024 px, the media demos use it as a texture).

## Rules (brand kit)

- Never retype the wordmark: use the logo files. Never stretch, rotate, recolour or add effects.
- Clear space: a third of the mark height on every side. Minimum: the mark 48 px (use the small master below that), the logo 140 px wide, the stacked logo 100 px wide.
- The weave pattern (`.sw-weave` in ui/tokens.css) is for backgrounds only. Text and logos sit on a solid panel (`.sw-weave-panel`).
- Data colors follow the palette too: chart series use `--sw-chart-1` .. `--sw-chart-7` (ui/tokens.css: copper and steel from the theme accents plus validated extra hues), and users without an own picture get a monogram on Copper 700 / Steel 600 / Slate 700 / Copper 500 / Steel 300 (`Sw.UserPicture`, ui/base.js).

## Build notes

- The web build embeds compressed text files (`.br`) and `.png` files only, so `.ttf` / `.woff2` files would not be in a deployed app. That is why the fonts are data urls inside `fonts.css`: about 400 kB of css, 150 kB brotli compressed, cached by the browser.
- The svg files are the brand kit files with their embedded provenance metadata (`<metadata>` C2PA manifest) removed, to keep the downloads small. The drawing is unchanged.
- Regenerate `fonts.css` after a font change: base64-encode each `fonts/*.ttf` into the matching `src: url("data:font/ttf;base64,...")` of `fonts.css`. Keep the header comment and the weights.
