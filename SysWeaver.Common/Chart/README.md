# Chart

[⬆ SysWeaver.Common](../README.md)

> The contract for exporting charts to file formats, so that chart rendering and export plug-ins can cooperate without referencing each other.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (contracts, namespace `SysWeaver.Chart`) |

## Purpose

A service that can export charts implements `IHaveChartExporters` and returns one or more `IChartExporter` instances. Each exporter declares a menu name (also its unique key), description, icon and sort order, whether a login is required and which input it expects (`ChartExportInputTypes`: a ChartJs configuration object, a PNG data url or an SVG data url).

## Contents

| Item | Description |
|---|---|
| [`IChartExporter.cs`](IChartExporter.cs) | An exporter of charts to some file format (menu metadata and `Export`) |
| [`IHaveChartExporters.cs`](IHaveChartExporters.cs) | Implemented by service objects that expose chart exporters |
| [`ChartExportInputTypes.cs`](ChartExportInputTypes.cs) | The kind of data an exporter expects (ChartJs config, PNG or SVG data url) |
| [`ChartExportOptions.cs`](ChartExportOptions.cs) | Export options: suggested file name and landscape / portrait swap for paged output |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Used by:** [SysWeaver.MicroService.ChartJs](../../SysWeaver.MicroService.ChartJs/README.md), [SysWeaver.MicroService.ChartJs.Excel](../../SysWeaver.MicroService.ChartJs.Excel/README.md)
- **Related:** [Data/](../Data/README.md) (the equivalent contract for table data, `ITableDataExporter`)
