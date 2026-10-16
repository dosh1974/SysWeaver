# Logging

[⬆ SimpleStack.Orm](../README.md)

> A minimal logging abstraction used by SimpleStack.Orm, with a debug-output and a no-op implementation.

| | |
|---|---|
| **Project** | [SimpleStack.Orm](../README.md) (vendored, used by SysWeaver) |
| **Kind** | Source code |

## Purpose

Keeps the core free of a logging framework dependency. An adapter to Microsoft.Extensions.Logging exists in [SimpleStack.Orm.Logging.MicrosoftExtensions](../../SimpleStack.Orm.Logging.MicrosoftExtensions/README.md), but SysWeaver does not use it.

## Contents

| Item | Description |
|---|---|
| [`DebugLoggerFactory.cs`](DebugLoggerFactory.cs) | Logger factory writing to `Debug.WriteLine` |
| [`DummyLoggerFactory.cs`](DummyLoggerFactory.cs) | Logger factory that discards everything |
| [`IDebugLogFilter.cs`](IDebugLogFilter.cs) | Optional logger interface telling whether debug messages are logged, so callers can skip building their arguments |
| [`ILogger.cs`](ILogger.cs) | Generic logger interface `ILogger<T>` |
| [`ILoggerFactory.cs`](ILoggerFactory.cs) | Factory interface for loggers |

## Relationships

- **Part of:** [SimpleStack.Orm](../README.md)
- **Implemented by:** [SimpleStack.Orm.Logging.MicrosoftExtensions](../../SimpleStack.Orm.Logging.MicrosoftExtensions/README.md) (unused)
