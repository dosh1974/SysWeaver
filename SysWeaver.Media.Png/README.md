# SysWeaver.Media.Png

[⬆ SysWeaver overview](../README.md)

> A dependency-free PNG decoder and chunk inspector.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Library |

## Purpose

Read PNG images and their metadata chunks (for example embedded text) without a native imaging library — useful where only simple decoding or metadata access is needed. Used by the OpenAI integration.

## Key features

- `PngTools.DecodeArgb`: decodes all color types and bit depths (1 to 16 bits, palette, gray, RGB, alpha) and tRNS transparency to 32-bit pixels in B, G, R, A byte order (cross checked against ImageMagick).
- Chunks: `ReadChunks`, `WriteChunks` / `MakePng` (optionally replacing the image data with new raw scanlines or a new IDAT chunk), `WriteChunk`, `DecodeIHDR`, `PngImageInfo.ToChunk`.
- Raw image data: `ApplyFilterToScanline` (all 5 filters), `GetFilters`, `UnfilterData`, `DecodeAndUnfilterData`, `DecodeAndUnfilterIDAT`.
- Text chunks: `CreateInformationChunk` (tEXt, zTXt or iTXt, whatever fits the text best), `SetCreationTimeInfo`, the well known `PngKeywords`.
- Checksums: `CalcCrc32`, `CalcAdler32`, and `GetCompressed` (a zlib stream).

## Limitations and considerations

- Decodes to 32-bit pixels only (16-bit channels are truncated to the high byte); use ImageMagick ([SysWeaver.Media](../SysWeaver.Media/README.md)) for general image handling.
- Interlaced images are not supported (`NotSupportedException`). Chunk CRC's are not validated.
- The streams must be seekable (`Position` / `Length` are used). `ReadChunks(Stream)` disposes the stream when the enumeration ends.
- Corrupt data throws `IOException` (including `EndOfStreamException`) or `InvalidDataException` (deflate errors); chunk lengths and image sizes are validated before allocating.
  An indexed image without a PLTE chunk or with a palette index outside the palette throws `IOException`.
- Not included in the main solution file although a solution project references it.

## Relationships

- **Project:** [`SysWeaver.Media.Png.csproj`](SysWeaver.Media.Png.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
- **Used by:** [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md)
