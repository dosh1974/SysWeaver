# edit

[⬆ srcWeb](../README.md)

> The browser object editor that renders a form for any type described by `TypeService`, plus the type information page and the Ace-based text / code editor.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Edit](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Served under `edit/`. There are three parts:

- **Object editor** — `Edit.js` is included by other pages (e.g. `explore/api.html` of the [API explorer](../../../SysWeaver.HttpServer.ExploreModule/README.md) and `ServerManager/services.html` of [SysWeaver.MicroService.ServerManager](../../../SysWeaver.MicroService.ServerManager/README.md)). It loads the type information from `TypeService`, picks an editor per member (`EditType*.js`, loaded on demand) and lays the form out in a desktop or mobile style. A member with a special type specification (`Type` in the member description) is handled by a global `EditType<Spec>Factory` function if one is present, which is how `EditTypeLanguage.js` plugs in.
- **Type information page** — `type.html?q=<type name>` shows the documentation of a type and its members; common table formatting links type names to it.
- **Text / code editor** — `text.html?r=<read url>` shows a text file with Ace, optionally with save (`u=`), delete (`d=`), download, reload and a Prettier based *Beautify* button. `TypeService` registers it as the editor for common text file extensions, and log or manifest tables link to it.

Processing (minification, Brotli compression, embedding) is described in [`srcWeb/`](../README.md).

## Contents

| Item | Description |
|---|---|
| [`external/`](external/README.md) | Vendored Ace editor (modes, themes, workers, extensions), Prettier and Ace snippets |
| [`Edit.js`](Edit.js) | `Edit` class: the object editor, type handler lookup, undo / redo, `editTestMain` for the test page |
| [`EditOptions.js`](EditOptions.js) | `EditOptions`: title, read-only, expansion, menu and preset option sets (`Clean`, `CleanReadOnly`, ...) |
| [`EditDesktop.js`](EditDesktop.js) / [`EditDesktop.css`](EditDesktop.css) | Desktop (table) layout of the form |
| [`EditMobile.js`](EditMobile.js) / [`EditMobile.css`](EditMobile.css) | Mobile layout of the form |
| [`Edit.css`](Edit.css) | Shared editor styles |
| [`UndoStack.js`](UndoStack.js) | Undo / redo stack used by the editor |
| [`EditTypeBoolean.js`](EditTypeBoolean.js) | Check box editor for booleans |
| [`EditTypeInteger.js`](EditTypeInteger.js) | Integer editor (ranges, sliders) |
| [`EditTypeNumber.js`](EditTypeNumber.js) | Floating point editor (ranges, sliders) |
| [`EditTypeString.js`](EditTypeString.js) | String editor (single / multi-line, password) |
| [`EditTypeEnum.js`](EditTypeEnum.js) | Enum and flags editor |
| [`EditTypeDateTime.js`](EditTypeDateTime.js) | Date and time editor |
| [`EditTypeObject.js`](EditTypeObject.js) | Nested object editor |
| [`EditTypeCollection.js`](EditTypeCollection.js) | Collection editor; loads `EditCollectionArray.js` / `EditCollectionMap.js` |
| [`EditCollectionArray.js`](EditCollectionArray.js) | Array / list operations for the collection editor |
| [`EditCollectionMap.js`](EditCollectionMap.js) | Dictionary / map operations for the collection editor |
| [`EditTypeLanguage.js`](EditTypeLanguage.js) / [`EditLanguage.css`](EditLanguage.css) | Language selector (`EditTypeLanguageFactory`), uses `iso_data/languages.js` |
| [`EditTypeDefault.js`](EditTypeDefault.js) | Fallback editor for other types |
| [`type.html`](type.html) / [`type.js`](type.js) / [`type.css`](type.css) | Type information page; `type.js` also defines `TypeMemberFlags` and the type info cache used by `Edit.js` |
| [`text.html`](text.html) / [`text.js`](text.js) / [`text.css`](text.css) | Text / code editor page |
| [`index.html`](index.html) | "Edit test" page that runs `editTestMain()` |

## Relationships

- **Part of:** [srcWeb](../README.md), [SysWeaver.MicroService.Edit](../../README.md)
- **Talks to:** [`TypeService`](../../TypeService.cs) (`GetTypeInfo`, `GetDefaultInstance`), ISO data from [`IsoDataService`](../../IsoDataService.cs)
- **Used by:** [SysWeaver.HttpServer.ExploreModule](../../../SysWeaver.HttpServer.ExploreModule/README.md), [SysWeaver.MicroService.ServerManager](../../../SysWeaver.MicroService.ServerManager/README.md), [SysWeaver.MicroService.FolderSync](../../../SysWeaver.MicroService.FolderSync/README.md) (manifest links to `text.html`)
