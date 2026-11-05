# Data

[⬆ SysWeaver.Common](../README.md)

> The table data vocabulary: the type agnostic table format returned by table data web APIs, the request / sort / filter types, the exporter contract and the attributes that control how a type's members become columns.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (contracts and attributes, namespace `SysWeaver.Data`) |

## Purpose

[SysWeaver.TableData](../../SysWeaver.TableData/README.md) turns collections of objects into paged, sortable and filterable tables for the web client; this folder holds everything both sides agree on, so that services can annotate their row types without referencing the table data project. A row type's public members become columns; the `TableData*` attributes rename, order, hide, expand or ignore members and attach a format hint (`TableDataRawFormatAttribute` and its many derived attributes) that is copied to `TableDataColumn.Format` and interpreted by the web client. Exporters (e.g. to Excel) implement `ITableDataExporter` and are exposed by services through `IHaveTableDataExporters`.

## Contents

| Item | Description |
|---|---|
| [`TableData.cs`](TableData.cs) | The table returned by table data web APIs (column change counter, refresh interval) |
| [`BaseTableData.cs`](BaseTableData.cs) | `CommonTableData`, `BaseTableData` and `TypedTableData`: rows, columns, title and paging members shared by all responses |
| [`TableDataRow.cs`](TableDataRow.cs) | A row (one value per column) |
| [`TableDataColumn.cs`](TableDataColumn.cs) | Column description (name, .NET type, description, title, format, properties) |
| [`TableDataColumnProps.cs`](TableDataColumnProps.cs) | Column property flags (sortable, sorted descending, hidden, ...) |
| [`TableDataRequest.cs`](TableDataRequest.cs) | Request types: sort and filter, order, paging (`TableDataRequest`) and `TableDataState` |
| [`TableDataFilter.cs`](TableDataFilter.cs) | A column filter condition |
| [`TableDataFilterOps.cs`](TableDataFilterOps.cs) | Filter operations and their properties |
| [`TableDataFormats.cs`](TableDataFormats.cs) | The built-in format names usable in a column format string |
| [`TableDataConsts.cs`](TableDataConsts.cs) | Constants and helpers (web search and Wikipedia url formats, ...) |
| [`ITableDataExporter.cs`](ITableDataExporter.cs) | An exporter of tables to some file format (menu metadata and `Export`) |
| [`IHaveTableDataExporters.cs`](IHaveTableDataExporters.cs) | Implemented by service objects that expose table exporters |
| [`TableDataExportOptions.cs`](TableDataExportOptions.cs) | Export options: suggested file name, no headers, portrait mode |
| Column behaviour attributes (10 files) | [`TableDataNameAttribute.cs`](TableDataNameAttribute.cs), [`TableDataTitleAttribute.cs`](TableDataTitleAttribute.cs), [`TableDataDescAttribute.cs`](TableDataDescAttribute.cs) (also word wrap), [`TableDataOrderAttribute.cs`](TableDataOrderAttribute.cs) (also include / search), [`TableDataHideAttribute.cs`](TableDataHideAttribute.cs), [`TableDataNoTitleAttribute.cs`](TableDataNoTitleAttribute.cs) (no header title text, for icon only columns; sort / filter unchanged), [`TableDataIgnoreAttribute.cs`](TableDataIgnoreAttribute.cs), [`TableDataNoSortAttribute.cs`](TableDataNoSortAttribute.cs), [`TableDataSortDescAttribute.cs`](TableDataSortDescAttribute.cs) (client hint: first sort descending), [`TableDataDefaultOrderAttribute.cs`](TableDataDefaultOrderAttribute.cs) (on the row type: the order used when a request has none, also sent as `DefaultOrder` with the columns), plus [`TableDataExpandAttribute.cs`](TableDataExpandAttribute.cs) (expands a member's own members into columns) |
| Format attributes (15 files) | [`TableDataRawFormatAttribute.cs`](TableDataRawFormatAttribute.cs) (the base: raw format string) and derived formats: [`TableDataFormatAttribute.cs`](TableDataFormatAttribute.cs), [`TableDataNumberAttribute.cs`](TableDataNumberAttribute.cs) (number, percentage, multiplier, key, amount, currency amount (`TableDataCurrency`: a number formatted by the web client with the currency of the column or row), XP, stars), [`TableDataByteSizeAttribute.cs`](TableDataByteSizeAttribute.cs) (also uptime), [`TableDataByteSpeedAttribute.cs`](TableDataByteSpeedAttribute.cs), [`TableDataDurationAttribute.cs`](TableDataDurationAttribute.cs) (also JSON, text, markdown), [`TableDataUrlAttribute.cs`](TableDataUrlAttribute.cs) (also web colour), [`TableDataImgAttribute.cs`](TableDataImgAttribute.cs) (also user icon), [`TableDataTagsAttribute.cs`](TableDataTagsAttribute.cs), [`TableDataAuthAttribute.cs`](TableDataAuthAttribute.cs) (an auth requirement as tags: null = the accent "Open" tag, "" = "Any logged in user", else the tokens; use it on every "required auth" column), [`TableDataActionsAttribute.cs`](TableDataActionsAttribute.cs), [`TableDataActionsExAttribute.cs`](TableDataActionsExAttribute.cs) (actions with a confirmation question and a per row visibility), [`TableDataActionsFullAttribute.cs`](TableDataActionsFullAttribute.cs) (also a done text, and with [`TableDataActionFlags.cs`](TableDataActionFlags.cs) the declared final actions: Delete, Kill, Withdraw ...; the web table never guesses it from the words, G47), [`TableDataBooleanToggleAttribute.cs`](TableDataBooleanToggleAttribute.cs), [`TableDataPerRowFormatAttribute.cs`](TableDataPerRowFormatAttribute.cs), [`TableDataExternals.cs`](TableDataExternals.cs) (links and images for ISO countries, currencies and languages, Wikipedia, Google search and maps, file extensions, MIME types, encodings, IP addresses, user agents) |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Used by:** [SysWeaver.TableData](../../SysWeaver.TableData/README.md) (processing and web API), [SysWeaver.Excel](../../SysWeaver.Excel/README.md) (exporter), [SysWeaver.MicroService.HttpServer](../../SysWeaver.MicroService.HttpServer/README.md) and any service that annotates row types
- **Related:** [Chart/](../Chart/README.md) (the equivalent exporter contract for charts)
