# srcData

[⬆ SysWeaver.Media.Svg](../README.md)

> TrueType fonts embedded in the assembly, used by `SvgFont` to convert text to glyph paths without depending on installed fonts.

| | |
|---|---|
| **Project** | [SysWeaver.Media.Svg](../README.md) |
| **Kind** | Embedded data (fonts) |

## Purpose

[`BrandFonts`](../BrandFonts.cs) exposes the fonts in this folder as static properties (`BrandFonts.Saira`, `BrandFonts.BarlowMedium`, `BrandFonts.BarlowRegular`) that are loaded on first use. `BarlowRegular` is also the fallback font when text is rasterised and no matching font file is found (see the Linux note in the project README).

## How it is built

The `EmbedLocal` target of [`SysWeaver.Media.Svg.csproj`](../SysWeaver.Media.Svg.csproj) runs `Compress.exe -Methods br .\srcData\*+ .\data` (see [`_tools`](../../_tools/README.md)), which writes Brotli compressed copies to the generated `data/` folder. All `data/**/*.br` and `data/**/*.ttf` files are embedded as resources (`SysWeaver.Media.data.<Name>.ttf[.br]`). `SvgFont` first looks for an uncompressed `.ttf` resource and otherwise decompresses the `.ttf.br` resource. The `data/` folder (including its copy of this README) is generated and not in git.

The project file also embeds the three `.ttf` files of this folder a second time, uncompressed, as `SvgIconExample.Fonts.<file>`; no code in the repositories reads those resources.

## Contents

| Item | Description |
|---|---|
| [`SairaSemiCondensed-SemiBold.ttf`](SairaSemiCondensed-SemiBold.ttf) | The display font (initials, wordmarks), `BrandFonts.Saira` |
| [`Barlow-Medium.ttf`](Barlow-Medium.ttf) | The text font, emphasis, `BrandFonts.BarlowMedium` |
| [`Barlow-Regular.ttf`](Barlow-Regular.ttf) | The text font (default fallback), `BrandFonts.BarlowRegular` |
| [`OFL-Barlow.txt`](OFL-Barlow.txt), [`OFL-SairaSemiCondensed.txt`](OFL-SairaSemiCondensed.txt) | The SIL Open Font License 1.1 of the fonts |

## Limitations and considerations

- The fonts are licensed under the SIL Open Font License 1.1 (the licence texts are in this folder); keep them with the fonts when redistributing.
- Adding a font here also requires a matching static property in `BrandFonts.cs` to be reachable by name.

## Relationships

- **Part of:** [SysWeaver.Media.Svg](../README.md)
- **Used by:** [`SvgFont`](../SvgFont.cs), and through it `SvgScene`, `SvgCanvas.RasterizeText` and [`SvgBitmapRenderer`](../SvgBitmapRenderer.cs) (Barlow Regular as the fallback typeface when rendering), [`BrandFonts`](../BrandFonts.cs)
