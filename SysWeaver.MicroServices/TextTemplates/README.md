# TextTemplates

[⬆ SysWeaver.MicroServices](../README.md)

> Interfaces through which services contribute named variables to the text templates used across the application.

| | |
|---|---|
| **Project** | [SysWeaver.MicroServices](../README.md) |
| **Kind** | Source code |

## Purpose

When a service registered in the service manager implements `IHaveTemplateVariables`, the HTTP server service (`HttpServerServiceBase` in [SysWeaver.MicroService.HttpServer](../../SysWeaver.MicroService.HttpServer/README.md)) adds its variable groups, keyed by group name, to the server's template variable groups. It removes them again when the service is removed. A group name that already exists is ignored with a warning. For example, `StaticVars` in [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md) provides the `Env` and `EnvInfo` groups.

A group (`ITemplateVariableGroup`) returns the value of one key, can enumerate all its key/value pairs, and has an `IsDynamic` flag.

## Contents

| Item | Description |
|---|---|
| [`IHaveTemplateVariables.cs`](IHaveTemplateVariables.cs) | Implemented by services that provide template variables |
| [`ITemplateVariableGroup.cs`](ITemplateVariableGroup.cs) | A group of variables: `IsDynamic`, `GetTemplateVariableValue`, `EnumTemplateVariableValues` |
| [`TemplateVariableGroup.cs`](TemplateVariableGroup.cs) | Implementation of `ITemplateVariableGroup` built from a getter and an enumerator delegate |

## Relationships

- **Part of:** [SysWeaver.MicroServices](../README.md)
- **Used by:** [SysWeaver.MicroService.HttpServer](../../SysWeaver.MicroService.HttpServer/README.md), [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md)
