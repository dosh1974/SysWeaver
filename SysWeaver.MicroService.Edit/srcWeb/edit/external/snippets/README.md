# snippets

[⬆ external](../README.md)

> Ace code snippet definitions, one file per language mode, part of the vendored Ace 1.44.0 distribution.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Edit](../../../../README.md) |
| **Kind** | Vendored library |

## Purpose

With snippets enabled by `ext-language_tools.js` (see [`external/`](../README.md)), Ace loads `ace/snippets/<mode>` from this folder when a mode is activated and offers the snippets in autocompletion. The file names match the `mode-*.js` files of the parent folder one to one.

## Contents

| Item | Description |
|---|---|
| `*.js` (199 files) | One file per Ace mode, e.g. [`csharp.js`](csharp.js), [`javascript.js`](javascript.js), [`css.js`](css.js), [`html.js`](html.js), [`python.js`](python.js), [`sql.js`](sql.js) |

About 565 KB in total. Around 147 of the files (e.g. `csharp.js`) are small stubs under 600 bytes that contain only the module loader wrapper and define no snippets; the actual snippet text is in files such as `javascript.js`, `css.js`, `html.js`, `ruby.js` or `gobstones.js`.

## Relationships

- **Part of:** [external](../README.md), [SysWeaver.MicroService.Edit](../../../../README.md)
- **Used by:** Ace (`ace.js` with `ext-language_tools.js`) in [`text.html`](../../text.html)
