# srcWeb

[⬆ SysWeaver.AI.McpService](../README.md)

> Source of the embedded MCP debug page.

| | |
|---|---|
| **Project** | [SysWeaver.AI.McpService](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

This folder only contains the `mcp_tools/` sub folder. The `EmbedLocal` target in [`SysWeaver.AI.McpService.csproj`](../SysWeaver.AI.McpService.csproj) runs `Compress.exe -Methods br .\srcWeb\*+ .\web` before each build. The generated `web/**/*.br` files (not in git) are embedded as resources and served as `mcp_tools/...` relative to the site root.

## Contents

| Item | Description |
|---|---|
| [`mcp_tools/`](mcp_tools/README.md) | The *Admin / MCP debug* page |

## Relationships

- **Part of:** [SysWeaver.AI.McpService](../README.md)
