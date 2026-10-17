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

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function. `_TF(text, context)` and `_T(text, args…, context)` are the common.js translation helpers; every user-visible string in this file goes through them.

### `Translator.js`

Page script for `Edit.html` (the translation editor), loaded with a static `<script defer>` after `../common/common.js`. It defines one global function, `translatorEditMain`, which the page calls from `<body onload="translatorEditMain()">`. All page building happens in inner helper functions; nothing runs at load time apart from the declaration.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `translatorEditMain` | function (global) | The page entry point (see below). No other globals are defined. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `translatorEditMain` | async `translatorEditMain()` → `Promise<void>` | Shows a loader (`AddLoading()`) and reads the URL param `id`; a missing id throws "No id supplied!". It appends a `SysWeaver-Page` element with class `Wide` to `document.body`. It adds a "Translation Key" section with the id, then calls `sendRequest("../Api/translator/GetTranslation", id)`; a falsy result throws "No data for the supplied id". It then adds, in order: an "Application" section (`data.ServiceName`, only if set); either a "Translator" section (when `data.IsManual`) or an "Auth" section, with `data.UserName` (only if set); "Source language" with a flag from `../iso_data/language/<data.From>` and `data.FromName ?? data.From`; "Text" with `data.Text` in a `SysWeaver-Text` with class `Bold`; "Context" (`data.Context`, only if set); "Target language" with a flag from `../iso_data/language/<data.To>` and `data.ToName ?? data.To`; and "Translation" followed by a `<textarea class="Translation">` holding `data.Translated`. Below these it adds a `SysWeaver-CenterBlock` with class `Space`, holding a **Save translation** `Button` (icon `../icons/disc.svg`, initially disabled). The button's click handler makes the textarea read-only, calls `StartWorking()`, and sends `sendRequest("../Api/translator/SetTranslation", {Key: id, OriginalTranslation: data.Translated, NewTranslation: ta.value})`. A falsy result throws "Couldn't set translation (key no longer exist?)". On success it shows `Info("Translation saved!")` and sets `data.Translated` to the new text. In a `finally` block it calls `Validate()`, makes the textarea editable again and calls `StopWorking()`. `ta.oninput` is set to `Validate`. Any error in the main body is shown with `Fail(e)`, and the loader is always removed. `PageLoaded()` is not called. |

#### Inner functions of `translatorEditMain`

| Function | Signature | Description |
|---|---|---|
| `AddSection` | inner `AddSection(name, title)` → `HTMLElement` | Creates a `SysWeaver-Section` element with `innerText = name` and an optional tooltip (`title`), appends it to the page and returns it. |
| `AddText` | inner `AddText(name, title)` → `HTMLElement` | Creates a `SysWeaver-Text` element with `innerText = name` and an optional tooltip, appends it to the page and returns it. The caller adds `Bold` to the "Text" one. |
| `AddImgText` | inner `AddImgText(icon, name, title)` → `undefined` | Creates a `SysWeaver-Text` element with class `Icon`, with `style.backgroundImage = url('<icon>')` (a language flag), `innerText = name` and an optional tooltip, and appends it to the page. It returns nothing. |
| `Validate` | inner `Validate()` → `undefined` | Enables the save button (`changeButton.SetEnabled`) only when the textarea is non-empty and differs from `data.Translated`. |

## CSS reference

### `Translator.css`

Styles `Edit.html`, the standalone translation editor (it does not use the app shell). It is loaded by a static `<link>` after `../common/theme.css`. `theme.css` provides the theme variables used here and the base `SysWeaver-Page` / `SysWeaver-Page.Wide` layout, which this file does not restyle. It is not templated. All elements are created by `translatorEditMain` in `Translator.js`.

#### Page

| Selector(s) | Description |
|---|---|
| `body`, `html` | No padding or margin. |

#### Sections and text blocks

| Selector(s) | Description |
|---|---|
| `SysWeaver-Section` | Section headings created by `AddSection`. They are block elements with 2em top and 1em bottom margin, uppercase, 80% size and bold, in the `--ThemeAcc1` color, with 0.25em padding. |
| `SysWeaver-Text` | Value blocks created by `AddText` and `AddImgText`. They are selectable blocks (`user-select: text`) with `white-space: pre-wrap` and horizontal scrolling, on a faint tinted background (`rgba(var(--ThemeMainRGB), 0.05)`), with 0.25em padding. |
| `SysWeaver-Text.Icon` | Language rows created by `AddImgText` (class `Icon`). The inline `background-image` flag is placed 8px from the left, vertically centered, 32px wide and not repeated, with 48px left padding to make room for it. |
| `SysWeaver-Text.Bold` | Bold text. `translatorEditMain` adds `Bold` to the source-text block. |

#### Editor

| Selector(s) | Description |
|---|---|
| `textarea.Translation` | The editable translation field. It is full width (border-box), resizable only vertically, at least 6em high, with no border. It uses the theme font and font size (`--ThemeFont`, `--ThemeFontSize`), bold, in the `--ThemeMain` color, with the input radius `--ThemeRadInp` (bottom-right corner square for the resize grip). The background is a faint tint and there is no outline; the outline change is animated over `--ThemeFadeDuration`. |
| `textarea.Translation:focus` | A 2px solid `--ThemeAcc2` outline on focus. |
| `SysWeaver-CenterBlock.Space` | The button row below the textarea (class `Space`, added in `translatorEditMain`). It has a 2em top and 4em bottom margin. |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--ThemeAcc1` | theme.css | `SysWeaver-Section` text color |
| `--ThemeAcc2` | theme.css | textarea focus outline |
| `--ThemeMain` | theme.css | textarea text color |
| `--ThemeMainRGB` | theme.css (RGB triplet) | faint background tint of `SysWeaver-Text` and the textarea |
| `--ThemeFont`, `--ThemeFontSize` | theme.css | textarea font |
| `--ThemeRadInp` | theme.css | textarea border radius |
| `--ThemeFadeDuration` | theme.css | outline transition time |

#### Animations, media queries and theme variants

There are none in this file. The only motion is the outline `transition` on `textarea.Translation`.
