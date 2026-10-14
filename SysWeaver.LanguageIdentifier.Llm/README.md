# SysWeaver.LanguageIdentifier.Llm

[⬆ SysWeaver overview](../README.md)

> Language identification using a large language model through the OpenAI service.

| | |
|---|---|
| **Layer** | Translation |
| **Kind** | Micro service (`ILanguageIdentifier`) |

## Purpose

More accurate language detection, also for short or mixed texts, as an alternative to [SysWeaver.LanguageIdentifier.FastText](../SysWeaver.LanguageIdentifier.FastText/README.md).

## How it fits into SysWeaver

It implements `ILanguageIdentifier` (defined in [SysWeaver.Common](../SysWeaver.Common/README.md)) and asks a model through the registered [`OpenAiService`](../SysWeaver.AI.OpenAI/README.md) (a required dependency, so it must be registered first). Any OpenAI compatible endpoint configured on that service can be used.

## Key features

- Returns the best language or a ranked list with confidences (ISO 639-1 codes), optionally biased towards the user's language
- Configurable model (`Model`), concurrency limit (`MaxConcurrency`) and a short in-memory result cache (`CacheSeconds`)
- Statistics and performance monitoring

## Key components

- [`LlmLanguageIdentifier`](LlmLanguageIdentifier.cs) — the language identifier service and its prompts
- [`LlmLanguageIdentifierParams`](LlmLanguageIdentifierParams.cs) — parameters

## Using it

```json
{ "Type": "SysWeaver.LanguageIdentifier.LlmLanguageIdentifier, SysWeaver.LanguageIdentifier.Llm", "Params": { "Model": "gpt-4.1-nano" } }
```

## Limitations and considerations

- Each identification is an LLM call: slower than fastText and costs money
- Requires the OpenAI provider specifically; the model must be available on its endpoint

## Relationships

- **Project:** [`SysWeaver.LanguageIdentifier.Llm.csproj`](SysWeaver.LanguageIdentifier.Llm.csproj)
- **Builds on:** [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md), [SysWeaver.MicroServices](../SysWeaver.MicroServices/README.md)
