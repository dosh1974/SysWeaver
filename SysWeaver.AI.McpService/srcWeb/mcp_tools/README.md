# mcp_tools

[⬆ srcWeb](../README.md)

> The MCP debug page shown under *Admin / MCP debug*: choose API's and AI tools of the running service and start a temporary MCP server with them, so that an AI agent can analyze (or use) the service.

| | |
|---|---|
| **Project** | [SysWeaver.AI.McpService](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

`McpService` adds `mcp_tools/index.html` to the menu as *Admin / MCP debug* (auth `Roles.Admin`). The page selects what the debug MCP server exposes, starts and stops it, and shows its API key, its tool calls and its tools.

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | The page (new format: `sw/core.js`, `sw/ui.js`, `explore/table.js`), `<body onload="mcpToolsMain()">` |
| [`mcp_tools.js`](mcp_tools.js) | `mcpToolsMain()` |
| [`mcp_tools.css`](mcp_tools.css) | `.mcpd-select` (the *Expose* card: search bar, summary, `details.mcpd-svc` services with `.mcpd-group` / `.mcpd-method` rows), `.mcpd-conn` / `.mcpd-rows` (url, redacted credentials with copy buttons, status and exposed services of the connection card), `.mcpd-table` (the live table containers) |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.AI.McpService](../../README.md)
- **Uses:** Sw core / ui of SysWeaver.HttpServer, `explore/table.js` of SysWeaver.HttpServer.ExploreModule.
- **Server API:** `Api/GetDebugMcpServices`, `Api/StartDebugMcp`, `Api/CheckDebugMcp`, `Api/RemoveTempServer`, `Api/CallLogTable`, `Api/ToolTable`.

## Behavior

1. The header has *Start debug* and *Stop debug*. *Start debug* is disabled while nothing is selected or while a debug server runs, *Stop debug* while none runs; both say why.
2. While no debug server runs, the *Expose* card lists the services of `GetDebugMcpServices` as collapsible rows: name (and instance name), type, a *Monitoring* badge for the default services and the number of selected methods. Inside, the *API's* and *AI tools* groups list each method with a check box, its tool name, the auth it normally requires and its description. The service and group check boxes select or clear all of it (mixed when only some are selected).
3. The search box filters the services and methods (service name or type: all its methods; else tool name, method name or description) and opens the services that only match by a method. *Defaults* selects all API's of the default services and no AI tools, *All* and *None* select or clear what is shown. The summary says how many tools from how many services are selected.
4. The selection is kept in the local storage (`SysWeaver.McpDebug.Selection`, methods that no longer exist are dropped); without one the defaults are selected.
5. *Start debug* calls `StartDebugMcp` with the services that have something selected (`{ Services: [{ TypeName, InstanceName, WebApis, AiTools }] }`). The guid, url and API key are kept in the session storage (`McpDebug`), so a reload keeps the state. On load a stored server is checked with `CheckDebugMcp` and forgotten if it's gone.
6. While running, the *Expose* card is hidden and the *Connection* card shows the full url of the server, the API key and the `Authorization: Bearer` header (both redacted, only the last 4 characters shown), each with a copy button that copies the real value, *Copy config* (an MCP client configuration: `{"mcpServers": {"sysweaver-debug": {"type": "http", "url": ..., "headers": {"Authorization": "Bearer <key>"}}}}`), the status line (tools, calls, errors, time to expiration, last error, warnings) and the exposed services. It's refreshed every 10 s; the tables are updated when the number of calls changed. When the server is gone (expired or stopped elsewhere) the page goes back to the stopped state.
7. *Stop debug* calls `RemoveTempServer` (the API key stops working at once).
8. The *Calls* (`CallLogTable`) and *Tools* (`ToolTable`) tables use the guid of the running debug server as `Param`, or `null` (the main MCP end point) when none runs.
