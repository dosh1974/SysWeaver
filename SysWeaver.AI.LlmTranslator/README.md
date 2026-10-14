# SysWeaver.AI.LlmTranslator

[⬆ SysWeaver overview](../README.md)

> A translation back-end that uses a large language model through the OpenAI service.

| | |
|---|---|
| **Layer** | Translation |
| **Kind** | Micro service (internal translator, `IInternalTranslator`) |

## Purpose

Provide context aware translation for the SysWeaver translation pipeline using an LLM instead of a traditional machine translation service. The model is told the source and target language, the content type and the caller's context, and to keep parameters such as `{0}` untranslated.

## How it fits into SysWeaver

It is one of the interchangeable *internal* translators (`IInternalTranslator`, defined in [SysWeaver.Common](../SysWeaver.Common/README.md)); a caching translator such as [SysWeaver.MicroService.TranslationDbCache](../SysWeaver.MicroService.TranslationDbCache/README.md) sits in front of it so each text is translated once. The LLM calls go through an [`OpenAiService`](../SysWeaver.AI.OpenAI/README.md) instance, which can point at OpenAI or any compatible endpoint (e.g. Gemini's OpenAI endpoint or a local server).

## Key features

- One model per translation effort (`LowModel`, `MediumModel`, `HighModel`)
- Content type aware prompts (plain text, Markdown, HTML), optional source language identification (`"*"`), multiple target languages per request
- Supported languages are queried from the model, cached for 30 days in the shared key/value store, and can be extended (`ForcedLanguages`) or opened up (`AllowAllLanguages`); operators can refresh them through the `UpdateLanguageList` API
- Retries failed requests (`RetryCount`); token usage per effort and performance are available as statistics

## Key components

- [`LlmTranslator`](LlmTranslator.cs) — the translator service and its prompts
- [`LlmTranslatorParams`](LlmTranslatorParams.cs) — parameters

## Using it

Register after the OpenAI service it uses (and before the translation cache):

```json
{ "Type": "SysWeaver.AI.OpenAiService, SysWeaver.AI.OpenAI", "Params": { "CredFile": "$(KeyFolder)/OpenAI.txt" } },
{ "Type": "SysWeaver.AI.LlmTranslator, SysWeaver.AI.LlmTranslator", "Params": { "LowModel": "gpt-4.1-nano", "MediumModel": "gpt-4.1-mini", "HighModel": "gpt-4.1" } }
```

## Limitations and considerations

- Requires the OpenAI provider specifically (`AiInstance` selects the instance); the model names must match its endpoint
- LLM calls cost money and add latency — always combine with a cache
- The list of supported languages is only as good as the model's answer

## Relationships

- **Project:** [`SysWeaver.AI.LlmTranslator.csproj`](SysWeaver.AI.LlmTranslator.csproj)
- **Builds on:** [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md), [SysWeaver.Storage](../SysWeaver.Storage/README.md)
- **Used by:** [TranslationService](https://github.com/dosh1974/SysWeaver.Services/blob/main/TranslationService/README.md) (SysWeaver.Services)
