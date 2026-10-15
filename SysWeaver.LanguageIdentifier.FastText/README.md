# SysWeaver.LanguageIdentifier.FastText

[⬆ SysWeaver overview](../README.md)

> Language identification with Facebook's fastText model: very fast, local, no external calls.

| | |
|---|---|
| **Layer** | Translation |
| **Kind** | Micro service (`ILanguageIdentifier`) |
| **Platform** | Windows and Linux (native fastText library) |

## Purpose

Detect the language of a text, e.g. user input before it is translated, without any external service or cost.

## How it fits into SysWeaver

It implements `ILanguageIdentifier` (defined in [SysWeaver.Common](../SysWeaver.Common/README.md)) and is interchangeable with [SysWeaver.LanguageIdentifier.Llm](../SysWeaver.LanguageIdentifier.Llm/README.md).

## Key features

- Embeds the compressed fastText language identification model (`lid.176.ftz`, 176 languages); a larger model file can be supplied (`ModelFile`)
- Returns the best language or a ranked list with confidences, optionally biased towards the user's language
- Results are cached in memory for a short time (`CacheSeconds`); statistics and performance monitoring
- Contains the source of a fastText .NET wrapper (`FastText.NetWrapper` folder) using the native `FastText.Native.*` packages

## Key components

- [`FastTextLanguageIdentifier`](FastTextLanguageIdentifier.cs) — the language identifier service
- [`FastTextWrapper`](FastText.NetWrapper/FastTextWrapper.cs) — the managed wrapper of the native fastText library

## Using it

```json
{ "Type": "SysWeaver.LanguageIdentifier.FastTextLanguageIdentifier, SysWeaver.LanguageIdentifier.FastText" }
```

## Limitations and considerations

- Very fast but not very accurate, especially for short texts; use [SysWeaver.LanguageIdentifier.Llm](../SysWeaver.LanguageIdentifier.Llm/README.md) when accuracy matters more than speed and cost
- Uses native libraries, which must be available for the target platform

## Folder layout

| Folder | Content |
|---|---|
| [`FastText.NetWrapper/`](FastText.NetWrapper/README.md) | Source of the managed fastText wrapper |
| [`srcData/`](srcData/README.md) | The `lid.176.ftz` model, embedded at build time |

## Relationships

- **Project:** [`SysWeaver.LanguageIdentifier.FastText.csproj`](SysWeaver.LanguageIdentifier.FastText.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.Compression](../SysWeaver.Compression/README.md), [SysWeaver.IsoData](../SysWeaver.IsoData/README.md)
- **Third-party:** FastText.Native.Windows, FastText.Native.Linux, AutoMapper
