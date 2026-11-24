# audit

[⬆ srcWeb](../README.md)

> The audit browser: search the audited API calls (who did what, when and from where).

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../../README.md) |
| **Kind** | Embedded web assets |

## Contents

| Item | Description |
|---|---|
| [`audit.html`](audit.html), [`audit.js`](audit.js), [`audit.css`](audit.css) | The page (`AuditBrowser`, entry `auditMain`) |

## Behavior

- Uses the `Api/Audit/...` APIs of the service that implements [`IApiAuditReportService`](../../IApiAuditReportService.cs) (`SimpleAuditService` or `MySqlApiAuditService`, which also add the menu item). The totals, the value lists and the table use the same query.
- **Search box**: words must all be found (in any field), `"a phrase"`, `-exclude`, `field:text` (ex: `user:anna ip:10.0. -state:failed`).
- **Time range**, **Conditions** (any field, all or any must match), **value lists** (select or exclude values) and the active filters as chips.
- **Time line**: completed, failed and in progress calls over time, drag to select a time range.
- **Table**: `Sw.Table` on `AuditTable` (columns, sort, export, live refresh). A click on a row shows the **details**, every value there can be copied or used as a filter.
- The address follows the state (`q` = the state as json), so a search can be bookmarked and shared; `id` shows the details of a call.
- Narrow screens: the value lists are in a dialog (button *Values*).

## Relationships

- **Part of:** [SysWeaver.HttpServer](../../README.md)
- **Uses:** `sw/core.js`, `sw/ui.js`, `explore/table.js`
