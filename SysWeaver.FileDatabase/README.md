# SysWeaver.FileDatabase

[⬆ SysWeaver overview](../README.md)

> Indexes files and their meta data into a database table and keeps it in sync with the file system.

| | |
|---|---|
| **Layer** | Data |
| **Kind** | Library |

## Purpose

Make large file collections queryable: a service supplies a callback that extracts meta data from each file, and the file database keeps the table in sync with the file system and answers table data requests.

## How it fits into SysWeaver

```mermaid
flowchart LR
  Folders["watched folders"] --> FDB["FileDatabase of T"]
  Extract["your meta data callback"] --> FDB
  FDB --> Db["DbSimpleStack table"]
  Query["TableDataRequest: filters, order, paging"] --> FDB
  FDB --> Result["matching rows / TableData"]
```

## Key features

- `FileDatabase<T>` (T derives from `DbFileData`: full path (primary key), lower case extension, last write time and size as UTC ticks, time of the last db change).
- `Init(folders)` creates the table, adds new files, updates changed ones (other last write time or size) and deletes rows of removed files; the folders are resolved to full paths first (relative ones against the current directory, a root like `C:\` stays a root) and processed parents first, folders that don't exist or are inside an already added folder are ignored with a warning.
- File system watchers (including sub folders) queue changes that are applied every 100 ms; files modified during the last 15 seconds wait until they are older, or until `MarkAsCompleted(file)` is called.
- Optional meta data files: a callback maps a changed file (ex: a sidecar) to the file whose row should be updated.
- Queries with `TableDataRequest` (`GetAsTable`, `GetFiltered`, `GetFilteredWithParams`).
- Statistics (insert / update / delete counts and failures) and performance monitoring integrated with the service manager.

## Limitations and considerations

- Requires a database back-end through [SysWeaver.DbSimpleStack](../SysWeaver.DbSimpleStack/README.md).
- Meta data quality depends entirely on the supplied extraction callback.
- Paths longer than 768 characters and extensions longer than 16 characters are skipped (counted as length failures).
- `Init` can only be called once: a second call (also after a failed one) throws an `InvalidOperationException`.
- `FindInFileDatabase` and the `FindFilter*` types are request / response data types only, nothing in this library uses them.

## Relationships

- **Project:** [`SysWeaver.FileDatabase.csproj`](SysWeaver.FileDatabase.csproj)
- **Builds on:** [SysWeaver.DbSimpleStack](../SysWeaver.DbSimpleStack/README.md)
