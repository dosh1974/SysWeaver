# SysWeaver.MicroService.ChartJs.Excel

[⬆ SysWeaver overview](../README.md)

> Chart export to Excel workbooks, with native Excel charts.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Plug-in (chart exporter provider) |

## Purpose

Adds "save as Excel workbook" to charts produced by [SysWeaver.MicroService.ChartJs](../SysWeaver.MicroService.ChartJs/README.md). The chart configuration is turned into a workbook with a native Excel chart on one sheet and the underlying data as a table on another, built on [SysWeaver.Excel](../SysWeaver.Excel/README.md).

## How it fits into SysWeaver

`ChartJsExcelService` implements `IHaveChartExporters`; `ChartJsService` picks up its exporters automatically when the service is registered and lists them in the chart page's export menu.

## Key components

- [`ChartJsExcelService`](ChartJsExcelService.cs) — the service registered in the manifest
- [`ChartJsExcelExporter`](ChartJsExcelExporter.cs) — converts a `ChartJsConfig` into an `.xlsx` with a bar, column, line, pie or doughnut chart (other types fall back to bar), keeping dataset colours
- [`ChartJsPdfExporter`](ChartJsPdfExporter.cs) — converts the chart's SVG rendering into a PDF; present but currently not registered by the service

## Using it

```json
{
  "Type": "SysWeaver.MicroService.ChartJsExcelService, SysWeaver.MicroService.ChartJs.Excel"
}
```

## Limitations and considerations

- Only the Excel exporter is active; the PDF exporter is disabled in `ChartJsExcelService`, so `ChartMarginInch` currently has no effect (the Excel exporter uses `TableMarginInch` from `ExcelService`).
- The service does not set the GemBox license itself; register `SysWeaver.Excel.ExcelService` as well (see [SysWeaver.Excel](../SysWeaver.Excel/README.md) for the licensing constraints).

## Relationships

- **Project:** [`SysWeaver.MicroService.ChartJs.Excel.csproj`](SysWeaver.MicroService.ChartJs.Excel.csproj)
- **Builds on:** [SysWeaver.Excel](../SysWeaver.Excel/README.md), [SysWeaver.MicroService.ChartJs](../SysWeaver.MicroService.ChartJs/README.md), [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
- **Used by:** [ExchangeRateService](https://github.com/dosh1974/SysWeaver.Services/blob/main/ExchangeRateService/README.md), [FakeEmailService](https://github.com/dosh1974/SysWeaver.Services/blob/main/FakeEmailService/README.md), [FakeTextService](https://github.com/dosh1974/SysWeaver.Services/blob/main/FakeTextService/README.md), [IpLocationService](https://github.com/dosh1974/SysWeaver.Services/blob/main/IpLocationService/README.md)
- **Third-party:** GemBox.Spreadsheet (through SysWeaver.Excel)
