# Db

[⬆ SysWeaver.MicroService.MySqlAudit](../README.md)

> Database row types (SimpleStack ORM) for the two MySQL tables that hold the API audit trail.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.MySqlAudit](../README.md) |
| **Kind** | Database rows |

## Purpose

[`MySqlApiAuditService`](../MySqlApiAuditService.cs) stores audited calls through these internal (`sealed`, non-public) classes in the `SysWeaver.MicroService.Db` namespace. Attributes on the classes set the table name (`[Alias]`), keys, indices, partitioning and string limits. The `TableData*` attributes control how the *Debug/Audit* tables display the rows: links to the API page, to the active session and to the client, user icons, and JSON formatting.

## Contents

| Item | Description |
|---|---|
| [`DbAuditApiCall.cs`](DbAuditApiCall.cs) | Table `AuditApiCalls`: one row per audited call with API name and group, state (in progress / completed / exception), begin and end time, capped JSON input and output, user, session and client id |
| [`DbAuditApiClient.cs`](DbAuditApiClient.cs) | Table `AuditApiClients`: de-duplicated clients (IP address, device id and other client details) referenced by `ClientId` |

## Relationships

- **Part of:** [SysWeaver.MicroService.MySqlAudit](../README.md)
- **Uses:** [SysWeaver.DbSimpleStack.MySql](../../SysWeaver.DbSimpleStack.MySql/README.md) (ORM attributes), [SysWeaver.Common](../../SysWeaver.Common/README.md) (`TableData*` display attributes in `SysWeaver.Data`)
