# SysWeaver.WebBrowser.WebView2

[⬆ SysWeaver overview](../README.md)

> Headless Microsoft Edge WebView2 implementation of the browser service.

| | |
|---|---|
| **Layer** | Automation |
| **Kind** | Service (`IBrowserService`) |

## Purpose

An alternative engine to CEF that uses the Edge WebView2 runtime, bundled for x64.

## Limitations and considerations

- Windows x64 only.
- WebView2 needs a synchronization context; calls are proxied to a dedicated thread.

## Relationships

- **Project:** [`SysWeaver.WebBrowser.WebView2.csproj`](SysWeaver.WebBrowser.WebView2.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.WebBrowser](../SysWeaver.WebBrowser/README.md)
