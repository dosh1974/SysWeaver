# Translation

[⬆ SysWeaver.Common](../README.md)

> The translation and language identification contracts: translator interfaces, request types, auto-translation attributes and options.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (contracts, namespaces `SysWeaver.Translation`, `SysWeaver.LanguageIdentifier` and `SysWeaver`) |

## Purpose

Translator services, translation caches, language identifiers and the code that needs translations (http servers, table data) only share the types in this folder. A member marked with `[AutoTranslate]` is translated automatically when returned from an API call (if auto-translation is enabled); the request types carry the effort, cache retention and text format options to the translator service.

## Contents

| Item | Description |
|---|---|
| [`ITranslator.cs`](ITranslator.cs) | A translator (single text, multiple texts, to one or more languages) |
| [`IInternalTranslator.cs`](IInternalTranslator.cs) | A translator with additional, non request based methods (implemented by the translator services) |
| [`ITranslationContext.cs`](ITranslationContext.cs) | A translator and target language, implemented by the http request (session language) |
| [`TranslateRequest.cs`](TranslateRequest.cs) | Request to translate one text (source and target language, text, options) |
| [`TranslateMultipleRequest.cs`](TranslateMultipleRequest.cs) | Request to translate several texts |
| [`TranslationEffort.cs`](TranslationEffort.cs) | How much time / cost to spend (quick, balanced, high quality), and the text format enum |
| [`TranslationCacheRetention.cs`](TranslationCacheRetention.cs) | How long a translation should be cached |
| [`TranslatorTypes.cs`](TranslatorTypes.cs) | Type of an auto translated text (text, markdown, html) |
| [`AutoTranslateAttribute.cs`](AutoTranslateAttribute.cs) | Marks members for automatic translation of API results |
| [`TranslationTools.cs`](TranslationTools.cs) | Constants, e.g. the prefix that excludes a string from translation |
| [`ILanguageIdentifier.cs`](ILanguageIdentifier.cs) | Identifies the language of a text (best guess or up to N guesses with confidence) |
| [`IdentifiedLanguage.cs`](IdentifiedLanguage.cs) | An identified language (ISO 639-1 code) and its confidence |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Implemented by:** [SysWeaver.MicroService.Translation](../../SysWeaver.MicroService.Translation/README.md), [SysWeaver.MicroService.TranslationDbCache](../../SysWeaver.MicroService.TranslationDbCache/README.md), [SysWeaver.Translation.Google](../../SysWeaver.Translation.Google/README.md), [SysWeaver.AI.LlmTranslator](../../SysWeaver.AI.LlmTranslator/README.md), [SysWeaver.LanguageIdentifier.FastText](../../SysWeaver.LanguageIdentifier.FastText/README.md), [SysWeaver.LanguageIdentifier.Llm](../../SysWeaver.LanguageIdentifier.Llm/README.md)
- **Used by:** [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md), [SysWeaver.NetHttpServer](../../SysWeaver.NetHttpServer/README.md), [SysWeaver.AspHttpServer](../../SysWeaver.AspHttpServer/README.md), [SysWeaver.TableData](../../SysWeaver.TableData/README.md)
