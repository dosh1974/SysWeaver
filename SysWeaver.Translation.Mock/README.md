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

## Key features

- Works offline; translating a mock translation again changes the tag (no `[de] [sv] ..`)
- Texts with the no translate prefix come back without it, as with a real translator
- `Delay`: wait before each answer (to test how a page shows a slow translation)
- `LogContext`: writes each request (count, target, context, first text) to the console, to check the context callers give

## Use

In the services file, in place of the real translator:

```json
{ "Type": "SysWeaver.Translation.MockTranslator, SysWeaver.Translation.Mock", "Params": { "Delay": 0, "LogContext": true } }
```

## Key components

- [`MockTranslator`](MockTranslator.cs) — the translator and its parameters (`MockTranslatorParams`)
