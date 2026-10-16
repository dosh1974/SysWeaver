# srcData

[⬆ SysWeaver.Media.Svg](../README.md)

> TrueType fonts embedded in the assembly, used by `SvgFont` to convert text to glyph paths without depending on installed fonts.

| | |
|---|---|
| **Project** | [SysWeaver.Media.Svg](../README.md) |
| **Kind** | Embedded data (fonts) |

## Purpose

[`SvgFont`](../SvgFont.cs) exposes each font in this folder as a static property (`SvgFont.MontserratBlack`, `SvgFont.AtariClassic`, ...) that is loaded on first use. `MontserratBlack` is also the fallback font when text is rasterised and no matching font file is found (see the Linux note in the project README).

## How it is built

The `EmbedLocal` target of [`SysWeaver.Media.Svg.csproj`](../SysWeaver.Media.Svg.csproj) runs `Compress.exe -Methods br .\srcData\*+ .\data` (see [`_tools`](../../_tools/README.md)), which writes Brotli compressed copies to the generated `data/` folder. All `data/**/*.br` and `data/**/*.ttf` files are embedded as resources (`SysWeaver.Media.data.<Name>.ttf[.br]`). `SvgFont` first looks for an uncompressed `.ttf` resource and otherwise decompresses the `.ttf.br` resource.

## Contents

| Item | Description |
|---|---|
| [`AsianNinja.ttf`](AsianNinja.ttf) | Decorative font, `SvgFont.AsianNinja` |
| [`AstroSpace.ttf`](AstroSpace.ttf) | Decorative font, `SvgFont.AstroSpace` |
| [`AtariClassic.ttf`](AtariClassic.ttf) | Pixel / retro font, `SvgFont.AtariClassic` |
| [`MontserratBlack.ttf`](MontserratBlack.ttf) | Heavy sans-serif font, `SvgFont.MontserratBlack` (default fallback) |
| [`RedemtionRegular.ttf`](RedemtionRegular.ttf) | Decorative font, `SvgFont.RedemtionRegular` |
| [`RoTwimchRegular.ttf`](RoTwimchRegular.ttf) | Decorative font, `SvgFont.RoTwimchRegular` |
| [`VegapunkFree.ttf`](VegapunkFree.ttf) | Decorative font, `SvgFont.VegapunkFree` |

## Limitations and considerations

- The licences of the fonts are not recorded in this folder; check them before redistributing.
- Adding a font here also requires a matching static property in `SvgFont.cs` to be reachable by name.

## Relationships

- **Part of:** [SysWeaver.Media.Svg](../README.md)
- **Used by:** [`SvgFont`](../SvgFont.cs), and through it `SvgScene`, `SvgCanvas.RasterizeText` and [`SvgBitmapRenderer`](../SvgBitmapRenderer.cs) (Montserrat Black as the fallback typeface when rendering)
