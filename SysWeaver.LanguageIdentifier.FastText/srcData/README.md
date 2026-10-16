# srcData

[⬆ SysWeaver.LanguageIdentifier.FastText](../README.md)

> The fastText language identification model, embedded into the assembly at build time.

| | |
|---|---|
| **Project** | [SysWeaver.LanguageIdentifier.FastText](../README.md) |
| **Kind** | Embedded data |

## Purpose

The `EmbedLocal` target in [`SysWeaver.LanguageIdentifier.FastText.csproj`](../SysWeaver.LanguageIdentifier.FastText.csproj) runs before every build and passes the files in this folder to Compress (`_tools/compress`, Brotli) with `data/` as output; it embeds `data/**/*.br` and `data/**/*.ftz`. According to the project file the quantized model does not compress, so Compress copies it unchanged. The generated `data/` folder is not in the repository.

At runtime `FastTextLanguageIdentifier` reads the resource `SysWeaver.LanguageIdentifier.data.lid.176.ftz` unless another model file is configured (`ModelFile`).

## Contents

| Item | Description |
|---|---|
| [`lid.176.ftz`](lid.176.ftz) | The compressed (quantized) fastText language identification model for 176 languages (about 0.9 MB) |

## Relationships

- **Part of:** [SysWeaver.LanguageIdentifier.FastText](../README.md)
- **Uses:** [Compress](../../_tools/compress/README.md) at build time
- **Used by:** [`FastTextLanguageIdentifier.cs`](../FastTextLanguageIdentifier.cs)
