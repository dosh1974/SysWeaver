# srcData

[⬆ SysWeaver.TableData](../README.md)

> Source of the embedded templates used by the HTML table exporters.

| | |
|---|---|
| **Project** | [SysWeaver.TableData](../README.md) |
| **Kind** | Embedded data (HTML templates) |

## Purpose

The `EmbedLocal` target in [`SysWeaver.TableData.csproj`](../SysWeaver.TableData.csproj) runs `Compress.exe -Methods br -OptimizeWebFiles false .\srcData\*+ .\data` before each build. The templates are compressed but not minified. The generated `data/**/*.br` files (not in git) are embedded as resources. The build also copies the README.md files of this folder into `data/` (next to their `.br` versions), and the project file embeds `data/**/*.md` too, so these READMEs end up as resources too (not read by the code of this project).

[`HtmlTableDataExporter`](../HtmlTableDataExporter.cs) loads a template set from a sub folder by name: `<folder>.<folder>Body.html`, `...Row.html`, `...Header.html` and `...Cell.html`. A new HTML export style can therefore be added as a new sub folder with those four files.

## Contents

| Item | Description |
|---|---|
| [`Simple/`](Simple/README.md) | Templates of `HtmlTableDataExporter.Simple`, a basic styled HTML table |

## Relationships

- **Part of:** [SysWeaver.TableData](../README.md)
- **Used by:** [`HtmlTableDataExporter.cs`](../HtmlTableDataExporter.cs)
