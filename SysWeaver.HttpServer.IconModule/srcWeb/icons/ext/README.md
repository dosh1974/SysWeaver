# ext

[⬆ icons](../README.md)

> Ready-made, coloured file type icons, served at `icons/ext/<extension>.svg`.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.IconModule](../../../README.md) |
| **Kind** | Icons |

## Purpose

These files are not optimised (the SvgOpt step for this folder is commented out in the `.csproj`); the build compresses `icons\ext\*.svg` directly with Brotli into `web/icons/ext`. When a request for `icons/ext/<ext>.svg` arrives, [`IconHttpServerModule`](../../../IconHttpServerModule.cs) first checks whether the static data module already has that URL; if one of these files exists it is served, otherwise the module generates an icon from the [`srcData/base.svg`](../../../srcData/README.md) template, labelled with the extension and coloured by MIME type.

## Contents

| Item | Description |
|---|---|
| Documents and office | e.g. [`pdf.svg`](pdf.svg), [`doc.svg`](doc.svg), [`docx.svg`](docx.svg), [`xlsx.svg`](xlsx.svg), [`pptx.svg`](pptx.svg), [`odt.svg`](odt.svg), [`rtf.svg`](rtf.svg), [`txt.svg`](txt.svg), [`epub.svg`](epub.svg) |
| Images and graphics | e.g. [`png.svg`](png.svg), [`jpg.svg`](jpg.svg), [`gif.svg`](gif.svg), [`svg.svg`](svg.svg), [`webp.svg`](webp.svg), [`psd.svg`](psd.svg), [`ai.svg`](ai.svg), [`dwg.svg`](dwg.svg), [`raw.svg`](raw.svg) |
| Audio and video | e.g. [`mp3.svg`](mp3.svg), [`wav.svg`](wav.svg), [`flac.svg`](flac.svg), [`ogg.svg`](ogg.svg), [`mp4.svg`](mp4.svg), [`mkv.svg`](mkv.svg), [`mov.svg`](mov.svg), [`webm.svg`](webm.svg) |
| Archives and disc images | e.g. [`zip.svg`](zip.svg), [`7z.svg`](7z.svg), [`rar.svg`](rar.svg), [`tar.svg`](tar.svg), [`gz.svg`](gz.svg), [`iso.svg`](iso.svg), [`dmg.svg`](dmg.svg), [`vmdk.svg`](vmdk.svg) |
| Source code and web | e.g. [`cs.svg`](cs.svg), [`cpp.svg`](cpp.svg), [`java.svg`](java.svg), [`js.svg`](js.svg), [`ts.svg`](ts.svg), [`py.svg`](py.svg), [`html.svg`](html.svg), [`css.svg`](css.svg), [`json.svg`](json.svg), [`xml.svg`](xml.svg), [`sql.svg`](sql.svg), [`csproj.svg`](csproj.svg), [`sln.svg`](sln.svg) |
| Executables, scripts and system | e.g. [`exe.svg`](exe.svg), [`dll.svg`](dll.svg), [`msi.svg`](msi.svg), [`apk.svg`](apk.svg), [`bat.svg`](bat.svg), [`sh.svg`](sh.svg), [`ini.svg`](ini.svg), [`config.svg`](config.svg) |
| Fonts and certificates | e.g. [`ttf.svg`](ttf.svg), [`otf.svg`](otf.svg), [`woff2.svg`](woff2.svg), [`cer.svg`](cer.svg), [`pem.svg`](pem.svg), [`pfx.svg`](pfx.svg) |
| Generic | [`blank.svg`](blank.svg), [`folder.svg`](folder.svg), [`image.svg`](image.svg) |

278 `*.svg` files in total, one per lower case extension.

## Relationships

- **Part of:** [SysWeaver.HttpServer.IconModule](../../../README.md)
- **Related:** [`srcData/`](../../../srcData/README.md) (template and overlays for generated extension and MIME type icons)
- **Used by:** links to `icons/ext/<ext>.svg`, e.g. the explorer pages of [SysWeaver.HttpServer.ExploreModule](../../../../SysWeaver.HttpServer.ExploreModule/README.md) (via `ExtPrefix`), the default AI tools of [SysWeaver.AI](../../../../SysWeaver.AI/README.md) and the table data externals of [SysWeaver.Common](../../../../SysWeaver.Common/README.md) (`Data/TableDataExternals.cs`)
