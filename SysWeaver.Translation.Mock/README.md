# SysWeaver.Translation.Mock

[⬆ SysWeaver overview](../README.md)

> A translator for tests: no outside service, no cost, no rate limit.

| | |
|---|---|
| **Layer** | Translation |
| **Kind** | Micro service (internal translator, `IInternalTranslator`) |

## Purpose

Real translation costs money and is slow. For tests, a translation is the text with the target language in front
(`"[de] Which animal is this?"`), so translate buttons, field updates and undo can be tested offline and translated
texts are easy to spot.

## How it fits into SysWeaver

It is one of the interchangeable *internal* translators (`IInternalTranslator`, defined in [SysWeaver.Common](../SysWeaver.Common/README.md)) and takes the place of a real one, such as [SysWeaver.Translation.Google](../SysWeaver.Translation.Google/README.md) or [SysWeaver.AI.LlmTranslator](../SysWeaver.AI.LlmTranslator/README.md), behind the caching translator of [SysWeaver.MicroService.TranslationDbCache](../SysWeaver.MicroService.TranslationDbCache/README.md).

## Key features

- Works offline; translating a mock translation again changes the tag (no `[de] [sv] ..`)
- Texts with the no translate prefix come back without it, as with a real translator
- Several targets (`"de,sv"`) give one translation per target, in that order
- `Languages`: the languages it "translates" from and to (null: a common set of 15, `MockTranslatorParams.DefaultLanguages`)
- `Delay`: wait (milliseconds) before each answer, to test how a page shows a slow translation
- `LogContext` (default true): writes each request (count, target, context, first text) to the console, to check the context callers give

## Key components

- [`MockTranslator`](MockTranslator.cs) — the translator and its parameters (`MockTranslatorParams`)

## Using it

In the services file, in place of the real translator:

```json
{ "Type": "SysWeaver.Translation.MockTranslator, SysWeaver.Translation.Mock", "Params": { "Delay": 0, "LogContext": true } }
```

## Limitations and considerations

- For tests only: nothing is translated
- The context, effort, retention and content type of a request are ignored (the context is only logged)

## Relationships

- **Project:** [`SysWeaver.Translation.Mock.csproj`](SysWeaver.Translation.Mock.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.IsoData](../SysWeaver.IsoData/README.md)
