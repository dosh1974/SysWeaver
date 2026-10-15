# SimpleStack.Orm.Logging.MicrosoftExtensions

[⬆ src](../README.md)

> Adapter that routes SimpleStack.Orm logging to Microsoft.Extensions.Logging; not used by SysWeaver.

| | |
|---|---|
| **Project** | SimpleStack.Orm.Logging.MicrosoftExtensions ([`SimpleStack.Orm.Logging.MicrosoftExtensions.csproj`](SimpleStack.Orm.Logging.MicrosoftExtensions.csproj)) |
| **Kind** | Vendored library project (.NET Standard 2.0) |
| **Used by SysWeaver** | No, not in `SysWeaver.slnx` and not referenced by any SysWeaver project; carried along unused |

## Purpose

Implements the logging interfaces of the core ([`Logging/`](../SimpleStack.Orm/Logging/README.md)) on top of a Microsoft.Extensions.Logging `ILoggerFactory`.

## Contents

| Item | Description |
|---|---|
| [`MsLoggerFactory.cs`](MsLoggerFactory.cs) | `MsLoggerFactory` and `MsLogger<T>` (also implements `IDebugLogFilter`) |
| [`SimpleStack.Orm.Logging.MicrosoftExtensions.csproj`](SimpleStack.Orm.Logging.MicrosoftExtensions.csproj) | Project file (`netstandard2.0`, packages `Microsoft.Extensions.Logging` and `.Abstractions`) |

## Relationships

- **Part of:** [simplestack.orm](../../README.md), vendored under [_external](../../../README.md)
- **Uses:** [SimpleStack.Orm](../SimpleStack.Orm/README.md)
