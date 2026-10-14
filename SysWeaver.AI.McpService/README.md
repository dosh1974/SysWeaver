# SysWeaver.AI.McpService

[⬆ SysWeaver overview](../README.md)

> An MCP (Model Context Protocol) server for SysWeaver: exposes the AI tools of the registered services to MCP clients (agents, IDEs, desktop assistants) over the Streamable HTTP transport.

| | |
|---|---|
| **Layer** | AI |
| **Kind** | Micro service (HTTP server module) |

## Purpose

Let external AI agents use the same tools that the built in AI chat uses. Any method marked `[AiTool]` / `[AiUse]` on a registered service becomes an MCP tool, with its description and input schema generated from the signature and XML documentation by [SysWeaver.AI](../SysWeaver.AI/README.md). No AI provider is needed.

## How it fits into SysWeaver

```mermaid
flowchart LR
  C["MCP client"] -- "JSON-RPC over HTTP POST" --> M["McpService<br/>IHttpServerModule"]
  M -- "tools/call" --> S["service methods with AiTool / AiUse"]
```

The service is an HTTP server module, so the web server picks it up automatically. Authentication uses the server's normal mechanisms (session, `Authorization: Bearer`, `x-api-key` headers); unauthenticated requests get a 401 instead of a login redirect.

## Key features

- Streamable HTTP transport on a single end point (`Url`, default `mcp`): `initialize`, `ping`, `tools/list` and `tools/call`
- Tools of all service instances are found automatically (also services added later), they don't have to implement `IHaveAiTools`; methods marked `[AiHideMcp]` are excluded and tools can also be added or removed in code
- Each tool keeps its own auth (`[WebApiAuth]`, else the service's `Auth`); tools a user may not call are not listed
- Files and links attached by tools (images, maps etc) are returned with the tool result; files are stored in the user's storage if available, otherwise served temporarily under `{Url}/files/`
- Browser origin checking (`AllowedOrigins`) to prevent DNS rebinding, configurable server name and instructions

## Key components

- [`McpService`](McpService.cs) — the HTTP module: JSON-RPC handling, tool registry, tool invocation and attached files
- [`McpToolContext`](McpToolContext.cs) — the tool context used for MCP calls (collects files and links added by tools)
- [`McpParams`](McpParams.cs) — parameters

## Using it

```json
{ "Type": "SysWeaver.AI.McpService, SysWeaver.AI.McpService", "Params": { "Url": "mcp", "Auth": "" } }
```

MCP clients then connect to `https://<server>/mcp`, authenticating with an API key (`Authorization: Bearer <key>`).

## Limitations and considerations

- Stateless: no `Mcp-Session-Id` and no server initiated event stream (GET returns 405); responses are plain JSON
- Tools only: no resources, prompts or sampling, and no tool list change notifications
- Exposing tools lets external agents call service methods on behalf of the authenticated user — set `Auth` and the tools' own auth accordingly, and use `[AiHideMcp]` for tools that only make sense inside the AI chat

## Relationships

- **Project:** [`SysWeaver.AI.McpService.csproj`](SysWeaver.AI.McpService.csproj)
- **Builds on:** [SysWeaver.AI](../SysWeaver.AI/README.md)
