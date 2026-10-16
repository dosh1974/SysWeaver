# srcData

[⬆ SysWeaver.TableData](../README.md)

> Source of the embedded templates used by the HTML table exporters.

| | |
|---|---|
| **Project** | [SysWeaver.TableData](../README.md) |
| **Kind** | Embedded data (HTML templates) |

## Purpose

The `EmbedLocal` target in [`SysWeaver.TableData.csproj`](../SysWeaver.TableData.csproj) runs `Compress.exe -Methods br -OptimizeWebFiles false .\srcData\*+ .\data` before each build. The templates are compressed but not minified. The generated `data/**/*.br` files (not in git) are embedded as resources. The project file also embeds `data/**/*.md`, but this folder currently has no Markdown files.

[`HtmlTableDataExporter`](../HtmlTableDataExporter.cs) loads a template set from a sub folder by name: `<folder>.<folder>Body.html`, `...Row.html`, `...Header.html` and `...Cell.html`. A new HTML export style can therefore be added as a new sub folder with those four files.

## Contents

| Item | Description |
|---|---|
| [`Simple/`](Simple/README.md) | Templates of `HtmlTableDataExporter.Simple`, a basic styled HTML table |

## Relationships

- **Part of:** [SysWeaver.TableData](../README.md)
- **Used by:** [`HtmlTableDataExporter.cs`](../HtmlTableDataExporter.cs)
