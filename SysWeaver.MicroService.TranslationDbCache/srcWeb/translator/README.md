# translator

[⬆ srcWeb](../README.md)

> The editor of one cached translation of the Translations DB.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.TranslationDbCache](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

`translator/Edit.html?id=<key>` is opened (same tab) from the "Edit" row action of the Translations DB table (`DbInput.cs`: `&../translator/Edit.html?id={0}`).

## Contents

| Item | Description |
|---|---|
| [`Edit.html`](Edit.html) | The page (new format: `sw/core.js`, `sw/ui.js`), `<body onload="translatorEditMain()">` |
| [`Translator.js`](Translator.js) | `translatorEditMain()`, `trLanguage(iso, name)`, `trBlock(title, tip, text, bold)` |
| [`Translator.css`](Translator.css) | Page styles |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.TranslationDbCache](../../README.md)
- **Server API (Roles.Debug):** `Api/translator/GetTranslation` (id: `{ServiceName, UserName, IsManual, From, FromName, To, ToName, Text, Context, Translated}`), `Api/translator/SetTranslation` (`{Key, OriginalTranslation, NewTranslation}`: bool, optimistic concurrency). Flags from `../iso_data/language/<iso>`.

## Behavior

A missing `id` shows the fail toast "No id supplied!", an unknown id "No data for the supplied id". Otherwise (Design v2 form page: page header, `Sw.Facts` facts strip, two cards) the facts strip: Translation Key, Application, Translator (manual) or Auth (automatic) user, Source language (flag + name), Target language (flag + name); the "Source" card: Text (bold), Context (optional); the "Translation" card: the editable translation (textarea, vertically resizable) and "Save translation" (enabled only for a non empty text that differs from the last saved one; Ctrl+Enter saves; read only while saving; a false result shows "Couldn't set translation (key no longer exist?)", success an info toast and a new baseline). All labels are translated (with contexts). The page signals loaded (`Sw.Page.Main` with a loading phase), so the app shell reveals it at once.

## JavaScript reference

| Name | Description |
|---|---|
| `translatorEditMain()` | Entry |
| `trSection(page, title, tip, text, {Flag, FlagTitle, Bold, Mono})` | Appends a heading and a read only value block (optionally with a language flag) |

## CSS reference

`.tr-page` (the `sw-page-form` container of `Sw.PageLayout`), `.tr-lang` + `img.tr-flag` (24 px), `.tr-block` (`h3.tr-label` + `.tr-text`, `.tr-bold`), `textarea.tr-translation` (`[readonly]` dimmed while saving), `.tr-buttons` (form footer, Save right aligned). No shadows.
