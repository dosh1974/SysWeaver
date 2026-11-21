# SysWeaver.Media.Svg

[⬆ SysWeaver overview](../README.md)

> Programmatic SVG creation and rendering: a canvas/scene API with paths, shapes, gradients, shadows, 3D-style extrusion, text with embedded fonts, barcodes, composition templates, and SVG-to-bitmap rendering.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Library |

## Purpose

Generate vector graphics on the server — maps, avatars, favicons, barcodes, badges, templated images — and rasterise them when a bitmap is required.

## How it fits into SysWeaver

```mermaid
flowchart LR
  Canvas["SvgCanvas / SvgScene"] --> Svg["SVG document"]
  Templates["composition templates"] --> Svg
  Fonts["embedded fonts"] --> Canvas
  Svg --> Render["SvgBitmapRenderer<br/>Skia"]
  Svg --> Map["Map"]
  Svg --> Avatar["Avatar"]
  Svg --> Http["HTTP server favicons"]
```

## Key features

- Path parsing, bounds, transforms and shape generation (n-gons, rounded and fancy rects, extrusion, bending)
- Scene builder with css styles, gradients, masks, drop shadows and 3D-style extruded faces, plus generated favicons
- XDocument based canvas for editing and combining existing SVG documents (embedding, fitting, inlining styles, recolouring)
- Text converted to glyph paths using the embedded brand fonts (`BrandFonts`: Saira Semi Condensed SemiBold, Barlow Regular and Medium) or font files found on disc
- Generated brand marks: app icons, user icons, app logos and banners built from a name's initials with woven thread motifs and a `BrandPalette` derived from `HashColors` (`SvgScene.AddAppIcon`, `AddUserIcon`, `AddAppLogo`, `AddAppBanner` in `SvgSceneBrandExt`); the same name, colors and seed always give the same result
- Code 128 (numeric, also GS1-128), Code 39 and ITF-14 barcodes as SVG paths
- Layered composition templates built from SVG/bitmap files selected by variables, with caching
- SVG to PNG/JPEG rendering with Svg.Skia, also on Linux
- All generated numbers use the invariant culture

## Key components

- [`SvgPath`](SvgPath.cs) — path parsing, geometry and transforms
- [`SvgScene`](SvgScene.cs) — builds a styled SVG document from shapes and text
- [`SvgCanvas`](SvgCanvas.cs) — XDocument based SVG editing
- [`SvgFont`](SvgFont.cs) — embedded and file based fonts for text to path conversion
- [`BrandFonts`](BrandFonts.cs) — the embedded brand fonts (`Saira`, `BarlowMedium`, `BarlowRegular`)
- [`SvgSceneBrandExt`](SvgSceneBrandExt.cs), [`MarkDesign`](MarkDesign.cs), [`BrandPalette`](BrandPalette.cs), [`BrandText`](BrandText.cs) — the generated brand icons, logos and banners
- [`SvgBitmapRenderer`](SvgBitmapRenderer.cs) — SVG to PNG/JPEG via Skia
- [`SvgBarcodeCode128`](SvgBarcodeCode128.cs), [`SvgBarcodeCode39`](SvgBarcodeCode39.cs), [`SvgBarcodeItf14`](SvgBarcodeItf14.cs) — barcodes as SVG paths
- [`SvgCompositionTemplate`](SvgCompositionTemplate.cs), [`SvgCompositionTemplateCache`](SvgCompositionTemplateCache.cs) — layered composition templates and their cache

## Using it

```csharp
// Rasterise an SVG document (0 = use the size of the SVG)
Byte[] png = SvgBitmapRenderer.CreatePng(svgText, 256, 256);
```

## Limitations and considerations

- Glyph-based text has no kerning or line breaking
- Bounds include curve control points (not tight bounds)
- `SvgCanvas.Image(url)` reads local files, don't pass untrusted urls
- `SvgFont.AddFontPath` and the font cache are global (static) state
- Rendering fidelity follows the Skia-based SVG renderer's feature coverage
- On Linux the referenced `SkiaSharp.NativeAssets.Linux.NoDependencies` needs no fontconfig or fonts, but installed fonts are not used: text elements fall back to font files from `SvgFont.AddFontPath` folders or the embedded `BrandFonts.BarlowRegular`. Convert text to paths (`SvgCanvas.RasterizeText`, `SvgScene.AddText`) for identical output on every machine

## Folder layout

| Folder | Content |
|---|---|
| [`srcData/`](srcData/README.md) | TrueType fonts embedded for text-to-path conversion (`BrandFonts`, `SvgFont`) |

## Relationships

- **Project:** [`SysWeaver.Media.Svg.csproj`](SysWeaver.Media.Svg.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.Compression](../SysWeaver.Compression/README.md)
- **Used by:** [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.Map](../SysWeaver.Map/README.md), [SysWeaver.MicroService.Avatar](../SysWeaver.MicroService.Avatar/README.md)
- **Third-party:** Svg.Skia / SkiaSharp, WaterTrans.GlyphLoader (font glyphs)
