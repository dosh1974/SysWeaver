# .vscode

[⬆ simplestack.orm](../README.md)

> Visual Studio Code workspace settings carried along from the upstream SimpleStack.Orm repository.

| | |
|---|---|
| **Library** | [simplestack.orm](../README.md) (vendored) |
| **Kind** | Editor settings |

## Purpose

Defines a single VS Code `build` task that runs `dotnet build` in the library folder. SysWeaver does not use it; the SysWeaver build compiles the vendored projects it needs through `SysWeaver.slnx`.

## Contents

| Item | Description |
|---|---|
| [`tasks.json`](tasks.json) | VS Code task `build` running `dotnet build` with the `$msCompile` problem matcher |

## Relationships

- **Part of:** [simplestack.orm](../README.md), vendored under [_external](../../README.md)
