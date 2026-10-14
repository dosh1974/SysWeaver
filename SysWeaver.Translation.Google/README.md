# SysWeaver.Translation.Google

[⬆ SysWeaver overview](../README.md)

> A translation back-end that uses Google's public translation web page.

| | |
|---|---|
| **Layer** | Translation |
| **Kind** | Micro service (internal translator, `IInternalTranslator`) |

## Purpose

Free machine translation for the SysWeaver translation pipeline without a paid API contract. As its own documentation puts it, it (ab)uses a Google web page.

## How it fits into SysWeaver

It is one of the interchangeable *internal* translators (`IInternalTranslator`, defined in [SysWeaver.Common](../SysWeaver.Common/README.md)); a caching translator such as [SysWeaver.MicroService.TranslationDbCache](../SysWeaver.MicroService.TranslationDbCache/README.md) sits in front of it so each text is translated once.

## Key features

- Translates by requesting the mobile translation page and extracting the result
- In-memory cache per retention level (short, medium, long durations configurable)
- When rate limited (HTTP 429) it can switch to the Tor network for a while (`UseTor`, `UseTorFor`), using the Tor client of [SysWeaver.Tor](../SysWeaver.Tor/README.md) when available; retries failed requests (`Retry`)
- Statistics on Tor usage and performance monitoring

## Key components

- [`GoogleTranslator`](GoogleTranslator.cs) — the translator service
- [`GoogleTranslatorParams`](GoogleTranslatorParams.cs) — parameters

## Using it

```json
{ "Type": "SysWeaver.Translation.GoogleTranslator, SysWeaver.Translation.Google", "Params": { "StartTor": false } }
```

## Limitations and considerations

- Not an official API: it can break whenever the page changes, is rate limited, and may conflict with the provider's terms of service — suitable for development or low-volume use
- The translation context and content type (Markdown, HTML) are not used, texts are translated as plain text
- Always put a cache ([SysWeaver.MicroService.TranslationDbCache](../SysWeaver.MicroService.TranslationDbCache/README.md)) in front of it

## Relationships

- **Project:** [`SysWeaver.Translation.Google.csproj`](SysWeaver.Translation.Google.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.IsoData](../SysWeaver.IsoData/README.md)
