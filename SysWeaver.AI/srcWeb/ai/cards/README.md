# cards

[⬆ ai](../README.md)

> Card templates of the AI model tables, served as `ai/cards/models.html`.

| | |
|---|---|
| **Project** | [SysWeaver.AI](../../../README.md) |
| **Kind** | Card templates |

## Purpose

The model row types point at these templates with `[TableDataCard]`, so the model tables of the AI providers (`GetLlmModelTable`, `GetImageModelTable`) can show each model as a card (the "Card templates" section of the [explore README](../../../../SysWeaver.HttpServer.ExploreModule/srcWeb/explore/README.md#card-templates-card) describes the format). At build time the file is Brotli compressed into `web/ai/cards/` and embedded (see [srcWeb](../../README.md)).

## Contents

| Item | Description |
|---|---|
| [`models.html`](models.html) | Card templates, not a page: `llm` ([`AiLlmModel`](../../../AiLlmModel.cs), cards by default: id, owner, description, input and output token limits, name, reasoning and creation time) and `image` ([`AiImageModel`](../../../AiImageModel.cs) and [`AiImageEditModel`](../../../AiImageEditModel.cs), table by default: id, owner, description, name and creation time) |

## Relationships

- **Part of:** [SysWeaver.AI](../../../README.md)
- **Used by:** `explore/table.js` of [SysWeaver.HttpServer.ExploreModule](../../../../SysWeaver.HttpServer.ExploreModule/README.md) (loads the templates)
