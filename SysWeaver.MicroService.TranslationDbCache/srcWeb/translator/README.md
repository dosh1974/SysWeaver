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

## Behavior (web analysis)

### Loading and initialization

`Edit.html` loads only `../common/common.js` and `Translator.js` (`defer`; no app shell JS or CSS), plus `../common/theme.css` and `Translator.css`, and calls `translatorEditMain()` on load. It is opened from the row action that `DbInput.cs` defines on the *Translations DB* table (`WebMenuTable` "Debug/Translator/{0}" on `TranslatorDbCacheService.TranslationsTable`, an explorer table): `"&../translator/Edit.html?id={0}"`, where `{0}` is the row key. The leading `&` is the table action URL convention handled by the explorer table code.

### Files

- **`Translator.js`**: a global `translatorEditMain()`.
  1. It shows a loader (`AddLoading`), reads `id` from the URL (it throws if missing), and creates `<SysWeaver-Page class="Wide">`.
  2. Local helpers append labelled blocks: `AddSection(name, title)` → `<SysWeaver-Section>`, `AddText` → `<SysWeaver-Text>`, `AddImgText(icon, ...)` → `<SysWeaver-Text class="Icon">` with a flag background.
  3. It calls `GetTranslation(id)` and renders, in order: key; application (`ServiceName`); translator (`UserName`, when `IsManual`) or auth (`UserName`, when automatic); source language (flag `../iso_data/language/<From>`, `FromName`); text (bold); context (optional); target language (`To`/`ToName`); then a `<textarea class="Translation">` with `Translated`.
  4. A `SysWeaver-CenterBlock.Space` holds the "Save translation" `Button` (icon `../icons/disc.svg`). It is enabled only when the text is non-empty and differs from the last saved value (`Validate` on `input`). Saving makes the textarea read-only and calls `SetTranslation {Key, OriginalTranslation, NewTranslation}`. A falsy result throws ("key no longer exist?"); on success it shows `Info` and updates the baseline.
  5. All labels go through `_TF`/`_T`, with translator context strings, so the page is itself translated. Errors go to `Fail`.
- **`Edit.html`**: shell page, empty body.
- **`Translator.css`**: styles for the custom elements `SysWeaver-Section` (uppercase accent caption), `SysWeaver-Text` (selectable pre-wrap block; `.Icon` with a 32px flag at the left; `.Bold`), `textarea.Translation` (theme-colored, focus outline `--ThemeAcc2`) and `SysWeaver-CenterBlock.Space`. `SysWeaver-Page` and `SysWeaver-CenterBlock` base styles come from theme/common CSS.

### Server interaction

Both endpoints are under `Api/translator/` (`[WebApiUrl("translator")]`), both require `Roles.Debug`, and both are sent with `sendRequest`:
- `GetTranslation(key)` (plain string body) → `{ServiceName, UserName, IsManual, From, FromName, To, ToName, Text, Context, Translated}`, or null.
- `SetTranslation {Key, OriginalTranslation, NewTranslation}` → bool (audited). `OriginalTranslation` lets the server detect a concurrent change.

Polling and push are not used.

### State and persistence

URL param `id` only. The in-memory `data.Translated` is the baseline for change detection.

### Dependencies

- common.js: `AddLoading`, `getUrlParams`, `sendRequest`, `Button`, `Info`, `Fail`, `_T`/`_TF`.
- theme.css.
- Language flags from `iso_data`.
- Consumer: the explorer table row action defined in `DbInput.cs`.

### Structural observations

- A standalone page without the app shell. The DOM is built from custom `SysWeaver-*` element names with local helper functions. It does not call `PageLoaded()`.
- The flag URL is built without the `.svg` extension (`../iso_data/language/<iso>`), unlike chat.js, which appends `.svg`; both forms are relied on from the iso_data handler.
- The editor pattern (load by id, show read-only metadata, one editable field, save with optimistic-concurrency field) is small and self-contained.
