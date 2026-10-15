# Search

[⬆ SysWeaver.Common](../README.md)

> The text search contracts and a simple built-in implementation based on sub string matching of words and numbers.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (contracts and implementation, namespace `SysWeaver.Search`) |

## Purpose

`ITextSearch` is a factory for searchers (for static or semi-static content, `ITextSearcher<T>`) and rankers (for dynamic data, `ITextRanker`). `SimpleTextSearch` is the built-in implementation: the search text is split into lower cased words and numbers, which are looked for as sub strings in the texts; its searcher is thread safe and ranks all content with a linear scan on every search.

## Contents

| Item | Description |
|---|---|
| [`ITextSearch.cs`](ITextSearch.cs) | Factory for searchers and rankers |
| [`ITextSearcher.cs`](ITextSearcher.cs) | A searchable collection of content with associated texts (add, remove, search) |
| [`ITextRanker.cs`](ITextRanker.cs) | Ranks texts against a search text, optionally with an explicit weight per text |
| [`SimpleTextSearch.cs`](SimpleTextSearch.cs) | The simple implementation of `ITextSearch` |
| [`SimpleTextSearch.Ranker.cs`](SimpleTextSearch.Ranker.cs) | Its ranker |
| [`SimpleTextSearch.Searcher.cs`](SimpleTextSearch.Searcher.cs) | Its thread safe, linear scan searcher |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Used by:** [SysWeaver.TableData](../../SysWeaver.TableData/README.md), [SysWeaver.Media.Svg](../../SysWeaver.Media.Svg/README.md)
