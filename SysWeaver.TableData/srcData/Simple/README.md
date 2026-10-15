# Simple

[⬆ srcData](../README.md)

> The four text templates of the "Simple HTML" table exporter.

| | |
|---|---|
| **Project** | [SysWeaver.TableData](../../README.md) |
| **Kind** | Embedded data (HTML templates) |

## Purpose

`HtmlTableDataExporter.Simple` ("Simple HTML", "A basic HTML table") builds a standalone HTML page from these templates. Placeholders use the `${...}` syntax. Body: `Title`, `Rows`. Row: `Cells`. Header: `Title` (column description), `Text`, `Class`. Cell: `Title` (raw value), `Text`, `TextFmt` (pre-formatted markup), `Class`. The body defines CSS classes that the exporter assigns to cells, by value type: `.n` (number), `.t` (time), `.b` (boolean) and `.m` (monospace), plus `.r` (right aligned) and `.w` (wrapped text).

## Contents

| Item | Description |
|---|---|
| [`SimpleBody.html`](SimpleBody.html) | Page skeleton with the inline stylesheet and the `<table>` |
| [`SimpleRow.html`](SimpleRow.html) | One `<tr>` row |
| [`SimpleHeader.html`](SimpleHeader.html) | One `<th>` header cell |
| [`SimpleCell.html`](SimpleCell.html) | One `<td>` data cell |

## Relationships

- **Part of:** [srcData](../README.md) of [SysWeaver.TableData](../../README.md)
- **Used by:** [`HtmlTableDataExporter.cs`](../../HtmlTableDataExporter.cs)
