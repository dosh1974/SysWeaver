# ExtensionHandlers

[⬆ SysWeaver.FileTranslation](../README.md)

> The HTML and JavaScript template builders that extract translatable texts from web files.

| | |
|---|---|
| **Project** | [SysWeaver.FileTranslation](../README.md) |
| **Kind** | Source code (namespace `SysWeaver.MicroService.ExtensionHandlers`) |

## Purpose

`TranslatorExtensionHandlers` (in the project root) registers these builders with `LanguageTemplate` for `.html`, `.htm` and `.js` files. Each builder replaces the translatable texts of a file with template variables, so that the file can be rendered in any language by translating the variables.

## Contents

| Item | Description |
|---|---|
| [`HtmlTranslation.cs`](HtmlTranslation.cs) | HTML template builder (HtmlAgilityPack): text nodes and the `title`, `placeholder` and `alt` attributes, inline scripts handled as JavaScript |
| [`JsTranslation.cs`](JsTranslation.cs) | JavaScript template builder: finds string literals, unescapes and re-escapes them |
| [`TranslationTools.cs`](TranslationTools.cs) | Shared helpers: variable dictionary, adding a text as a variable (identical texts in the same context share one), HTML attribute decoding |

## Relationships

- **Part of:** [SysWeaver.FileTranslation](../README.md)
- **Used by:** [`TranslatorExtensionHandlers.cs`](../TranslatorExtensionHandlers.cs)
