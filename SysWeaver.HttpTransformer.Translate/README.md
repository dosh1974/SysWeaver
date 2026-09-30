# SysWeaver.HttpTransformer.Translate

[⬆ SysWeaver overview](../README.md)

> File transformer for the SysWeaver web server that translates served text files (e.g. HTML, JavaScript) into the session language with SysWeaver.FileTranslation, caching the results on disc.

| | |
|---|---|
| **Layer** | HTTP (transformer plug-in) |
| **Kind** | Cached file transformer (manifest-creatable) |

## Purpose

Transformers let the web server deliver a *processed* version of a file without anyone preparing it in advance. This transformer translates served text files (e.g. HTML, JavaScript) into the session language with SysWeaver.FileTranslation. Results are cached on disc and rebuilt only when the source changes, so the cost is paid once per file version.

## How it fits into SysWeaver

```mermaid
flowchart LR
  Req["request for a file"] --> Srv["HTTP server"]
  Srv --> Chain{"transformer registered<br/>for this extension?"}
  Chain -->|yes| Cache{"cached result current?"}
  Cache -->|yes| Out["serve cached result"]
  Cache -->|no| T["TranslationTransformer"]
  T --> Store["disc cache"]
  Store --> Out
  Chain -->|no| Plain["serve original"]
```

The transformer is a `CachedTransformer`, which implements the server's transformer-service interface; the HTTP server service attaches it automatically when it is registered. Folders marked as dynamic in the file module bypass transformer chains.

## Limitations and considerations

- Requires a translator service; translation quality and cost depend on that translator.
- The disc cache grows with the number of distinct files and versions; entries unused for a while are removed.
- This project is not included in the main solution file and must be added to your build explicitly.

## Using it

```json
{ "Type": "SysWeaver.HttpTransformer.TranslationTransformer, SysWeaver.HttpTransformer.Translate" }
```

## Relationships

- **Project:** [`SysWeaver.HttpTransformer.Translate.csproj`](SysWeaver.HttpTransformer.Translate.csproj)
- **Builds on:** [SysWeaver.FileTranslation](../SysWeaver.FileTranslation/README.md), [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md)
