# srcWeb

[⬆ SysWeaver.MicroService.TranslationDbCache](../README.md)

> Source of the embedded translation editor page.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.TranslationDbCache](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

This folder only contains the `translator/` sub folder. The `EmbedLocal` target in [`SysWeaver.MicroService.TranslationDbCache.csproj`](../SysWeaver.MicroService.TranslationDbCache.csproj) runs `Compress.exe -Methods br .\srcWeb\translator\* .\web\translator` before each build. The generated `web/**/*.br` files (not in git) are embedded as resources and served as `translator/...` relative to the site root.

## Contents

| Item | Description |
|---|---|
| [`translator/`](translator/README.md) | Editor for a single cached translation |

## Relationships

- **Part of:** [SysWeaver.MicroService.TranslationDbCache](../README.md)
