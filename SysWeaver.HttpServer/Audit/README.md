# Audit

[⬆ SysWeaver.HttpServer](../README.md)

> Types and tools for searching and reporting audited API calls, see [`IApiAuditReportService`](../IApiAuditReportService.cs).

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../README.md) |
| **Kind** | Source folder |

## Contents

| Item | Description |
|---|---|
| [`ApiAuditEntry.cs`](ApiAuditEntry.cs) | The "fat" entry: a call and its client in one row, and the states of a call |
| [`ApiAuditQuery.cs`](ApiAuditQuery.cs) | The query (time range, search terms, selected values, conditions) and the search, facet and summary requests |
| [`ApiAuditResults.cs`](ApiAuditResults.cs) | The results: information, a page of entries, facets and the summary with a time line |
| [`ApiAuditField.cs`](ApiAuditField.cs) | The fields that can be used in queries |
| [`ApiAuditMatcher.cs`](ApiAuditMatcher.cs) | The interpretation of a query: matches entries in memory (`SimpleAuditService`) or is translated to SQL (`MySqlApiAuditService`) |
| [`ApiAuditTools.cs`](ApiAuditTools.cs) | Paging, order, time line and table helpers for implementations |

## Relationships

- **Part of:** [SysWeaver.HttpServer](../README.md)
- **Used by:** [SysWeaver.MicroService.HttpServer](../../SysWeaver.MicroService.HttpServer/README.md) (`SimpleAuditService`), [SysWeaver.MicroService.MySqlAudit](../../SysWeaver.MicroService.MySqlAudit/README.md), the page [`audit/`](../srcWeb/audit/README.md)
