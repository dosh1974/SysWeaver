# SysWeaver.Translation.Google

[⬆ SysWeaver overview](../README.md)

> A translation back-end that uses Google's public translation web page.

| | |
|---|---|
| **Layer** | Translation |
| **Kind** | Internal translator (`IInternalTranslator`) |

## Purpose

Free machine translation for the SysWeaver translation pipeline without a paid API contract. Its own documentation describes it as (ab)using a Google web page.

## Limitations and considerations

- Not an official API: it can break whenever the page changes, may be throttled, and may conflict with the provider's terms of service. Suitable for development or low-volume use.
- Always put a cache ([SysWeaver.MicroService.TranslationDbCache](../SysWeaver.MicroService.TranslationDbCache/README.md)) in front of it.

## Relationships

- **Project:** [`SysWeaver.Translation.Google.csproj`](SysWeaver.Translation.Google.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.IsoData](../SysWeaver.IsoData/README.md)
