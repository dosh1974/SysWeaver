# translator

[⬆ srcWeb](../README.md)

> The translation editor: inspect and override one cached translation.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.TranslationDbCache](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

The *Translations DB* table (menu `Debug/Translator/...`) has an *Edit* action on each row. It opens `translator/Edit.html?id={0}` (defined in [`DbInput.cs`](../../DbInput.cs)), and the page then calls the service API under `Api/translator/`.

## Contents

| Item | Description |
|---|---|
| [`Edit.html`](Edit.html) | Editor page for one translation (`translatorEditMain()`) |
| [`Translator.js`](Translator.js) | Editor logic: loads the translation (`GetTranslation`) and saves changes (`SetTranslation`) |
| [`Translator.css`](Translator.css) | Styles for the editor |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.TranslationDbCache](../../README.md)
- **Uses:** `common/common.js` from [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md)
