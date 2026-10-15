# Translation

[⬆ SysWeaver.TableData](../README.md)

> In-place translation of `[AutoTranslate]` string members in object graphs, with generated and cached per-type translators.

| | |
|---|---|
| **Project** | [SysWeaver.TableData](../README.md) |
| **Kind** | Source code |

## Purpose

`TypeTranslator` walks an object graph (nested objects, arrays, collections and dictionary values) and translates every `String` member marked with `[AutoTranslate]` through an `ITranslator`. The source language comes from `AutoTranslateAttribute.FromLanguage` (default `"en"`) or from `[AutoTranslateDynLanguage]`. Translation context is built from the member's XML doc summary and from `[AutoTranslateContext]` / `[AutoTranslateType]`. The code for each type is generated once with expression trees (`TypeTranslatorT<T>`) and cached. The table data system uses it to translate table rows (`TableDataTools.Translate`), and the HTTP server uses it to translate API responses.

## Contents

| Item | Description |
|---|---|
| [`TypeTranslator.cs`](TypeTranslator.cs) | `TypeTranslator`: entry points for translating objects and looking up a type's translator (`TryGetTranslator`) |
| [`TypeTranslatorT.cs`](TypeTranslatorT.cs) | `ITypeTranslator` and the generated `TypeTranslatorT<T>`, which also detects invalid attribute usage when it is built |

## Relationships

- **Part of:** [SysWeaver.TableData](../README.md)
- **Used by:** [`TableDataTools.cs`](../TableDataTools.cs), [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md), [SysWeaver.Chat](../../SysWeaver.Chat/README.md)
