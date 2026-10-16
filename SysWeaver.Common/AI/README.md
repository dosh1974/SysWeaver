# AI

[⬆ SysWeaver.Common](../README.md)

> The attribute vocabulary and context interface that turn ordinary service methods into AI tools, without the service referencing any AI project.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (contracts and attributes, namespace `SysWeaver.AI`) |

## Purpose

A service marks methods with `[AiTool]` (or `[AiUse]`) and implements the marker interface `IHaveAiTools`; the AI chat services and the MCP service discover these methods at runtime and expose them to language models. The other attributes control the tool name, the JSON schema generated for arguments and return values, and MCP visibility. The attributes only *describe*; they have no effect unless an AI project is deployed.

## Contents

| Item | Description |
|---|---|
| [`AiToolAttribute.cs`](AiToolAttribute.cs) | `AiToolAttribute` (marks a method as a tool, with an icon shown when it is called), `AiUseAttribute` (marks a method as a tool without an icon), the `IHaveAiTools` marker interface and `AiTableRowTypeAttribute` (row type of a returned table data reference) |
| [`AiToolNameAttribute.cs`](AiToolNameAttribute.cs) | Overrides the tool name seen by the model (format string, `{0}` = prefix, `{1}` = method name) |
| [`AiToolPrefixAttribute.cs`](AiToolPrefixAttribute.cs) | Sets the tool name prefix for all tools declared in a type |
| [`AiIgnoreAttribute.cs`](AiIgnoreAttribute.cs) | Excludes a field or property from the generated JSON schema |
| [`AiOptionalAttribute.cs`](AiOptionalAttribute.cs) | Marks a field or property as optional in the generated JSON schema |
| [`AiHideMcpAttribute.cs`](AiHideMcpAttribute.cs) | Hides a tool from MCP clients (for tools that only make sense in an AI chat) |
| [`IAiToolContext.cs`](IAiToolContext.cs) | Context of a tool invocation: add links, attach files and keep per chat session properties |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Used by:** [SysWeaver.AI](../../SysWeaver.AI/README.md), [SysWeaver.AI.McpService](../../SysWeaver.AI.McpService/README.md), [SysWeaver.Chat](../../SysWeaver.Chat/README.md) and services that publish AI tools, e.g. [SysWeaver.MicroService.ChartJs](../../SysWeaver.MicroService.ChartJs/README.md), [SysWeaver.MicroService.Map](../../SysWeaver.MicroService.Map/README.md) (with the annotated parameter classes of [SysWeaver.Map](../../SysWeaver.Map/README.md))
