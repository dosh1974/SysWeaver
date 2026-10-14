# SysWeaver.Media.Png

[⬆ SysWeaver overview](../README.md)

> A managed PNG decoder and chunk reader/writer with no native dependencies.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Library |

## Purpose

Read PNG images and read or rewrite their chunks (for example embedded text metadata) without a native imaging library — useful where only simple decoding or metadata access is needed. The AI projects use it to stamp generated images with creation time, software and source information.

## Key features

- Decoding of all colour types and bit depths (1 to 16 bits, palette, gray, RGB, alpha, tRNS transparency) to 32-bit pixels (`PngTools.DecodeArgb`)
- Reading and writing chunks, optionally replacing the image data (`ReadChunks`, `WriteChunks`, `MakePng`)
- Scanline filtering and unfiltering helpers for raw image data
- Text chunks (tEXt, zTXt or iTXt, whichever fits the text best) with the well-known `PngKeywords`, and a creation time chunk
- CRC-32, Adler-32 and zlib stream helpers

## Key components

- [`PngTools`](PngTools.cs) — decoding, chunk reading/writing and helpers
- [`PngChunk`](PngChunk.cs) — a single chunk (id and data)
- [`PngImageInfo`](PngImageInfo.cs) — the decoded IHDR header

## Limitations and considerations

- Decodes to 32-bit pixels only (16-bit channels are reduced to 8 bits); use ImageMagick ([SysWeaver.Media](../SysWeaver.Media/README.md)) for general image handling
- Interlaced images are not supported (`NotSupportedException`); chunk CRCs are not validated
- Streams must be seekable

## Relationships

- **Project:** [`SysWeaver.Media.Png.csproj`](SysWeaver.Media.Png.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
- **Used by:** [SysWeaver.AI](../SysWeaver.AI/README.md), [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md)
- **Third-party:** System.IO.Hashing (CRC-32)
