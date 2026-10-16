# external

[⬆ edit](../README.md)

> The vendored Ace code editor (version 1.44.0): core, extensions, key bindings, 199 language modes and syntax-check workers, plus Prettier and the Ace snippet files in sub folders.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Edit](../../../README.md) |
| **Kind** | Vendored library |

## Purpose

[`text.html`](../text.html) loads `ace.js` and `ext-language_tools.js` directly. Ace then loads the mode (`ace/mode/<type>`, chosen from the file extension or the `t=` parameter), its worker and snippets on demand from this folder, relative to `ace.js`. The files are served from `edit/external/`; the build Brotli compresses them without re-minifying (`-OptimizeWebFiles false`, see [`srcWeb/`](../../README.md)).

| Library | Version | Licence |
|---|---|---|
| [Ace](https://ace.c9.io) (Ajax.org Cloud9 Editor), `src-min-noconflict` style build (`ace.define`) | 1.44.0 (`version="1.44.0"` in `ace.js`) | BSD 3-clause upstream; the files carry no licence header |

## Contents

| Item | Description |
|---|---|
| [`prettier/`](prettier/README.md) | Prettier 3.9.6 standalone build and parser plugins, used by the *Beautify* button |
| [`snippets/`](snippets/README.md) | Ace snippet definitions, one per language mode (199 files) |
| [`ace.js`](ace.js) | Ace core (includes the default `textmate` theme) |
| `ext-*.js` (27 files) | Ace extensions: `beautify`, `code_lens`, `command_bar`, `diff`, `elastic_tabstops_lite`, `emmet`, `error_marker`, `hardwrap`, `inline_autocomplete`, `keybinding_menu`, `language_tools` (autocompletion and snippets, loaded by `text.html`), `linking`, `modelist`, `options`, `prompt`, `rtl`, `searchbox`, `settings_menu`, `simple_tokenizer`, `spellcheck`, `split`, `static_highlight`, `statusbar`, `textarea`, `themelist`, `whitespace`, `whitespaces_in_selection` |
| `keybinding-*.js` (4 files) | Alternative key bindings: `emacs`, `sublime`, `vim`, `vscode` |
| `mode-*.js` (199 files) | Syntax highlighting modes, e.g. `mode-csharp.js`, `mode-javascript.js`, `mode-json.js`, `mode-xml.js`, `mode-markdown.js`, `mode-mysql.js`, `mode-batchfile.js`, `mode-plain_text.js` |
| `worker-*.js` (11 files) | Background syntax checkers: `base`, `coffee`, `css`, `html`, `javascript`, `json`, `lua`, `php`, `xml`, `xquery`, `yaml` |

About 15 MB in total, including the sub folders; the modes are about 5.6 MB and the workers about 3 MB.

## Limitations and considerations

- No separate `theme-*.js` files are included, so only the theme built into `ace.js` is available.
- Apart from `ext-language_tools.js`, `text.js` does not reference the extensions or key bindings (the `ace/ext/beautify` call is commented out); they are kept as part of the Ace distribution. Any mode can be selected with the `t=` parameter of `text.html`.
- When updating Ace, replace the whole folder (core, modes, workers and [`snippets/`](snippets/README.md)) with one release.

## Relationships

- **Part of:** [edit](../README.md), [SysWeaver.MicroService.Edit](../../../README.md)
- **Used by:** [`text.html`](../text.html) / [`text.js`](../text.js)
