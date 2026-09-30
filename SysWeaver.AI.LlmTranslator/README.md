# SysWeaver.AI.LlmTranslator

[⬆ SysWeaver overview](../README.md)

> A translation back-end that uses a large language model through the OpenAI service.

| | |
|---|---|
| **Layer** | AI / Translation |
| **Kind** | Internal translator (`IInternalTranslator`) |

## Purpose

Provide high-quality, context-aware translation for the SysWeaver translation pipeline using an LLM instead of a traditional machine translation service.

## How it fits into SysWeaver

It is one of the interchangeable *internal* translators; a caching translator such as [SysWeaver.MicroService.TranslationDbCache](../SysWeaver.MicroService.TranslationDbCache/README.md) sits in front of it so each text is translated once.

## Limitations and considerations

- Costs and latency of LLM calls; always combine with a cache.
- The list of supported languages is maintained by the translator and can be refreshed by operators.

## Relationships

- **Project:** [`SysWeaver.AI.LlmTranslator.csproj`](SysWeaver.AI.LlmTranslator.csproj)
- **Builds on:** [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md), [SysWeaver.Storage](../SysWeaver.Storage/README.md)
