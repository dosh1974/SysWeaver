# SysWeaver.LanguageIdentifier.FastText

[⬆ SysWeaver overview](../README.md)

> Language identification with Facebook's fastText model: very fast, local, no external calls.

| | |
|---|---|
| **Layer** | Translation |
| **Kind** | Service (`ILanguageIdentifier`) |

## Purpose

Detect the language of user input (e.g. chat messages) before translating it. The project embeds a compressed model and wraps the native fastText library for Windows and Linux.

## Limitations and considerations

- Its own documentation calls it very fast but not very accurate — best for longer texts; short phrases are often misidentified.
- Uses native libraries, which must be available for the target platform.
- Use [SysWeaver.LanguageIdentifier.Llm](../SysWeaver.LanguageIdentifier.Llm/README.md) when accuracy matters more than speed and cost.

## Relationships

- **Project:** [`SysWeaver.LanguageIdentifier.FastText.csproj`](SysWeaver.LanguageIdentifier.FastText.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.Compression](../SysWeaver.Compression/README.md), [SysWeaver.IsoData](../SysWeaver.IsoData/README.md)
