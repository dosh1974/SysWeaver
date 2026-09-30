# SysWeaver.LanguageIdentifier.Llm

[⬆ SysWeaver overview](../README.md)

> Language identification using a large language model through the OpenAI service.

| | |
|---|---|
| **Layer** | Translation / AI |
| **Kind** | Micro service (`ILanguageIdentifier`) |

## Purpose

Accurate language detection, including for short or mixed texts, as an alternative to [SysWeaver.LanguageIdentifier.FastText](../SysWeaver.LanguageIdentifier.FastText/README.md).

## Limitations and considerations

- Each identification is an LLM call: slower and costs money; cache or batch where possible.

## Relationships

- **Project:** [`SysWeaver.LanguageIdentifier.Llm.csproj`](SysWeaver.LanguageIdentifier.Llm.csproj)
- **Builds on:** [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md), [SysWeaver.MicroServices](../SysWeaver.MicroServices/README.md)
