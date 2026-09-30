# SysWeaver.WebBrowser.Cef

[⬆ SysWeaver overview](../README.md)

> Headless Chromium (CEF, off-screen) implementation of the browser service.

| | |
|---|---|
| **Layer** | Automation |
| **Kind** | Service (`IBrowserService`) |

## Purpose

Render and script web pages without a visible window — screenshots, thumbnails, automation.

## Limitations and considerations

- The pinned native Chromium runtimes are Windows builds.
- CEF is large and memory hungry; all calls are marshalled to a dedicated thread.
- The project references the MySQL audit service, pulling database dependencies into consumers.

## Relationships

- **Project:** [`SysWeaver.WebBrowser.Cef.csproj`](SysWeaver.WebBrowser.Cef.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.MicroService.MySqlAudit](../SysWeaver.MicroService.MySqlAudit/README.md), [SysWeaver.WebBrowser](../SysWeaver.WebBrowser/README.md)
