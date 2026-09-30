# SysWeaver.Media.Png

[⬆ SysWeaver overview](../README.md)

> A dependency-free PNG decoder and chunk inspector.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Library |

## Purpose

Read PNG images and their metadata chunks (for example embedded text) without a native imaging library — useful where only simple decoding or metadata access is needed. Used by the OpenAI integration.

## Limitations and considerations

- Decodes to 32-bit ARGB only, and its own documentation lists PNG features it does not support; use ImageMagick ([SysWeaver.Media](../SysWeaver.Media/README.md)) for general image handling.
- Not included in the main solution file although a solution project references it.

## Relationships

- **Project:** [`SysWeaver.Media.Png.csproj`](SysWeaver.Media.Png.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
- **Used by:** [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md)
