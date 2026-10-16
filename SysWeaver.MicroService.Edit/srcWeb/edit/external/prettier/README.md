# prettier

[⬆ external](../README.md)

> The vendored Prettier 3.9.6 browser build: the standalone formatter and its parser plugins, used by the text editor's *Beautify* button.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Edit](../../../../README.md) |
| **Kind** | Vendored library |

## Purpose

[`text.html`](../../text.html) loads `standalone.js` and the plugins `estree`, `typescript`, `babel`, `postcss`, `markdown` and `html`. [`text.js`](../../text.js) then formats the text with `prettier.format` (tabs for indentation) when the user clicks *Beautify*, or automatically with `b=true`:

| Editor type | Prettier parser |
|---|---|
| `json`, `json5` | `json5` (quotes preserved, trailing commas) |
| `css` | `css` |
| `html` | `html` (unquoted attribute values containing `/` or `:` are quoted first) |
| `markdown` | `markdown` |
| `javascript` | `typescript` |
| `xml` | `xml` |

| Library | Version | Licence |
|---|---|---|
| [Prettier](https://prettier.io) standalone (UMD) build | 3.9.6 (version string in `standalone.js`) | MIT upstream; the files carry no licence header |

## Contents

| Item | Description | Loaded by `text.html` |
|---|---|---|
| [`standalone.js`](standalone.js) | The formatter core (`prettier.format`) | yes |
| [`estree.js`](estree.js) | Printer for JavaScript / TypeScript syntax trees | yes |
| [`typescript.js`](typescript.js) | TypeScript parser (about 960 KB) | yes |
| [`babel.js`](babel.js) | Babel parser (JavaScript, JSON, JSON5) | yes |
| [`postcss.js`](postcss.js) | CSS / Less / SCSS parser and printer | yes |
| [`markdown.js`](markdown.js) | Markdown parser and printer | yes |
| [`html.js`](html.js) | HTML / Vue / Angular template parser and printer | yes |
| [`acorn.js`](acorn.js) | Acorn JavaScript parser | no |
| [`meriyah.js`](meriyah.js) | Meriyah JavaScript parser | no |
| [`flow.js`](flow.js) | Flow parser (about 1.1 MB) | no |
| [`angular.js`](angular.js) | Angular expression parser | no |
| [`glimmer.js`](glimmer.js) | Handlebars / Glimmer parser and printer | no |
| [`graphql.js`](graphql.js) | GraphQL parser and printer | no |
| [`yaml.js`](yaml.js) | YAML parser and printer | no |

About 3.9 MB in total.

## Limitations and considerations

- No XML plugin (`@prettier/plugin-xml`) is included, so formatting with the `xml` parser fails; `text.js` catches the error, logs a warning and keeps the text unchanged.
- Seven of the plugins are not loaded by any page in the repository.

## Relationships

- **Part of:** [external](../README.md), [SysWeaver.MicroService.Edit](../../../../README.md)
- **Used by:** [`text.html`](../../text.html) / [`text.js`](../../text.js)
